using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using VFXComposer.Protocol.Hashing;

namespace VFXComposer.Broker.Security;

/// <summary>
/// Immutable canonical Windows SID material used only for host-owned policy and
/// OS-observed peer facts. It is never accepted from a wire DTO or caller path.
/// </summary>
internal sealed class WindowsSid
{
    internal const string UserIdentityType = "vfxcomposer.windows-user-sid/1";

    private readonly byte[] _binary;

    private WindowsSid(
        string canonicalValue,
        WindowsSidPrincipalKind principalKind,
        byte[] binary)
    {
        CanonicalValue = canonicalValue;
        PrincipalKind = principalKind;
        _binary = binary;
        UserIdentityHash = TypedHash.Compute(UserIdentityType, binary);
    }

    public string CanonicalValue { get; }

    public WindowsSidPrincipalKind PrincipalKind { get; }

    internal TypedHash UserIdentityHash { get; }

    internal static WindowsSid ParseService(string value) =>
        Parse(value, WindowsSidPrincipalKind.Service);

    internal static WindowsSid ParseUser(string value) =>
        Parse(value, WindowsSidPrincipalKind.User);

    internal static WindowsSid FromBinary(
        ReadOnlySpan<byte> binary,
        WindowsSidPrincipalKind expectedKind)
    {
        if (binary.Length < 12)
        {
            throw new ArgumentException("Windows SID binary form is too short.", nameof(binary));
        }

        var revision = binary[0];
        var subAuthorityCount = binary[1];
        var expectedLength = checked(8 + (subAuthorityCount * sizeof(uint)));
        if (subAuthorityCount is 0 or > 15 || binary.Length != expectedLength)
        {
            throw new ArgumentException("Windows SID binary form is invalid.", nameof(binary));
        }

        ulong authority = 0;
        for (var index = 0; index < 6; index++)
        {
            authority = (authority << 8) | binary[2 + index];
        }

        var components = new string[subAuthorityCount + 3];
        components[0] = "S";
        components[1] = revision.ToString(CultureInfo.InvariantCulture);
        components[2] = authority.ToString(CultureInfo.InvariantCulture);
        for (var index = 0; index < subAuthorityCount; index++)
        {
            var value = BinaryPrimitives.ReadUInt32LittleEndian(
                binary.Slice(8 + (index * sizeof(uint)), sizeof(uint)));
            components[index + 3] = value.ToString(CultureInfo.InvariantCulture);
        }

        return Parse(string.Join('-', components), expectedKind);
    }

    internal bool FixedEquals(WindowsSid? other) =>
        other is not null &&
        PrincipalKind == other.PrincipalKind &&
        CryptographicOperations.FixedTimeEquals(_binary, other._binary);

    private static WindowsSid Parse(string value, WindowsSidPrincipalKind expectedKind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var parts = value.Split('-', StringSplitOptions.None);
        if (parts.Length is < 4 or > 18 || !string.Equals(parts[0], "S", StringComparison.Ordinal))
        {
            throw new ArgumentException("Windows SID text is not canonical.", nameof(value));
        }

        var revision = ParseByte(parts[1], nameof(value));
        var authority = ParseAuthority(parts[2], nameof(value));
        var subAuthorities = new uint[parts.Length - 3];
        for (var index = 3; index < parts.Length; index++)
        {
            subAuthorities[index - 3] = ParseUInt32(parts[index], nameof(value));
        }

        var canonical = BuildCanonicalText(revision, authority, subAuthorities);
        if (!string.Equals(value, canonical, StringComparison.Ordinal))
        {
            throw new ArgumentException("Windows SID text is not canonical.", nameof(value));
        }

        ValidatePrincipalKind(revision, authority, subAuthorities, expectedKind, nameof(value));
        return new WindowsSid(canonical, expectedKind, BuildBinary(revision, authority, subAuthorities));
    }

    private static byte ParseByte(string value, string parameterName)
    {
        if (!byte.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) ||
            !string.Equals(value, parsed.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal))
        {
            throw new ArgumentException("Windows SID component is not canonical.", parameterName);
        }

        return parsed;
    }

    private static ulong ParseAuthority(string value, string parameterName)
    {
        if (!ulong.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) ||
            parsed > 0x0000FFFFFFFFFFFFUL ||
            !string.Equals(value, parsed.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal))
        {
            throw new ArgumentException("Windows SID authority is not canonical.", parameterName);
        }

        return parsed;
    }

    private static uint ParseUInt32(string value, string parameterName)
    {
        if (!uint.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) ||
            !string.Equals(value, parsed.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal))
        {
            throw new ArgumentException("Windows SID sub-authority is not canonical.", parameterName);
        }

        return parsed;
    }

    private static string BuildCanonicalText(
        byte revision,
        ulong authority,
        IReadOnlyList<uint> subAuthorities)
    {
        var components = new string[subAuthorities.Count + 3];
        components[0] = "S";
        components[1] = revision.ToString(CultureInfo.InvariantCulture);
        components[2] = authority.ToString(CultureInfo.InvariantCulture);
        for (var index = 0; index < subAuthorities.Count; index++)
        {
            components[index + 3] = subAuthorities[index].ToString(CultureInfo.InvariantCulture);
        }

        return string.Join('-', components);
    }

    private static byte[] BuildBinary(
        byte revision,
        ulong authority,
        IReadOnlyList<uint> subAuthorities)
    {
        var binary = new byte[checked(8 + (subAuthorities.Count * sizeof(uint)))];
        binary[0] = revision;
        binary[1] = checked((byte)subAuthorities.Count);
        for (var index = 0; index < 6; index++)
        {
            binary[7 - index] = (byte)(authority >> (index * 8));
        }

        for (var index = 0; index < subAuthorities.Count; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(
                binary.AsSpan(8 + (index * sizeof(uint)), sizeof(uint)),
                subAuthorities[index]);
        }

        return binary;
    }

    private static void ValidatePrincipalKind(
        byte revision,
        ulong authority,
        IReadOnlyList<uint> subAuthorities,
        WindowsSidPrincipalKind expectedKind,
        string parameterName)
    {
        var valid = expectedKind switch
        {
            WindowsSidPrincipalKind.Service =>
                revision == 1 && authority == 5 && subAuthorities.Count == 6 &&
                subAuthorities[0] == 80,
            WindowsSidPrincipalKind.User =>
                revision == 1 &&
                ((authority == 5 && subAuthorities.Count == 5 && subAuthorities[0] == 21) ||
                 (authority == 12 && subAuthorities.Count == 5 && subAuthorities[0] == 1)),
            _ => false,
        };
        if (!valid)
        {
            throw new ArgumentException("Windows SID does not match the required principal kind.", parameterName);
        }
    }
}

internal enum WindowsSidPrincipalKind
{
    Service,
    User,
}

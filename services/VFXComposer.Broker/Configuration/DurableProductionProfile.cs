using System.Buffers;
using System.Buffers.Binary;
using System.Globalization;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Text;
using VFXComposer.Broker.Security;
using VFXComposer.Protocol.Hashing;

namespace VFXComposer.Broker.Configuration;

/// <summary>
/// Immutable, non-wire requirements for one dormant durable-store profile.
/// The caller must supply the already-pinned directory capability; this type
/// neither discovers a location nor provisions a Windows security principal.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class DurableProductionProfile
{
    internal const string ProfileDigestType = "vfxcomposer.durable-production-profile/1";
    internal const string RootProtectionDigestType = "vfxcomposer.durable-production-root-protection/1";
    internal const string StoreFileProtectionDigestType = "vfxcomposer.durable-production-store-file-protection/1";
    internal const uint CanonicalVersion = 2;
    internal const uint RootAccessMask = 0x001200A2;
    internal const uint StoreFileAccessMask = 0x00130083;

    private const uint RequiredRootControlFlags = 0x1404; // DACL present, auto-inherited, protected.
    private const uint RequiredStoreFileControlFlags = 0x1004; // DACL present, protected.
    private readonly byte[] _canonicalBytes;
    private readonly byte[] _rootCanonicalBytes;
    private readonly byte[] _storeFileCanonicalBytes;
    private readonly byte[] _rootSecurityDescriptorBytes;
    private readonly byte[] _storeFileSecurityDescriptorBytes;

    internal DurableProductionProfile(string profileId, WindowsSid serviceSid)
    {
        ProfileId = RequireProfileId(profileId);
        ServiceSid = serviceSid ?? throw new ArgumentNullException(nameof(serviceSid));
        if (ServiceSid.PrincipalKind != WindowsSidPrincipalKind.Service)
        {
            throw new ArgumentException("The durable profile principal must be an exact service SID.", nameof(serviceSid));
        }

        CanonicalRootSecurityDescriptor = BuildCanonicalSecurityDescriptor(
            ServiceSid,
            RootAccessMask,
            includeAutoInheritedControl: true);
        CanonicalStoreFileSecurityDescriptor = BuildCanonicalSecurityDescriptor(
            ServiceSid,
            StoreFileAccessMask,
            includeAutoInheritedControl: false);

        RawSecurityDescriptor rootDescriptor;
        RawSecurityDescriptor storeFileDescriptor;
        try
        {
            rootDescriptor = new RawSecurityDescriptor(CanonicalRootSecurityDescriptor);
            storeFileDescriptor = new RawSecurityDescriptor(CanonicalStoreFileSecurityDescriptor);
        }
        catch (Exception exception) when (exception is ArgumentException or OverflowException)
        {
            throw new ArgumentException("The durable-store security descriptor is invalid.", nameof(serviceSid), exception);
        }

        _rootSecurityDescriptorBytes = GetBinaryForm(rootDescriptor);
        _storeFileSecurityDescriptorBytes = GetBinaryForm(storeFileDescriptor);
        var binaryRootDescriptor = new RawSecurityDescriptor(_rootSecurityDescriptorBytes, 0);
        var binaryStoreFileDescriptor = new RawSecurityDescriptor(_storeFileSecurityDescriptorBytes, 0);
        if (!MatchesExpectedRootSecurityDescriptor(binaryRootDescriptor) ||
            !MatchesExpectedStoreFileSecurityDescriptor(binaryStoreFileDescriptor))
        {
            throw new ArgumentException("The durable-store security descriptor is not exact.", nameof(serviceSid));
        }

        _rootCanonicalBytes = BuildProtectionBytes(
            "vfxcomposer.durable-production-profile.root/1",
            RootAccessMask,
            RequiredRootControlFlags,
            CanonicalRootSecurityDescriptor);
        _storeFileCanonicalBytes = BuildProtectionBytes(
            "vfxcomposer.durable-production-profile.store-file/1",
            StoreFileAccessMask,
            RequiredStoreFileControlFlags,
            CanonicalStoreFileSecurityDescriptor);
        RootProtectionDigest = TypedHash.Compute(RootProtectionDigestType, _rootCanonicalBytes);
        StoreFileProtectionDigest = TypedHash.Compute(StoreFileProtectionDigestType, _storeFileCanonicalBytes);
        _canonicalBytes = BuildCanonicalBytes();
        ProfileDigest = TypedHash.Compute(ProfileDigestType, _canonicalBytes);
    }

    internal string ProfileId { get; }

    internal WindowsSid ServiceSid { get; }

    /// <summary>Exact protected root descriptor: one service-SID allow ACE only.</summary>
    internal string CanonicalRootSecurityDescriptor { get; }

    /// <summary>Exact protected store-file descriptor: one service-SID allow ACE only.</summary>
    internal string CanonicalStoreFileSecurityDescriptor { get; }

    internal TypedHash RootProtectionDigest { get; }

    internal TypedHash StoreFileProtectionDigest { get; }

    internal TypedHash ProfileDigest { get; }

    internal byte[] GetCanonicalBytes() => _canonicalBytes.ToArray();

    internal byte[] GetCanonicalRootBytes() => _rootCanonicalBytes.ToArray();

    internal byte[] GetCanonicalStoreFileBytes() => _storeFileCanonicalBytes.ToArray();

    internal byte[] GetRootSecurityDescriptorBytes() => _rootSecurityDescriptorBytes.ToArray();

    internal byte[] GetStoreFileSecurityDescriptorBytes() => _storeFileSecurityDescriptorBytes.ToArray();

    /// <summary>
    /// Tests an observed root descriptor for the complete security fact: exact
    /// owner/group, absent SACL, protected DACL control, and its sole ordered
    /// allow ACE. Broad, deny, inherited, callback, opaque, or duplicate ACEs
    /// therefore cannot be accepted as the durable root profile.
    /// </summary>
    internal bool MatchesExpectedRootSecurityDescriptor(RawSecurityDescriptor? descriptor) =>
        MatchesExpectedSecurityDescriptor(descriptor, RequiredRootControlFlags, RootAccessMask);

    internal bool MatchesExpectedStoreFileSecurityDescriptor(RawSecurityDescriptor? descriptor) =>
        MatchesExpectedSecurityDescriptor(descriptor, RequiredStoreFileControlFlags, StoreFileAccessMask);

    private bool MatchesExpectedSecurityDescriptor(
        RawSecurityDescriptor? descriptor,
        uint requiredControlFlags,
        uint requiredAccessMask)
    {
        if (descriptor is null ||
            descriptor.Owner is null ||
            descriptor.Group is null ||
            descriptor.SystemAcl is not null ||
            descriptor.DiscretionaryAcl is null ||
            !string.Equals(descriptor.Owner.Value, ServiceSid.CanonicalValue, StringComparison.Ordinal) ||
            !string.Equals(descriptor.Group.Value, ServiceSid.CanonicalValue, StringComparison.Ordinal))
        {
            return false;
        }

        var flags = (uint)descriptor.ControlFlags;
        if (flags != ((uint)ControlFlags.SelfRelative | requiredControlFlags) ||
            (flags & (uint)ControlFlags.SystemAclPresent) != 0 ||
            descriptor.DiscretionaryAcl.Count != 1)
        {
            return false;
        }

        return MatchesExpectedServiceAllowAce(descriptor.DiscretionaryAcl[0], requiredAccessMask);
    }

    private bool MatchesExpectedServiceAllowAce(GenericAce actual, uint requiredAccessMask)
    {
        return actual is CommonAce common &&
            common.AceType == AceType.AccessAllowed &&
            common.AceFlags == AceFlags.None &&
            !common.IsInherited &&
            common.OpaqueLength == 0 &&
            common.AccessMask == unchecked((int)requiredAccessMask) &&
            common.SecurityIdentifier is { } sid &&
            string.Equals(sid.Value, ServiceSid.CanonicalValue, StringComparison.Ordinal);
    }

    private byte[] BuildCanonicalBytes()
    {
        var writer = new ArrayBufferWriter<byte>(384);
        AppendAscii(writer, "vfxcomposer.durable-production-profile.canonical/2");
        AppendUInt32(writer, CanonicalVersion);
        AppendUtf8(writer, ProfileId);
        AppendUtf8(writer, ServiceSid.CanonicalValue);
        AppendTypedHash(writer, RootProtectionDigest);
        AppendTypedHash(writer, StoreFileProtectionDigest);
        return writer.WrittenSpan.ToArray();
    }

    private byte[] BuildProtectionBytes(
        string domain,
        uint accessMask,
        uint controlFlags,
        string descriptor)
    {
        var writer = new ArrayBufferWriter<byte>(256);
        AppendAscii(writer, domain);
        AppendUInt32(writer, CanonicalVersion);
        AppendUtf8(writer, ProfileId);
        AppendUtf8(writer, ServiceSid.CanonicalValue);
        AppendUInt32(writer, controlFlags);
        AppendUInt32(writer, accessMask);
        AppendUInt32(writer, 0); // exact absent SACL
        AppendUInt32(writer, 1); // exact sole DACL ACE
        writer.GetSpan(1)[0] = (byte)DurableProfileAceKind.Allow;
        writer.Advance(1);
        writer.GetSpan(1)[0] = 0; // ACE flags are exactly none.
        writer.Advance(1);
        AppendUInt32(writer, accessMask);
        AppendUtf8(writer, ServiceSid.CanonicalValue);
        AppendUtf8(writer, descriptor);
        return writer.WrittenSpan.ToArray();
    }

    private static byte[] GetBinaryForm(RawSecurityDescriptor descriptor)
    {
        var bytes = new byte[descriptor.BinaryLength];
        descriptor.GetBinaryForm(bytes, 0);
        return bytes;
    }

    private static string BuildCanonicalSecurityDescriptor(
        WindowsSid serviceSid,
        uint accessMask,
        bool includeAutoInheritedControl)
    {
        return string.Concat(
            "O:", serviceSid.CanonicalValue,
            "G:", serviceSid.CanonicalValue,
            includeAutoInheritedControl ? "D:PAI" : "D:P",
            "(A;;0x", accessMask.ToString("x8", CultureInfo.InvariantCulture), ";;;",
            serviceSid.CanonicalValue,
            ")");
    }

    private static string RequireProfileId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value.Length is > 96 ||
            value.Any(character => character is not (>= 'a' and <= 'z') and
                not (>= '0' and <= '9') and not '.' and not '_' and not '-'))
        {
            throw new ArgumentException("The durable profile identifier is not canonical.", nameof(value));
        }

        return value;
    }

    private static void AppendTypedHash(ArrayBufferWriter<byte> writer, TypedHash hash)
    {
        AppendUtf8(writer, hash.TypeTag);
        AppendUtf8(writer, hash.Digest);
    }

    private static void AppendAscii(ArrayBufferWriter<byte> writer, string value)
    {
        var bytes = Encoding.ASCII.GetBytes(value);
        try
        {
            AppendUInt32(writer, checked((uint)bytes.Length));
            writer.Write(bytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    private static void AppendUtf8(ArrayBufferWriter<byte> writer, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        try
        {
            AppendUInt32(writer, checked((uint)bytes.Length));
            writer.Write(bytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    private static void AppendUInt32(ArrayBufferWriter<byte> writer, uint value)
    {
        var destination = writer.GetSpan(sizeof(uint));
        BinaryPrimitives.WriteUInt32BigEndian(destination, value);
        writer.Advance(sizeof(uint));
    }
}

internal enum DurableProfileAceKind : byte
{
    Allow = 1,
}

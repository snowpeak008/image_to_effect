using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VFXComposer.Protocol.Hashing;

/// <summary>
/// SHA-256 over a fixed domain, a UTF-8 type tag and the payload, all with explicit
/// big-endian lengths. A digest for one type cannot be replayed as another type.
/// </summary>
public sealed record TypedHash
{
    public const string EncodingName = "vfxcomposer.typed-sha256.length-prefixed/1";
    private const string DigestPrefix = "sha256:";
    private static readonly byte[] Domain = Encoding.ASCII.GetBytes(
        "vfxcomposer.typed-sha256.length-prefixed/1\0");

    [JsonConstructor]
    public TypedHash(string typeTag, string digest)
    {
        TypeTag = ValidateTypeTag(typeTag);
        Digest = ValidateDigest(digest);
    }

    [JsonPropertyName("typeTag")]
    public string TypeTag { get; }

    [JsonPropertyName("digest")]
    public string Digest { get; }

    public static TypedHash Compute(string typeTag, ReadOnlySpan<byte> payload)
    {
        var validatedTypeTag = ValidateTypeTag(typeTag);
        var typeBytes = Encoding.UTF8.GetBytes(validatedTypeTag);
        Span<byte> typeLength = stackalloc byte[sizeof(uint)];
        Span<byte> payloadLength = stackalloc byte[sizeof(ulong)];
        BinaryPrimitives.WriteUInt32BigEndian(typeLength, checked((uint)typeBytes.Length));
        BinaryPrimitives.WriteUInt64BigEndian(payloadLength, checked((ulong)payload.Length));

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(Domain);
        hash.AppendData(typeLength);
        hash.AppendData(typeBytes);
        hash.AppendData(payloadLength);
        hash.AppendData(payload);
        return new TypedHash(validatedTypeTag, DigestPrefix + Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant());
    }

    public static TypedHash ComputeUtf8(string typeTag, string payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        var strictUtf8 = new UTF8Encoding(
            encoderShouldEmitUTF8Identifier: false,
            throwOnInvalidBytes: true);
        return Compute(typeTag, strictUtf8.GetBytes(payload));
    }

    public static TypedHash FromJson(JsonElement element)
    {
        Json.ExactObjectValidator.Validate(element, ["typeTag", "digest"]);
        return new TypedHash(
            Json.ExactObjectValidator.RequireString(element, "typeTag"),
            Json.ExactObjectValidator.RequireString(element, "digest"));
    }

    public bool FixedTimeEquals(TypedHash? other)
    {
        if (other is null || !string.Equals(TypeTag, other.TypeTag, StringComparison.Ordinal))
        {
            return false;
        }

        var left = Convert.FromHexString(Digest[DigestPrefix.Length..]);
        var right = Convert.FromHexString(other.Digest[DigestPrefix.Length..]);
        return CryptographicOperations.FixedTimeEquals(left, right);
    }

    private static string ValidateTypeTag(string typeTag)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(typeTag);
        if (typeTag.Length is < 3 or > 96 || typeTag[0] is < 'a' or > 'z')
        {
            throw new ArgumentException("Type tag has an invalid shape.", nameof(typeTag));
        }

        var slashCount = 0;
        for (var index = 0; index < typeTag.Length; index++)
        {
            var character = typeTag[index];
            if (character == '/')
            {
                slashCount++;
                if (index == 0 || index == typeTag.Length - 1)
                {
                    throw new ArgumentException("Type tag has an invalid version separator.", nameof(typeTag));
                }

                continue;
            }

            if (character is not (>= 'a' and <= 'z') and
                not (>= '0' and <= '9') and
                not '.' and not '_' and not '-')
            {
                throw new ArgumentException("Type tag contains an invalid character.", nameof(typeTag));
            }
        }

        if (slashCount != 1)
        {
            throw new ArgumentException("Type tag must contain exactly one version separator.", nameof(typeTag));
        }

        return typeTag;
    }

    private static string ValidateDigest(string digest)
    {
        ArgumentNullException.ThrowIfNull(digest);
        if (digest.Length != DigestPrefix.Length + 64 ||
            !digest.StartsWith(DigestPrefix, StringComparison.Ordinal))
        {
            throw new ArgumentException("Digest is not canonical SHA-256 text.", nameof(digest));
        }

        foreach (var character in digest.AsSpan(DigestPrefix.Length))
        {
            if (character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f'))
            {
                throw new ArgumentException("Digest is not canonical SHA-256 text.", nameof(digest));
            }
        }

        return digest;
    }
}

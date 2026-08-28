using System.Text.Json;
using VFXComposer.Protocol.Json;

namespace VFXComposer.Protocol.Hashing;

/// <summary>Computes and verifies a typed canonical hash excluding one root field.</summary>
public static class SelfHash
{
    public const string DefaultPropertyName = "selfHash";

    public static TypedHash Compute(
        ReadOnlySpan<byte> utf8Json,
        string typeTag,
        string selfHashPropertyName = DefaultPropertyName,
        StrictJsonLimits? limits = null)
    {
        using var document = StrictJsonReader.Parse(utf8Json, limits);
        return ComputeElement(document.RootElement, typeTag, selfHashPropertyName);
    }

    internal static TypedHash ComputeElement(
        JsonElement root,
        string typeTag,
        string selfHashPropertyName = DefaultPropertyName)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new StrictJsonException("EXPECTED_OBJECT", "A self-hashed value must be an object.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(selfHashPropertyName);
        return TypedHash.Compute(typeTag, CanonicalJson.Encode(root, selfHashPropertyName));
    }

    public static bool Verify(
        ReadOnlySpan<byte> utf8Json,
        string typeTag,
        string selfHashPropertyName = DefaultPropertyName,
        StrictJsonLimits? limits = null)
    {
        using var document = StrictJsonReader.Parse(utf8Json, limits);
        return VerifyElement(document.RootElement, typeTag, selfHashPropertyName);
    }

    internal static bool VerifyElement(
        JsonElement root,
        string typeTag,
        string selfHashPropertyName = DefaultPropertyName)
    {
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty(selfHashPropertyName, out var claimedElement) ||
            claimedElement.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        TypedHash claimed;
        try
        {
            claimed = TypedHash.FromJson(claimedElement);
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (StrictJsonException)
        {
            return false;
        }

        var computed = ComputeElement(root, typeTag, selfHashPropertyName);
        return string.Equals(claimed.TypeTag, typeTag, StringComparison.Ordinal) &&
               computed.FixedTimeEquals(claimed);
    }
}

using System.Collections.ObjectModel;
using VFXComposer.Protocol.Hashing;

namespace VFXComposer.Protocol;

internal static class WireModelGuard
{
    public static int PositiveInt32(int value, string parameterName)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }

        return value;
    }

    public static long NonNegativeInt64(long value, string parameterName)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }

        return value;
    }

    public static IReadOnlyList<string> KnownSortedTokens(
        IEnumerable<string> values,
        IReadOnlySet<string> known,
        string parameterName,
        int maximumCount = 32)
    {
        ArgumentNullException.ThrowIfNull(values, parameterName);
        var result = values.Select(value => Guard.Token(value, parameterName)).ToArray();
        if (result.Length > maximumCount ||
            result.Any(value => !known.Contains(value)) ||
            result.Distinct(StringComparer.Ordinal).Count() != result.Length ||
            !result.SequenceEqual(result.Order(StringComparer.Ordinal), StringComparer.Ordinal))
        {
            throw new ArgumentException(
                "The token set must be known, unique, bounded, and ordinal-sorted.",
                parameterName);
        }

        return new ReadOnlyCollection<string>(result);
    }

    public static TypedHash TypedHash(
        TypedHash value,
        string expectedTypeTag,
        string parameterName)
    {
        ArgumentNullException.ThrowIfNull(value, parameterName);
        if (!string.Equals(value.TypeTag, expectedTypeTag, StringComparison.Ordinal))
        {
            throw new ArgumentException("Typed hash uses the wrong domain.", parameterName);
        }

        return value;
    }
}

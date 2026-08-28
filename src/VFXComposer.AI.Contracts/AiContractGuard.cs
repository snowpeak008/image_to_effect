using System.Collections.ObjectModel;

namespace VFXComposer.AI.Contracts;

internal static class AiContractGuard
{
    public static string Identifier(string value, string parameterName, int maximumLength = 64)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (value.Length > maximumLength || value[0] is < 'a' or > 'z')
        {
            throw new ArgumentException("Identifier is invalid.", parameterName);
        }

        foreach (var character in value)
        {
            if (character is not (>= 'a' and <= 'z') and
                not (>= '0' and <= '9') and
                not '-' and not '_')
            {
                throw new ArgumentException("Identifier is invalid.", parameterName);
            }
        }

        return value;
    }

    public static string ModelId(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (value.Length > 128 || !char.IsLetterOrDigit(value[0]))
        {
            throw new ArgumentException("Model identifier is invalid.", parameterName);
        }

        foreach (var character in value)
        {
            if (character is not (>= 'A' and <= 'Z') and
                not (>= 'a' and <= 'z') and
                not (>= '0' and <= '9') and
                not '.' and not '_' and not ':' and not '-')
            {
                throw new ArgumentException("Model identifier is invalid.", parameterName);
            }
        }

        return value;
    }

    public static string ProtocolId(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (value.Length > 64 || !value.EndsWith("-v1", StringComparison.Ordinal))
        {
            throw new ArgumentException("Protocol identifier is invalid.", parameterName);
        }

        foreach (var character in value)
        {
            if (character is not (>= 'a' and <= 'z') and
                not (>= '0' and <= '9') and
                not '.' and not '-')
            {
                throw new ArgumentException("Protocol identifier is invalid.", parameterName);
            }
        }

        return value;
    }

    public static string DisplayName(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (value.Length > 80 || HasControl(value))
        {
            throw new ArgumentException("Display name is invalid.", parameterName);
        }

        return value;
    }

    public static string Prompt(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (value.Length > 16 * 1024 || value.IndexOf('\0') >= 0)
        {
            throw new ArgumentException("Prompt is invalid.", parameterName);
        }

        return value;
    }

    public static string CorrelationId(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (value.Length > 96 || HasControl(value))
        {
            throw new ArgumentException("Correlation identifier is invalid.", parameterName);
        }

        return value;
    }

    public static DateTimeOffset Utc(DateTimeOffset value, string parameterName)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Timestamp must be UTC.", parameterName);
        }

        return value;
    }

    public static IReadOnlyList<T> CopyList<T>(IEnumerable<T> values, string parameterName, int maximumCount)
    {
        ArgumentNullException.ThrowIfNull(values, parameterName);
        var copied = values.ToArray();
        if (copied.Length > maximumCount || copied.Any(static value => value is null))
        {
            throw new ArgumentException("Collection is invalid.", parameterName);
        }

        return new ReadOnlyCollection<T>(copied);
    }

    public static bool HasControl(string value)
    {
        foreach (var character in value)
        {
            if (char.IsControl(character))
            {
                return true;
            }
        }

        return false;
    }
}

namespace VFXComposer.Protocol;

internal static class Guard
{
    public static string Token(string value, string parameterName, int maximumLength = 128)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (value.Length > maximumLength)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }

        foreach (var character in value)
        {
            if (character is not (>= 'A' and <= 'Z') and
                not (>= 'a' and <= 'z') and
                not (>= '0' and <= '9') and
                not '.' and not '_' and not ':' and not '-')
            {
                throw new ArgumentException("Token contains a prohibited character.", parameterName);
            }
        }

        return value;
    }

    public static string DiagnosticMessage(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (value.Length > 512)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }

        if (value.IndexOf('\r') >= 0 ||
            value.IndexOf('\n') >= 0 ||
            value.IndexOf('\0') >= 0)
        {
            throw new ArgumentException("Diagnostic messages must be single-line text.", parameterName);
        }

        if (value.StartsWith("/", StringComparison.Ordinal) ||
            value.StartsWith("\\\\", StringComparison.Ordinal) ||
            ContainsDriveRoot(value))
        {
            throw new ArgumentException("Diagnostic messages must not contain an absolute path.", parameterName);
        }

        return value;
    }

    private static bool ContainsDriveRoot(string value)
    {
        for (var index = 0; index + 2 < value.Length; index++)
        {
            if (((value[index] is >= 'A' and <= 'Z') || (value[index] is >= 'a' and <= 'z')) &&
                value[index + 1] == ':' &&
                value[index + 2] is '/' or '\\')
            {
                return true;
            }
        }

        return false;
    }

    public static DateTimeOffset Utc(DateTimeOffset value, string parameterName)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Timestamp must use the UTC offset.", parameterName);
        }

        return value;
    }
}

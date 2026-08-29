namespace VFXComposer.Jobs;

/// <summary>
/// Store-local field discipline mirroring the Protocol guard rules: bounded tokens
/// from a safe alphabet and UTC-only timestamps. Protocol's guard is internal to
/// that assembly, so the rules are restated here rather than referenced.
/// </summary>
internal static class JobsGuard
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

    public static DateTimeOffset Utc(DateTimeOffset value, string parameterName)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Timestamp must use the UTC offset.", parameterName);
        }

        return value;
    }
}

namespace VFXComposer.Client;

/// <summary>
/// Correlates one request without carrying a path, credential, or authority claim.
/// </summary>
public readonly record struct RequestCorrelation
{
    public const int MaximumTokenLength = 128;

    public RequestCorrelation(string requestId, string idempotencyKey)
    {
        RequestId = ValidateToken(requestId, nameof(requestId));
        IdempotencyKey = ValidateToken(idempotencyKey, nameof(idempotencyKey));
    }

    public string RequestId { get; }

    public string IdempotencyKey { get; }

    public static RequestCorrelation CreateNew() => new(
        $"req_{Guid.NewGuid():N}",
        $"idem_{Guid.NewGuid():N}");

    private static string ValidateToken(string value, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(value, parameterName);

        if (value.Length is 0 or > MaximumTokenLength)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                $"Correlation tokens must contain between 1 and {MaximumTokenLength} characters.");
        }

        foreach (var character in value)
        {
            if (!IsAllowedCharacter(character))
            {
                throw new ArgumentException(
                    "Correlation tokens may contain only ASCII letters, digits, '.', '_', ':' and '-'.",
                    parameterName);
            }
        }

        return value;
    }

    private static bool IsAllowedCharacter(char character) =>
        character is >= 'a' and <= 'z'
            or >= 'A' and <= 'Z'
            or >= '0' and <= '9'
            or '.' or '_' or ':' or '-';
}

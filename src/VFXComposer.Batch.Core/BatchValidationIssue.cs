namespace VFXComposer.Batch.Core;

/// <summary>
/// One validation finding in the S4 five-tuple shape (code, severity, path, message,
/// actualValue, allowedRange). <see cref="ActualValue"/> is always a bounded descriptor derived
/// by the parser — never raw prompt text, never a filesystem path outside the manifest-relative
/// value the user authored.
/// </summary>
public sealed record BatchValidationIssue
{
    public const int MaximumPathLength = 1024;
    public const int MaximumDescriptorLength = 256;

    public BatchValidationIssue(string code, string path, string? actualValue = null, string? allowedRange = null)
    {
        var definition = BatchDiagnosticCatalog.Require(code);
        Code = definition.Code;
        Severity = definition.Severity;
        Message = definition.Message;
        Path = Descriptor(path, nameof(path), MaximumPathLength)
            ?? throw new ArgumentException("A validation issue requires a JSON path.", nameof(path));
        ActualValue = Descriptor(actualValue, nameof(actualValue), MaximumDescriptorLength);
        AllowedRange = Descriptor(allowedRange, nameof(allowedRange), MaximumDescriptorLength);
    }

    public string Code { get; }
    public string Severity { get; }
    public string Path { get; }
    public string Message { get; }
    public string? ActualValue { get; }
    public string? AllowedRange { get; }

    public bool IsError => string.Equals(Severity, Protocol.Diagnostics.DiagnosticSeverities.Error, StringComparison.Ordinal);

    public override string ToString() => Code + " " + Path;

    private static string? Descriptor(string? value, string parameterName, int maximumLength)
    {
        if (value is null)
        {
            return null;
        }

        if (value.Length == 0 || value.Length > maximumLength)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }

        foreach (var character in value)
        {
            if (char.IsControl(character))
            {
                throw new ArgumentException("Validation descriptors must not contain control characters.", parameterName);
            }
        }

        return value;
    }
}

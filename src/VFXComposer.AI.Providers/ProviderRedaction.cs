using VFXComposer.AI.Contracts;

namespace VFXComposer.AI.Providers;

/// <summary>One-way diagnostics surface: no prompt, raw request/response, endpoint, secret or authorization value escapes.</summary>
public static class ProviderRedaction
{
    public const string Redacted = "<redacted>";

    public static string Redact(string? value) => string.IsNullOrEmpty(value) ? string.Empty : Redacted;

    public static string RedactEndpoint(EndpointDefinition? endpoint) => endpoint is null ? string.Empty : Redacted;

    public static AiDiagnostic Diagnostic(AiErrorCode code, bool retryable = false) => new(code, retryable);
}

using System.Security.Cryptography;
using System.Text;
using VFXComposer.AI.Contracts;

namespace VFXComposer.AI.Providers;

/// <summary>One-way diagnostics surface: no prompt, raw request/response, endpoint, secret or authorization value escapes.</summary>
public static class ProviderRedaction
{
    public const string Redacted = "<redacted>";

    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    public static string Redact(string? value) => string.IsNullOrEmpty(value) ? string.Empty : Redacted;

    /// <summary>
    /// Returns a one-way endpoint summary suitable for diagnostics, receipts, and ordinary display. It intentionally
    /// contains only the UTF-8 length and a fingerprint, never the raw endpoint, query, user-info, or fragment.
    /// </summary>
    public static string RedactEndpoint(OpaqueEndpoint? endpoint)
    {
        if (endpoint is null)
        {
            return "<endpoint length=0 fingerprint=none>";
        }

        var bytes = StrictUtf8.GetBytes(endpoint.Value);
        try
        {
            var digest = SHA256.HashData(bytes);
            try
            {
                return "<endpoint length=" + endpoint.Utf8ByteLength.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                    " fingerprint=sha256:" + Convert.ToHexString(digest).ToLowerInvariant() + ">";
            }
            finally
            {
                CryptographicOperations.ZeroMemory(digest);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    public static AiDiagnostic Diagnostic(AiErrorCode code, bool retryable = false) => new(code, retryable);
}

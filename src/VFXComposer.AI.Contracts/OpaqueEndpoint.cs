using System.Text;

namespace VFXComposer.AI.Contracts;

/// <summary>
/// User-owned endpoint configuration text. It is deliberately opaque at configuration time: this type does not
/// parse, normalize, authorize, or otherwise interpret its value as a URI or network destination.
/// </summary>
public sealed class OpaqueEndpoint : IEquatable<OpaqueEndpoint>
{
    /// <summary>The maximum persisted UTF-8 byte length for one endpoint value.</summary>
    public const int MaximumUtf8ByteLength = 8192;

    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    /// <summary>
    /// Creates an opaque endpoint without changing its text. Empty and whitespace-only values are valid persisted
    /// configuration values; adapters may decide whether they can use a value only when they make a request.
    /// </summary>
    public OpaqueEndpoint(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        try
        {
            Utf8ByteLength = StrictUtf8.GetByteCount(value);
        }
        catch (EncoderFallbackException exception)
        {
            // A malformed UTF-16 string cannot be saved and recovered byte-for-byte as UTF-8 JSON.
            throw new ArgumentException("Endpoint text is not valid UTF-8 data.", nameof(value), exception);
        }

        if (Utf8ByteLength > MaximumUtf8ByteLength)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Endpoint text exceeds the configured storage limit.");
        }

        Value = value;
    }

    /// <summary>The exact user-supplied string, with no URI parsing or normalization.</summary>
    public string Value { get; }

    /// <summary>The exact UTF-8 storage size used for the bounded configuration value.</summary>
    public int Utf8ByteLength { get; }

    public bool Equals(OpaqueEndpoint? other) =>
        other is not null && string.Equals(Value, other.Value, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is OpaqueEndpoint other && Equals(other);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);

    public override string ToString() => "OpaqueEndpoint(<redacted>)";
}

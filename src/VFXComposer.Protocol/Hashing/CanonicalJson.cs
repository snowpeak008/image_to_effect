using System.Buffers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace VFXComposer.Protocol.Hashing;

/// <summary>Deterministic UTF-8 JSON used only as a hashing preimage.</summary>
public static class CanonicalJson
{
    public static byte[] Canonicalize(
        ReadOnlySpan<byte> utf8Json,
        VFXComposer.Protocol.Json.StrictJsonLimits? limits = null)
    {
        using var document = VFXComposer.Protocol.Json.StrictJsonReader.Parse(utf8Json, limits);
        return Encode(document.RootElement, excludedRootProperty: null);
    }

    internal static byte[] Encode(JsonElement value, string? excludedRootProperty)
    {
        var output = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(
                   output,
                   new JsonWriterOptions
                   {
                       Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                       Indented = false,
                       SkipValidation = false,
                   }))
        {
            Write(value, writer, excludedRootProperty, isRoot: true);
        }

        return output.WrittenSpan.ToArray();
    }

    private static void Write(
        JsonElement value,
        Utf8JsonWriter writer,
        string? excludedRootProperty,
        bool isRoot)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                var properties = value
                    .EnumerateObject()
                    .Where(property =>
                        !isRoot ||
                        excludedRootProperty is null ||
                        !string.Equals(property.Name, excludedRootProperty, StringComparison.Ordinal))
                    .Select(property => new OrderedProperty(
                        property,
                        Encoding.UTF8.GetBytes(property.Name)))
                    .Order(OrderedPropertyComparer.Instance)
                    .ToArray();
                foreach (var property in properties)
                {
                    writer.WritePropertyName(property.Property.Name);
                    Write(property.Property.Value, writer, excludedRootProperty: null, isRoot: false);
                }

                writer.WriteEndObject();
                break;

            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in value.EnumerateArray())
                {
                    Write(item, writer, excludedRootProperty: null, isRoot: false);
                }

                writer.WriteEndArray();
                break;

            case JsonValueKind.String:
                writer.WriteStringValue(value.GetString());
                break;

            case JsonValueKind.Number:
                writer.WriteRawValue(
                    VFXComposer.Protocol.Json.ExactDecimalNormalizer.Normalize(
                        Encoding.UTF8.GetBytes(value.GetRawText())),
                    skipInputValidation: false);
                break;

            case JsonValueKind.True:
                writer.WriteBooleanValue(true);
                break;

            case JsonValueKind.False:
                writer.WriteBooleanValue(false);
                break;

            case JsonValueKind.Null:
                writer.WriteNullValue();
                break;

            default:
                throw new ArgumentException("Unsupported JSON value kind.", nameof(value));
        }
    }

    private sealed record OrderedProperty(JsonProperty Property, byte[] Utf8Name);

    private sealed class OrderedPropertyComparer : IComparer<OrderedProperty>
    {
        public static OrderedPropertyComparer Instance { get; } = new();

        public int Compare(OrderedProperty? left, OrderedProperty? right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left is null)
            {
                return -1;
            }

            if (right is null)
            {
                return 1;
            }

            return left.Utf8Name.AsSpan().SequenceCompareTo(right.Utf8Name);
        }
    }
}

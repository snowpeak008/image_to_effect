using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace VFXComposer.AI.Providers.Recipes;

/// <summary>
/// .NET twin of the Unity-side RecipeCanonicalizer rules: object keys ordinal-sorted, arrays kept in order,
/// integers written invariantly, floats via round-trip formatting with zero collapsed to "0", duplicate
/// properties rejected. The canonical text feeds the SHA-256 confirmation binding (REQ-001-15).
/// </summary>
internal static class RecipeCanonicalJson
{
    public static string Canonicalize(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        using var document = JsonDocument.Parse(json);
        return Canonicalize(document.RootElement);
    }

    public static string Canonicalize(JsonElement element)
    {
        var builder = new StringBuilder();
        WriteCanonical(builder, element);
        return builder.ToString();
    }

    public static string ComputeSha256(string json)
    {
        var canonical = Canonicalize(json);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static void WriteCanonical(StringBuilder builder, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                builder.Append('{');
                var first = true;
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var property in element.EnumerateObject().OrderBy(static property => property.Name, StringComparer.Ordinal))
                {
                    if (!seen.Add(property.Name))
                    {
                        throw new JsonException("Duplicate JSON properties are not supported.");
                    }

                    if (!first)
                    {
                        builder.Append(',');
                    }

                    first = false;
                    WriteString(builder, property.Name);
                    builder.Append(':');
                    WriteCanonical(builder, property.Value);
                }

                builder.Append('}');
                break;
            case JsonValueKind.Array:
                builder.Append('[');
                var firstItem = true;
                foreach (var item in element.EnumerateArray())
                {
                    if (!firstItem)
                    {
                        builder.Append(',');
                    }

                    firstItem = false;
                    WriteCanonical(builder, item);
                }

                builder.Append(']');
                break;
            case JsonValueKind.Number:
                WriteNumber(builder, element);
                break;
            case JsonValueKind.String:
                WriteString(builder, element.GetString() ?? string.Empty);
                break;
            case JsonValueKind.True:
                builder.Append("true");
                break;
            case JsonValueKind.False:
                builder.Append("false");
                break;
            case JsonValueKind.Null:
                builder.Append("null");
                break;
            default:
                throw new JsonException("Unsupported JSON token type in canonicalizer.");
        }
    }

    private static void WriteNumber(StringBuilder builder, JsonElement element)
    {
        // Same integer/float split as the Newtonsoft-based canonicalizer: a literal without '.', 'e', or 'E'
        // is an integer token; anything else canonicalizes through a finite double.
        var raw = element.GetRawText();
        if (raw.IndexOfAny(['.', 'e', 'E']) < 0)
        {
            builder.Append(BigInteger.Parse(raw, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture)
                .ToString(CultureInfo.InvariantCulture));
            return;
        }

        var number = double.Parse(raw, NumberStyles.Float, CultureInfo.InvariantCulture);
        if (double.IsNaN(number) || double.IsInfinity(number))
        {
            throw new JsonException("Non-finite JSON numbers are not supported.");
        }

        builder.Append(number == 0d ? "0" : number.ToString("R", CultureInfo.InvariantCulture));
    }

    private static void WriteString(StringBuilder builder, string value)
    {
        builder.Append('"');
        foreach (var character in value)
        {
            switch (character)
            {
                case '"':
                    builder.Append("\\\"");
                    break;
                case '\\':
                    builder.Append("\\\\");
                    break;
                case '\b':
                    builder.Append("\\b");
                    break;
                case '\f':
                    builder.Append("\\f");
                    break;
                case '\n':
                    builder.Append("\\n");
                    break;
                case '\r':
                    builder.Append("\\r");
                    break;
                case '\t':
                    builder.Append("\\t");
                    break;
                case '\u0085' or '\u2028' or '\u2029':
                    AppendUnicodeEscape(builder, character);
                    break;
                default:
                    if (character < ' ')
                    {
                        AppendUnicodeEscape(builder, character);
                    }
                    else
                    {
                        builder.Append(character);
                    }

                    break;
            }
        }

        builder.Append('"');
    }

    private static void AppendUnicodeEscape(StringBuilder builder, char character) =>
        builder.Append("\\u").Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
}

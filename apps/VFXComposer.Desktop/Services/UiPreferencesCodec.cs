using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using VFXComposer.Desktop.Localization;

namespace VFXComposer.Desktop.Services;

/// <summary>
/// Serializes and strictly parses the <c>ui-preferences.json</c> document. Storage-free so the schema rules are
/// testable without touching a disk.
/// </summary>
public static class UiPreferencesCodec
{
    public const string SchemaId = "vfxcomposer.ui-preferences/1";

    private const string SchemaProperty = "schema";
    private const string LanguageProperty = "language";

    // Persisted language names are part of the schema, so they are spelled out rather than derived from the enum:
    // renaming a member must not silently change the stored document.
    private const string EnglishName = "English";
    private const string ChineseSimplifiedName = "ChineseSimplified";

    public static string Serialize(UiPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);

        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = false }))
        {
            writer.WriteStartObject();
            writer.WriteString(SchemaProperty, SchemaId);
            writer.WriteString(LanguageProperty, LanguageName(preferences.Language));
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    /// <summary>
    /// Accepts only an exact <see cref="SchemaId"/> document with the two known properties and a known language.
    /// Anything else is reported as unusable so the caller can fall back to the default.
    /// </summary>
    public static bool TryParse(string? text, [NotNullWhen(true)] out UiPreferences? preferences)
    {
        preferences = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            string? schema = null;
            string? language = null;
            var properties = 0;
            foreach (var property in root.EnumerateObject())
            {
                properties++;
                if (property.Value.ValueKind != JsonValueKind.String)
                {
                    return false;
                }

                if (string.Equals(property.Name, SchemaProperty, StringComparison.Ordinal))
                {
                    schema = property.Value.GetString();
                }
                else if (string.Equals(property.Name, LanguageProperty, StringComparison.Ordinal))
                {
                    language = property.Value.GetString();
                }
                else
                {
                    return false;
                }
            }

            if (properties != 2
                || !string.Equals(schema, SchemaId, StringComparison.Ordinal)
                || language is null
                || !TryParseLanguage(language, out var parsed))
            {
                return false;
            }

            preferences = new UiPreferences(parsed);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryParseLanguage(string value, out UiLanguage language)
    {
        if (string.Equals(value, EnglishName, StringComparison.Ordinal))
        {
            language = UiLanguage.English;
            return true;
        }

        if (string.Equals(value, ChineseSimplifiedName, StringComparison.Ordinal))
        {
            language = UiLanguage.ChineseSimplified;
            return true;
        }

        language = default;
        return false;
    }

    private static string LanguageName(UiLanguage language) => language switch
    {
        UiLanguage.English => EnglishName,
        UiLanguage.ChineseSimplified => ChineseSimplifiedName,
        _ => throw new ArgumentOutOfRangeException(nameof(language)),
    };
}

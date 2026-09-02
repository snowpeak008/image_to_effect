using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using VFXComposer.Desktop.Localization;

namespace VFXComposer.Desktop.Services;

/// <summary>
/// Serializes and strictly parses the <c>ui-preferences.json</c> document. Storage-free so the schema rules are
/// testable without touching a disk. Writes are always schema <c>/2</c>; reads accept <c>/2</c> and, for the one
/// committed upgrade path, a legacy <c>/1</c> document whose language is adopted verbatim (REQ-004-09).
/// </summary>
public static class UiPreferencesCodec
{
    public const string SchemaId = "vfxcomposer.ui-preferences/2";

    /// <summary>The pre-F8b4 schema: <c>{schema, language}</c> only. Read for upgrade, never written again.</summary>
    public const string LegacySchemaId = "vfxcomposer.ui-preferences/1";

    private const string SchemaProperty = "schema";
    private const string LanguageProperty = "language";
    private const string GenerationModeProperty = "generationMode";

    // Persisted names are part of the schema, so they are spelled out rather than derived from the enums:
    // renaming a member must not silently change the stored document (REQ-004-08).
    private const string EnglishName = "English";
    private const string ChineseSimplifiedName = "ChineseSimplified";
    private const string SimpleName = "Simple";
    private const string ProfessionalName = "Professional";

    public static string Serialize(UiPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);

        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = false }))
        {
            writer.WriteStartObject();
            writer.WriteString(SchemaProperty, SchemaId);
            writer.WriteString(LanguageProperty, LanguageName(preferences.Language));
            writer.WriteString(GenerationModeProperty, GenerationModeName(preferences.GenerationMode));
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    /// <summary>
    /// Accepts only an exact <see cref="SchemaId"/> document with the three known properties and known values, or an
    /// exact legacy <see cref="LegacySchemaId"/> document with its two known properties (its language is adopted and
    /// the mode defaults to Simple; the next explicit save rebuilds the file as <c>/2</c>). Anything else is
    /// reported as unusable so the caller can fall back to the default (REQ-004-10).
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
            string? generationMode = null;
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
                else if (string.Equals(property.Name, GenerationModeProperty, StringComparison.Ordinal))
                {
                    generationMode = property.Value.GetString();
                }
                else
                {
                    return false;
                }
            }

            if (language is null || !TryParseLanguage(language, out var parsedLanguage))
            {
                return false;
            }

            if (string.Equals(schema, LegacySchemaId, StringComparison.Ordinal))
            {
                // The strict /1 shape: exactly {schema, language}. A /1 document naming a mode never existed, so
                // one that does is not a legacy file and stays unusable.
                if (properties != 2 || generationMode is not null)
                {
                    return false;
                }

                preferences = new UiPreferences(parsedLanguage);
                return true;
            }

            if (properties != 3
                || !string.Equals(schema, SchemaId, StringComparison.Ordinal)
                || generationMode is null
                || !TryParseGenerationMode(generationMode, out var parsedMode))
            {
                return false;
            }

            preferences = new UiPreferences(parsedLanguage, parsedMode);
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

    private static bool TryParseGenerationMode(string value, out GenerationMode mode)
    {
        if (string.Equals(value, SimpleName, StringComparison.Ordinal))
        {
            mode = GenerationMode.Simple;
            return true;
        }

        if (string.Equals(value, ProfessionalName, StringComparison.Ordinal))
        {
            mode = GenerationMode.Professional;
            return true;
        }

        mode = default;
        return false;
    }

    private static string LanguageName(UiLanguage language) => language switch
    {
        UiLanguage.English => EnglishName,
        UiLanguage.ChineseSimplified => ChineseSimplifiedName,
        _ => throw new ArgumentOutOfRangeException(nameof(language)),
    };

    private static string GenerationModeName(GenerationMode mode) => mode switch
    {
        GenerationMode.Simple => SimpleName,
        GenerationMode.Professional => ProfessionalName,
        _ => throw new ArgumentOutOfRangeException(nameof(mode)),
    };
}

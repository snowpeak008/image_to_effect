using System.Globalization;
using System.Text.Json;
using VFXComposer.AI.Contracts;
using VFXComposer.AI.Contracts.Recipes;

namespace VFXComposer.AI.Providers.Recipes;

/// <summary>
/// Hand-written, versioned wire format for retained recipe drafts. Every field round-trips through the
/// RecipeDraftRecord constructor, so a tampered file can never smuggle an out-of-contract record into memory.
/// The format version is checked by exact equality: a file whose integer <c>formatVersion</c> differs fails with
/// <see cref="RecipeDraftStoreErrorCode.UnsupportedVersion"/>, everything else unreadable is corruption.
/// </summary>
internal static class RecipeDraftCodec
{
    public static byte[] Serialize(RecipeDraftStoreDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteNumber("formatVersion", AiContractVersions.RecipeDraftRecordFormatVersion);
            writer.WriteStartArray("lineages");
            foreach (var (lineageId, watermark) in document.RevisionWatermarks.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
            {
                writer.WriteStartObject();
                writer.WriteString("lineageId", lineageId);
                writer.WriteNumber("revisionWatermark", watermark);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteStartArray("records");
            foreach (var record in document.Records)
            {
                WriteRecord(writer, record);
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return buffer.ToArray();
    }

    public static RecipeDraftStoreDocument Deserialize(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        using var document = JsonDocument.Parse(bytes);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("formatVersion", out var version) ||
            version.ValueKind != JsonValueKind.Number ||
            !version.TryGetInt32(out var formatVersion))
        {
            throw new InvalidDataException("Recipe draft storage is invalid.");
        }

        if (formatVersion != AiContractVersions.RecipeDraftRecordFormatVersion)
        {
            throw new RecipeDraftStoreException(RecipeDraftStoreErrorCode.UnsupportedVersion);
        }

        if (!root.TryGetProperty("lineages", out var lineagesElement) ||
            lineagesElement.ValueKind != JsonValueKind.Array ||
            !root.TryGetProperty("records", out var recordsElement) ||
            recordsElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException("Recipe draft storage is invalid.");
        }

        var watermarks = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var lineageElement in lineagesElement.EnumerateArray())
        {
            if (lineageElement.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidDataException("Recipe draft storage is invalid.");
            }

            var lineageId = ReadString(lineageElement, "lineageId");
            var watermark = ReadInt32(lineageElement, "revisionWatermark");
            if (watermark < 1 || !watermarks.TryAdd(lineageId, watermark))
            {
                throw new InvalidDataException("Recipe draft storage is invalid.");
            }
        }

        var records = new List<RecipeDraftRecord>();
        foreach (var recordElement in recordsElement.EnumerateArray())
        {
            records.Add(ReadRecord(recordElement));
        }

        var loaded = new RecipeDraftStoreDocument(records, watermarks);
        loaded.ThrowIfInvalid();
        return loaded;
    }

    /// <summary>The UTF-8 bytes the store writes for one record's recipeJson value, escaping included.</summary>
    public static int PersistedRecipeJsonBytes(RecipeDraftRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return JsonEncodedText.Encode(record.RecipeJson).EncodedUtf8Bytes.Length;
    }

    private static void WriteRecord(Utf8JsonWriter writer, RecipeDraftRecord record)
    {
        writer.WriteStartObject();
        writer.WriteString("draftId", record.DraftId);
        writer.WriteString("status", record.Status.ToString());
        writer.WriteString("createdUtc", record.CreatedUtc.UtcDateTime.ToString("O", CultureInfo.InvariantCulture));
        writer.WriteString("updatedUtc", record.UpdatedUtc.UtcDateTime.ToString("O", CultureInfo.InvariantCulture));
        writer.WriteString("correlationId", record.CorrelationId);
        writer.WriteString("promptTemplateVersion", record.PromptTemplateVersion);
        writer.WriteString("templateCatalogVersion", record.TemplateCatalogVersion);
        writer.WriteString("recipeJson", record.RecipeJson);
        writer.WriteString("canonicalSha256", record.CanonicalSha256);
        writer.WriteString("recipeId", record.RecipeId);
        writer.WriteString("archetype", record.Archetype);
        writer.WriteString("dimension", record.Dimension);
        writer.WriteString("targetProfile", record.TargetProfile);
        writer.WriteNumber("requestCount", record.RequestCount);
        writer.WriteStartArray("issues");
        foreach (var issue in record.Issues)
        {
            writer.WriteStartObject();
            writer.WriteString("code", issue.Code);
            writer.WriteString("severity", issue.Severity.ToString());
            writer.WriteString("path", issue.Path);
            writer.WriteString("message", issue.Message);
            writer.WriteString("actualValueJson", issue.ActualValueJson);
            writer.WriteString("allowedRange", issue.AllowedRange);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteString("lineageId", record.LineageId);
        writer.WriteString("parentDraftId", record.ParentDraftId);
        writer.WriteNumber("revisionOrdinal", record.RevisionOrdinal);
        writer.WriteString("origin", RecipeDraftOriginNames.Of(record.Origin));
        writer.WriteString("feedbackText", record.FeedbackText);
        writer.WriteStartArray("guardRestorations");
        foreach (var restoration in record.GuardRestorations)
        {
            writer.WriteStartObject();
            writer.WriteString("parameterPath", restoration.ParameterPath);
            writer.WriteString("sourceDraftId", restoration.SourceDraftId);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteNumber("guardRestorationCount", record.GuardRestorationCount);
        writer.WriteString("presetId", record.PresetId);
        writer.WriteEndObject();
    }

    private static RecipeDraftRecord ReadRecord(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("Recipe draft storage is invalid.");
        }

        var issues = new List<RecipeValidationIssue>();
        foreach (var issueElement in ReadArray(element, "issues"))
        {
            issues.Add(new RecipeValidationIssue(
                ReadString(issueElement, "code"),
                ReadEnum<RecipeValidationSeverity>(issueElement, "severity"),
                ReadString(issueElement, "path"),
                ReadString(issueElement, "message"),
                ReadOptionalString(issueElement, "actualValueJson"),
                ReadOptionalString(issueElement, "allowedRange")));
        }

        var restorations = new List<RecipeGuardRestoration>();
        foreach (var restorationElement in ReadArray(element, "guardRestorations"))
        {
            restorations.Add(new RecipeGuardRestoration(
                ReadString(restorationElement, "parameterPath"),
                ReadString(restorationElement, "sourceDraftId")));
        }

        if (!RecipeDraftOriginNames.TryParse(ReadString(element, "origin"), out var origin))
        {
            throw new InvalidDataException("Recipe draft storage is invalid.");
        }

        var provenance = new RecipeDraftProvenance(
            ReadString(element, "lineageId"),
            ReadOptionalString(element, "parentDraftId"),
            ReadInt32(element, "revisionOrdinal"),
            origin,
            ReadOptionalString(element, "feedbackText"),
            restorations,
            ReadInt32(element, "guardRestorationCount"),
            ReadOptionalString(element, "presetId"));

        return new RecipeDraftRecord(
            ReadString(element, "draftId"),
            ReadEnum<RecipeDraftStatus>(element, "status"),
            ReadUtc(element, "createdUtc"),
            ReadUtc(element, "updatedUtc"),
            ReadString(element, "correlationId"),
            ReadString(element, "promptTemplateVersion"),
            ReadString(element, "templateCatalogVersion"),
            ReadString(element, "recipeJson"),
            ReadOptionalString(element, "canonicalSha256"),
            ReadOptionalString(element, "recipeId"),
            ReadOptionalString(element, "archetype"),
            ReadOptionalString(element, "dimension"),
            ReadOptionalString(element, "targetProfile"),
            issues,
            ReadInt32(element, "requestCount"),
            provenance);
    }

    private static JsonElement.ArrayEnumerator ReadArray(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException("Recipe draft storage is invalid.");
        }

        return value.EnumerateArray();
    }

    private static string ReadString(JsonElement element, string name) =>
        ReadOptionalString(element, name)
            ?? throw new InvalidDataException("Recipe draft storage is invalid.");

    private static string? ReadOptionalString(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (value.ValueKind != JsonValueKind.String)
        {
            throw new InvalidDataException("Recipe draft storage is invalid.");
        }

        return value.GetString();
    }

    private static int ReadInt32(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value) ||
            value.ValueKind != JsonValueKind.Number ||
            !value.TryGetInt32(out var parsed))
        {
            throw new InvalidDataException("Recipe draft storage is invalid.");
        }

        return parsed;
    }

    private static TEnum ReadEnum<TEnum>(JsonElement element, string name)
        where TEnum : struct, Enum
    {
        var text = ReadString(element, name);
        if (!Enum.TryParse<TEnum>(text, ignoreCase: false, out var parsed) || !Enum.IsDefined(parsed))
        {
            throw new InvalidDataException("Recipe draft storage is invalid.");
        }

        return parsed;
    }

    private static DateTimeOffset ReadUtc(JsonElement element, string name)
    {
        var text = ReadString(element, name);
        if (!DateTimeOffset.TryParseExact(
                text,
                "O",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsed))
        {
            throw new InvalidDataException("Recipe draft storage is invalid.");
        }

        return parsed;
    }
}

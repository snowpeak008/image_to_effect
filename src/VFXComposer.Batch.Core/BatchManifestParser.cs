using System.Collections.Frozen;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace VFXComposer.Batch.Core;

/// <summary>Outcome of one manifest parse: the manifest when it is usable, plus every finding.</summary>
public sealed record BatchManifestParseResult(BatchManifest? Manifest, IReadOnlyList<BatchValidationIssue> Issues)
{
    public bool IsValid => Manifest is not null && !Issues.Any(static issue => issue.IsError);
}

/// <summary>
/// Hand-written two-layer validator for <c>vfxcomposer.batch-manifest/1</c> (REQ-002 §5.4):
/// structure (required fields, types, closed vocabularies, bounds, uniqueness, unknown fields)
/// then semantics (recipe references, composed description bound) and finally the capability
/// gate. Unknown fields are rejected rather than ignored, and every finding carries the exact
/// JSON path. No JSON Schema library is used, matching the S3 decision.
/// </summary>
public static class BatchManifestParser
{
    private const int MaximumPathSegmentLength = 64;

    private static readonly FrozenSet<string> RootFields =
        new[] { "schemaVersion", "batchId", "onFailure", "defaults", "items" }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> ItemFields =
        new[] { "itemId", "kind", "prompt", "recipePath", "constraints" }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> ReservedDeviceNames =
        new[]
        {
            "CON", "PRN", "AUX", "NUL",
            "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
        }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>Parses and validates a manifest document. The probe is only consulted by the semantic layer.</summary>
    public static BatchManifestParseResult Parse(
        string manifestJson,
        IBatchRecipeProbe recipeProbe,
        BatchCapabilityProfile capability)
    {
        ArgumentNullException.ThrowIfNull(manifestJson);
        ArgumentNullException.ThrowIfNull(recipeProbe);
        ArgumentNullException.ThrowIfNull(capability);
        var issues = new List<BatchValidationIssue>();
        var byteCount = Encoding.UTF8.GetByteCount(manifestJson);
        if (byteCount > BatchManifestLimits.MaximumManifestBytes)
        {
            issues.Add(new BatchValidationIssue(
                BatchDiagnosticCodes.ManifestTooLarge,
                "$",
                Number(byteCount),
                "<= " + Number(BatchManifestLimits.MaximumManifestBytes) + " bytes"));
            return new BatchManifestParseResult(null, issues);
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(
                manifestJson,
                new JsonDocumentOptions { AllowTrailingCommas = false, CommentHandling = JsonCommentHandling.Disallow });
        }
        catch (JsonException)
        {
            issues.Add(new BatchValidationIssue(BatchDiagnosticCodes.MalformedJson, "$"));
            return new BatchManifestParseResult(null, issues);
        }

        using (document)
        {
            return ParseRoot(document.RootElement, recipeProbe, capability, issues);
        }
    }

    private static BatchManifestParseResult ParseRoot(
        JsonElement root,
        IBatchRecipeProbe recipeProbe,
        BatchCapabilityProfile capability,
        List<BatchValidationIssue> issues)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            issues.Add(new BatchValidationIssue(
                BatchDiagnosticCodes.UnexpectedFieldType,
                "$",
                root.ValueKind.ToString(),
                "object"));
            return new BatchManifestParseResult(null, issues);
        }

        RejectUnknownFields(root, RootFields, "$", issues);
        if (!ReadSchemaVersion(root, issues))
        {
            return new BatchManifestParseResult(null, issues);
        }

        var batchId = ReadToken(root, "batchId", "$.batchId", required: true, issues);
        var failurePolicy = ReadFailurePolicy(root, issues);
        var defaults = ReadDefaults(root, issues);
        var items = ReadItems(root, defaults, issues);
        ApplySemanticLayer(items, recipeProbe, issues);
        ApplyCapabilityGate(items, capability, issues);
        if (batchId is null || items is null || issues.Any(static issue => issue.IsError))
        {
            return new BatchManifestParseResult(null, issues);
        }

        return new BatchManifestParseResult(
            new BatchManifest(BatchManifestLimits.SchemaVersion, batchId, failurePolicy, items.Select(static entry => entry.Item).ToArray()),
            issues);
    }

    private static bool ReadSchemaVersion(JsonElement root, List<BatchValidationIssue> issues)
    {
        if (!root.TryGetProperty("schemaVersion", out var element))
        {
            issues.Add(new BatchValidationIssue(
                BatchDiagnosticCodes.MissingRequiredField,
                "$.schemaVersion",
                allowedRange: BatchManifestLimits.SchemaVersion));
            return false;
        }

        if (element.ValueKind != JsonValueKind.String)
        {
            issues.Add(new BatchValidationIssue(
                BatchDiagnosticCodes.UnexpectedFieldType,
                "$.schemaVersion",
                element.ValueKind.ToString(),
                "string"));
            return false;
        }

        var value = element.GetString() ?? string.Empty;
        if (!string.Equals(value, BatchManifestLimits.SchemaVersion, StringComparison.Ordinal))
        {
            issues.Add(new BatchValidationIssue(
                BatchDiagnosticCodes.UnsupportedSchemaVersion,
                "$.schemaVersion",
                Descriptor(value),
                BatchManifestLimits.SchemaVersion));
            return false;
        }

        return true;
    }

    private static string ReadFailurePolicy(JsonElement root, List<BatchValidationIssue> issues)
    {
        if (!root.TryGetProperty("onFailure", out var element))
        {
            return BatchFailurePolicies.Continue;
        }

        if (element.ValueKind != JsonValueKind.String)
        {
            issues.Add(new BatchValidationIssue(
                BatchDiagnosticCodes.UnexpectedFieldType,
                "$.onFailure",
                element.ValueKind.ToString(),
                "string"));
            return BatchFailurePolicies.Continue;
        }

        var value = element.GetString() ?? string.Empty;
        if (!BatchFailurePolicies.IsKnown(value))
        {
            issues.Add(new BatchValidationIssue(
                BatchDiagnosticCodes.UnknownEnumValue,
                "$.onFailure",
                Descriptor(value),
                string.Join('|', BatchFailurePolicies.All.Order(StringComparer.Ordinal))));
            return BatchFailurePolicies.Continue;
        }

        return value;
    }

    private static BatchConstraints ReadDefaults(JsonElement root, List<BatchValidationIssue> issues)
    {
        if (!root.TryGetProperty("defaults", out var element))
        {
            return BatchConstraints.Empty;
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            issues.Add(new BatchValidationIssue(
                BatchDiagnosticCodes.UnexpectedFieldType,
                "$.defaults",
                element.ValueKind.ToString(),
                "object"));
            return BatchConstraints.Empty;
        }

        return ReadConstraints(element, "$.defaults", issues);
    }

    private static List<ParsedEntry>? ReadItems(
        JsonElement root,
        BatchConstraints defaults,
        List<BatchValidationIssue> issues)
    {
        if (!root.TryGetProperty("items", out var element))
        {
            issues.Add(new BatchValidationIssue(BatchDiagnosticCodes.MissingRequiredField, "$.items"));
            return null;
        }

        if (element.ValueKind != JsonValueKind.Array)
        {
            issues.Add(new BatchValidationIssue(
                BatchDiagnosticCodes.UnexpectedFieldType,
                "$.items",
                element.ValueKind.ToString(),
                "array"));
            return null;
        }

        var length = element.GetArrayLength();
        if (length is < BatchManifestLimits.MinimumItemCount or > BatchManifestLimits.MaximumItemCount)
        {
            issues.Add(new BatchValidationIssue(
                BatchDiagnosticCodes.ValueOutOfRange,
                "$.items",
                Number(length),
                Number(BatchManifestLimits.MinimumItemCount) + ".." + Number(BatchManifestLimits.MaximumItemCount)));
        }

        var entries = new List<ParsedEntry>(Math.Min(length, BatchManifestLimits.MaximumItemCount));
        var seenItemIds = new HashSet<string>(StringComparer.Ordinal);
        var index = 0;
        foreach (var itemElement in element.EnumerateArray())
        {
            if (index >= BatchManifestLimits.MaximumItemCount)
            {
                break;
            }

            var path = "$.items[" + Number(index) + "]";
            var item = ReadItem(itemElement, path, defaults, issues);
            if (item is not null)
            {
                if (!seenItemIds.Add(item.ItemId))
                {
                    issues.Add(new BatchValidationIssue(
                        BatchDiagnosticCodes.DuplicateItemId,
                        path + ".itemId",
                        Descriptor(item.ItemId)));
                }

                entries.Add(new ParsedEntry(item, path));
            }

            index++;
        }

        return entries;
    }

    private static BatchManifestItem? ReadItem(
        JsonElement element,
        string path,
        BatchConstraints defaults,
        List<BatchValidationIssue> issues)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            issues.Add(new BatchValidationIssue(
                BatchDiagnosticCodes.UnexpectedFieldType,
                path,
                element.ValueKind.ToString(),
                "object"));
            return null;
        }

        RejectUnknownFields(element, ItemFields, path, issues);
        var itemId = ReadToken(element, "itemId", path + ".itemId", required: true, issues);
        var kind = ReadKind(element, path, issues);
        if (itemId is null || kind is null)
        {
            return null;
        }

        var isPrompt = string.Equals(kind, BatchItemKinds.Prompt, StringComparison.Ordinal);
        var prompt = ReadPrompt(element, path, isPrompt, issues);
        var recipePath = ReadRecipePath(element, path, isPrompt, issues);
        var constraints = ReadItemConstraints(element, path, isPrompt, defaults, issues);
        if (isPrompt ? prompt is null : recipePath is null)
        {
            return null;
        }

        return new BatchManifestItem(itemId, kind, prompt, recipePath, constraints);
    }

    private static string? ReadKind(JsonElement element, string path, List<BatchValidationIssue> issues)
    {
        if (!element.TryGetProperty("kind", out var kindElement))
        {
            issues.Add(new BatchValidationIssue(
                BatchDiagnosticCodes.MissingRequiredField,
                path + ".kind",
                allowedRange: string.Join('|', BatchItemKinds.All.Order(StringComparer.Ordinal))));
            return null;
        }

        if (kindElement.ValueKind != JsonValueKind.String)
        {
            issues.Add(new BatchValidationIssue(
                BatchDiagnosticCodes.UnexpectedFieldType,
                path + ".kind",
                kindElement.ValueKind.ToString(),
                "string"));
            return null;
        }

        var kind = kindElement.GetString() ?? string.Empty;
        if (!BatchItemKinds.IsKnown(kind))
        {
            issues.Add(new BatchValidationIssue(
                BatchDiagnosticCodes.UnknownEnumValue,
                path + ".kind",
                Descriptor(kind),
                string.Join('|', BatchItemKinds.All.Order(StringComparer.Ordinal))));
            return null;
        }

        return kind;
    }

    private static string? ReadPrompt(
        JsonElement element,
        string path,
        bool isPrompt,
        List<BatchValidationIssue> issues)
    {
        var promptPath = path + ".prompt";
        if (!element.TryGetProperty("prompt", out var promptElement))
        {
            if (isPrompt)
            {
                issues.Add(new BatchValidationIssue(BatchDiagnosticCodes.MissingRequiredField, promptPath));
            }

            return null;
        }

        if (!isPrompt)
        {
            issues.Add(new BatchValidationIssue(
                BatchDiagnosticCodes.FieldNotAllowedForKind,
                promptPath,
                allowedRange: BatchItemKinds.Prompt));
            return null;
        }

        if (promptElement.ValueKind != JsonValueKind.String)
        {
            issues.Add(new BatchValidationIssue(
                BatchDiagnosticCodes.UnexpectedFieldType,
                promptPath,
                promptElement.ValueKind.ToString(),
                "string"));
            return null;
        }

        var prompt = promptElement.GetString() ?? string.Empty;
        var promptBytes = Encoding.UTF8.GetByteCount(prompt);
        if (prompt.Length == 0 || promptBytes > BatchManifestLimits.MaximumPromptUtf8Bytes || prompt.Contains('\0'))
        {
            // The prompt itself never reaches the report; only its measured size does.
            issues.Add(new BatchValidationIssue(
                BatchDiagnosticCodes.ValueOutOfRange,
                promptPath,
                Number(promptBytes) + " bytes",
                "1.." + Number(BatchManifestLimits.MaximumPromptUtf8Bytes) + " bytes"));
            return null;
        }

        return prompt;
    }

    private static string? ReadRecipePath(
        JsonElement element,
        string path,
        bool isPrompt,
        List<BatchValidationIssue> issues)
    {
        var recipePathPath = path + ".recipePath";
        if (!element.TryGetProperty("recipePath", out var recipeElement))
        {
            if (!isPrompt)
            {
                issues.Add(new BatchValidationIssue(BatchDiagnosticCodes.MissingRequiredField, recipePathPath));
            }

            return null;
        }

        if (isPrompt)
        {
            issues.Add(new BatchValidationIssue(
                BatchDiagnosticCodes.FieldNotAllowedForKind,
                recipePathPath,
                allowedRange: BatchItemKinds.Recipe));
            return null;
        }

        if (recipeElement.ValueKind != JsonValueKind.String)
        {
            issues.Add(new BatchValidationIssue(
                BatchDiagnosticCodes.UnexpectedFieldType,
                recipePathPath,
                recipeElement.ValueKind.ToString(),
                "string"));
            return null;
        }

        var value = recipeElement.GetString() ?? string.Empty;
        if (!IsContainedRelativeJsonPath(value))
        {
            issues.Add(new BatchValidationIssue(
                BatchDiagnosticCodes.UnsafeRecipePath,
                recipePathPath,
                Descriptor(value),
                "contained relative *.json path"));
            return null;
        }

        return value;
    }

    private static BatchConstraints ReadItemConstraints(
        JsonElement element,
        string path,
        bool isPrompt,
        BatchConstraints defaults,
        List<BatchValidationIssue> issues)
    {
        var constraintsPath = path + ".constraints";
        if (!element.TryGetProperty("constraints", out var constraintsElement))
        {
            return isPrompt ? defaults : BatchConstraints.Empty;
        }

        if (!isPrompt)
        {
            issues.Add(new BatchValidationIssue(
                BatchDiagnosticCodes.FieldNotAllowedForKind,
                constraintsPath,
                allowedRange: BatchItemKinds.Prompt));
            return BatchConstraints.Empty;
        }

        if (constraintsElement.ValueKind != JsonValueKind.Object)
        {
            issues.Add(new BatchValidationIssue(
                BatchDiagnosticCodes.UnexpectedFieldType,
                constraintsPath,
                constraintsElement.ValueKind.ToString(),
                "object"));
            return defaults;
        }

        return ReadConstraints(constraintsElement, constraintsPath, issues).InheritFrom(defaults);
    }

    private static BatchConstraints ReadConstraints(
        JsonElement element,
        string path,
        List<BatchValidationIssue> issues)
    {
        RejectUnknownFields(element, BatchConstraintKeys.All, path, issues);
        return new BatchConstraints(
            ReadConstraintText(element, BatchConstraintKeys.Archetype, path, issues),
            ReadDimension(element, path, issues),
            ReadConstraintText(element, BatchConstraintKeys.Element, path, issues),
            ReadConstraintText(element, BatchConstraintKeys.Style, path, issues),
            ReadConstraintText(element, BatchConstraintKeys.TargetProfile, path, issues),
            ReadRandomSeed(element, path, issues));
    }

    private static string? ReadConstraintText(
        JsonElement element,
        string key,
        string path,
        List<BatchValidationIssue> issues)
    {
        if (!element.TryGetProperty(key, out var value))
        {
            return null;
        }

        var keyPath = path + "." + key;
        if (value.ValueKind != JsonValueKind.String)
        {
            issues.Add(new BatchValidationIssue(
                BatchDiagnosticCodes.UnexpectedFieldType,
                keyPath,
                value.ValueKind.ToString(),
                "string"));
            return null;
        }

        var text = value.GetString() ?? string.Empty;
        if (text.Length is 0 or > BatchManifestLimits.MaximumConstraintValueLength || HasControl(text))
        {
            issues.Add(new BatchValidationIssue(
                BatchDiagnosticCodes.ValueOutOfRange,
                keyPath,
                Number(text.Length) + " characters",
                "1.." + Number(BatchManifestLimits.MaximumConstraintValueLength) + " characters"));
            return null;
        }

        return text;
    }

    private static string? ReadDimension(JsonElement element, string path, List<BatchValidationIssue> issues)
    {
        if (!element.TryGetProperty(BatchConstraintKeys.Dimension, out var value))
        {
            return null;
        }

        var keyPath = path + "." + BatchConstraintKeys.Dimension;
        if (value.ValueKind != JsonValueKind.String)
        {
            issues.Add(new BatchValidationIssue(
                BatchDiagnosticCodes.UnexpectedFieldType,
                keyPath,
                value.ValueKind.ToString(),
                "string"));
            return null;
        }

        var dimension = value.GetString() ?? string.Empty;
        if (!BatchDimensions.IsKnown(dimension))
        {
            issues.Add(new BatchValidationIssue(
                BatchDiagnosticCodes.UnknownEnumValue,
                keyPath,
                Descriptor(dimension),
                string.Join('|', BatchDimensions.All.Order(StringComparer.Ordinal))));
            return null;
        }

        return dimension;
    }

    private static int? ReadRandomSeed(JsonElement element, string path, List<BatchValidationIssue> issues)
    {
        if (!element.TryGetProperty(BatchConstraintKeys.RandomSeed, out var value))
        {
            return null;
        }

        var keyPath = path + "." + BatchConstraintKeys.RandomSeed;
        if (value.ValueKind != JsonValueKind.Number)
        {
            issues.Add(new BatchValidationIssue(
                BatchDiagnosticCodes.UnexpectedFieldType,
                keyPath,
                value.ValueKind.ToString(),
                "number"));
            return null;
        }

        if (!value.TryGetInt32(out var seed))
        {
            issues.Add(new BatchValidationIssue(
                BatchDiagnosticCodes.ValueOutOfRange,
                keyPath,
                "non-integer or out-of-range number",
                "int32"));
            return null;
        }

        return seed;
    }

    private static string? ReadToken(
        JsonElement owner,
        string propertyName,
        string path,
        bool required,
        List<BatchValidationIssue> issues)
    {
        if (!owner.TryGetProperty(propertyName, out var element))
        {
            if (required)
            {
                issues.Add(new BatchValidationIssue(BatchDiagnosticCodes.MissingRequiredField, path));
            }

            return null;
        }

        if (element.ValueKind != JsonValueKind.String)
        {
            issues.Add(new BatchValidationIssue(
                BatchDiagnosticCodes.UnexpectedFieldType,
                path,
                element.ValueKind.ToString(),
                "string"));
            return null;
        }

        var token = element.GetString() ?? string.Empty;
        if (!IsLowerToken(token))
        {
            issues.Add(new BatchValidationIssue(
                BatchDiagnosticCodes.ValueOutOfRange,
                path,
                Descriptor(token),
                "lower kebab/snake token, 1.." + Number(BatchManifestLimits.MaximumTokenLength) + " characters"));
            return null;
        }

        return token;
    }

    private static void ApplySemanticLayer(
        List<ParsedEntry>? entries,
        IBatchRecipeProbe recipeProbe,
        List<BatchValidationIssue> issues)
    {
        if (entries is null)
        {
            return;
        }

        foreach (var entry in entries)
        {
            if (entry.Item.RecipePath is string recipePath)
            {
                var probe = recipeProbe.Probe(recipePath);
                if (probe == BatchRecipeProbeResult.Missing)
                {
                    issues.Add(new BatchValidationIssue(
                        BatchDiagnosticCodes.RecipeFileMissing,
                        entry.Path + ".recipePath",
                        Descriptor(recipePath)));
                }
                else if (probe == BatchRecipeProbeResult.NotJsonObject)
                {
                    issues.Add(new BatchValidationIssue(
                        BatchDiagnosticCodes.RecipeFileNotJsonObject,
                        entry.Path + ".recipePath",
                        Descriptor(recipePath)));
                }
            }
        }
    }

    private static void ApplyCapabilityGate(
        List<ParsedEntry>? entries,
        BatchCapabilityProfile capability,
        List<BatchValidationIssue> issues)
    {
        if (entries is null)
        {
            return;
        }

        if (!capability.RecipeBuildSupported)
        {
            foreach (var entry in entries.Where(static entry =>
                string.Equals(entry.Item.Kind, BatchItemKinds.Recipe, StringComparison.Ordinal)))
            {
                issues.Add(new BatchValidationIssue(
                    BatchDiagnosticCodes.RecipeBuildNotSupported,
                    entry.Path + ".kind",
                    BatchItemKinds.Recipe,
                    BatchItemKinds.Prompt));
            }
        }

        if (!capability.PromptGenerationAvailable &&
            entries.Any(static entry => string.Equals(entry.Item.Kind, BatchItemKinds.Prompt, StringComparison.Ordinal)))
        {
            issues.Add(new BatchValidationIssue(BatchDiagnosticCodes.PromptGenerationUnavailable, "$.items"));
        }
    }

    private static void RejectUnknownFields(
        JsonElement element,
        IReadOnlySet<string> knownFields,
        string path,
        List<BatchValidationIssue> issues)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (!knownFields.Contains(property.Name))
            {
                issues.Add(new BatchValidationIssue(
                    BatchDiagnosticCodes.UnknownField,
                    path + "." + Segment(property.Name),
                    Descriptor(property.Name),
                    string.Join('|', knownFields.Order(StringComparer.Ordinal))));
            }
        }
    }

    /// <summary>Lower kebab/snake token as required for <c>batchId</c> and <c>itemId</c>.</summary>
    private static bool IsLowerToken(string value)
    {
        if (value.Length is 0 or > BatchManifestLimits.MaximumTokenLength)
        {
            return false;
        }

        var previousWasSeparator = true;
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (character is (>= 'a' and <= 'z') or (>= '0' and <= '9'))
            {
                previousWasSeparator = false;
                continue;
            }

            if (character is '-' or '_')
            {
                if (previousWasSeparator)
                {
                    return false;
                }

                previousWasSeparator = true;
                continue;
            }

            return false;
        }

        return !previousWasSeparator;
    }

    /// <summary>
    /// Containment rules for <c>recipePath</c> (REQ-002 §5.3): relative, forward-slash separated,
    /// no traversal, no drive/UNC/device/ADS form, no Windows reserved device segment, and a
    /// <c>.json</c> suffix.
    /// </summary>
    private static bool IsContainedRelativeJsonPath(string value)
    {
        if (value.Length is 0 or > BatchManifestLimits.MaximumRecipePathLength ||
            HasControl(value) ||
            !value.EndsWith(".json", StringComparison.Ordinal) ||
            value.Contains('\\', StringComparison.Ordinal) ||
            value.Contains(':', StringComparison.Ordinal) ||
            value.StartsWith('/') ||
            value.StartsWith('~'))
        {
            return false;
        }

        var segments = value.Split('/');
        foreach (var segment in segments)
        {
            if (segment.Length == 0 ||
                segment is "." or ".." ||
                segment.EndsWith('.') ||
                segment.EndsWith(' ') ||
                segment.StartsWith(' '))
            {
                return false;
            }

            var stem = segment.Split('.')[0];
            if (ReservedDeviceNames.Contains(stem))
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasControl(string value)
    {
        foreach (var character in value)
        {
            if (char.IsControl(character))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Bounds and sanitises a user-authored value before it is quoted back in a finding.</summary>
    private static string Descriptor(string value)
    {
        var trimmed = value.Length > BatchValidationIssue.MaximumDescriptorLength
            ? value[..BatchValidationIssue.MaximumDescriptorLength]
            : value;
        var builder = new StringBuilder(trimmed.Length);
        foreach (var character in trimmed)
        {
            builder.Append(char.IsControl(character) ? '?' : character);
        }

        return builder.Length == 0 ? "<empty>" : builder.ToString();
    }

    private static string Segment(string name)
    {
        var trimmed = name.Length > MaximumPathSegmentLength ? name[..MaximumPathSegmentLength] : name;
        var builder = new StringBuilder(trimmed.Length);
        foreach (var character in trimmed)
        {
            builder.Append(char.IsControl(character) ? '?' : character);
        }

        return builder.Length == 0 ? "<empty>" : builder.ToString();
    }

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

    private sealed record ParsedEntry(BatchManifestItem Item, string Path);
}

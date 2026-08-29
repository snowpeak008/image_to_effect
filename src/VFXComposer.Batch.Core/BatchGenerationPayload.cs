using System.Text;
using System.Text.Json;
using VFXComposer.AI.Contracts.Recipes;

namespace VFXComposer.Batch.Core;

/// <summary>
/// The opaque queue payload of one prompt entry. It is serialised with sorted keys and no
/// insignificant whitespace so that the same manifest entry always derives the same entry
/// idempotency key (REQ-002 §9.3). The payload holds the prompt text because the queue store is
/// current-user local data (REQ-003 §7.1); it must never reach output, logs or diagnostics.
/// </summary>
public static class BatchGenerationPayload
{
    public const string SchemaVersion = "vfxcomposer.generate-payload/1";

    /// <summary>Serialises one prompt entry into its canonical payload form.</summary>
    public static string Create(BatchManifestItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (!string.Equals(item.Kind, BatchItemKinds.Prompt, StringComparison.Ordinal) || item.Prompt is null)
        {
            throw new ArgumentException("Only prompt entries have a generation payload.", nameof(item));
        }

        var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = false }))
        {
            writer.WriteStartObject();
            writer.WriteStartObject("constraints");
            WriteConstraint(writer, BatchConstraintKeys.Archetype, item.Constraints.Archetype);
            WriteConstraint(writer, BatchConstraintKeys.Dimension, item.Constraints.Dimension);
            WriteConstraint(writer, BatchConstraintKeys.Element, item.Constraints.Element);
            if (item.Constraints.RandomSeed is int seed)
            {
                writer.WriteNumber(BatchConstraintKeys.RandomSeed, seed);
            }

            WriteConstraint(writer, BatchConstraintKeys.Style, item.Constraints.Style);
            WriteConstraint(writer, BatchConstraintKeys.TargetProfile, item.Constraints.TargetProfile);
            writer.WriteEndObject();
            writer.WriteString("prompt", item.Prompt);
            writer.WriteString("schemaVersion", SchemaVersion);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    /// <summary>Reads a payload back. An unknown schema or shape fails closed.</summary>
    public static BatchGenerationPayloadContent Parse(string payload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(payload);
        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("schemaVersion", out var schema) ||
            schema.ValueKind != JsonValueKind.String ||
            !string.Equals(schema.GetString(), SchemaVersion, StringComparison.Ordinal))
        {
            throw new InvalidDataException("The generation payload schema is not supported.");
        }

        if (!root.TryGetProperty("prompt", out var prompt) || prompt.ValueKind != JsonValueKind.String)
        {
            throw new InvalidDataException("The generation payload is missing its prompt.");
        }

        var constraints = BatchConstraints.Empty;
        if (root.TryGetProperty("constraints", out var constraintElement) &&
            constraintElement.ValueKind == JsonValueKind.Object)
        {
            constraints = ReadConstraints(constraintElement);
        }

        return new BatchGenerationPayloadContent(
            prompt.GetString() ?? throw new InvalidDataException("The generation payload prompt is null."),
            constraints);
    }

    /// <summary>
    /// Builds the effect description handed to the F1 channel: the prompt followed by a
    /// deterministic rendering of the whitelisted constraints, so the constraints the user wrote
    /// actually reach the model instead of being silently dropped.
    /// </summary>
    public static string ComposeDescription(string prompt, BatchConstraints constraints)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);
        ArgumentNullException.ThrowIfNull(constraints);
        var builder = new StringBuilder(prompt);
        var lines = new List<string>(6);
        AppendConstraint(lines, BatchConstraintKeys.Archetype, constraints.Archetype);
        AppendConstraint(lines, BatchConstraintKeys.Dimension, constraints.Dimension);
        AppendConstraint(lines, BatchConstraintKeys.Element, constraints.Element);
        AppendConstraint(lines, BatchConstraintKeys.Style, constraints.Style);
        AppendConstraint(lines, BatchConstraintKeys.TargetProfile, constraints.TargetProfile);
        if (constraints.RandomSeed is int seed)
        {
            AppendConstraint(
                lines,
                BatchConstraintKeys.RandomSeed,
                seed.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        if (lines.Count == 0)
        {
            return builder.ToString();
        }

        builder.Append("\n\nConstraints:");
        foreach (var line in lines)
        {
            builder.Append('\n').Append(line);
        }

        return builder.ToString();
    }

    /// <summary>UTF-8 byte length of the composed description, used by the manifest semantic check.</summary>
    public static int ComposedDescriptionByteCount(string prompt, BatchConstraints constraints) =>
        Encoding.UTF8.GetByteCount(ComposeDescription(prompt, constraints));

    /// <summary>Upper bound the composed description must respect (<see cref="RecipeChannelLimits"/>).</summary>
    public static int MaximumComposedDescriptionBytes => RecipeChannelLimits.MaximumDescriptionUtf8Bytes;

    private static void AppendConstraint(List<string> lines, string key, string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            lines.Add("- " + key + ": " + value);
        }
    }

    private static void WriteConstraint(Utf8JsonWriter writer, string key, string? value)
    {
        if (value is not null)
        {
            writer.WriteString(key, value);
        }
    }

    private static BatchConstraints ReadConstraints(JsonElement element) => new(
        ReadString(element, BatchConstraintKeys.Archetype),
        ReadString(element, BatchConstraintKeys.Dimension),
        ReadString(element, BatchConstraintKeys.Element),
        ReadString(element, BatchConstraintKeys.Style),
        ReadString(element, BatchConstraintKeys.TargetProfile),
        element.TryGetProperty(BatchConstraintKeys.RandomSeed, out var seed) && seed.ValueKind == JsonValueKind.Number
            ? seed.GetInt32()
            : null);

    private static string? ReadString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}

/// <summary>Decoded generation payload content.</summary>
public sealed record BatchGenerationPayloadContent(string Prompt, BatchConstraints Constraints)
{
    public override string ToString() => "BatchGenerationPayloadContent(<redacted>)";
}

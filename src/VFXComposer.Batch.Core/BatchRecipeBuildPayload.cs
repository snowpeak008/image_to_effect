using System.Text;
using System.Text.Json;
using VFXComposer.AI.Contracts.Recipes;

namespace VFXComposer.Batch.Core;

/// <summary>
/// The opaque queue payload of one recipe build entry. It carries the confirmed recipe text and the
/// canonical hash the confirmation is bound to, so the build survives the submitting process exiting
/// without ever trusting a filesystem path it did not verify itself.
///
/// <para><c>draftId</c> is present when the entry came from a confirmed Desktop draft, and absent when
/// it came from a batch manifest, where the batch submission itself is the build authorization
/// (REQ-002-21). Only the draft-backed form advances draft state.</para>
/// </summary>
public static class BatchRecipeBuildPayload
{
    public const string SchemaVersion = "vfxcomposer.recipe-build-payload/1";

    /// <summary>Builds the payload for a manifest recipe entry from the recipe file's text.</summary>
    public static string Create(BatchManifestItem item, string recipeJson)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (!string.Equals(item.Kind, BatchItemKinds.Recipe, StringComparison.Ordinal))
        {
            throw new ArgumentException("Only recipe entries have a build payload.", nameof(item));
        }

        return Create(draftId: null, recipeJson);
    }

    /// <summary>Builds the payload for a confirmed draft, or for a manifest entry when the id is null.</summary>
    public static string Create(string? draftId, string recipeJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recipeJson);
        if (recipeJson.Length > RecipeChannelLimits.MaximumDraftJsonCharacters)
        {
            throw new ArgumentException("The recipe exceeds the retained draft bound.", nameof(recipeJson));
        }

        var canonicalSha256 = RecipeCanonicalJson.ComputeSha256(recipeJson);
        var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = false }))
        {
            // Keys are written in ordinal order so the same entry always derives the same entry
            // idempotency key (REQ-002 §9.3).
            writer.WriteStartObject();
            writer.WriteString("canonicalSha256", canonicalSha256);
            if (draftId is not null)
            {
                writer.WriteString("draftId", draftId);
            }

            writer.WriteString("recipeJson", recipeJson);
            writer.WriteString("schemaVersion", SchemaVersion);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    /// <summary>Reads a payload back. An unknown schema or shape fails closed.</summary>
    public static BatchRecipeBuildPayloadContent Parse(string payload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(payload);
        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("schemaVersion", out var schema) ||
            schema.ValueKind != JsonValueKind.String ||
            !string.Equals(schema.GetString(), SchemaVersion, StringComparison.Ordinal))
        {
            throw new InvalidDataException("The recipe build payload schema is not supported.");
        }

        if (!root.TryGetProperty("recipeJson", out var recipe) || recipe.ValueKind != JsonValueKind.String ||
            !root.TryGetProperty("canonicalSha256", out var hash) || hash.ValueKind != JsonValueKind.String)
        {
            throw new InvalidDataException("The recipe build payload is incomplete.");
        }

        string? draftId = null;
        if (root.TryGetProperty("draftId", out var draft))
        {
            if (draft.ValueKind != JsonValueKind.String)
            {
                throw new InvalidDataException("The recipe build payload draft id is invalid.");
            }

            draftId = draft.GetString();
        }

        return new BatchRecipeBuildPayloadContent(
            draftId,
            recipe.GetString() ?? throw new InvalidDataException("The recipe build payload recipe is null."),
            hash.GetString() ?? throw new InvalidDataException("The recipe build payload hash is null."));
    }
}

/// <summary>Decoded build payload content. It is never echoed into logs, diagnostics or reports.</summary>
public sealed record BatchRecipeBuildPayloadContent(string? DraftId, string RecipeJson, string CanonicalSha256)
{
    public override string ToString() => "BatchRecipeBuildPayloadContent(<redacted>)";
}

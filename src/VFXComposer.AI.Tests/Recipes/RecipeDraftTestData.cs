using System.Globalization;
using System.Text.Json.Nodes;
using VFXComposer.AI.Contracts;
using VFXComposer.AI.Contracts.Recipes;
using VFXComposer.AI.Providers.Recipes;

namespace VFXComposer.AI.Tests.Recipes;

/// <summary>
/// Shared builders for the F8b2 version-chain tests. The prompt version is a fixed short-text literal so these
/// tests never depend on the prompt assembler surface or its composite version string.
/// </summary>
internal static class RecipeDraftTestData
{
    public const string PromptVersion = "vfxcomposer.ai.recipe-prompt-test/1";

    public static string CatalogVersion => RecipeTemplateCatalogSnapshot.Default.TemplateCatalogVersion;

    /// <summary>A real-size recipe document distinguished from every other variant by one extra top-level field.</summary>
    public static string RealRecipeJson(int variant)
    {
        var presets = RecipePresetSkeletons.All;
        var source = variant % (presets.Count + 1) == presets.Count
            ? RecipeTemplateCatalogSnapshot.Default.CanonicalExampleJson
            : presets[variant % (presets.Count + 1)].RecipeJson;
        var node = JsonNode.Parse(source)!.AsObject();
        node["variant"] = variant;
        return node.ToJsonString();
    }

    /// <summary>A syntactically valid JSON object whose string length is exactly <paramref name="characters"/>.</summary>
    public static string PaddedRecipeJson(int characters, int variant)
    {
        const string prefix = "{\"pad\":\"";
        var suffix = "\",\"variant\":" + variant.ToString(CultureInfo.InvariantCulture) + "}";
        var fill = characters - prefix.Length - suffix.Length;
        if (fill < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(characters));
        }

        return prefix + new string('x', fill) + suffix;
    }

    public static RecipeDraft Draft(string recipeJson, string? correlationId = null) => new(
        correlationId ?? "corr-" + Guid.NewGuid().ToString("N"),
        recipeJson,
        RecipeCanonicalJson.ComputeSha256(recipeJson),
        "spark_projectile_2d",
        "projectile",
        "2d",
        "mobile_medium",
        PromptVersion,
        CatalogVersion);

    public static RecipeGenerationResult DraftedResult(int variant = 0) =>
        RecipeGenerationResult.Drafted(Draft(RealRecipeJson(variant)), [new RecipeGenerationAttempt(1, [])]);

    public static RecipeGenerationResult FailedResult(int issueCount = 1)
    {
        var issues = Enumerable.Range(0, issueCount).Select(index => new RecipeValidationIssue(
            "E101",
            RecipeValidationSeverity.Error,
            "/stages/" + index.ToString(CultureInfo.InvariantCulture),
            "Missing required field: stages"));
        return RecipeGenerationResult.ValidationFailed(
            "corr-" + Guid.NewGuid().ToString("N"),
            "{}",
            issues,
            [new RecipeGenerationAttempt(1, ["E101"])],
            PromptVersion,
            CatalogVersion);
    }

    /// <summary>A lineage root of the requested origin; AI roots go through the production factory.</summary>
    public static RecipeDraftRecord Root(
        RecipeDraftOrigin origin,
        DateTimeOffset? createdUtc = null,
        string? recipeJson = null,
        RecipeDraftStatus status = RecipeDraftStatus.PendingConfirmation)
    {
        var created = createdUtc ?? DateTimeOffset.UtcNow;
        if (origin == RecipeDraftOrigin.Preset && recipeJson is null)
        {
            return RecipePresetSkeletons.All[0].CreateDraftRecord(created);
        }

        var draft = Draft(recipeJson ?? RealRecipeJson(0));
        var provenance = origin == RecipeDraftOrigin.Preset
            ? RecipeDraftProvenance.Root(RecipeDraftProvenance.NewLineageId(), origin, RecipePresetSkeletons.All[0].PresetId)
            : RecipeDraftProvenance.Root(RecipeDraftProvenance.NewLineageId(), origin);
        return new RecipeDraftRecord(
            RecipeDraftRecord.NewDraftId(),
            status,
            created,
            created,
            draft.CorrelationId,
            draft.PromptTemplateVersion,
            draft.TemplateCatalogVersion,
            draft.RecipeJson,
            draft.CanonicalSha256,
            draft.RecipeId,
            draft.Archetype,
            draft.Dimension,
            draft.TargetProfile,
            Array.Empty<RecipeValidationIssue>(),
            requestCount: 1,
            provenance);
    }

    public static RecipeDraftRevision Revision(
        RecipeDraftOrigin origin,
        string? recipeJson = null,
        string? feedbackText = null,
        IEnumerable<RecipeGuardRestoration>? guardRestorations = null,
        int variant = 1)
    {
        var draft = Draft(recipeJson ?? RealRecipeJson(variant));
        return origin switch
        {
            RecipeDraftOrigin.AiRefine => new RecipeDraftRevision(
                draft,
                origin,
                requestCount: 1,
                feedbackText ?? "make the trail shorter",
                guardRestorations),
            _ => new RecipeDraftRevision(draft, origin, requestCount: 0, feedbackText, guardRestorations),
        };
    }

    /// <summary>Appends one pending version after the current head of <paramref name="head"/>'s lineage.</summary>
    public static RecipeDraftSaveOutcome Append(
        RecipeDraftStore store,
        RecipeDraftRecord head,
        RecipeDraftOrigin origin = RecipeDraftOrigin.HumanEdit,
        string? recipeJson = null,
        DateTimeOffset? createdUtc = null,
        int variant = 1) =>
        store.AppendVersion(
            head.DraftId,
            head.CanonicalSha256!,
            Revision(origin, recipeJson, variant: variant),
            createdUtc ?? DateTimeOffset.UtcNow);

    /// <summary>Grows a lineage until it holds <paramref name="totalVersions"/> versions, returning them oldest first.</summary>
    public static List<RecipeDraftRecord> Grow(RecipeDraftStore store, RecipeDraftRecord root, int totalVersions)
    {
        var versions = new List<RecipeDraftRecord> { root };
        while (versions.Count < totalVersions)
        {
            var outcome = Append(store, versions[^1], variant: versions.Count);
            Assert.IsTrue(outcome.RetainedEverything, "Growing to the cap must not trim anything.");
            versions.Add(outcome.Record);
        }

        return versions;
    }

    /// <summary>
    /// The REQ-004-34 invariant by construction: one root, every other version parented to its ordinal
    /// predecessor, ordinals strictly increasing, one lineage identifier throughout.
    /// </summary>
    public static void AssertLinear(IReadOnlyList<RecipeDraftRecord> lineage)
    {
        Assert.IsTrue(lineage.Count > 0, "A lineage under test must retain at least its head.");
        var lineageId = lineage[0].LineageId;
        var known = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < lineage.Count; index++)
        {
            var version = lineage[index];
            Assert.AreEqual(lineageId, version.LineageId);
            if (index == 0)
            {
                Assert.IsNull(version.ParentDraftId, "The oldest retained version is the root.");
            }
            else
            {
                Assert.AreEqual(lineage[index - 1].DraftId, version.ParentDraftId, "Every version points at its retained predecessor.");
                Assert.IsTrue(version.RevisionOrdinal > lineage[index - 1].RevisionOrdinal, "Ordinals are strictly increasing.");
                Assert.IsTrue(known.Contains(version.ParentDraftId!), "A parent must be a retained record of the same lineage.");
            }

            Assert.IsTrue(known.Add(version.DraftId), "Draft identifiers are unique.");
        }
    }

    public static RecipeDraftStoreException Throws(RecipeDraftStoreErrorCode expected, Action action, string? message = null)
    {
        var exception = Assert.ThrowsExactly<RecipeDraftStoreException>(action, message ?? string.Empty);
        Assert.AreEqual(expected, exception.Code, message ?? string.Empty);
        return exception;
    }

    public static string StorePath(A1TestDirectory directory) => Path.Combine(directory.Path, "recipe-drafts.json");
}

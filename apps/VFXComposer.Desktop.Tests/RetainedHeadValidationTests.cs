using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VFXComposer.AI.Contracts;
using VFXComposer.AI.Contracts.Chat;
using VFXComposer.AI.Contracts.Desktop;
using VFXComposer.AI.Contracts.Recipes;
using VFXComposer.AI.Providers.Recipes;
using VFXComposer.Desktop.Localization;
using VFXComposer.Desktop.ViewModels;

namespace VFXComposer.Desktop.Tests;

/// <summary>
/// F8b3b remainder N7: the verdict box runs the same L1.5 prevalidation on every retained head. Before F8b4 the
/// AI-success and example-card paths asserted a bare pass while the revert path re-derived warnings; now a drafted
/// recipe that is structurally valid but violates the strict red line shows its L1.5 findings at once, and a clean
/// one still shows the pass line — through the shared prevalidator on both paths.
/// </summary>
[TestClass]
public sealed class RetainedHeadValidationTests
{
    [TestMethod]
    public async Task AnAiDraftWithStrictWarningsShowsItsL15FindingsInsteadOfABarePass()
    {
        // The mock recipe is L1-valid (the channel guaranteed that) but exceeds the strict module budget, so the
        // prevalidator reports it; before N7 this surface claimed "passed" until the first hand edit.
        var recipe = FireBoltWith(static root =>
        {
            var travelModules = root["stages"]![1]!["modules"]!.AsArray();
            travelModules.Add(new JsonObject
            {
                ["id"] = "trail",
                ["kind"] = "motion_trail",
                ["templateId"] = "PFT_2D_FireTrail",
                ["parameters"] = new JsonObject { ["time"] = 0.22, ["width"] = 0.42 },
                ["enabled"] = true,
            });
            root["stages"]![2]!["modules"]!.AsArray().Add(new JsonObject
            {
                ["id"] = "burst",
                ["kind"] = "impact_burst",
                ["templateId"] = "PFT_2D_FireImpact",
                ["parameters"] = new JsonObject { ["count"] = 24, ["speed"] = 3.5 },
                ["enabled"] = true,
            });
        });
        var runtime = new DraftingRuntime(request => Drafted(request, recipe));
        var viewModel = new CreateViewModel(LocalizationTestSupport.CreateEnglish(), runtime);
        viewModel.EffectDescription = "a synthetic fire bolt";

        await viewModel.GenerateRecipeCommand.ExecuteAsync(null);

        Assert.AreNotEqual(
            LocalizationTestSupport.English(UiStringKeys.CreateValidationPassed),
            viewModel.RecipeValidationSummary,
            "A strict violation must not render as a bare pass (N7).");
        StringAssert.Contains(viewModel.RecipeValidationSummary, RecipePrevalidationCodes.ModuleBudgetExceeded);
        StringAssert.Contains(
            viewModel.RecipeValidationSummary,
            LocalizationTestSupport.English(UiStringKeys.RecipeSuggestionReduceModuleCount),
            "The finding carries its bilingual repair suggestion like every other L1.5 surface.");
        Assert.AreEqual(RecipeDraftStatus.PendingConfirmation, viewModel.DraftStatus, "L1.5 warns; it does not block.");
        Assert.IsTrue(viewModel.ConfirmRecipeDraftCommand.CanExecute(null));
    }

    [TestMethod]
    public async Task ACleanAiDraftStillShowsThePassLine()
    {
        var runtime = new DraftingRuntime(static request => Drafted(request, FireBoltJson));
        var viewModel = new CreateViewModel(LocalizationTestSupport.CreateEnglish(), runtime);
        viewModel.EffectDescription = "a synthetic fire bolt";

        await viewModel.GenerateRecipeCommand.ExecuteAsync(null);

        Assert.AreEqual(
            LocalizationTestSupport.English(UiStringKeys.CreateValidationPassed),
            viewModel.RecipeValidationSummary);
    }

    [TestMethod]
    public void AnAppliedExampleCardShowsThePassLineThroughTheSharedPrevalidator()
    {
        // Committed skeletons pass L1.5 strictly by their build-time tests; the page now proves it per click
        // instead of asserting it.
        var runtime = new DraftingRuntime(static _ => throw new AssertFailedException("A card click never generates."));
        var viewModel = new CreateViewModel(LocalizationTestSupport.CreateEnglish(), runtime);

        foreach (var card in viewModel.PresetCards)
        {
            viewModel.ApplyPresetCommand.Execute(card);
            Assert.AreEqual(
                LocalizationTestSupport.English(UiStringKeys.CreateValidationPassed),
                viewModel.RecipeValidationSummary,
                card.Skeleton.PresetId);
        }
    }

    private static string FireBoltJson => RecipePresetSkeletons.All
        .Single(static skeleton => skeleton.PresetId == "fire-bolt").RecipeJson;

    private static string FireBoltWith(Action<JsonObject> mutate)
    {
        var root = JsonNode.Parse(FireBoltJson)!.AsObject();
        mutate(root);
        return root.ToJsonString();
    }

    private static RecipeGenerationResult Drafted(RecipeGenerationRequest request, string recipeJson)
    {
        var canonical = RecipeCanonicalJson.Canonicalize(recipeJson);
        var draft = new RecipeDraft(
            request.CorrelationId,
            canonical,
            RecipeCanonicalJson.ComputeSha256(canonical),
            "preset_fire_bolt_2d",
            "projectile",
            "2d",
            "mobile_medium",
            "prompt/1",
            RecipeTemplateCatalogSnapshot.Default.TemplateCatalogVersion);
        return RecipeGenerationResult.Drafted(draft, [new RecipeGenerationAttempt(1, [])]);
    }

    /// <summary>An in-memory lineage save behind a mock generation channel; nothing else is reachable.</summary>
    private sealed class DraftingRuntime : IAiDesktopRuntime, IRecipeGenerationChannel, IRecipeDraftLineageStore
    {
        private readonly Func<RecipeGenerationRequest, RecipeGenerationResult> _nextResult;
        private readonly Dictionary<string, RecipeDraftRecord> _records = new(StringComparer.Ordinal);

        public DraftingRuntime(Func<RecipeGenerationRequest, RecipeGenerationResult> nextResult)
        {
            _nextResult = nextResult;
        }

        public IAiGateway Gateway => throw new NotSupportedException();
        public IAiDesktopSettings Settings => throw new NotSupportedException();
        public IRecipeGenerationChannel RecipeGeneration => this;
        public IRecipeDraftLineageStore RecipeDrafts => this;

        public ValueTask<RecipeGenerationResult> GenerateAsync(
            RecipeGenerationRequest request,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(_nextResult(request));

        public RecipeDraftSaveOutcome SaveVersion(RecipeDraftRecord record)
        {
            _records[record.DraftId] = record;
            return new RecipeDraftSaveOutcome(record, [], [], [], 0);
        }

        public RecipeDraftRecord? TryGet(string draftId) =>
            _records.TryGetValue(draftId, out var record) ? record : null;

        public IReadOnlyList<RecipeDraftRecord> ListLineage(string lineageId) => _records.Values
            .Where(record => string.Equals(record.LineageId, lineageId, StringComparison.Ordinal))
            .OrderBy(static record => record.RevisionOrdinal)
            .ToArray();

        public IReadOnlyList<RecipeDraftRecord> ListConfirmedAwaitingBuild() => [];

        public RecipeDraftSaveOutcome AppendVersion(
            string parentDraftId,
            string parentCanonicalSha256,
            RecipeDraftRevision revision,
            DateTimeOffset createdUtc) =>
            throw new NotSupportedException("These tests only exercise a root save.");

        public RecipeDraftTruncateOutcome TruncateAfter(string draftId) => throw new NotSupportedException();

        public RecipeDraftRecord Save(RecipeDraftRecord record) => throw new NotSupportedException();

        public RecipeDraftRecord Confirm(string draftId, string canonicalSha256) => throw new NotSupportedException();

        public RecipeDraftRecord MarkBuilt(string draftId, string canonicalSha256) => throw new NotSupportedException();

        public RecipeDraftRecord MarkBuildFailed(string draftId, string canonicalSha256) => throw new NotSupportedException();

        public ValueTask<Stream> OpenImageArtifactAsync(string privateArtifactId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

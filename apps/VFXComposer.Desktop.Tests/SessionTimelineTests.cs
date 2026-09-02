using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VFXComposer.AI.Contracts.Chat;
using VFXComposer.AI.Contracts.Recipes;
using VFXComposer.AI.Providers.Recipes;
using VFXComposer.Desktop.Localization;
using VFXComposer.Desktop.Services;
using VFXComposer.Desktop.ViewModels;

namespace VFXComposer.Desktop.Tests;

/// <summary>
/// The session timeline (F8b4, REQ-004-20/21): every round appends one entry carrying its request count, stable
/// error-code sequence, L1.5 codes, guard restoration count and paths, the resulting version id with origin and
/// the prompt template version; trims fold into the entry of the save that caused them; and no entry ever carries
/// feedback text, the user description, prompt content or an endpoint (redaction negative). The timeline is
/// session-scoped by ruling: nothing here is persisted, and a language switch re-renders the entries in place.
/// </summary>
[TestClass]
public sealed class SessionTimelineTests
{
    private const string FeedbackMarker = "UNIQUE-FEEDBACK-MARKER-93f1";
    private const string DescriptionMarker = "UNIQUE-DESCRIPTION-MARKER-71c4";

    private string _storeDirectory = string.Empty;

    [TestInitialize]
    public void CreateStoreDirectory() => _storeDirectory = Path.Combine(
        Path.GetTempPath(),
        "vfxcomposer-session-timeline-tests",
        Guid.NewGuid().ToString("N"));

    [TestCleanup]
    public void RemoveStoreDirectory()
    {
        if (Directory.Exists(_storeDirectory))
        {
            Directory.Delete(_storeDirectory, recursive: true);
        }
    }

    [TestMethod]
    public async Task GenerationRefinementEditRevertAndConfirmationEachAppendOneEntry()
    {
        var runtime = CreateRuntime(request => RefinedResult(request, WithScale(request.Head.RecipeJson, "1.8")));
        runtime.NextGeneration = DraftedFireBolt;
        var viewModel = NewCreatePage(runtime);
        Assert.IsFalse(viewModel.Timeline.HasEntries);

        // Round 1: generation with one repair retry (2 requests, E101 on the first).
        viewModel.EffectDescription = "a synthetic fire bolt";
        await viewModel.GenerateRecipeCommand.ExecuteAsync(null);
        Assert.AreEqual(1, viewModel.Timeline.Entries.Count);
        var generationEntry = viewModel.Timeline.Entries[0].Text;
        StringAssert.Contains(generationEntry, viewModel.DraftId!, "The entry names the resulting version id.");
        StringAssert.Contains(generationEntry, RecipeDraftOriginNames.AiDraft, "The entry names the origin.");
        StringAssert.Contains(generationEntry, "2 request(s)");
        StringAssert.Contains(generationEntry, "1:[E101]; 2:[]", "The per-request stable-code sequence.");
        StringAssert.Contains(generationEntry, "prompt/1", "The prompt template version rides along.");

        // Round 2: refinement.
        viewModel.RefineFeedback = "make it bigger";
        await viewModel.RefineRecipeCommand.ExecuteAsync(null);
        Assert.AreEqual(2, viewModel.Timeline.Entries.Count);
        var refineEntry = viewModel.Timeline.Entries[1].Text;
        StringAssert.Contains(refineEntry, viewModel.DraftId!);
        StringAssert.Contains(refineEntry, RecipeDraftOriginNames.AiRefine);
        StringAssert.Contains(refineEntry, "prompt/refine-tests");

        // Round 3: hand edit.
        ScaleRow(viewModel).EditText = "1.2";
        viewModel.ApplyParameterEditsCommand.Execute(null);
        Assert.AreEqual(3, viewModel.Timeline.Entries.Count);
        var editEntry = viewModel.Timeline.Entries[2].Text;
        StringAssert.Contains(editEntry, viewModel.DraftId!);
        StringAssert.Contains(editEntry, "v3");

        // Round 4: revert to v2 (deletes v3).
        var v2 = viewModel.Lineage.Versions.Single(static version => version.RevisionOrdinal == 2);
        viewModel.Lineage.SelectedVersion = v2;
        viewModel.RevertToSelectedVersionCommand.Execute(null);
        viewModel.ConfirmRevertCommand.Execute(null);
        Assert.AreEqual(4, viewModel.Timeline.Entries.Count);
        var revertEntry = viewModel.Timeline.Entries[3].Text;
        StringAssert.Contains(revertEntry, "v2");
        StringAssert.Contains(revertEntry, "1", "One newer version was deleted.");

        // Round 5: confirmation.
        viewModel.ConfirmRecipeDraftCommand.Execute(null);
        Assert.AreEqual(5, viewModel.Timeline.Entries.Count);
        StringAssert.Contains(viewModel.Timeline.Entries[4].Text, viewModel.DraftId!);

        Assert.IsTrue(viewModel.IsTimelineVisible, "Professional mode with entries shows the card.");
    }

    [TestMethod]
    public async Task NoEntryEverCarriesFeedbackDescriptionPromptOrEndpointText()
    {
        // REQ-004-21 negative: marker strings placed into the free-text inputs never surface in the timeline.
        var runtime = CreateRuntime(request => RefinedResult(request, WithScale(request.Head.RecipeJson, "1.8")));
        runtime.NextGeneration = DraftedFireBolt;
        var viewModel = NewCreatePage(runtime);

        viewModel.EffectDescription = DescriptionMarker + " a fire bolt description";
        await viewModel.GenerateRecipeCommand.ExecuteAsync(null);
        viewModel.RefineFeedback = FeedbackMarker + " make the core bigger";
        await viewModel.RefineRecipeCommand.ExecuteAsync(null);
        ScaleRow(viewModel).EditText = "1.2";
        viewModel.ApplyParameterEditsCommand.Execute(null);
        viewModel.ConfirmRecipeDraftCommand.Execute(null);
        Assert.AreEqual(4, viewModel.Timeline.Entries.Count);

        var persisted = runtime.Store.TryGet(viewModel.Lineage.Versions[1].DraftId)!;
        StringAssert.Contains(persisted.FeedbackText!, FeedbackMarker, "The store may keep the feedback (REQ-004-21)...");
        foreach (var entry in viewModel.Timeline.Entries)
        {
            Assert.IsFalse(entry.Text.Contains(FeedbackMarker, StringComparison.Ordinal), "...but the timeline may not: " + entry.Text);
            Assert.IsFalse(entry.Text.Contains(DescriptionMarker, StringComparison.Ordinal), entry.Text);
            Assert.IsFalse(entry.Text.Contains("http", StringComparison.OrdinalIgnoreCase), entry.Text);
            Assert.IsFalse(entry.Text.Contains("You are", StringComparison.Ordinal), "No prompt content: " + entry.Text);
        }
    }

    [TestMethod]
    public void ATrimFoldsIntoTheEntryOfTheSaveThatCausedIt()
    {
        // REQ-004-33 timeline half: the save outcome's trim counts land inside the causing entry, not silently.
        var runtime = new FoldingSaveRuntime(
            supersededDraftIds: ["draft-superseded"],
            trimmedDraftIds: ["draft-a", "draft-b"],
            evictedLineageIds: ["lineage-old"],
            evictedVersionCount: 5);
        var viewModel = new CreateViewModel(
            LocalizationTestSupport.CreateEnglish(),
            runtime,
            new GenerationModeService(GenerationMode.Professional));

        viewModel.ApplyPresetCommand.Execute(FireBoltCard(viewModel));

        var text = viewModel.Timeline.Entries.Single().Text;
        StringAssert.Contains(
            text,
            LocalizationTestSupport.EnglishFormat(UiStringKeys.CreateTimelineRetentionLine, 1, 2, 1, 5));

        // A fully retained save folds nothing.
        runtime.RetainEverything();
        viewModel.ApplyPresetCommand.Execute(FireBoltCard(viewModel));
        var retentionPrefix = LocalizationTestSupport.English(UiStringKeys.CreateTimelineRetentionLine).Split('{')[0];
        Assert.IsFalse(viewModel.Timeline.Entries[1].Text.Contains(retentionPrefix, StringComparison.Ordinal));
    }

    [TestMethod]
    public void EntriesRerenderInPlaceOnALanguageSwitch()
    {
        var localization = LocalizationTestSupport.CreateEnglish();
        var runtime = new FoldingSaveRuntime([], [], [], 0);
        var viewModel = new CreateViewModel(
            localization,
            runtime,
            new GenerationModeService(GenerationMode.Professional));
        viewModel.ApplyPresetCommand.Execute(FireBoltCard(viewModel));
        var entry = viewModel.Timeline.Entries.Single();
        var draftId = viewModel.DraftId!;
        Assert.AreEqual(
            LocalizationTestSupport.EnglishFormat(UiStringKeys.CreateTimelineEntryPresetApplied, draftId, "fire-bolt"),
            entry.Text);

        localization.SetLanguage(UiLanguage.ChineseSimplified);

        Assert.AreEqual(
            LocalizationTestSupport.ChineseSimplifiedFormat(UiStringKeys.CreateTimelineEntryPresetApplied, draftId, "fire-bolt"),
            entry.Text,
            "The entry re-renders; it is not baked at append time.");
        StringAssert.Contains(entry.Text, draftId, "The version id is a carrier and stays verbatim.");
    }

    private string StorePath => Path.Combine(_storeDirectory, "recipe-drafts.json");

    private RefinementFlowTests.RefiningRuntime CreateRuntime(
        Func<RecipeRefinementRequest, RecipeRefinementResult> nextRefinement) => new(StorePath, nextRefinement);

    private static CreateViewModel NewCreatePage(RefinementFlowTests.RefiningRuntime runtime) => new(
        LocalizationTestSupport.CreateEnglish(),
        runtime,
        new GenerationModeService(GenerationMode.Professional));

    private static ParameterRowViewModel ScaleRow(CreateViewModel viewModel) => viewModel.ParameterPanel.Modules
        .Single(static module => module.ModuleId == "core")
        .Parameters.Single(static row => row.Name == "scale");

    private static string FireBoltJson => RecipePresetSkeletons.All
        .Single(static skeleton => skeleton.PresetId == "fire-bolt").RecipeJson;

    private static PresetCardViewModel FireBoltCard(CreateViewModel viewModel) =>
        viewModel.PresetCards.Single(static card => card.Skeleton.PresetId == "fire-bolt");

    /// <summary>
    /// An in-memory root save whose outcome reports configurable retention counts, so the timeline's folding can be
    /// pinned without growing a real store past its caps. Nothing else on this runtime is reachable.
    /// </summary>
    private sealed class FoldingSaveRuntime : VFXComposer.AI.Contracts.Desktop.IAiDesktopRuntime, IRecipeDraftLineageStore
    {
        private IReadOnlyList<string> _supersededDraftIds;
        private IReadOnlyList<string> _trimmedDraftIds;
        private IReadOnlyList<string> _evictedLineageIds;
        private int _evictedVersionCount;
        private RecipeDraftRecord? _saved;

        public FoldingSaveRuntime(
            IReadOnlyList<string> supersededDraftIds,
            IReadOnlyList<string> trimmedDraftIds,
            IReadOnlyList<string> evictedLineageIds,
            int evictedVersionCount)
        {
            _supersededDraftIds = supersededDraftIds;
            _trimmedDraftIds = trimmedDraftIds;
            _evictedLineageIds = evictedLineageIds;
            _evictedVersionCount = evictedVersionCount;
        }

        public void RetainEverything()
        {
            _supersededDraftIds = [];
            _trimmedDraftIds = [];
            _evictedLineageIds = [];
            _evictedVersionCount = 0;
        }

        public VFXComposer.AI.Contracts.IAiGateway Gateway => throw new NotSupportedException("The timeline never reaches the gateway.");
        public VFXComposer.AI.Contracts.Desktop.IAiDesktopSettings Settings => throw new NotSupportedException();
        public IRecipeGenerationChannel RecipeGeneration => throw new NotSupportedException("The timeline never generates.");
        public IRecipeDraftLineageStore RecipeDrafts => this;

        public RecipeDraftSaveOutcome SaveVersion(RecipeDraftRecord record)
        {
            _saved = record;
            return new RecipeDraftSaveOutcome(record, _supersededDraftIds, _trimmedDraftIds, _evictedLineageIds, _evictedVersionCount);
        }

        public RecipeDraftRecord? TryGet(string draftId) =>
            _saved is not null && string.Equals(_saved.DraftId, draftId, StringComparison.Ordinal) ? _saved : null;

        public IReadOnlyList<RecipeDraftRecord> ListLineage(string lineageId) =>
            _saved is not null && string.Equals(_saved.LineageId, lineageId, StringComparison.Ordinal) ? [_saved] : [];

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

    private static string WithScale(string recipeJson, string literal)
    {
        var root = JsonNode.Parse(recipeJson)!.AsObject();
        foreach (var stage in root["stages"]!.AsArray())
        {
            foreach (var module in stage!["modules"]!.AsArray())
            {
                if (string.Equals(module!["id"]!.GetValue<string>(), "core", StringComparison.Ordinal))
                {
                    module["parameters"]!.AsObject()["scale"] = JsonNode.Parse(literal);
                }
            }
        }

        return RecipeCanonicalJson.Canonicalize(root.ToJsonString());
    }

    private static RecipeRefinementResult RefinedResult(RecipeRefinementRequest request, string refinedJson)
    {
        var head = request.Head;
        var draft = new RecipeDraft(
            request.CorrelationId,
            refinedJson,
            RecipeCanonicalJson.ComputeSha256(refinedJson),
            head.RecipeId ?? "recipe",
            head.Archetype ?? "projectile",
            head.Dimension ?? "2d",
            head.TargetProfile ?? "mobile_medium",
            "prompt/refine-tests",
            head.TemplateCatalogVersion);
        return RecipeRefinementResult.Refined(
            draft,
            head.DraftId,
            head.CanonicalSha256!,
            request.FeedbackText,
            [],
            [new RecipeGenerationAttempt(1, [])]);
    }

    /// <summary>A drafted generation that consumed one repair retry, so the entry has a code sequence to show.</summary>
    private static RecipeGenerationResult DraftedFireBolt(RecipeGenerationRequest request)
    {
        var canonical = RecipeCanonicalJson.Canonicalize(FireBoltJson);
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
        return RecipeGenerationResult.Drafted(
            draft,
            [new RecipeGenerationAttempt(1, ["E101"]), new RecipeGenerationAttempt(2, [])]);
    }
}

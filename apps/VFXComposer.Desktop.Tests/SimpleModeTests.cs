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
/// Simple-mode behavior (F8a2): applying an example card persists the committed skeleton as a pending draft
/// with zero AI involvement, the capability and scope lines derive from the snapshot, suggestion sentences only
/// fill the input, and the honest build handoff carries a copyable command.
/// </summary>
[TestClass]
public sealed class SimpleModeTests
{
    [TestMethod]
    public void ThePageCarriesOneCardPerCommittedPresetInOrder()
    {
        var viewModel = CreateViewModelWith(new CountingRuntime());

        CollectionAssert.AreEqual(
            RecipePresetSkeletons.All.Select(static skeleton => skeleton.PresetId).ToArray(),
            viewModel.PresetCards.Select(static card => card.Skeleton.PresetId).ToArray());
        foreach (var card in viewModel.PresetCards)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(card.Title), card.Skeleton.PresetId + " renders no title.");
            Assert.IsFalse(string.IsNullOrWhiteSpace(card.Description), card.Skeleton.PresetId + " renders no description.");
        }
    }

    [TestMethod]
    public void ApplyingACardPersistsAPendingDraftWithoutAnyGenerationCall()
    {
        var runtime = new CountingRuntime();
        var viewModel = CreateViewModelWith(runtime);
        var card = viewModel.PresetCards[0];

        viewModel.ApplyPresetCommand.Execute(card);

        Assert.AreEqual(0, runtime.GenerateCalls, "A card click must never construct a prompt or send a request.");
        Assert.AreEqual(1, runtime.SaveCalls);
        Assert.AreEqual(RecipeDraftStatus.PendingConfirmation, viewModel.DraftStatus);
        Assert.IsTrue(viewModel.ConfirmRecipeDraftCommand.CanExecute(null));
        Assert.IsTrue(viewModel.RecipeDraftJson.Contains(card.Skeleton.RecipeId, StringComparison.Ordinal));
        Assert.AreEqual(
            LocalizationTestSupport.English(UiStringKeys.CreateRecipeStatusPresetApplied),
            viewModel.RecipeStatus);

        var retained = runtime.Records[viewModel.DraftId!];
        Assert.AreEqual(card.Skeleton.CanonicalSha256, retained.CanonicalSha256);
        Assert.AreEqual(RecipePresetSkeleton.PresetPromptTemplateVersion, retained.PromptTemplateVersion);
        Assert.AreEqual(0, retained.RequestCount);
        Assert.AreEqual(RecipeDraftOrigin.Preset, retained.Origin, "A card click must be recorded as a preset lineage root.");
        Assert.AreEqual(card.Skeleton.PresetId, retained.PresetId);
        Assert.IsNull(retained.ParentDraftId);
    }

    [TestMethod]
    public void APresetDraftConfirmsThroughTheOrdinaryHashBoundFlow()
    {
        var runtime = new CountingRuntime();
        var viewModel = CreateViewModelWith(runtime);

        viewModel.ApplyPresetCommand.Execute(viewModel.PresetCards[1]);
        viewModel.ConfirmRecipeDraftCommand.Execute(null);

        Assert.AreEqual(RecipeDraftStatus.ConfirmedAwaitingBuild, viewModel.DraftStatus);
        Assert.AreEqual(
            RecipeDraftStatus.ConfirmedAwaitingBuild,
            runtime.Records[viewModel.DraftId!].Status);
        Assert.AreEqual(0, runtime.GenerateCalls);
    }

    [TestMethod]
    public void EveryCardClickCreatesAFreshDraftIdentity()
    {
        var runtime = new CountingRuntime();
        var viewModel = CreateViewModelWith(runtime);
        var card = viewModel.PresetCards[0];

        viewModel.ApplyPresetCommand.Execute(card);
        var firstDraftId = viewModel.DraftId;
        viewModel.ApplyPresetCommand.Execute(card);

        Assert.AreNotEqual(firstDraftId, viewModel.DraftId);
        Assert.AreEqual(2, runtime.Records.Count);
    }

    [TestMethod]
    public void AStoreFailureOnCardApplySurfacesTheStableCodeAndRetainsNothing()
    {
        var runtime = new CountingRuntime { ThrowOnSave = true };
        var viewModel = CreateViewModelWith(runtime);

        viewModel.ApplyPresetCommand.Execute(viewModel.PresetCards[0]);

        Assert.IsTrue(viewModel.RecipeStatus.Contains("StorageFailed", StringComparison.Ordinal));
        Assert.IsNull(viewModel.DraftStatus);
        Assert.IsFalse(viewModel.ConfirmRecipeDraftCommand.CanExecute(null));
    }

    [TestMethod]
    public void TheCapabilityAndScopeLinesDeriveFromTheCommittedSnapshot()
    {
        var snapshot = RecipeTemplateCatalogSnapshot.Default;
        var viewModel = CreateViewModelWith(new CountingRuntime());

        // Every number and enumeration comes from the snapshot; the test recomputes them from the same source
        // so a snapshot re-export moves the rendered line without touching this test (REQ-004-04).
        StringAssert.Contains(viewModel.CapabilityLine, snapshot.Templates.Count.ToString());
        StringAssert.Contains(
            viewModel.CapabilityLine,
            snapshot.Templates.Sum(static template => template.Parameters.Count).ToString());
        StringAssert.Contains(viewModel.CapabilityLine, string.Join(", ", snapshot.BuildableArchetypes));
        StringAssert.Contains(viewModel.CapabilityLine, string.Join(", ", snapshot.BuildableDimensions));
        StringAssert.Contains(viewModel.CapabilityLine, snapshot.TemplateCatalogVersion);
        StringAssert.Contains(viewModel.CapabilityLine, snapshot.ContractRevision);

        StringAssert.Contains(viewModel.ScopeNotice, string.Join(", ", snapshot.BuildableDimensions));
        StringAssert.Contains(viewModel.ScopeNotice, string.Join(", ", snapshot.BuildableArchetypes));
    }

    [TestMethod]
    public void AClickedSuggestionOnlyFillsTheDescriptionBox()
    {
        var runtime = new CountingRuntime();
        var viewModel = CreateViewModelWith(runtime);
        var sentence = viewModel.SuggestionSentences[0];

        viewModel.UseSuggestionCommand.Execute(sentence);

        Assert.AreEqual(sentence, viewModel.EffectDescription);
        Assert.AreEqual(0, runtime.GenerateCalls, "Clicking a suggestion must not trigger generation.");
        Assert.AreEqual(0, runtime.SaveCalls);
        Assert.IsNull(viewModel.DraftStatus);
    }

    [TestMethod]
    public void TheBuildHandoffCarriesTheCopyableCommand()
    {
        var viewModel = CreateViewModelWith(new CountingRuntime());

        StringAssert.Contains(viewModel.BuildCommandLine, "batch run");
        StringAssert.Contains(
            LocalizationTestSupport.English(UiStringKeys.CreateBuildHandoffNotice),
            "Unity editor");
    }

    [TestMethod]
    public void SimpleModeTextRerendersOnALanguageSwitch()
    {
        var localization = LocalizationTestSupport.CreateEnglish();
        var viewModel = new CreateViewModel(localization, new CountingRuntime());
        var englishTitle = viewModel.PresetCards[0].Title;
        var englishCapability = viewModel.CapabilityLine;

        localization.SetLanguage(UiLanguage.ChineseSimplified);

        Assert.AreNotEqual(englishTitle, viewModel.PresetCards[0].Title);
        Assert.AreNotEqual(englishCapability, viewModel.CapabilityLine);
        Assert.AreEqual(
            LocalizationTestSupport.ChineseSimplified(UiStringKeys.CreatePresetFireBoltTitle),
            viewModel.PresetCards[0].Title);
    }

    [TestMethod]
    public void AMappedValidationFailureRendersItsBilingualRepairSuggestion()
    {
        var runtime = new CountingRuntime
        {
            NextResult = static request => RecipeGenerationResult.ValidationFailed(
                request.CorrelationId,
                "{}",
                [
                    new RecipeValidationIssue(
                        "E101",
                        RecipeValidationSeverity.Error,
                        "/stages",
                        "Missing required field: stages"),
                    new RecipeValidationIssue(
                        "E999",
                        RecipeValidationSeverity.Error,
                        "/",
                        "An unmapped code renders without a suggestion."),
                ],
                [new RecipeGenerationAttempt(1, ["E101"])],
                "prompt/1",
                "1.0.0"),
        };
        var localization = LocalizationTestSupport.CreateEnglish();
        var viewModel = new CreateViewModel(localization, runtime) { EffectDescription = "a synthetic fireball" };

        viewModel.GenerateRecipeCommand.ExecuteAsync(null).GetAwaiter().GetResult();

        StringAssert.Contains(
            viewModel.RecipeValidationSummary,
            LocalizationTestSupport.English(UiStringKeys.RecipeSuggestionAddRequiredField));
        StringAssert.Contains(viewModel.RecipeValidationSummary, "E999");

        localization.SetLanguage(UiLanguage.ChineseSimplified);

        StringAssert.Contains(
            viewModel.RecipeValidationSummary,
            LocalizationTestSupport.ChineseSimplified(UiStringKeys.RecipeSuggestionAddRequiredField));
        StringAssert.Contains(
            viewModel.RecipeValidationSummary,
            "E101 /stages: Missing required field: stages");
    }

    [TestMethod]
    public void EverySuggestionKeyOfTheClosedProviderSetCarriesPinnedCatalogCopy()
    {
        // The provider set and the catalog copy list must stay the same closed set, and every mapped issue code
        // must resolve into it; RecipeSuggestionCopy refuses anything outside the pinned list.
        CollectionAssert.AreEquivalent(
            RecipeSuggestionKeys.All.ToArray(),
            RecipeSuggestionCopy.CatalogKeys.ToArray());

        foreach (var mapping in RecipeIssueSuggestions.All)
        {
            Assert.IsTrue(
                RecipeSuggestionCopy.TryGetCatalogKey(mapping.Key, out var catalogKey),
                mapping.Key + " resolves no catalog copy.");
            Assert.AreEqual(mapping.Value, catalogKey);
        }

        Assert.IsFalse(RecipeSuggestionCopy.TryGetCatalogKey("E999", out _));
    }

    private static CreateViewModel CreateViewModelWith(CountingRuntime runtime) =>
        new(LocalizationTestSupport.CreateEnglish(), runtime);

    private sealed class CountingRuntime : IAiDesktopRuntime, IAiGateway, IRecipeGenerationChannel, IRecipeDraftStore
    {
        public Func<RecipeGenerationRequest, RecipeGenerationResult>? NextResult { get; init; }
        public bool ThrowOnSave { get; init; }

        public int GenerateCalls { get; private set; }
        public int SaveCalls { get; private set; }
        public Dictionary<string, RecipeDraftRecord> Records { get; } = [];

        public IAiGateway Gateway => this;
        public IAiDesktopSettings Settings => throw new NotSupportedException();
        public IRecipeGenerationChannel RecipeGeneration => this;
        public IRecipeDraftStore RecipeDrafts => this;

        public ValueTask<RecipeGenerationResult> GenerateAsync(
            RecipeGenerationRequest request,
            CancellationToken cancellationToken = default)
        {
            GenerateCalls++;
            return ValueTask.FromResult(NextResult!(request));
        }

        public RecipeDraftRecord Save(RecipeDraftRecord record)
        {
            SaveCalls++;
            if (ThrowOnSave)
            {
                throw new RecipeDraftStoreException(RecipeDraftStoreErrorCode.StorageFailed);
            }

            Records[record.DraftId] = record;
            return record;
        }

        public RecipeDraftRecord Confirm(string draftId, string canonicalSha256)
        {
            var current = Records[draftId];
            if (current.Status != RecipeDraftStatus.PendingConfirmation ||
                !string.Equals(current.CanonicalSha256, canonicalSha256, StringComparison.Ordinal))
            {
                throw new RecipeDraftStoreException(RecipeDraftStoreErrorCode.InvalidStatus);
            }

            var confirmed = new RecipeDraftRecord(
                current.DraftId,
                RecipeDraftStatus.ConfirmedAwaitingBuild,
                current.CreatedUtc,
                DateTimeOffset.UtcNow,
                current.CorrelationId,
                current.PromptTemplateVersion,
                current.TemplateCatalogVersion,
                current.RecipeJson,
                current.CanonicalSha256,
                current.RecipeId,
                current.Archetype,
                current.Dimension,
                current.TargetProfile,
                current.Issues,
                current.RequestCount);
            Records[draftId] = confirmed;
            return confirmed;
        }

        public RecipeDraftRecord? TryGet(string draftId) =>
            Records.TryGetValue(draftId, out var record) ? record : null;

        public IReadOnlyList<RecipeDraftRecord> ListConfirmedAwaitingBuild() =>
            Records.Values.Where(static record => record.Status == RecipeDraftStatus.ConfirmedAwaitingBuild).ToArray();

        public RecipeDraftRecord MarkBuilt(string draftId, string canonicalSha256) =>
            throw new NotSupportedException("The Create page never advances build state.");

        public RecipeDraftRecord MarkBuildFailed(string draftId, string canonicalSha256) =>
            throw new NotSupportedException("The Create page never advances build state.");

        public ValueTask<ChatResponse> ChatAsync(ChatRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ValueTask<ImageGenerationResponse> GenerateImageAsync(
            ImageGenerationRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ValueTask<Stream> OpenImageArtifactAsync(string privateArtifactId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

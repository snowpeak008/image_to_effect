using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VFXComposer.AI.Contracts;
using VFXComposer.AI.Contracts.Chat;
using VFXComposer.AI.Contracts.Desktop;
using VFXComposer.AI.Contracts.Recipes;
using VFXComposer.AI.Providers.Recipes;
using VFXComposer.Desktop.Localization;
using VFXComposer.Desktop.Services;
using VFXComposer.Desktop.ViewModels;

namespace VFXComposer.Desktop.Tests;

/// <summary>
/// The refinement input area over the real lineage store (F8b4, REQ-004 §6): a successful round is exactly one
/// request landing one ai_refine version (AC-2), an exhausted budget keeps the head and lands nothing (AC-3), a
/// network failure is one request with a stable code and an intact retry path (AC-4), the three preflight
/// negatives refuse without any network (REQ-004-14), the guard's restorations are disclosed (REQ-004-48), and
/// the session timeline records every round without ever carrying free text (REQ-004-20/21).
/// </summary>
[TestClass]
public sealed class RefinementFlowTests
{
    private const string Feedback = "make the fire core bigger";

    private string _storeDirectory = string.Empty;

    [TestInitialize]
    public void CreateStoreDirectory() => _storeDirectory = Path.Combine(
        Path.GetTempPath(),
        "vfxcomposer-refinement-flow-tests",
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
    public async Task ASuccessfulRoundLandsTheOneAiRefineVersionAndClearsTheInput()
    {
        // AC-2: exactly one request; the version appends after the head with origin ai_refine, the persisted
        // feedback, the incremented ordinal; the page's head, panel, chain and verdict move; the input clears.
        var runtime = CreateRuntime(request => RefinedResult(request, WithScale(request.Head.RecipeJson, "1.8")));
        var viewModel = NewCreatePage(runtime);
        viewModel.ApplyPresetCommand.Execute(FireBoltCard(viewModel));
        var head = runtime.Store.TryGet(viewModel.DraftId!)!;

        viewModel.RefineFeedback = Feedback;
        await viewModel.RefineRecipeCommand.ExecuteAsync(null);

        Assert.AreEqual(1, runtime.RefineCalls, "One explicit click, one refine round.");
        var refined = runtime.Store.TryGet(viewModel.DraftId!)!;
        Assert.AreNotEqual(head.DraftId, refined.DraftId, "The head moved.");
        Assert.AreEqual(RecipeDraftOrigin.AiRefine, refined.Origin);
        Assert.AreEqual(RecipeDraftStatus.PendingConfirmation, refined.Status);
        Assert.AreEqual(head.DraftId, refined.ParentDraftId);
        Assert.AreEqual(head.RevisionOrdinal + 1, refined.RevisionOrdinal);
        Assert.AreEqual(head.LineageId, refined.LineageId);
        Assert.AreEqual(Feedback, refined.FeedbackText, "This round's feedback is persisted on the version.");
        Assert.AreEqual(1, refined.RequestCount);
        Assert.AreEqual(string.Empty, viewModel.RefineFeedback, "The input box clears after a landed round.");
        Assert.AreEqual(
            LocalizationTestSupport.EnglishFormat(UiStringKeys.CreateRefineStatusCompleted, refined.RevisionOrdinal, 1),
            viewModel.RefineStatus);
        Assert.AreEqual(2, viewModel.Lineage.Versions.Count, "The chain re-listed through SetCurrentDraft.");
        Assert.AreEqual("1.8", ScaleRow(viewModel).CurrentValueLiteral, "The panel re-renders from the refined head.");
        StringAssert.Contains(viewModel.RecipeDraftJson, "1.8");
        Assert.AreEqual(LocalizationTestSupport.English(UiStringKeys.CreateValidationPassed), viewModel.RecipeValidationSummary);
        Assert.IsFalse(viewModel.HasGuardRestorations, "Nothing was restored this round.");
        Assert.IsTrue(viewModel.ConfirmRecipeDraftCommand.CanExecute(null));
        runtime.AssertNoGatewayTraffic();
    }

    [TestMethod]
    public async Task AnExhaustedBudgetKeepsTheHeadAndShowsTheLastOutputWithItsReport()
    {
        // AC-3: the round used its whole 1+N budget on invalid output; nothing lands and the report is inspectable.
        var runtime = CreateRuntime(static request => RecipeRefinementResult.ValidationFailed(
            request.CorrelationId,
            "not a recipe",
            [new RecipeValidationIssue("E101", RecipeValidationSeverity.Error, "/", "Not a JSON object.")],
            [
                new RecipeGenerationAttempt(1, ["E101"]),
                new RecipeGenerationAttempt(2, ["E101"]),
                new RecipeGenerationAttempt(3, ["E101"]),
            ],
            "prompt/refine-tests",
            "1.0.0"));
        var viewModel = NewCreatePage(runtime);
        viewModel.ApplyPresetCommand.Execute(FireBoltCard(viewModel));
        var headId = viewModel.DraftId!;
        var lineageId = runtime.Store.TryGet(headId)!.LineageId;

        viewModel.RefineFeedback = Feedback;
        await viewModel.RefineRecipeCommand.ExecuteAsync(null);

        Assert.AreEqual(1, runtime.RefineCalls);
        Assert.AreEqual(headId, viewModel.DraftId, "The head did not move.");
        Assert.AreEqual(1, runtime.Store.ListLineage(lineageId).Count, "No version landed.");
        Assert.AreEqual(
            LocalizationTestSupport.EnglishFormat(UiStringKeys.CreateRefineStatusValidationFailed, 3, "E101"),
            viewModel.RefineStatus);
        StringAssert.Contains(viewModel.RefineStatus, "3", "The status carries the request count.");
        Assert.AreEqual("not a recipe", viewModel.RecipeDraftJson, "The last raw output stays inspectable.");
        StringAssert.Contains(viewModel.RecipeValidationSummary, "E101");
        Assert.AreEqual(Feedback, viewModel.RefineFeedback, "A failed round keeps the feedback for a retry.");
        runtime.AssertNoGatewayTraffic();
    }

    [TestMethod]
    public async Task ANetworkFailureIsOneRequestWithAStableCodeAndTheButtonStaysAvailable()
    {
        // AC-4: a timed-out round terminates at once — one request, no version, explicit re-click possible.
        var runtime = CreateRuntime(static request => RecipeRefinementResult.ChannelFailed(
            request.CorrelationId,
            ChatChannelErrorCode.TimedOut,
            [new RecipeGenerationAttempt(1, [])],
            "prompt/refine-tests",
            "1.0.0"));
        var viewModel = NewCreatePage(runtime);
        viewModel.ApplyPresetCommand.Execute(FireBoltCard(viewModel));
        var headId = viewModel.DraftId!;
        var lineageId = runtime.Store.TryGet(headId)!.LineageId;

        viewModel.RefineFeedback = Feedback;
        await viewModel.RefineRecipeCommand.ExecuteAsync(null);

        Assert.AreEqual(1, runtime.RefineCalls, "Exactly one request; a network failure is never retried.");
        Assert.AreEqual(headId, viewModel.DraftId);
        Assert.AreEqual(1, runtime.Store.ListLineage(lineageId).Count);
        Assert.AreEqual(
            LocalizationTestSupport.EnglishFormat(
                UiStringKeys.CreateRefineStatusChannelFailedWithCode,
                ChatChannelErrorCode.TimedOut),
            viewModel.RefineStatus);
        StringAssert.Contains(viewModel.RefineStatus, nameof(ChatChannelErrorCode.TimedOut));
        Assert.IsTrue(viewModel.RefineRecipeCommand.CanExecute(null), "The user re-sends explicitly.");

        await viewModel.RefineRecipeCommand.ExecuteAsync(null);
        Assert.AreEqual(2, runtime.RefineCalls, "The re-click is a fresh round.");
        runtime.AssertNoGatewayTraffic();
    }

    [TestMethod]
    public async Task TheThreePreflightNegativesRefuseWithoutAnyNetwork()
    {
        // REQ-004-14: no head, empty feedback, unbound route — all refuse before any request.
        var runtime = CreateRuntime(static _ => throw new AssertFailedException("No round may start."));
        var viewModel = NewCreatePage(runtime);

        // 1. No head draft.
        viewModel.RefineFeedback = Feedback;
        await viewModel.RefineRecipeCommand.ExecuteAsync(null);
        Assert.AreEqual(
            LocalizationTestSupport.English(UiStringKeys.CreateRefineStatusNoHead),
            viewModel.RefineStatus);
        Assert.AreEqual(0, runtime.RefineCalls);

        // 2. Empty feedback.
        viewModel.ApplyPresetCommand.Execute(FireBoltCard(viewModel));
        viewModel.RefineFeedback = "   ";
        await viewModel.RefineRecipeCommand.ExecuteAsync(null);
        Assert.AreEqual(
            LocalizationTestSupport.English(UiStringKeys.CreateRefineStatusEmptyFeedback),
            viewModel.RefineStatus);
        Assert.AreEqual(0, runtime.RefineCalls);
        runtime.AssertNoGatewayTraffic();

        // 3. Unbound route: the channel fails closed before the network (REQ-004-15) and points to Settings.
        var unbound = CreateRuntime(static _ => throw new AiGatewayException(AiErrorCode.ConfigurationUnavailable));
        var unboundPage = NewCreatePage(unbound);
        unboundPage.ApplyPresetCommand.Execute(FireBoltCard(unboundPage));
        var headId = unboundPage.DraftId!;
        unboundPage.RefineFeedback = Feedback;
        await unboundPage.RefineRecipeCommand.ExecuteAsync(null);
        Assert.AreEqual(
            LocalizationTestSupport.EnglishFormat(
                UiStringKeys.CreateRefineStatusNotConfiguredWithCode,
                AiErrorCode.ConfigurationUnavailable),
            unboundPage.RefineStatus);
        Assert.AreEqual(headId, unboundPage.DraftId, "Nothing landed.");
        unbound.AssertNoGatewayTraffic();
    }

    [TestMethod]
    public async Task GuardRestorationsAreDisclosedInTheConfirmationAreaAndTheTimeline()
    {
        // REQ-004-48 / AC-9 visibility half: the round restored two hand-tuned values; both surfaces say so.
        var roundRestorations = new List<RecipeRefinementGuardRestoration>
        {
            new("stages[travel].modules[core].parameters.scale", "draft-source", "0.42", "0.20"),
            new("stages[travel].modules[core].parameters.speed", "draft-source", "9.0", "5.5"),
        };
        var runtime = CreateRuntime(request => RefinedResult(
            request,
            WithScale(request.Head.RecipeJson, "1.8"),
            roundRestorations.ToArray()));
        var viewModel = NewCreatePage(runtime);
        viewModel.ApplyPresetCommand.Execute(FireBoltCard(viewModel));

        viewModel.RefineFeedback = Feedback;
        await viewModel.RefineRecipeCommand.ExecuteAsync(null);

        Assert.IsTrue(viewModel.HasGuardRestorations);
        Assert.AreEqual(2, viewModel.GuardRestorations.Count);
        Assert.AreEqual(
            LocalizationTestSupport.EnglishFormat(UiStringKeys.CreateRefineGuardHeading, 2),
            viewModel.GuardRestorationsHeading);
        StringAssert.Contains(viewModel.GuardRestorationsReport, "stages[travel].modules[core].parameters.scale");
        StringAssert.Contains(viewModel.GuardRestorationsReport, "0.42");
        StringAssert.Contains(viewModel.GuardRestorationsReport, "0.20");
        StringAssert.Contains(
            viewModel.GuardRestorationsReport,
            LocalizationTestSupport.EnglishFormat(
                UiStringKeys.CreateRefineGuardLine,
                "stages[travel].modules[core].parameters.speed",
                "9.0",
                "5.5"));

        var timelineText = string.Join("\n", viewModel.Timeline.Entries.Select(static entry => entry.Text));
        StringAssert.Contains(timelineText, "2", "The timeline carries the restoration count.");
        StringAssert.Contains(timelineText, "stages[travel].modules[core].parameters.scale");
        StringAssert.Contains(timelineText, "stages[travel].modules[core].parameters.speed");

        var persisted = runtime.Store.TryGet(viewModel.DraftId!)!;
        Assert.AreEqual(2, persisted.GuardRestorationCount, "The restorations rode into the persisted version.");

        // The next round's disclosure replaces this one: it belongs to the round that produced it.
        roundRestorations.Clear();
        viewModel.RefineFeedback = "another round";
        await viewModel.RefineRecipeCommand.ExecuteAsync(null);
        Assert.AreEqual(0, viewModel.GuardRestorations.Count);
        Assert.IsFalse(viewModel.HasGuardRestorations);
    }

    [TestMethod]
    public async Task TheAnchoredTripleUsesTheChainsOwnDescriptionAndOnlyThisRoundsFeedback()
    {
        // REQ-004 §6.1/-13: a preset chain anchors on the skeleton's English description; an AI chain anchors on
        // the generate click's description; the second round's request carries no first-round feedback.
        var runtime = CreateRuntime(request => RefinedResult(request, WithScale(request.Head.RecipeJson, "1.8")));
        var viewModel = NewCreatePage(runtime);
        var card = FireBoltCard(viewModel);
        viewModel.ApplyPresetCommand.Execute(card);
        viewModel.RefineFeedback = "first round feedback";
        await viewModel.RefineRecipeCommand.ExecuteAsync(null);

        Assert.AreEqual(card.Skeleton.EnglishDescription, runtime.Requests[0].OriginalDescription);
        Assert.AreEqual("first round feedback", runtime.Requests[0].FeedbackText);

        viewModel.RefineFeedback = "second round feedback";
        await viewModel.RefineRecipeCommand.ExecuteAsync(null);

        Assert.AreEqual(2, runtime.Requests.Count);
        Assert.AreEqual(card.Skeleton.EnglishDescription, runtime.Requests[1].OriginalDescription, "Immutable across the chain.");
        Assert.AreEqual("second round feedback", runtime.Requests[1].FeedbackText);
        Assert.IsFalse(
            runtime.Requests[1].FeedbackText.Contains("first round", StringComparison.Ordinal),
            "Earlier rounds are never carried (REQ-004-13).");
        Assert.AreEqual(2, runtime.Requests[1].Lineage.Count, "The second round anchors on the grown chain.");

        // The AI chain: the generate description is cached and anchors every later round.
        var aiRuntime = CreateRuntime(request => RefinedResult(request, WithScale(request.Head.RecipeJson, "1.1")));
        aiRuntime.NextGeneration = DraftedFireBolt;
        var aiPage = NewCreatePage(aiRuntime);
        aiPage.EffectDescription = "a very specific fire bolt description";
        await aiPage.GenerateRecipeCommand.ExecuteAsync(null);
        aiPage.RefineFeedback = Feedback;
        await aiPage.RefineRecipeCommand.ExecuteAsync(null);

        Assert.AreEqual("a very specific fire bolt description", aiRuntime.Requests.Single().OriginalDescription);
    }

    [TestMethod]
    public async Task RefinementIsGatedIntoProfessionalModeAndItsSurfaceStaysZeroNetwork()
    {
        var runtime = CreateRuntime(request => RefinedResult(request, WithScale(request.Head.RecipeJson, "1.8")));
        var modes = new GenerationModeService();
        var viewModel = new CreateViewModel(LocalizationTestSupport.CreateEnglish(), runtime, modes);
        viewModel.ApplyPresetCommand.Execute(FireBoltCard(viewModel));

        Assert.IsFalse(viewModel.IsRefineVisible, "Simple mode shows no refinement input (REQ-004-07).");
        Assert.IsFalse(viewModel.IsTimelineVisible);

        modes.SetMode(GenerationMode.Professional);

        Assert.IsTrue(viewModel.IsRefineVisible);
        Assert.IsTrue(viewModel.IsTimelineVisible, "The preset click already wrote an entry.");
        Assert.AreEqual(0, runtime.RefineCalls, "Typing and switching send nothing (REQ-004-12).");
        viewModel.RefineFeedback = Feedback;
        Assert.AreEqual(0, runtime.RefineCalls);

        await viewModel.RefineRecipeCommand.ExecuteAsync(null);
        Assert.AreEqual(1, runtime.RefineCalls, "Only the explicit click starts the round.");
        runtime.AssertNoGatewayTraffic();
    }

    private string StorePath => Path.Combine(_storeDirectory, "recipe-drafts.json");

    private RefiningRuntime CreateRuntime(Func<RecipeRefinementRequest, RecipeRefinementResult> nextRefinement) =>
        new(StorePath, nextRefinement);

    private static CreateViewModel NewCreatePage(RefiningRuntime runtime) => new(
        LocalizationTestSupport.CreateEnglish(),
        runtime,
        new GenerationModeService(GenerationMode.Professional));

    private static PresetCardViewModel FireBoltCard(CreateViewModel viewModel) =>
        viewModel.PresetCards.Single(static card => card.Skeleton.PresetId == "fire-bolt");

    private static ParameterRowViewModel ScaleRow(CreateViewModel viewModel) => viewModel.ParameterPanel.Modules
        .Single(static module => module.ModuleId == "core")
        .Parameters.Single(static row => row.Name == "scale");

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

    /// <summary>A refined outcome anchored on the request's own head, ready for the page's AppendVersion.</summary>
    private static RecipeRefinementResult RefinedResult(
        RecipeRefinementRequest request,
        string refinedJson,
        IEnumerable<RecipeRefinementGuardRestoration>? guardRestorations = null)
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
            guardRestorations ?? [],
            [new RecipeGenerationAttempt(1, [])]);
    }

    private static RecipeGenerationResult DraftedFireBolt(RecipeGenerationRequest request)
    {
        var canonical = RecipeCanonicalJson.Canonicalize(
            RecipePresetSkeletons.All.Single(static skeleton => skeleton.PresetId == "fire-bolt").RecipeJson);
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

    /// <summary>
    /// The real lineage store behind a counting refinement channel and gateway; the refinement fake overrides the
    /// contract's default member explicitly and captures every request for the anchoring assertions.
    /// </summary>
    internal sealed class RefiningRuntime : IAiDesktopRuntime, IAiGateway, IRecipeGenerationChannel, IRecipeRefinementChannel
    {
        private readonly Func<RecipeRefinementRequest, RecipeRefinementResult> _nextRefinement;

        public RefiningRuntime(string storePath, Func<RecipeRefinementRequest, RecipeRefinementResult> nextRefinement)
        {
            Store = new RecipeDraftStore(storePath);
            _nextRefinement = nextRefinement;
        }

        public RecipeDraftStore Store { get; }
        public Func<RecipeGenerationRequest, RecipeGenerationResult>? NextGeneration { get; set; }
        public List<RecipeRefinementRequest> Requests { get; } = [];
        public int RefineCalls { get; private set; }
        public int GenerateCalls { get; private set; }
        public int ChatCalls { get; private set; }
        public int ImageCalls { get; private set; }

        public IAiGateway Gateway => this;
        public IAiDesktopSettings Settings => throw new NotSupportedException();
        public IRecipeGenerationChannel RecipeGeneration => this;
        public IRecipeRefinementChannel RecipeRefinement => this;
        public IRecipeDraftLineageStore RecipeDrafts => Store;

        public void AssertNoGatewayTraffic()
        {
            Assert.AreEqual(0, ChatCalls, "The refinement surface must never send a raw chat request.");
            Assert.AreEqual(0, ImageCalls, "The refinement surface must never send an image request.");
        }

        public ValueTask<RecipeRefinementResult> RefineAsync(
            RecipeRefinementRequest request,
            CancellationToken cancellationToken = default)
        {
            RefineCalls++;
            Requests.Add(request);
            return ValueTask.FromResult(_nextRefinement(request));
        }

        public ValueTask<RecipeGenerationResult> GenerateAsync(
            RecipeGenerationRequest request,
            CancellationToken cancellationToken = default)
        {
            GenerateCalls++;
            return ValueTask.FromResult(NextGeneration!(request));
        }

        public ValueTask<ChatResponse> ChatAsync(ChatRequest request, CancellationToken cancellationToken = default)
        {
            ChatCalls++;
            return ValueTask.FromException<ChatResponse>(new AiGatewayException(AiErrorCode.ConfigurationUnavailable));
        }

        public ValueTask<ImageGenerationResponse> GenerateImageAsync(
            ImageGenerationRequest request,
            CancellationToken cancellationToken = default)
        {
            ImageCalls++;
            return ValueTask.FromException<ImageGenerationResponse>(new AiGatewayException(AiErrorCode.ConfigurationUnavailable));
        }

        public ValueTask<Stream> OpenImageArtifactAsync(string privateArtifactId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

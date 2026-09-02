using Microsoft.VisualStudio.TestTools.UnitTesting;
using VFXComposer.AI.Contracts;
using VFXComposer.AI.Contracts.Chat;
using VFXComposer.AI.Contracts.Desktop;
using VFXComposer.AI.Contracts.Recipes;
using VFXComposer.Desktop.Localization;
using VFXComposer.Desktop.Services;
using VFXComposer.Desktop.ViewModels;

namespace VFXComposer.Desktop.Tests;

/// <summary>
/// The in-app build action (F8c, ADR-008 §2.1/§2.3): the explicit Build button exists only for the
/// exact ConfirmedAwaitingBuild head, launches the private host with the draft identity and
/// nothing else, surfaces a launch refusal as its stable code with zero builds, and the manual
/// refresh re-reads the shared store so a finished build's terminal state becomes visible. No test
/// starts a process: the launcher seam records instead of launching.
/// </summary>
[TestClass]
public sealed class BuildActionTests
{
    [TestMethod]
    public void BuildIsOfferedExactlyForTheConfirmedHeadAndCarriesTheDraftIdentity()
    {
        var runtime = new BuildFlowRuntime();
        var launcher = new RecordingLauncher();
        var viewModel = CreateViewModelWith(runtime, launcher);

        Assert.IsFalse(viewModel.BuildRecipeDraftCommand.CanExecute(null), "No draft: no build.");

        viewModel.EffectDescription = "a fire bolt";
        viewModel.GenerateRecipeCommand.Execute(null);
        Assert.IsFalse(
            viewModel.BuildRecipeDraftCommand.CanExecute(null),
            "A pending draft is not buildable; confirmation is the gate.");

        viewModel.ConfirmRecipeDraftCommand.Execute(null);
        Assert.AreEqual(RecipeDraftStatus.ConfirmedAwaitingBuild, viewModel.DraftStatus);
        Assert.IsTrue(viewModel.BuildRecipeDraftCommand.CanExecute(null));

        viewModel.BuildRecipeDraftCommand.Execute(null);

        var launch = launcher.Launches.Single();
        Assert.AreEqual(viewModel.DraftId, launch.DraftId);
        Assert.AreEqual(runtime.Records[viewModel.DraftId!].CanonicalSha256, launch.CanonicalSha256);
        Assert.AreEqual(
            LocalizationTestSupport.English(UiStringKeys.CreateRecipeStatusBuildStarted),
            viewModel.RecipeStatus);
        Assert.IsTrue(
            viewModel.Timeline.Entries.Any(entry => entry.Text.Contains(viewModel.DraftId!, StringComparison.Ordinal)
                && entry.Text.Contains("Build started", StringComparison.Ordinal)),
            "The session timeline records the explicit build action.");
    }

    [TestMethod]
    public void AShellWithoutALauncherNeverOffersTheBuildAction()
    {
        var runtime = new BuildFlowRuntime();
        var viewModel = new CreateViewModel(LocalizationTestSupport.CreateEnglish(), runtime);

        viewModel.EffectDescription = "a fire bolt";
        viewModel.GenerateRecipeCommand.Execute(null);
        viewModel.ConfirmRecipeDraftCommand.Execute(null);

        Assert.AreEqual(RecipeDraftStatus.ConfirmedAwaitingBuild, viewModel.DraftStatus);
        Assert.IsFalse(viewModel.BuildRecipeDraftCommand.CanExecute(null));
    }

    [TestMethod]
    public void ARefusedLaunchShowsItsStableCodeAndBuildsNothing()
    {
        var runtime = new BuildFlowRuntime();
        var launcher = new RecordingLauncher
        {
            NextOutcome = BuildHostLaunchOutcome.Refused(BuildHostLauncher.HostMissingDiagnosticCode),
        };
        var viewModel = CreateViewModelWith(runtime, launcher);
        viewModel.EffectDescription = "a fire bolt";
        viewModel.GenerateRecipeCommand.Execute(null);
        viewModel.ConfirmRecipeDraftCommand.Execute(null);

        viewModel.BuildRecipeDraftCommand.Execute(null);

        StringAssert.Contains(viewModel.RecipeStatus, BuildHostLauncher.HostMissingDiagnosticCode);
        Assert.AreEqual(
            RecipeDraftStatus.ConfirmedAwaitingBuild,
            viewModel.DraftStatus,
            "A refused launch changes nothing: the draft stays confirmed and buildable.");
        Assert.IsFalse(
            viewModel.Timeline.Entries.Any(entry => entry.Text.Contains("Build started", StringComparison.Ordinal)),
            "No timeline entry may claim a build that never started.");
    }

    [TestMethod]
    public void RefreshReReadsTheStoreSoTheHostWrittenTerminalStateBecomesVisible()
    {
        var runtime = new BuildFlowRuntime();
        var launcher = new RecordingLauncher();
        var viewModel = CreateViewModelWith(runtime, launcher);
        viewModel.EffectDescription = "a fire bolt";
        viewModel.GenerateRecipeCommand.Execute(null);
        viewModel.ConfirmRecipeDraftCommand.Execute(null);
        viewModel.BuildRecipeDraftCommand.Execute(null);
        var draftId = viewModel.DraftId!;

        // The host process advances the shared record out of band; the page must not know until refresh.
        runtime.AdvanceToBuilt(draftId);
        Assert.AreEqual(RecipeDraftStatus.ConfirmedAwaitingBuild, viewModel.DraftStatus);

        viewModel.RefreshBuildStatusCommand.Execute(null);

        Assert.AreEqual(RecipeDraftStatus.Built, viewModel.DraftStatus);
        StringAssert.Contains(viewModel.RecipeStatus, nameof(RecipeDraftStatus.Built));
        Assert.IsFalse(
            viewModel.BuildRecipeDraftCommand.CanExecute(null),
            "A built head is terminal; the build action must retire with it.");
        Assert.AreEqual(0, runtime.GenerateCalls - 1, "Refresh performs zero generation requests.");
    }

    [TestMethod]
    public void RefreshOfADraftRemovedByRetentionSaysSoInsteadOfThrowing()
    {
        var runtime = new BuildFlowRuntime();
        var viewModel = CreateViewModelWith(runtime, new RecordingLauncher());
        viewModel.EffectDescription = "a fire bolt";
        viewModel.GenerateRecipeCommand.Execute(null);
        var draftId = viewModel.DraftId!;

        runtime.Records.Remove(draftId);
        viewModel.RefreshBuildStatusCommand.Execute(null);

        Assert.AreEqual(
            LocalizationTestSupport.English(UiStringKeys.CreateRecipeStatusRefreshDraftGone),
            viewModel.RecipeStatus);
    }

    private static CreateViewModel CreateViewModelWith(BuildFlowRuntime runtime, RecordingLauncher launcher) =>
        new(LocalizationTestSupport.CreateEnglish(), runtime, buildHostLauncher: launcher);

    private sealed class RecordingLauncher : IBuildHostLauncher
    {
        public List<(string DraftId, string CanonicalSha256)> Launches { get; } = [];

        public BuildHostLaunchOutcome NextOutcome { get; init; } = BuildHostLaunchOutcome.Launched;

        public BuildHostLaunchOutcome TryLaunch(string draftId, string canonicalSha256, Action<int>? exited = null)
        {
            if (NextOutcome.Started)
            {
                Launches.Add((draftId, canonicalSha256));
            }

            return NextOutcome;
        }
    }

    /// <summary>In-memory runtime whose store records the F8c host's out-of-band state advances.</summary>
    private sealed class BuildFlowRuntime : IAiDesktopRuntime, IAiGateway, IRecipeGenerationChannel, IRecipeDraftLineageStore
    {
        public Dictionary<string, RecipeDraftRecord> Records { get; } = [];

        public int GenerateCalls { get; private set; }

        public IAiGateway Gateway => this;
        public IAiDesktopSettings Settings => throw new NotSupportedException();
        public IRecipeGenerationChannel RecipeGeneration => this;
        public IRecipeDraftLineageStore RecipeDrafts => this;

        public ValueTask<ChatResponse> ChatAsync(ChatRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("The build flow never chats.");

        public ValueTask<ImageGenerationResponse> GenerateImageAsync(
            ImageGenerationRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("The build flow never generates images.");

        public ValueTask<Stream> OpenImageArtifactAsync(string privateArtifactId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("The build flow never opens artifacts.");

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public ValueTask<RecipeGenerationResult> GenerateAsync(
            RecipeGenerationRequest request,
            CancellationToken cancellationToken = default)
        {
            GenerateCalls++;
            var recipeJson = "{\"id\":\"fx_build_probe\",\"archetype\":\"projectile\",\"dimension\":\"2d\"}";
            var draft = new RecipeDraft(
                request.CorrelationId,
                recipeJson,
                RecipeCanonicalJson.ComputeSha256(recipeJson),
                "fx_build_probe",
                "projectile",
                "2d",
                "mobile_medium",
                "prompt/1",
                "1.0.0");
            return ValueTask.FromResult(
                RecipeGenerationResult.Drafted(draft, [new RecipeGenerationAttempt(1, [])]));
        }

        /// <summary>Simulates the host's MarkBuilt landing in the shared store while the page shows stale state.</summary>
        public void AdvanceToBuilt(string draftId)
        {
            var current = Records[draftId];
            Records[draftId] = new RecipeDraftRecord(
                current.DraftId,
                RecipeDraftStatus.Built,
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
        }

        public RecipeDraftSaveOutcome SaveVersion(RecipeDraftRecord record)
        {
            Records[record.DraftId] = record;
            return new RecipeDraftSaveOutcome(record, [], [], [], 0);
        }

        public RecipeDraftRecord Save(RecipeDraftRecord record) => SaveVersion(record).Record;

        public RecipeDraftSaveOutcome AppendVersion(
            string parentDraftId,
            string parentCanonicalSha256,
            RecipeDraftRevision revision,
            DateTimeOffset createdUtc) =>
            throw new NotSupportedException("The build flow never appends a version.");

        public RecipeDraftTruncateOutcome TruncateAfter(string draftId) =>
            throw new NotSupportedException("The build flow never truncates.");

        public IReadOnlyList<RecipeDraftRecord> ListLineage(string lineageId) =>
            Records.Values
                .Where(record => string.Equals(record.LineageId, lineageId, StringComparison.Ordinal))
                .OrderBy(static record => record.RevisionOrdinal)
                .ToArray();

        public RecipeDraftRecord Confirm(string draftId, string canonicalSha256)
        {
            var current = Records[draftId];
            if (!string.Equals(current.CanonicalSha256, canonicalSha256, StringComparison.Ordinal))
            {
                throw new RecipeDraftStoreException(RecipeDraftStoreErrorCode.HashMismatch);
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
            Records.Values
                .Where(static record => record.Status == RecipeDraftStatus.ConfirmedAwaitingBuild)
                .OrderBy(static record => record.UpdatedUtc)
                .ToArray();

        public RecipeDraftRecord MarkBuilt(string draftId, string canonicalSha256) =>
            throw new NotSupportedException("Only the host process advances build state.");

        public RecipeDraftRecord MarkBuildFailed(string draftId, string canonicalSha256) =>
            throw new NotSupportedException("Only the host process advances build state.");
    }
}

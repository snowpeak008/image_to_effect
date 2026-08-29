using Microsoft.VisualStudio.TestTools.UnitTesting;
using VFXComposer.AI.Contracts;
using VFXComposer.AI.Contracts.Chat;
using VFXComposer.AI.Contracts.Desktop;
using VFXComposer.AI.Contracts.Recipes;
using VFXComposer.Desktop.ViewModels;

namespace VFXComposer.Desktop.Tests;

[TestClass]
public sealed class CreateRecipeFlowTests
{
    private const string RecipeJson = "{\"id\":\"synthetic_recipe\"}";
    private static readonly string CanonicalSha = new('a', 64);

    [TestMethod]
    public void GenerationIsDisabledWithoutADescriptionAndNothingIsCalledBeforeTheClick()
    {
        var runtime = new FakeRecipeRuntime();
        var viewModel = new CreateViewModel(runtime);

        Assert.IsFalse(viewModel.GenerateRecipeCommand.CanExecute(null));
        viewModel.EffectDescription = "a synthetic fireball";
        Assert.IsTrue(viewModel.GenerateRecipeCommand.CanExecute(null));
        Assert.AreEqual(0, runtime.GenerateCalls);
        Assert.IsNull(viewModel.DraftStatus);
        Assert.IsFalse(viewModel.ConfirmRecipeDraftCommand.CanExecute(null));
    }

    [TestMethod]
    public async Task ADraftedResultLandsAsPendingConfirmationAndConfirmOnlyFlipsTheRetainedState()
    {
        var runtime = new FakeRecipeRuntime { NextResult = DraftedResult };
        var viewModel = new CreateViewModel(runtime)
        {
            EffectDescription = "a synthetic fireball",
        };

        await viewModel.GenerateRecipeCommand.ExecuteAsync(null);

        Assert.AreEqual(1, runtime.GenerateCalls);
        Assert.AreEqual(RecipeDraftStatus.PendingConfirmation, viewModel.DraftStatus);
        Assert.IsTrue(viewModel.RecipeDraftJson.Contains("synthetic_recipe", StringComparison.Ordinal));
        Assert.IsTrue(viewModel.RecipeStatus.Contains("confirm", StringComparison.OrdinalIgnoreCase));
        Assert.IsTrue(viewModel.ConfirmRecipeDraftCommand.CanExecute(null));

        viewModel.ConfirmRecipeDraftCommand.Execute(null);

        Assert.AreEqual(RecipeDraftStatus.ConfirmedAwaitingBuild, viewModel.DraftStatus);
        Assert.IsFalse(viewModel.ConfirmRecipeDraftCommand.CanExecute(null));
        Assert.IsTrue(viewModel.RecipeStatus.Contains("awaiting build", StringComparison.Ordinal));
        var retained = runtime.Records[viewModel.DraftId!];
        Assert.AreEqual(RecipeDraftStatus.ConfirmedAwaitingBuild, retained.Status);
        Assert.AreEqual(1, runtime.GenerateCalls);
    }

    [TestMethod]
    public async Task AValidationFailureShowsStableCodesRetainsTheFailedRecordAndCannotBeConfirmed()
    {
        var runtime = new FakeRecipeRuntime { NextResult = FailedResult };
        var viewModel = new CreateViewModel(runtime)
        {
            EffectDescription = "a synthetic fireball",
        };

        await viewModel.GenerateRecipeCommand.ExecuteAsync(null);

        Assert.AreEqual(RecipeDraftStatus.Failed, viewModel.DraftStatus);
        Assert.IsTrue(viewModel.RecipeStatus.Contains("E101", StringComparison.Ordinal));
        Assert.IsTrue(viewModel.RecipeValidationSummary.Contains("E101", StringComparison.Ordinal));
        Assert.IsFalse(viewModel.ConfirmRecipeDraftCommand.CanExecute(null));
        Assert.AreEqual(RecipeDraftStatus.Failed, runtime.Records[viewModel.DraftId!].Status);
    }

    [TestMethod]
    public async Task AChannelFailureShowsTheStableErrorCodeAndRetainsNoDraft()
    {
        var runtime = new FakeRecipeRuntime
        {
            NextResult = static request => RecipeGenerationResult.ChannelFailed(
                request.CorrelationId,
                ChatChannelErrorCode.TransportFailed,
                [],
                "prompt/1",
                "1.0.0"),
        };
        var viewModel = new CreateViewModel(runtime)
        {
            EffectDescription = "a synthetic fireball",
        };

        await viewModel.GenerateRecipeCommand.ExecuteAsync(null);

        Assert.IsTrue(viewModel.RecipeStatus.Contains("TransportFailed", StringComparison.Ordinal));
        Assert.IsNull(viewModel.DraftStatus);
        Assert.AreEqual(0, runtime.Records.Count);
        Assert.IsFalse(viewModel.ConfirmRecipeDraftCommand.CanExecute(null));
    }

    [TestMethod]
    public async Task ACancelledResultReportsCancellationWithoutARetainedDraft()
    {
        var runtime = new FakeRecipeRuntime
        {
            NextResult = static request => RecipeGenerationResult.ChannelFailed(
                request.CorrelationId,
                ChatChannelErrorCode.Cancelled,
                [],
                "prompt/1",
                "1.0.0"),
        };
        var viewModel = new CreateViewModel(runtime)
        {
            EffectDescription = "a synthetic fireball",
        };

        await viewModel.GenerateRecipeCommand.ExecuteAsync(null);

        Assert.IsTrue(viewModel.RecipeStatus.Contains("cancelled", StringComparison.OrdinalIgnoreCase));
        Assert.IsNull(viewModel.DraftStatus);
    }

    [TestMethod]
    public async Task ADraftStorageFailureSurfacesItsStableCode()
    {
        var runtime = new FakeRecipeRuntime { NextResult = DraftedResult, ThrowOnSave = true };
        var viewModel = new CreateViewModel(runtime)
        {
            EffectDescription = "a synthetic fireball",
        };

        await viewModel.GenerateRecipeCommand.ExecuteAsync(null);

        Assert.IsTrue(viewModel.RecipeStatus.Contains("StorageFailed", StringComparison.Ordinal));
        Assert.IsNull(viewModel.DraftStatus);
        Assert.IsFalse(viewModel.ConfirmRecipeDraftCommand.CanExecute(null));
    }

    [TestMethod]
    public async Task AStaleConfirmationFailsClosedWithTheHashMismatchCode()
    {
        var runtime = new FakeRecipeRuntime { NextResult = DraftedResult, MutateRecordAfterSave = true };
        var viewModel = new CreateViewModel(runtime)
        {
            EffectDescription = "a synthetic fireball",
        };

        await viewModel.GenerateRecipeCommand.ExecuteAsync(null);
        viewModel.ConfirmRecipeDraftCommand.Execute(null);

        Assert.IsTrue(viewModel.RecipeStatus.Contains("HashMismatch", StringComparison.Ordinal));
        Assert.AreEqual(RecipeDraftStatus.PendingConfirmation, viewModel.DraftStatus);
    }

    private static RecipeGenerationResult DraftedResult(RecipeGenerationRequest request)
    {
        var draft = new RecipeDraft(
            request.CorrelationId,
            RecipeJson,
            CanonicalSha,
            "synthetic_recipe",
            "projectile",
            "2d",
            "mobile_medium",
            "prompt/1",
            "1.0.0");
        return RecipeGenerationResult.Drafted(draft, [new RecipeGenerationAttempt(1, [])]);
    }

    private static RecipeGenerationResult FailedResult(RecipeGenerationRequest request) =>
        RecipeGenerationResult.ValidationFailed(
            request.CorrelationId,
            "{}",
            [
                new RecipeValidationIssue(
                    "E101",
                    RecipeValidationSeverity.Error,
                    "/stages",
                    "Missing required field: stages"),
            ],
            [new RecipeGenerationAttempt(1, ["E101"])],
            "prompt/1",
            "1.0.0");

    private sealed class FakeRecipeRuntime : IAiDesktopRuntime, IAiGateway, IRecipeGenerationChannel, IRecipeDraftStore
    {
        public Func<RecipeGenerationRequest, RecipeGenerationResult>? NextResult { get; init; }
        public bool ThrowOnSave { get; init; }

        /// <summary>Simulates another writer changing the retained draft between display and confirmation.</summary>
        public bool MutateRecordAfterSave { get; init; }

        public int GenerateCalls { get; private set; }
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
            if (ThrowOnSave)
            {
                throw new RecipeDraftStoreException(RecipeDraftStoreErrorCode.StorageFailed);
            }

            var stored = record;
            if (MutateRecordAfterSave && record.CanonicalSha256 is not null)
            {
                stored = new RecipeDraftRecord(
                    record.DraftId,
                    record.Status,
                    record.CreatedUtc,
                    record.UpdatedUtc,
                    record.CorrelationId,
                    record.PromptTemplateVersion,
                    record.TemplateCatalogVersion,
                    record.RecipeJson,
                    new string('b', 64),
                    record.RecipeId,
                    record.Archetype,
                    record.Dimension,
                    record.TargetProfile,
                    record.Issues,
                    record.RequestCount);
            }

            Records[record.DraftId] = stored;
            return record;
        }

        public RecipeDraftRecord Confirm(string draftId, string canonicalSha256)
        {
            if (!Records.TryGetValue(draftId, out var current))
            {
                throw new RecipeDraftStoreException(RecipeDraftStoreErrorCode.NotFound);
            }

            if (current.Status != RecipeDraftStatus.PendingConfirmation)
            {
                throw new RecipeDraftStoreException(RecipeDraftStoreErrorCode.InvalidStatus);
            }

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

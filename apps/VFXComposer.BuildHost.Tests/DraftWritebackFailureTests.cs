using Microsoft.VisualStudio.TestTools.UnitTesting;
using VFXComposer.AI.Contracts.Recipes;
using VFXComposer.Batch.Core;

namespace VFXComposer.BuildHost.Tests;

/// <summary>
/// ADR-008 §5: a draft-store write-back failure never rolls back the build fact. The batchmode
/// process already wrote the artifacts; the store fault must surface as its own stable code
/// (VFXB1012) on the queue entry instead of masquerading as a build failure — and after a failed
/// build the same store fault must not mask the build's own failure code.
/// </summary>
[TestClass]
public sealed class DraftWritebackFailureTests
{
    private string _root = string.Empty;

    [TestInitialize]
    public void CreateRoot() => _root = BuildHostTestHarness.CreateDirectory();

    [TestCleanup]
    public void DeleteRoot() => BuildHostTestHarness.DeleteDirectory(_root);

    [TestMethod]
    public async Task ASuccessfulBuildWhoseWritebackFailsSurfacesTheTransitionCodeNotARollback()
    {
        var inner = new InMemoryRecipeDraftStore();
        var saved = inner.Save(BuildHostTestHarness.Draft());
        var confirmed = inner.Confirm(saved.DraftId, saved.CanonicalSha256!);
        var store = new WritebackRefusingStore(inner);
        var runner = new FakeUnityRecipeBuildRunner(
            UnityRecipeBuildExitCodes.Succeeded,
            _ => BuildHostTestHarness.SuccessResult(confirmed.CanonicalSha256!));
        var sink = new RecordingSink();

        var pending = new RecipeBuildOrchestrator(runner, () => store, new RecipeBuildOptions { TimeoutSeconds = 30 })
            .ExecuteAsync(
                BatchRecipeBuildPayload.Create(confirmed.DraftId, confirmed.RecipeJson),
                Path.Combine(_root, "job"),
                sink,
                CancellationToken.None);

        var exception = await Assert.ThrowsExactlyAsync<RecipeBuildFailureException>(() => pending);
        Assert.AreEqual(RecipeBuildFailureCodes.DraftTransitionFailed, exception.Code);
        Assert.AreEqual(1, runner.StartCount, "The build itself really happened.");
        CollectionAssert.Contains(
            sink.Artifacts,
            RecipeBuildFailureCodes.FailureArtifact(RecipeBuildFailureCodes.DraftTransitionFailed),
            "The store fault must survive as its own code on the queue-visible artifact surface.");
        Assert.AreEqual(
            RecipeDraftStatus.ConfirmedAwaitingBuild,
            inner.TryGet(saved.DraftId)!.Status,
            "The record stays confirmed: nothing pretends the write-back happened.");
    }

    [TestMethod]
    public async Task AFailedBuildWhoseWritebackAlsoFailsKeepsTheBuildsOwnFailureCode()
    {
        var inner = new InMemoryRecipeDraftStore();
        var saved = inner.Save(BuildHostTestHarness.Draft());
        var confirmed = inner.Confirm(saved.DraftId, saved.CanonicalSha256!);
        var store = new WritebackRefusingStore(inner);
        var runner = new FakeUnityRecipeBuildRunner(
            UnityRecipeBuildExitCodes.StructuredFailure,
            _ => BuildHostTestHarness.FailureResult("VFXB0008"));
        var sink = new RecordingSink();

        var decision = await new RecipeBuildOrchestrator(runner, () => store, new RecipeBuildOptions { TimeoutSeconds = 30 })
            .ExecuteAsync(
                BatchRecipeBuildPayload.Create(confirmed.DraftId, confirmed.RecipeJson),
                Path.Combine(_root, "job"),
                sink,
                CancellationToken.None);

        Assert.IsFalse(decision.Succeeded);
        Assert.AreEqual(
            "VFXB0008",
            decision.FailureCode,
            "The build's own verdict outranks the store fault: the user must see why the build failed.");
        CollectionAssert.Contains(sink.Artifacts, RecipeBuildFailureCodes.FailureArtifact("VFXB0008"));
    }

    /// <summary>Reads succeed; both build-outcome transitions refuse, simulating a wedged store file.</summary>
    private sealed class WritebackRefusingStore : IRecipeDraftStore
    {
        private readonly InMemoryRecipeDraftStore _inner;

        internal WritebackRefusingStore(InMemoryRecipeDraftStore inner)
        {
            _inner = inner;
        }

        public RecipeDraftRecord Save(RecipeDraftRecord record) => _inner.Save(record);

        public RecipeDraftRecord Confirm(string draftId, string canonicalSha256) =>
            _inner.Confirm(draftId, canonicalSha256);

        public RecipeDraftRecord? TryGet(string draftId) => _inner.TryGet(draftId);

        public IReadOnlyList<RecipeDraftRecord> ListConfirmedAwaitingBuild() => _inner.ListConfirmedAwaitingBuild();

        public RecipeDraftRecord MarkBuilt(string draftId, string canonicalSha256) =>
            throw new RecipeDraftStoreException(RecipeDraftStoreErrorCode.StorageFailed);

        public RecipeDraftRecord MarkBuildFailed(string draftId, string canonicalSha256) =>
            throw new RecipeDraftStoreException(RecipeDraftStoreErrorCode.StorageFailed);
    }

    private sealed class RecordingSink : IRecipeBuildSink
    {
        internal List<string> Artifacts { get; } = [];

        public void ReportProgress(int progressPermille)
        {
        }

        public void ReportLog(string level, string diagnosticCode)
        {
        }

        public void ReportArtifact(string artifactId) => Artifacts.Add(artifactId);

        public void RegisterChildProcess(int processId, DateTimeOffset processStartUtc)
        {
        }

        public void ClearChildProcess()
        {
        }
    }
}

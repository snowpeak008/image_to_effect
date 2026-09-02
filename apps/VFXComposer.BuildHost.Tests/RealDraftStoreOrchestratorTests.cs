using Microsoft.VisualStudio.TestTools.UnitTesting;
using VFXComposer.AI.Contracts.Recipes;
using VFXComposer.AI.Providers.Recipes;
using VFXComposer.Batch.Core;
using VFXComposer.Protocol.Jobs;

namespace VFXComposer.BuildHost.Tests;

/// <summary>
/// Milestone-audit ledger item ①: the draft-backed chain against the real file-backed
/// <see cref="RecipeDraftStore"/> (not the in-memory fake) — Confirm → successful build → Built;
/// failed build → BuildFailed; a superseded confirmation → DraftNotConfirmed with zero writes.
/// The store file lives in a temporary directory; the Unity process stays faked.
/// </summary>
[TestClass]
public sealed class RealDraftStoreOrchestratorTests
{
    private string _root = string.Empty;

    [TestInitialize]
    public void CreateRoot() => _root = BuildHostTestHarness.CreateDirectory();

    [TestCleanup]
    public void DeleteRoot() => BuildHostTestHarness.DeleteDirectory(_root);

    [TestMethod]
    public async Task AConfirmedDraftInTheRealStoreBuildsAndAdvancesToBuilt()
    {
        var store = CreateStore();
        var saved = store.SaveVersion(BuildHostTestHarness.Draft()).Record;
        var confirmed = store.Confirm(saved.DraftId, saved.CanonicalSha256!);
        var runner = new FakeUnityRecipeBuildRunner(
            UnityRecipeBuildExitCodes.Succeeded,
            _ => BuildHostTestHarness.SuccessResult(confirmed.CanonicalSha256!));

        var decision = await Execute(runner, store, confirmed);

        Assert.IsTrue(decision.Succeeded, decision.FailureCode);
        var persisted = ReopenStore().TryGet(saved.DraftId);
        Assert.AreEqual(
            RecipeDraftStatus.Built,
            persisted!.Status,
            "The Built state must survive a store reopen: it was written to the file, not to a cache.");
    }

    [TestMethod]
    public async Task AFailedBuildInTheRealStoreAdvancesToBuildFailed()
    {
        var store = CreateStore();
        var saved = store.SaveVersion(BuildHostTestHarness.Draft()).Record;
        var confirmed = store.Confirm(saved.DraftId, saved.CanonicalSha256!);
        var runner = new FakeUnityRecipeBuildRunner(
            UnityRecipeBuildExitCodes.StructuredFailure,
            _ => BuildHostTestHarness.FailureResult("VFXB0008"));

        var decision = await Execute(runner, store, confirmed);

        Assert.IsFalse(decision.Succeeded);
        Assert.AreEqual("VFXB0008", decision.FailureCode);
        Assert.AreEqual(RecipeDraftStatus.BuildFailed, ReopenStore().TryGet(saved.DraftId)!.Status);
    }

    [TestMethod]
    public async Task ASupersededConfirmationIsRefusedAsNotConfirmedWithZeroWrites()
    {
        var store = CreateStore();
        var saved = store.SaveVersion(BuildHostTestHarness.Draft()).Record;
        var confirmed = store.Confirm(saved.DraftId, saved.CanonicalSha256!);

        // A newer version landing on the lineage supersedes the confirmation (REQ-004 §7.3 rule 6);
        // the payload still carries the old draft's identity, exactly the stale-click shape.
        var editedJson = BuildHostTestHarness.Recipe(revision: 2);
        var appendOutcome = store.AppendVersion(
            confirmed.DraftId,
            confirmed.CanonicalSha256!,
            new RecipeDraftRevision(
                new RecipeDraft(
                    Guid.NewGuid().ToString("N"),
                    editedJson,
                    RecipeCanonicalJson.ComputeSha256(editedJson),
                    BuildHostTestHarness.EffectId,
                    "projectile",
                    "2d",
                    "mobile_medium",
                    "prompt-template-1",
                    "1.0.0"),
                RecipeDraftOrigin.HumanEdit),
            DateTimeOffset.UtcNow);
        CollectionAssert.Contains(
            appendOutcome.SupersededDraftIds.ToArray(),
            confirmed.DraftId,
            "The append must have superseded the confirmation for this scenario to be real.");

        var runner = new FakeUnityRecipeBuildRunner(
            UnityRecipeBuildExitCodes.Succeeded,
            _ => BuildHostTestHarness.SuccessResult(confirmed.CanonicalSha256!));
        var decision = await Execute(runner, store, confirmed);

        Assert.AreEqual(RecipeBuildFailureCodes.DraftNotConfirmed, decision.FailureCode);
        Assert.AreEqual(0, runner.StartCount, "A superseded confirmation must never start Unity.");
        Assert.AreEqual(RecipeDraftStatus.Superseded, ReopenStore().TryGet(saved.DraftId)!.Status);
        Assert.IsFalse(
            ReopenStore().ListConfirmedAwaitingBuild().Any(record =>
                string.Equals(record.DraftId, confirmed.DraftId, StringComparison.Ordinal)),
            "A superseded version never appears in the awaiting-build backlog (REQ-004 §8 rule 4).");
    }

    private RecipeDraftStore CreateStore() => new(StorePath());

    /// <summary>A fresh instance over the same file, proving state landed on disk.</summary>
    private RecipeDraftStore ReopenStore() => new(StorePath());

    private string StorePath() => Path.Combine(_root, "recipe-drafts.json");

    private Task<RecipeBuildDecision> Execute(
        FakeUnityRecipeBuildRunner runner,
        RecipeDraftStore store,
        RecipeDraftRecord draft)
    {
        var payload = BatchRecipeBuildPayload.Create(draft.DraftId, draft.RecipeJson);
        return new RecipeBuildOrchestrator(runner, () => store, new RecipeBuildOptions { TimeoutSeconds = 30 })
            .ExecuteAsync(payload, Path.Combine(_root, "job"), new NullSink(), CancellationToken.None);
    }

    private sealed class NullSink : IRecipeBuildSink
    {
        public void ReportProgress(int progressPermille)
        {
        }

        public void ReportLog(string level, string diagnosticCode)
        {
        }

        public void ReportArtifact(string artifactId)
        {
        }

        public void RegisterChildProcess(int processId, DateTimeOffset processStartUtc)
        {
        }

        public void ClearChildProcess()
        {
        }
    }
}

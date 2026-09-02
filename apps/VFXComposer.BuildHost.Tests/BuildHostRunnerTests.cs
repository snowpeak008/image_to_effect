using Microsoft.VisualStudio.TestTools.UnitTesting;
using VFXComposer.AI.Contracts.Recipes;
using VFXComposer.Batch.Core;
using VFXComposer.Jobs;
using VFXComposer.Protocol.Jobs;

namespace VFXComposer.BuildHost.Tests;

/// <summary>
/// The full host chain and the ADR-008 §5 fail-closed rows the host itself owns: the happy path
/// (Confirmed → Built with the draft-backed payload), every identity refusal with zero enqueue and
/// zero writes, the executor-lock refusal that leaves the entry queued, the WaitingProjectLock
/// bounded wait against a busy fake probe, and the exit-code mapping. Everything runs against a
/// faked wrapper process — no Unity, no user application data.
/// </summary>
[TestClass]
public sealed class BuildHostRunnerTests
{
    private string _root = string.Empty;

    [TestInitialize]
    public void CreateRoot() => _root = BuildHostTestHarness.CreateDirectory();

    [TestCleanup]
    public void DeleteRoot() => BuildHostTestHarness.DeleteDirectory(_root);

    [TestMethod]
    public async Task AConfirmedDraftIdentityBuildsThroughTheDraftBackedPayloadAndAdvancesToBuilt()
    {
        var store = new InMemoryRecipeDraftStore();
        var draft = Confirm(store);
        var queue = new JobStore(Path.Combine(_root, "jobs"));
        var runner = new FakeUnityRecipeBuildRunner(
            UnityRecipeBuildExitCodes.Succeeded,
            _ => BuildHostTestHarness.SuccessResult(draft.CanonicalSha256!));
        var output = new StringWriter();
        var buildHost = BuildHostTestHarness.CreateBuildHost(_root);

        var exitCode = await BuildHostRunner.RunAsync(
            [draft.DraftId, draft.CanonicalSha256!],
            BuildHostTestHarness.Environment(output, new TestDraftSession(store), queue, buildHost, runner),
            CancellationToken.None);

        Assert.AreEqual(BuildHostExitCodes.BuildSucceeded, exitCode);
        Assert.AreEqual(RecipeDraftStatus.Built, store.TryGet(draft.DraftId)!.Status);
        var job = queue.ReadSnapshot().Jobs.Single();
        Assert.AreEqual(JobStatusStates.Succeeded, job.State);
        Assert.AreEqual(JobSourceEntries.Desktop, job.SourceEntry);
        Assert.AreEqual(BatchJobKinds.RecipeBuild, job.JobKind);

        // The queue entry carries the draft-backed payload: the ADR-008 §1 fact-6 broken link is
        // closed exactly here — this is the draft-backed form's production call site.
        var content = BatchRecipeBuildPayload.Parse(job.Payload);
        Assert.AreEqual(draft.DraftId, content.DraftId);
        Assert.AreEqual(draft.CanonicalSha256, content.CanonicalSha256);
        CollectionAssert.Contains(job.ArtifactIds.ToArray(), "recipe:sha256:" + draft.CanonicalSha256);
    }

    [TestMethod]
    public async Task AFailedBuildAdvancesTheDraftToBuildFailedAndKeepsThePreciseCodeOnTheEntry()
    {
        var store = new InMemoryRecipeDraftStore();
        var draft = Confirm(store);
        var queue = new JobStore(Path.Combine(_root, "jobs"));
        var runner = new FakeUnityRecipeBuildRunner(
            UnityRecipeBuildExitCodes.StructuredFailure,
            _ => BuildHostTestHarness.FailureResult("VFXB0008"));
        var output = new StringWriter();

        var exitCode = await BuildHostRunner.RunAsync(
            [draft.DraftId, draft.CanonicalSha256!],
            BuildHostTestHarness.Environment(
                output, new TestDraftSession(store), queue, BuildHostTestHarness.CreateBuildHost(_root), runner),
            CancellationToken.None);

        Assert.AreEqual(BuildHostExitCodes.BuildFailed, exitCode);
        Assert.AreEqual(RecipeDraftStatus.BuildFailed, store.TryGet(draft.DraftId)!.Status);
        var job = queue.ReadSnapshot().Jobs.Single();
        Assert.AreEqual(JobStatusStates.Failed, job.State);
        CollectionAssert.Contains(
            job.ArtifactIds.ToArray(),
            RecipeBuildFailureCodes.FailureArtifact("VFXB0008"),
            "The precise build code must survive on the queue-visible artifact surface.");
    }

    [TestMethod]
    [DataRow("draft-absent", null, RecipeBuildFailureCodes.DraftNotFound, DisplayName = "UnknownDraftIsRefused")]
    [DataRow(null, null, RecipeBuildFailureCodes.DraftNotConfirmed, DisplayName = "PendingDraftIsRefused")]
    [DataRow(null, "drift", RecipeBuildFailureCodes.DraftHashMismatch, DisplayName = "HashDriftIsRefused")]
    public async Task AForgedOrDriftedIdentityIsRefusedWithZeroEnqueueAndZeroWrites(
        string? draftIdOverride,
        string? hashOverride,
        string expectedCode)
    {
        var store = new InMemoryRecipeDraftStore();
        // The pending draft covers the not-confirmed row; the other rows never need it confirmed.
        var draft = store.Save(BuildHostTestHarness.Draft());
        if (expectedCode == RecipeBuildFailureCodes.DraftHashMismatch)
        {
            draft = store.Confirm(draft.DraftId, draft.CanonicalSha256!);
        }

        var queue = new JobStore(Path.Combine(_root, "jobs"));
        var runner = new FakeUnityRecipeBuildRunner(UnityRecipeBuildExitCodes.Succeeded, _ => null);
        var output = new StringWriter();
        var arguments = new[]
        {
            draftIdOverride ?? draft.DraftId,
            hashOverride is null ? draft.CanonicalSha256! : new string('d', 64),
        };

        var exitCode = await BuildHostRunner.RunAsync(
            arguments,
            BuildHostTestHarness.Environment(
                output, new TestDraftSession(store), queue, BuildHostTestHarness.CreateBuildHost(_root), runner),
            CancellationToken.None);

        Assert.AreEqual(BuildHostExitCodes.DraftIdentityRefused, exitCode);
        StringAssert.Contains(output.ToString(), expectedCode);
        Assert.AreEqual(0, queue.ReadSnapshot().Jobs.Count, "A refused identity must enqueue nothing.");
        Assert.AreEqual(0, runner.StartCount, "A refused identity must never start a wrapper process.");
        Assert.AreEqual(
            draft.Status,
            store.TryGet(draft.DraftId)!.Status,
            "A refused identity must write nothing to the draft store.");
    }

    [TestMethod]
    public async Task ArgumentsOtherThanExactlyTwoIdentitiesAreAUsageError()
    {
        var queue = new JobStore(Path.Combine(_root, "jobs"));
        var output = new StringWriter();
        var environment = BuildHostTestHarness.Environment(
            output,
            new TestDraftSession(new InMemoryRecipeDraftStore()),
            queue,
            BuildHostTestHarness.CreateBuildHost(_root),
            new FakeUnityRecipeBuildRunner(UnityRecipeBuildExitCodes.Succeeded, _ => null));

        Assert.AreEqual(
            BuildHostExitCodes.UsageError,
            await BuildHostRunner.RunAsync(["only-one"], environment, CancellationToken.None));
        Assert.AreEqual(
            BuildHostExitCodes.UsageError,
            await BuildHostRunner.RunAsync(["a", "b", "c"], environment, CancellationToken.None));
        Assert.AreEqual(
            BuildHostExitCodes.UsageError,
            await BuildHostRunner.RunAsync(["a", "  "], environment, CancellationToken.None));
        StringAssert.Contains(output.ToString(), BuildHostDiagnosticCodes.UsageInvalid);
        Assert.AreEqual(0, queue.ReadSnapshot().Jobs.Count);
    }

    [TestMethod]
    public async Task AMissingBuildEnvironmentRefusesBeforeTheDraftStoreAndTheQueue()
    {
        var output = new StringWriter();
        var drafts = new TestDraftSession(new InMemoryRecipeDraftStore());
        var environment = new BuildHostEnvironment
        {
            Output = output,
            OpenDrafts = () => drafts,
            OpenQueue = () => throw new InvalidOperationException("The queue must not be opened."),
            LocateBuildHost = static () => null,
            CreateRunner = static _ => throw new InvalidOperationException("No runner without a build host."),
        };

        var exitCode = await BuildHostRunner.RunAsync(
            ["draft-x", new string('a', 64)], environment, CancellationToken.None);

        Assert.AreEqual(BuildHostExitCodes.BuildEnvironmentUnavailable, exitCode);
        StringAssert.Contains(output.ToString(), BuildHostDiagnosticCodes.BuildEnvironmentUnavailable);
        Assert.IsFalse(drafts.Disposed, "The draft store is never opened when the environment is missing.");
    }

    [TestMethod]
    public async Task AForeignExecutorLockLeavesTheEntryQueuedAndExitsWithTheLockCode()
    {
        var store = new InMemoryRecipeDraftStore();
        var draft = Confirm(store);
        var queue = new JobStore(Path.Combine(_root, "jobs"));
        var runner = new FakeUnityRecipeBuildRunner(UnityRecipeBuildExitCodes.Succeeded, _ => null);
        var output = new StringWriter();

        // A foreign build-capable host already owns execution but is itself blocked on the busy
        // project lock, so it holds the executor lock without consuming the entry — the exact
        // CLI-foreground shape the ADR names as the concurrent holder.
        await using var foreignHost = CreateForeignLockHolder(queue);
        foreignHost.Start();

        var exitCode = await BuildHostRunner.RunAsync(
            [draft.DraftId, draft.CanonicalSha256!],
            BuildHostTestHarness.Environment(
                output, new TestDraftSession(store), queue, BuildHostTestHarness.CreateBuildHost(_root), runner),
            CancellationToken.None);

        Assert.AreEqual(BuildHostExitCodes.ExecutorLockHeld, exitCode);
        StringAssert.Contains(output.ToString(), BuildHostDiagnosticCodes.ExecutorLockHeld);
        var job = queue.ReadSnapshot().Jobs.Single();
        Assert.AreEqual(
            JobStatusStates.Queued,
            job.State,
            "The self-sufficient draft-backed entry stays queued for the lock holder or the next host.");
        Assert.AreEqual(
            RecipeDraftStatus.ConfirmedAwaitingBuild,
            store.TryGet(draft.DraftId)!.Status,
            "A host that could not execute must not advance the draft.");
    }

    [TestMethod]
    public async Task AReClickAfterALockHeldExitAdoptsTheQueuedEntryInsteadOfStackingADuplicate()
    {
        var store = new InMemoryRecipeDraftStore();
        var draft = Confirm(store);
        var queue = new JobStore(Path.Combine(_root, "jobs"));
        var runner = new FakeUnityRecipeBuildRunner(
            UnityRecipeBuildExitCodes.Succeeded,
            _ => BuildHostTestHarness.SuccessResult(draft.CanonicalSha256!));
        var buildHost = BuildHostTestHarness.CreateBuildHost(_root);

        // First run: refused by a foreign lock, entry left queued.
        await using (var foreignHost = CreateForeignLockHolder(queue))
        {
            foreignHost.Start();
            var refused = await BuildHostRunner.RunAsync(
                [draft.DraftId, draft.CanonicalSha256!],
                BuildHostTestHarness.Environment(
                    new StringWriter(), new TestDraftSession(store), queue, buildHost, runner),
                CancellationToken.None);
            Assert.AreEqual(BuildHostExitCodes.ExecutorLockHeld, refused);
        }

        // Second run: the lock is free; the stranded entry is adopted and drained, not duplicated.
        var exitCode = await BuildHostRunner.RunAsync(
            [draft.DraftId, draft.CanonicalSha256!],
            BuildHostTestHarness.Environment(
                new StringWriter(), new TestDraftSession(store), queue, buildHost, runner),
            CancellationToken.None);

        Assert.AreEqual(BuildHostExitCodes.BuildSucceeded, exitCode);
        Assert.AreEqual(1, queue.ReadSnapshot().Jobs.Count, "The re-click must not stack a second entry.");
        Assert.AreEqual(RecipeDraftStatus.Built, store.TryGet(draft.DraftId)!.Status);
    }

    [TestMethod]
    public async Task ABusyProjectLockKeepsTheEntryQueuedInWaitingStateUntilTheEditorCloses()
    {
        var store = new InMemoryRecipeDraftStore();
        var draft = Confirm(store);
        var queue = new JobStore(Path.Combine(_root, "jobs"));
        var runner = new FakeUnityRecipeBuildRunner(
            UnityRecipeBuildExitCodes.Succeeded,
            _ => BuildHostTestHarness.SuccessResult(draft.CanonicalSha256!));
        var probe = new TogglingProjectLockProbe(busy: true);

        var run = BuildHostRunner.RunAsync(
            [draft.DraftId, draft.CanonicalSha256!],
            BuildHostTestHarness.Environment(
                new StringWriter(),
                new TestDraftSession(store),
                queue,
                BuildHostTestHarness.CreateBuildHost(_root),
                runner,
                probe),
            CancellationToken.None);

        // While the probe reports busy, the entry must stay QUEUED and the queue must say why —
        // the same F3 semantics the CLI foreground run gets, now wired in the host (ADR-008 §2.4).
        await WaitUntilAsync(() =>
            queue.ReadSnapshot() is { } snapshot &&
            string.Equals(snapshot.QueueState, JobQueueStates.WaitingProjectLock, StringComparison.Ordinal) &&
            snapshot.Jobs.Single().State == JobStatusStates.Queued);
        Assert.AreEqual(0, runner.StartCount, "No wrapper process may start while the editor owns the project.");
        Assert.IsFalse(run.IsCompleted, "The host waits with the queue instead of failing the entry.");

        probe.SetBusy(false);
        Assert.AreEqual(BuildHostExitCodes.BuildSucceeded, await run);
        Assert.AreEqual(RecipeDraftStatus.Built, store.TryGet(draft.DraftId)!.Status);
    }

    [TestMethod]
    public async Task AUserCancellationWhileWaitingForTheProjectLockSettlesCancelledWithoutABuild()
    {
        var store = new InMemoryRecipeDraftStore();
        var draft = Confirm(store);
        var queue = new JobStore(Path.Combine(_root, "jobs"));
        var runner = new FakeUnityRecipeBuildRunner(UnityRecipeBuildExitCodes.Succeeded, _ => null);
        var probe = new TogglingProjectLockProbe(busy: true);

        var run = BuildHostRunner.RunAsync(
            [draft.DraftId, draft.CanonicalSha256!],
            BuildHostTestHarness.Environment(
                new StringWriter(),
                new TestDraftSession(store),
                queue,
                BuildHostTestHarness.CreateBuildHost(_root),
                runner,
                probe),
            CancellationToken.None);
        await WaitUntilAsync(() => string.Equals(
            queue.ReadSnapshot().QueueState, JobQueueStates.WaitingProjectLock, StringComparison.Ordinal));

        // The Desktop cancel surface is IJobQueueClient.RequestCancel on the shared store; a
        // queued entry settles immediately without the executor ever seeing it.
        queue.RequestCancel(queue.ReadSnapshot().Jobs.Single().JobId);

        Assert.AreEqual(BuildHostExitCodes.BuildCancelled, await run);
        Assert.AreEqual(0, runner.StartCount);
        Assert.AreEqual(
            RecipeDraftStatus.ConfirmedAwaitingBuild,
            store.TryGet(draft.DraftId)!.Status,
            "A cancelled wait leaves the confirmation intact for a later explicit build.");
    }

    [TestMethod]
    public async Task AnUnavailableDraftStoreRefusesWithItsOwnStableCodeBeforeTheQueue()
    {
        var output = new StringWriter();
        var environment = new BuildHostEnvironment
        {
            Output = output,
            OpenDrafts = static () => throw new RecipeDraftStoreException(RecipeDraftStoreErrorCode.StorageFailed),
            OpenQueue = () => throw new InvalidOperationException("The queue must not be opened."),
            LocateBuildHost = () => BuildHostTestHarness.CreateBuildHost(_root),
            CreateRunner = static _ => throw new InvalidOperationException("No runner in this refusal."),
        };

        var exitCode = await BuildHostRunner.RunAsync(
            ["draft-x", new string('a', 64)], environment, CancellationToken.None);

        Assert.AreEqual(BuildHostExitCodes.DraftStoreUnavailable, exitCode);
        StringAssert.Contains(output.ToString(), BuildHostDiagnosticCodes.DraftStoreUnavailable);
    }

    [TestMethod]
    public async Task AnUnavailableQueueStoreRefusesAfterIdentityVerificationWithTheQueueCode()
    {
        var store = new InMemoryRecipeDraftStore();
        var draft = Confirm(store);
        var output = new StringWriter();
        var environment = new BuildHostEnvironment
        {
            Output = output,
            OpenDrafts = () => new TestDraftSession(store),
            OpenQueue = static () => throw new JobQueueException(JobQueueDiagnosticCodes.StoreUnavailable),
            LocateBuildHost = () => BuildHostTestHarness.CreateBuildHost(_root),
            CreateRunner = static _ => throw new InvalidOperationException("No runner in this refusal."),
        };

        var exitCode = await BuildHostRunner.RunAsync(
            [draft.DraftId, draft.CanonicalSha256!], environment, CancellationToken.None);

        Assert.AreEqual(BuildHostExitCodes.QueueUnavailable, exitCode);
        StringAssert.Contains(output.ToString(), BuildHostDiagnosticCodes.QueueUnavailable);
        StringAssert.Contains(output.ToString(), JobQueueDiagnosticCodes.StoreUnavailable);
        Assert.AreEqual(
            RecipeDraftStatus.ConfirmedAwaitingBuild,
            store.TryGet(draft.DraftId)!.Status,
            "A queue refusal must not advance the draft.");
    }

    [TestMethod]
    public void TheHostOutputNeverCarriesRecipeContentOrPaths()
    {
        // The diagnostic stream writes stable code tokens only. This pins the shape by scanning
        // every code the host can emit for path-like or content-like fragments.
        var emittable = new[]
        {
            BuildHostDiagnosticCodes.UsageInvalid,
            BuildHostDiagnosticCodes.DraftStoreUnavailable,
            BuildHostDiagnosticCodes.BuildEnvironmentUnavailable,
            BuildHostDiagnosticCodes.QueueUnavailable,
            BuildHostDiagnosticCodes.ExecutorLockHeld,
            RecipeBuildFailureCodes.DraftNotFound,
            RecipeBuildFailureCodes.DraftNotConfirmed,
            RecipeBuildFailureCodes.DraftHashMismatch,
        };
        foreach (var code in emittable)
        {
            Assert.IsFalse(code.Contains('\\', StringComparison.Ordinal), code);
            Assert.IsFalse(code.Contains('/', StringComparison.Ordinal), code);
            Assert.IsFalse(code.Contains(' ', StringComparison.Ordinal), code);
        }
    }

    private static RecipeDraftRecord Confirm(InMemoryRecipeDraftStore store)
    {
        var saved = store.Save(BuildHostTestHarness.Draft());
        return store.Confirm(saved.DraftId, saved.CanonicalSha256!);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(20);
        while (!condition())
        {
            if (DateTimeOffset.UtcNow > deadline)
            {
                throw new TimeoutException("The expected queue condition was not reached in time.");
            }

            await Task.Delay(25);
        }
    }

    /// <summary>
    /// A queue host that owns the executor lock but cannot consume the build entry: its build-kind
    /// executor is gated behind an always-busy project-lock probe, which is exactly the CLI
    /// foreground shape while an editor owns the project.
    /// </summary>
    private static JobQueueHost CreateForeignLockHolder(JobStore queue) => new(
        queue,
        [new BlockedBuildKindExecutor()],
        BuildHostTestHarness.FastHostOptions,
        new TogglingProjectLockProbe(busy: true));

    private sealed class BlockedBuildKindExecutor : IJobExecutor
    {
        public string JobKind => BatchJobKinds.RecipeBuild;

        public bool RequiresProjectLock => true;

        public Task ExecuteAsync(JobExecutionContext context, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("The blocked foreign holder must never execute.");
    }
}

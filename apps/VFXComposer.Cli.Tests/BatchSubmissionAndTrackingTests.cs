using Microsoft.VisualStudio.TestTools.UnitTesting;
using VFXComposer.Batch.Core;
using VFXComposer.Jobs;
using VFXComposer.Protocol.Jobs;

namespace VFXComposer.Cli.Tests;

[TestClass]
public sealed class BatchSubmissionAndTrackingTests
{
    [TestMethod]
    public void ManifestOrderBecomesQueueOrder()
    {
        var store = new JobStore(CliTestHarness.CreateDirectory());
        var manifest = Manifest("alpha", "beta", "gamma");

        var submission = new BatchSubmissionService(store, JobSourceEntries.Cli).Submit(manifest, force: false);

        CollectionAssert.AreEqual(
            new[] { "alpha", "beta", "gamma" },
            submission.Items.Select(static item => item.ItemId).ToArray());
        var positions = store.ReadSnapshot().Jobs.Select(static job => job.QueuePosition).ToArray();
        CollectionAssert.AreEqual(positions.OrderBy(static position => position).ToArray(), positions);
        Assert.IsTrue(store.ReadSnapshot().Jobs.All(job =>
            job.SourceEntry == JobSourceEntries.Cli &&
            job.JobKind == BatchJobKinds.RecipeGeneration &&
            job.BatchId == manifest.BatchId &&
            job.BatchPolicy == JobBatchPolicies.Continue));
    }

    [TestMethod]
    public async Task CompletedEntriesAreSkippedAndForcedRunsReEnqueueThem()
    {
        var store = new JobStore(CliTestHarness.CreateDirectory());
        var manifest = Manifest("alpha", "beta");
        var service = new BatchSubmissionService(store, JobSourceEntries.Cli);
        var first = service.Submit(manifest, force: false);
        await DrainAsync(store, expectedTerminalJobs: 2);

        var second = service.Submit(manifest, force: false);
        var forced = service.Submit(manifest, force: true);

        Assert.AreEqual(BatchItemDispositions.SkippedIdempotent, second.Items[0].Disposition);
        Assert.IsNull(second.Items[0].JobId);
        Assert.AreEqual(BatchItemDispositions.Enqueued, second.Items[1].Disposition);
        Assert.AreEqual(
            first.Items[0].EntryIdempotencyKey,
            second.Items[0].EntryIdempotencyKey,
            "The content key stays stable across submissions.");
        Assert.IsTrue(forced.Items.All(item => item.Disposition == BatchItemDispositions.Enqueued));
    }

    [TestMethod]
    public void AnUnavailableStoreRejectsTheWholeSubmission()
    {
        var service = new BatchSubmissionService(new UnavailableQueueClient(), JobSourceEntries.Cli);

        var exception = Assert.ThrowsExactly<JobQueueException>(() => service.Submit(Manifest("alpha"), force: false));

        Assert.AreEqual(JobQueueDiagnosticCodes.StoreUnavailable, exception.Code);
    }

    [TestMethod]
    public async Task TrackingCompletesWhenEveryEntryIsTerminal()
    {
        var store = new JobStore(CliTestHarness.CreateDirectory());
        var submission = new BatchSubmissionService(store, JobSourceEntries.Cli).Submit(Manifest("alpha", "beta"), force: false);
        await DrainAsync(store, expectedTerminalJobs: 2);
        var sink = new RecordingTrackingSink();

        var result = await new BatchTracker(store, CliTestHarness.FastTracking)
            .TrackAsync(submission.Items, sink, CancellationToken.None);

        Assert.AreEqual(BatchTrackingStatus.Completed, result.Status);
        Assert.AreEqual(JobStatusStates.Succeeded, result.JobsByItemId["alpha"].State);
        Assert.AreEqual(JobStatusStates.Failed, result.JobsByItemId["beta"].State);
        CollectionAssert.AreEquivalent(new[] { "alpha", "beta" }, sink.Updates.Select(u => u.ItemId).Distinct().ToArray());
    }

    [TestMethod]
    public async Task TrackingStopsImmediatelyWhenTheCallerInterruptsIt()
    {
        var store = new JobStore(CliTestHarness.CreateDirectory());
        var submission = new BatchSubmissionService(store, JobSourceEntries.Cli).Submit(Manifest("alpha"), force: false);
        using var interrupted = new CancellationTokenSource();
        await interrupted.CancelAsync();

        var result = await new BatchTracker(store, CliTestHarness.FastTracking)
            .TrackAsync(submission.Items, new RecordingTrackingSink(), interrupted.Token);

        Assert.AreEqual(BatchTrackingStatus.Interrupted, result.Status);
        Assert.AreEqual(
            JobStatusStates.Queued,
            store.ReadSnapshot().Jobs.Single().State,
            "An interrupted run leaves its entries queued for the executor.");
    }

    [TestMethod]
    public async Task TrackingGivesUpWhenTheProjectLockWaitExceedsTheBound()
    {
        var store = new JobStore(CliTestHarness.CreateDirectory());
        var submission = new BatchSubmissionService(store, JobSourceEntries.Cli).Submit(Manifest("alpha"), force: false);
        var sink = new RecordingTrackingSink();
        var options = CliTestHarness.FastTracking with { ProjectLockTimeout = TimeSpan.Zero };

        var result = await new BatchTracker(new ProjectLockWaitingQueueClient(store), options)
            .TrackAsync(submission.Items, sink, CancellationToken.None);

        Assert.AreEqual(BatchTrackingStatus.ProjectLockTimeout, result.Status);
        Assert.AreEqual(1, sink.ProjectLockNotices);
    }

    [TestMethod]
    public async Task TrackingReportsAnUnreadableStore()
    {
        var submission = new BatchSubmissionResult(
            "batch-a",
            JobBatchPolicies.Continue,
            [new BatchSubmissionItem("alpha", "sha256:" + new string('a', 64), BatchItemDispositions.Enqueued, "job-a")]);

        var result = await new BatchTracker(new UnavailableQueueClient(), CliTestHarness.FastTracking)
            .TrackAsync(submission.Items, new RecordingTrackingSink(), CancellationToken.None);

        Assert.AreEqual(BatchTrackingStatus.StoreUnavailable, result.Status);
    }

    [TestMethod]
    public async Task TrackingAFullySkippedBatchCompletesWithoutReadingTheQueue()
    {
        var items = new[]
        {
            new BatchSubmissionItem("alpha", "sha256:" + new string('a', 64), BatchItemDispositions.SkippedIdempotent, null),
        };

        var result = await new BatchTracker(new UnavailableQueueClient(), CliTestHarness.FastTracking)
            .TrackAsync(items, new RecordingTrackingSink(), CancellationToken.None);

        Assert.AreEqual(BatchTrackingStatus.Completed, result.Status);
        Assert.AreEqual(0, result.JobsByItemId.Count);
    }

    internal static BatchManifest Manifest(params string[] itemIds) =>
        new(
            BatchManifestLimits.SchemaVersion,
            "batch-order",
            BatchFailurePolicies.Continue,
            itemIds
                .Select(static itemId => new BatchManifestItem(
                    itemId,
                    BatchItemKinds.Prompt,
                    "prompt for " + itemId,
                    null,
                    BatchConstraints.Empty))
                .ToArray());

    /// <summary>Drains the queue with a channel that fails exactly the "beta" entry.</summary>
    private static Task DrainAsync(JobStore store, int expectedTerminalJobs) =>
        CliTestHarness.DrainAsync(
            store,
            new RecipeGenerationJobExecutor(
                () => CliTestHarness.Channel(static description => description.Contains("beta", StringComparison.Ordinal)),
                () => new InMemoryRecipeDraftStore()),
            expectedTerminalJobs);
}

/// <summary>Collects tracking callbacks for assertions.</summary>
internal sealed class RecordingTrackingSink : IBatchTrackingSink
{
    private readonly List<BatchTrackingUpdate> _updates = [];

    public IReadOnlyList<BatchTrackingUpdate> Updates
    {
        get
        {
            lock (_updates)
            {
                return _updates.ToArray();
            }
        }
    }

    public int ProjectLockNotices { get; private set; }

    public void OnJobUpdated(BatchTrackingUpdate update)
    {
        lock (_updates)
        {
            _updates.Add(update);
        }
    }

    public void OnWaitingProjectLock() => ProjectLockNotices++;
}

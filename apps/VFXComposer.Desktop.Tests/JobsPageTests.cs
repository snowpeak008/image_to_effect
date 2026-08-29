using Microsoft.VisualStudio.TestTools.UnitTesting;
using VFXComposer.Desktop.ViewModels;
using VFXComposer.Jobs;
using VFXComposer.Protocol.Jobs;

namespace VFXComposer.Desktop.Tests;

[TestClass]
public sealed class JobsPageTests
{
    private const string SensitivePayload = "SENSITIVE-PROMPT with D:/fake/unity/project path";

    [TestMethod]
    public void PlaceholderConstructorKeepsTheEmptyStateAndNeverThrows()
    {
        var viewModel = new JobsViewModel();

        viewModel.Refresh();

        Assert.AreEqual("jobs", viewModel.Key);
        Assert.IsFalse(viewModel.HasJobs);
        Assert.AreEqual("No jobs are running", viewModel.EmptyStateMessage);
    }

    [TestMethod]
    public void ListShowsRunningFirstWithStatesProgressAndDiagnosticCodes()
    {
        var queue = new FakeJobQueueClient();
        queue.AddQueued("job-queued0001");
        queue.AddRunning("job-running001", progressPermille: 420);
        queue.AddFailed("job-failed0001");
        var viewModel = new JobsViewModel(queue);

        viewModel.Refresh();

        Assert.IsTrue(viewModel.HasJobs);
        var rows = viewModel.Jobs.ToArray();
        Assert.AreEqual(3, rows.Length);
        Assert.AreEqual("job-running001", rows[0].JobId, "The running job must be pinned first.");
        Assert.IsTrue(rows[0].IsRunning);
        Assert.AreEqual("42%", rows[0].ProgressDisplay);
        Assert.AreEqual(JobStatusStates.Queued, rows[1].State);
        Assert.AreEqual(JobStatusStates.Failed, rows[2].State);
        Assert.AreEqual(JobQueueDiagnosticCodes.ExecutionFailed, rows[2].DiagnosticDisplay);
        Assert.IsTrue(rows[2].CanResubmit);
        Assert.IsFalse(rows[2].CanCancel);
    }

    [TestMethod]
    public void WaitingProjectLockBannerAppearsAndClears()
    {
        var queue = new FakeJobQueueClient { QueueState = JobQueueStates.WaitingProjectLock };
        queue.AddQueued("job-queued0001");
        var viewModel = new JobsViewModel(queue);

        viewModel.Refresh();
        Assert.IsTrue(viewModel.IsWaitingForProjectLock);
        Assert.IsTrue(viewModel.QueueStatus.Contains(JobQueueDiagnosticCodes.WaitingProjectLock, StringComparison.Ordinal));

        queue.QueueState = JobQueueStates.Idle;
        viewModel.Refresh();
        Assert.IsFalse(viewModel.IsWaitingForProjectLock);
    }

    [TestMethod]
    public void CancellationIsTwoStepAndOnlyTheConfirmationReachesTheQueue()
    {
        var queue = new FakeJobQueueClient();
        queue.AddQueued("job-queued0001");
        var viewModel = new JobsViewModel(queue);
        viewModel.Refresh();
        var row = viewModel.Jobs.Single();

        viewModel.RequestCancelCommand.Execute(row);
        Assert.IsTrue(row.IsCancelPending);
        Assert.AreEqual(0, queue.CancelRequests.Count, "Arming the confirmation must not cancel yet.");

        viewModel.DismissCancelCommand.Execute(row);
        Assert.IsFalse(row.IsCancelPending);
        Assert.AreEqual(0, queue.CancelRequests.Count);

        viewModel.RequestCancelCommand.Execute(row);
        viewModel.ConfirmCancelCommand.Execute(row);
        CollectionAssert.AreEqual(new[] { "job-queued0001" }, queue.CancelRequests);
    }

    [TestMethod]
    public void ResubmitTargetsOnlyFailedOrDisconnectedJobs()
    {
        var queue = new FakeJobQueueClient();
        queue.AddFailed("job-failed0001");
        queue.AddQueued("job-queued0001");
        var viewModel = new JobsViewModel(queue);
        viewModel.Refresh();

        viewModel.ResubmitCommand.Execute(viewModel.Jobs.Single(job => job.JobId == "job-queued0001"));
        Assert.AreEqual(0, queue.Resubmissions.Count, "A queued job must not be resubmittable.");

        viewModel.ResubmitCommand.Execute(viewModel.Jobs.Single(job => job.JobId == "job-failed0001"));
        CollectionAssert.AreEqual(new[] { "job-failed0001" }, queue.Resubmissions);
    }

    [TestMethod]
    public void TimelineShowsStableCodesAndCatalogTextForTheSelectedJob()
    {
        var queue = new FakeJobQueueClient();
        queue.AddFailed("job-failed0001");
        var viewModel = new JobsViewModel(queue);
        viewModel.Refresh();

        viewModel.SelectedJob = viewModel.Jobs.Single();

        Assert.AreNotEqual(0, viewModel.SelectedJobTimeline.Count);
        Assert.IsTrue(viewModel.SelectedJobTimeline[0].Contains("STATUS QUEUED", StringComparison.Ordinal));
        Assert.IsTrue(viewModel.SelectedJobTimeline[^1].Contains(JobQueueDiagnosticCodes.ExecutionFailed, StringComparison.Ordinal));
        Assert.IsTrue(viewModel.SelectedDiagnostic.Contains(JobQueueDiagnosticCodes.ExecutionFailed, StringComparison.Ordinal));
        Assert.IsTrue(viewModel.SelectedDiagnostic.Contains("The job payload failed.", StringComparison.Ordinal));
    }

    [TestMethod]
    public void ItemIdIsShownForBatchEntriesAndOmittedForEveryOtherJob()
    {
        var queue = new FakeJobQueueClient();
        queue.AddQueuedBatchEntry("job-batch00001", "batch-alpha", "fire_slash-01");
        queue.AddQueued("job-solo000001");
        var viewModel = new JobsViewModel(queue);
        viewModel.Refresh();

        var batchRow = viewModel.Jobs.Single(job => job.JobId == "job-batch00001");
        Assert.IsTrue(batchRow.HasItemId);
        Assert.AreEqual("fire_slash-01", batchRow.ItemId);
        var soloRow = viewModel.Jobs.Single(job => job.JobId == "job-solo000001");
        Assert.IsFalse(soloRow.HasItemId, "A job without an item id must not render a placeholder value.");
        Assert.IsNull(soloRow.ItemId);

        viewModel.SelectedJob = batchRow;
        Assert.AreEqual("Batch item fire_slash-01", viewModel.SelectedItemDisplay);

        viewModel.SelectedJob = soloRow;
        Assert.AreEqual(string.Empty, viewModel.SelectedItemDisplay, "The detail line must be absent, not a dash.");

        viewModel.SelectedJob = null;
        Assert.AreEqual(string.Empty, viewModel.SelectedItemDisplay);
    }

    [TestMethod]
    public void NoRenderedTextEverContainsThePayloadOrAPath()
    {
        var queue = new FakeJobQueueClient();
        queue.AddRunning("job-running001", progressPermille: 100);
        queue.AddFailed("job-failed0001");
        queue.AddQueuedBatchEntry("job-batch00001", "batch-alpha", "fire_slash-01");
        var viewModel = new JobsViewModel(queue);
        viewModel.Refresh();
        viewModel.SelectedJob = viewModel.Jobs.Last();

        var rendered = new List<string>
        {
            viewModel.QueueStatus,
            viewModel.StoreStatus,
            viewModel.SelectedDiagnostic,
            viewModel.SelectedArtifacts,
            viewModel.SelectedItemDisplay,
        };
        rendered.AddRange(viewModel.SelectedJobTimeline);
        foreach (var row in viewModel.Jobs)
        {
            rendered.AddRange(
            [
                row.ShortJobId, row.SourceEntry, row.JobKind, row.BatchDisplay, row.State,
                row.ProgressDisplay, row.EnqueuedDisplay, row.StartedDisplay, row.CompletedDisplay,
                row.DiagnosticDisplay, row.ItemId ?? string.Empty,
            ]);
        }

        foreach (var text in rendered)
        {
            Assert.IsFalse(text.Contains("SENSITIVE-PROMPT", StringComparison.Ordinal), text);
            Assert.IsFalse(text.Contains("fake/unity", StringComparison.Ordinal), text);
        }
    }

    [TestMethod]
    public void StoreFailureSurfacesOnlyTheStableCode()
    {
        var queue = new FakeJobQueueClient { ThrowOnRead = true };
        var viewModel = new JobsViewModel(queue);

        viewModel.Refresh();

        Assert.IsTrue(viewModel.StoreStatus.Contains(JobQueueDiagnosticCodes.StoreUnavailable, StringComparison.Ordinal));
        Assert.IsFalse(viewModel.StoreStatus.Contains(":\\", StringComparison.Ordinal));
        Assert.IsFalse(viewModel.StoreStatus.Contains(":/", StringComparison.Ordinal));
    }

    /// <summary>Synthetic queue built from public store records; no filesystem or network involved.</summary>
    private sealed class FakeJobQueueClient : IJobQueueClient
    {
        private readonly List<JobRecord> _jobs = [];
        private readonly Dictionary<string, List<JobStoreEvent>> _events = new(StringComparer.Ordinal);
        private long _nextPosition = 1;

        public string QueueState { get; set; } = JobQueueStates.Idle;

        public bool ThrowOnRead { get; init; }

        public List<string> CancelRequests { get; } = [];

        public List<string> Resubmissions { get; } = [];

        public void AddQueued(string jobId) => Add(jobId, JobStatusStates.Queued, 0, null);

        public void AddRunning(string jobId, int progressPermille) =>
            Add(jobId, JobStatusStates.Running, progressPermille, null);

        public void AddFailed(string jobId) =>
            Add(jobId, JobStatusStates.Failed, 500, JobQueueDiagnosticCodes.ExecutionFailed);

        public void AddQueuedBatchEntry(string jobId, string batchId, string itemId) =>
            Add(jobId, JobStatusStates.Queued, 0, null, batchId, itemId);

        public JobQueueSnapshotView ReadSnapshot() =>
            ThrowOnRead
                ? throw new JobQueueException(JobQueueDiagnosticCodes.StoreUnavailable)
                : new JobQueueSnapshotView(QueueState, _jobs.ToArray());

        public IReadOnlyList<JobStoreEvent> ReadEvents(string jobId) =>
            _events.TryGetValue(jobId, out var events) ? events : Array.Empty<JobStoreEvent>();

        public JobRecord Enqueue(JobEnqueueRequest request) =>
            throw new NotSupportedException("The Jobs page never enqueues.");

        public JobCancellationResult RequestCancel(string jobId)
        {
            CancelRequests.Add(jobId);
            return new JobCancellationResult(JobStatusStates.Cancelled, Accepted: true);
        }

        public JobRecord Resubmit(string jobId)
        {
            Resubmissions.Add(jobId);
            return _jobs.Single(job => job.JobId == jobId);
        }

        private void Add(
            string jobId,
            string state,
            int progressPermille,
            string? diagnosticCode,
            string? batchId = null,
            string? itemId = null)
        {
            var enqueued = new DateTimeOffset(2026, 8, 29, 2, 0, 0, TimeSpan.Zero);
            var running = !string.Equals(state, JobStatusStates.Queued, StringComparison.Ordinal);
            var terminal = string.Equals(state, JobStatusStates.Failed, StringComparison.Ordinal);
            var record = new JobRecord(
                jobId,
                "req-" + jobId,
                "idk-" + jobId,
                JobEntryIdempotency.Derive(batchId, itemId, SensitivePayload),
                batchId,
                batchId is null ? null : JobBatchPolicies.Continue,
                JobSourceEntries.Desktop,
                "test.job",
                SensitivePayload,
                _nextPosition++,
                enqueued,
                running ? enqueued.AddSeconds(1) : null,
                terminal ? enqueued.AddSeconds(5) : null,
                state,
                cancelRequested: false,
                lastEventSequence: terminal ? 3 : 1,
                progressPermille,
                terminal ? diagnosticCode : null,
                Array.Empty<string>(),
                childProcessId: null,
                childProcessStartUtc: null,
                itemId: itemId);
            _jobs.Add(record);
            var timeline = new List<JobStoreEvent>
            {
                new(
                    JobStoreEvent.CurrentSchema,
                    jobId,
                    1,
                    JobStoreEventKinds.Status,
                    enqueued,
                    JobStatusStates.Queued,
                    progressPermille: null,
                    level: null,
                    diagnosticCode: null,
                    outcome: null,
                    artifactId: null),
            };
            if (terminal)
            {
                timeline.Add(new JobStoreEvent(
                    JobStoreEvent.CurrentSchema,
                    jobId,
                    3,
                    JobStoreEventKinds.Completion,
                    enqueued.AddSeconds(5),
                    state: null,
                    progressPermille: null,
                    level: null,
                    diagnosticCode: diagnosticCode,
                    outcome: JobCompletionOutcomes.Failed,
                    artifactId: null));
            }

            _events[jobId] = timeline;
        }
    }
}

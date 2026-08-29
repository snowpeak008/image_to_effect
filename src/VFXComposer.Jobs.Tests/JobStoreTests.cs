using Microsoft.VisualStudio.TestTools.UnitTesting;
using VFXComposer.Protocol.Jobs;

namespace VFXComposer.Jobs.Tests;

[TestClass]
public sealed class JobStoreTests
{
    [TestMethod]
    public void EnqueueAssignsDistinctIdentityTokensAndStableEntryKey()
    {
        var store = new JobStore(JobQueueTestHarness.CreateStoreDirectory());

        var job = store.Enqueue(JobQueueTestHarness.Request(payload: "content-a", itemId: null));

        Assert.AreEqual(JobStatusStates.Queued, job.State);
        var tokens = new[] { job.JobId, job.RequestId, job.IdempotencyKey };
        Assert.AreEqual(3, tokens.Distinct(StringComparer.Ordinal).Count());
        Assert.AreEqual(
            JobEntryIdempotency.Derive(null, null, "test.job\ncontent-a"),
            job.EntryIdempotencyKey);
        Assert.AreEqual(1, job.LastEventSequence);
        var events = store.ReadEvents(job.JobId);
        Assert.AreEqual(1, events.Count);
        Assert.AreEqual(JobStoreEventKinds.Status, events[0].Kind);
        Assert.AreEqual(JobStatusStates.Queued, events[0].State);
    }

    [TestMethod]
    public void EnqueueBeyondThePendingBoundIsRejectedWithTheStableQueueFullError()
    {
        var store = new JobStore(
            JobQueueTestHarness.CreateStoreDirectory(),
            new JobStoreOptions { MaximumPendingJobs = 2 });
        store.Enqueue(JobQueueTestHarness.Request());
        store.Enqueue(JobQueueTestHarness.Request());

        var exception = Assert.ThrowsExactly<JobQueueException>(
            () => store.Enqueue(JobQueueTestHarness.Request()));

        Assert.AreEqual(JobQueueDiagnosticCodes.QueueFull, exception.Code);
    }

    [TestMethod]
    public void QueuedJobsAreClaimedStrictlyInEnqueueOrder()
    {
        var store = new JobStore(JobQueueTestHarness.CreateStoreDirectory());
        var first = store.Enqueue(JobQueueTestHarness.Request(payload: "one"));
        var second = store.Enqueue(JobQueueTestHarness.Request(payload: "two"));

        Assert.AreEqual(first.JobId, store.PeekNextQueued()!.JobId);
        Assert.IsNull(store.TryClaim(second.JobId), "Claiming a non-head job must not succeed.");
        var claimed = store.TryClaim(first.JobId);

        Assert.IsNotNull(claimed);
        Assert.AreEqual(JobStatusStates.Running, claimed.State);
        Assert.AreEqual(second.JobId, store.PeekNextQueued()!.JobId);
    }

    [TestMethod]
    public void CancellingAQueuedJobSettlesItImmediatelyWithTheQueuedCancelDiagnostic()
    {
        var store = new JobStore(JobQueueTestHarness.CreateStoreDirectory());
        var job = store.Enqueue(JobQueueTestHarness.Request());

        var result = store.RequestCancel(job.JobId);

        Assert.IsTrue(result.Accepted);
        Assert.AreEqual(JobStatusStates.Cancelled, result.State);
        var settled = JobQueueTestHarness.GetJob(store, job.JobId);
        Assert.AreEqual(JobQueueDiagnosticCodes.CancelledQueued, settled.FinalDiagnosticCode);
        var completion = store.ReadEvents(job.JobId).Single(e => e.Kind == JobStoreEventKinds.Completion);
        Assert.AreEqual(JobCompletionOutcomes.Cancelled, completion.Outcome);
        Assert.AreEqual(JobQueueDiagnosticCodes.CancelledQueued, completion.DiagnosticCode);
    }

    [TestMethod]
    public void CancellingARunningJobOnlyMarksTheCooperativeRequest()
    {
        var store = new JobStore(JobQueueTestHarness.CreateStoreDirectory());
        var job = store.Enqueue(JobQueueTestHarness.Request());
        store.TryClaim(job.JobId);

        var result = store.RequestCancel(job.JobId);

        Assert.IsTrue(result.Accepted);
        Assert.AreEqual(JobStatusStates.Running, result.State);
        Assert.IsTrue(JobQueueTestHarness.GetJob(store, job.JobId).CancelRequested);
        var progress = store.ReadEvents(job.JobId).Last(e => e.Kind == JobStoreEventKinds.Progress);
        Assert.AreEqual(JobProgressStates.CancellationRequested, progress.State);
    }

    [TestMethod]
    public void CancellingATerminalJobIsAnIdempotentNoOp()
    {
        var store = new JobStore(JobQueueTestHarness.CreateStoreDirectory());
        var job = store.Enqueue(JobQueueTestHarness.Request());
        store.TryClaim(job.JobId);
        store.Complete(job.JobId, JobStatusStates.Succeeded, diagnosticCode: null);
        var eventCount = store.ReadEvents(job.JobId).Count;

        var result = store.RequestCancel(job.JobId);

        Assert.IsFalse(result.Accepted);
        Assert.AreEqual(JobStatusStates.Succeeded, result.State);
        Assert.AreEqual(eventCount, store.ReadEvents(job.JobId).Count, "A no-op must not append events.");
    }

    [TestMethod]
    public void TerminalJobsRejectEveryFurtherTransition()
    {
        var store = new JobStore(JobQueueTestHarness.CreateStoreDirectory());
        var job = store.Enqueue(JobQueueTestHarness.Request());
        store.TryClaim(job.JobId);
        store.Complete(job.JobId, JobStatusStates.Failed, JobQueueDiagnosticCodes.ExecutionFailed);

        Assert.AreEqual(
            JobQueueDiagnosticCodes.InvalidTransition,
            Assert.ThrowsExactly<JobQueueException>(
                () => store.Complete(job.JobId, JobStatusStates.Succeeded, null)).Code);
        Assert.AreEqual(
            JobQueueDiagnosticCodes.InvalidTransition,
            Assert.ThrowsExactly<JobQueueException>(
                () => store.ReportProgress(job.JobId, 900)).Code);
    }

    [TestMethod]
    public void CompletingAQueuedJobAsSucceededIsRejected()
    {
        var store = new JobStore(JobQueueTestHarness.CreateStoreDirectory());
        var job = store.Enqueue(JobQueueTestHarness.Request());

        var exception = Assert.ThrowsExactly<JobQueueException>(
            () => store.Complete(job.JobId, JobStatusStates.Succeeded, null));

        Assert.AreEqual(JobQueueDiagnosticCodes.InvalidTransition, exception.Code);
    }

    [TestMethod]
    public void CompletionDiagnosticShapeFollowsTheOutcomeVocabulary()
    {
        var store = new JobStore(JobQueueTestHarness.CreateStoreDirectory());
        var job = store.Enqueue(JobQueueTestHarness.Request());
        store.TryClaim(job.JobId);

        Assert.ThrowsExactly<ArgumentException>(
            () => store.Complete(job.JobId, JobStatusStates.Succeeded, JobQueueDiagnosticCodes.ExecutionFailed));
        Assert.ThrowsExactly<ArgumentException>(
            () => store.Complete(job.JobId, JobStatusStates.Failed, diagnosticCode: null));

        store.Complete(job.JobId, JobStatusStates.Succeeded, diagnosticCode: null);
        Assert.AreEqual(1000, JobQueueTestHarness.GetJob(store, job.JobId).LastProgressPermille);
    }

    [TestMethod]
    public void ProgressIsMonotonicAndSequencesAreStrictlyIncreasing()
    {
        var store = new JobStore(JobQueueTestHarness.CreateStoreDirectory());
        var job = store.Enqueue(JobQueueTestHarness.Request());
        store.TryClaim(job.JobId);
        store.ReportProgress(job.JobId, 250);
        store.ReportProgress(job.JobId, 250);
        store.ReportProgress(job.JobId, 800);

        Assert.AreEqual(
            JobQueueDiagnosticCodes.InvalidTransition,
            Assert.ThrowsExactly<JobQueueException>(() => store.ReportProgress(job.JobId, 700)).Code);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => store.ReportProgress(job.JobId, 1001));

        var events = store.ReadEvents(job.JobId);
        CollectionAssert.AreEqual(
            Enumerable.Range(1, events.Count).Select(i => (long)i).ToArray(),
            events.Select(e => e.EventSequence).ToArray());
    }

    [TestMethod]
    public void ArtifactsBeyondTheBoundedCountAreRejectedAndLeaveTheJobUntouched()
    {
        var store = new JobStore(JobQueueTestHarness.CreateStoreDirectory());
        var job = store.Enqueue(JobQueueTestHarness.Request());
        store.TryClaim(job.JobId);
        for (var index = 0; index < JobRecord.MaximumArtifactCount; index++)
        {
            store.AppendArtifact(job.JobId, "artifact-" + index);
        }

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => store.AppendArtifact(job.JobId, "artifact-overflow"));

        var current = JobQueueTestHarness.GetJob(store, job.JobId);
        Assert.AreEqual(JobRecord.MaximumArtifactCount, current.ArtifactIds.Count);
        CollectionAssert.DoesNotContain(current.ArtifactIds.ToArray(), "artifact-overflow");
        Assert.AreEqual(
            JobRecord.MaximumArtifactCount,
            store.ReadEvents(job.JobId).Count(e => e.Kind == JobStoreEventKinds.Artifact),
            "A rejected artifact must not leave an event behind.");
    }

    [TestMethod]
    public void PayloadBeyondTheBoundedLengthIsRejectedBeforeItReachesTheStore()
    {
        var store = new JobStore(JobQueueTestHarness.CreateStoreDirectory());
        var atBound = new string('p', JobRecord.MaximumPayloadLength);
        var accepted = store.Enqueue(JobQueueTestHarness.Request(payload: atBound));

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => JobQueueTestHarness.Request(payload: atBound + "p"));

        var jobs = store.ReadSnapshot().Jobs;
        Assert.AreEqual(1, jobs.Count, "The rejected submission must never reach the store.");
        Assert.AreEqual(accepted.JobId, jobs[0].JobId);
    }

    [TestMethod]
    public void ResubmitCreatesAFreshJobWithTheSameEntryKeyAndLeavesTheOriginalUntouched()
    {
        var store = new JobStore(JobQueueTestHarness.CreateStoreDirectory());
        var original = store.Enqueue(JobQueueTestHarness.Request(payload: "stable-content", itemId: "item-1"));
        store.TryClaim(original.JobId);
        store.Complete(original.JobId, JobStatusStates.Failed, JobQueueDiagnosticCodes.ExecutionFailed);
        var originalSettled = JobQueueTestHarness.GetJob(store, original.JobId);

        var resubmitted = store.Resubmit(original.JobId);

        Assert.AreNotEqual(original.JobId, resubmitted.JobId);
        Assert.AreNotEqual(original.RequestId, resubmitted.RequestId);
        Assert.AreNotEqual(original.IdempotencyKey, resubmitted.IdempotencyKey);
        Assert.AreEqual(original.EntryIdempotencyKey, resubmitted.EntryIdempotencyKey);
        Assert.AreEqual(original.Payload, resubmitted.Payload);
        Assert.AreEqual(JobStatusStates.Queued, resubmitted.State);
        Assert.AreEqual(originalSettled, JobQueueTestHarness.GetJob(store, original.JobId));
    }

    [TestMethod]
    public void ResubmittingANonTerminalJobIsRejected()
    {
        var store = new JobStore(JobQueueTestHarness.CreateStoreDirectory());
        var job = store.Enqueue(JobQueueTestHarness.Request());

        var exception = Assert.ThrowsExactly<JobQueueException>(() => store.Resubmit(job.JobId));

        Assert.AreEqual(JobQueueDiagnosticCodes.InvalidTransition, exception.Code);
    }

    [TestMethod]
    public void CancellingAnUnknownJobReportsTheStableNotFoundError()
    {
        var store = new JobStore(JobQueueTestHarness.CreateStoreDirectory());

        var exception = Assert.ThrowsExactly<JobQueueException>(
            () => store.RequestCancel("job-missing"));

        Assert.AreEqual(JobQueueDiagnosticCodes.JobNotFound, exception.Code);
    }

    [TestMethod]
    public void CorruptPrimaryIsRecoveredFromTheBackup()
    {
        var directory = JobQueueTestHarness.CreateStoreDirectory();
        var store = new JobStore(directory);
        var first = store.Enqueue(JobQueueTestHarness.Request(payload: "first"));
        store.Enqueue(JobQueueTestHarness.Request(payload: "second"));
        var primaryPath = Path.Combine(directory, "job-store.json");
        File.WriteAllText(primaryPath, "{ this is not a snapshot ");

        var view = store.ReadSnapshot();

        // The backup precedes the corrupted write by one revision: it holds the first job.
        Assert.AreEqual(1, view.Jobs.Count);
        Assert.AreEqual(first.JobId, view.Jobs[0].JobId);
        var restored = store.ReadSnapshot();
        Assert.AreEqual(1, restored.Jobs.Count, "Primary must be restored from backup, not rebuilt empty.");
    }

    [TestMethod]
    public void CorruptPrimaryAndBackupFailClosedWithoutRebuilding()
    {
        var directory = JobQueueTestHarness.CreateStoreDirectory();
        var store = new JobStore(directory);
        store.Enqueue(JobQueueTestHarness.Request());
        store.Enqueue(JobQueueTestHarness.Request());
        File.WriteAllText(Path.Combine(directory, "job-store.json"), "corrupt-primary");
        File.WriteAllText(Path.Combine(directory, "job-store.json.bak"), "corrupt-backup");

        Assert.AreEqual(
            JobQueueDiagnosticCodes.StoreUnavailable,
            Assert.ThrowsExactly<JobQueueException>(() => store.ReadSnapshot()).Code);
        Assert.AreEqual(
            JobQueueDiagnosticCodes.StoreUnavailable,
            Assert.ThrowsExactly<JobQueueException>(
                () => store.Enqueue(JobQueueTestHarness.Request())).Code);
        Assert.AreEqual("corrupt-primary", File.ReadAllText(Path.Combine(directory, "job-store.json")));
        Assert.AreEqual("corrupt-backup", File.ReadAllText(Path.Combine(directory, "job-store.json.bak")));
    }

    [TestMethod]
    public void UnknownStoreSchemaVersionFailsClosedWithoutMigration()
    {
        var directory = JobQueueTestHarness.CreateStoreDirectory();
        var store = new JobStore(directory);
        store.Enqueue(JobQueueTestHarness.Request());
        var futureSnapshot = """{"schema":"vfxcomposer.job-store/3","queueState":"IDLE","nextQueuePosition":2,"jobs":[]}""";
        File.WriteAllText(Path.Combine(directory, "job-store.json"), futureSnapshot);
        File.Delete(Path.Combine(directory, "job-store.json.bak"));

        var exception = Assert.ThrowsExactly<JobQueueException>(() => store.ReadSnapshot());

        Assert.AreEqual(JobQueueDiagnosticCodes.StoreVersionUnsupported, exception.Code);
    }

    [TestMethod]
    public void UnknownSnapshotFieldsAreRejectedFailClosed()
    {
        var directory = JobQueueTestHarness.CreateStoreDirectory();
        var store = new JobStore(directory);
        var unknownField = """{"schema":"vfxcomposer.job-store/2","queueState":"IDLE","nextQueuePosition":1,"jobs":[],"surprise":true}""";
        File.WriteAllText(Path.Combine(directory, "job-store.json"), unknownField);

        var exception = Assert.ThrowsExactly<JobQueueException>(() => store.ReadSnapshot());

        Assert.AreEqual(JobQueueDiagnosticCodes.StoreUnavailable, exception.Code);
    }

    [TestMethod]
    public void MalformedMiddleEventLineFailsClosedWhileATornFinalLineIsTolerated()
    {
        var directory = JobQueueTestHarness.CreateStoreDirectory();
        var store = new JobStore(directory);
        var job = store.Enqueue(JobQueueTestHarness.Request());
        var eventsPath = Path.Combine(directory, "job-events.jsonl");

        File.AppendAllText(eventsPath, "{\"torn\":");
        Assert.AreEqual(1, store.ReadEvents(job.JobId).Count, "A torn final line must not break reading.");

        File.AppendAllText(eventsPath, "\n" + JobStoreCodecProbe.SerializeStatusLine(job) + "\n");
        Assert.AreEqual(
            JobQueueDiagnosticCodes.StoreUnavailable,
            Assert.ThrowsExactly<JobQueueException>(() => store.ReadEvents(job.JobId)).Code);
    }

    [TestMethod]
    public void RetentionPrunesOldestTerminalJobsAndTheirEventsOnly()
    {
        var store = new JobStore(
            JobQueueTestHarness.CreateStoreDirectory(),
            new JobStoreOptions { MaximumTerminalJobs = 1 });
        var oldest = store.Enqueue(JobQueueTestHarness.Request(payload: "a"));
        store.TryClaim(oldest.JobId);
        store.Complete(oldest.JobId, JobStatusStates.Succeeded, null);
        var newest = store.Enqueue(JobQueueTestHarness.Request(payload: "b"));
        store.TryClaim(newest.JobId);
        store.Complete(newest.JobId, JobStatusStates.Succeeded, null);
        var pending = store.Enqueue(JobQueueTestHarness.Request(payload: "c"));

        var removed = store.CleanupTerminalJobs();

        Assert.AreEqual(1, removed);
        var remaining = store.ReadSnapshot().Jobs.Select(job => job.JobId).ToArray();
        CollectionAssert.AreEquivalent(new[] { newest.JobId, pending.JobId }, remaining);
        Assert.AreEqual(0, store.ReadEvents(oldest.JobId).Count);
        Assert.AreNotEqual(0, store.ReadEvents(newest.JobId).Count);
    }

    [TestMethod]
    public void QueueStateIsPersistedAndReadable()
    {
        var store = new JobStore(JobQueueTestHarness.CreateStoreDirectory());
        Assert.AreEqual(JobQueueStates.Idle, store.ReadSnapshot().QueueState);

        store.SetQueueState(JobQueueStates.WaitingProjectLock);

        Assert.AreEqual(JobQueueStates.WaitingProjectLock, store.ReadSnapshot().QueueState);
    }

    [TestMethod]
    public void EventsNeverContainThePayload()
    {
        var directory = JobQueueTestHarness.CreateStoreDirectory();
        var store = new JobStore(directory);
        const string sensitivePayload = "SENSITIVE-PROMPT-TEXT with D:/fake/project path";
        var job = store.Enqueue(JobQueueTestHarness.Request(payload: sensitivePayload));
        store.TryClaim(job.JobId);
        store.ReportProgress(job.JobId, 500);
        store.AppendLog(job.JobId, VFXComposer.Protocol.Jobs.JobLogLevels.Info, JobQueueDiagnosticCodes.WaitingProjectLock);
        store.AppendArtifact(job.JobId, "artifact-1");
        store.Complete(job.JobId, JobStatusStates.Succeeded, null);

        var eventLog = File.ReadAllText(Path.Combine(directory, "job-events.jsonl"));

        Assert.IsFalse(eventLog.Contains("SENSITIVE-PROMPT-TEXT", StringComparison.Ordinal));
        Assert.IsFalse(eventLog.Contains("fake/project", StringComparison.Ordinal));
    }
}

/// <summary>Builds a syntactically valid but misplaced event line for the fail-closed test.</summary>
internal static class JobStoreCodecProbe
{
    public static string SerializeStatusLine(JobRecord job) => "{\"schema\":\"vfxcomposer.job-event/9\",\"jobId\":\"" +
        job.JobId + "\",\"eventSequence\":9,\"kind\":\"STATUS\",\"occurredAtUtc\":\"2026-08-29T00:00:00+00:00\",\"state\":\"QUEUED\"}";
}

using Microsoft.VisualStudio.TestTools.UnitTesting;
using VFXComposer.Protocol.Jobs;

namespace VFXComposer.Jobs.Tests;

/// <summary>
/// Persistence of the batch entry <c>itemId</c> (REQ-003 §9.1) and the store schema version
/// scheme it introduced: snapshots are written as <c>vfxcomposer.job-store/2</c> and any older
/// snapshot fails closed on the version gate instead of being read as "no item".
/// </summary>
[TestClass]
public sealed class JobStoreItemIdTests
{
    private const string LegacySnapshotSchema = "vfxcomposer.job-store/1";

    [TestMethod]
    public void ItemIdIsPersistedAndReadBackByAnotherStoreInstance()
    {
        var directory = JobQueueTestHarness.CreateStoreDirectory();
        var store = new JobStore(directory);

        var job = store.Enqueue(JobQueueTestHarness.Request(
            batchId: "batch-alpha",
            batchPolicy: JobBatchPolicies.Continue,
            itemId: "fire_slash-01"));

        Assert.AreEqual("fire_slash-01", job.ItemId);
        var reopened = new JobStore(directory).ReadSnapshot().Jobs.Single();
        Assert.AreEqual("fire_slash-01", reopened.ItemId);
        Assert.AreEqual("batch-alpha", reopened.BatchId);
        var snapshotText = File.ReadAllText(Path.Combine(directory, "job-store.json"));
        Assert.IsTrue(
            snapshotText.Contains(JobStoreSnapshot.CurrentSchema, StringComparison.Ordinal),
            "The snapshot must declare the current store schema.");
        Assert.IsTrue(snapshotText.Contains("\"itemId\": \"fire_slash-01\"", StringComparison.Ordinal));
    }

    [TestMethod]
    public void SubmissionWithoutABatchEntryPersistsNoItemId()
    {
        var directory = JobQueueTestHarness.CreateStoreDirectory();
        var store = new JobStore(directory);

        var job = store.Enqueue(JobQueueTestHarness.Request(itemId: null));

        Assert.IsNull(job.ItemId);
        Assert.IsNull(new JobStore(directory).ReadSnapshot().Jobs.Single().ItemId);
        Assert.IsTrue(
            File.ReadAllText(Path.Combine(directory, "job-store.json"))
                .Contains("\"itemId\": null", StringComparison.Ordinal),
            "Absence must be written as an explicit null, not omitted.");
    }

    [TestMethod]
    public void ItemIdSurvivesEveryStateTransitionOfTheJob()
    {
        var directory = JobQueueTestHarness.CreateStoreDirectory();
        var store = new JobStore(directory);
        var job = store.Enqueue(JobQueueTestHarness.Request(
            batchId: "batch-beta", batchPolicy: JobBatchPolicies.Continue, itemId: "item-transitions"));

        Assert.AreEqual("item-transitions", store.TryClaim(job.JobId)!.ItemId);
        Assert.AreEqual("item-transitions", store.ReportProgress(job.JobId, 500).ItemId);
        Assert.AreEqual("item-transitions", store.AppendArtifact(job.JobId, "artifact-1").ItemId);
        Assert.AreEqual("item-transitions", store.RegisterChildProcess(job.JobId, 4321, DateTimeOffset.UtcNow).ItemId);
        Assert.AreEqual("item-transitions", store.ClearChildProcess(job.JobId).ItemId);
        Assert.AreEqual(
            "item-transitions",
            store.Complete(job.JobId, JobStatusStates.Succeeded, null).ItemId);
    }

    [TestMethod]
    public void ItemIdSurvivesCrashRecoveryForQueuedAndRunningJobs()
    {
        var directory = JobQueueTestHarness.CreateStoreDirectory();
        var crashed = new JobStore(directory);
        var running = crashed.Enqueue(JobQueueTestHarness.Request(
            payload: "one", batchId: "batch-gamma", batchPolicy: JobBatchPolicies.Continue, itemId: "item-running"));
        var queued = crashed.Enqueue(JobQueueTestHarness.Request(
            payload: "two", batchId: "batch-gamma", batchPolicy: JobBatchPolicies.Continue, itemId: "item-queued"));
        crashed.TryClaim(running.JobId);
        crashed.RegisterChildProcess(running.JobId, 4321, new DateTimeOffset(2026, 8, 29, 3, 0, 0, TimeSpan.Zero));

        // A fresh instance over the same directory is the crash-restart path (REQ-003 §7.2).
        var restarted = new JobStore(directory);
        var recovery = restarted.RecoverOnStartup();

        CollectionAssert.AreEqual(new[] { running.JobId }, recovery.RecoveredJobIds.ToArray());
        var recovered = restarted.ReadSnapshot().Jobs;
        var settled = recovered.Single(job => job.JobId == running.JobId);
        Assert.AreEqual(JobStatusStates.Disconnected, settled.State);
        Assert.AreEqual("item-running", settled.ItemId, "Recovery must not drop the item id.");
        var preserved = recovered.Single(job => job.JobId == queued.JobId);
        Assert.AreEqual(JobStatusStates.Queued, preserved.State);
        Assert.AreEqual("item-queued", preserved.ItemId);
    }

    [TestMethod]
    public void ItemIdIsPreservedAcrossResubmission()
    {
        var store = new JobStore(JobQueueTestHarness.CreateStoreDirectory());
        var original = store.Enqueue(JobQueueTestHarness.Request(
            payload: "stable-content",
            batchId: "batch-delta",
            batchPolicy: JobBatchPolicies.Abort,
            itemId: "item-resubmitted"));
        store.TryClaim(original.JobId);
        store.Complete(original.JobId, JobStatusStates.Failed, JobQueueDiagnosticCodes.ExecutionFailed);

        var resubmitted = store.Resubmit(original.JobId);

        Assert.AreNotEqual(original.JobId, resubmitted.JobId);
        Assert.AreEqual("item-resubmitted", resubmitted.ItemId);
        Assert.AreEqual(
            original.EntryIdempotencyKey,
            resubmitted.EntryIdempotencyKey,
            "The item id and the content-derived entry key must stay consistent across resubmission.");
        Assert.AreEqual("item-resubmitted", JobQueueTestHarness.GetJob(store, resubmitted.JobId).ItemId);
    }

    [TestMethod]
    public void LegacySchemaSnapshotFailsClosedWithTheUnsupportedVersionCodeAndIsLeftUntouched()
    {
        var directory = JobQueueTestHarness.CreateStoreDirectory();
        var snapshotPath = Path.Combine(directory, "job-store.json");
        var legacy = LegacySnapshot();
        File.WriteAllText(snapshotPath, legacy);
        var store = new JobStore(directory);

        Assert.AreEqual(
            JobQueueDiagnosticCodes.StoreVersionUnsupported,
            Assert.ThrowsExactly<JobQueueException>(() => store.ReadSnapshot()).Code,
            "A pre-itemId snapshot must be rejected, not read as a job without an item id.");
        Assert.AreEqual(
            JobQueueDiagnosticCodes.StoreVersionUnsupported,
            Assert.ThrowsExactly<JobQueueException>(
                () => store.Enqueue(JobQueueTestHarness.Request())).Code,
            "Writing must fail closed too, so the legacy file is never partially migrated.");
        Assert.AreEqual(legacy, File.ReadAllText(snapshotPath), "The legacy file must not be rewritten.");
        Assert.IsFalse(
            File.Exists(snapshotPath + ".bak"),
            "An unsupported version is not corruption: backup recovery must not be attempted.");
    }

    [TestMethod]
    public void CurrentSchemaSnapshotWithoutTheItemIdMemberReadsAsNoItem()
    {
        var directory = JobQueueTestHarness.CreateStoreDirectory();
        File.WriteAllText(
            Path.Combine(directory, "job-store.json"),
            LegacySnapshot().Replace(LegacySnapshotSchema, JobStoreSnapshot.CurrentSchema, StringComparison.Ordinal));

        var job = new JobStore(directory).ReadSnapshot().Jobs.Single();

        Assert.IsNull(job.ItemId, "Within the current schema an absent member is defined as 'no item'.");
    }

    [TestMethod]
    public void IllegalPersistedItemIdIsRejectedFailClosedWithoutEchoingTheToken()
    {
        const string illegal = "item with spaces";
        var directory = JobQueueTestHarness.CreateStoreDirectory();
        File.WriteAllText(
            Path.Combine(directory, "job-store.json"),
            LegacySnapshot()
                .Replace(LegacySnapshotSchema, JobStoreSnapshot.CurrentSchema, StringComparison.Ordinal)
                .Replace("\"batchId\":null", "\"itemId\":\"" + illegal + "\",\"batchId\":null", StringComparison.Ordinal));

        var exception = Assert.ThrowsExactly<JobQueueException>(
            () => new JobStore(directory).ReadSnapshot());

        Assert.AreEqual(JobQueueDiagnosticCodes.StoreUnavailable, exception.Code);
        Assert.IsFalse(
            exception.Message.Contains(illegal, StringComparison.Ordinal),
            "The stable code carries the failure; the token must not reach the message.");
    }

    [TestMethod]
    public void RejectedItemIdTokensNeverAppearInTheFailureMessage()
    {
        const string illegal = "Item/With/Slashes";

        var exception = Assert.ThrowsExactly<ArgumentException>(
            () => JobQueueTestHarness.Request(itemId: illegal));

        Assert.IsFalse(exception.Message.Contains(illegal, StringComparison.Ordinal));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => JobQueueTestHarness.Request(itemId: new string('i', 129)));
    }

    /// <summary>
    /// A snapshot in the pre-itemId shape: exactly the members version 1 wrote, so the version
    /// gate — not a missing-member error — decides its fate.
    /// </summary>
    private static string LegacySnapshot() =>
        "{\"schema\":\"" + LegacySnapshotSchema + "\",\"queueState\":\"IDLE\",\"nextQueuePosition\":2,\"jobs\":[" +
        "{\"jobId\":\"job-legacy0001\",\"requestId\":\"req-legacy0001\",\"idempotencyKey\":\"idk-legacy0001\"," +
        "\"entryIdempotencyKey\":\"sha256:00\",\"batchId\":null,\"batchPolicy\":null,\"sourceEntry\":\"CLI\"," +
        "\"jobKind\":\"test.job\",\"payload\":\"legacy-payload\",\"queuePosition\":1," +
        "\"enqueuedAtUtc\":\"2026-08-29T03:00:00+00:00\",\"startedAtUtc\":null,\"completedAtUtc\":null," +
        "\"state\":\"QUEUED\",\"cancelRequested\":false,\"lastEventSequence\":1,\"lastProgressPermille\":0," +
        "\"finalDiagnosticCode\":null,\"artifactIds\":[],\"childProcessId\":null,\"childProcessStartUtc\":null}]}";
}

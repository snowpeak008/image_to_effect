using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace VFXComposer.Jobs.Tests;

[TestClass]
public sealed class JobEntryIdempotencyTests
{
    [TestMethod]
    public void DerivationIsDeterministicAndContentSensitive()
    {
        var key = JobEntryIdempotency.Derive("batch-1", "item-1", "content");

        Assert.AreEqual(key, JobEntryIdempotency.Derive("batch-1", "item-1", "content"));
        Assert.AreNotEqual(key, JobEntryIdempotency.Derive("batch-1", "item-1", "content-b"));
        Assert.AreNotEqual(key, JobEntryIdempotency.Derive("batch-1", "item-2", "content"));
        Assert.AreNotEqual(key, JobEntryIdempotency.Derive("batch-2", "item-1", "content"));
        Assert.IsTrue(key.StartsWith("sha256:", StringComparison.Ordinal));
        Assert.AreEqual("sha256:".Length + 64, key.Length);
    }

    [TestMethod]
    public void KeySurvivesResubmissionWhileQueueTokensRotate()
    {
        var store = new JobStore(JobQueueTestHarness.CreateStoreDirectory());
        var original = store.Enqueue(new JobEnqueueRequest(
            JobSourceEntries.Cli,
            "test.job",
            "list-entry-content",
            batchId: "batch-9",
            batchPolicy: JobBatchPolicies.Continue,
            itemId: "item-9"));
        store.TryClaim(original.JobId);
        store.Complete(original.JobId, Protocol.Jobs.JobStatusStates.Disconnected, JobQueueDiagnosticCodes.DisconnectedRecovery);

        var second = store.Resubmit(original.JobId);
        store.TryClaim(second.JobId);
        store.Complete(second.JobId, Protocol.Jobs.JobStatusStates.Failed, JobQueueDiagnosticCodes.ExecutionFailed);
        var third = store.Resubmit(second.JobId);

        Assert.AreEqual(original.EntryIdempotencyKey, second.EntryIdempotencyKey);
        Assert.AreEqual(original.EntryIdempotencyKey, third.EntryIdempotencyKey);
        var tokens = new[]
        {
            original.JobId, original.RequestId, original.IdempotencyKey,
            second.JobId, second.RequestId, second.IdempotencyKey,
            third.JobId, third.RequestId, third.IdempotencyKey,
        };
        Assert.AreEqual(tokens.Length, tokens.Distinct(StringComparer.Ordinal).Count());
    }
}

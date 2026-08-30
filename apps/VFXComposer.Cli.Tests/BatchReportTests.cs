using Microsoft.VisualStudio.TestTools.UnitTesting;
using VFXComposer.Batch.Core;
using VFXComposer.Jobs;
using VFXComposer.Protocol.Jobs;

namespace VFXComposer.Cli.Tests;

[TestClass]
public sealed class BatchReportTests
{
    [TestMethod]
    public async Task ReportCountsEveryDispositionAndRoundTrips()
    {
        var store = new JobStore(CliTestHarness.CreateDirectory());
        var manifest = BatchSubmissionAndTrackingTests.Manifest("alpha", "beta", "gamma");
        var service = new BatchSubmissionService(store, JobSourceEntries.Cli);
        service.Submit(manifest, force: false);
        await CliTestHarness.DrainAsync(store, Executor(), expectedTerminalJobs: 3);
        var second = service.Submit(manifest, force: false);
        await CliTestHarness.DrainAsync(store, Executor(), expectedTerminalJobs: 4);
        var tracked = await new BatchTracker(store, CliTestHarness.FastTracking)
            .TrackAsync(second.Items, new RecordingTrackingSink(), CancellationToken.None);

        var report = BatchReportBuilder.Create(manifest, second, tracked.JobsByItemId, DateTimeOffset.UtcNow);

        Assert.AreEqual(BatchReport.CurrentSchema, report.SchemaVersion);
        Assert.AreEqual(3, report.Summary.Total);
        Assert.AreEqual(2, report.Summary.SkippedIdempotent, "alpha and gamma already succeeded.");
        Assert.AreEqual(1, report.Summary.Failed);
        Assert.AreEqual(0, report.Summary.Pending);
        var beta = report.Items.Single(item => item.ItemId == "beta");
        Assert.AreEqual(JobCompletionOutcomes.Failed, beta.Outcome);
        Assert.AreEqual(JobQueueDiagnosticCodes.GenerationValidationExhausted, beta.Diagnostic);
        var alpha = report.Items.Single(item => item.ItemId == "alpha");
        Assert.AreEqual(BatchItemDispositions.SkippedIdempotent, alpha.Outcome);
        Assert.IsNull(alpha.JobId);

        var round = BatchReportBuilder.Deserialize(BatchReportBuilder.Serialize(report));
        Assert.AreEqual(report.BatchId, round.BatchId);
        Assert.AreEqual(report.Items.Count, round.Items.Count);
        Assert.AreEqual(report.Summary.Failed, round.Summary.Failed);
    }

    [TestMethod]
    public async Task SuccessfulEntriesCarryTheirArtifactCountAndNoDiagnostic()
    {
        var store = new JobStore(CliTestHarness.CreateDirectory());
        var manifest = BatchSubmissionAndTrackingTests.Manifest("alpha");
        var submission = new BatchSubmissionService(store, JobSourceEntries.Cli).Submit(manifest, force: false);
        await CliTestHarness.DrainAsync(store, Executor(), expectedTerminalJobs: 1);
        var tracked = await new BatchTracker(store, CliTestHarness.FastTracking)
            .TrackAsync(submission.Items, new RecordingTrackingSink(), CancellationToken.None);

        var report = BatchReportBuilder.Create(manifest, submission, tracked.JobsByItemId, DateTimeOffset.UtcNow);

        var alpha = report.Items.Single();
        Assert.AreEqual(JobCompletionOutcomes.Succeeded, alpha.Outcome);
        Assert.IsNull(alpha.Diagnostic);
        Assert.AreEqual(2, alpha.ArtifactCount, "The draft identity and the canonical hash are the two artifacts.");
        Assert.IsFalse(
            BatchReportBuilder.Serialize(report).Contains("prompt for alpha", StringComparison.Ordinal),
            "The report must never contain prompt content.");
    }

    [TestMethod]
    public void PendingEntriesKeepTheirStateAndHaveNoOutcome()
    {
        var store = new JobStore(CliTestHarness.CreateDirectory());
        var manifest = BatchSubmissionAndTrackingTests.Manifest("alpha");
        var submission = new BatchSubmissionService(store, JobSourceEntries.Cli).Submit(manifest, force: false);

        var report = BatchReportBuilder.Create(
            manifest,
            submission,
            new Dictionary<string, JobRecord>(StringComparer.Ordinal),
            DateTimeOffset.UtcNow);

        Assert.AreEqual(1, report.Summary.Pending);
        Assert.AreEqual(JobStatusStates.Queued, report.Items[0].State);
        Assert.IsNull(report.Items[0].Outcome);
        Assert.AreEqual(BatchVerdict.Pending, BatchReportBuilder.Evaluate(report, BatchFailurePolicies.Continue));
    }

    [TestMethod]
    public void VerdictFollowsTheFailurePolicy()
    {
        var allGood = Report(succeeded: 2, failed: 0, cancelled: 0);
        var oneFailed = Report(succeeded: 1, failed: 1, cancelled: 0);
        var abortShape = Report(succeeded: 1, failed: 1, cancelled: 1);
        var cancelledOnly = Report(succeeded: 1, failed: 0, cancelled: 1);

        Assert.AreEqual(BatchVerdict.AllSucceeded, BatchReportBuilder.Evaluate(allGood, BatchFailurePolicies.Continue));
        Assert.AreEqual(
            BatchVerdict.CompletedWithFailures,
            BatchReportBuilder.Evaluate(oneFailed, BatchFailurePolicies.Continue));
        Assert.AreEqual(BatchVerdict.Aborted, BatchReportBuilder.Evaluate(abortShape, BatchFailurePolicies.Abort));
        Assert.AreEqual(
            BatchVerdict.CompletedWithFailures,
            BatchReportBuilder.Evaluate(cancelledOnly, BatchFailurePolicies.Abort),
            "Without a failed entry an abort batch is just a batch that lost an entry to cancellation.");
    }

    private static RecipeGenerationJobExecutor Executor() =>
        new(
            () => CliTestHarness.Channel(static description => description.Contains("beta", StringComparison.Ordinal)),
            () => new InMemoryRecipeDraftStore());

    private static BatchReport Report(int succeeded, int failed, int cancelled) =>
        new(
            BatchReport.CurrentSchema,
            "batch-a",
            BatchFailurePolicies.Continue,
            DateTimeOffset.UtcNow,
            Array.Empty<BatchReportItem>(),
            new BatchReportSummary(
                succeeded + failed + cancelled,
                succeeded,
                failed,
                cancelled,
                disconnected: 0,
                skippedIdempotent: 0,
                pending: 0));
}

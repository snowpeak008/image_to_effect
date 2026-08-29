using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VFXComposer.Batch.Core;
using VFXComposer.Cli;
using VFXComposer.Jobs;
using VFXComposer.Protocol.Jobs;

namespace VFXComposer.Cli.Tests;

[TestClass]
public sealed class CliRunnerTests
{
    private const string FailingPrompt = "POISON";

    [TestMethod]
    public async Task ValidateAcceptsAGoodManifest()
    {
        var fixture = new CliFixture();
        var manifest = fixture.WriteManifest(CliTestHarness.ThreePromptManifest());

        var exit = await fixture.RunAsync(["batch", "validate", manifest]);

        Assert.AreEqual(CliExitCodes.Success, exit, fixture.Output);
        StringAssert.Contains(fixture.Output, "manifest fire-pack accepted");
        Assert.AreEqual(0, fixture.Store.ReadSnapshot().Jobs.Count, "Validation never enqueues.");
    }

    [TestMethod]
    public async Task ValidateRefusesAnEscapingRecipePathAndEnqueuesNothing()
    {
        var fixture = new CliFixture();
        var manifest = fixture.WriteManifest(
            """
            {
              "schemaVersion": "vfxcomposer.batch-manifest/1",
              "batchId": "escape-test",
              "items": [ { "itemId": "a", "kind": "recipe", "recipePath": "..\\..\\ProjectSettings\\x.json" } ]
            }
            """);

        var exit = await fixture.RunAsync(["batch", "validate", manifest]);

        Assert.AreEqual(CliExitCodes.DataError, exit, fixture.Output);
        StringAssert.Contains(fixture.Output, BatchDiagnosticCodes.UnsafeRecipePath);
        StringAssert.Contains(fixture.Output, "$.items[0].recipePath");
        Assert.AreEqual(0, fixture.Store.ReadSnapshot().Jobs.Count);
    }

    [TestMethod]
    public async Task RunCompletesEveryEntryAndWritesTheReport()
    {
        var fixture = new CliFixture(shouldFail: static _ => false);
        var manifest = fixture.WriteManifest(CliTestHarness.ThreePromptManifest());

        var exit = await fixture.RunAsync(["batch", "run", manifest]);

        Assert.AreEqual(CliExitCodes.Success, exit, fixture.Output + fixture.Errors);
        var jobs = fixture.Store.ReadSnapshot().Jobs;
        Assert.AreEqual(3, jobs.Count);
        Assert.IsTrue(jobs.All(job => job.State == JobStatusStates.Succeeded));
        var report = fixture.ReadReport(manifest);
        Assert.AreEqual(3, report.Summary.Succeeded);
        Assert.AreEqual(0, report.Summary.Pending);
        CollectionAssert.AreEqual(
            new[] { "alpha", "beta", "gamma" },
            report.Items.Select(static item => item.ItemId).ToArray());
    }

    [TestMethod]
    public async Task ASingleFailureDoesNotStopTheBatchUnderTheContinuePolicy()
    {
        var fixture = new CliFixture();
        var manifest = fixture.WriteManifest(CliTestHarness.ThreePromptManifest());

        var exit = await fixture.RunAsync(["batch", "run", manifest]);

        Assert.AreEqual(CliExitCodes.BatchCompletedWithFailures, exit, fixture.Output + fixture.Errors);
        var report = fixture.ReadReport(manifest);
        Assert.AreEqual(2, report.Summary.Succeeded);
        Assert.AreEqual(1, report.Summary.Failed);
        Assert.AreEqual(
            JobQueueDiagnosticCodes.ExecutionFailed,
            report.Items.Single(item => item.ItemId == "beta").Diagnostic);
        Assert.AreEqual(
            JobCompletionOutcomes.Succeeded,
            report.Items.Single(item => item.ItemId == "gamma").Outcome,
            "The entry after the failure still ran.");
    }

    [TestMethod]
    public async Task TheAbortPolicyCancelsTheRemainderWithoutRunningIt()
    {
        var fixture = new CliFixture();
        var manifest = fixture.WriteManifest(CliTestHarness.ThreePromptManifest());

        var exit = await fixture.RunAsync(["batch", "run", manifest, "--on-failure", "abort"]);

        Assert.AreEqual(CliExitCodes.BatchAborted, exit, fixture.Output + fixture.Errors);
        var report = fixture.ReadReport(manifest);
        Assert.AreEqual(BatchFailurePolicies.Abort, report.OnFailure);
        var gamma = report.Items.Single(item => item.ItemId == "gamma");
        Assert.AreEqual(JobCompletionOutcomes.Cancelled, gamma.Outcome);
        Assert.AreEqual(JobQueueDiagnosticCodes.BatchAborted, gamma.Diagnostic);
        var gammaJob = fixture.Store.ReadSnapshot().Jobs.Single(job => job.JobId == gamma.JobId);
        Assert.IsNull(gammaJob.StartedAtUtc, "A batch-aborted entry must never have entered execution.");
    }

    [TestMethod]
    public async Task ResumeSkipsAlreadyCompletedEntriesAndRerunsTheRest()
    {
        var fixture = new CliFixture();
        var manifest = fixture.WriteManifest(CliTestHarness.ThreePromptManifest());
        var firstExit = await fixture.RunAsync(["batch", "run", manifest]);
        fixture.Reset(shouldFail: static _ => false);

        var secondExit = await fixture.RunAsync(["batch", "run", manifest, "--resume"]);

        Assert.AreEqual(CliExitCodes.BatchCompletedWithFailures, firstExit);
        Assert.AreEqual(CliExitCodes.Success, secondExit, fixture.Output + fixture.Errors);
        StringAssert.Contains(fixture.Output, "[alpha] " + BatchItemDispositions.SkippedIdempotent);
        var report = fixture.ReadReport(manifest);
        Assert.AreEqual(2, report.Summary.SkippedIdempotent);
        Assert.AreEqual(1, report.Summary.Succeeded);
        Assert.AreEqual(
            BatchItemDispositions.SkippedIdempotent,
            report.Items.Single(item => item.ItemId == "gamma").Outcome);
        Assert.AreEqual(4, fixture.Store.ReadSnapshot().Jobs.Count, "Only the failed entry was re-enqueued.");
    }

    [TestMethod]
    public async Task ForceReEnqueuesEveryEntry()
    {
        var fixture = new CliFixture(shouldFail: static _ => false);
        var manifest = fixture.WriteManifest(CliTestHarness.ThreePromptManifest());
        await fixture.RunAsync(["batch", "run", manifest]);
        fixture.Reset(shouldFail: static _ => false);

        var exit = await fixture.RunAsync(["batch", "run", manifest, "--force"]);

        Assert.AreEqual(CliExitCodes.Success, exit, fixture.Output + fixture.Errors);
        Assert.AreEqual(6, fixture.Store.ReadSnapshot().Jobs.Count);
    }

    [TestMethod]
    public async Task DetachOnlyEnqueues()
    {
        var fixture = new CliFixture();
        var manifest = fixture.WriteManifest(CliTestHarness.ThreePromptManifest());

        var exit = await fixture.RunAsync(["batch", "run", manifest, "--detach"]);

        Assert.AreEqual(CliExitCodes.Success, exit, fixture.Output + fixture.Errors);
        var jobs = fixture.Store.ReadSnapshot().Jobs;
        Assert.AreEqual(3, jobs.Count);
        Assert.IsTrue(jobs.All(job => job.State == JobStatusStates.Queued));
        Assert.AreEqual(0, fixture.Channel.Descriptions.Count, "Detaching must not execute anything in this process.");
        StringAssert.Contains(fixture.Output, CliNoticeCodes.BatchDetached);
        Assert.AreEqual(3, fixture.ReadReport(manifest).Summary.Pending);
    }

    [TestMethod]
    public async Task DryRunPrintsThePlanAndEnqueuesNothing()
    {
        var fixture = new CliFixture();
        var manifest = fixture.WriteManifest(CliTestHarness.ThreePromptManifest());

        var exit = await fixture.RunAsync(["batch", "run", manifest, "--dry-run"]);

        Assert.AreEqual(CliExitCodes.Success, exit, fixture.Output + fixture.Errors);
        Assert.AreEqual(0, fixture.Store.ReadSnapshot().Jobs.Count);
        StringAssert.Contains(fixture.Output, "[alpha] PLANNED " + BatchItemDispositions.Enqueued);
        StringAssert.Contains(fixture.Output, CliNoticeCodes.DryRunPlanOnly);
        Assert.IsFalse(File.Exists(manifest + ".report.json"), "A dry run writes no report.");
    }

    [TestMethod]
    public async Task UnknownOptionsAndVerbsExitWithTheUsageCode()
    {
        var fixture = new CliFixture();
        var manifest = fixture.WriteManifest(CliTestHarness.ThreePromptManifest());

        Assert.AreEqual(CliExitCodes.UsageError, await fixture.RunAsync(["batch", "run", manifest, "--approve"]));
        Assert.AreEqual(CliExitCodes.UsageError, await fixture.RunAsync(["batch", "explode", manifest]));
        Assert.AreEqual(CliExitCodes.Success, await fixture.RunAsync(["--help"]));
    }

    [TestMethod]
    public async Task AnUnreadableManifestIsADataError()
    {
        var fixture = new CliFixture();

        var exit = await fixture.RunAsync(["batch", "run", Path.Combine(fixture.WorkspaceDirectory, "absent.json")]);

        Assert.AreEqual(CliExitCodes.DataError, exit);
        StringAssert.Contains(fixture.Errors, BatchDiagnosticCodes.ManifestUnreadable);
        Assert.IsFalse(fixture.Errors.Contains("absent.json", StringComparison.Ordinal), "Paths stay out of output.");
    }

    [TestMethod]
    public async Task AnUnavailableQueueStopsTheRunBeforeAnythingIsEnqueued()
    {
        var fixture = new CliFixture { QueueClientOverride = new UnavailableQueueClient() };
        var manifest = fixture.WriteManifest(CliTestHarness.ThreePromptManifest());

        var runExit = await fixture.RunAsync(["batch", "run", manifest]);
        var listExit = await fixture.RunAsync(["queue", "list"]);

        Assert.AreEqual(CliExitCodes.QueueUnavailable, runExit);
        Assert.AreEqual(CliExitCodes.QueueUnavailable, listExit);
        StringAssert.Contains(fixture.Errors, CliNoticeCodes.QueueUnavailable);
    }

    [TestMethod]
    public async Task AProjectLockWaitBeyondTheBoundExitsSeventyThree()
    {
        var fixture = new CliFixture { WrapQueueAsProjectLockWaiting = true, AllowExecutor = false };
        var manifest = fixture.WriteManifest(CliTestHarness.ThreePromptManifest());

        var exit = await fixture.RunAsync(["batch", "run", manifest, "--lock-timeout", "0"]);

        Assert.AreEqual(CliExitCodes.ProjectLockTimeout, exit, fixture.Output + fixture.Errors);
        StringAssert.Contains(fixture.Output, CliNoticeCodes.WaitingProjectLock);
        StringAssert.Contains(fixture.Errors, CliNoticeCodes.ProjectLockTimeout);
    }

    [TestMethod]
    public async Task AnInterruptedForegroundRunExitsOneHundredThirty()
    {
        var fixture = new CliFixture { AllowExecutor = false };
        var manifest = fixture.WriteManifest(CliTestHarness.ThreePromptManifest());
        using var interrupted = new CancellationTokenSource();
        await interrupted.CancelAsync();

        var exit = await fixture.RunAsync(["batch", "run", manifest], interrupted.Token);

        Assert.AreEqual(CliExitCodes.Interrupted, exit, fixture.Output + fixture.Errors);
        Assert.IsTrue(
            fixture.Store.ReadSnapshot().Jobs.All(job => job.State == JobStatusStates.Queued),
            "An interrupted run leaves its entries queued, exactly like a detached run.");
        StringAssert.Contains(fixture.Errors, CliNoticeCodes.Interrupted);
    }

    [TestMethod]
    public async Task QueryCommandsNeverOpenTheGenerationRuntime()
    {
        var fixture = new CliFixture(shouldFail: static _ => false);
        var manifest = fixture.WriteManifest(CliTestHarness.ThreePromptManifest());
        await fixture.RunAsync(["batch", "run", manifest]);
        fixture.Reset(shouldFail: static _ => false);
        fixture.ForbidGenerationRuntime = true;
        var jobId = fixture.Store.ReadSnapshot().Jobs[0].JobId;

        Assert.AreEqual(CliExitCodes.Success, await fixture.RunAsync(["queue", "list"]));
        Assert.AreEqual(CliExitCodes.Success, await fixture.RunAsync(["job", "status", jobId]));
        Assert.AreEqual(CliExitCodes.Success, await fixture.RunAsync(["batch", "status", "fire-pack"]));
        Assert.AreEqual(CliExitCodes.Success, await fixture.RunAsync(["job", "cancel", jobId]));
    }

    [TestMethod]
    public async Task LookupsFailClosedOnUnknownIdentifiers()
    {
        var fixture = new CliFixture();

        Assert.AreEqual(CliExitCodes.DataError, await fixture.RunAsync(["job", "status", "job-missing"]));
        Assert.AreEqual(CliExitCodes.DataError, await fixture.RunAsync(["batch", "status", "batch-missing"]));
        Assert.AreEqual(CliExitCodes.DataError, await fixture.RunAsync(["job", "cancel", "job-missing"]));
        StringAssert.Contains(fixture.Errors, CliNoticeCodes.NotFound);
    }

    [TestMethod]
    public async Task CancellingAQueuedEntrySettlesItImmediately()
    {
        var fixture = new CliFixture { AllowExecutor = false };
        var manifest = fixture.WriteManifest(CliTestHarness.ThreePromptManifest());
        await fixture.RunAsync(["batch", "run", manifest, "--detach"]);
        var jobId = fixture.Store.ReadSnapshot().Jobs[0].JobId;

        var exit = await fixture.RunAsync(["job", "cancel", jobId]);

        Assert.AreEqual(CliExitCodes.Success, exit);
        var job = fixture.Store.ReadSnapshot().Jobs.Single(record => record.JobId == jobId);
        Assert.AreEqual(JobStatusStates.Cancelled, job.State);
        Assert.AreEqual(JobQueueDiagnosticCodes.CancelledQueued, job.FinalDiagnosticCode);
    }

    [TestMethod]
    public async Task TheRunProcessWritesNothingIntoTheUnityProjectDirectory()
    {
        var fixture = new CliFixture(shouldFail: static _ => false);
        var manifest = fixture.WriteManifest(CliTestHarness.ThreePromptManifest());
        var projectDirectory = Path.Combine(fixture.WorkspaceDirectory, "project", "Assets", "VFX", "Generated");
        Directory.CreateDirectory(projectDirectory);
        var asset = Path.Combine(projectDirectory, "existing.prefab");
        File.WriteAllText(asset, "unchanged");
        var before = SnapshotTree(Path.Combine(fixture.WorkspaceDirectory, "project"));

        await fixture.RunAsync(["batch", "run", manifest]);

        CollectionAssert.AreEqual(
            before,
            SnapshotTree(Path.Combine(fixture.WorkspaceDirectory, "project")),
            "The entry surface must not touch the Unity project.");
    }

    [TestMethod]
    public async Task JsonOutputUsesTheProtocolFieldNames()
    {
        var fixture = new CliFixture(shouldFail: static _ => false);
        var manifest = fixture.WriteManifest(CliTestHarness.ThreePromptManifest());

        var exit = await fixture.RunAsync(["batch", "run", manifest, "--json"]);

        Assert.AreEqual(CliExitCodes.Success, exit, fixture.Output + fixture.Errors);
        var events = fixture.Output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => JsonDocument.Parse(line).RootElement)
            .ToArray();
        var updates = events.Where(e => e.GetProperty("kind").GetString() == "jobUpdated").ToArray();
        Assert.IsTrue(updates.Length >= 3);
        foreach (var update in updates)
        {
            Assert.IsTrue(update.TryGetProperty("state", out _));
            Assert.IsTrue(update.TryGetProperty("progressPermille", out _));
        }

        Assert.IsTrue(updates.Any(update =>
            update.TryGetProperty("outcome", out var outcome) &&
            outcome.GetString() == JobCompletionOutcomes.Succeeded));
        var summary = events.Single(e => e.GetProperty("kind").GetString() == "batchSummary");
        Assert.AreEqual(BatchReport.CurrentSchema, summary.GetProperty("schemaVersion").GetString());
    }

    [TestMethod]
    public async Task NoOutputSurfaceEverContainsPromptText()
    {
        var fixture = new CliFixture();
        var manifest = fixture.WriteManifest(CliTestHarness.ThreePromptManifest());

        await fixture.RunAsync(["batch", "run", manifest, "--on-failure", "abort"]);

        var reportText = File.ReadAllText(manifest + ".report.json");
        foreach (var surface in new[] { fixture.Output, fixture.Errors, reportText })
        {
            Assert.IsFalse(surface.Contains("calm blue spark", StringComparison.Ordinal));
            Assert.IsFalse(surface.Contains("slow ember trail", StringComparison.Ordinal));
            Assert.IsFalse(surface.Contains(FailingPrompt, StringComparison.Ordinal));
        }
    }

    private static string[] SnapshotTree(string root) =>
        Directory.EnumerateFileSystemEntries(root, "*", SearchOption.AllDirectories)
            .OrderBy(static entry => entry, StringComparer.Ordinal)
            .Select(entry => File.Exists(entry)
                ? entry + ":" + Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(entry)))
                : entry)
            .ToArray();

    /// <summary>One CLI invocation environment over a temporary store, manifest and fake channel.</summary>
    private sealed class CliFixture
    {
        private readonly StringBuilder _output = new();
        private readonly StringBuilder _errors = new();
        private Func<string, bool> _shouldFail;

        public CliFixture(Func<string, bool>? shouldFail = null)
        {
            _shouldFail = shouldFail ?? (static description => description.Contains(FailingPrompt, StringComparison.Ordinal));
            WorkspaceDirectory = CliTestHarness.CreateDirectory();
            Store = new JobStore(Path.Combine(WorkspaceDirectory, "store"));
            Channel = CreateChannel();
            Drafts = new InMemoryRecipeDraftStore();
        }

        public string WorkspaceDirectory { get; }

        public JobStore Store { get; }

        public FakeRecipeGenerationChannel Channel { get; private set; }

        public InMemoryRecipeDraftStore Drafts { get; private set; }

        public bool AllowExecutor { get; init; } = true;

        public bool WrapQueueAsProjectLockWaiting { get; init; }

        public IJobQueueClient? QueueClientOverride { get; init; }

        public bool ForbidGenerationRuntime { get; set; }

        public string Output => _output.ToString();

        public string Errors => _errors.ToString();

        public string WriteManifest(string json) => CliTestHarness.WriteManifest(WorkspaceDirectory, json);

        public void Reset(Func<string, bool>? shouldFail = null)
        {
            _shouldFail = shouldFail ?? _shouldFail;
            Channel = CreateChannel();
            Drafts = new InMemoryRecipeDraftStore();
            _output.Clear();
            _errors.Clear();
        }

        public async Task<int> RunAsync(string[] arguments, CancellationToken cancellationToken = default)
        {
            var output = new StringWriter();
            var error = new StringWriter();
            try
            {
                return await CliRunner.RunAsync(
                    arguments,
                    new CliEnvironment
                    {
                        Output = output,
                        Error = error,
                        OpenQueue = OpenQueue,
                        OpenGenerationRuntime = OpenGenerationRuntime,
                        Tracking = CliTestHarness.FastTracking,
                    },
                    cancellationToken);
            }
            finally
            {
                _output.Append(output.ToString());
                _errors.Append(error.ToString());
            }
        }

        public BatchReport ReadReport(string manifestPath) =>
            BatchReportBuilder.Deserialize(File.ReadAllText(manifestPath + ".report.json"));

        private FakeRecipeGenerationChannel CreateChannel() => new(request => _shouldFail(request.Description)
            ? CliTestHarness.ValidationFailedResult(request.CorrelationId)
            : CliTestHarness.DraftedResult(request.CorrelationId, "fx-generated"));

        private ICliQueueSession OpenQueue()
        {
            if (QueueClientOverride is not null)
            {
                return new StubQueueSession(QueueClientOverride);
            }

            return WrapQueueAsProjectLockWaiting
                ? new StubQueueSession(new ProjectLockWaitingQueueClient(Store))
                : new TestQueueSession(Store, AllowExecutor);
        }

        private ICliGenerationRuntime OpenGenerationRuntime() => ForbidGenerationRuntime
            ? throw new InvalidOperationException("This command must not open the generation runtime.")
            : new FakeGenerationRuntime(Channel, Drafts);
    }
}

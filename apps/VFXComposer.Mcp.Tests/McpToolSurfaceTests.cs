using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VFXComposer.Batch.Core;
using VFXComposer.Jobs;
using VFXComposer.Mcp;
using VFXComposer.Protocol.Jobs;

namespace VFXComposer.Mcp.Tests;

/// <summary>
/// Every tool of the closed set over the stdio round trip, on the happy path and on its refusal
/// paths. The transport, the queue store and the capability profile are all synthetic; nothing
/// here reaches a provider, a socket or a Unity project.
/// </summary>
[TestClass]
public sealed class McpToolSurfaceTests
{
    [TestMethod]
    public void ValidateManifestAcceptsAGoodManifestWithoutEnqueueingAnything()
    {
        using var fixture = new McpFixture();

        using var session = fixture.RunInitialized(Call(2, McpToolNames.ValidateManifest, Manifest()));

        Assert.IsFalse(session.ToolIsError(2), session.RawOutput);
        using var payload = session.ToolPayload(2);
        Assert.AreEqual(McpToolNames.ValidateManifest, payload.RootElement.GetProperty("tool").GetString());
        Assert.IsTrue(payload.RootElement.GetProperty("accepted").GetBoolean());
        Assert.AreEqual("fire-pack", payload.RootElement.GetProperty("batchId").GetString());
        Assert.AreEqual(3, payload.RootElement.GetProperty("itemCount").GetInt32());
        Assert.AreEqual(0, fixture.Store.ReadSnapshot().Jobs.Count, "Validation never enqueues.");
    }

    [TestMethod]
    public void ValidateManifestRefusesAnEscapingRecipePathWithTheExactJsonPath()
    {
        using var fixture = new McpFixture();

        using var session = fixture.RunInitialized(
            Call(2, McpToolNames.ValidateManifest, McpManifests.EscapingRecipePath()));

        Assert.IsTrue(session.ToolIsError(2), "A rejected manifest is a tool error, not a protocol error.");
        using var payload = session.ToolPayload(2);
        Assert.AreEqual(
            McpDiagnosticCodes.ManifestRejected,
            payload.RootElement.GetProperty("error").GetProperty("code").GetString());
        var issues = payload.RootElement.GetProperty("issues").EnumerateArray().ToArray();
        Assert.IsTrue(issues.Any(issue =>
            issue.GetProperty("code").GetString() == BatchDiagnosticCodes.UnsafeRecipePath &&
            issue.GetProperty("path").GetString() == "$.items[0].recipePath"));
        Assert.AreEqual(0, fixture.Store.ReadSnapshot().Jobs.Count);
    }

    [TestMethod]
    public void ValidateManifestRefusesAManifestFieldItDoesNotKnow()
    {
        using var fixture = new McpFixture();

        using var session = fixture.RunInitialized(
            Call(2, McpToolNames.ValidateManifest, McpManifests.UnknownField()));

        Assert.IsTrue(session.ToolIsError(2));
        using var payload = session.ToolPayload(2);
        Assert.IsTrue(payload.RootElement.GetProperty("issues").EnumerateArray().Any(issue =>
            issue.GetProperty("code").GetString() == BatchDiagnosticCodes.UnknownField &&
            issue.GetProperty("path").GetString() == "$.items[0].authority"));
    }

    [TestMethod]
    public void ValidateManifestFailsTheWholeManifestWhenTheChannelIsUnbound()
    {
        using var fixture = new McpFixture { Capability = BatchCapabilityProfile.GenerationUnavailable };

        using var session = fixture.RunInitialized(Call(2, McpToolNames.ValidateManifest, Manifest()));

        Assert.IsTrue(session.ToolIsError(2));
        using var payload = session.ToolPayload(2);
        Assert.IsTrue(payload.RootElement.GetProperty("issues").EnumerateArray().Any(issue =>
            issue.GetProperty("code").GetString() == BatchDiagnosticCodes.PromptGenerationUnavailable));
    }

    [TestMethod]
    public void SubmitBatchEnqueuesEveryEntryInManifestOrderAndDetaches()
    {
        using var fixture = new McpFixture();

        using var session = fixture.RunInitialized(Call(2, McpToolNames.SubmitBatch, Manifest()));

        Assert.IsFalse(session.ToolIsError(2), session.RawOutput);
        using var payload = session.ToolPayload(2);
        Assert.AreEqual("fire-pack", payload.RootElement.GetProperty("batchId").GetString());
        Assert.AreEqual(3, payload.RootElement.GetProperty("enqueued").GetInt32());
        var items = payload.RootElement.GetProperty("items").EnumerateArray().ToArray();
        CollectionAssert.AreEqual(
            new[] { "alpha", "beta", "gamma" },
            items.Select(item => item.GetProperty("itemId").GetString()).ToArray());
        var jobs = fixture.Store.ReadSnapshot().Jobs;
        Assert.AreEqual(3, jobs.Count);
        Assert.IsTrue(jobs.All(job => job.State == JobStatusStates.Queued), "This surface never executes.");
        Assert.IsTrue(jobs.All(job => job.SourceEntry == JobSourceEntries.Mcp));
        CollectionAssert.AreEqual(
            items.Select(item => item.GetProperty("jobId").GetString()).ToArray(),
            jobs.Select(job => job.JobId).ToArray(),
            "Manifest order is queue order.");
    }

    [TestMethod]
    public void SubmitBatchAppliesTheFailurePolicyOverride()
    {
        using var fixture = new McpFixture();

        using var session = fixture.RunInitialized(
            McpFrames.ToolCall(2, McpToolNames.SubmitBatch,
                "{\"manifest\":" + McpFrames.Quote(Manifest()) + ",\"onFailure\":\"abort\"}"));

        using var payload = session.ToolPayload(2);
        Assert.AreEqual(BatchFailurePolicies.Abort, payload.RootElement.GetProperty("onFailure").GetString());
        Assert.IsTrue(fixture.Store.ReadSnapshot().Jobs.All(job => job.BatchPolicy == JobBatchPolicies.Abort));
    }

    [TestMethod]
    public async Task SubmitBatchSkipsEntriesWhoseContentAlreadySucceeded()
    {
        using var fixture = new McpFixture();
        Submit(fixture);
        await DrainAsync(fixture.Store, expectedTerminal: 3);

        using var session = fixture.RunInitialized(Call(2, McpToolNames.SubmitBatch, Manifest()));

        using var payload = session.ToolPayload(2);
        var items = payload.RootElement.GetProperty("items").EnumerateArray().ToArray();
        Assert.AreEqual(
            BatchItemDispositions.SkippedIdempotent,
            items[0].GetProperty("disposition").GetString(),
            "The entry that already succeeded is skipped.");
        Assert.IsFalse(items[0].TryGetProperty("jobId", out _), "A skipped entry has no new job.");
        Assert.AreEqual(
            BatchItemDispositions.Enqueued,
            items[1].GetProperty("disposition").GetString(),
            "The entry that failed is re-enqueued.");
        Assert.AreEqual(1, payload.RootElement.GetProperty("enqueued").GetInt32());
    }

    [TestMethod]
    public void GenerateEffectEnqueuesOneEntryUnderADerivedBatch()
    {
        using var fixture = new McpFixture();

        using var session = fixture.RunInitialized(
            McpFrames.ToolCall(2, McpToolNames.GenerateEffect,
                "{\"item\":{\"itemId\":\"solo-spark\",\"kind\":\"prompt\",\"prompt\":\"a single quiet spark\"}}"));

        Assert.IsFalse(session.ToolIsError(2), session.RawOutput);
        using var payload = session.ToolPayload(2);
        Assert.AreEqual("solo-spark", payload.RootElement.GetProperty("itemId").GetString());
        Assert.AreEqual(
            BatchItemDispositions.Enqueued,
            payload.RootElement.GetProperty("disposition").GetString());
        var batchId = payload.RootElement.GetProperty("batchId").GetString()!;
        StringAssert.StartsWith(batchId, "single-");
        Assert.AreEqual(
            BatchFailurePolicies.Continue,
            payload.RootElement.GetProperty("onFailure").GetString(),
            "The derived one-entry manifest settles under the continue policy (F5 audit ④).");
        var jobs = fixture.Store.ReadSnapshot().Jobs;
        Assert.AreEqual(1, jobs.Count);
        Assert.AreEqual(batchId, jobs[0].BatchId);
        Assert.AreEqual(JobBatchPolicies.Continue, jobs[0].BatchPolicy);
        Assert.AreEqual(payload.RootElement.GetProperty("jobId").GetString(), jobs[0].JobId);
    }

    [TestMethod]
    public void GenerateEffectRefusesAnEntryTheManifestSchemaWouldRefuse()
    {
        using var fixture = new McpFixture();

        using var session = fixture.RunInitialized(
            McpFrames.ToolCall(2, McpToolNames.GenerateEffect,
                "{\"item\":{\"itemId\":\"Solo Spark\",\"kind\":\"prompt\",\"prompt\":\"text\"}}"),
            McpFrames.ToolCall(3, McpToolNames.GenerateEffect,
                "{\"item\":{\"itemId\":\"solo\",\"kind\":\"recipe\",\"recipePath\":\"r.json\"}}"),
            McpFrames.ToolCall(4, McpToolNames.GenerateEffect,
                "{\"item\":{\"itemId\":\"solo\",\"kind\":\"prompt\"}}"));

        foreach (var id in new long[] { 2, 3, 4 })
        {
            Assert.IsTrue(session.ToolIsError(id), "Entry " + id + " must be refused.");
            using var payload = session.ToolPayload(id);
            Assert.AreEqual(
                McpDiagnosticCodes.ManifestRejected,
                payload.RootElement.GetProperty("error").GetProperty("code").GetString());
        }

        Assert.AreEqual(0, fixture.Store.ReadSnapshot().Jobs.Count);
    }

    [TestMethod]
    public void BatchStatusReportsTheBatchEntriesAndFailsClosedOnAnUnknownBatch()
    {
        using var fixture = new McpFixture();

        using var session = fixture.RunInitialized(
            Call(2, McpToolNames.SubmitBatch, Manifest()),
            McpFrames.ToolCall(3, McpToolNames.BatchStatus, "{\"batchId\":\"fire-pack\"}"),
            McpFrames.ToolCall(4, McpToolNames.BatchStatus, "{\"batchId\":\"absent-pack\"}"));

        Assert.IsFalse(session.ToolIsError(3));
        using var found = session.ToolPayload(3);
        Assert.AreEqual(JobQueueStates.Idle, found.RootElement.GetProperty("queueState").GetString());
        var jobs = found.RootElement.GetProperty("jobs").EnumerateArray().ToArray();
        Assert.AreEqual(3, jobs.Length);
        Assert.AreEqual(JobStatusStates.Queued, jobs[0].GetProperty("state").GetString());
        Assert.AreEqual(BatchJobKinds.RecipeGeneration, jobs[0].GetProperty("jobKind").GetString());
        Assert.IsTrue(session.ToolIsError(4));
        using var missing = session.ToolPayload(4);
        Assert.AreEqual(
            McpDiagnosticCodes.NotFound,
            missing.RootElement.GetProperty("error").GetProperty("code").GetString());
    }

    [TestMethod]
    public void JobStatusReportsOneEntryAndFailsClosedOnAnUnknownJob()
    {
        using var fixture = new McpFixture();
        Submit(fixture);
        var jobId = fixture.Store.ReadSnapshot().Jobs[1].JobId;

        using var session = fixture.RunInitialized(
            McpFrames.ToolCall(2, McpToolNames.JobStatus, "{\"jobId\":" + McpFrames.Quote(jobId) + "}"),
            McpFrames.ToolCall(3, McpToolNames.JobStatus, "{\"jobId\":\"job-absent\"}"));

        Assert.IsFalse(session.ToolIsError(2));
        using var payload = session.ToolPayload(2);
        var job = payload.RootElement.GetProperty("job");
        Assert.AreEqual(jobId, job.GetProperty("jobId").GetString());
        Assert.AreEqual(JobSourceEntries.Mcp, job.GetProperty("sourceEntry").GetString());
        Assert.AreEqual(0, job.GetProperty("progressPermille").GetInt32());
        Assert.IsFalse(job.TryGetProperty("outcome", out _), "A queued entry has no outcome yet.");
        Assert.IsTrue(session.ToolIsError(3));
    }

    [TestMethod]
    public void CancelJobSettlesAQueuedEntryAndFailsClosedOnAnUnknownJob()
    {
        using var fixture = new McpFixture();
        Submit(fixture);
        var jobId = fixture.Store.ReadSnapshot().Jobs[0].JobId;

        using var session = fixture.RunInitialized(
            McpFrames.ToolCall(2, McpToolNames.CancelJob, "{\"jobId\":" + McpFrames.Quote(jobId) + "}"),
            McpFrames.ToolCall(3, McpToolNames.CancelJob, "{\"jobId\":" + McpFrames.Quote(jobId) + "}"),
            McpFrames.ToolCall(4, McpToolNames.CancelJob, "{\"jobId\":\"job-absent\"}"));

        using var first = session.ToolPayload(2);
        Assert.IsTrue(first.RootElement.GetProperty("accepted").GetBoolean());
        Assert.AreEqual(JobStatusStates.Cancelled, first.RootElement.GetProperty("state").GetString());
        using var repeat = session.ToolPayload(3);
        Assert.IsFalse(repeat.RootElement.GetProperty("accepted").GetBoolean(), "A terminal entry is a no-op.");
        Assert.IsTrue(session.ToolIsError(4));
        using var missing = session.ToolPayload(4);
        Assert.AreEqual(
            JobQueueDiagnosticCodes.JobNotFound,
            missing.RootElement.GetProperty("error").GetProperty("queueDiagnostic").GetString());
        var settled = fixture.Store.ReadSnapshot().Jobs.Single(record => record.JobId == jobId);
        Assert.AreEqual(JobQueueDiagnosticCodes.CancelledQueued, settled.FinalDiagnosticCode);
    }

    [TestMethod]
    public void CancelBatchSettlesTheWholeBatchAndIsIdempotent()
    {
        using var fixture = new McpFixture();
        Submit(fixture);

        using var session = fixture.RunInitialized(
            McpFrames.ToolCall(2, McpToolNames.CancelBatch, "{\"batchId\":\"fire-pack\"}"),
            McpFrames.ToolCall(3, McpToolNames.CancelBatch, "{\"batchId\":\"fire-pack\"}"),
            McpFrames.ToolCall(4, McpToolNames.CancelBatch, "{\"batchId\":\"absent-pack\"}"));

        using var first = session.ToolPayload(2);
        Assert.AreEqual(3, first.RootElement.GetProperty("requested").GetInt32());
        Assert.AreEqual(3, first.RootElement.GetProperty("accepted").GetInt32());
        Assert.AreEqual(0, first.RootElement.GetProperty("noOp").GetInt32());
        using var repeat = session.ToolPayload(3);
        Assert.AreEqual(0, repeat.RootElement.GetProperty("accepted").GetInt32());
        Assert.AreEqual(3, repeat.RootElement.GetProperty("noOp").GetInt32());
        Assert.IsTrue(session.ToolIsError(4));
        Assert.IsTrue(fixture.Store.ReadSnapshot().Jobs.All(job =>
            job.State == JobStatusStates.Cancelled &&
            job.FinalDiagnosticCode == JobQueueDiagnosticCodes.CancelledQueued));
    }

    [TestMethod]
    public void GetBatchReportReturnsTheBatchReportSchema()
    {
        using var fixture = new McpFixture();
        Submit(fixture);

        using var session = fixture.RunInitialized(
            McpFrames.ToolCall(2, McpToolNames.GetBatchReport, "{\"batchId\":\"fire-pack\"}"),
            McpFrames.ToolCall(3, McpToolNames.GetBatchReport, "{\"batchId\":\"absent-pack\"}"));

        Assert.IsFalse(session.ToolIsError(2), session.RawOutput);
        using var payload = session.ToolPayload(2);
        var report = payload.RootElement.GetProperty("report");
        Assert.AreEqual(BatchReport.CurrentSchema, report.GetProperty("schemaVersion").GetString());
        Assert.AreEqual("fire-pack", report.GetProperty("batchId").GetString());
        Assert.AreEqual(BatchFailurePolicies.Continue, report.GetProperty("onFailure").GetString());
        var summary = report.GetProperty("summary");
        Assert.AreEqual(3, summary.GetProperty("total").GetInt32());
        Assert.AreEqual(3, summary.GetProperty("pending").GetInt32());
        Assert.AreEqual(0, summary.GetProperty("succeeded").GetInt32());
        Assert.AreEqual(3, report.GetProperty("items").GetArrayLength());
        Assert.IsTrue(session.ToolIsError(3));
    }

    [TestMethod]
    public async Task GetBatchReportCountsTerminalOutcomes()
    {
        using var fixture = new McpFixture();
        Submit(fixture);
        await DrainAsync(fixture.Store, expectedTerminal: 3);

        using var session = fixture.RunInitialized(
            McpFrames.ToolCall(2, McpToolNames.GetBatchReport, "{\"batchId\":\"fire-pack\"}"));

        using var payload = session.ToolPayload(2);
        var report = payload.RootElement.GetProperty("report");
        var summary = report.GetProperty("summary");
        Assert.AreEqual(2, summary.GetProperty("succeeded").GetInt32());
        Assert.AreEqual(1, summary.GetProperty("failed").GetInt32());
        Assert.AreEqual(0, summary.GetProperty("pending").GetInt32());
        Assert.AreEqual(
            0,
            summary.GetProperty("skippedIdempotent").GetInt32(),
            "A queue-derived report can only describe entries the queue holds.");
        var items = report.GetProperty("items").EnumerateArray().ToArray();
        Assert.AreEqual(JobCompletionOutcomes.Failed, items[1].GetProperty("outcome").GetString());
        Assert.AreEqual(
            JobQueueDiagnosticCodes.ExecutionFailed,
            items[1].GetProperty("diagnostic").GetString());
        Assert.AreEqual(1, items[0].GetProperty("artifactCount").GetInt32());
    }

    [TestMethod]
    public void GetBatchReportCountsACancelledBatch()
    {
        using var fixture = new McpFixture();
        Submit(fixture);

        using var session = fixture.RunInitialized(
            McpFrames.ToolCall(2, McpToolNames.CancelBatch, "{\"batchId\":\"fire-pack\"}"),
            McpFrames.ToolCall(3, McpToolNames.GetBatchReport, "{\"batchId\":\"fire-pack\"}"));

        using var payload = session.ToolPayload(3);
        var summary = payload.RootElement.GetProperty("report").GetProperty("summary");
        Assert.AreEqual(3, summary.GetProperty("cancelled").GetInt32());
        Assert.AreEqual(0, summary.GetProperty("pending").GetInt32());
    }

    [TestMethod]
    public void AnUnknownToolIsAProtocolError()
    {
        using var fixture = new McpFixture();

        using var session = fixture.RunInitialized(
            McpFrames.ToolCall(2, "vfx_build_prefab", "{}"),
            McpFrames.ToolCall(3, "vfx_submit_batch_v2", "{}"));

        foreach (var id in new long[] { 2, 3 })
        {
            Assert.AreEqual(JsonRpcErrorCodes.InvalidParams, session.ErrorCode(id));
            Assert.AreEqual(McpDiagnosticCodes.UnknownTool, session.ErrorDiagnostic(id));
        }
    }

    [TestMethod]
    public void ArgumentsThatAreMissingMistypedUnknownOrOutOfBoundsAreProtocolErrors()
    {
        using var fixture = new McpFixture();

        using var session = fixture.RunInitialized(
            McpFrames.ToolCallWithoutArguments(2, McpToolNames.ValidateManifest),
            McpFrames.ToolCall(3, McpToolNames.ValidateManifest, "{\"manifest\":123}"),
            McpFrames.ToolCall(4, McpToolNames.ValidateManifest,
                "{\"manifest\":\"{}\",\"approvalToken\":\"x\"}"),
            McpFrames.ToolCall(5, McpToolNames.SubmitBatch,
                "{\"manifest\":\"{}\",\"onFailure\":\"ignore\"}"),
            McpFrames.ToolCall(6, McpToolNames.JobStatus, "{\"jobId\":\"../../etc/passwd\"}"),
            McpFrames.ToolCall(7, McpToolNames.CancelBatch,
                "{\"batchId\":" + McpFrames.Quote(new string('a', 129)) + "}"),
            McpFrames.ToolCall(8, McpToolNames.GenerateEffect, "{\"item\":\"not-an-object\"}"),
            McpFrames.ToolCall(9, McpToolNames.BatchStatus, "{}"));

        foreach (var id in new long[] { 2, 3, 4, 5, 6, 7, 8, 9 })
        {
            Assert.AreEqual(JsonRpcErrorCodes.InvalidParams, session.ErrorCode(id), "Call " + id);
            Assert.AreEqual(McpDiagnosticCodes.InvalidToolArguments, session.ErrorDiagnostic(id), "Call " + id);
        }

        Assert.AreEqual(0, fixture.Store.ReadSnapshot().Jobs.Count);
    }

    [TestMethod]
    public void AnOverlongManifestArgumentIsRefusedBeforeParsing()
    {
        using var fixture = new McpFixture();
        var oversized = "{\"pad\":\"" + new string('p', BatchManifestLimits.MaximumManifestBytes) + "\"}";

        using var session = fixture.RunInitialized(
            Call(2, McpToolNames.ValidateManifest, oversized));

        Assert.AreEqual(JsonRpcErrorCodes.InvalidParams, session.ErrorCode(2));
        Assert.AreEqual(McpDiagnosticCodes.InvalidToolArguments, session.ErrorDiagnostic(2));
    }

    [TestMethod]
    public void AStoreFaultIsReportedAsAToolErrorWithTheStableQueueCode()
    {
        using var fixture = new McpFixture { QueueClientOverride = new UnavailableQueueClient() };

        using var session = fixture.RunInitialized(
            Call(2, McpToolNames.SubmitBatch, Manifest()),
            McpFrames.ToolCall(3, McpToolNames.BatchStatus, "{\"batchId\":\"fire-pack\"}"),
            McpFrames.ToolCall(4, McpToolNames.CancelBatch, "{\"batchId\":\"fire-pack\"}"),
            McpFrames.ToolCall(5, McpToolNames.GetBatchReport, "{\"batchId\":\"fire-pack\"}"),
            McpFrames.ToolCall(6, McpToolNames.CancelJob, "{\"jobId\":\"job-1\"}"),
            McpFrames.ToolCall(7, McpToolNames.JobStatus, "{\"jobId\":\"job-1\"}"));

        foreach (var id in new long[] { 2, 3, 4, 5, 6, 7 })
        {
            Assert.IsTrue(session.ToolIsError(id), "Call " + id);
            using var payload = session.ToolPayload(id);
            var error = payload.RootElement.GetProperty("error");
            Assert.AreEqual(McpDiagnosticCodes.QueueUnavailable, error.GetProperty("code").GetString(), "Call " + id);
            Assert.AreEqual(
                JobQueueDiagnosticCodes.StoreUnavailable,
                error.GetProperty("queueDiagnostic").GetString(),
                "Call " + id);
        }
    }

    [TestMethod]
    public void QueryAndCancelToolsNeverOpenTheGenerationRuntime()
    {
        using var fixture = new McpFixture();
        Submit(fixture);
        var jobId = fixture.Store.ReadSnapshot().Jobs[0].JobId;
        fixture.ForbidGenerationRuntime = true;

        using var session = fixture.RunInitialized(
            McpFrames.ToolCall(2, McpToolNames.BatchStatus, "{\"batchId\":\"fire-pack\"}"),
            McpFrames.ToolCall(3, McpToolNames.JobStatus, "{\"jobId\":" + McpFrames.Quote(jobId) + "}"),
            McpFrames.ToolCall(4, McpToolNames.GetBatchReport, "{\"batchId\":\"fire-pack\"}"),
            McpFrames.ToolCall(5, McpToolNames.CancelJob, "{\"jobId\":" + McpFrames.Quote(jobId) + "}"),
            McpFrames.ToolCall(6, McpToolNames.CancelBatch, "{\"batchId\":\"fire-pack\"}"));

        foreach (var id in new long[] { 2, 3, 4, 5, 6 })
        {
            Assert.IsFalse(session.ToolIsError(id), "Call " + id + ": " + session.RawOutput);
        }
    }

    private static string Manifest() => McpManifests.ThreePrompts();

    private static string Call(long id, string tool, string manifest) =>
        McpFrames.ToolCall(id, tool, "{\"manifest\":" + McpFrames.Quote(manifest) + "}");

    private static void Submit(McpFixture fixture)
    {
        using var session = fixture.RunInitialized(Call(2, McpToolNames.SubmitBatch, Manifest()));
        Assert.IsFalse(session.ToolIsError(2), session.RawOutput);
    }

    /// <summary>
    /// Settles the queue through the queue's own executor host, so terminal states come from the
    /// real transition rules instead of being written into the store by a test. Nothing about the
    /// MCP surface hosts an executor; this only stands in for whichever process would.
    /// </summary>
    private static async Task DrainAsync(JobStore store, int expectedTerminal)
    {
        await using var host = new JobQueueHost(store, [new PayloadDrivenExecutor()], FastHostOptions);
        host.Start();
        var deadline = DateTimeOffset.UtcNow.AddSeconds(20);
        while (store.ReadSnapshot().Jobs.Count(static job => job.IsTerminal) < expectedTerminal)
        {
            if (DateTimeOffset.UtcNow > deadline)
            {
                throw new TimeoutException("The queue did not settle the expected entries in time.");
            }

            await Task.Delay(25);
        }
    }

    private static readonly JobQueueHostOptions FastHostOptions = new()
    {
        IdlePollInterval = TimeSpan.FromMilliseconds(25),
        ProjectLockInitialBackoff = TimeSpan.FromMilliseconds(25),
        ProjectLockMaximumBackoff = TimeSpan.FromMilliseconds(50),
        CancellationPollInterval = TimeSpan.FromMilliseconds(25),
        JobTimeout = TimeSpan.FromSeconds(30),
        CancellationGracePeriod = TimeSpan.FromMilliseconds(500),
    };

    /// <summary>
    /// Settles each entry from its own payload, so the outcome depends on the entry rather than on
    /// execution order: the fixture's second entry is the one marked to fail.
    /// </summary>
    private sealed class PayloadDrivenExecutor : IJobExecutor
    {
        private const string FailingMarker = "POISON";

        public string JobKind => BatchJobKinds.RecipeGeneration;

        public bool RequiresProjectLock => false;

        public Task ExecuteAsync(JobExecutionContext context, CancellationToken cancellationToken)
        {
            var content = BatchGenerationPayload.Parse(context.Payload);
            if (content.Prompt.Contains(FailingMarker, StringComparison.Ordinal))
            {
                throw new JobQueueException(JobQueueDiagnosticCodes.ExecutionFailed);
            }

            context.ReportArtifact("draft-synthetic");
            return Task.CompletedTask;
        }
    }
}

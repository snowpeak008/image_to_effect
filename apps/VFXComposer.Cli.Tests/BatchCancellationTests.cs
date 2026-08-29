using Microsoft.VisualStudio.TestTools.UnitTesting;
using VFXComposer.Batch.Core;
using VFXComposer.Jobs;
using VFXComposer.Protocol.Jobs;

namespace VFXComposer.Cli.Tests;

/// <summary>
/// The three cancellation paths of REQ-003 §8.2 as the batch-level entry point exercises them,
/// plus the store-fault and unknown-batch boundaries. The service is the single implementation
/// both the CLI command and the MCP tool call.
/// </summary>
[TestClass]
public sealed class BatchCancellationTests
{
    [TestMethod]
    public void QueuedEntriesSettleImmediatelyAcrossTheWholeBatch()
    {
        using var fixture = new StoreFixture();
        fixture.Enqueue("fire-pack", "alpha");
        fixture.Enqueue("fire-pack", "beta");
        fixture.Enqueue("other-pack", "gamma");

        var result = new BatchCancellationService(fixture.Store).Cancel("fire-pack");

        Assert.IsTrue(result.BatchFound);
        Assert.AreEqual(2, result.Requested);
        Assert.AreEqual(2, result.Accepted);
        Assert.AreEqual(0, result.NoOp);
        Assert.IsTrue(result.Items.All(item => item.State == JobStatusStates.Cancelled));
        var jobs = fixture.Store.ReadSnapshot().Jobs;
        Assert.IsTrue(jobs
            .Where(job => job.BatchId == "fire-pack")
            .All(job => job.State == JobStatusStates.Cancelled &&
                job.FinalDiagnosticCode == JobQueueDiagnosticCodes.CancelledQueued));
        Assert.AreEqual(
            JobStatusStates.Queued,
            jobs.Single(job => job.BatchId == "other-pack").State,
            "Cancelling one batch must not touch another batch.");
    }

    [TestMethod]
    public async Task ARunningEntryIsCancelledCooperativelyAndSettlesAsCancelled()
    {
        using var fixture = new StoreFixture();
        fixture.Enqueue("fire-pack", "alpha");
        var executor = new BlockingJobExecutor();
        await using var host = new JobQueueHost(fixture.Store, [executor], CliTestHarness.FastHostOptions);
        host.Start();
        Assert.IsTrue(executor.Started.Wait(TimeSpan.FromSeconds(20)), "The entry never started executing.");

        var result = new BatchCancellationService(fixture.Store).Cancel("fire-pack");

        Assert.AreEqual(1, result.Accepted);
        Assert.AreEqual(
            JobStatusStates.Running,
            result.Items[0].State,
            "A running entry stays running until the executor observes the cooperative request.");
        var settled = await fixture.WaitForTerminalAsync(1);
        Assert.AreEqual(JobStatusStates.Cancelled, settled[0].State);
        Assert.AreEqual(JobQueueDiagnosticCodes.CancelledRunning, settled[0].FinalDiagnosticCode);
    }

    [TestMethod]
    public void ATerminalEntryIsAnIdempotentNoOp()
    {
        using var fixture = new StoreFixture();
        var job = fixture.Enqueue("fire-pack", "alpha");
        fixture.Store.RequestCancel(job.JobId);
        var service = new BatchCancellationService(fixture.Store);

        var first = service.Cancel("fire-pack");
        var second = service.Cancel("fire-pack");

        foreach (var result in new[] { first, second })
        {
            Assert.AreEqual(1, result.Requested);
            Assert.AreEqual(0, result.Accepted);
            Assert.AreEqual(1, result.NoOp);
            Assert.AreEqual(JobStatusStates.Cancelled, result.Items[0].State);
        }
    }

    [TestMethod]
    public void AnUnknownBatchIsReportedAsNotFoundWithoutTouchingTheQueue()
    {
        using var fixture = new StoreFixture();
        fixture.Enqueue("fire-pack", "alpha");

        var result = new BatchCancellationService(fixture.Store).Cancel("absent-pack");

        Assert.IsFalse(result.BatchFound);
        Assert.AreEqual(0, result.Requested);
        Assert.AreEqual(
            JobStatusStates.Queued,
            fixture.Store.ReadSnapshot().Jobs[0].State,
            "An unknown batch id must not cancel anything.");
    }

    [TestMethod]
    public void AStoreFaultSurfacesAsTheTypedQueueFailure()
    {
        var service = new BatchCancellationService(new UnavailableQueueClient());

        var exception = Assert.ThrowsExactly<JobQueueException>(() => service.Cancel("fire-pack"));

        Assert.AreEqual(JobQueueDiagnosticCodes.StoreUnavailable, exception.Code);
    }

    /// <summary>Executor that parks until the host cancels it, so a job can be observed RUNNING.</summary>
    private sealed class BlockingJobExecutor : IJobExecutor
    {
        public ManualResetEventSlim Started { get; } = new(initialState: false);

        public string JobKind => BatchJobKinds.RecipeGeneration;

        public bool RequiresProjectLock => false;

        public Task ExecuteAsync(JobExecutionContext context, CancellationToken cancellationToken)
        {
            Started.Set();
            return Task.Delay(Timeout.Infinite, cancellationToken);
        }
    }

    private sealed class StoreFixture : IDisposable
    {
        private readonly string _directory;

        public StoreFixture()
        {
            _directory = CliTestHarness.CreateDirectory();
            Store = new JobStore(Path.Combine(_directory, "store"));
        }

        public JobStore Store { get; }

        public JobRecord Enqueue(string batchId, string itemId) => Store.Enqueue(new JobEnqueueRequest(
            JobSourceEntries.Cli,
            BatchJobKinds.RecipeGeneration,
            BatchGenerationPayload.Create(new BatchManifestItem(
                itemId,
                BatchItemKinds.Prompt,
                "a bounded synthetic prompt",
                RecipePath: null,
                BatchConstraints.Empty)),
            batchId,
            JobBatchPolicies.Continue,
            itemId));

        public async Task<IReadOnlyList<JobRecord>> WaitForTerminalAsync(int expected)
        {
            var deadline = DateTimeOffset.UtcNow.AddSeconds(20);
            while (true)
            {
                var terminal = Store.ReadSnapshot().Jobs.Where(job => job.IsTerminal).ToArray();
                if (terminal.Length >= expected)
                {
                    return terminal;
                }

                if (DateTimeOffset.UtcNow > deadline)
                {
                    throw new TimeoutException("The queue did not settle the expected entries in time.");
                }

                await Task.Delay(25);
            }
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_directory, recursive: true);
            }
            catch (IOException)
            {
                // Temporary fixture cleanup is best effort.
            }
        }
    }
}

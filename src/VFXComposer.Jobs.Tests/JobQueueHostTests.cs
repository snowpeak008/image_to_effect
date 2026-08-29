using Microsoft.VisualStudio.TestTools.UnitTesting;
using VFXComposer.Protocol.Jobs;

namespace VFXComposer.Jobs.Tests;

[TestClass]
public sealed class JobQueueHostTests
{
    [TestMethod]
    public async Task AcceptanceOne_ConcurrentSubmissionsFromAllSurfacesExecuteStrictlySerially()
    {
        var store = new JobStore(JobQueueTestHarness.CreateStoreDirectory());
        var running = 0;
        var maxObserved = 0;
        var executionOrder = new List<string>();
        var executor = new DelegateJobExecutor("test.job", async (context, token) =>
        {
            var current = Interlocked.Increment(ref running);
            InterlockedMax(ref maxObserved, current);
            lock (executionOrder)
            {
                executionOrder.Add(context.Payload);
            }

            await Task.Delay(100, token);
            Interlocked.Decrement(ref running);
        });

        var submissions = await Task.WhenAll(
            Task.Run(() => store.Enqueue(JobQueueTestHarness.Request(payload: "p0", sourceEntry: JobSourceEntries.Desktop))),
            Task.Run(() => store.Enqueue(JobQueueTestHarness.Request(payload: "p1", sourceEntry: JobSourceEntries.Cli))),
            Task.Run(() => store.Enqueue(JobQueueTestHarness.Request(payload: "p2", sourceEntry: JobSourceEntries.Cli))));
        await using (var host = new JobQueueHost(store, [executor], JobQueueTestHarness.FastOptions))
        {
            host.Start();
            await JobQueueTestHarness.WaitUntilAsync(() =>
                store.ReadSnapshot().Jobs.All(job => job.State == JobStatusStates.Succeeded));
        }

        Assert.AreEqual(1, maxObserved, "Global concurrency must never exceed one.");
        var expectedOrder = store.ReadSnapshot().Jobs
            .OrderBy(job => job.QueuePosition)
            .Select(job => job.Payload)
            .ToArray();
        CollectionAssert.AreEqual(expectedOrder, executionOrder, "Execution must follow strict FIFO order.");
        Assert.AreEqual(3, submissions.Select(job => job.JobId).Distinct().Count());
    }

    [TestMethod]
    public async Task AcceptanceTwo_CancellingAQueuedJobNeverStartsItAndLeavesTheRunningJobAlone()
    {
        var store = new JobStore(JobQueueTestHarness.CreateStoreDirectory());
        var started = new List<string>();
        var release = new TaskCompletionSource();
        var executor = new DelegateJobExecutor("test.job", async (context, token) =>
        {
            lock (started)
            {
                started.Add(context.JobId);
            }

            await release.Task.WaitAsync(token);
        });
        var first = store.Enqueue(JobQueueTestHarness.Request(payload: "first"));
        var second = store.Enqueue(JobQueueTestHarness.Request(payload: "second"));

        await using var host = new JobQueueHost(store, [executor], JobQueueTestHarness.FastOptions);
        host.Start();
        await JobQueueTestHarness.WaitUntilAsync(() =>
            JobQueueTestHarness.GetJob(store, first.JobId).State == JobStatusStates.Running);

        var result = store.RequestCancel(second.JobId);
        Assert.AreEqual(JobStatusStates.Cancelled, result.State);

        release.SetResult();
        await JobQueueTestHarness.WaitUntilAsync(() =>
            JobQueueTestHarness.GetJob(store, first.JobId).State == JobStatusStates.Succeeded);

        var cancelled = JobQueueTestHarness.GetJob(store, second.JobId);
        Assert.AreEqual(JobStatusStates.Cancelled, cancelled.State);
        Assert.AreEqual(JobQueueDiagnosticCodes.CancelledQueued, cancelled.FinalDiagnosticCode);
        Assert.IsNull(cancelled.StartedAtUtc, "A queued job cancelled before claim must never run.");
        CollectionAssert.AreEqual(new[] { first.JobId }, started);
    }

    [TestMethod]
    public async Task AcceptanceThree_CancellingARunningJobIsCooperativeAndCleansUp()
    {
        var store = new JobStore(JobQueueTestHarness.CreateStoreDirectory());
        var inspector = new RecordingProcessInspector();
        var childStart = new DateTimeOffset(2026, 8, 29, 10, 0, 0, TimeSpan.Zero);
        string? temporaryDirectory = null;
        var executor = new DelegateJobExecutor("test.job", async (context, token) =>
        {
            temporaryDirectory = context.TemporaryDirectory;
            await File.WriteAllTextAsync(Path.Combine(context.TemporaryDirectory, "scratch.txt"), "wip", token);
            context.ReportProgress(300);
            context.RegisterChildProcess(43_210, childStart);
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
        });
        var job = store.Enqueue(JobQueueTestHarness.Request());

        await using var host = new JobQueueHost(
            store, [executor], JobQueueTestHarness.FastOptions, processInspector: inspector);
        host.Start();
        await JobQueueTestHarness.WaitUntilAsync(() =>
            JobQueueTestHarness.GetJob(store, job.JobId).LastProgressPermille == 300);

        store.RequestCancel(job.JobId);
        await JobQueueTestHarness.WaitUntilAsync(() =>
            JobQueueTestHarness.GetJob(store, job.JobId).State == JobStatusStates.Cancelled);

        var settled = JobQueueTestHarness.GetJob(store, job.JobId);
        Assert.AreEqual(JobQueueDiagnosticCodes.CancelledRunning, settled.FinalDiagnosticCode);
        var progressStates = store.ReadEvents(job.JobId)
            .Where(storeEvent => storeEvent.Kind == JobStoreEventKinds.Progress)
            .Select(storeEvent => storeEvent.State)
            .ToArray();
        CollectionAssert.Contains(progressStates, JobProgressStates.CancellationRequested);
        Assert.IsFalse(Directory.Exists(temporaryDirectory), "The job temp directory must be cleaned up.");
        Assert.AreEqual((43_210, childStart), inspector.Terminated.Single());
    }

    [TestMethod]
    public async Task AcceptanceFour_CrashRecoverySettlesRunningAsDisconnectedAndContinuesTheQueue()
    {
        var directory = JobQueueTestHarness.CreateStoreDirectory();
        var store = new JobStore(directory);
        var childStart = new DateTimeOffset(2026, 8, 29, 9, 0, 0, TimeSpan.Zero);
        var crashed = store.Enqueue(JobQueueTestHarness.Request(payload: "crashed"));
        var queuedA = store.Enqueue(JobQueueTestHarness.Request(payload: "queued-a"));
        var queuedB = store.Enqueue(JobQueueTestHarness.Request(payload: "queued-b"));

        // Equivalent crash simulation: the job is claimed with a recorded child process, then no
        // executor host exists anymore (as if the process was killed without any teardown).
        store.TryClaim(crashed.JobId);
        store.RegisterChildProcess(crashed.JobId, 55_555, childStart);
        Directory.CreateDirectory(store.GetTemporaryDirectory(crashed.JobId));

        var inspector = new RecordingProcessInspector();
        var executed = new List<string>();
        var executor = new DelegateJobExecutor("test.job", (context, _) =>
        {
            lock (executed)
            {
                executed.Add(context.Payload);
            }

            return Task.CompletedTask;
        });
        await using (var host = new JobQueueHost(
            store, [executor], JobQueueTestHarness.FastOptions, processInspector: inspector))
        {
            host.Start();
            await JobQueueTestHarness.WaitUntilAsync(() =>
                JobQueueTestHarness.GetJob(store, queuedB.JobId).State == JobStatusStates.Succeeded);
        }

        var disconnected = JobQueueTestHarness.GetJob(store, crashed.JobId);
        Assert.AreEqual(JobStatusStates.Disconnected, disconnected.State);
        Assert.AreEqual(JobQueueDiagnosticCodes.DisconnectedRecovery, disconnected.FinalDiagnosticCode);
        CollectionAssert.AreEqual(
            new[] { "queued-a", "queued-b" },
            executed,
            "Queued jobs must survive recovery in order and never include an automatic re-run.");
        Assert.AreEqual((55_555, childStart), inspector.Terminated.Single());
        Assert.IsFalse(
            Directory.Exists(store.GetTemporaryDirectory(crashed.JobId)),
            "Recovery must clean the crashed job's temp directory.");
        var sequences = store.ReadEvents(crashed.JobId).Select(e => e.EventSequence).ToArray();
        CollectionAssert.AreEqual(
            Enumerable.Range(1, sequences.Length).Select(i => (long)i).ToArray(),
            sequences,
            "Event sequences must stay contiguous across the crash.");
    }

    [TestMethod]
    public async Task AcceptanceFive_EditorOwnedProjectKeepsTheJobQueuedAndResumesAutomatically()
    {
        var store = new JobStore(JobQueueTestHarness.CreateStoreDirectory());
        var probe = new FakeProjectLockProbe { Busy = true };
        var executor = new DelegateJobExecutor(
            "build.job",
            (_, _) => Task.CompletedTask,
            requiresProjectLock: true);
        var job = store.Enqueue(JobQueueTestHarness.Request(jobKind: "build.job"));

        await using var host = new JobQueueHost(
            store, [executor], JobQueueTestHarness.FastOptions, projectLockProbe: probe);
        host.Start();
        await JobQueueTestHarness.WaitUntilAsync(() =>
            store.ReadSnapshot().QueueState == JobQueueStates.WaitingProjectLock);

        Assert.AreEqual(
            JobStatusStates.Queued,
            JobQueueTestHarness.GetJob(store, job.JobId).State,
            "An editor-owned project must never fail the job.");

        probe.Busy = false;
        await JobQueueTestHarness.WaitUntilAsync(() =>
            JobQueueTestHarness.GetJob(store, job.JobId).State == JobStatusStates.Succeeded);
        await JobQueueTestHarness.WaitUntilAsync(() =>
            store.ReadSnapshot().QueueState == JobQueueStates.Idle);
    }

    [TestMethod]
    public async Task CancellationDuringProjectLockWaitStillWorks()
    {
        var store = new JobStore(JobQueueTestHarness.CreateStoreDirectory());
        var probe = new FakeProjectLockProbe { Busy = true };
        var executor = new DelegateJobExecutor(
            "build.job",
            (_, _) => Task.CompletedTask,
            requiresProjectLock: true);
        var job = store.Enqueue(JobQueueTestHarness.Request(jobKind: "build.job"));

        await using var host = new JobQueueHost(
            store, [executor], JobQueueTestHarness.FastOptions, projectLockProbe: probe);
        host.Start();
        await JobQueueTestHarness.WaitUntilAsync(() =>
            store.ReadSnapshot().QueueState == JobQueueStates.WaitingProjectLock);

        var result = store.RequestCancel(job.JobId);

        Assert.AreEqual(JobStatusStates.Cancelled, result.State);
        await JobQueueTestHarness.WaitUntilAsync(() =>
            store.ReadSnapshot().QueueState == JobQueueStates.Idle);
    }

    [TestMethod]
    public async Task SecondExecutorHostFailsClosedWhileTheFirstIsAliveAndCanTakeOverAfterwards()
    {
        var store = new JobStore(JobQueueTestHarness.CreateStoreDirectory());
        var executor = new DelegateJobExecutor("test.job", (_, _) => Task.CompletedTask);

        var first = new JobQueueHost(store, [executor], JobQueueTestHarness.FastOptions);
        first.Start();
        try
        {
            var second = new JobQueueHost(store, [executor], JobQueueTestHarness.FastOptions);
            var exception = Assert.ThrowsExactly<JobQueueException>(second.Start);
            Assert.AreEqual(JobQueueDiagnosticCodes.ExecutorLockUnavailable, exception.Code);
            await second.DisposeAsync();
        }
        finally
        {
            await first.DisposeAsync();
        }

        // The durable anchor outlives the lease; a successor acquires it without deleting anything.
        await using var successor = new JobQueueHost(store, [executor], JobQueueTestHarness.FastOptions);
        successor.Start();
        Assert.IsTrue(File.Exists(store.ExecutorLockPath), "The durable lock anchor must never be deleted.");
    }

    [TestMethod]
    public async Task ExecutionTimeoutFailsTheJobWithTheStableTimeoutCode()
    {
        var store = new JobStore(JobQueueTestHarness.CreateStoreDirectory());
        var options = JobQueueTestHarness.FastOptions with
        {
            JobTimeout = TimeSpan.FromMilliseconds(150),
            CancellationGracePeriod = TimeSpan.FromMilliseconds(100),
        };
        var executor = new DelegateJobExecutor("test.job", async (_, _) =>
        {
            // Deliberately ignores the cancellation token to exercise the abandonment path.
            await Task.Delay(TimeSpan.FromSeconds(30), CancellationToken.None);
        });
        var job = store.Enqueue(JobQueueTestHarness.Request());

        await using var host = new JobQueueHost(store, [executor], options);
        host.Start();
        await JobQueueTestHarness.WaitUntilAsync(() =>
            JobQueueTestHarness.GetJob(store, job.JobId).State == JobStatusStates.Failed);

        Assert.AreEqual(
            JobQueueDiagnosticCodes.ExecutionTimeout,
            JobQueueTestHarness.GetJob(store, job.JobId).FinalDiagnosticCode);
    }

    [TestMethod]
    public async Task PayloadFailureSettlesFailedWithTheGenericExecutionCode()
    {
        var store = new JobStore(JobQueueTestHarness.CreateStoreDirectory());
        var executor = new DelegateJobExecutor("test.job", (_, _) =>
            throw new InvalidOperationException("Synthetic payload failure."));
        var job = store.Enqueue(JobQueueTestHarness.Request());

        await using var host = new JobQueueHost(store, [executor], JobQueueTestHarness.FastOptions);
        host.Start();
        await JobQueueTestHarness.WaitUntilAsync(() =>
            JobQueueTestHarness.GetJob(store, job.JobId).State == JobStatusStates.Failed);

        Assert.AreEqual(
            JobQueueDiagnosticCodes.ExecutionFailed,
            JobQueueTestHarness.GetJob(store, job.JobId).FinalDiagnosticCode);
    }

    [TestMethod]
    public async Task UnregisteredJobKindFailsClosedWithTheUnsupportedKindCode()
    {
        var store = new JobStore(JobQueueTestHarness.CreateStoreDirectory());
        var executor = new DelegateJobExecutor("test.job", (_, _) => Task.CompletedTask);
        var job = store.Enqueue(JobQueueTestHarness.Request(jobKind: "unknown.kind"));

        await using var host = new JobQueueHost(store, [executor], JobQueueTestHarness.FastOptions);
        host.Start();
        await JobQueueTestHarness.WaitUntilAsync(() =>
            JobQueueTestHarness.GetJob(store, job.JobId).State == JobStatusStates.Failed);

        Assert.AreEqual(
            JobQueueDiagnosticCodes.JobKindUnsupported,
            JobQueueTestHarness.GetJob(store, job.JobId).FinalDiagnosticCode);
    }

    [TestMethod]
    public async Task AHostSideIoFaultSettlesTheJobWithTheHostFaultCodeAndKeepsTheQueueDraining()
    {
        var store = new JobStore(JobQueueTestHarness.CreateStoreDirectory());
        var executed = new List<string>();
        var executor = new DelegateJobExecutor("test.job", (context, _) =>
        {
            lock (executed)
            {
                executed.Add(context.Payload);
            }

            return Task.CompletedTask;
        });
        var blocked = store.Enqueue(JobQueueTestHarness.Request(payload: "blocked"));
        var survivor = store.Enqueue(JobQueueTestHarness.Request(payload: "survivor"));

        // A real IO fault instead of an injected seam: a file occupies the exact path of the
        // job's scratch directory, so the loop's Directory.CreateDirectory throws IOException,
        // which is not a JobQueueException and used to fault the loop task outright.
        var scratchPath = store.GetTemporaryDirectory(blocked.JobId);
        Directory.CreateDirectory(Path.GetDirectoryName(scratchPath)!);
        await File.WriteAllTextAsync(scratchPath, "occupied");

        var host = new JobQueueHost(store, [executor], JobQueueTestHarness.FastOptions);
        host.Start();
        await JobQueueTestHarness.WaitUntilAsync(() =>
            JobQueueTestHarness.GetJob(store, survivor.JobId).State == JobStatusStates.Succeeded);

        var settled = JobQueueTestHarness.GetJob(store, blocked.JobId);
        Assert.AreEqual(JobStatusStates.Failed, settled.State, "A host-side fault must settle the claimed job.");
        Assert.AreEqual(JobQueueDiagnosticCodes.ExecutorHostFault, settled.FinalDiagnosticCode);
        CollectionAssert.AreEqual(
            new[] { "survivor" },
            executed,
            "The faulted job never reaches its payload, and the next job runs normally.");

        await host.DisposeAsync();

        // Teardown neither hangs nor rethrows, and the executor lease was really released.
        await using var successor = new JobQueueHost(store, [executor], JobQueueTestHarness.FastOptions);
        successor.Start();
        Assert.IsTrue(successor.IsExecuting);
    }

    [TestMethod]
    public async Task AHostWithoutPayloadExecutorsNeitherClaimsJobsNorTakesTheExecutorLock()
    {
        var store = new JobStore(JobQueueTestHarness.CreateStoreDirectory());
        var job = store.Enqueue(JobQueueTestHarness.Request());

        var observer = new JobQueueHost(store, Array.Empty<IJobExecutor>(), JobQueueTestHarness.FastOptions);
        observer.Start();
        try
        {
            Assert.IsFalse(observer.IsExecuting, "A host with no executors must never own queue execution.");
            await Task.Delay(250);
            Assert.AreEqual(
                JobStatusStates.Queued,
                JobQueueTestHarness.GetJob(store, job.JobId).State,
                "An observer must not claim a job it could never execute.");
            Assert.IsFalse(
                File.Exists(store.ExecutorLockPath),
                "An observer must not even create the executor lock anchor.");

            // The entry surface that does have an executor takes over while the observer is live.
            var executor = new DelegateJobExecutor("test.job", (_, _) => Task.CompletedTask);
            await using var worker = new JobQueueHost(store, [executor], JobQueueTestHarness.FastOptions);
            worker.Start();

            Assert.IsTrue(worker.IsExecuting);
            await JobQueueTestHarness.WaitUntilAsync(() =>
                JobQueueTestHarness.GetJob(store, job.JobId).State == JobStatusStates.Succeeded);
        }
        finally
        {
            await observer.DisposeAsync();
        }
    }

    [TestMethod]
    public async Task AbortBatchPolicyCancelsTheRemainingQueuedBatchJobs()
    {
        var store = new JobStore(JobQueueTestHarness.CreateStoreDirectory());
        var executor = new DelegateJobExecutor("test.job", (context, _) =>
            context.Payload == "fails"
                ? throw new InvalidOperationException("Synthetic failure.")
                : Task.CompletedTask);
        var failing = store.Enqueue(JobQueueTestHarness.Request(
            payload: "fails", batchId: "batch-1", batchPolicy: JobBatchPolicies.Abort, itemId: "i1"));
        var abortedA = store.Enqueue(JobQueueTestHarness.Request(
            payload: "later-a", batchId: "batch-1", batchPolicy: JobBatchPolicies.Abort, itemId: "i2"));
        var unrelated = store.Enqueue(JobQueueTestHarness.Request(payload: "other"));

        await using var host = new JobQueueHost(store, [executor], JobQueueTestHarness.FastOptions);
        host.Start();
        await JobQueueTestHarness.WaitUntilAsync(() =>
            JobQueueTestHarness.GetJob(store, unrelated.JobId).State == JobStatusStates.Succeeded);

        Assert.AreEqual(JobStatusStates.Failed, JobQueueTestHarness.GetJob(store, failing.JobId).State);
        var aborted = JobQueueTestHarness.GetJob(store, abortedA.JobId);
        Assert.AreEqual(JobStatusStates.Cancelled, aborted.State);
        Assert.AreEqual(JobQueueDiagnosticCodes.BatchAborted, aborted.FinalDiagnosticCode);
    }

    [TestMethod]
    public async Task ContinueBatchPolicyExecutesTheRemainingBatchJobs()
    {
        var store = new JobStore(JobQueueTestHarness.CreateStoreDirectory());
        var executor = new DelegateJobExecutor("test.job", (context, _) =>
            context.Payload == "fails"
                ? throw new InvalidOperationException("Synthetic failure.")
                : Task.CompletedTask);
        var failing = store.Enqueue(JobQueueTestHarness.Request(
            payload: "fails", batchId: "batch-2", batchPolicy: JobBatchPolicies.Continue, itemId: "i1"));
        var survivor = store.Enqueue(JobQueueTestHarness.Request(
            payload: "later", batchId: "batch-2", batchPolicy: JobBatchPolicies.Continue, itemId: "i2"));

        await using var host = new JobQueueHost(store, [executor], JobQueueTestHarness.FastOptions);
        host.Start();
        await JobQueueTestHarness.WaitUntilAsync(() =>
            JobQueueTestHarness.GetJob(store, survivor.JobId).State == JobStatusStates.Succeeded);

        Assert.AreEqual(JobStatusStates.Failed, JobQueueTestHarness.GetJob(store, failing.JobId).State);
    }

    [TestMethod]
    public async Task GracefulHostShutdownSettlesTheRunningJobAsDisconnected()
    {
        var store = new JobStore(JobQueueTestHarness.CreateStoreDirectory());
        var executor = new DelegateJobExecutor("test.job", async (_, _) =>
            await Task.Delay(TimeSpan.FromSeconds(30), CancellationToken.None));
        var job = store.Enqueue(JobQueueTestHarness.Request());

        var host = new JobQueueHost(
            store,
            [executor],
            JobQueueTestHarness.FastOptions with { CancellationGracePeriod = TimeSpan.FromMilliseconds(100) });
        host.Start();
        await JobQueueTestHarness.WaitUntilAsync(() =>
            JobQueueTestHarness.GetJob(store, job.JobId).State == JobStatusStates.Running);

        await host.DisposeAsync();

        var settled = JobQueueTestHarness.GetJob(store, job.JobId);
        Assert.AreEqual(JobStatusStates.Disconnected, settled.State);
        Assert.AreEqual(JobQueueDiagnosticCodes.DisconnectedRecovery, settled.FinalDiagnosticCode);
    }

    [TestMethod]
    public async Task SuccessfulJobReportsArtifactsLogsAndFullProgressContract()
    {
        var store = new JobStore(JobQueueTestHarness.CreateStoreDirectory());
        var executor = new DelegateJobExecutor("test.job", (context, _) =>
        {
            context.ReportProgress(250);
            context.ReportLog(JobLogLevels.Info, JobQueueDiagnosticCodes.WaitingProjectLock);
            context.ReportArtifact("candidate-1");
            context.ReportProgress(900);
            return Task.CompletedTask;
        });
        var job = store.Enqueue(JobQueueTestHarness.Request());

        await using var host = new JobQueueHost(store, [executor], JobQueueTestHarness.FastOptions);
        host.Start();
        await JobQueueTestHarness.WaitUntilAsync(() =>
            JobQueueTestHarness.GetJob(store, job.JobId).State == JobStatusStates.Succeeded);

        var settled = JobQueueTestHarness.GetJob(store, job.JobId);
        Assert.AreEqual(1000, settled.LastProgressPermille);
        Assert.IsNull(settled.FinalDiagnosticCode);
        CollectionAssert.AreEqual(new[] { "candidate-1" }, settled.ArtifactIds.ToArray());
        var events = store.ReadEvents(job.JobId);
        var permilles = events
            .Where(e => e.Kind == JobStoreEventKinds.Progress)
            .Select(e => e.ProgressPermille!.Value)
            .ToArray();
        CollectionAssert.AreEqual(permilles.OrderBy(p => p).ToArray(), permilles, "Progress must be monotonic.");
        Assert.AreEqual(1, events.Count(e => e.Kind == JobStoreEventKinds.Log));
        Assert.AreEqual(1, events.Count(e => e.Kind == JobStoreEventKinds.Artifact));
        Assert.AreEqual(JobCompletionOutcomes.Succeeded, events.Last().Outcome);
    }

    private static void InterlockedMax(ref int target, int value)
    {
        int current;
        while (value > (current = Volatile.Read(ref target)))
        {
            Interlocked.CompareExchange(ref target, value, current);
        }
    }
}

using VFXComposer.Protocol.Jobs;

namespace VFXComposer.Jobs;

/// <summary>
/// In-process executor host. Exactly one host runs per store across all processes: startup
/// acquires the durable executor lock fail-closed, runs crash recovery, then drains the queue
/// strictly FIFO with global concurrency one. Entry surfaces submit through
/// <see cref="IJobQueueClient"/> and never execute jobs themselves.
/// </summary>
public sealed class JobQueueHost : IAsyncDisposable
{
    private readonly JobStore _store;
    private readonly IReadOnlyDictionary<string, IJobExecutor> _executors;
    private readonly JobQueueHostOptions _options;
    private readonly IProjectLockProbe _projectLockProbe;
    private readonly IJobProcessInspector _processInspector;
    private readonly CancellationTokenSource _shutdown = new();
    private JobExecutorLock? _lease;
    private Task? _loop;

    public JobQueueHost(
        JobStore store,
        IEnumerable<IJobExecutor> executors,
        JobQueueHostOptions? options = null,
        IProjectLockProbe? projectLockProbe = null,
        IJobProcessInspector? processInspector = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        ArgumentNullException.ThrowIfNull(executors);
        var registry = new Dictionary<string, IJobExecutor>(StringComparer.Ordinal);
        foreach (var executor in executors)
        {
            if (!registry.TryAdd(executor.JobKind, executor))
            {
                throw new ArgumentException("Duplicate payload executor for one job kind.", nameof(executors));
            }
        }

        _executors = registry;
        _options = options ?? JobQueueHostOptions.Default;
        _projectLockProbe = projectLockProbe ?? new AlwaysFreeProjectLockProbe();
        _processInspector = processInspector ?? new SystemJobProcessInspector();
    }

    /// <summary>
    /// Acquires the executor lock, settles crashed RUNNING jobs as DISCONNECTED, disposes
    /// recorded orphan processes by exact PID + start time, applies retention, then starts the
    /// serial execution loop. A second live host fails closed here with the stable lock error.
    /// </summary>
    public void Start()
    {
        if (_lease is not null)
        {
            throw new InvalidOperationException("The executor host is already started.");
        }

        _lease = JobExecutorLock.Acquire(_store.ExecutorLockPath);
        try
        {
            var recovery = _store.RecoverOnStartup();
            foreach (var orphan in recovery.OrphanProcesses)
            {
                _processInspector.TerminateExact(orphan.ProcessId, orphan.ProcessStartUtc);
            }

            _store.CleanupTerminalJobs();
        }
        catch
        {
            _lease.Dispose();
            _lease = null;
            throw;
        }

        _loop = Task.Run(RunLoopAsync);
    }

    public async ValueTask DisposeAsync()
    {
        if (!_shutdown.IsCancellationRequested)
        {
            await _shutdown.CancelAsync();
        }

        if (_loop is not null)
        {
            await _loop;
            _loop = null;
        }

        _lease?.Dispose();
        _lease = null;
        _shutdown.Dispose();
    }

    private async Task RunLoopAsync()
    {
        var projectLockBackoff = _options.ProjectLockInitialBackoff;
        while (!_shutdown.IsCancellationRequested)
        {
            JobRecord? head;
            try
            {
                head = _store.PeekNextQueued();
            }
            catch (JobQueueException)
            {
                await DelaySafeAsync(_options.IdlePollInterval);
                continue;
            }

            if (head is null)
            {
                TrySetQueueState(JobQueueStates.Idle);
                await DelaySafeAsync(_options.IdlePollInterval);
                continue;
            }

            _executors.TryGetValue(head.JobKind, out var executor);
            if (executor is not null &&
                executor.RequiresProjectLock &&
                _projectLockProbe.Probe() == ProjectLockAvailability.Busy)
            {
                // The editor owning the project is a normal working condition, not a job
                // failure: the job stays QUEUED and the queue waits with bounded backoff.
                TrySetQueueState(JobQueueStates.WaitingProjectLock);
                await DelaySafeAsync(projectLockBackoff);
                var doubled = projectLockBackoff + projectLockBackoff;
                projectLockBackoff = doubled <= _options.ProjectLockMaximumBackoff
                    ? doubled
                    : _options.ProjectLockMaximumBackoff;
                continue;
            }

            projectLockBackoff = _options.ProjectLockInitialBackoff;
            JobRecord? claimed;
            try
            {
                claimed = _store.TryClaim(head.JobId);
            }
            catch (JobQueueException)
            {
                await DelaySafeAsync(_options.IdlePollInterval);
                continue;
            }

            if (claimed is null)
            {
                continue;
            }

            TrySetQueueState(JobQueueStates.Executing);
            await ExecuteClaimedAsync(claimed, executor);
        }

        TrySetQueueState(JobQueueStates.Idle);
    }

    private async Task ExecuteClaimedAsync(JobRecord job, IJobExecutor? executor)
    {
        if (executor is null)
        {
            Settle(job, JobStatusStates.Failed, JobQueueDiagnosticCodes.JobKindUnsupported);
            return;
        }

        var temporaryDirectory = _store.GetTemporaryDirectory(job.JobId);
        Directory.CreateDirectory(temporaryDirectory);
        var context = new JobExecutionContext(_store, job, temporaryDirectory);
        using var jobCancellation = CancellationTokenSource.CreateLinkedTokenSource(_shutdown.Token);
        var payloadTask = Task.Run(
            () => executor.ExecuteAsync(context, jobCancellation.Token),
            CancellationToken.None);
        var deadline = DateTimeOffset.UtcNow + _options.JobTimeout;
        var userCancelled = false;
        var timedOut = false;

        while (!payloadTask.IsCompleted)
        {
            var finished = await Task.WhenAny(
                payloadTask,
                Task.Delay(_options.CancellationPollInterval, CancellationToken.None));
            if (finished == payloadTask)
            {
                break;
            }

            if (!jobCancellation.IsCancellationRequested)
            {
                if (DateTimeOffset.UtcNow > deadline)
                {
                    timedOut = true;
                    await jobCancellation.CancelAsync();
                }
                else if (_shutdown.IsCancellationRequested)
                {
                    await jobCancellation.CancelAsync();
                }
                else if (ReadCancelRequested(job.JobId))
                {
                    userCancelled = true;
                    await jobCancellation.CancelAsync();
                }
            }
            else
            {
                // The payload got its grace period after cancellation; settle without it if it
                // does not come back. Its faults are observed so nothing goes unhandled.
                var grace = await Task.WhenAny(
                    payloadTask,
                    Task.Delay(_options.CancellationGracePeriod, CancellationToken.None));
                if (grace != payloadTask)
                {
                    ObserveAbandonedFaults(payloadTask);
                    break;
                }
            }
        }

        if (!timedOut && payloadTask.IsCompletedSuccessfully)
        {
            // A cancellation request that lost the race against normal completion stays a
            // request: cancellation is documented as not instantaneous, and finished work wins.
            Settle(job, JobStatusStates.Succeeded, diagnosticCode: null);
            return;
        }

        TerminateRegisteredChildProcess(job.JobId);
        if (timedOut)
        {
            Settle(job, JobStatusStates.Failed, JobQueueDiagnosticCodes.ExecutionTimeout);
            return;
        }

        if (userCancelled)
        {
            Settle(job, JobStatusStates.Cancelled, JobQueueDiagnosticCodes.CancelledRunning);
            return;
        }

        if (!payloadTask.IsCompleted || _shutdown.IsCancellationRequested)
        {
            // Graceful host shutdown while the payload was still running: the job gets the same
            // deterministic verdict a crash recovery would assign, and is never re-run silently.
            Settle(job, JobStatusStates.Disconnected, JobQueueDiagnosticCodes.DisconnectedRecovery);
            return;
        }

        var failureCode = payloadTask.Exception?.InnerException switch
        {
            JobQueueException queueException =>
                string.Equals(queueException.Code, JobQueueDiagnosticCodes.StoreUnavailable, StringComparison.Ordinal)
                    ? JobQueueDiagnosticCodes.EventLogWriteFailed
                    : queueException.Code,
            OperationCanceledException => JobQueueDiagnosticCodes.ExecutionFailed,
            _ => JobQueueDiagnosticCodes.ExecutionFailed,
        };
        Settle(job, JobStatusStates.Failed, failureCode);
    }

    private void Settle(JobRecord job, string terminalState, string? diagnosticCode)
    {
        JobRecord settled;
        try
        {
            settled = _store.Complete(job.JobId, terminalState, diagnosticCode);
        }
        catch (JobQueueException)
        {
            try
            {
                settled = _store.Complete(
                    job.JobId,
                    JobStatusStates.Failed,
                    JobQueueDiagnosticCodes.EventLogWriteFailed);
            }
            catch (JobQueueException)
            {
                // The store refused both verdicts (for example a cancel race already settled the
                // job, or storage is gone). The queue keeps moving; recovery settles stragglers.
                _store.DeleteTemporaryDirectory(job.JobId);
                return;
            }
        }

        _store.DeleteTemporaryDirectory(job.JobId);
        if (string.Equals(settled.State, JobStatusStates.Failed, StringComparison.Ordinal) &&
            string.Equals(settled.BatchPolicy, JobBatchPolicies.Abort, StringComparison.Ordinal) &&
            settled.BatchId is not null)
        {
            try
            {
                _store.CancelBatchRemainder(settled.BatchId, settled.JobId);
            }
            catch (JobQueueException)
            {
                // Abort propagation is retried implicitly: the remaining jobs will fail the same
                // way when executed, so a storage hiccup here must not stall the loop.
            }
        }
    }

    private bool ReadCancelRequested(string jobId)
    {
        try
        {
            return _store.GetJob(jobId)?.CancelRequested == true;
        }
        catch (JobQueueException)
        {
            return false;
        }
    }

    private void TerminateRegisteredChildProcess(string jobId)
    {
        JobRecord? current;
        try
        {
            current = _store.GetJob(jobId);
        }
        catch (JobQueueException)
        {
            return;
        }

        if (current?.ChildProcessId is int processId &&
            current.ChildProcessStartUtc is DateTimeOffset startUtc)
        {
            _processInspector.TerminateExact(processId, startUtc);
        }
    }

    private void TrySetQueueState(string queueState)
    {
        try
        {
            _store.SetQueueState(queueState);
        }
        catch (JobQueueException)
        {
            // Queue-level state is observability data; it must never stop execution.
        }
    }

    private async Task DelaySafeAsync(TimeSpan delay)
    {
        try
        {
            await Task.Delay(delay, _shutdown.Token);
        }
        catch (OperationCanceledException)
        {
            // Shutdown wakes the loop; the outer while observes the token.
        }
    }

    private static void ObserveAbandonedFaults(Task task) =>
        _ = task.ContinueWith(
            static abandoned => _ = abandoned.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);
}

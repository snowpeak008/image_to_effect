using System.Text;
using VFXComposer.Protocol.Jobs;

namespace VFXComposer.Jobs;

/// <summary>
/// Durable current-user job store: one versioned snapshot (atomic replace with <c>.bak</c>
/// recovery) plus one append-only JSONL event log. Every observation and mutation runs under
/// the cross-process store lock. A corrupt primary is recovered from the backup; when both are
/// corrupt the store fails closed with a stable error instead of silently rebuilding.
/// </summary>
public sealed class JobStore : IJobQueueClient
{
    private readonly string _snapshotPath;
    private readonly string _backupPath;
    private readonly string _eventLogPath;
    private readonly JobStoreRevisionLock _revisionLock;
    private readonly JobStoreOptions _options;

    public JobStore(string storeDirectory, JobStoreOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storeDirectory);
        StoreDirectory = Path.GetFullPath(storeDirectory);
        _snapshotPath = Path.Combine(StoreDirectory, "job-store.json");
        _backupPath = _snapshotPath + ".bak";
        _eventLogPath = Path.Combine(StoreDirectory, "job-events.jsonl");
        _revisionLock = new JobStoreRevisionLock(_snapshotPath);
        _options = options ?? JobStoreOptions.Default;
    }

    /// <summary>Store root under the current user's application data; never inside a Unity project.</summary>
    public string StoreDirectory { get; }

    internal string ExecutorLockPath => Path.Combine(StoreDirectory, "executor.lock");

    internal string TemporaryRootPath => Path.Combine(StoreDirectory, "temp");

    public override string ToString() => "JobStore(<redacted>)";

    public JobRecord Enqueue(JobEnqueueRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Execute(() =>
        {
            var snapshot = LoadOrCreate();
            var pending = snapshot.Jobs.Count(job =>
                string.Equals(job.State, JobStatusStates.Queued, StringComparison.Ordinal));
            if (pending >= _options.MaximumPendingJobs)
            {
                throw new JobQueueException(JobQueueDiagnosticCodes.QueueFull);
            }

            var now = DateTimeOffset.UtcNow;
            var record = new JobRecord(
                jobId: "job-" + Guid.NewGuid().ToString("N"),
                requestId: "req-" + Guid.NewGuid().ToString("N"),
                idempotencyKey: "idk-" + Guid.NewGuid().ToString("N"),
                entryIdempotencyKey: request.EntryIdempotencyKey,
                batchId: request.BatchId,
                batchPolicy: request.BatchPolicy,
                itemId: request.ItemId,
                sourceEntry: request.SourceEntry,
                jobKind: request.JobKind,
                payload: request.Payload,
                queuePosition: snapshot.NextQueuePosition,
                enqueuedAtUtc: now,
                startedAtUtc: null,
                completedAtUtc: null,
                state: JobStatusStates.Queued,
                cancelRequested: false,
                lastEventSequence: 1,
                lastProgressPermille: 0,
                finalDiagnosticCode: null,
                artifactIds: Array.Empty<string>(),
                childProcessId: null,
                childProcessStartUtc: null);
            AppendEvents([CreateStatusEvent(record, now)]);
            WriteSnapshot(new JobStoreSnapshot(
                JobStoreSnapshot.CurrentSchema,
                snapshot.QueueState,
                snapshot.NextQueuePosition + 1,
                [.. snapshot.Jobs, record]));
            return record;
        });
    }

    public JobCancellationResult RequestCancel(string jobId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jobId);
        return Execute(() =>
        {
            var snapshot = LoadOrCreate();
            var job = RequireJob(snapshot, jobId);
            if (job.IsTerminal)
            {
                return new JobCancellationResult(job.State, Accepted: false);
            }

            var now = DateTimeOffset.UtcNow;
            if (string.Equals(job.State, JobStatusStates.Queued, StringComparison.Ordinal))
            {
                var settled = job.Completed(JobStatusStates.Cancelled, JobQueueDiagnosticCodes.CancelledQueued, now);
                AppendEvents([CreateCompletionEvent(settled, now)]);
                WriteSnapshot(ReplaceJob(snapshot, settled));
                return new JobCancellationResult(settled.State, Accepted: true);
            }

            if (job.CancelRequested)
            {
                return new JobCancellationResult(job.State, Accepted: true);
            }

            var marked = job.WithCancelRequested();
            AppendEvents([CreateProgressEvent(marked, now)]);
            WriteSnapshot(ReplaceJob(snapshot, marked));
            return new JobCancellationResult(marked.State, Accepted: true);
        });
    }

    public JobRecord Resubmit(string jobId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jobId);
        return Execute(() =>
        {
            var snapshot = LoadOrCreate();
            var original = RequireJob(snapshot, jobId);
            if (!original.IsTerminal)
            {
                throw new JobQueueException(JobQueueDiagnosticCodes.InvalidTransition);
            }

            var pending = snapshot.Jobs.Count(job =>
                string.Equals(job.State, JobStatusStates.Queued, StringComparison.Ordinal));
            if (pending >= _options.MaximumPendingJobs)
            {
                throw new JobQueueException(JobQueueDiagnosticCodes.QueueFull);
            }

            var now = DateTimeOffset.UtcNow;
            var record = new JobRecord(
                jobId: "job-" + Guid.NewGuid().ToString("N"),
                requestId: "req-" + Guid.NewGuid().ToString("N"),
                idempotencyKey: "idk-" + Guid.NewGuid().ToString("N"),
                entryIdempotencyKey: original.EntryIdempotencyKey,
                batchId: original.BatchId,
                batchPolicy: original.BatchPolicy,
                itemId: original.ItemId,
                sourceEntry: original.SourceEntry,
                jobKind: original.JobKind,
                payload: original.Payload,
                queuePosition: snapshot.NextQueuePosition,
                enqueuedAtUtc: now,
                startedAtUtc: null,
                completedAtUtc: null,
                state: JobStatusStates.Queued,
                cancelRequested: false,
                lastEventSequence: 1,
                lastProgressPermille: 0,
                finalDiagnosticCode: null,
                artifactIds: Array.Empty<string>(),
                childProcessId: null,
                childProcessStartUtc: null);
            AppendEvents([CreateStatusEvent(record, now)]);
            WriteSnapshot(new JobStoreSnapshot(
                JobStoreSnapshot.CurrentSchema,
                snapshot.QueueState,
                snapshot.NextQueuePosition + 1,
                [.. snapshot.Jobs, record]));
            return record;
        });
    }

    public JobQueueSnapshotView ReadSnapshot() =>
        Execute(() =>
        {
            var snapshot = LoadOrCreate();
            var ordered = snapshot.Jobs.OrderBy(job => job.QueuePosition).ToArray();
            return new JobQueueSnapshotView(snapshot.QueueState, ordered);
        });

    public IReadOnlyList<JobStoreEvent> ReadEvents(string jobId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jobId);
        return Execute(() =>
            ReadAllEvents()
                .Where(storeEvent => string.Equals(storeEvent.JobId, jobId, StringComparison.Ordinal))
                .OrderBy(storeEvent => storeEvent.EventSequence)
                .ToArray());
    }

    internal JobRecord? GetJob(string jobId) =>
        Execute(() =>
        {
            var snapshot = LoadOrCreate();
            return snapshot.Jobs.FirstOrDefault(job =>
                string.Equals(job.JobId, jobId, StringComparison.Ordinal));
        });

    internal JobRecord? PeekNextQueued() =>
        Execute(() => SelectNextQueued(LoadOrCreate()));

    /// <summary>
    /// Claims the FIFO head for execution. Returns null when the job was cancelled or overtaken
    /// between observation and claim; the caller simply re-reads the queue.
    /// </summary>
    internal JobRecord? TryClaim(string jobId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jobId);
        return Execute(() =>
        {
            var snapshot = LoadOrCreate();
            var head = SelectNextQueued(snapshot);
            if (head is null || !string.Equals(head.JobId, jobId, StringComparison.Ordinal))
            {
                return null;
            }

            var now = DateTimeOffset.UtcNow;
            var claimed = head.Claimed(now);
            AppendEvents([CreateProgressEvent(claimed, now)]);
            WriteSnapshot(ReplaceJob(snapshot, claimed));
            return claimed;
        });
    }

    internal JobRecord ReportProgress(string jobId, int progressPermille) =>
        MutateRunningJob(jobId, job => job.WithProgress(progressPermille), CreateProgressEvent);

    internal JobRecord AppendLog(string jobId, string level, string diagnosticCode) =>
        MutateRunningJob(
            jobId,
            job => job.WithNextEventSequence(),
            (job, now) => new JobStoreEvent(
                JobStoreEvent.CurrentSchema,
                job.JobId,
                job.LastEventSequence,
                JobStoreEventKinds.Log,
                now,
                state: null,
                progressPermille: null,
                level: level,
                diagnosticCode: diagnosticCode,
                outcome: null,
                artifactId: null));

    internal JobRecord AppendArtifact(string jobId, string artifactId) =>
        MutateRunningJob(
            jobId,
            job => job.WithArtifact(artifactId),
            (job, now) => new JobStoreEvent(
                JobStoreEvent.CurrentSchema,
                job.JobId,
                job.LastEventSequence,
                JobStoreEventKinds.Artifact,
                now,
                state: null,
                progressPermille: null,
                level: null,
                diagnosticCode: null,
                outcome: null,
                artifactId: artifactId));

    internal JobRecord RegisterChildProcess(string jobId, int processId, DateTimeOffset processStartUtc) =>
        MutateRunningJob(jobId, job => job.WithChildProcess(processId, processStartUtc), eventFactory: null);

    internal JobRecord ClearChildProcess(string jobId) =>
        MutateRunningJob(jobId, job => job.WithoutChildProcess(), eventFactory: null);

    internal JobRecord Complete(string jobId, string terminalState, string? diagnosticCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jobId);
        return Execute(() =>
        {
            var snapshot = LoadOrCreate();
            var job = RequireJob(snapshot, jobId);
            var now = DateTimeOffset.UtcNow;
            var settled = job.Completed(terminalState, diagnosticCode, now);
            AppendEvents([CreateCompletionEvent(settled, now)]);
            WriteSnapshot(ReplaceJob(snapshot, settled));
            return settled;
        });
    }

    internal void SetQueueState(string queueState)
    {
        JobQueueStates.Require(queueState, nameof(queueState));
        Execute(() =>
        {
            var snapshot = LoadOrCreate();
            if (!string.Equals(snapshot.QueueState, queueState, StringComparison.Ordinal))
            {
                WriteSnapshot(new JobStoreSnapshot(
                    JobStoreSnapshot.CurrentSchema,
                    queueState,
                    snapshot.NextQueuePosition,
                    snapshot.Jobs));
            }

            return 0;
        });
    }

    /// <summary>
    /// Crash-recovery pass (REQ-003 §7.2): every previously RUNNING job is settled as
    /// DISCONNECTED with the recovery diagnostic and is never re-run automatically; QUEUED jobs
    /// are preserved in order. Recorded child processes are returned to the host for exact
    /// PID + start-time disposal.
    /// </summary>
    internal JobStoreRecoveryResult RecoverOnStartup()
    {
        var result = Execute(() =>
        {
            var snapshot = LoadOrCreate();
            var orphans = new List<JobOrphanProcess>();
            var recovered = new List<string>();
            var events = new List<JobStoreEvent>();
            var jobs = new List<JobRecord>(snapshot.Jobs.Count);
            var now = DateTimeOffset.UtcNow;
            foreach (var job in snapshot.Jobs.OrderBy(record => record.QueuePosition))
            {
                if (!string.Equals(job.State, JobStatusStates.Running, StringComparison.Ordinal))
                {
                    jobs.Add(job);
                    continue;
                }

                if (job.ChildProcessId is int processId && job.ChildProcessStartUtc is DateTimeOffset startUtc)
                {
                    orphans.Add(new JobOrphanProcess(job.JobId, processId, startUtc));
                }

                var settled = job.Completed(
                    JobStatusStates.Disconnected,
                    JobQueueDiagnosticCodes.DisconnectedRecovery,
                    now);
                events.Add(CreateCompletionEvent(settled, now));
                recovered.Add(settled.JobId);
                jobs.Add(settled);
            }

            if (events.Count > 0)
            {
                AppendEvents(events);
            }

            WriteSnapshot(new JobStoreSnapshot(
                JobStoreSnapshot.CurrentSchema,
                JobQueueStates.Idle,
                snapshot.NextQueuePosition,
                jobs));
            return new JobStoreRecoveryResult(recovered, orphans);
        });
        CleanupTemporaryDirectories(result.RecoveredJobIds);
        return result;
    }

    /// <summary>Applies the terminal-job retention policy; non-terminal jobs are never touched.</summary>
    internal int CleanupTerminalJobs()
    {
        return Execute(() =>
        {
            var snapshot = LoadOrCreate();
            var now = DateTimeOffset.UtcNow;
            var terminal = snapshot.Jobs
                .Where(job => job.IsTerminal)
                .OrderByDescending(job => job.CompletedAtUtc)
                .ToArray();
            var keep = terminal
                .Take(_options.MaximumTerminalJobs)
                .Where(job => now - job.CompletedAtUtc!.Value <= _options.TerminalRetention)
                .Select(job => job.JobId)
                .ToHashSet(StringComparer.Ordinal);
            var removed = terminal.Length - keep.Count;
            if (removed == 0)
            {
                return 0;
            }

            var survivors = snapshot.Jobs
                .Where(job => !job.IsTerminal || keep.Contains(job.JobId))
                .ToArray();
            var survivorIds = survivors.Select(job => job.JobId).ToHashSet(StringComparer.Ordinal);
            RewriteEvents(survivorIds);
            WriteSnapshot(new JobStoreSnapshot(
                JobStoreSnapshot.CurrentSchema,
                snapshot.QueueState,
                snapshot.NextQueuePosition,
                survivors));
            return removed;
        });
    }

    /// <summary>Settles every remaining QUEUED job of an aborted batch (REQ-003-17).</summary>
    internal int CancelBatchRemainder(string batchId, string excludedJobId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(batchId);
        return Execute(() =>
        {
            var snapshot = LoadOrCreate();
            var now = DateTimeOffset.UtcNow;
            var events = new List<JobStoreEvent>();
            var jobs = new List<JobRecord>(snapshot.Jobs.Count);
            var cancelled = 0;
            foreach (var job in snapshot.Jobs)
            {
                var isRemainder =
                    string.Equals(job.State, JobStatusStates.Queued, StringComparison.Ordinal) &&
                    string.Equals(job.BatchId, batchId, StringComparison.Ordinal) &&
                    !string.Equals(job.JobId, excludedJobId, StringComparison.Ordinal);
                if (!isRemainder)
                {
                    jobs.Add(job);
                    continue;
                }

                var settled = job.Completed(JobStatusStates.Cancelled, JobQueueDiagnosticCodes.BatchAborted, now);
                events.Add(CreateCompletionEvent(settled, now));
                jobs.Add(settled);
                cancelled++;
            }

            if (cancelled > 0)
            {
                AppendEvents(events);
                WriteSnapshot(new JobStoreSnapshot(
                    JobStoreSnapshot.CurrentSchema,
                    snapshot.QueueState,
                    snapshot.NextQueuePosition,
                    jobs));
            }

            return cancelled;
        });
    }

    internal string GetTemporaryDirectory(string jobId) =>
        Path.Combine(TemporaryRootPath, JobsGuard.Token(jobId, nameof(jobId)));

    internal void DeleteTemporaryDirectory(string jobId)
    {
        var directory = GetTemporaryDirectory(jobId);
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch (IOException)
        {
            // Temp cleanup is best-effort; the next recovery pass retries.
        }
        catch (UnauthorizedAccessException)
        {
            // Same as above: never block queue progress on temp cleanup.
        }
    }

    private void CleanupTemporaryDirectories(IReadOnlyList<string> jobIds)
    {
        foreach (var jobId in jobIds)
        {
            DeleteTemporaryDirectory(jobId);
        }
    }

    private JobRecord MutateRunningJob(
        string jobId,
        Func<JobRecord, JobRecord> mutation,
        Func<JobRecord, DateTimeOffset, JobStoreEvent>? eventFactory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jobId);
        return Execute(() =>
        {
            var snapshot = LoadOrCreate();
            var job = RequireJob(snapshot, jobId);
            var mutated = mutation(job);
            if (eventFactory is not null)
            {
                var now = DateTimeOffset.UtcNow;
                AppendEvents([eventFactory(mutated, now)]);
            }

            WriteSnapshot(ReplaceJob(snapshot, mutated));
            return mutated;
        });
    }

    private static JobRecord? SelectNextQueued(JobStoreSnapshot snapshot) =>
        snapshot.Jobs
            .Where(job => string.Equals(job.State, JobStatusStates.Queued, StringComparison.Ordinal))
            .OrderBy(job => job.QueuePosition)
            .FirstOrDefault();

    private static JobRecord RequireJob(JobStoreSnapshot snapshot, string jobId) =>
        snapshot.Jobs.FirstOrDefault(job => string.Equals(job.JobId, jobId, StringComparison.Ordinal))
            ?? throw new JobQueueException(JobQueueDiagnosticCodes.JobNotFound);

    private static JobStoreSnapshot ReplaceJob(JobStoreSnapshot snapshot, JobRecord replacement) =>
        new(
            JobStoreSnapshot.CurrentSchema,
            snapshot.QueueState,
            snapshot.NextQueuePosition,
            snapshot.Jobs
                .Select(job => string.Equals(job.JobId, replacement.JobId, StringComparison.Ordinal) ? replacement : job)
                .ToArray());

    private static JobStoreEvent CreateStatusEvent(JobRecord job, DateTimeOffset now) =>
        new(
            JobStoreEvent.CurrentSchema,
            job.JobId,
            job.LastEventSequence,
            JobStoreEventKinds.Status,
            now,
            state: job.State,
            progressPermille: null,
            level: null,
            diagnosticCode: null,
            outcome: null,
            artifactId: null);

    private static JobStoreEvent CreateProgressEvent(JobRecord job, DateTimeOffset now) =>
        new(
            JobStoreEvent.CurrentSchema,
            job.JobId,
            job.LastEventSequence,
            JobStoreEventKinds.Progress,
            now,
            state: job.CancelRequested ? JobProgressStates.CancellationRequested : JobProgressStates.Running,
            progressPermille: job.LastProgressPermille,
            level: null,
            diagnosticCode: null,
            outcome: null,
            artifactId: null);

    private static JobStoreEvent CreateCompletionEvent(JobRecord job, DateTimeOffset now) =>
        new(
            JobStoreEvent.CurrentSchema,
            job.JobId,
            job.LastEventSequence,
            JobStoreEventKinds.Completion,
            now,
            state: null,
            progressPermille: null,
            level: null,
            diagnosticCode: job.FinalDiagnosticCode,
            outcome: job.State,
            artifactId: null);

    private T Execute<T>(Func<T> operation)
    {
        try
        {
            return _revisionLock.Execute(operation);
        }
        catch (JobQueueException)
        {
            throw;
        }
        catch (IOException exception)
        {
            throw new JobQueueException(JobQueueDiagnosticCodes.StoreUnavailable, exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new JobQueueException(JobQueueDiagnosticCodes.StoreUnavailable, exception);
        }
    }

    private JobStoreSnapshot LoadOrCreate()
    {
        var primaryExists = File.Exists(_snapshotPath);
        var backupExists = File.Exists(_backupPath);
        if (!primaryExists && !backupExists)
        {
            return JobStoreSnapshot.CreateEmpty();
        }

        if (primaryExists && TryRead(_snapshotPath, out var primary))
        {
            return primary!;
        }

        if (backupExists && TryRead(_backupPath, out var backup))
        {
            JobStoreFileWriter.RestorePrimaryPreservingBackup(
                _snapshotPath,
                JobStoreCodec.SerializeSnapshot(backup!));
            return backup!;
        }

        throw new JobQueueException(JobQueueDiagnosticCodes.StoreUnavailable);
    }

    private void WriteSnapshot(JobStoreSnapshot snapshot) =>
        JobStoreFileWriter.WriteReplace(_snapshotPath, _backupPath, JobStoreCodec.SerializeSnapshot(snapshot));

    private void AppendEvents(IReadOnlyList<JobStoreEvent> events)
    {
        var builder = new StringBuilder();
        foreach (var storeEvent in events)
        {
            builder.Append(JobStoreCodec.SerializeEventLine(storeEvent)).Append('\n');
        }

        Directory.CreateDirectory(StoreDirectory);
        using var stream = new FileStream(
            _eventLogPath,
            FileMode.Append,
            FileAccess.Write,
            FileShare.Read,
            bufferSize: 4096,
            FileOptions.WriteThrough);
        var bytes = Encoding.UTF8.GetBytes(builder.ToString());
        stream.Write(bytes);
        stream.Flush(flushToDisk: true);
    }

    private IReadOnlyList<JobStoreEvent> ReadAllEvents()
    {
        if (!File.Exists(_eventLogPath))
        {
            return Array.Empty<JobStoreEvent>();
        }

        var lines = File.ReadAllLines(_eventLogPath);
        var events = new List<JobStoreEvent>(lines.Length);
        for (var index = 0; index < lines.Length; index++)
        {
            if (string.IsNullOrWhiteSpace(lines[index]))
            {
                continue;
            }

            try
            {
                events.Add(JobStoreCodec.DeserializeEventLine(lines[index]));
            }
            catch (JobQueueException) when (index == lines.Length - 1)
            {
                // A torn final line is possible after a crash mid-append; the snapshot remains
                // authoritative. Any earlier malformed line still fails closed above.
            }
        }

        return events;
    }

    private void RewriteEvents(IReadOnlySet<string> survivingJobIds)
    {
        var surviving = ReadAllEvents()
            .Where(storeEvent => survivingJobIds.Contains(storeEvent.JobId))
            .ToArray();
        var builder = new StringBuilder();
        foreach (var storeEvent in surviving)
        {
            builder.Append(JobStoreCodec.SerializeEventLine(storeEvent)).Append('\n');
        }

        JobStoreFileWriter.RestorePrimaryPreservingBackup(
            _eventLogPath,
            Encoding.UTF8.GetBytes(builder.ToString()));
    }

    private static bool TryRead(string path, out JobStoreSnapshot? snapshot)
    {
        snapshot = null;
        try
        {
            var bytes = JobStoreFileWriter.ReadBounded(path, JobStoreCodec.MaximumSnapshotBytes);
            snapshot = JobStoreCodec.DeserializeSnapshot(bytes);
            return true;
        }
        catch (JobQueueException exception)
            when (!string.Equals(exception.Code, JobQueueDiagnosticCodes.StoreVersionUnsupported, StringComparison.Ordinal))
        {
            // An unsupported schema version is not treated as corruption: it must fail closed
            // instead of silently migrating or falling through to backup recovery.
            return false;
        }
        catch (IOException)
        {
            return false;
        }
        catch (InvalidDataException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}

/// <summary>Recorded child process of a crashed RUNNING job, for exact PID + start-time disposal.</summary>
public sealed record JobOrphanProcess(string JobId, int ProcessId, DateTimeOffset ProcessStartUtc);

/// <summary>Outcome of the crash-recovery pass.</summary>
public sealed record JobStoreRecoveryResult(
    IReadOnlyList<string> RecoveredJobIds,
    IReadOnlyList<JobOrphanProcess> OrphanProcesses);

using System.Text.Json.Serialization;
using VFXComposer.Protocol.Jobs;

namespace VFXComposer.Jobs;

/// <summary>
/// One persisted queue entry. States are exactly the Protocol <see cref="JobStatusStates"/>
/// closed set; every transition is validated here so an invalid store mutation cannot be
/// constructed. This is a versioned store record, not a registered wire DTO.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record JobRecord
{
    public const int MaximumPayloadLength = 65_536;
    public const int MaximumArtifactCount = 64;

    [JsonConstructor]
    public JobRecord(
        string jobId,
        string requestId,
        string idempotencyKey,
        string entryIdempotencyKey,
        string? batchId,
        string? batchPolicy,
        string sourceEntry,
        string jobKind,
        string payload,
        long queuePosition,
        DateTimeOffset enqueuedAtUtc,
        DateTimeOffset? startedAtUtc,
        DateTimeOffset? completedAtUtc,
        string state,
        bool cancelRequested,
        long lastEventSequence,
        int lastProgressPermille,
        string? finalDiagnosticCode,
        IReadOnlyList<string> artifactIds,
        int? childProcessId,
        DateTimeOffset? childProcessStartUtc)
    {
        JobId = JobsGuard.Token(jobId, nameof(jobId));
        RequestId = JobsGuard.Token(requestId, nameof(requestId));
        IdempotencyKey = JobsGuard.Token(idempotencyKey, nameof(idempotencyKey));
        EntryIdempotencyKey = JobsGuard.Token(entryIdempotencyKey, nameof(entryIdempotencyKey));
        var identityTokens = new[] { JobId, RequestId, IdempotencyKey };
        if (identityTokens.Distinct(StringComparer.Ordinal).Count() != identityTokens.Length)
        {
            throw new ArgumentException("jobId, requestId and idempotencyKey must be pairwise distinct.");
        }

        BatchId = batchId is null ? null : JobsGuard.Token(batchId, nameof(batchId));
        if ((batchId is null) != (batchPolicy is null))
        {
            throw new ArgumentException("batchPolicy must be present exactly when batchId is present.", nameof(batchPolicy));
        }

        BatchPolicy = batchPolicy is null ? null : JobBatchPolicies.Require(batchPolicy, nameof(batchPolicy));
        SourceEntry = JobSourceEntries.Require(sourceEntry, nameof(sourceEntry));
        JobKind = JobsGuard.Token(jobKind, nameof(jobKind));
        ArgumentNullException.ThrowIfNull(payload);
        if (payload.Length > MaximumPayloadLength)
        {
            throw new ArgumentOutOfRangeException(nameof(payload));
        }

        Payload = payload;
        if (queuePosition < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(queuePosition));
        }

        QueuePosition = queuePosition;
        EnqueuedAtUtc = JobsGuard.Utc(enqueuedAtUtc, nameof(enqueuedAtUtc));
        StartedAtUtc = startedAtUtc is null ? null : JobsGuard.Utc(startedAtUtc.Value, nameof(startedAtUtc));
        CompletedAtUtc = completedAtUtc is null ? null : JobsGuard.Utc(completedAtUtc.Value, nameof(completedAtUtc));
        State = JobStatusStateSet.Require(state, nameof(state));
        CancelRequested = cancelRequested;
        if (lastEventSequence < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(lastEventSequence));
        }

        LastEventSequence = lastEventSequence;
        if (lastProgressPermille is < 0 or > JobProgress.MaximumPermille)
        {
            throw new ArgumentOutOfRangeException(nameof(lastProgressPermille));
        }

        LastProgressPermille = lastProgressPermille;
        FinalDiagnosticCode = finalDiagnosticCode is null
            ? null
            : JobQueueDiagnosticCatalog.Require(finalDiagnosticCode).Code;
        ArgumentNullException.ThrowIfNull(artifactIds);
        if (artifactIds.Count > MaximumArtifactCount)
        {
            throw new ArgumentOutOfRangeException(nameof(artifactIds));
        }

        ArtifactIds = artifactIds.Select(id => JobsGuard.Token(id, nameof(artifactIds))).ToArray();
        if ((childProcessId is null) != (childProcessStartUtc is null))
        {
            throw new ArgumentException("Child process id and start time must be recorded together.", nameof(childProcessId));
        }

        if (childProcessId is < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(childProcessId));
        }

        ChildProcessId = childProcessId;
        ChildProcessStartUtc = childProcessStartUtc is null
            ? null
            : JobsGuard.Utc(childProcessStartUtc.Value, nameof(childProcessStartUtc));
        RequireShape();
    }

    [JsonPropertyName("jobId")] public string JobId { get; }
    [JsonPropertyName("requestId")] public string RequestId { get; }
    [JsonPropertyName("idempotencyKey")] public string IdempotencyKey { get; }

    /// <summary>Content-derived key that stays stable across resubmissions (REQ-003 §7.1).</summary>
    [JsonPropertyName("entryIdempotencyKey")] public string EntryIdempotencyKey { get; }

    [JsonPropertyName("batchId")] public string? BatchId { get; }
    [JsonPropertyName("batchPolicy")] public string? BatchPolicy { get; }
    [JsonPropertyName("sourceEntry")] public string SourceEntry { get; }
    [JsonPropertyName("jobKind")] public string JobKind { get; }

    /// <summary>Opaque payload for the executor. It never appears in events, diagnostics or UI text.</summary>
    [JsonPropertyName("payload")] public string Payload { get; }

    [JsonPropertyName("queuePosition")] public long QueuePosition { get; }
    [JsonPropertyName("enqueuedAtUtc")] public DateTimeOffset EnqueuedAtUtc { get; }
    [JsonPropertyName("startedAtUtc")] public DateTimeOffset? StartedAtUtc { get; }
    [JsonPropertyName("completedAtUtc")] public DateTimeOffset? CompletedAtUtc { get; }
    [JsonPropertyName("state")] public string State { get; }
    [JsonPropertyName("cancelRequested")] public bool CancelRequested { get; }
    [JsonPropertyName("lastEventSequence")] public long LastEventSequence { get; }
    [JsonPropertyName("lastProgressPermille")] public int LastProgressPermille { get; }
    [JsonPropertyName("finalDiagnosticCode")] public string? FinalDiagnosticCode { get; }
    [JsonPropertyName("artifactIds")] public IReadOnlyList<string> ArtifactIds { get; }
    [JsonPropertyName("childProcessId")] public int? ChildProcessId { get; }
    [JsonPropertyName("childProcessStartUtc")] public DateTimeOffset? ChildProcessStartUtc { get; }

    [JsonIgnore]
    public bool IsTerminal =>
        State is JobStatusStates.Succeeded or JobStatusStates.Failed
            or JobStatusStates.Cancelled or JobStatusStates.Disconnected;

    internal JobRecord Claimed(DateTimeOffset startedAtUtc)
    {
        RequireState(JobStatusStates.Queued);
        return Mutate(
            state: JobStatusStates.Running,
            startedAtUtc: startedAtUtc,
            lastEventSequence: LastEventSequence + 1);
    }

    internal JobRecord WithProgress(int progressPermille)
    {
        RequireState(JobStatusStates.Running);
        if (progressPermille < LastProgressPermille)
        {
            throw new JobQueueException(JobQueueDiagnosticCodes.InvalidTransition);
        }

        return Mutate(lastProgressPermille: progressPermille, lastEventSequence: LastEventSequence + 1);
    }

    internal JobRecord WithNextEventSequence()
    {
        RequireState(JobStatusStates.Running);
        return Mutate(lastEventSequence: LastEventSequence + 1);
    }

    internal JobRecord WithCancelRequested()
    {
        RequireState(JobStatusStates.Running);
        return Mutate(cancelRequested: true, lastEventSequence: LastEventSequence + 1);
    }

    internal JobRecord WithArtifact(string artifactId)
    {
        RequireState(JobStatusStates.Running);
        var artifacts = new List<string>(ArtifactIds) { artifactId };
        return Mutate(artifactIds: artifacts, lastEventSequence: LastEventSequence + 1);
    }

    internal JobRecord WithChildProcess(int processId, DateTimeOffset processStartUtc)
    {
        RequireState(JobStatusStates.Running);
        return Mutate(childProcessId: processId, childProcessStartUtc: processStartUtc, setChildProcess: true);
    }

    internal JobRecord WithoutChildProcess()
    {
        RequireState(JobStatusStates.Running);
        return Mutate(childProcessId: null, childProcessStartUtc: null, setChildProcess: true);
    }

    internal JobRecord Completed(string terminalState, string? diagnosticCode, DateTimeOffset completedAtUtc)
    {
        if (IsTerminal)
        {
            throw new JobQueueException(JobQueueDiagnosticCodes.InvalidTransition);
        }

        var allowed = terminalState switch
        {
            JobStatusStates.Cancelled => true,
            JobStatusStates.Succeeded or JobStatusStates.Failed or JobStatusStates.Disconnected =>
                string.Equals(State, JobStatusStates.Running, StringComparison.Ordinal),
            _ => false,
        };
        if (!allowed)
        {
            throw new JobQueueException(JobQueueDiagnosticCodes.InvalidTransition);
        }

        var permille = string.Equals(terminalState, JobStatusStates.Succeeded, StringComparison.Ordinal)
            ? JobProgress.MaximumPermille
            : LastProgressPermille;
        return Mutate(
            state: terminalState,
            completedAtUtc: completedAtUtc,
            finalDiagnosticCode: diagnosticCode,
            setFinalDiagnosticCode: true,
            lastProgressPermille: permille,
            lastEventSequence: LastEventSequence + 1,
            cancelRequested: false,
            childProcessId: null,
            childProcessStartUtc: null,
            setChildProcess: true);
    }

    private void RequireState(string expected)
    {
        if (!string.Equals(State, expected, StringComparison.Ordinal))
        {
            throw new JobQueueException(JobQueueDiagnosticCodes.InvalidTransition);
        }
    }

    private void RequireShape()
    {
        var succeeded = string.Equals(State, JobStatusStates.Succeeded, StringComparison.Ordinal);
        if (IsTerminal)
        {
            if (CompletedAtUtc is null)
            {
                throw new ArgumentException("Terminal jobs must record a completion time.");
            }

            if (succeeded != (FinalDiagnosticCode is null))
            {
                throw new ArgumentException("Completion diagnostic shape does not match the outcome vocabulary.");
            }

            if (CancelRequested || ChildProcessId is not null)
            {
                throw new ArgumentException("Terminal jobs must not carry runtime execution markers.");
            }

            return;
        }

        if (CompletedAtUtc is not null || FinalDiagnosticCode is not null)
        {
            throw new ArgumentException("Non-terminal jobs must not carry completion data.");
        }

        if (string.Equals(State, JobStatusStates.Queued, StringComparison.Ordinal) &&
            (StartedAtUtc is not null || CancelRequested || ChildProcessId is not null))
        {
            throw new ArgumentException("Queued jobs must not carry execution markers.");
        }

        if (string.Equals(State, JobStatusStates.Running, StringComparison.Ordinal) && StartedAtUtc is null)
        {
            throw new ArgumentException("Running jobs must record a start time.");
        }
    }

    private JobRecord Mutate(
        string? state = null,
        DateTimeOffset? startedAtUtc = null,
        DateTimeOffset? completedAtUtc = null,
        bool? cancelRequested = null,
        long? lastEventSequence = null,
        int? lastProgressPermille = null,
        string? finalDiagnosticCode = null,
        bool setFinalDiagnosticCode = false,
        IReadOnlyList<string>? artifactIds = null,
        int? childProcessId = null,
        DateTimeOffset? childProcessStartUtc = null,
        bool setChildProcess = false)
    {
        return new JobRecord(
            JobId,
            RequestId,
            IdempotencyKey,
            EntryIdempotencyKey,
            BatchId,
            BatchPolicy,
            SourceEntry,
            JobKind,
            Payload,
            QueuePosition,
            EnqueuedAtUtc,
            startedAtUtc ?? StartedAtUtc,
            completedAtUtc ?? CompletedAtUtc,
            state ?? State,
            cancelRequested ?? CancelRequested,
            lastEventSequence ?? LastEventSequence,
            lastProgressPermille ?? LastProgressPermille,
            setFinalDiagnosticCode ? finalDiagnosticCode : FinalDiagnosticCode,
            artifactIds ?? ArtifactIds,
            setChildProcess ? childProcessId : ChildProcessId,
            setChildProcess ? childProcessStartUtc : ChildProcessStartUtc);
    }
}

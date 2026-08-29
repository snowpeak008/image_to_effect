namespace VFXComposer.Jobs;

/// <summary>
/// Execution-side surface handed to a payload executor: progress, structured logs, artifact
/// identities and child-process registration, all persisted through the store. Log and progress
/// data never contain the payload or any filesystem path.
/// </summary>
public sealed class JobExecutionContext
{
    private readonly JobStore _store;

    internal JobExecutionContext(JobStore store, JobRecord job, string temporaryDirectory)
    {
        _store = store;
        JobId = job.JobId;
        JobKind = job.JobKind;
        Payload = job.Payload;
        TemporaryDirectory = temporaryDirectory;
    }

    public string JobId { get; }

    public string JobKind { get; }

    /// <summary>The opaque payload as submitted. It must never be echoed into logs or diagnostics.</summary>
    public string Payload { get; }

    /// <summary>Job-private scratch directory; deleted on completion, cancellation and recovery.</summary>
    public string TemporaryDirectory { get; }

    /// <summary>Reports a monotonically non-decreasing progress value in 0–1000 permille.</summary>
    public void ReportProgress(int progressPermille) => _store.ReportProgress(JobId, progressPermille);

    /// <summary>Appends a structured log event: a closed level plus a stable jobs-domain code.</summary>
    public void ReportLog(string level, string diagnosticCode) => _store.AppendLog(JobId, level, diagnosticCode);

    /// <summary>Announces one bounded artifact identity (no location, mirroring the wire contract).</summary>
    public void ReportArtifact(string artifactId) => _store.AppendArtifact(JobId, artifactId);

    /// <summary>
    /// Records the exact PID + start time of a spawned child process so cancellation, timeout
    /// and crash recovery can terminate precisely that process and nothing else (REQ-003 §6.4).
    /// </summary>
    public void RegisterChildProcess(int processId, DateTimeOffset processStartUtc) =>
        _store.RegisterChildProcess(JobId, processId, processStartUtc);

    /// <summary>Clears the child-process registration after the child exits normally.</summary>
    public void ClearChildProcess() => _store.ClearChildProcess(JobId);
}

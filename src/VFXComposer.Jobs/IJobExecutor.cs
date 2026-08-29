namespace VFXComposer.Jobs;

/// <summary>
/// Pluggable payload executor for one job kind. F1 generation payloads and F2 build payloads
/// plug in through this interface; the queue itself never interprets payload content.
/// Implementations signal failure by throwing (a <see cref="JobQueueException"/> for a stable
/// code, anything else maps to the generic execution-failure code) and must observe the
/// cancellation token for cooperative cancellation.
/// </summary>
public interface IJobExecutor
{
    /// <summary>The job kind this executor handles; one executor per kind.</summary>
    string JobKind { get; }

    /// <summary>
    /// True when the payload opens the Unity project, so the host must hold off while the
    /// editor owns the project lock (REQ-003 §6.3).
    /// </summary>
    bool RequiresProjectLock { get; }

    /// <summary>Executes one claimed job. Normal return means success.</summary>
    Task ExecuteAsync(JobExecutionContext context, CancellationToken cancellationToken);
}

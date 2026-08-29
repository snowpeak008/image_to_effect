namespace VFXComposer.Jobs;

/// <summary>Read-only queue observation shared by every consumer surface (Jobs page, CLI, MCP).</summary>
public interface IJobQueueReader
{
    /// <summary>Reads the queue-level state and every job record, ordered by queue position.</summary>
    JobQueueSnapshotView ReadSnapshot();

    /// <summary>Reads the persisted event timeline for one job, ordered by event sequence.</summary>
    IReadOnlyList<JobStoreEvent> ReadEvents(string jobId);
}

/// <summary>
/// The one queue client API every entry surface uses. Entry surfaces can enqueue, observe and
/// request cancellation; they never execute jobs themselves.
/// </summary>
public interface IJobQueueClient : IJobQueueReader
{
    /// <summary>Appends one job in FIFO order; rejects with the stable queue-full error when the pending bound is reached.</summary>
    JobRecord Enqueue(JobEnqueueRequest request);

    /// <summary>
    /// Cancels a queued job immediately, requests cooperative cancellation of a running job, and
    /// is an idempotent no-op for a terminal job.
    /// </summary>
    JobCancellationResult RequestCancel(string jobId);

    /// <summary>
    /// Re-enqueues a terminal job as a new job with fresh queue identity tokens. The
    /// content-derived entry idempotency key is preserved and the original record is unchanged.
    /// </summary>
    JobRecord Resubmit(string jobId);
}

/// <summary>Result of one cancellation request: the job's state after the request.</summary>
public sealed record JobCancellationResult(string State, bool Accepted);

/// <summary>Point-in-time view of the queue for observers.</summary>
public sealed record JobQueueSnapshotView(string QueueState, IReadOnlyList<JobRecord> Jobs);

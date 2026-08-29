using VFXComposer.Jobs;

namespace VFXComposer.Batch.Core;

/// <summary>One entry of a batch after its cancellation request was placed.</summary>
public sealed record BatchCancellationItem(string JobId, string State, bool Accepted);

/// <summary>
/// Acceptance summary of one batch-level cancellation request, in queue order. The queue refuses
/// acceptance only for entries it already considers settled, so <see cref="NoOp"/> counts exactly
/// the idempotent terminal entries.
/// </summary>
public sealed record BatchCancellationResult(string BatchId, IReadOnlyList<BatchCancellationItem> Items)
{
    /// <summary>False when no queue entry carries this batch id, which the caller reports as not found.</summary>
    public bool BatchFound => Items.Count > 0;

    public int Requested => Items.Count;

    public int Accepted => Items.Count(static item => item.Accepted);

    /// <summary>Entries the queue treated as already settled.</summary>
    public int NoOp => Items.Count(static item => !item.Accepted);
}

/// <summary>
/// Batch-level cancellation for every entry surface (REQ-002-17, REQ-002 §6.2 errata). It is the
/// one place the batch fan-out lives: the CLI command and the MCP tool both call this method, so
/// neither surface re-implements the enumeration or the per-entry semantics.
/// </summary>
public sealed class BatchCancellationService
{
    private readonly IJobQueueClient _queue;

    public BatchCancellationService(IJobQueueClient queue)
    {
        _queue = queue ?? throw new ArgumentNullException(nameof(queue));
    }

    public override string ToString() => "BatchCancellationService";

    /// <summary>
    /// Requests cancellation of every entry of one batch, in queue order. Per-entry semantics are
    /// delegated to REQ-003 §8.2 as the queue implements them: a QUEUED entry settles as
    /// CANCELLED immediately, a RUNNING entry gets a cooperative cancellation request and stays
    /// running until the executor settles it, and a terminal entry is an idempotent no-op.
    /// </summary>
    public BatchCancellationResult Cancel(string batchId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(batchId);
        var jobs = _queue.ReadSnapshot().Jobs
            .Where(job => string.Equals(job.BatchId, batchId, StringComparison.Ordinal))
            .ToArray();
        var items = new List<BatchCancellationItem>(jobs.Length);
        foreach (var job in jobs)
        {
            items.Add(CancelOne(job));
        }

        return new BatchCancellationResult(batchId, items);
    }

    private BatchCancellationItem CancelOne(JobRecord job)
    {
        try
        {
            var result = _queue.RequestCancel(job.JobId);
            return new BatchCancellationItem(job.JobId, result.State, result.Accepted);
        }
        catch (JobQueueException exception)
            when (string.Equals(exception.Code, JobQueueDiagnosticCodes.JobNotFound, StringComparison.Ordinal))
        {
            // The entry left the store between the snapshot and the request, which the terminal
            // retention policy is allowed to do. A vanished entry can only have been settled, so
            // it is reported as an unaccepted no-op instead of failing the whole batch request.
            return new BatchCancellationItem(job.JobId, job.State, Accepted: false);
        }
    }
}

using VFXComposer.Jobs;

namespace VFXComposer.Batch.Core;

/// <summary>Why foreground tracking stopped.</summary>
public enum BatchTrackingStatus
{
    /// <summary>Every tracked job reached a terminal state.</summary>
    Completed,

    /// <summary>The caller interrupted tracking; enqueued jobs keep running (detach equivalence).</summary>
    Interrupted,

    /// <summary>The queue stayed blocked on the Unity project lock past the caller's bound.</summary>
    ProjectLockTimeout,

    /// <summary>The queue store became unreadable while tracking.</summary>
    StoreUnavailable,
}

/// <summary>Timing policy for foreground tracking.</summary>
public sealed record BatchTrackingOptions
{
    public static BatchTrackingOptions Default { get; } = new();

    public TimeSpan PollInterval { get; init; } = TimeSpan.FromMilliseconds(200);

    /// <summary>How long the queue may stay in <c>WAITING_PROJECT_LOCK</c>; null waits forever.</summary>
    public TimeSpan? ProjectLockTimeout { get; init; }
}

/// <summary>One observed transition of a tracked entry.</summary>
public sealed record BatchTrackingUpdate(string ItemId, JobRecord Job);

/// <summary>Receives transitions as they are observed; implementations only format output.</summary>
public interface IBatchTrackingSink
{
    void OnJobUpdated(BatchTrackingUpdate update);

    /// <summary>Reports that the queue is waiting for the Unity editor to release the project.</summary>
    void OnWaitingProjectLock();
}

/// <summary>Result of one tracking session, including the last observed record per entry.</summary>
public sealed record BatchTrackingResult(
    BatchTrackingStatus Status,
    IReadOnlyDictionary<string, JobRecord> JobsByItemId);

/// <summary>
/// Foreground observation of one batch. Tracking is read-only: it polls the queue snapshot and
/// never claims or mutates a job, so it behaves identically whether this process hosts the
/// executor or another one does.
/// </summary>
public sealed class BatchTracker
{
    private readonly IJobQueueReader _queue;
    private readonly BatchTrackingOptions _options;

    public BatchTracker(IJobQueueReader queue, BatchTrackingOptions? options = null)
    {
        _queue = queue ?? throw new ArgumentNullException(nameof(queue));
        _options = options ?? BatchTrackingOptions.Default;
    }

    public override string ToString() => "BatchTracker";

    public async Task<BatchTrackingResult> TrackAsync(
        IReadOnlyList<BatchSubmissionItem> items,
        IBatchTrackingSink sink,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(sink);
        var itemIdByJobId = items
            .Where(static item => item.JobId is not null)
            .ToDictionary(static item => item.JobId!, static item => item.ItemId, StringComparer.Ordinal);
        var observed = new Dictionary<string, JobRecord>(StringComparer.Ordinal);
        if (itemIdByJobId.Count == 0)
        {
            return new BatchTrackingResult(BatchTrackingStatus.Completed, observed);
        }

        var announcedWaiting = false;
        DateTimeOffset? waitingSince = null;
        while (true)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return new BatchTrackingResult(BatchTrackingStatus.Interrupted, observed);
            }

            JobQueueSnapshotView snapshot;
            try
            {
                snapshot = _queue.ReadSnapshot();
            }
            catch (JobQueueException)
            {
                return new BatchTrackingResult(BatchTrackingStatus.StoreUnavailable, observed);
            }

            var terminal = 0;
            foreach (var job in snapshot.Jobs)
            {
                if (!itemIdByJobId.TryGetValue(job.JobId, out var itemId))
                {
                    continue;
                }

                if (job.IsTerminal)
                {
                    terminal++;
                }

                if (!observed.TryGetValue(itemId, out var previous) || HasChanged(previous, job))
                {
                    observed[itemId] = job;
                    sink.OnJobUpdated(new BatchTrackingUpdate(itemId, job));
                }
            }

            if (terminal == itemIdByJobId.Count)
            {
                return new BatchTrackingResult(BatchTrackingStatus.Completed, observed);
            }

            if (string.Equals(snapshot.QueueState, JobQueueStates.WaitingProjectLock, StringComparison.Ordinal))
            {
                if (!announcedWaiting)
                {
                    announcedWaiting = true;
                    sink.OnWaitingProjectLock();
                }

                waitingSince ??= DateTimeOffset.UtcNow;
                if (_options.ProjectLockTimeout is TimeSpan bound &&
                    DateTimeOffset.UtcNow - waitingSince.Value >= bound)
                {
                    return new BatchTrackingResult(BatchTrackingStatus.ProjectLockTimeout, observed);
                }
            }
            else
            {
                announcedWaiting = false;
                waitingSince = null;
            }

            try
            {
                await Task.Delay(_options.PollInterval, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return new BatchTrackingResult(BatchTrackingStatus.Interrupted, observed);
            }
        }
    }

    private static bool HasChanged(JobRecord previous, JobRecord current) =>
        !string.Equals(previous.State, current.State, StringComparison.Ordinal) ||
        previous.LastProgressPermille != current.LastProgressPermille ||
        previous.CancelRequested != current.CancelRequested;
}

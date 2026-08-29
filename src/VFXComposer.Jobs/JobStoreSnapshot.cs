using System.Text.Json.Serialization;

namespace VFXComposer.Jobs;

/// <summary>
/// Versioned store snapshot: the authoritative current state of the queue. Unknown fields and
/// unknown schema versions are rejected fail-closed; there is no silent migration. Version 2
/// added the persisted <see cref="JobRecord.ItemId"/>; a version 1 file is rejected rather than
/// read as "no item id", because the item id of a version 1 record is unrecoverable.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record JobStoreSnapshot
{
    public const string CurrentSchema = "vfxcomposer.job-store/2";

    [JsonConstructor]
    public JobStoreSnapshot(
        string schema,
        string queueState,
        long nextQueuePosition,
        IReadOnlyList<JobRecord> jobs)
    {
        if (!string.Equals(schema, CurrentSchema, StringComparison.Ordinal))
        {
            throw new JobQueueException(JobQueueDiagnosticCodes.StoreVersionUnsupported);
        }

        Schema = schema;
        QueueState = JobQueueStates.Require(queueState, nameof(queueState));
        if (nextQueuePosition < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(nextQueuePosition));
        }

        NextQueuePosition = nextQueuePosition;
        ArgumentNullException.ThrowIfNull(jobs);
        if (jobs.Select(job => job.JobId).Distinct(StringComparer.Ordinal).Count() != jobs.Count)
        {
            throw new ArgumentException("Job ids must be unique within the store.", nameof(jobs));
        }

        Jobs = jobs.ToArray();
    }

    [JsonPropertyName("schema")] public string Schema { get; }
    [JsonPropertyName("queueState")] public string QueueState { get; }
    [JsonPropertyName("nextQueuePosition")] public long NextQueuePosition { get; }
    [JsonPropertyName("jobs")] public IReadOnlyList<JobRecord> Jobs { get; }

    internal static JobStoreSnapshot CreateEmpty() =>
        new(CurrentSchema, JobQueueStates.Idle, nextQueuePosition: 1, Array.Empty<JobRecord>());
}

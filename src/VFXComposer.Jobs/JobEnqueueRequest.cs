namespace VFXComposer.Jobs;

/// <summary>
/// One submission from an entry surface. The payload is opaque queue data interpreted only by
/// the payload executor for <see cref="JobKind"/>; it is persisted in the store but never
/// copied into events, diagnostics or UI text.
/// </summary>
public sealed record JobEnqueueRequest
{
    public JobEnqueueRequest(
        string sourceEntry,
        string jobKind,
        string payload,
        string? batchId = null,
        string? batchPolicy = null,
        string? itemId = null,
        string? entryIdempotencyKey = null)
    {
        SourceEntry = JobSourceEntries.Require(sourceEntry, nameof(sourceEntry));
        JobKind = JobsGuard.Token(jobKind, nameof(jobKind));
        ArgumentNullException.ThrowIfNull(payload);
        if (payload.Length > JobRecord.MaximumPayloadLength)
        {
            throw new ArgumentOutOfRangeException(nameof(payload));
        }

        Payload = payload;
        BatchId = batchId is null ? null : JobsGuard.Token(batchId, nameof(batchId));
        if ((batchId is null) != (batchPolicy is null))
        {
            throw new ArgumentException("batchPolicy must be present exactly when batchId is present.", nameof(batchPolicy));
        }

        BatchPolicy = batchPolicy is null ? null : JobBatchPolicies.Require(batchPolicy, nameof(batchPolicy));
        ItemId = itemId is null ? null : JobsGuard.Token(itemId, nameof(itemId));
        EntryIdempotencyKey = entryIdempotencyKey is null
            ? JobEntryIdempotency.Derive(BatchId, ItemId, JobKind + "\n" + Payload)
            : JobsGuard.Token(entryIdempotencyKey, nameof(entryIdempotencyKey));
    }

    public string SourceEntry { get; }
    public string JobKind { get; }
    public string Payload { get; }
    public string? BatchId { get; }
    public string? BatchPolicy { get; }
    public string? ItemId { get; }

    /// <summary>Content-derived key; stable across resubmissions of the same entry.</summary>
    public string EntryIdempotencyKey { get; }
}

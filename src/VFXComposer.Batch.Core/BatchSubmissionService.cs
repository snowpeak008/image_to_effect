using VFXComposer.Jobs;
using VFXComposer.Protocol.Jobs;

namespace VFXComposer.Batch.Core;

/// <summary>What happened to one manifest entry at submission time.</summary>
public static class BatchItemDispositions
{
    public const string Enqueued = "ENQUEUED";

    /// <summary>Report-layer outcome for an entry whose content already completed successfully.</summary>
    public const string SkippedIdempotent = "SKIPPED_IDEMPOTENT";
}

/// <summary>One entry after submission: its stable content key and, when enqueued, its job id.</summary>
public sealed record BatchSubmissionItem(
    string ItemId,
    string EntryIdempotencyKey,
    string Disposition,
    string? JobId);

/// <summary>Outcome of submitting one manifest.</summary>
public sealed record BatchSubmissionResult(
    string BatchId,
    string QueuePolicy,
    IReadOnlyList<BatchSubmissionItem> Items)
{
    public IReadOnlyList<BatchSubmissionItem> Enqueued => Items
        .Where(static item => string.Equals(item.Disposition, BatchItemDispositions.Enqueued, StringComparison.Ordinal))
        .ToArray();
}

/// <summary>
/// Turns a validated manifest into queue entries. Manifest order is submission order, which the
/// FIFO queue then turns into execution order (REQ-002-13). Entries whose content key already
/// has a successful terminal record are skipped unless the caller forces a full re-run
/// (REQ-002-15); the content key itself is derived by the queue from batch id, item id and the
/// canonical payload, so it survives resubmission and changes as soon as the entry changes.
/// </summary>
public sealed class BatchSubmissionService
{
    private readonly IJobQueueClient _queue;
    private readonly string _sourceEntry;

    /// <summary>
    /// The submitting surface is recorded on every entry it creates, so a queue reader can tell
    /// which entry point asked for the work (REQ-003 §9.1). It is provenance only: it never
    /// selects behaviour, and the same manifest submitted from two surfaces produces otherwise
    /// identical entries.
    /// </summary>
    public BatchSubmissionService(IJobQueueClient queue, string sourceEntry)
    {
        _queue = queue ?? throw new ArgumentNullException(nameof(queue));
        _sourceEntry = RequireSourceEntry(sourceEntry);
    }

    public override string ToString() => "BatchSubmissionService(" + _sourceEntry + ")";

    /// <summary>
    /// Submits every entry in manifest order. The store is probed before the first enqueue so an
    /// unavailable store fails the whole submission without leaving a partial batch behind; if a
    /// later enqueue still fails, the entries already accepted for this batch are cancelled.
    /// </summary>
    public BatchSubmissionResult Submit(BatchManifest manifest, bool force)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        var completedKeys = force ? new HashSet<string>(StringComparer.Ordinal) : ReadCompletedEntryKeys();
        var queuePolicy = BatchFailurePolicies.ToQueuePolicy(manifest.FailurePolicy);
        var submitted = new List<BatchSubmissionItem>(manifest.Items.Count);
        try
        {
            foreach (var item in manifest.Items)
            {
                var request = CreateRequest(_sourceEntry, manifest, queuePolicy, item);
                if (completedKeys.Contains(request.EntryIdempotencyKey))
                {
                    submitted.Add(new BatchSubmissionItem(
                        item.ItemId,
                        request.EntryIdempotencyKey,
                        BatchItemDispositions.SkippedIdempotent,
                        JobId: null));
                    continue;
                }

                var record = _queue.Enqueue(request);
                submitted.Add(new BatchSubmissionItem(
                    item.ItemId,
                    record.EntryIdempotencyKey,
                    BatchItemDispositions.Enqueued,
                    record.JobId));
            }
        }
        catch (JobQueueException)
        {
            RollBack(submitted);
            throw;
        }

        return new BatchSubmissionResult(manifest.BatchId, queuePolicy, submitted);
    }

    /// <summary>Derives the queue request for one entry, including its canonical payload.</summary>
    public static JobEnqueueRequest CreateRequest(
        string sourceEntry,
        BatchManifest manifest,
        string queuePolicy,
        BatchManifestItem item)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(item);
        if (!string.Equals(item.Kind, BatchItemKinds.Prompt, StringComparison.Ordinal))
        {
            throw new ArgumentException("Only prompt entries are executable in this build.", nameof(item));
        }

        return new JobEnqueueRequest(
            RequireSourceEntry(sourceEntry),
            BatchJobKinds.RecipeGeneration,
            BatchGenerationPayload.Create(item),
            manifest.BatchId,
            queuePolicy,
            item.ItemId);
    }

    private static string RequireSourceEntry(string sourceEntry) =>
        JobSourceEntries.All.Contains(sourceEntry)
            ? sourceEntry
            : throw new ArgumentOutOfRangeException(nameof(sourceEntry));

    private HashSet<string> ReadCompletedEntryKeys()
    {
        var snapshot = _queue.ReadSnapshot();
        return snapshot.Jobs
            .Where(static job => string.Equals(job.State, JobStatusStates.Succeeded, StringComparison.Ordinal))
            .Select(static job => job.EntryIdempotencyKey)
            .ToHashSet(StringComparer.Ordinal);
    }

    private void RollBack(IReadOnlyList<BatchSubmissionItem> submitted)
    {
        foreach (var item in submitted)
        {
            if (item.JobId is null)
            {
                continue;
            }

            try
            {
                _queue.RequestCancel(item.JobId);
            }
            catch (JobQueueException)
            {
                // Roll-back is best effort: the store fault that broke the submission may also
                // block the cancellation. The surviving entries are visible in the queue and the
                // batch policy still governs them.
            }
        }
    }
}

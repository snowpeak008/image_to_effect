using VFXComposer.Jobs;
using VFXComposer.Protocol.Jobs;

namespace VFXComposer.Batch.Core;

/// <summary>
/// Rebuilds a <c>vfxcomposer.batch-report/1</c> document from the queue alone. The CLI writes its
/// report next to the manifest and can therefore read it back from disk; a surface that only ever
/// receives a batch id (the MCP report tool) has neither the manifest nor a path, so it derives
/// the same document from the persisted entries. Both paths produce the same schema and the same
/// counters, which is what keeps the two entry surfaces reporting equivalently (REQ-002-11).
/// </summary>
public static class BatchQueueReportBuilder
{
    /// <summary>
    /// Builds the report for one batch from its queue entries, in queue order. Entries that were
    /// skipped as already complete are not queue entries at all, so a queue-derived report can
    /// never contain them; its <c>skippedIdempotent</c> counter is therefore always zero.
    /// </summary>
    public static BatchReport Create(
        string batchId,
        IReadOnlyList<JobRecord> batchJobs,
        DateTimeOffset generatedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(batchId);
        ArgumentNullException.ThrowIfNull(batchJobs);
        var items = new List<BatchReportItem>(batchJobs.Count);
        var succeeded = 0;
        var failed = 0;
        var cancelled = 0;
        var disconnected = 0;
        var pending = 0;
        var failurePolicy = BatchFailurePolicies.Continue;
        if (batchJobs.Count > 0 && batchJobs[0].BatchPolicy is string queuePolicy)
        {
            // One submission fixes one policy for its entries. Resubmitting the same batch id under
            // a different policy would mix them, so the earliest entry in queue order decides what
            // the report says the batch was asked to do.
            failurePolicy = BatchFailurePolicies.FromQueuePolicy(queuePolicy);
        }

        foreach (var job in batchJobs)
        {
            switch (job.State)
            {
                case JobStatusStates.Succeeded when job.IsTerminal:
                    succeeded++;
                    break;
                case JobStatusStates.Failed when job.IsTerminal:
                    failed++;
                    break;
                case JobStatusStates.Cancelled when job.IsTerminal:
                    cancelled++;
                    break;
                case JobStatusStates.Disconnected when job.IsTerminal:
                    disconnected++;
                    break;
                default:
                    pending++;
                    break;
            }

            items.Add(new BatchReportItem(
                EntryLabel(job),
                job.JobId,
                job.State,
                job.IsTerminal ? job.State : null,
                job.FinalDiagnosticCode,
                job.ArtifactIds.Count));
        }

        return new BatchReport(
            BatchReport.CurrentSchema,
            batchId,
            failurePolicy,
            generatedAtUtc,
            items,
            new BatchReportSummary(
                items.Count,
                succeeded,
                failed,
                cancelled,
                disconnected,
                skippedIdempotent: 0,
                pending));
    }

    /// <summary>
    /// Per-entry label of a queue-derived report. The store does not persist the manifest item id
    /// yet, so the job id stands in as the entry label; this is the single place that changes once
    /// the store carries the item id.
    /// </summary>
    private static string EntryLabel(JobRecord job) => job.JobId;
}

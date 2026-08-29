using System.Text.Json;
using System.Text.Json.Serialization;
using VFXComposer.Jobs;
using VFXComposer.Protocol.Jobs;

namespace VFXComposer.Batch.Core;

/// <summary>
/// One line of the batch report. Field names follow the Protocol job event DTOs
/// (<c>state</c>, <c>outcome</c>, <c>diagnostic</c>) so the report, the NDJSON stream and the
/// wire vocabulary agree. Nothing here carries prompt text, endpoints or filesystem paths.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record BatchReportItem
{
    [JsonConstructor]
    public BatchReportItem(
        string itemId,
        string? jobId,
        string? state,
        string? outcome,
        string? diagnostic,
        int artifactCount)
    {
        ItemId = itemId;
        JobId = jobId;
        State = state;
        Outcome = outcome;
        Diagnostic = diagnostic;
        ArtifactCount = artifactCount;
    }

    [JsonPropertyName("itemId")] public string ItemId { get; }
    [JsonPropertyName("jobId")] public string? JobId { get; }
    [JsonPropertyName("state")] public string? State { get; }
    [JsonPropertyName("outcome")] public string? Outcome { get; }
    [JsonPropertyName("diagnostic")] public string? Diagnostic { get; }
    [JsonPropertyName("artifactCount")] public int ArtifactCount { get; }
}

/// <summary>Aggregate counters for one batch report.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record BatchReportSummary
{
    [JsonConstructor]
    public BatchReportSummary(
        int total,
        int succeeded,
        int failed,
        int cancelled,
        int disconnected,
        int skippedIdempotent,
        int pending)
    {
        Total = total;
        Succeeded = succeeded;
        Failed = failed;
        Cancelled = cancelled;
        Disconnected = disconnected;
        SkippedIdempotent = skippedIdempotent;
        Pending = pending;
    }

    [JsonPropertyName("total")] public int Total { get; }
    [JsonPropertyName("succeeded")] public int Succeeded { get; }
    [JsonPropertyName("failed")] public int Failed { get; }
    [JsonPropertyName("cancelled")] public int Cancelled { get; }
    [JsonPropertyName("disconnected")] public int Disconnected { get; }
    [JsonPropertyName("skippedIdempotent")] public int SkippedIdempotent { get; }
    [JsonPropertyName("pending")] public int Pending { get; }
}

/// <summary>The <c>vfxcomposer.batch-report/1</c> document (REQ-002 §6.4).</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record BatchReport
{
    public const string CurrentSchema = "vfxcomposer.batch-report/1";

    [JsonConstructor]
    public BatchReport(
        string schemaVersion,
        string batchId,
        string onFailure,
        DateTimeOffset generatedAtUtc,
        IReadOnlyList<BatchReportItem> items,
        BatchReportSummary summary)
    {
        SchemaVersion = schemaVersion;
        BatchId = batchId;
        OnFailure = onFailure;
        GeneratedAtUtc = generatedAtUtc;
        Items = items;
        Summary = summary;
    }

    [JsonPropertyName("schemaVersion")] public string SchemaVersion { get; }
    [JsonPropertyName("batchId")] public string BatchId { get; }
    [JsonPropertyName("onFailure")] public string OnFailure { get; }
    [JsonPropertyName("generatedAtUtc")] public DateTimeOffset GeneratedAtUtc { get; }
    [JsonPropertyName("items")] public IReadOnlyList<BatchReportItem> Items { get; }
    [JsonPropertyName("summary")] public BatchReportSummary Summary { get; }
}

/// <summary>Overall batch verdict, which the entry surface maps onto its own exit-code table.</summary>
public enum BatchVerdict
{
    AllSucceeded,
    CompletedWithFailures,
    Aborted,
    Pending,
}

/// <summary>Builds and serialises the batch report from submission and tracking results.</summary>
public static class BatchReportBuilder
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    private static readonly JsonSerializerOptions CompactSerializerOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    public static BatchReport Create(
        BatchManifest manifest,
        BatchSubmissionResult submission,
        IReadOnlyDictionary<string, JobRecord> observedJobs,
        DateTimeOffset generatedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(submission);
        ArgumentNullException.ThrowIfNull(observedJobs);
        var items = new List<BatchReportItem>(submission.Items.Count);
        var succeeded = 0;
        var failed = 0;
        var cancelled = 0;
        var disconnected = 0;
        var skipped = 0;
        var pending = 0;
        foreach (var submitted in submission.Items)
        {
            if (submitted.JobId is null)
            {
                skipped++;
                items.Add(new BatchReportItem(
                    submitted.ItemId,
                    jobId: null,
                    state: null,
                    BatchItemDispositions.SkippedIdempotent,
                    diagnostic: null,
                    artifactCount: 0));
                continue;
            }

            observedJobs.TryGetValue(submitted.ItemId, out var job);
            var state = job?.State ?? JobStatusStates.Queued;
            var terminal = job?.IsTerminal == true;
            switch (state)
            {
                case JobStatusStates.Succeeded when terminal:
                    succeeded++;
                    break;
                case JobStatusStates.Failed when terminal:
                    failed++;
                    break;
                case JobStatusStates.Cancelled when terminal:
                    cancelled++;
                    break;
                case JobStatusStates.Disconnected when terminal:
                    disconnected++;
                    break;
                default:
                    pending++;
                    break;
            }

            items.Add(new BatchReportItem(
                submitted.ItemId,
                submitted.JobId,
                state,
                terminal ? state : null,
                job?.FinalDiagnosticCode,
                job?.ArtifactIds.Count ?? 0));
        }

        return new BatchReport(
            BatchReport.CurrentSchema,
            manifest.BatchId,
            manifest.FailurePolicy,
            generatedAtUtc,
            items,
            new BatchReportSummary(items.Count, succeeded, failed, cancelled, disconnected, skipped, pending));
    }

    /// <summary>
    /// Classifies the batch. An abort-policy batch that lost an entry is reported as aborted even
    /// when the queue had nothing left to cancel, because the policy — not the remainder count —
    /// is what the user asked for.
    /// </summary>
    public static BatchVerdict Evaluate(BatchReport report, string failurePolicy)
    {
        ArgumentNullException.ThrowIfNull(report);
        if (report.Summary.Pending > 0)
        {
            return BatchVerdict.Pending;
        }

        var unsuccessful = report.Summary.Failed + report.Summary.Cancelled + report.Summary.Disconnected;
        if (unsuccessful == 0)
        {
            return BatchVerdict.AllSucceeded;
        }

        return string.Equals(failurePolicy, BatchFailurePolicies.Abort, StringComparison.Ordinal) && report.Summary.Failed > 0
            ? BatchVerdict.Aborted
            : BatchVerdict.CompletedWithFailures;
    }

    public static string Serialize(BatchReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        return JsonSerializer.Serialize(report, SerializerOptions);
    }

    /// <summary>
    /// Compact form for surfaces that embed the report inside another document instead of writing
    /// it to a file, where the indented form's line breaks would be unwelcome.
    /// </summary>
    public static string SerializeCompact(BatchReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        return JsonSerializer.Serialize(report, CompactSerializerOptions);
    }

    public static BatchReport Deserialize(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        return JsonSerializer.Deserialize<BatchReport>(json, SerializerOptions)
            ?? throw new InvalidDataException("The batch report document is empty.");
    }
}

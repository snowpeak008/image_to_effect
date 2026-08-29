using System.Collections.Frozen;
using System.Text.Json.Serialization;
using VFXComposer.Protocol.Jobs;

namespace VFXComposer.Jobs;

/// <summary>Closed vocabulary for append-only store event kinds.</summary>
public static class JobStoreEventKinds
{
    public const string Status = "STATUS";
    public const string Progress = "PROGRESS";
    public const string Log = "LOG";
    public const string Artifact = "ARTIFACT";
    public const string Completion = "COMPLETION";

    private static readonly FrozenSet<string> Known =
        new[] { Status, Progress, Log, Artifact, Completion }.ToFrozenSet(StringComparer.Ordinal);

    public static IReadOnlySet<string> All => Known;

    internal static string Require(string value, string parameterName) =>
        Known.Contains(value) ? value : throw new ArgumentOutOfRangeException(parameterName);
}

/// <summary>
/// One append-only event-log line. Field shapes mirror the Protocol job event DTOs
/// (<c>JobProgress</c>, <c>JobLogEvent</c>, <c>JobArtifact</c>, <c>JobCompletion</c>) but this
/// is a versioned local store schema, not a registered wire contract. Events never carry the
/// payload, prompt text or any filesystem path.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record JobStoreEvent
{
    public const string CurrentSchema = "vfxcomposer.job-event/1";

    [JsonConstructor]
    public JobStoreEvent(
        string schema,
        string jobId,
        long eventSequence,
        string kind,
        DateTimeOffset occurredAtUtc,
        string? state,
        int? progressPermille,
        string? level,
        string? diagnosticCode,
        string? outcome,
        string? artifactId)
    {
        if (!string.Equals(schema, CurrentSchema, StringComparison.Ordinal))
        {
            throw new JobQueueException(JobQueueDiagnosticCodes.StoreVersionUnsupported);
        }

        Schema = schema;
        JobId = JobsGuard.Token(jobId, nameof(jobId));
        if (eventSequence < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(eventSequence));
        }

        EventSequence = eventSequence;
        Kind = JobStoreEventKinds.Require(kind, nameof(kind));
        OccurredAtUtc = JobsGuard.Utc(occurredAtUtc, nameof(occurredAtUtc));
        switch (Kind)
        {
            case JobStoreEventKinds.Status:
                State = JobStatusStateSet.Require(state, nameof(state));
                RequireAbsent(progressPermille is null && level is null && diagnosticCode is null && outcome is null && artifactId is null);
                break;
            case JobStoreEventKinds.Progress:
                State = state is not null && JobProgressStates.All.Contains(state)
                    ? state
                    : throw new ArgumentOutOfRangeException(nameof(state));
                ProgressPermille = progressPermille is >= 0 and <= JobProgress.MaximumPermille
                    ? progressPermille
                    : throw new ArgumentOutOfRangeException(nameof(progressPermille));
                RequireAbsent(level is null && diagnosticCode is null && outcome is null && artifactId is null);
                break;
            case JobStoreEventKinds.Log:
                Level = level is not null && JobLogLevels.All.Contains(level)
                    ? level
                    : throw new ArgumentOutOfRangeException(nameof(level));
                DiagnosticCode = JobQueueDiagnosticCatalog.Require(
                    diagnosticCode ?? throw new ArgumentNullException(nameof(diagnosticCode))).Code;
                RequireAbsent(state is null && progressPermille is null && outcome is null && artifactId is null);
                break;
            case JobStoreEventKinds.Artifact:
                ArtifactId = JobsGuard.Token(
                    artifactId ?? throw new ArgumentNullException(nameof(artifactId)),
                    nameof(artifactId));
                RequireAbsent(state is null && progressPermille is null && level is null && diagnosticCode is null && outcome is null);
                break;
            default:
                Outcome = outcome is not null && JobCompletionOutcomes.All.Contains(outcome)
                    ? outcome
                    : throw new ArgumentOutOfRangeException(nameof(outcome));
                var succeeded = string.Equals(Outcome, JobCompletionOutcomes.Succeeded, StringComparison.Ordinal);
                if (succeeded != (diagnosticCode is null))
                {
                    throw new ArgumentException("Completion diagnostic shape does not match the outcome vocabulary.", nameof(diagnosticCode));
                }

                DiagnosticCode = diagnosticCode is null ? null : JobQueueDiagnosticCatalog.Require(diagnosticCode).Code;
                RequireAbsent(state is null && progressPermille is null && level is null && artifactId is null);
                break;
        }
    }

    [JsonPropertyName("schema")] public string Schema { get; }
    [JsonPropertyName("jobId")] public string JobId { get; }
    [JsonPropertyName("eventSequence")] public long EventSequence { get; }
    [JsonPropertyName("kind")] public string Kind { get; }
    [JsonPropertyName("occurredAtUtc")] public DateTimeOffset OccurredAtUtc { get; }
    [JsonPropertyName("state")] public string? State { get; }
    [JsonPropertyName("progressPermille")] public int? ProgressPermille { get; }
    [JsonPropertyName("level")] public string? Level { get; }
    [JsonPropertyName("diagnosticCode")] public string? DiagnosticCode { get; }
    [JsonPropertyName("outcome")] public string? Outcome { get; }
    [JsonPropertyName("artifactId")] public string? ArtifactId { get; }

    private static void RequireAbsent(bool condition)
    {
        if (!condition)
        {
            throw new ArgumentException("Event carries fields that do not belong to its kind.");
        }
    }
}

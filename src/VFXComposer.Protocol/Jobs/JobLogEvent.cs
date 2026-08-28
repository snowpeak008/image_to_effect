using System.Text.Json.Serialization;
using VFXComposer.Protocol.Diagnostics;
using VFXComposer.Protocol.Hashing;

namespace VFXComposer.Protocol.Jobs;

/// <summary>Structured job log data uses a fixed diagnostic, never caller-authored text.</summary>
public sealed record JobLogEvent : JobEventEnvelope
{
    public const string SelfHashType = "vfxcomposer.job-log-event/1";

    [JsonConstructor]
    public JobLogEvent(
        string protocolVersion,
        string messageKind,
        TypedHash projectIdentity,
        string leaseId,
        long leaseGeneration,
        JobCorrelation job,
        long eventSequence,
        string level,
        StableDiagnostic diagnostic,
        TypedHash selfHash)
        : base(protocolVersion, messageKind, projectIdentity, leaseId, leaseGeneration, job, eventSequence, selfHash)
    {
        RequireMessageKind(messageKind, JobMessageKinds.LogEvent, nameof(messageKind));
        Level = JobLogLevels.Require(level, nameof(level));
        Diagnostic = diagnostic ?? throw new ArgumentNullException(nameof(diagnostic));
    }

    [JsonPropertyName("level")] public string Level { get; }
    [JsonPropertyName("diagnostic")] public StableDiagnostic Diagnostic { get; }
}

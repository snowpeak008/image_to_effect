using System.Text.Json.Serialization;
using VFXComposer.Protocol.Diagnostics;
using VFXComposer.Protocol.Hashing;

namespace VFXComposer.Protocol.Jobs;

/// <summary>
/// A completion report is only a job-domain fact. It is never machine, visual, user,
/// L3, or L4 authority.
/// </summary>
public sealed record JobCompletion : JobEventEnvelope
{
    public const string SelfHashType = "vfxcomposer.job-completion/1";
    public const int MaximumArtifactCount = 64;

    [JsonConstructor]
    public JobCompletion(
        string protocolVersion,
        string messageKind,
        TypedHash projectIdentity,
        string leaseId,
        long leaseGeneration,
        JobCorrelation job,
        long eventSequence,
        string outcome,
        int finalArtifactCount,
        StableDiagnostic? diagnostic,
        DateTimeOffset completedAtUtc,
        TypedHash selfHash)
        : base(protocolVersion, messageKind, projectIdentity, leaseId, leaseGeneration, job, eventSequence, selfHash)
    {
        RequireMessageKind(messageKind, JobMessageKinds.Completion, nameof(messageKind));
        Outcome = JobCompletionOutcomes.Require(outcome, nameof(outcome));
        if (finalArtifactCount is < 0 or > MaximumArtifactCount)
        {
            throw new ArgumentOutOfRangeException(nameof(finalArtifactCount));
        }

        if ((string.Equals(Outcome, JobCompletionOutcomes.Succeeded, StringComparison.Ordinal) && diagnostic is not null) ||
            (!string.Equals(Outcome, JobCompletionOutcomes.Succeeded, StringComparison.Ordinal) && diagnostic is null))
        {
            throw new ArgumentException("Completion diagnostic shape does not match the outcome vocabulary.", nameof(diagnostic));
        }

        FinalArtifactCount = finalArtifactCount;
        Diagnostic = diagnostic;
        CompletedAtUtc = Guard.Utc(completedAtUtc, nameof(completedAtUtc));
    }

    [JsonPropertyName("outcome")] public string Outcome { get; }
    [JsonPropertyName("finalArtifactCount")] public int FinalArtifactCount { get; }
    [JsonPropertyName("diagnostic")] public StableDiagnostic? Diagnostic { get; }
    [JsonPropertyName("completedAtUtc")] public DateTimeOffset CompletedAtUtc { get; }
}

using System.Text.Json.Serialization;
using VFXComposer.Protocol.Hashing;

namespace VFXComposer.Protocol.Jobs;

/// <summary>Bounded job-progress vocabulary and correlation data only.</summary>
public sealed record JobProgress : JobEventEnvelope
{
    public const string SelfHashType = "vfxcomposer.job-progress/1";
    public const int MaximumPermille = 1_000;

    [JsonConstructor]
    public JobProgress(
        string protocolVersion,
        string messageKind,
        TypedHash projectIdentity,
        string leaseId,
        long leaseGeneration,
        JobCorrelation job,
        long eventSequence,
        string state,
        int progressPermille,
        TypedHash selfHash)
        : base(protocolVersion, messageKind, projectIdentity, leaseId, leaseGeneration, job, eventSequence, selfHash)
    {
        RequireMessageKind(messageKind, JobMessageKinds.Progress, nameof(messageKind));
        State = JobProgressStates.Require(state, nameof(state));
        if (progressPermille is < 0 or > MaximumPermille)
        {
            throw new ArgumentOutOfRangeException(nameof(progressPermille));
        }

        ProgressPermille = progressPermille;
    }

    [JsonPropertyName("state")] public string State { get; }
    [JsonPropertyName("progressPermille")] public int ProgressPermille { get; }
}

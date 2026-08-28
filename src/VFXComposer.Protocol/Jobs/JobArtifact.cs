using System.Text.Json.Serialization;
using VFXComposer.Protocol.Hashing;

namespace VFXComposer.Protocol.Jobs;

/// <summary>An opaque, bounded artifact identity; it deliberately contains no location.</summary>
public sealed record JobArtifact : JobEventEnvelope
{
    public const string SelfHashType = "vfxcomposer.job-artifact/1";
    public const int MaximumByteLength = 1_048_576;

    [JsonConstructor]
    public JobArtifact(
        string protocolVersion,
        string messageKind,
        TypedHash projectIdentity,
        string leaseId,
        long leaseGeneration,
        JobCorrelation job,
        long eventSequence,
        string artifactKind,
        string artifactId,
        TypedHash artifactHash,
        int byteLength,
        TypedHash selfHash)
        : base(protocolVersion, messageKind, projectIdentity, leaseId, leaseGeneration, job, eventSequence, selfHash)
    {
        RequireMessageKind(messageKind, JobMessageKinds.Artifact, nameof(messageKind));
        ArtifactKind = artifactKind;
        ArtifactId = Guard.Token(artifactId, nameof(artifactId), 96);
        ArtifactHash = WireModelGuard.TypedHash(
            artifactHash,
            JobArtifactKinds.RequireHashType(artifactKind, nameof(artifactKind)),
            nameof(artifactHash));
        if (byteLength is < 0 or > MaximumByteLength)
        {
            throw new ArgumentOutOfRangeException(nameof(byteLength));
        }

        ByteLength = byteLength;
    }

    [JsonPropertyName("artifactKind")] public string ArtifactKind { get; }
    [JsonPropertyName("artifactId")] public string ArtifactId { get; }
    [JsonPropertyName("artifactHash")] public TypedHash ArtifactHash { get; }
    [JsonPropertyName("byteLength")] public int ByteLength { get; }
}

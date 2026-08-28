using System.Text.Json.Serialization;
using VFXComposer.Protocol.Hashing;
using VFXComposer.Protocol.Registration;

namespace VFXComposer.Protocol.Jobs;

/// <summary>
/// Shared immutable job-event correlation envelope. It is data only, not a job state
/// machine or a projection into any machine, visual, user, L3, or L4 authority domain.
/// </summary>
public abstract record JobEventEnvelope
{
    protected JobEventEnvelope(
        string protocolVersion,
        string messageKind,
        TypedHash projectIdentity,
        string leaseId,
        long leaseGeneration,
        JobCorrelation job,
        long eventSequence,
        TypedHash selfHash)
    {
        if (!string.Equals(protocolVersion, ProtocolVersions.Current, StringComparison.Ordinal))
        {
            throw new ArgumentException("Unsupported protocol version.", nameof(protocolVersion));
        }

        var descriptor = JobContractRegistry.RequireForMessageKind(messageKind, nameof(messageKind));
        if (leaseGeneration <= 0 || eventSequence <= 0)
        {
            throw new ArgumentOutOfRangeException(leaseGeneration <= 0 ? nameof(leaseGeneration) : nameof(eventSequence));
        }

        ProtocolVersion = protocolVersion;
        MessageKind = descriptor.MessageKind;
        ProjectIdentity = WireModelGuard.TypedHash(
            projectIdentity,
            ProjectRegistrationAttestation.ProjectIdentityType,
            nameof(projectIdentity));
        LeaseId = Guard.Token(leaseId, nameof(leaseId));
        LeaseGeneration = leaseGeneration;
        Job = job ?? throw new ArgumentNullException(nameof(job));
        EventSequence = eventSequence;
        SelfHash = WireModelGuard.TypedHash(selfHash, descriptor.SelfHashType, nameof(selfHash));
    }

    [JsonPropertyName("protocolVersion")] public string ProtocolVersion { get; }
    [JsonPropertyName("messageKind")] public string MessageKind { get; }
    [JsonPropertyName("projectIdentity")] public TypedHash ProjectIdentity { get; }
    [JsonPropertyName("leaseId")] public string LeaseId { get; }
    [JsonPropertyName("leaseGeneration")] public long LeaseGeneration { get; }
    [JsonPropertyName("job")] public JobCorrelation Job { get; }
    [JsonPropertyName("eventSequence")] public long EventSequence { get; }
    [JsonPropertyName("selfHash")] public TypedHash SelfHash { get; }

    protected static void RequireMessageKind(string actual, string expected, string parameterName)
    {
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
        {
            throw new ArgumentException("Unexpected message kind.", parameterName);
        }
    }
}

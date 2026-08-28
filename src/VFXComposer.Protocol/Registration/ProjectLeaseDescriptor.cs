using System.Text.Json.Serialization;
using VFXComposer.Protocol.Hashing;

namespace VFXComposer.Protocol.Registration;

/// <summary>Wire-visible lease identity; the actual capability remains opaque and process-local.</summary>
public sealed record ProjectLeaseDescriptor
{
    public const string SelfHashType = "vfxcomposer.project-lease-descriptor/1";

    [JsonConstructor]
    public ProjectLeaseDescriptor(
        string protocolVersion,
        string messageKind,
        string requestId,
        string leaseId,
        string registeredProjectId,
        TypedHash projectIdentity,
        long brokerGeneration,
        long registrationGeneration,
        string workerSessionId,
        string workerProcessEpoch,
        long leaseGeneration,
        TypedHash selfHash)
    {
        if (!string.Equals(protocolVersion, ProtocolVersions.Current, StringComparison.Ordinal))
        {
            throw new ArgumentException("Unsupported protocol version.", nameof(protocolVersion));
        }

        if (!string.Equals(messageKind, MessageKinds.ProjectLeaseDescriptor, StringComparison.Ordinal))
        {
            throw new ArgumentException("Unexpected message kind.", nameof(messageKind));
        }

        if (brokerGeneration <= 0 || registrationGeneration <= 0 || leaseGeneration <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(brokerGeneration));
        }

        ProtocolVersion = protocolVersion;
        MessageKind = messageKind;
        RequestId = Guard.Token(requestId, nameof(requestId));
        LeaseId = Guard.Token(leaseId, nameof(leaseId));
        RegisteredProjectId = Guard.Token(registeredProjectId, nameof(registeredProjectId));
        ProjectIdentity = WireModelGuard.TypedHash(
            projectIdentity,
            ProjectRegistrationAttestation.ProjectIdentityType,
            nameof(projectIdentity));
        BrokerGeneration = brokerGeneration;
        RegistrationGeneration = registrationGeneration;
        WorkerSessionId = Guard.Token(workerSessionId, nameof(workerSessionId));
        WorkerProcessEpoch = Guard.Token(workerProcessEpoch, nameof(workerProcessEpoch));
        LeaseGeneration = leaseGeneration;
        SelfHash = WireModelGuard.TypedHash(selfHash, SelfHashType, nameof(selfHash));
    }

    [JsonPropertyName("protocolVersion")] public string ProtocolVersion { get; }
    [JsonPropertyName("messageKind")] public string MessageKind { get; }
    [JsonPropertyName("requestId")] public string RequestId { get; }
    [JsonPropertyName("leaseId")] public string LeaseId { get; }
    [JsonPropertyName("registeredProjectId")] public string RegisteredProjectId { get; }
    [JsonPropertyName("projectIdentity")] public TypedHash ProjectIdentity { get; }
    [JsonPropertyName("brokerGeneration")] public long BrokerGeneration { get; }
    [JsonPropertyName("registrationGeneration")] public long RegistrationGeneration { get; }
    [JsonPropertyName("workerSessionId")] public string WorkerSessionId { get; }
    [JsonPropertyName("workerProcessEpoch")] public string WorkerProcessEpoch { get; }
    [JsonPropertyName("leaseGeneration")] public long LeaseGeneration { get; }
    [JsonPropertyName("selfHash")] public TypedHash SelfHash { get; }
}

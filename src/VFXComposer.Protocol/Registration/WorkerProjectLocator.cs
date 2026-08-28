using System.Text.Json.Serialization;
using VFXComposer.Protocol.Hashing;

namespace VFXComposer.Protocol.Registration;

/// <summary>
/// Immutable host-owned project identity correlations for one already bounded Worker.
/// Decoding this DTO neither locates a project nor creates a runtime capability.
/// </summary>
public sealed record WorkerProjectLocator
{
    public const string SelfHashType = "vfxcomposer.worker-project-locator/1";

    [JsonConstructor]
    public WorkerProjectLocator(
        string protocolVersion,
        string messageKind,
        string requestId,
        string registeredProjectId,
        TypedHash projectIdentity,
        TypedHash volumeIdentity,
        TypedHash repositoryIdentity,
        TypedHash projectRootIdentity,
        long brokerGeneration,
        long registrationGeneration,
        long enrollmentGeneration,
        string workerSessionId,
        string workerProcessEpoch,
        TypedHash selfHash)
    {
        if (!string.Equals(protocolVersion, ProtocolVersions.Current, StringComparison.Ordinal))
        {
            throw new ArgumentException("Unsupported protocol version.", nameof(protocolVersion));
        }

        if (!string.Equals(messageKind, MessageKinds.WorkerProjectLocator, StringComparison.Ordinal))
        {
            throw new ArgumentException("Unexpected message kind.", nameof(messageKind));
        }

        if (brokerGeneration <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(brokerGeneration));
        }

        if (registrationGeneration <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(registrationGeneration));
        }

        if (enrollmentGeneration <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(enrollmentGeneration));
        }

        ProtocolVersion = protocolVersion;
        MessageKind = messageKind;
        RequestId = Guard.Token(requestId, nameof(requestId));
        RegisteredProjectId = Guard.Token(registeredProjectId, nameof(registeredProjectId));
        ProjectIdentity = WireModelGuard.TypedHash(
            projectIdentity,
            ProjectRegistrationAttestation.ProjectIdentityType,
            nameof(projectIdentity));
        VolumeIdentity = WireModelGuard.TypedHash(
            volumeIdentity,
            ProjectRegistrationAttestation.VolumeIdentityType,
            nameof(volumeIdentity));
        RepositoryIdentity = WireModelGuard.TypedHash(
            repositoryIdentity,
            ProjectRegistrationAttestation.DirectoryIdentityType,
            nameof(repositoryIdentity));
        ProjectRootIdentity = WireModelGuard.TypedHash(
            projectRootIdentity,
            ProjectRegistrationAttestation.DirectoryIdentityType,
            nameof(projectRootIdentity));
        BrokerGeneration = brokerGeneration;
        RegistrationGeneration = registrationGeneration;
        EnrollmentGeneration = enrollmentGeneration;
        WorkerSessionId = Guard.Token(workerSessionId, nameof(workerSessionId));
        WorkerProcessEpoch = Guard.Token(workerProcessEpoch, nameof(workerProcessEpoch));
        SelfHash = WireModelGuard.TypedHash(selfHash, SelfHashType, nameof(selfHash));
    }

    [JsonPropertyName("protocolVersion")] public string ProtocolVersion { get; }
    [JsonPropertyName("messageKind")] public string MessageKind { get; }
    [JsonPropertyName("requestId")] public string RequestId { get; }
    [JsonPropertyName("registeredProjectId")] public string RegisteredProjectId { get; }
    [JsonPropertyName("projectIdentity")] public TypedHash ProjectIdentity { get; }
    [JsonPropertyName("volumeIdentity")] public TypedHash VolumeIdentity { get; }
    [JsonPropertyName("repositoryIdentity")] public TypedHash RepositoryIdentity { get; }
    [JsonPropertyName("projectRootIdentity")] public TypedHash ProjectRootIdentity { get; }
    [JsonPropertyName("brokerGeneration")] public long BrokerGeneration { get; }
    [JsonPropertyName("registrationGeneration")] public long RegistrationGeneration { get; }
    [JsonPropertyName("enrollmentGeneration")] public long EnrollmentGeneration { get; }
    [JsonPropertyName("workerSessionId")] public string WorkerSessionId { get; }
    [JsonPropertyName("workerProcessEpoch")] public string WorkerProcessEpoch { get; }
    [JsonPropertyName("selfHash")] public TypedHash SelfHash { get; }
}

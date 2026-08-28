using System.Text.Json.Serialization;
using VFXComposer.Protocol.Hashing;

namespace VFXComposer.Protocol.Registration;

/// <summary>
/// A broker-issued identity projection. Decoding this DTO does not authenticate its issuer
/// and therefore never grants a project capability by itself.
/// </summary>
public sealed record ProjectRegistrationAttestation
{
    public const string SelfHashType = "vfxcomposer.project-registration-attestation/1";
    public const string ProjectIdentityType = "vfxcomposer.project-identity/1";
    public const string VolumeIdentityType = "vfxcomposer.volume-identity/1";
    public const string DirectoryIdentityType = "vfxcomposer.directory-identity/1";

    [JsonConstructor]
    public ProjectRegistrationAttestation(
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
        string workerSessionId,
        string workerProcessEpoch,
        TypedHash selfHash)
    {
        if (!string.Equals(protocolVersion, ProtocolVersions.Current, StringComparison.Ordinal))
        {
            throw new ArgumentException("Unsupported protocol version.", nameof(protocolVersion));
        }

        if (!string.Equals(messageKind, MessageKinds.ProjectRegistrationAttestation, StringComparison.Ordinal))
        {
            throw new ArgumentException("Unexpected message kind.", nameof(messageKind));
        }

        if (brokerGeneration <= 0 || registrationGeneration <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(brokerGeneration));
        }

        ProtocolVersion = protocolVersion;
        MessageKind = messageKind;
        RequestId = Guard.Token(requestId, nameof(requestId));
        RegisteredProjectId = Guard.Token(registeredProjectId, nameof(registeredProjectId));
        ProjectIdentity = WireModelGuard.TypedHash(projectIdentity, ProjectIdentityType, nameof(projectIdentity));
        VolumeIdentity = WireModelGuard.TypedHash(volumeIdentity, VolumeIdentityType, nameof(volumeIdentity));
        RepositoryIdentity = WireModelGuard.TypedHash(repositoryIdentity, DirectoryIdentityType, nameof(repositoryIdentity));
        ProjectRootIdentity = WireModelGuard.TypedHash(projectRootIdentity, DirectoryIdentityType, nameof(projectRootIdentity));
        BrokerGeneration = brokerGeneration;
        RegistrationGeneration = registrationGeneration;
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
    [JsonPropertyName("workerSessionId")] public string WorkerSessionId { get; }
    [JsonPropertyName("workerProcessEpoch")] public string WorkerProcessEpoch { get; }
    [JsonPropertyName("selfHash")] public TypedHash SelfHash { get; }
}

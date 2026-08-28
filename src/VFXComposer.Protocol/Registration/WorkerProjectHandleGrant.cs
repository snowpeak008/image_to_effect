using System.Text.Json.Serialization;
using VFXComposer.Protocol.Hashing;

namespace VFXComposer.Protocol.Registration;

/// <summary>
/// Worker-only description of handles already duplicated into the authenticated
/// Worker process. The text values are meaningful only inside that exact process.
/// </summary>
public sealed record WorkerProjectHandleGrant
{
    public const string HandleEncodingName = "win-handle-u64-lower-hex/1";
    public const string SelfHashType = "vfxcomposer.worker-project-handle-grant/1";

    [JsonConstructor]
    public WorkerProjectHandleGrant(
        string protocolVersion,
        string messageKind,
        string requestId,
        string leaseId,
        string registeredProjectId,
        TypedHash projectIdentity,
        TypedHash volumeIdentity,
        TypedHash repositoryIdentity,
        TypedHash projectRootIdentity,
        long brokerGeneration,
        long registrationGeneration,
        long leaseGeneration,
        string workerSessionId,
        string workerProcessEpoch,
        string handleEncoding,
        string volumeHandle,
        string repositoryHandle,
        string projectRootHandle,
        TypedHash selfHash)
    {
        if (!string.Equals(protocolVersion, ProtocolVersions.Current, StringComparison.Ordinal))
        {
            throw new ArgumentException("Unsupported protocol version.", nameof(protocolVersion));
        }

        if (!string.Equals(messageKind, MessageKinds.WorkerProjectHandleGrant, StringComparison.Ordinal))
        {
            throw new ArgumentException("Unexpected message kind.", nameof(messageKind));
        }

        if (brokerGeneration <= 0 || registrationGeneration <= 0 || leaseGeneration <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(brokerGeneration));
        }

        if (!string.Equals(handleEncoding, HandleEncodingName, StringComparison.Ordinal))
        {
            throw new ArgumentException("Unsupported native handle encoding.", nameof(handleEncoding));
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
        LeaseGeneration = leaseGeneration;
        WorkerSessionId = Guard.Token(workerSessionId, nameof(workerSessionId));
        WorkerProcessEpoch = Guard.Token(workerProcessEpoch, nameof(workerProcessEpoch));
        this.HandleEncoding = handleEncoding;
        VolumeHandle = RequireHandle(volumeHandle, nameof(volumeHandle));
        RepositoryHandle = RequireHandle(repositoryHandle, nameof(repositoryHandle));
        ProjectRootHandle = RequireHandle(projectRootHandle, nameof(projectRootHandle));
        SelfHash = WireModelGuard.TypedHash(selfHash, SelfHashType, nameof(selfHash));
    }

    [JsonPropertyName("protocolVersion")] public string ProtocolVersion { get; }
    [JsonPropertyName("messageKind")] public string MessageKind { get; }
    [JsonPropertyName("requestId")] public string RequestId { get; }
    [JsonPropertyName("leaseId")] public string LeaseId { get; }
    [JsonPropertyName("registeredProjectId")] public string RegisteredProjectId { get; }
    [JsonPropertyName("projectIdentity")] public TypedHash ProjectIdentity { get; }
    [JsonPropertyName("volumeIdentity")] public TypedHash VolumeIdentity { get; }
    [JsonPropertyName("repositoryIdentity")] public TypedHash RepositoryIdentity { get; }
    [JsonPropertyName("projectRootIdentity")] public TypedHash ProjectRootIdentity { get; }
    [JsonPropertyName("brokerGeneration")] public long BrokerGeneration { get; }
    [JsonPropertyName("registrationGeneration")] public long RegistrationGeneration { get; }
    [JsonPropertyName("leaseGeneration")] public long LeaseGeneration { get; }
    [JsonPropertyName("workerSessionId")] public string WorkerSessionId { get; }
    [JsonPropertyName("workerProcessEpoch")] public string WorkerProcessEpoch { get; }
    [JsonPropertyName("handleEncoding")] public string HandleEncoding { get; }
    [JsonPropertyName("volumeHandle")] public string VolumeHandle { get; }
    [JsonPropertyName("repositoryHandle")] public string RepositoryHandle { get; }
    [JsonPropertyName("projectRootHandle")] public string ProjectRootHandle { get; }
    [JsonPropertyName("selfHash")] public TypedHash SelfHash { get; }

    private static string RequireHandle(string value, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(value, parameterName);
        if (value.Length != 16 ||
            value.All(character => character == '0') ||
            value.All(character => character == 'f') ||
            value.Any(character => character is not (>= '0' and <= '9') and
                not (>= 'a' and <= 'f')))
        {
            throw new ArgumentException("Native handle text is not canonical.", parameterName);
        }

        return value;
    }
}

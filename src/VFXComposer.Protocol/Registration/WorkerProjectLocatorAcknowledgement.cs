using System.Text.Json.Serialization;
using VFXComposer.Protocol.Hashing;

namespace VFXComposer.Protocol.Registration;

/// <summary>
/// Immutable acknowledgement of one exact Worker project locator correlation.
/// It carries no handle, session issuance, lease, permission, or runtime authority.
/// </summary>
public sealed record WorkerProjectLocatorAcknowledgement
{
    public const string AcceptedDisposition = "LOCATOR_ACCEPTED";
    public const string SelfHashType = "vfxcomposer.worker-project-locator-ack/1";

    [JsonConstructor]
    public WorkerProjectLocatorAcknowledgement(
        string protocolVersion,
        string messageKind,
        string requestId,
        string registeredProjectId,
        long brokerGeneration,
        long registrationGeneration,
        long enrollmentGeneration,
        string workerSessionId,
        string workerProcessEpoch,
        TypedHash locatorSelfHash,
        string disposition,
        TypedHash selfHash)
    {
        if (!string.Equals(protocolVersion, ProtocolVersions.Current, StringComparison.Ordinal))
        {
            throw new ArgumentException("Unsupported protocol version.", nameof(protocolVersion));
        }

        if (!string.Equals(messageKind, MessageKinds.WorkerProjectLocatorAcknowledgement, StringComparison.Ordinal))
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

        if (!string.Equals(disposition, AcceptedDisposition, StringComparison.Ordinal))
        {
            throw new ArgumentException("Unexpected locator acknowledgement disposition.", nameof(disposition));
        }

        ProtocolVersion = protocolVersion;
        MessageKind = messageKind;
        RequestId = Guard.Token(requestId, nameof(requestId));
        RegisteredProjectId = Guard.Token(registeredProjectId, nameof(registeredProjectId));
        BrokerGeneration = brokerGeneration;
        RegistrationGeneration = registrationGeneration;
        EnrollmentGeneration = enrollmentGeneration;
        WorkerSessionId = Guard.Token(workerSessionId, nameof(workerSessionId));
        WorkerProcessEpoch = Guard.Token(workerProcessEpoch, nameof(workerProcessEpoch));
        LocatorSelfHash = WireModelGuard.TypedHash(
            locatorSelfHash,
            WorkerProjectLocator.SelfHashType,
            nameof(locatorSelfHash));
        Disposition = disposition;
        SelfHash = WireModelGuard.TypedHash(selfHash, SelfHashType, nameof(selfHash));
    }

    [JsonPropertyName("protocolVersion")] public string ProtocolVersion { get; }
    [JsonPropertyName("messageKind")] public string MessageKind { get; }
    [JsonPropertyName("requestId")] public string RequestId { get; }
    [JsonPropertyName("registeredProjectId")] public string RegisteredProjectId { get; }
    [JsonPropertyName("brokerGeneration")] public long BrokerGeneration { get; }
    [JsonPropertyName("registrationGeneration")] public long RegistrationGeneration { get; }
    [JsonPropertyName("enrollmentGeneration")] public long EnrollmentGeneration { get; }
    [JsonPropertyName("workerSessionId")] public string WorkerSessionId { get; }
    [JsonPropertyName("workerProcessEpoch")] public string WorkerProcessEpoch { get; }
    [JsonPropertyName("locatorSelfHash")] public TypedHash LocatorSelfHash { get; }
    [JsonPropertyName("disposition")] public string Disposition { get; }
    [JsonPropertyName("selfHash")] public TypedHash SelfHash { get; }
}

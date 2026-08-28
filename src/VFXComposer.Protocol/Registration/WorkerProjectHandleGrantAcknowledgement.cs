using System.Text.Json.Serialization;
using VFXComposer.Protocol.Hashing;

namespace VFXComposer.Protocol.Registration;

public sealed record WorkerProjectHandleGrantAcknowledgement
{
    public const string AcceptedDisposition = "GRANT_ACCEPTED";
    public const string SelfHashType = "vfxcomposer.worker-project-handle-grant-ack/1";

    [JsonConstructor]
    public WorkerProjectHandleGrantAcknowledgement(
        string protocolVersion,
        string messageKind,
        string requestId,
        string leaseId,
        long brokerGeneration,
        long leaseGeneration,
        string workerSessionId,
        string workerProcessEpoch,
        TypedHash grantSelfHash,
        string disposition,
        TypedHash selfHash)
    {
        WorkerHandleLifecycleWireGuard.RequireHeader(
            protocolVersion,
            messageKind,
            MessageKinds.WorkerProjectHandleGrantAcknowledgement,
            brokerGeneration,
            leaseGeneration);
        if (!string.Equals(disposition, AcceptedDisposition, StringComparison.Ordinal))
        {
            throw new ArgumentException("Unexpected grant acknowledgement disposition.", nameof(disposition));
        }

        ProtocolVersion = protocolVersion;
        MessageKind = messageKind;
        RequestId = Guard.Token(requestId, nameof(requestId));
        LeaseId = Guard.Token(leaseId, nameof(leaseId));
        BrokerGeneration = brokerGeneration;
        LeaseGeneration = leaseGeneration;
        WorkerSessionId = Guard.Token(workerSessionId, nameof(workerSessionId));
        WorkerProcessEpoch = Guard.Token(workerProcessEpoch, nameof(workerProcessEpoch));
        GrantSelfHash = WireModelGuard.TypedHash(
            grantSelfHash,
            WorkerProjectHandleGrant.SelfHashType,
            nameof(grantSelfHash));
        Disposition = disposition;
        SelfHash = WireModelGuard.TypedHash(selfHash, SelfHashType, nameof(selfHash));
    }

    [JsonPropertyName("protocolVersion")] public string ProtocolVersion { get; }
    [JsonPropertyName("messageKind")] public string MessageKind { get; }
    [JsonPropertyName("requestId")] public string RequestId { get; }
    [JsonPropertyName("leaseId")] public string LeaseId { get; }
    [JsonPropertyName("brokerGeneration")] public long BrokerGeneration { get; }
    [JsonPropertyName("leaseGeneration")] public long LeaseGeneration { get; }
    [JsonPropertyName("workerSessionId")] public string WorkerSessionId { get; }
    [JsonPropertyName("workerProcessEpoch")] public string WorkerProcessEpoch { get; }
    [JsonPropertyName("grantSelfHash")] public TypedHash GrantSelfHash { get; }
    [JsonPropertyName("disposition")] public string Disposition { get; }
    [JsonPropertyName("selfHash")] public TypedHash SelfHash { get; }
}

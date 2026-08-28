using System.Text.Json.Serialization;
using VFXComposer.Protocol.Hashing;

namespace VFXComposer.Protocol.Registration;

public sealed record WorkerProjectHandleRevokeAcknowledgement
{
    public const string ClosedDisposition = "HANDLES_CLOSED";
    public const string SelfHashType = "vfxcomposer.worker-project-handle-revoke-ack/1";

    [JsonConstructor]
    public WorkerProjectHandleRevokeAcknowledgement(
        string protocolVersion,
        string messageKind,
        string requestId,
        string leaseId,
        long brokerGeneration,
        long leaseGeneration,
        string workerSessionId,
        string workerProcessEpoch,
        TypedHash grantSelfHash,
        TypedHash revokeSelfHash,
        string disposition,
        TypedHash selfHash)
    {
        WorkerHandleLifecycleWireGuard.RequireHeader(
            protocolVersion,
            messageKind,
            MessageKinds.WorkerProjectHandleRevokeAcknowledgement,
            brokerGeneration,
            leaseGeneration);
        if (!string.Equals(disposition, ClosedDisposition, StringComparison.Ordinal))
        {
            throw new ArgumentException("Unexpected revocation acknowledgement disposition.", nameof(disposition));
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
        RevokeSelfHash = WireModelGuard.TypedHash(
            revokeSelfHash,
            WorkerProjectHandleRevoke.SelfHashType,
            nameof(revokeSelfHash));
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
    [JsonPropertyName("revokeSelfHash")] public TypedHash RevokeSelfHash { get; }
    [JsonPropertyName("disposition")] public string Disposition { get; }
    [JsonPropertyName("selfHash")] public TypedHash SelfHash { get; }
}

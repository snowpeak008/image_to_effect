using System.Text.Json.Serialization;

namespace VFXComposer.Protocol.Ipc;

/// <summary>Correlation receipt only; authenticated session authority remains broker-local.</summary>
public sealed record PeerSessionAccepted
{
    public PeerSessionAccepted(
        string requestId,
        string sessionId,
        string peerRole,
        string brokerInstanceId,
        long brokerGeneration,
        string processEpoch,
        IEnumerable<string> negotiatedCapabilities)
        : this(
            ProtocolVersions.Current,
            MessageKinds.PeerSessionAccepted,
            requestId,
            sessionId,
            peerRole,
            brokerInstanceId,
            brokerGeneration,
            processEpoch,
            negotiatedCapabilities?.ToArray()
                ?? throw new ArgumentNullException(nameof(negotiatedCapabilities)))
    {
    }

    [JsonConstructor]
    public PeerSessionAccepted(
        string protocolVersion,
        string messageKind,
        string requestId,
        string sessionId,
        string peerRole,
        string brokerInstanceId,
        long brokerGeneration,
        string processEpoch,
        IReadOnlyList<string> negotiatedCapabilities)
    {
        if (!string.Equals(protocolVersion, ProtocolVersions.Current, StringComparison.Ordinal))
        {
            throw new ArgumentException("Unsupported protocol version.", nameof(protocolVersion));
        }

        if (!string.Equals(messageKind, MessageKinds.PeerSessionAccepted, StringComparison.Ordinal))
        {
            throw new ArgumentException("Unexpected message kind.", nameof(messageKind));
        }

        if (brokerGeneration <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(brokerGeneration));
        }

        ProtocolVersion = protocolVersion;
        MessageKind = messageKind;
        RequestId = Guard.Token(requestId, nameof(requestId));
        SessionId = Guard.Token(sessionId, nameof(sessionId));
        PeerRole = PeerRoles.Require(peerRole, nameof(peerRole));
        BrokerInstanceId = Guard.Token(brokerInstanceId, nameof(brokerInstanceId));
        BrokerGeneration = brokerGeneration;
        ProcessEpoch = Guard.Token(processEpoch, nameof(processEpoch));
        NegotiatedCapabilities = WireModelGuard.KnownSortedTokens(
            negotiatedCapabilities,
            PeerCapabilityIds.All,
            nameof(negotiatedCapabilities));
    }

    [JsonPropertyName("protocolVersion")]
    public string ProtocolVersion { get; }

    [JsonPropertyName("messageKind")]
    public string MessageKind { get; }

    [JsonPropertyName("requestId")]
    public string RequestId { get; }

    [JsonPropertyName("sessionId")]
    public string SessionId { get; }

    [JsonPropertyName("peerRole")]
    public string PeerRole { get; }

    [JsonPropertyName("brokerInstanceId")]
    public string BrokerInstanceId { get; }

    [JsonPropertyName("brokerGeneration")]
    public long BrokerGeneration { get; }

    [JsonPropertyName("processEpoch")]
    public string ProcessEpoch { get; }

    [JsonPropertyName("negotiatedCapabilities")]
    public IReadOnlyList<string> NegotiatedCapabilities { get; }
}

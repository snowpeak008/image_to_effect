using System.Text.Json.Serialization;
using VFXComposer.Protocol.Hashing;

namespace VFXComposer.Protocol.Ipc;

/// <summary>
/// A peer claim presented on an already local pipe connection. It grants no trust:
/// the broker must compare every process claim with OS-observed peer facts.
/// </summary>
public sealed record PeerHello
{
    public const string ProcessImageIdentityType = "vfxcomposer.process-image/1";

    public PeerHello(
        string requestId,
        string peerRole,
        string peerInstanceId,
        int processId,
        string processEpoch,
        IEnumerable<string> offeredCapabilities,
        TypedHash imageIdentity)
        : this(
            ProtocolVersions.Current,
            MessageKinds.PeerHello,
            requestId,
            peerRole,
            peerInstanceId,
            processId,
            processEpoch,
            offeredCapabilities?.ToArray() ?? throw new ArgumentNullException(nameof(offeredCapabilities)),
            imageIdentity)
    {
    }

    [JsonConstructor]
    public PeerHello(
        string protocolVersion,
        string messageKind,
        string requestId,
        string peerRole,
        string peerInstanceId,
        int processId,
        string processEpoch,
        IReadOnlyList<string> offeredCapabilities,
        TypedHash imageIdentity)
    {
        if (!string.Equals(protocolVersion, ProtocolVersions.Current, StringComparison.Ordinal))
        {
            throw new ArgumentException("Unsupported protocol version.", nameof(protocolVersion));
        }

        if (!string.Equals(messageKind, MessageKinds.PeerHello, StringComparison.Ordinal))
        {
            throw new ArgumentException("Unexpected message kind.", nameof(messageKind));
        }

        ProtocolVersion = protocolVersion;
        MessageKind = messageKind;
        RequestId = Guard.Token(requestId, nameof(requestId));
        PeerRole = PeerRoles.Require(peerRole, nameof(peerRole));
        PeerInstanceId = Guard.Token(peerInstanceId, nameof(peerInstanceId));
        ProcessId = WireModelGuard.PositiveInt32(processId, nameof(processId));
        ProcessEpoch = Guard.Token(processEpoch, nameof(processEpoch));
        OfferedCapabilities = WireModelGuard.KnownSortedTokens(
            offeredCapabilities,
            PeerCapabilityIds.All,
            nameof(offeredCapabilities));
        ImageIdentity = WireModelGuard.TypedHash(
            imageIdentity,
            ProcessImageIdentityType,
            nameof(imageIdentity));
    }

    [JsonPropertyName("protocolVersion")]
    public string ProtocolVersion { get; }

    [JsonPropertyName("messageKind")]
    public string MessageKind { get; }

    [JsonPropertyName("requestId")]
    public string RequestId { get; }

    [JsonPropertyName("peerRole")]
    public string PeerRole { get; }

    [JsonPropertyName("peerInstanceId")]
    public string PeerInstanceId { get; }

    [JsonPropertyName("processId")]
    public int ProcessId { get; }

    [JsonPropertyName("processEpoch")]
    public string ProcessEpoch { get; }

    [JsonPropertyName("offeredCapabilities")]
    public IReadOnlyList<string> OfferedCapabilities { get; }

    [JsonPropertyName("imageIdentity")]
    public TypedHash ImageIdentity { get; }
}

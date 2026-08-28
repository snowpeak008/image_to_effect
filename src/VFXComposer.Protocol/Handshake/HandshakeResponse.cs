using System.Text.Json.Serialization;
using VFXComposer.Protocol.Diagnostics;

namespace VFXComposer.Protocol.Handshake;

public sealed record HandshakeResponse
{
    [JsonConstructor]
    public HandshakeResponse(
        string protocolVersion,
        string messageKind,
        string requestId,
        string serverInstanceId,
        bool accepted,
        IReadOnlyList<string> negotiatedCapabilities,
        StableDiagnostic? diagnostic)
    {
        if (!string.Equals(protocolVersion, ProtocolVersions.Current, StringComparison.Ordinal))
        {
            throw new ArgumentException("Unsupported protocol version.", nameof(protocolVersion));
        }

        if (!string.Equals(messageKind, MessageKinds.HandshakeResponse, StringComparison.Ordinal))
        {
            throw new ArgumentException("Unexpected message kind.", nameof(messageKind));
        }

        var capabilities = negotiatedCapabilities?.ToArray()
            ?? throw new ArgumentNullException(nameof(negotiatedCapabilities));
        if (capabilities.Length > 64 ||
            capabilities.Any(capability => !CapabilityIds.IsKnown(capability)) ||
            capabilities.Distinct(StringComparer.Ordinal).Count() != capabilities.Length ||
            !capabilities.SequenceEqual(capabilities.Order(StringComparer.Ordinal), StringComparer.Ordinal))
        {
            throw new ArgumentException("Negotiated capabilities must be known, unique and sorted.", nameof(negotiatedCapabilities));
        }

        if ((accepted && diagnostic is not null) ||
            (!accepted && (diagnostic is null || capabilities.Length != 0)))
        {
            throw new ArgumentException("Handshake outcome fields are inconsistent.");
        }

        ProtocolVersion = protocolVersion;
        MessageKind = messageKind;
        RequestId = Guard.Token(requestId, nameof(requestId));
        ServerInstanceId = Guard.Token(serverInstanceId, nameof(serverInstanceId));
        Accepted = accepted;
        NegotiatedCapabilities = Array.AsReadOnly(capabilities);
        Diagnostic = diagnostic;
    }

    [JsonPropertyName("protocolVersion")]
    public string ProtocolVersion { get; }

    [JsonPropertyName("messageKind")]
    public string MessageKind { get; }

    [JsonPropertyName("requestId")]
    public string RequestId { get; }

    [JsonPropertyName("serverInstanceId")]
    public string ServerInstanceId { get; }

    [JsonPropertyName("accepted")]
    public bool Accepted { get; }

    [JsonPropertyName("negotiatedCapabilities")]
    public IReadOnlyList<string> NegotiatedCapabilities { get; }

    [JsonPropertyName("diagnostic")]
    public StableDiagnostic? Diagnostic { get; }

    public static HandshakeResponse Accept(
        string requestId,
        string serverInstanceId,
        IEnumerable<string> negotiatedCapabilities) =>
        new(
            ProtocolVersions.Current,
            MessageKinds.HandshakeResponse,
            requestId,
            serverInstanceId,
            accepted: true,
            negotiatedCapabilities?.ToArray()
                ?? throw new ArgumentNullException(nameof(negotiatedCapabilities)),
            diagnostic: null);

    public static HandshakeResponse Reject(
        string requestId,
        string serverInstanceId,
        StableDiagnostic diagnostic) =>
        new(
            ProtocolVersions.Current,
            MessageKinds.HandshakeResponse,
            requestId,
            serverInstanceId,
            accepted: false,
            negotiatedCapabilities: Array.Empty<string>(),
            diagnostic);
}

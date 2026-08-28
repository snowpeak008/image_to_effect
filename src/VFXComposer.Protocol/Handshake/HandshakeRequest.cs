using System.Text.Json.Serialization;

namespace VFXComposer.Protocol.Handshake;

public sealed record HandshakeRequest
{
    public HandshakeRequest(
        string requestId,
        string clientInstanceId,
        IEnumerable<string> offeredCapabilities)
        : this(
            ProtocolVersions.Current,
            MessageKinds.HandshakeRequest,
            requestId,
            clientInstanceId,
            offeredCapabilities?.ToArray()
                ?? throw new ArgumentNullException(nameof(offeredCapabilities)))
    {
    }

    [JsonConstructor]
    public HandshakeRequest(
        string protocolVersion,
        string messageKind,
        string requestId,
        string clientInstanceId,
        IReadOnlyList<string> offeredCapabilities)
    {
        if (!string.Equals(protocolVersion, ProtocolVersions.Current, StringComparison.Ordinal))
        {
            throw new ArgumentException("Unsupported protocol version.", nameof(protocolVersion));
        }

        if (!string.Equals(messageKind, MessageKinds.HandshakeRequest, StringComparison.Ordinal))
        {
            throw new ArgumentException("Unexpected message kind.", nameof(messageKind));
        }

        ProtocolVersion = protocolVersion;
        MessageKind = messageKind;
        RequestId = Guard.Token(requestId, nameof(requestId));
        ClientInstanceId = Guard.Token(clientInstanceId, nameof(clientInstanceId));
        OfferedCapabilities = ValidateCapabilities(offeredCapabilities, nameof(offeredCapabilities));
    }

    [JsonPropertyName("protocolVersion")]
    public string ProtocolVersion { get; }

    [JsonPropertyName("messageKind")]
    public string MessageKind { get; }

    [JsonPropertyName("requestId")]
    public string RequestId { get; }

    [JsonPropertyName("clientInstanceId")]
    public string ClientInstanceId { get; }

    [JsonPropertyName("offeredCapabilities")]
    public IReadOnlyList<string> OfferedCapabilities { get; }

    private static IReadOnlyList<string> ValidateCapabilities(
        IEnumerable<string> capabilities,
        string parameterName)
    {
        ArgumentNullException.ThrowIfNull(capabilities, parameterName);
        var result = capabilities
            .Select(value => Guard.Token(value, parameterName))
            .ToArray();
        if (result.Length > 64 || result.Distinct(StringComparer.Ordinal).Count() != result.Length)
        {
            throw new ArgumentException("Capability list is too large or contains duplicates.", parameterName);
        }

        return Array.AsReadOnly(result);
    }
}

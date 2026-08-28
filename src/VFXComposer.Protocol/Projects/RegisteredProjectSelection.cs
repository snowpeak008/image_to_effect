using System.Text.Json.Serialization;
using VFXComposer.Protocol.Hashing;

namespace VFXComposer.Protocol.Projects;

/// <summary>
/// A bounded registered-project correlation selected from an already admitted registry.
/// Decoding this data neither issues a lease nor establishes trust or authority.
/// </summary>
public sealed record RegisteredProjectSelection
{
    public const string ProjectIdentityType = "vfxcomposer.project-identity/1";

    [JsonConstructor]
    public RegisteredProjectSelection(
        string protocolVersion,
        string messageKind,
        string requestId,
        string registeredProjectId,
        TypedHash projectIdentity,
        long brokerGeneration,
        long registrationGeneration)
    {
        if (!string.Equals(protocolVersion, ProtocolVersions.Current, StringComparison.Ordinal))
        {
            throw new ArgumentException("Unsupported protocol version.", nameof(protocolVersion));
        }

        if (!string.Equals(messageKind, MessageKinds.RegisteredProjectSelection, StringComparison.Ordinal))
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

        ProtocolVersion = protocolVersion;
        MessageKind = messageKind;
        RequestId = Guard.Token(requestId, nameof(requestId));
        RegisteredProjectId = Guard.Token(registeredProjectId, nameof(registeredProjectId));
        ProjectIdentity = WireModelGuard.TypedHash(
            projectIdentity,
            ProjectIdentityType,
            nameof(projectIdentity));
        BrokerGeneration = brokerGeneration;
        RegistrationGeneration = registrationGeneration;
    }

    [JsonPropertyName("protocolVersion")] public string ProtocolVersion { get; }
    [JsonPropertyName("messageKind")] public string MessageKind { get; }
    [JsonPropertyName("requestId")] public string RequestId { get; }
    [JsonPropertyName("registeredProjectId")] public string RegisteredProjectId { get; }
    [JsonPropertyName("projectIdentity")] public TypedHash ProjectIdentity { get; }
    [JsonPropertyName("brokerGeneration")] public long BrokerGeneration { get; }
    [JsonPropertyName("registrationGeneration")] public long RegistrationGeneration { get; }
}

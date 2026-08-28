using System.Text.Json.Serialization;
using VFXComposer.Protocol.Hashing;
using VFXComposer.Protocol.Registration;

namespace VFXComposer.Protocol.Queries;

/// <summary>A registry-key query. It deliberately contains no path or filename.</summary>
public sealed record ReadDocumentQuery
{
    [JsonConstructor]
    public ReadDocumentQuery(
        string protocolVersion,
        string messageKind,
        string requestId,
        string leaseId,
        TypedHash projectIdentity,
        long leaseGeneration,
        string documentKind,
        string documentId,
        TypedHash? expectedContentHash)
    {
        if (!string.Equals(protocolVersion, ProtocolVersions.Current, StringComparison.Ordinal))
        {
            throw new ArgumentException("Unsupported protocol version.", nameof(protocolVersion));
        }

        if (!string.Equals(messageKind, MessageKinds.ReadDocumentQuery, StringComparison.Ordinal))
        {
            throw new ArgumentException("Unexpected message kind.", nameof(messageKind));
        }

        if (leaseGeneration <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(leaseGeneration));
        }

        ProtocolVersion = protocolVersion;
        MessageKind = messageKind;
        RequestId = Guard.Token(requestId, nameof(requestId));
        LeaseId = Guard.Token(leaseId, nameof(leaseId));
        ProjectIdentity = WireModelGuard.TypedHash(
            projectIdentity,
            ProjectRegistrationAttestation.ProjectIdentityType,
            nameof(projectIdentity));
        LeaseGeneration = leaseGeneration;
        DocumentKind = DocumentKinds.Require(documentKind, nameof(documentKind));
        DocumentId = DocumentKinds.RequireDocumentId(DocumentKind, documentId, nameof(documentId));
        ExpectedContentHash = expectedContentHash is null
            ? null
            : WireModelGuard.TypedHash(
                expectedContentHash,
                ReadDocumentResult.ContentHashType,
                nameof(expectedContentHash));
    }

    [JsonPropertyName("protocolVersion")] public string ProtocolVersion { get; }
    [JsonPropertyName("messageKind")] public string MessageKind { get; }
    [JsonPropertyName("requestId")] public string RequestId { get; }
    [JsonPropertyName("leaseId")] public string LeaseId { get; }
    [JsonPropertyName("projectIdentity")] public TypedHash ProjectIdentity { get; }
    [JsonPropertyName("leaseGeneration")] public long LeaseGeneration { get; }
    [JsonPropertyName("documentKind")] public string DocumentKind { get; }
    [JsonPropertyName("documentId")] public string DocumentId { get; }
    [JsonPropertyName("expectedContentHash")] public TypedHash? ExpectedContentHash { get; }
}

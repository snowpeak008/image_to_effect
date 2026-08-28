using System.Text.Json.Serialization;
using VFXComposer.Protocol.Diagnostics;
using VFXComposer.Protocol.Hashing;
using VFXComposer.Protocol.Registration;

namespace VFXComposer.Protocol.Queries;

public sealed record ReadDocumentResult
{
    public const string ContentHashType = "vfxcomposer.document-content/1";
    public const int MaximumDecodedBytes = 512 * 1024;

    [JsonConstructor]
    public ReadDocumentResult(
        string protocolVersion,
        string messageKind,
        string requestId,
        bool accepted,
        TypedHash projectIdentity,
        string documentKind,
        string documentId,
        TypedHash? contentHash,
        int byteLength,
        string? contentBase64,
        StableDiagnostic? diagnostic)
    {
        if (!string.Equals(protocolVersion, ProtocolVersions.Current, StringComparison.Ordinal))
        {
            throw new ArgumentException("Unsupported protocol version.", nameof(protocolVersion));
        }

        if (!string.Equals(messageKind, MessageKinds.ReadDocumentResult, StringComparison.Ordinal))
        {
            throw new ArgumentException("Unexpected message kind.", nameof(messageKind));
        }

        ProtocolVersion = protocolVersion;
        MessageKind = messageKind;
        RequestId = Guard.Token(requestId, nameof(requestId));
        Accepted = accepted;
        ProjectIdentity = WireModelGuard.TypedHash(
            projectIdentity,
            ProjectRegistrationAttestation.ProjectIdentityType,
            nameof(projectIdentity));
        DocumentKind = DocumentKinds.Require(documentKind, nameof(documentKind));
        DocumentId = DocumentKinds.RequireDocumentId(DocumentKind, documentId, nameof(documentId));

        if (!accepted)
        {
            if (contentHash is not null || contentBase64 is not null || byteLength != 0 || diagnostic is null)
            {
                throw new ArgumentException("Rejected document results must contain only a stable diagnostic.");
            }

            ContentHash = null;
            ByteLength = 0;
            ContentBase64 = null;
            Diagnostic = diagnostic;
            return;
        }

        if (diagnostic is not null || contentHash is null || contentBase64 is null ||
            byteLength < 0 || byteLength > MaximumDecodedBytes)
        {
            throw new ArgumentException("Accepted document result shape is invalid.");
        }

        byte[] content;
        try
        {
            content = Convert.FromBase64String(contentBase64);
        }
        catch (FormatException exception)
        {
            throw new ArgumentException("Document content is not canonical base64.", nameof(contentBase64), exception);
        }

        if (content.Length != byteLength ||
            !string.Equals(Convert.ToBase64String(content), contentBase64, StringComparison.Ordinal))
        {
            throw new ArgumentException("Document content length or base64 encoding is invalid.");
        }

        var expected = TypedHash.Compute(ContentHashType, content);
        var validatedHash = WireModelGuard.TypedHash(contentHash, ContentHashType, nameof(contentHash));
        if (!expected.FixedTimeEquals(validatedHash))
        {
            throw new ArgumentException("Document content hash does not match its bytes.", nameof(contentHash));
        }

        ContentHash = validatedHash;
        ByteLength = byteLength;
        ContentBase64 = contentBase64;
        Diagnostic = null;
    }

    [JsonPropertyName("protocolVersion")] public string ProtocolVersion { get; }
    [JsonPropertyName("messageKind")] public string MessageKind { get; }
    [JsonPropertyName("requestId")] public string RequestId { get; }
    [JsonPropertyName("accepted")] public bool Accepted { get; }
    [JsonPropertyName("projectIdentity")] public TypedHash ProjectIdentity { get; }
    [JsonPropertyName("documentKind")] public string DocumentKind { get; }
    [JsonPropertyName("documentId")] public string DocumentId { get; }
    [JsonPropertyName("contentHash")] public TypedHash? ContentHash { get; }
    [JsonPropertyName("byteLength")] public int ByteLength { get; }
    [JsonPropertyName("contentBase64")] public string? ContentBase64 { get; }
    [JsonPropertyName("diagnostic")] public StableDiagnostic? Diagnostic { get; }
}

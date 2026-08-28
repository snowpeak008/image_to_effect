using System.Text.Json.Serialization;
using VFXComposer.Protocol.Hashing;

namespace VFXComposer.Protocol.Commands;

/// <summary>Names opaque candidate and preview identities without defining playback behavior.</summary>
public sealed record OpenPreviewJobCommand
{
    public const string SelfHashType = "vfxcomposer.command.open-preview-job/1";

    [JsonConstructor]
    public OpenPreviewJobCommand(
        string protocolVersion,
        string messageKind,
        CommandEnvelope envelope,
        string candidateId,
        TypedHash candidateIdentity,
        string previewId,
        TypedHash previewIdentity,
        TypedHash selfHash)
    {
        CommandWireGuard.RequireHeader(protocolVersion, messageKind, MessageKinds.OpenPreviewJobCommand, envelope, CommandKinds.OpenPreviewJob);
        ProtocolVersion = protocolVersion;
        MessageKind = messageKind;
        Envelope = envelope;
        CandidateId = Guard.Token(candidateId, nameof(candidateId), 96);
        CandidateIdentity = WireModelGuard.TypedHash(candidateIdentity, CommandContentHashTypes.CandidateIdentity, nameof(candidateIdentity));
        PreviewId = Guard.Token(previewId, nameof(previewId), 96);
        PreviewIdentity = WireModelGuard.TypedHash(previewIdentity, CommandContentHashTypes.PreviewIdentity, nameof(previewIdentity));
        SelfHash = CommandWireGuard.RequireSelfHash(selfHash, SelfHashType, nameof(selfHash));
    }

    [JsonPropertyName("protocolVersion")] public string ProtocolVersion { get; }
    [JsonPropertyName("messageKind")] public string MessageKind { get; }
    [JsonPropertyName("envelope")] public CommandEnvelope Envelope { get; }
    [JsonPropertyName("candidateId")] public string CandidateId { get; }
    [JsonPropertyName("candidateIdentity")] public TypedHash CandidateIdentity { get; }
    [JsonPropertyName("previewId")] public string PreviewId { get; }
    [JsonPropertyName("previewIdentity")] public TypedHash PreviewIdentity { get; }
    [JsonPropertyName("selfHash")] public TypedHash SelfHash { get; }
}

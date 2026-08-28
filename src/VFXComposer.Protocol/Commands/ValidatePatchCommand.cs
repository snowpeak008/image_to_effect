using System.Text.Json.Serialization;
using VFXComposer.Protocol.Hashing;

namespace VFXComposer.Protocol.Commands;

/// <summary>Opaque patch and candidate identities only; raw patch JSON is excluded.</summary>
public sealed record ValidatePatchCommand
{
    public const string SelfHashType = "vfxcomposer.command.validate-patch/1";

    [JsonConstructor]
    public ValidatePatchCommand(
        string protocolVersion,
        string messageKind,
        CommandEnvelope envelope,
        string patchId,
        TypedHash patchContentHash,
        string targetCandidateId,
        TypedHash targetCandidateIdentity,
        TypedHash selfHash)
    {
        CommandWireGuard.RequireHeader(protocolVersion, messageKind, MessageKinds.ValidatePatchCommand, envelope, CommandKinds.ValidatePatch);
        ProtocolVersion = protocolVersion;
        MessageKind = messageKind;
        Envelope = envelope;
        PatchId = Guard.Token(patchId, nameof(patchId), 96);
        PatchContentHash = WireModelGuard.TypedHash(patchContentHash, CommandContentHashTypes.PatchContent, nameof(patchContentHash));
        TargetCandidateId = Guard.Token(targetCandidateId, nameof(targetCandidateId), 96);
        TargetCandidateIdentity = WireModelGuard.TypedHash(targetCandidateIdentity, CommandContentHashTypes.CandidateIdentity, nameof(targetCandidateIdentity));
        SelfHash = CommandWireGuard.RequireSelfHash(selfHash, SelfHashType, nameof(selfHash));
    }

    [JsonPropertyName("protocolVersion")] public string ProtocolVersion { get; }
    [JsonPropertyName("messageKind")] public string MessageKind { get; }
    [JsonPropertyName("envelope")] public CommandEnvelope Envelope { get; }
    [JsonPropertyName("patchId")] public string PatchId { get; }
    [JsonPropertyName("patchContentHash")] public TypedHash PatchContentHash { get; }
    [JsonPropertyName("targetCandidateId")] public string TargetCandidateId { get; }
    [JsonPropertyName("targetCandidateIdentity")] public TypedHash TargetCandidateIdentity { get; }
    [JsonPropertyName("selfHash")] public TypedHash SelfHash { get; }
}

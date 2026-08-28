using System.Text.Json.Serialization;
using VFXComposer.Protocol.Hashing;

namespace VFXComposer.Protocol.Commands;

/// <summary>Patch-apply identity data only; it contains no raw patch or destination.</summary>
public sealed record ApplyPatchCommand
{
    public const string SelfHashType = "vfxcomposer.command.apply-patch/1";

    [JsonConstructor]
    public ApplyPatchCommand(
        string protocolVersion,
        string messageKind,
        CommandEnvelope envelope,
        string patchId,
        TypedHash patchValidationHash,
        string targetCandidateId,
        TypedHash targetCandidateIdentity,
        TypedHash selfHash)
    {
        CommandWireGuard.RequireHeader(protocolVersion, messageKind, MessageKinds.ApplyPatchCommand, envelope, CommandKinds.ApplyPatch);
        ProtocolVersion = protocolVersion;
        MessageKind = messageKind;
        Envelope = envelope;
        PatchId = Guard.Token(patchId, nameof(patchId), 96);
        PatchValidationHash = WireModelGuard.TypedHash(patchValidationHash, CommandContentHashTypes.PatchValidation, nameof(patchValidationHash));
        TargetCandidateId = Guard.Token(targetCandidateId, nameof(targetCandidateId), 96);
        TargetCandidateIdentity = WireModelGuard.TypedHash(targetCandidateIdentity, CommandContentHashTypes.CandidateIdentity, nameof(targetCandidateIdentity));
        SelfHash = CommandWireGuard.RequireSelfHash(selfHash, SelfHashType, nameof(selfHash));
    }

    [JsonPropertyName("protocolVersion")] public string ProtocolVersion { get; }
    [JsonPropertyName("messageKind")] public string MessageKind { get; }
    [JsonPropertyName("envelope")] public CommandEnvelope Envelope { get; }
    [JsonPropertyName("patchId")] public string PatchId { get; }
    [JsonPropertyName("patchValidationHash")] public TypedHash PatchValidationHash { get; }
    [JsonPropertyName("targetCandidateId")] public string TargetCandidateId { get; }
    [JsonPropertyName("targetCandidateIdentity")] public TypedHash TargetCandidateIdentity { get; }
    [JsonPropertyName("selfHash")] public TypedHash SelfHash { get; }
}

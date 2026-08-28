using System.Text.Json.Serialization;
using VFXComposer.Protocol.Hashing;

namespace VFXComposer.Protocol.Commands;

/// <summary>Candidate build identity data without recipe bytes or an output location.</summary>
public sealed record BuildCandidateCommand
{
    public const string SelfHashType = "vfxcomposer.command.build-candidate/1";

    [JsonConstructor]
    public BuildCandidateCommand(
        string protocolVersion,
        string messageKind,
        CommandEnvelope envelope,
        string recipeId,
        TypedHash recipeValidationHash,
        TypedHash buildDefinitionHash,
        TypedHash candidateIdentity,
        TypedHash selfHash)
    {
        CommandWireGuard.RequireHeader(protocolVersion, messageKind, MessageKinds.BuildCandidateCommand, envelope, CommandKinds.BuildCandidate);
        ProtocolVersion = protocolVersion;
        MessageKind = messageKind;
        Envelope = envelope;
        RecipeId = Guard.Token(recipeId, nameof(recipeId), 96);
        RecipeValidationHash = WireModelGuard.TypedHash(recipeValidationHash, CommandContentHashTypes.RecipeValidation, nameof(recipeValidationHash));
        BuildDefinitionHash = WireModelGuard.TypedHash(buildDefinitionHash, CommandContentHashTypes.BuildDefinition, nameof(buildDefinitionHash));
        CandidateIdentity = WireModelGuard.TypedHash(candidateIdentity, CommandContentHashTypes.CandidateIdentity, nameof(candidateIdentity));
        SelfHash = CommandWireGuard.RequireSelfHash(selfHash, SelfHashType, nameof(selfHash));
    }

    [JsonPropertyName("protocolVersion")] public string ProtocolVersion { get; }
    [JsonPropertyName("messageKind")] public string MessageKind { get; }
    [JsonPropertyName("envelope")] public CommandEnvelope Envelope { get; }
    [JsonPropertyName("recipeId")] public string RecipeId { get; }
    [JsonPropertyName("recipeValidationHash")] public TypedHash RecipeValidationHash { get; }
    [JsonPropertyName("buildDefinitionHash")] public TypedHash BuildDefinitionHash { get; }
    [JsonPropertyName("candidateIdentity")] public TypedHash CandidateIdentity { get; }
    [JsonPropertyName("selfHash")] public TypedHash SelfHash { get; }
}

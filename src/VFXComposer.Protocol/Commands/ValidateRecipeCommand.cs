using System.Text.Json.Serialization;
using VFXComposer.Protocol.Hashing;

namespace VFXComposer.Protocol.Commands;

/// <summary>Opaque recipe identities only; raw recipe JSON is never a formal ticket.</summary>
public sealed record ValidateRecipeCommand
{
    public const string SelfHashType = "vfxcomposer.command.validate-recipe/1";

    [JsonConstructor]
    public ValidateRecipeCommand(
        string protocolVersion,
        string messageKind,
        CommandEnvelope envelope,
        string recipeId,
        TypedHash recipeContentHash,
        TypedHash recipeContractHash,
        TypedHash selfHash)
    {
        CommandWireGuard.RequireHeader(protocolVersion, messageKind, MessageKinds.ValidateRecipeCommand, envelope, CommandKinds.ValidateRecipe);
        ProtocolVersion = protocolVersion;
        MessageKind = messageKind;
        Envelope = envelope;
        RecipeId = Guard.Token(recipeId, nameof(recipeId), 96);
        RecipeContentHash = WireModelGuard.TypedHash(recipeContentHash, CommandContentHashTypes.RecipeContent, nameof(recipeContentHash));
        RecipeContractHash = WireModelGuard.TypedHash(recipeContractHash, CommandContentHashTypes.RecipeContract, nameof(recipeContractHash));
        SelfHash = CommandWireGuard.RequireSelfHash(selfHash, SelfHashType, nameof(selfHash));
    }

    [JsonPropertyName("protocolVersion")] public string ProtocolVersion { get; }
    [JsonPropertyName("messageKind")] public string MessageKind { get; }
    [JsonPropertyName("envelope")] public CommandEnvelope Envelope { get; }
    [JsonPropertyName("recipeId")] public string RecipeId { get; }
    [JsonPropertyName("recipeContentHash")] public TypedHash RecipeContentHash { get; }
    [JsonPropertyName("recipeContractHash")] public TypedHash RecipeContractHash { get; }
    [JsonPropertyName("selfHash")] public TypedHash SelfHash { get; }
}

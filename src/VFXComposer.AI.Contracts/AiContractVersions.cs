namespace VFXComposer.AI.Contracts;

/// <summary>Versions owned by the AI provider contract boundary.</summary>
public static class AiContractVersions
{
    public const int ProviderConfigurationFormatVersion = 1;
    public const string Gateway = "vfxcomposer.ai.gateway/1";
    public const string ChatRequest = "vfxcomposer.ai.chat-request/1";
    public const string ImageGenerationRequest = "vfxcomposer.ai.image-generation-request/1";
    public const string RecipeGenerationRequest = "vfxcomposer.ai.recipe-generation-request/1";

    /// <summary>
    /// Identifies the recipe system prompt carried by every draft record. Revision 2 replaced the legacy-exempt
    /// reference recipe with a strict-budget one and states the build budget explicitly.
    /// </summary>
    public const string RecipeSystemPrompt = "vfxcomposer.ai.recipe-prompt/2";

    public const int RecipeDraftRecordFormatVersion = 1;
}

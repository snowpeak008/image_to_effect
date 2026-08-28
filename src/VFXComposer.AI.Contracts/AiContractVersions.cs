namespace VFXComposer.AI.Contracts;

/// <summary>Versions owned by the AI provider contract boundary.</summary>
public static class AiContractVersions
{
    public const int ProviderConfigurationFormatVersion = 1;
    public const string Gateway = "vfxcomposer.ai.gateway/1";
    public const string ChatRequest = "vfxcomposer.ai.chat-request/1";
    public const string ImageGenerationRequest = "vfxcomposer.ai.image-generation-request/1";
}

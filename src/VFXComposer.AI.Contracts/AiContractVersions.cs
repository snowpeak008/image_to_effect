namespace VFXComposer.AI.Contracts;

/// <summary>Versions owned by the AI provider contract boundary.</summary>
public static class AiContractVersions
{
    public const int ProviderConfigurationFormatVersion = 1;
    public const string Gateway = "vfxcomposer.ai.gateway/1";
    public const string ChatRequest = "vfxcomposer.ai.chat-request/1";
    public const string ImageGenerationRequest = "vfxcomposer.ai.image-generation-request/1";
    public const string RecipeGenerationRequest = "vfxcomposer.ai.recipe-generation-request/1";
    public const string RecipeRefinementRequest = "vfxcomposer.ai.recipe-refinement-request/1";

    /// <summary>
    /// Identifies the prompt assembler revision that leads the composite prompt version carried by every draft
    /// record (F8b1). The assembler composes it with each registered fragment's id and version; bump this
    /// revision when the assembly mechanics (packing, splitting, ordering) change rather than fragment content.
    /// Lineage: revision 1 of the assembler supersedes the monolithic <c>vfxcomposer.ai.recipe-prompt/2</c>
    /// template version (whose revision 2 replaced the legacy-exempt reference recipe with a strict-budget one).
    /// </summary>
    public const string RecipePromptAssembler = "vfxcomposer.ai.recipe-prompt-assembler/1";

    /// <summary>
    /// Wire version of the retained recipe draft store. Version 2 added the version-chain provenance
    /// (lineage, parent, ordinal, origin) and the per-lineage revision watermark; version 1 files are refused
    /// with <see cref="Recipes.RecipeDraftStoreErrorCode.UnsupportedVersion"/> and never migrated (REQ-004 §7.4).
    /// </summary>
    public const int RecipeDraftRecordFormatVersion = 2;
}

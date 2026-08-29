namespace VFXComposer.Batch.Core;

/// <summary>
/// What this build can actually execute. A manifest asking for an entry kind the build cannot
/// run is rejected as a whole (REQ-002 §12 fail-closed rows) instead of being partially enqueued.
/// </summary>
public sealed record BatchCapabilityProfile(bool PromptGenerationAvailable, bool RecipeBuildSupported)
{
    /// <summary>
    /// The shipped profile: prompt entries execute on the F1 generation channel and recipe entries
    /// execute on the restricted Unity build path, so both manifest kinds are accepted.
    /// </summary>
    public static BatchCapabilityProfile GenerationAndRecipeBuild { get; } = new(PromptGenerationAvailable: true, RecipeBuildSupported: true);

    /// <summary>
    /// Prompt entries execute on the F1 generation channel; recipe build entries have no executor,
    /// so they are refused with a stable code instead of being partially enqueued.
    /// </summary>
    public static BatchCapabilityProfile GenerationOnly { get; } = new(PromptGenerationAvailable: true, RecipeBuildSupported: false);

    /// <summary>
    /// The generation channel is unbound, but the restricted build path does not depend on it, so a
    /// manifest of recipe entries alone still runs.
    /// </summary>
    public static BatchCapabilityProfile RecipeBuildOnly { get; } = new(PromptGenerationAvailable: false, RecipeBuildSupported: true);

    /// <summary>Nothing is executable; every manifest is refused as a whole.</summary>
    public static BatchCapabilityProfile GenerationUnavailable { get; } = new(PromptGenerationAvailable: false, RecipeBuildSupported: false);
}

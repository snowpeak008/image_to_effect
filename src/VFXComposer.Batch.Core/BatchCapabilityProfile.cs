namespace VFXComposer.Batch.Core;

/// <summary>
/// What this build can actually execute. A manifest asking for an entry kind the build cannot
/// run is rejected as a whole (REQ-002 §12 fail-closed rows) instead of being partially enqueued.
/// </summary>
public sealed record BatchCapabilityProfile(bool PromptGenerationAvailable, bool RecipeBuildSupported)
{
    /// <summary>
    /// The shipped profile: prompt entries execute on the F1 generation channel; recipe build
    /// entries have no executor in this build, so they are refused with a stable code.
    /// </summary>
    public static BatchCapabilityProfile GenerationOnly { get; } = new(PromptGenerationAvailable: true, RecipeBuildSupported: false);

    /// <summary>The same profile with an unbound generation channel.</summary>
    public static BatchCapabilityProfile GenerationUnavailable { get; } = new(PromptGenerationAvailable: false, RecipeBuildSupported: false);
}

namespace VFXComposer.Batch.Core;

/// <summary>
/// Closed job-kind vocabulary this execution layer submits. Kinds are queue tokens, so they use
/// the token alphabet rather than the slash-suffixed schema style used for document versions.
/// </summary>
public static class BatchJobKinds
{
    /// <summary>One prompt entry generating a Recipe draft on the F1 channel.</summary>
    public const string RecipeGeneration = "vfx.recipe.generate.v1";
}

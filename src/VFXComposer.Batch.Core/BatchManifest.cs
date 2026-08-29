using System.Collections.Frozen;
using VFXComposer.Jobs;

namespace VFXComposer.Batch.Core;

/// <summary>Bounds of the <c>vfxcomposer.batch-manifest/1</c> schema (REQ-002 §5.3).</summary>
public static class BatchManifestLimits
{
    public const string SchemaVersion = "vfxcomposer.batch-manifest/1";
    public const int MaximumManifestBytes = 512 * 1024;
    public const int MaximumTokenLength = 96;
    public const int MinimumItemCount = 1;
    public const int MaximumItemCount = 64;
    public const int MaximumPromptUtf8Bytes = 8 * 1024;
    public const int MaximumConstraintValueLength = 96;
    public const int MaximumRecipePathLength = 256;
}

/// <summary>Closed item-kind vocabulary.</summary>
public static class BatchItemKinds
{
    public const string Prompt = "prompt";
    public const string Recipe = "recipe";

    private static readonly FrozenSet<string> Known =
        new[] { Prompt, Recipe }.ToFrozenSet(StringComparer.Ordinal);

    public static IReadOnlySet<string> All => Known;

    public static bool IsKnown(string value) => Known.Contains(value);
}

/// <summary>Closed manifest failure-policy vocabulary; maps onto the queue-side policy words.</summary>
public static class BatchFailurePolicies
{
    public const string Continue = "continue";
    public const string Abort = "abort";

    private static readonly FrozenSet<string> Known =
        new[] { Continue, Abort }.ToFrozenSet(StringComparer.Ordinal);

    public static IReadOnlySet<string> All => Known;

    public static bool IsKnown(string value) => Known.Contains(value);

    /// <summary>Translates the manifest word into the persisted queue policy (<see cref="JobBatchPolicies"/>).</summary>
    public static string ToQueuePolicy(string policy) => policy switch
    {
        Continue => JobBatchPolicies.Continue,
        Abort => JobBatchPolicies.Abort,
        _ => throw new ArgumentOutOfRangeException(nameof(policy)),
    };
}

/// <summary>Closed constraint key vocabulary shared by <c>defaults</c> and <c>items[].constraints</c>.</summary>
public static class BatchConstraintKeys
{
    public const string Archetype = "archetype";
    public const string Dimension = "dimension";
    public const string Element = "element";
    public const string Style = "style";
    public const string TargetProfile = "targetProfile";
    public const string RandomSeed = "randomSeed";

    private static readonly FrozenSet<string> Known =
        new[] { Archetype, Dimension, Element, Style, TargetProfile, RandomSeed }.ToFrozenSet(StringComparer.Ordinal);

    public static IReadOnlySet<string> All => Known;

    public static bool IsKnown(string value) => Known.Contains(value);
}

/// <summary>Closed dimension vocabulary.</summary>
public static class BatchDimensions
{
    public const string TwoDimensional = "2d";
    public const string ThreeDimensional = "3d";

    private static readonly FrozenSet<string> Known =
        new[] { TwoDimensional, ThreeDimensional }.ToFrozenSet(StringComparer.Ordinal);

    public static IReadOnlySet<string> All => Known;

    public static bool IsKnown(string value) => Known.Contains(value);
}

/// <summary>Whitelisted generation constraints after manifest defaults have been merged in.</summary>
public sealed record BatchConstraints(
    string? Archetype,
    string? Dimension,
    string? Element,
    string? Style,
    string? TargetProfile,
    int? RandomSeed)
{
    public static BatchConstraints Empty { get; } = new(null, null, null, null, null, null);

    /// <summary>Item values win over manifest defaults; absent item values inherit the default.</summary>
    public BatchConstraints InheritFrom(BatchConstraints defaults) => new(
        Archetype ?? defaults.Archetype,
        Dimension ?? defaults.Dimension,
        Element ?? defaults.Element,
        Style ?? defaults.Style,
        TargetProfile ?? defaults.TargetProfile,
        RandomSeed ?? defaults.RandomSeed);
}

/// <summary>One manifest entry. Exactly one of <see cref="Prompt"/> and <see cref="RecipePath"/> is set.</summary>
public sealed record BatchManifestItem(
    string ItemId,
    string Kind,
    string? Prompt,
    string? RecipePath,
    BatchConstraints Constraints)
{
    public override string ToString() => "BatchManifestItem(" + ItemId + "," + Kind + ")";
}

/// <summary>A parsed and validated <c>vfxcomposer.batch-manifest/1</c> document.</summary>
public sealed record BatchManifest(
    string SchemaVersion,
    string BatchId,
    string FailurePolicy,
    IReadOnlyList<BatchManifestItem> Items)
{
    public override string ToString() => "BatchManifest(" + BatchId + "," + Items.Count + ")";
}

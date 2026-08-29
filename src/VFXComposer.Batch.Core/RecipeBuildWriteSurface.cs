namespace VFXComposer.Batch.Core;

/// <summary>
/// Execution-layer restatement of the closed three-member project write surface. The Unity build
/// entry point owns the authoritative check before it writes anything; this twin exists so a
/// reported success whose targets fall outside the clause is refused here too, without the
/// execution layer needing to reference Unity code.
/// </summary>
public static class RecipeBuildWriteSurface
{
    public const string GeneratedRoot = "Assets/VFX/Generated";
    public const string OwnershipManifestRoot = "ProjectSettings/VFXComposer/BuildManifests";
    public const string ProvenanceRecipeRoot = "Assets/VFX/Recipes";

    /// <summary>Longest accepted effect id, matching the entry point's own bound.</summary>
    public const int MaximumEffectIdLength = 64;

    private static readonly HashSet<string> ReservedDeviceNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "con", "prn", "aux", "nul", "clock$",
        "com1", "com2", "com3", "com4", "com5", "com6", "com7", "com8", "com9",
        "lpt1", "lpt2", "lpt3", "lpt4", "lpt5", "lpt6", "lpt7", "lpt8", "lpt9",
    };

    /// <summary>True when the three reported members are exactly the ones this effect id allows.</summary>
    public static bool DescribesExactly(
        string? effectId,
        string? prefabPath,
        string? ownershipManifestPath,
        string? provenanceRecipePath)
    {
        if (!IsAcceptedEffectId(effectId))
        {
            return false;
        }

        var assetRoot = GeneratedRoot + "/" + effectId + "/";
        return prefabPath is not null &&
               prefabPath.StartsWith(assetRoot, StringComparison.Ordinal) &&
               prefabPath.EndsWith(".prefab", StringComparison.Ordinal) &&
               !prefabPath.AsSpan(assetRoot.Length).Contains('/') &&
               string.Equals(ownershipManifestPath, OwnershipManifestRoot + "/" + effectId + ".manifest.json", StringComparison.Ordinal) &&
               string.Equals(provenanceRecipePath, ProvenanceRecipeRoot + "/" + effectId + ".json", StringComparison.Ordinal);
    }

    /// <summary>
    /// Accepts only an id that is safe as a single path component under every member: lower snake
    /// case, bounded, and never a reserved Windows device name.
    /// </summary>
    public static bool IsAcceptedEffectId(string? effectId)
    {
        if (string.IsNullOrEmpty(effectId) || effectId.Length > MaximumEffectIdLength)
        {
            return false;
        }

        foreach (var character in effectId)
        {
            if (character is not ((>= 'a' and <= 'z') or (>= '0' and <= '9') or '_'))
            {
                return false;
            }
        }

        return effectId[0] != '_' &&
               effectId[^1] != '_' &&
               !effectId.Contains("__", StringComparison.Ordinal) &&
               !ReservedDeviceNames.Contains(effectId);
    }
}

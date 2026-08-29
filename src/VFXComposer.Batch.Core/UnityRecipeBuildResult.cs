using System.Text.Json;

namespace VFXComposer.Batch.Core;

/// <summary>
/// The structured outcome the Unity batchmode build entry point writes. Only the fields the
/// execution layer needs are surfaced; the file itself stays in the job's private scratch
/// directory, outside the Unity project.
/// </summary>
public sealed record UnityRecipeBuildResult(
    string DraftId,
    bool Succeeded,
    string? FailureCode,
    string? EffectId,
    string? RecipeHash,
    string? BuildHash,
    string? CompilerVersion,
    string? DeclaredTemplateCatalogVersion,
    string? CatalogIdentityHash,
    string? PrefabPath,
    string? BuildManifestPath,
    string? OwnershipManifestPath,
    string? ProvenanceRecipePath,
    string? DryRunState,
    IReadOnlyList<string> IssueCodes)
{
    public const string SchemaVersion = "vfxcomposer.recipe-build-result/1";

    public override string ToString() => "UnityRecipeBuildResult(" + (Succeeded ? "succeeded" : FailureCode ?? "failed") + ")";
}

/// <summary>
/// Reader for the Unity result document. An unknown schema version, an unknown field or a missing
/// required field fails closed rather than being interpreted optimistically: both sides ship from
/// the same repository, so a shape drift is a defect and never a compatibility case.
/// </summary>
public static class UnityRecipeBuildResultCodec
{
    private const int MaximumResultBytes = 512 * 1024;

    private static readonly string[] KnownFields =
    [
        "schemaVersion", "draftId", "succeeded", "failureCode", "effectId", "recipeHash", "buildHash",
        "recipeRevision", "compilerVersion", "unityVersion", "declaredTemplateCatalogVersion",
        "catalogIdentityHash", "prefabPath", "buildManifestPath", "ownershipManifestPath",
        "provenanceRecipePath", "dryRunState", "cleanedResiduePaths", "issues",
    ];

    /// <summary>Reads the result file. Throws <see cref="InvalidDataException"/> on any rejection.</summary>
    public static UnityRecipeBuildResult Read(string absolutePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(absolutePath);
        var info = new FileInfo(absolutePath);
        if (!info.Exists)
        {
            throw new InvalidDataException("The recipe build produced no structured result.");
        }

        if (info.Length > MaximumResultBytes)
        {
            throw new InvalidDataException("The recipe build result exceeds its size bound.");
        }

        using var stream = new FileStream(absolutePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var document = JsonDocument.Parse(stream);
        return Parse(document.RootElement);
    }

    /// <summary>Parses a result document already held in memory.</summary>
    public static UnityRecipeBuildResult Parse(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("The recipe build result is not a JSON object.");
        }

        foreach (var property in root.EnumerateObject())
        {
            if (!KnownFields.Contains(property.Name, StringComparer.Ordinal))
            {
                throw new InvalidDataException("The recipe build result carries an unknown field.");
            }
        }

        if (!string.Equals(ReadString(root, "schemaVersion"), UnityRecipeBuildResult.SchemaVersion, StringComparison.Ordinal))
        {
            throw new InvalidDataException("The recipe build result schema is not supported.");
        }

        if (!root.TryGetProperty("succeeded", out var succeeded) ||
            succeeded.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            throw new InvalidDataException("The recipe build result is missing its outcome.");
        }

        var issues = new List<string>();
        if (root.TryGetProperty("issues", out var issueArray) && issueArray.ValueKind == JsonValueKind.Array)
        {
            foreach (var issue in issueArray.EnumerateArray())
            {
                var code = issue.ValueKind == JsonValueKind.Object ? ReadString(issue, "code") : null;
                if (code is not null)
                {
                    issues.Add(code);
                }
            }
        }

        return new UnityRecipeBuildResult(
            ReadString(root, "draftId") ?? throw new InvalidDataException("The recipe build result is missing its draft id."),
            succeeded.GetBoolean(),
            ReadString(root, "failureCode"),
            ReadString(root, "effectId"),
            ReadString(root, "recipeHash"),
            ReadString(root, "buildHash"),
            ReadString(root, "compilerVersion"),
            ReadString(root, "declaredTemplateCatalogVersion"),
            ReadString(root, "catalogIdentityHash"),
            ReadString(root, "prefabPath"),
            ReadString(root, "buildManifestPath"),
            ReadString(root, "ownershipManifestPath"),
            ReadString(root, "provenanceRecipePath"),
            ReadString(root, "dryRunState"),
            issues);
    }

    private static string? ReadString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}

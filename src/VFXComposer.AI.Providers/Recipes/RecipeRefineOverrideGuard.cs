using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using VFXComposer.AI.Contracts.Recipes;

namespace VFXComposer.AI.Providers.Recipes;

/// <summary>
/// The refinement override guard (REQ-004 §9.3, REQ-004-45..50): a pure, deterministic post-processing step that
/// runs after an AI refinement output passes L1 and before the version is persisted. For every module parameter
/// scalar it restores the user's hand-tuned value when three conditions hold together: an ancestor
/// <c>human_edit</c> version set the parameter (its value differs from its parent's) and no newer
/// <c>human_edit</c> overrode it; this round's feedback does not name the parameter under the deterministic
/// alias lexicon; and the AI value differs numerically from the hand-tuned value. Structural differences
/// (added or removed stages or modules, a changed <c>templateId</c>) are outside the guard domain. The guard
/// never creates a version: the restored document is the content of the one <c>ai_refine</c> version the caller
/// persists. Nothing here does I/O or network, and no input ever reaches diagnostics.
/// </summary>
public static class RecipeRefineOverrideGuard
{
    /// <summary>
    /// Applies the guard.
    /// </summary>
    /// <param name="ancestorChain">
    /// The lineage records from the head upward: index 0 is the current head (the version being refined), each
    /// following record is the previous one's parent. This is <c>IRecipeDraftLineageStore.ListLineage</c> reversed.
    /// The chain must be linked and non-empty; a malformed chain is an <see cref="ArgumentException"/>.
    /// </param>
    /// <param name="aiRecipeJson">The AI refinement output, already past L1.</param>
    /// <param name="feedbackText">This round's user feedback, matched verbatim against the alias lexicon.</param>
    /// <param name="knowledge">The alias lexicon source; pass <see cref="RecipeRefineKnowledge.Default"/>.</param>
    public static RecipeRefineGuardOutcome Apply(
        IReadOnlyList<RecipeDraftRecord> ancestorChain,
        string aiRecipeJson,
        string feedbackText,
        RecipeRefineKnowledge knowledge)
    {
        GuardChain(ancestorChain);
        ArgumentException.ThrowIfNullOrWhiteSpace(aiRecipeJson);
        ArgumentException.ThrowIfNullOrWhiteSpace(feedbackText);
        ArgumentNullException.ThrowIfNull(knowledge);

        var aiParameters = ExtractModuleParameters(aiRecipeJson);
        var protectedValues = CollectProtectedHumanValues(ancestorChain);

        var restorations = new List<RecipeRefineGuardRestoration>();
        foreach (var (path, humanValue) in protectedValues)
        {
            if (!aiParameters.TryGetValue(path, out var aiValue) ||
                !string.Equals(aiValue.TemplateId, humanValue.TemplateId, StringComparison.Ordinal))
            {
                // The module is gone, renamed, or re-templated in the AI output, or the key itself is absent:
                // a structural difference, outside the guard domain (REQ-004-45).
                continue;
            }

            if (ValuesEqual(aiValue.RawLiteral, humanValue.RawLiteral))
            {
                continue;
            }

            if (FeedbackNamesParameter(feedbackText, humanValue.TemplateId, humanValue.ParameterName, knowledge))
            {
                continue;
            }

            restorations.Add(new RecipeRefineGuardRestoration(
                path,
                humanValue.SourceDraftId,
                aiValue.RawLiteral,
                humanValue.RawLiteral));
        }

        // Restorations apply in deterministic path order; the guarded document is canonical either way so the
        // caller hashes and persists one shape.
        restorations.Sort(static (left, right) => string.CompareOrdinal(left.ParameterPath, right.ParameterPath));
        var guardedJson = restorations.Count == 0
            ? RecipeCanonicalJson.Canonicalize(aiRecipeJson)
            : RestoreValues(aiRecipeJson, restorations);
        return new RecipeRefineGuardOutcome(guardedJson, restorations);
    }

    /// <summary>
    /// True when the feedback names the parameter under the lexicon rows covering
    /// <c>templateId.parameter</c> (REQ-004-47): an English alias matches as a consecutive ordinal-ignore-case
    /// token sequence, a Chinese alias as an ordinal substring. No semantic inference, no similarity: an alias
    /// either literally occurs or the parameter counts as not named, which restores the hand-tuned value.
    /// </summary>
    private static bool FeedbackNamesParameter(
        string feedbackText,
        string templateId,
        string parameterName,
        RecipeRefineKnowledge knowledge)
    {
        var parameterPath = templateId + "." + parameterName;
        var feedbackTokens = Tokenize(feedbackText);
        foreach (var translation in knowledge.FeedbackTranslations)
        {
            if (!translation.ParameterPaths.Contains(parameterPath, StringComparer.Ordinal))
            {
                continue;
            }

            foreach (var alias in translation.Aliases)
            {
                if (ContainsTokenSequence(feedbackTokens, Tokenize(alias)))
                {
                    return true;
                }
            }

            foreach (var alias in translation.AliasesZh)
            {
                if (feedbackText.Contains(alias, StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// The newest surviving hand-tuned value per parameter path: walking from the head down, the first
    /// <c>human_edit</c> that set a path (diff against its own parent) wins, which is exactly "the latest
    /// human edit not overridden by a newer one" (REQ-004-46 condition a).
    /// </summary>
    private static Dictionary<string, ProtectedValue> CollectProtectedHumanValues(
        IReadOnlyList<RecipeDraftRecord> ancestorChain)
    {
        var values = new Dictionary<string, ProtectedValue>(StringComparer.Ordinal);
        for (var index = 0; index < ancestorChain.Count; index++)
        {
            var record = ancestorChain[index];
            if (record.Origin != RecipeDraftOrigin.HumanEdit || index + 1 >= ancestorChain.Count)
            {
                // A human edit whose parent fell to a trim has no diffable baseline; the conservative reading
                // of "set value = diff against the parent" is to claim nothing for it.
                continue;
            }

            var edited = ExtractModuleParameters(record.RecipeJson);
            var baseline = ExtractModuleParameters(ancestorChain[index + 1].RecipeJson);
            foreach (var (path, value) in edited)
            {
                if (values.ContainsKey(path))
                {
                    continue; // A newer human edit already owns this path.
                }

                var isSet = !baseline.TryGetValue(path, out var parentValue) ||
                    !string.Equals(parentValue.TemplateId, value.TemplateId, StringComparison.Ordinal) ||
                    !ValuesEqual(parentValue.RawLiteral, value.RawLiteral);
                if (isSet)
                {
                    values[path] = new ProtectedValue(
                        value.TemplateId,
                        value.ParameterName,
                        value.RawLiteral,
                        record.DraftId);
                }
            }
        }

        return values;
    }

    /// <summary>
    /// Every module parameter scalar of a recipe document, keyed by the guard path
    /// <c>stages[&lt;stageId&gt;].modules[&lt;moduleId&gt;].parameters.&lt;name&gt;</c>. Non-scalar parameter
    /// values and modules without usable identity are skipped: they are structural or invalid territory that
    /// L1/L1.5 own.
    /// </summary>
    private static Dictionary<string, ExtractedValue> ExtractModuleParameters(string recipeJson)
    {
        var values = new Dictionary<string, ExtractedValue>(StringComparer.Ordinal);
        using var document = JsonDocument.Parse(recipeJson);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("stages", out var stages) ||
            stages.ValueKind != JsonValueKind.Array)
        {
            return values;
        }

        foreach (var stage in stages.EnumerateArray())
        {
            if (stage.ValueKind != JsonValueKind.Object ||
                !TryReadString(stage, "id", out var stageId) ||
                !stage.TryGetProperty("modules", out var modules) ||
                modules.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var module in modules.EnumerateArray())
            {
                if (module.ValueKind != JsonValueKind.Object ||
                    !TryReadString(module, "id", out var moduleId) ||
                    !TryReadString(module, "templateId", out var templateId) ||
                    !module.TryGetProperty("parameters", out var parameters) ||
                    parameters.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                foreach (var property in parameters.EnumerateObject())
                {
                    if (property.Value.ValueKind is not (JsonValueKind.Number or JsonValueKind.String
                        or JsonValueKind.True or JsonValueKind.False))
                    {
                        continue;
                    }

                    var path = RecipeParameterEditor.ParameterPath(stageId, moduleId, property.Name);
                    values[path] = new ExtractedValue(templateId, property.Name, property.Value.GetRawText());
                }
            }
        }

        return values;
    }

    /// <summary>
    /// Numeric equality by parsed value, so <c>1.2</c> and <c>1.20</c> are the same setting (REQ-004-46
    /// condition c); non-numeric scalars compare by ordinal raw text.
    /// </summary>
    private static bool ValuesEqual(string leftLiteral, string rightLiteral)
    {
        if (double.TryParse(leftLiteral, NumberStyles.Float, CultureInfo.InvariantCulture, out var left) &&
            double.TryParse(rightLiteral, NumberStyles.Float, CultureInfo.InvariantCulture, out var right))
        {
            return left == right;
        }

        return string.Equals(leftLiteral, rightLiteral, StringComparison.Ordinal);
    }

    private static string RestoreValues(string aiRecipeJson, IReadOnlyList<RecipeRefineGuardRestoration> restorations)
    {
        var root = JsonNode.Parse(aiRecipeJson)!.AsObject();
        var stages = root["stages"]!.AsArray();
        foreach (var restoration in restorations)
        {
            var (stageId, moduleId, parameterName) = ParsePath(restoration.ParameterPath);
            foreach (var stage in stages)
            {
                if (stage is not JsonObject stageObject ||
                    stageObject["id"]?.GetValue<string>() is not { } id ||
                    !string.Equals(id, stageId, StringComparison.Ordinal))
                {
                    continue;
                }

                foreach (var module in stageObject["modules"]!.AsArray())
                {
                    if (module is JsonObject moduleObject &&
                        moduleObject["id"]?.GetValue<string>() is { } candidate &&
                        string.Equals(candidate, moduleId, StringComparison.Ordinal))
                    {
                        moduleObject["parameters"]!.AsObject()[parameterName] =
                            JsonNode.Parse(restoration.RestoredValueLiteral);
                    }
                }
            }
        }

        return RecipeCanonicalJson.Canonicalize(root.ToJsonString());
    }

    private static (string StageId, string ModuleId, string ParameterName) ParsePath(string path)
    {
        // The path was produced by RecipeParameterEditor.ParameterPath above; it always parses back.
        const string stagesPrefix = "stages[";
        const string modulesInfix = "].modules[";
        const string parametersInfix = "].parameters.";
        var moduleStart = path.IndexOf(modulesInfix, StringComparison.Ordinal);
        var parameterStart = path.IndexOf(parametersInfix, StringComparison.Ordinal);
        return (
            path[stagesPrefix.Length..moduleStart],
            path[(moduleStart + modulesInfix.Length)..parameterStart],
            path[(parameterStart + parametersInfix.Length)..]);
    }

    private static void GuardChain(IReadOnlyList<RecipeDraftRecord> ancestorChain)
    {
        ArgumentNullException.ThrowIfNull(ancestorChain);
        if (ancestorChain.Count == 0 || ancestorChain.Any(static record => record is null))
        {
            throw new ArgumentException("The ancestor chain is invalid.", nameof(ancestorChain));
        }

        for (var index = 0; index < ancestorChain.Count - 1; index++)
        {
            if (!string.Equals(
                    ancestorChain[index].ParentDraftId,
                    ancestorChain[index + 1].DraftId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    ancestorChain[index].LineageId,
                    ancestorChain[index + 1].LineageId,
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "The ancestor chain must run from the head upward with linked parents.",
                    nameof(ancestorChain));
            }
        }
    }

    private static bool TryReadString(JsonElement objectElement, string name, out string value)
    {
        if (objectElement.TryGetProperty(name, out var property) &&
            property.ValueKind == JsonValueKind.String &&
            !string.IsNullOrEmpty(property.GetString()))
        {
            value = property.GetString()!;
            return true;
        }

        value = string.Empty;
        return false;
    }

    /// <summary>Letter/digit token runs, lowercased ordinally; everything else separates tokens.</summary>
    private static List<string> Tokenize(string text)
    {
        var tokens = new List<string>();
        var start = -1;
        for (var index = 0; index <= text.Length; index++)
        {
            var isTokenCharacter = index < text.Length && char.IsLetterOrDigit(text[index]);
            if (isTokenCharacter && start < 0)
            {
                start = index;
            }
            else if (!isTokenCharacter && start >= 0)
            {
                tokens.Add(text[start..index].ToLowerInvariant());
                start = -1;
            }
        }

        return tokens;
    }

    private static bool ContainsTokenSequence(List<string> haystack, List<string> needle)
    {
        if (needle.Count == 0 || haystack.Count < needle.Count)
        {
            return false;
        }

        for (var start = 0; start <= haystack.Count - needle.Count; start++)
        {
            var matched = true;
            for (var offset = 0; offset < needle.Count; offset++)
            {
                if (!string.Equals(haystack[start + offset], needle[offset], StringComparison.Ordinal))
                {
                    matched = false;
                    break;
                }
            }

            if (matched)
            {
                return true;
            }
        }

        return false;
    }

    private readonly record struct ExtractedValue(string TemplateId, string ParameterName, string RawLiteral);

    private readonly record struct ProtectedValue(
        string TemplateId,
        string ParameterName,
        string RawLiteral,
        string SourceDraftId);
}

/// <summary>
/// The guard's typed result: the canonical recipe document to persist as the <c>ai_refine</c> version and the
/// full restoration list. Persistence bounds the list (first 64 entries) and keeps the total as a count
/// (REQ-004 §7.2); this result carries everything so the caller can also surface the before/after literals.
/// </summary>
public sealed class RecipeRefineGuardOutcome
{
    internal RecipeRefineGuardOutcome(string guardedRecipeJson, IReadOnlyList<RecipeRefineGuardRestoration> restorations)
    {
        GuardedRecipeJson = guardedRecipeJson;
        Restorations = new ReadOnlyCollection<RecipeRefineGuardRestoration>(restorations.ToArray());
    }

    /// <summary>The canonical document after restorations; equal in content to the AI output when none applied.</summary>
    public string GuardedRecipeJson { get; }

    /// <summary>Every restoration, ordinal-ordered by parameter path; may exceed the persisted list bound.</summary>
    public IReadOnlyList<RecipeRefineGuardRestoration> Restorations { get; }

    /// <summary>The contract-shaped restoration list for persistence.</summary>
    public IReadOnlyList<RecipeGuardRestoration> ToGuardRestorations() =>
        Restorations
            .Select(static restoration => new RecipeGuardRestoration(restoration.ParameterPath, restoration.SourceDraftId))
            .ToArray();

    public override string ToString() => "RecipeRefineGuardOutcome(" + Restorations.Count + ")";
}

/// <summary>
/// One restored parameter: its guard path, the human-edit version whose value was restored, and the two value
/// literals for the confirmation panel and the timeline (REQ-004-48).
/// </summary>
public sealed class RecipeRefineGuardRestoration
{
    internal RecipeRefineGuardRestoration(
        string parameterPath,
        string sourceDraftId,
        string aiValueLiteral,
        string restoredValueLiteral)
    {
        ParameterPath = parameterPath;
        SourceDraftId = sourceDraftId;
        AiValueLiteral = aiValueLiteral;
        RestoredValueLiteral = restoredValueLiteral;
    }

    public string ParameterPath { get; }

    /// <summary>The <c>human_edit</c> version the restored value comes from.</summary>
    public string SourceDraftId { get; }

    /// <summary>The value the AI wrote before the guard restored the hand-tuned one.</summary>
    public string AiValueLiteral { get; }

    public string RestoredValueLiteral { get; }

    public override string ToString() => "RecipeRefineGuardRestoration(" + ParameterPath + ")";
}

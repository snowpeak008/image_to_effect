using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using VFXComposer.AI.Providers.Recipes;

namespace VFXComposer.Desktop.Localization;

/// <summary>
/// Bridges the provider-side closed suggestion-key set (F8a1) to the bilingual catalog: a suggestion key names
/// its catalog entry verbatim, and this closed list is what lets the shell resolve one without ever passing an
/// unpinned string into the catalog indexer. The parity test asserts the two closed sets stay identical.
/// </summary>
public static class RecipeSuggestionCopy
{
    /// <summary>The catalog keys carrying suggestion copy, spelled through the UiStringKeys constants.</summary>
    public static readonly IReadOnlyList<string> CatalogKeys =
    [
        UiStringKeys.RecipeSuggestionChooseCatalogTemplate,
        UiStringKeys.RecipeSuggestionMatchTemplateKind,
        UiStringKeys.RecipeSuggestionAddMissingParameter,
        UiStringKeys.RecipeSuggestionRemoveUnknownParameter,
        UiStringKeys.RecipeSuggestionClampParameterToRange,
        UiStringKeys.RecipeSuggestionUseParameterNumericType,
        UiStringKeys.RecipeSuggestionAddMissingStageRoot,
        UiStringKeys.RecipeSuggestionReorderStageRoots,
        UiStringKeys.RecipeSuggestionReduceModuleCount,
        UiStringKeys.RecipeSuggestionRemoveAttachment,
        UiStringKeys.RecipeSuggestionUseBuildableArchetype,
        UiStringKeys.RecipeSuggestionUseBuildableDimension,
        UiStringKeys.RecipeSuggestionRemoveUnknownField,
        UiStringKeys.RecipeSuggestionAddRequiredField,
        UiStringKeys.RecipeSuggestionUseDeclaredValueType,
        UiStringKeys.RecipeSuggestionUseAllowedEnumValue,
        UiStringKeys.RecipeSuggestionReturnOneJsonObject,
    ];

    private static readonly FrozenSet<string> CatalogKeySet = CatalogKeys.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>
    /// Resolves the catalog key carrying the repair suggestion for one issue code. An unmapped code renders
    /// without a suggestion line; a mapped key outside the pinned catalog set would be a programming error and
    /// is refused the same way, never reaching the throwing catalog indexer.
    /// </summary>
    public static bool TryGetCatalogKey(string issueCode, [NotNullWhen(true)] out string? catalogKey)
    {
        ArgumentNullException.ThrowIfNull(issueCode);
        if (RecipeIssueSuggestions.TryGetSuggestionKey(issueCode, out var suggestionKey) &&
            CatalogKeySet.Contains(suggestionKey))
        {
            catalogKey = suggestionKey;
            return true;
        }

        catalogKey = null;
        return false;
    }
}

using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using VFXComposer.AI.Providers.Recipes;

namespace VFXComposer.Desktop.Localization;

/// <summary>
/// Bridges the editor's closed rejection-code set (F8b3, <see cref="RecipeParameterEditCodes"/>) to the bilingual
/// catalog. Each sentence keeps the machine-readable parameter path, offending text and allowed range as format
/// arguments, so the language switch re-renders the shell sentence while the carriers stay verbatim.
/// </summary>
public static class RecipeParameterEditCopy
{
    private static readonly FrozenDictionary<string, string> CatalogKeysByCode =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [RecipeParameterEditCodes.NoChanges] = UiStringKeys.RecipeParameterEditNoChanges,
            [RecipeParameterEditCodes.TargetNotFound] = UiStringKeys.RecipeParameterEditTargetNotFound,
            [RecipeParameterEditCodes.ValueNotInteger] = UiStringKeys.RecipeParameterEditValueNotInteger,
            [RecipeParameterEditCodes.ValueNotFinite] = UiStringKeys.RecipeParameterEditValueNotFinite,
            [RecipeParameterEditCodes.ValueOutOfRange] = UiStringKeys.RecipeParameterEditValueOutOfRange,
            [RecipeParameterEditCodes.DocumentNotEditable] = UiStringKeys.RecipeParameterEditDocumentNotEditable,
            [RecipeParameterEditCodes.DuplicateTarget] = UiStringKeys.RecipeParameterEditDuplicateTarget,
        }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>The editor codes this shell can render; the parity test pins it against the provider set.</summary>
    public static IReadOnlyCollection<string> Codes => CatalogKeysByCode.Keys;

    /// <summary>
    /// Resolves the catalog key rendering one editor rejection. The sentence takes the issue path as <c>{0}</c>,
    /// the offending text as <c>{1}</c> and the allowed range as <c>{2}</c>; codes outside the editor set resolve
    /// nothing and fall back to the verbatim issue line.
    /// </summary>
    public static bool TryGetCatalogKey(string issueCode, [NotNullWhen(true)] out string? catalogKey)
    {
        ArgumentNullException.ThrowIfNull(issueCode);
        return CatalogKeysByCode.TryGetValue(issueCode, out catalogKey);
    }
}

using VFXComposer.AI.Contracts.Recipes;

namespace VFXComposer.Desktop.Localization;

/// <summary>
/// Renders a typed issue list for the Create page. Editor rejections (F8b3) become a bilingual sentence around
/// their verbatim path, text and range; every other issue keeps its stable code, JSON path and validator message
/// verbatim, followed by the bilingual repair suggestion when the code maps to one (F8a1/F8a2).
/// </summary>
internal static class RecipeIssueReport
{
    public static string Render(LocalizationService localization, IReadOnlyList<RecipeValidationIssue> issues) =>
        string.Join("\n", issues.Select(issue => RenderLine(localization, issue)));

    private static string RenderLine(LocalizationService localization, RecipeValidationIssue issue)
    {
        if (RecipeParameterEditCopy.TryGetCatalogKey(issue.Code, out var editKey))
        {
            return issue.Code + " " + localization.Format(
                editKey,
                issue.Path,
                issue.ActualValueJson ?? string.Empty,
                issue.AllowedRange ?? string.Empty);
        }

        var line = issue.Code + " " + issue.Path + ": " + issue.Message;
        return RecipeSuggestionCopy.TryGetCatalogKey(issue.Code, out var catalogKey)
            ? line + "\n    → " + localization[catalogKey]
            : line;
    }
}

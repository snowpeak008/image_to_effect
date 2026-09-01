using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;

namespace VFXComposer.AI.Providers.Recipes;

/// <summary>
/// Closed set of suggestion keys. A key is a stable identifier only: this assembly deliberately produces no
/// user-facing sentence, because the bilingual copy lives in the Desktop string catalog. Each constant's value is
/// <c>RecipeSuggestion</c> + the constant name so a catalog entry spells the key identically to the call site.
/// </summary>
public static class RecipeSuggestionKeys
{
    public const string ChooseCatalogTemplate = "RecipeSuggestionChooseCatalogTemplate";
    public const string MatchTemplateKind = "RecipeSuggestionMatchTemplateKind";
    public const string AddMissingParameter = "RecipeSuggestionAddMissingParameter";
    public const string RemoveUnknownParameter = "RecipeSuggestionRemoveUnknownParameter";
    public const string ClampParameterToRange = "RecipeSuggestionClampParameterToRange";
    public const string UseParameterNumericType = "RecipeSuggestionUseParameterNumericType";
    public const string AddMissingStageRoot = "RecipeSuggestionAddMissingStageRoot";
    public const string ReorderStageRoots = "RecipeSuggestionReorderStageRoots";
    public const string ReduceModuleCount = "RecipeSuggestionReduceModuleCount";
    public const string RemoveAttachment = "RecipeSuggestionRemoveAttachment";
    public const string UseBuildableArchetype = "RecipeSuggestionUseBuildableArchetype";
    public const string UseBuildableDimension = "RecipeSuggestionUseBuildableDimension";
    public const string RemoveUnknownField = "RecipeSuggestionRemoveUnknownField";
    public const string AddRequiredField = "RecipeSuggestionAddRequiredField";
    public const string UseDeclaredValueType = "RecipeSuggestionUseDeclaredValueType";
    public const string UseAllowedEnumValue = "RecipeSuggestionUseAllowedEnumValue";
    public const string ReturnOneJsonObject = "RecipeSuggestionReturnOneJsonObject";

    /// <summary>The closed key set.</summary>
    public static IReadOnlySet<string> All => RecipeIssueSuggestions.Keys;
}

/// <summary>
/// Closed map from a validation issue code to the one suggestion key that describes how to repair it. Every L1.5
/// code is mapped; the mapped L1 codes are the structural ones a failed generation reports most often. An unmapped
/// code is not an error: a caller that finds no key renders the issue without a suggestion line.
/// </summary>
public static class RecipeIssueSuggestions
{
    /// <summary>The high-frequency L1 structural codes that carry a suggestion, mirrored from <c>RecipeL1Validator</c>.</summary>
    private const string UnknownField = "E100";

    private const string RequiredField = "E101";
    private const string InvalidType = "E102";
    private const string InvalidEnum = "E103";
    private const string InvalidJson = "E104";

    private static readonly FrozenDictionary<string, string> Map =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [RecipePrevalidationCodes.TemplateUnknown] = RecipeSuggestionKeys.ChooseCatalogTemplate,
            [RecipePrevalidationCodes.TemplateKindMismatch] = RecipeSuggestionKeys.MatchTemplateKind,
            [RecipePrevalidationCodes.ParameterMissing] = RecipeSuggestionKeys.AddMissingParameter,
            [RecipePrevalidationCodes.ParameterUnknown] = RecipeSuggestionKeys.RemoveUnknownParameter,
            [RecipePrevalidationCodes.ParameterOutOfRange] = RecipeSuggestionKeys.ClampParameterToRange,
            [RecipePrevalidationCodes.ParameterTypeMismatch] = RecipeSuggestionKeys.UseParameterNumericType,
            [RecipePrevalidationCodes.StageRootMissing] = RecipeSuggestionKeys.AddMissingStageRoot,
            [RecipePrevalidationCodes.StageRootOutOfOrder] = RecipeSuggestionKeys.ReorderStageRoots,
            [RecipePrevalidationCodes.ModuleBudgetExceeded] = RecipeSuggestionKeys.ReduceModuleCount,
            [RecipePrevalidationCodes.AttachmentNotAllowed] = RecipeSuggestionKeys.RemoveAttachment,
            [RecipePrevalidationCodes.ArchetypeNotBuildable] = RecipeSuggestionKeys.UseBuildableArchetype,
            [RecipePrevalidationCodes.DimensionNotBuildable] = RecipeSuggestionKeys.UseBuildableDimension,
            [UnknownField] = RecipeSuggestionKeys.RemoveUnknownField,
            [RequiredField] = RecipeSuggestionKeys.AddRequiredField,
            [InvalidType] = RecipeSuggestionKeys.UseDeclaredValueType,
            [InvalidEnum] = RecipeSuggestionKeys.UseAllowedEnumValue,
            [InvalidJson] = RecipeSuggestionKeys.ReturnOneJsonObject,
        }.ToFrozenDictionary(StringComparer.Ordinal);

    internal static FrozenSet<string> Keys { get; } = Map.Values.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>Every mapping, keyed by issue code.</summary>
    public static IReadOnlyDictionary<string, string> All => Map;

    /// <summary>Resolves the suggestion key for an issue code, if the code carries one.</summary>
    public static bool TryGetSuggestionKey(string issueCode, [NotNullWhen(true)] out string? suggestionKey)
    {
        ArgumentNullException.ThrowIfNull(issueCode);
        return Map.TryGetValue(issueCode, out suggestionKey);
    }
}

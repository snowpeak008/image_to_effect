using System.Collections.Frozen;

namespace VFXComposer.AI.Providers.Recipes;

/// <summary>
/// Closed L1.5 pre-validation code set. L1 (<c>E1xx</c>/<c>E3xx</c>) mirrors the Unity structural vocabulary and
/// must not be extended, so the catalog-aware layer carries its own stable prefix with the same discipline as
/// <c>VFXJ</c>/<c>VFXB</c>: fixed single-line path-free messages, one code per defect class, closed set.
/// </summary>
public static class RecipePrevalidationCodes
{
    /// <summary>The module names a templateId the committed catalog snapshot does not declare.</summary>
    public const string TemplateUnknown = "VFXP0001";

    /// <summary>The module kind disagrees with the kind its template declares.</summary>
    public const string TemplateKindMismatch = "VFXP0002";

    /// <summary>A parameter the template declares is absent from the module.</summary>
    public const string ParameterMissing = "VFXP0003";

    /// <summary>The module declares a parameter key the template does not declare.</summary>
    public const string ParameterUnknown = "VFXP0004";

    /// <summary>The parameter value falls outside the inclusive committed bounds.</summary>
    public const string ParameterOutOfRange = "VFXP0005";

    /// <summary>The parameter value does not have the declared numeric type.</summary>
    public const string ParameterTypeMismatch = "VFXP0006";

    /// <summary>One of the three fixed stage roots is absent.</summary>
    public const string StageRootMissing = "VFXP0007";

    /// <summary>The three fixed stage roots appear in the wrong order.</summary>
    public const string StageRootOutOfOrder = "VFXP0008";

    /// <summary>The recipe declares more modules than the strict build budget allows.</summary>
    public const string ModuleBudgetExceeded = "VFXP0009";

    /// <summary>The module nests under another module through attachTo.</summary>
    public const string AttachmentNotAllowed = "VFXP0010";

    /// <summary>The archetype is outside the catalog's buildable set.</summary>
    public const string ArchetypeNotBuildable = "VFXP0011";

    /// <summary>The dimension is outside the catalog's buildable set.</summary>
    public const string DimensionNotBuildable = "VFXP0012";

    /// <summary>The closed code set.</summary>
    public static IReadOnlySet<string> All => RecipePrevalidationCatalog.Codes;
}

/// <summary>One immutable definition per L1.5 pre-validation code.</summary>
public sealed record RecipePrevalidationDefinition(string Code, string Message);

/// <summary>
/// Closed catalog resolving L1.5 codes to their fixed English message. Messages are diagnostic carriers, never
/// user-facing copy: the bilingual sentence a user reads is rendered from the suggestion key instead.
/// </summary>
public static class RecipePrevalidationCatalog
{
    private static readonly FrozenDictionary<string, RecipePrevalidationDefinition> Definitions =
        new[]
        {
            new RecipePrevalidationDefinition(
                RecipePrevalidationCodes.TemplateUnknown,
                "The module names a template that the committed catalog does not declare."),
            new RecipePrevalidationDefinition(
                RecipePrevalidationCodes.TemplateKindMismatch,
                "The module kind does not match the kind declared by its template."),
            new RecipePrevalidationDefinition(
                RecipePrevalidationCodes.ParameterMissing,
                "A parameter declared by the template is missing from the module."),
            new RecipePrevalidationDefinition(
                RecipePrevalidationCodes.ParameterUnknown,
                "The module declares a parameter that its template does not declare."),
            new RecipePrevalidationDefinition(
                RecipePrevalidationCodes.ParameterOutOfRange,
                "The parameter value is outside the inclusive range declared by the template."),
            new RecipePrevalidationDefinition(
                RecipePrevalidationCodes.ParameterTypeMismatch,
                "The parameter value does not have the numeric type declared by the template."),
            new RecipePrevalidationDefinition(
                RecipePrevalidationCodes.StageRootMissing,
                "A required stage root is missing; a buildable recipe carries launch, travel and impact."),
            new RecipePrevalidationDefinition(
                RecipePrevalidationCodes.StageRootOutOfOrder,
                "The stage roots are not in the required order launch, travel, impact."),
            new RecipePrevalidationDefinition(
                RecipePrevalidationCodes.ModuleBudgetExceeded,
                "The recipe declares more modules than the strict build budget allows."),
            new RecipePrevalidationDefinition(
                RecipePrevalidationCodes.AttachmentNotAllowed,
                "attachTo is not allowed; nesting a module exceeds the strict hierarchy depth budget."),
            new RecipePrevalidationDefinition(
                RecipePrevalidationCodes.ArchetypeNotBuildable,
                "The archetype is not buildable by the committed template catalog."),
            new RecipePrevalidationDefinition(
                RecipePrevalidationCodes.DimensionNotBuildable,
                "The dimension is not buildable by the committed template catalog."),
        }.ToFrozenDictionary(definition => definition.Code, StringComparer.Ordinal);

    internal static FrozenSet<string> Codes { get; } = Definitions.Keys.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>Every definition, keyed by code.</summary>
    public static IReadOnlyDictionary<string, RecipePrevalidationDefinition> All => Definitions;

    /// <summary>Resolves one code, throwing when it is outside the closed set.</summary>
    public static RecipePrevalidationDefinition Require(string code) =>
        Definitions.TryGetValue(code, out var definition)
            ? definition
            : throw new ArgumentOutOfRangeException(nameof(code));
}

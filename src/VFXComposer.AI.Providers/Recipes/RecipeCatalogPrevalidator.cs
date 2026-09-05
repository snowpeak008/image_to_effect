using System.Globalization;
using System.Text.Json;
using VFXComposer.AI.Contracts.Recipes;

namespace VFXComposer.AI.Providers.Recipes;

/// <summary>
/// L1.5 catalog-aware pre-validation: the deterministic layer between L1 structural validation and the Unity L2
/// build. It answers the questions L1 cannot (L1 only checks that a module's <c>parameters</c> is an object) and
/// that today surface as a build rejection: does the template exist, does the module kind match it, is the
/// parameter key set exactly the declared one, does every value sit inside the committed inclusive bounds, and does
/// the recipe respect the strict simple-profile structure budget (three stage roots in order, at most
/// <see cref="MaximumModules"/> modules, no attachTo).
/// </summary>
/// <remarks>
/// <para>
/// This is a pure function library with no state and no I/O. By the §7 ruling it is a presentation-layer warning
/// surface in v1: it is not wired into <c>RecipeGenerationService</c>, it does not consume the request budget, and
/// it changes no existing verdict. Findings therefore carry
/// <see cref="RecipeValidationSeverity.Warning"/>, so concatenating them into an existing issue list can never
/// alter an error-driven decision by accident. Whether an L1.5 finding becomes a retry trigger is decided later.
/// </para>
/// <para>
/// Pre-validation presupposes an L1 pass and never repeats an L1 verdict: a defect L1 owns (unparseable document,
/// missing or wrongly typed field, unknown field, duplicate id) is silently skipped here rather than reported
/// twice under a second code.
/// </para>
/// </remarks>
public static class RecipeCatalogPrevalidator
{
    /// <summary>
    /// The strict budget declaration for a recipe: at most two modules across all stages. This is an
    /// independent strict-budget rule of its own (T1b raised the simple-profile
    /// <c>maxLocalMaterials</c> in <c>VfxProjectRules.json</c> to 6 for layered templates, which is
    /// exactly two modules of at most three renderer layers each, so the two-module cap still holds).
    /// </summary>
    public const int MaximumModules = 2;

    /// <summary>The three stage roots the runtime controller wires, in their required order.</summary>
    public static readonly IReadOnlyList<string> RequiredStageRoots = ["launch", "travel", "impact"];

    private const int MaximumActualValueCharacters = 200;

    /// <summary>Pre-validates a recipe document against the committed catalog snapshot.</summary>
    public static IReadOnlyList<RecipeValidationIssue> Prevalidate(string recipeJson) =>
        Prevalidate(recipeJson, RecipeTemplateCatalogSnapshot.Default);

    /// <summary>
    /// Pre-validates a recipe document against the given catalog snapshot. A document that does not parse, or whose
    /// root is not an object, yields no findings: that verdict belongs to L1.
    /// </summary>
    public static IReadOnlyList<RecipeValidationIssue> Prevalidate(
        string recipeJson,
        RecipeTemplateCatalogSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(recipeJson);
        ArgumentNullException.ThrowIfNull(snapshot);
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(recipeJson);
        }
        catch (JsonException)
        {
            return Array.Empty<RecipeValidationIssue>();
        }

        using (document)
        {
            return Prevalidate(document.RootElement, snapshot);
        }
    }

    /// <summary>Pre-validates an already parsed recipe root against the given catalog snapshot.</summary>
    public static IReadOnlyList<RecipeValidationIssue> Prevalidate(
        JsonElement recipeRoot,
        RecipeTemplateCatalogSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var issues = new List<RecipeValidationIssue>();
        if (recipeRoot.ValueKind != JsonValueKind.Object)
        {
            return issues;
        }

        ValidateBuildableTarget(recipeRoot, "archetype", snapshot.BuildableArchetypes, RecipePrevalidationCodes.ArchetypeNotBuildable, issues);
        ValidateBuildableTarget(recipeRoot, "dimension", snapshot.BuildableDimensions, RecipePrevalidationCodes.DimensionNotBuildable, issues);

        if (!recipeRoot.TryGetProperty("stages", out var stages) || stages.ValueKind != JsonValueKind.Array)
        {
            return issues;
        }

        ValidateStageRoots(stages, issues);
        ValidateModules(stages, snapshot, issues);
        return issues;
    }

    private static void ValidateBuildableTarget(
        JsonElement root,
        string propertyName,
        IReadOnlyList<string> buildableValues,
        string code,
        List<RecipeValidationIssue> issues)
    {
        if (!root.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.String)
        {
            return;
        }

        if (!buildableValues.Contains(value.GetString(), StringComparer.Ordinal))
        {
            issues.Add(Issue(code, "/" + propertyName, value, Bracket(buildableValues)));
        }
    }

    /// <summary>
    /// Reports every absent stage root at its own path, and reports a single order finding when the roots that are
    /// present do not keep the required relative order.
    /// </summary>
    private static void ValidateStageRoots(JsonElement stages, List<RecipeValidationIssue> issues)
    {
        var encountered = new List<string>();
        foreach (var stage in stages.EnumerateArray())
        {
            if (stage.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var id = ReadString(stage, "id");
            if (id is not null && RequiredStageRoots.Contains(id, StringComparer.Ordinal) &&
                !encountered.Contains(id, StringComparer.Ordinal))
            {
                encountered.Add(id);
            }
        }

        foreach (var root in RequiredStageRoots)
        {
            if (!encountered.Contains(root, StringComparer.Ordinal))
            {
                issues.Add(Issue(
                    RecipePrevalidationCodes.StageRootMissing,
                    "/stages/" + root,
                    actualValueJson: null,
                    Bracket(RequiredStageRoots)));
            }
        }

        var required = RequiredStageRoots
            .Where(root => encountered.Contains(root, StringComparer.Ordinal))
            .ToArray();
        if (!encountered.SequenceEqual(required, StringComparer.Ordinal))
        {
            issues.Add(Issue(
                RecipePrevalidationCodes.StageRootOutOfOrder,
                "/stages",
                JsonSerializer.Serialize(encountered),
                Bracket(RequiredStageRoots)));
        }
    }

    private static void ValidateModules(
        JsonElement stages,
        RecipeTemplateCatalogSnapshot snapshot,
        List<RecipeValidationIssue> issues)
    {
        var moduleCount = 0;
        foreach (var stage in stages.EnumerateArray())
        {
            if (stage.ValueKind != JsonValueKind.Object ||
                !stage.TryGetProperty("modules", out var modules) ||
                modules.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            var stagePath = StagePath(ReadString(stage, "id"));
            foreach (var module in modules.EnumerateArray())
            {
                if (module.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                moduleCount++;
                ValidateModule(module, ModulePath(stagePath, ReadString(module, "id")), snapshot, issues);
            }
        }

        if (moduleCount > MaximumModules)
        {
            issues.Add(Issue(
                RecipePrevalidationCodes.ModuleBudgetExceeded,
                "/stages",
                moduleCount.ToString(CultureInfo.InvariantCulture),
                "[0, " + MaximumModules.ToString(CultureInfo.InvariantCulture) + "]"));
        }
    }

    private static void ValidateModule(
        JsonElement module,
        string path,
        RecipeTemplateCatalogSnapshot snapshot,
        List<RecipeValidationIssue> issues)
    {
        if (module.TryGetProperty("attachTo", out var attachTo))
        {
            issues.Add(Issue(
                RecipePrevalidationCodes.AttachmentNotAllowed,
                path + "/attachTo",
                attachTo,
                allowedRange: null));
        }

        var templateId = ReadString(module, "templateId");
        if (templateId is null)
        {
            return;
        }

        if (!snapshot.TryGetTemplate(templateId, out var template))
        {
            issues.Add(Issue(
                RecipePrevalidationCodes.TemplateUnknown,
                path + "/templateId",
                module.GetProperty("templateId"),
                Bracket(snapshot.Templates.Select(static candidate => candidate.TemplateId).ToArray())));
            return;
        }

        var kind = ReadString(module, "kind");
        if (kind is not null && !string.Equals(kind, template.Kind, StringComparison.Ordinal))
        {
            issues.Add(Issue(
                RecipePrevalidationCodes.TemplateKindMismatch,
                path + "/kind",
                module.GetProperty("kind"),
                "[" + template.Kind + "]"));
        }

        if (module.TryGetProperty("parameters", out var parameters) && parameters.ValueKind == JsonValueKind.Object)
        {
            ValidateParameters(parameters, path + "/parameters", template, issues);
        }
    }

    /// <summary>
    /// The declared key set is exhaustive in both directions: an undeclared key is reported in document order, then
    /// every declared parameter is checked for presence, numeric type and inclusive bounds in catalog order.
    /// </summary>
    private static void ValidateParameters(
        JsonElement parameters,
        string path,
        RecipeTemplateCatalogSnapshot.TemplateSnapshot template,
        List<RecipeValidationIssue> issues)
    {
        foreach (var property in parameters.EnumerateObject())
        {
            if (!template.TryGetParameter(property.Name, out _))
            {
                issues.Add(Issue(
                    RecipePrevalidationCodes.ParameterUnknown,
                    path + "/" + property.Name,
                    property.Value,
                    Bracket(template.Parameters.Select(static parameter => parameter.Name).ToArray())));
            }
        }

        foreach (var parameter in template.Parameters)
        {
            var parameterPath = path + "/" + parameter.Name;
            var expectation = parameter.Type + " in " + parameter.RangeLiteral;
            if (!parameters.TryGetProperty(parameter.Name, out var value))
            {
                issues.Add(Issue(
                    RecipePrevalidationCodes.ParameterMissing,
                    parameterPath,
                    actualValueJson: null,
                    expectation));
                continue;
            }

            if (value.ValueKind != JsonValueKind.Number ||
                !value.TryGetDouble(out var number) ||
                !double.IsFinite(number) ||
                (parameter.IsInteger && !IsIntegerToken(value)))
            {
                issues.Add(Issue(
                    RecipePrevalidationCodes.ParameterTypeMismatch,
                    parameterPath,
                    value,
                    expectation));
                continue;
            }

            if (number < parameter.Minimum || number > parameter.Maximum)
            {
                issues.Add(Issue(
                    RecipePrevalidationCodes.ParameterOutOfRange,
                    parameterPath,
                    value,
                    parameter.RangeLiteral));
            }
        }
    }

    private static string? ReadString(JsonElement objectElement, string name) =>
        objectElement.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static bool IsIntegerToken(JsonElement value) =>
        value.GetRawText().IndexOfAny(['.', 'e', 'E']) < 0;

    private static string Bracket(IEnumerable<string> values) => "[" + string.Join(", ", values) + "]";

    private static RecipeValidationIssue Issue(
        string code,
        string path,
        JsonElement actualValue,
        string? allowedRange) =>
        Issue(code, path, Truncate(actualValue.GetRawText()), allowedRange);

    private static RecipeValidationIssue Issue(
        string code,
        string path,
        string? actualValueJson,
        string? allowedRange) =>
        new(
            code,
            RecipeValidationSeverity.Warning,
            path,
            RecipePrevalidationCatalog.Require(code).Message,
            actualValueJson,
            allowedRange);

    private static string Truncate(string value) =>
        value.Length <= MaximumActualValueCharacters
            ? value
            : value[..MaximumActualValueCharacters] + "…";

    private static string StagePath(string? stageId) =>
        "/stages/" + (string.IsNullOrEmpty(stageId) ? "{invalid-stage}" : stageId);

    private static string ModulePath(string stagePath, string? moduleId) =>
        stagePath + "/modules/" + (string.IsNullOrEmpty(moduleId) ? "{invalid-module}" : moduleId);
}

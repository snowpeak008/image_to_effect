using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using VFXComposer.AI.Contracts.Recipes;

namespace VFXComposer.AI.Providers.Recipes;

/// <summary>
/// The public hand-edit surface of a recipe draft (REQ-004 §9.1–9.2). <see cref="Describe"/> renders the current
/// document against the committed catalog snapshot as an editable panel model; <see cref="Apply"/> turns a set of
/// scalar value edits into a new canonical document after type discipline, inclusive bounds, L1 and L1.5 checks.
/// The API can only change the value of a declared module parameter: it has no way to add or remove stages,
/// modules or keys, or to touch <c>id</c>/<c>kind</c>/<c>templateId</c>/<c>attachTo</c>. Values are never clamped,
/// rounded or silently corrected. Everything here is a pure function with no I/O, no network and no gateway.
/// </summary>
public static class RecipeParameterEditor
{
    /// <summary>Drafts created by a hand edit carry this fixed marker instead of a prompt template version.</summary>
    public const string HumanEditPromptTemplateVersion = "human-edit/1";

    private const int MaximumLiteralCharacters = 200;
    private const int MaximumPathCharacters = 1024;
    private const string InvalidStagePlaceholder = "{invalid-stage}";
    private const string InvalidModulePlaceholder = "{invalid-module}";

    /// <summary>Renders the panel model of a recipe document against the committed snapshot.</summary>
    public static RecipeParameterPanel Describe(string recipeJson)
    {
        ArgumentNullException.ThrowIfNull(recipeJson);
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(recipeJson);
        }
        catch (JsonException)
        {
            return RecipeParameterPanel.Empty;
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("stages", out var stages) ||
                stages.ValueKind != JsonValueKind.Array)
            {
                return RecipeParameterPanel.Empty;
            }

            var snapshot = RecipeTemplateCatalogSnapshot.Default;
            var modules = new List<RecipeParameterPanelModule>();
            var warnings = new List<RecipeParameterPanelWarning>();
            foreach (var stage in stages.EnumerateArray())
            {
                if (stage.ValueKind != JsonValueKind.Object ||
                    !stage.TryGetProperty("modules", out var stageModules) ||
                    stageModules.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                var stageId = ReadString(stage, "id");
                foreach (var module in stageModules.EnumerateArray())
                {
                    if (module.ValueKind == JsonValueKind.Object)
                    {
                        DescribeModule(module, stageId, snapshot, modules, warnings);
                    }
                }
            }

            return new RecipeParameterPanel(modules, warnings);
        }
    }

    /// <summary>
    /// Applies scalar value edits to declared module parameters. Every edit is checked before any is applied, so
    /// a rejection reports all offending edits; the accepted document is canonical and L1-valid, and any non-error
    /// L1 findings followed by the L1.5 findings on it are returned as warnings. Zero edits, or edits that leave
    /// the canonical document unchanged, are rejected: no change lands no version. An empty, whitespace-only or
    /// unparseable document is rejected with <see cref="RecipeParameterEditCodes.DocumentNotEditable"/>.
    /// </summary>
    public static RecipeParameterEditResult Apply(string recipeJson, IReadOnlyList<RecipeParameterEdit> edits)
    {
        ArgumentNullException.ThrowIfNull(recipeJson);
        ArgumentNullException.ThrowIfNull(edits);
        if (edits.Count == 0)
        {
            return RecipeParameterEditResult.Rejected([Issue(RecipeParameterEditCodes.NoChanges, "stages")]);
        }

        if (edits.Any(static edit => edit is null))
        {
            throw new ArgumentException("The edit list cannot contain null entries.", nameof(edits));
        }

        string canonicalInput;
        JsonObject root;
        try
        {
            canonicalInput = RecipeCanonicalJson.Canonicalize(recipeJson);
            root = JsonNode.Parse(canonicalInput) as JsonObject
                ?? throw new JsonException("The recipe root is not an object.");
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException)
        {
            // Canonicalize refuses an empty or whitespace-only document with ArgumentException rather than a
            // JsonException; both mean the same thing to the editor: there is no document to edit.
            return RecipeParameterEditResult.Rejected([Issue(RecipeParameterEditCodes.DocumentNotEditable, "/")]);
        }

        if (root["stages"] is not JsonArray stages)
        {
            return RecipeParameterEditResult.Rejected([Issue(RecipeParameterEditCodes.DocumentNotEditable, "stages")]);
        }

        var snapshot = RecipeTemplateCatalogSnapshot.Default;
        var issues = new List<RecipeValidationIssue>();
        var pending = new List<(JsonObject Parameters, RecipeTemplateCatalogSnapshot.TemplateParameterSnapshot Declaration, JsonNode Value)>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var edit in edits)
        {
            var path = ParameterPath(edit.StageId, edit.ModuleId, edit.ParameterName);
            if (!seen.Add(path))
            {
                issues.Add(Issue(RecipeParameterEditCodes.DuplicateTarget, path));
                continue;
            }

            var module = FindModule(stages, edit.StageId, edit.ModuleId);
            if (module is null ||
                !TryReadString(module["templateId"], out var templateId) ||
                !snapshot.TryGetParameter(templateId, edit.ParameterName, out var declaration) ||
                module["parameters"] is not JsonObject parameters)
            {
                issues.Add(Issue(RecipeParameterEditCodes.TargetNotFound, path));
                continue;
            }

            var value = ParseValue(edit.RawText, declaration, path, issues);
            if (value is not null)
            {
                pending.Add((parameters, declaration, value));
            }
        }

        if (issues.Count > 0)
        {
            return RecipeParameterEditResult.Rejected(issues);
        }

        foreach (var (parameters, declaration, value) in pending)
        {
            parameters[declaration.Name] = value;
        }

        var canonicalOutput = RecipeCanonicalJson.Canonicalize(root.ToJsonString());
        if (string.Equals(canonicalOutput, canonicalInput, StringComparison.Ordinal))
        {
            return RecipeParameterEditResult.Rejected([Issue(RecipeParameterEditCodes.NoChanges, "stages")]);
        }

        var l1Issues = RecipeL1Validator.Validate(canonicalOutput);
        if (RecipeL1Validator.HasErrors(l1Issues))
        {
            return RecipeParameterEditResult.Rejected(l1Issues);
        }

        // L1 currently emits errors only, so this list is empty today; should a future L1 rule emit a non-error
        // finding, it rides along the accepted result ahead of the L1.5 findings instead of being dropped.
        var findings = new List<RecipeValidationIssue>(l1Issues);
        findings.AddRange(RecipeCatalogPrevalidator.Prevalidate(canonicalOutput, snapshot));
        return RecipeParameterEditResult.Accepted(canonicalOutput, findings);
    }

    /// <summary>
    /// Wraps an accepted edit as the pending <see cref="RecipeDraftOrigin.HumanEdit"/> version to append after
    /// <paramref name="parent"/>. The parent must be a validated version (it carries a hash and the recipe summary
    /// fields); the summary fields are inherited because the editor cannot change them.
    /// </summary>
    public static RecipeDraftRevision CreateHumanEditRevision(RecipeDraftRecord parent, RecipeParameterEditResult accepted)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(accepted);
        if (!accepted.IsAccepted)
        {
            throw new ArgumentException("Only an accepted edit can become a version.", nameof(accepted));
        }

        if (parent.CanonicalSha256 is null ||
            parent.RecipeId is null ||
            parent.Archetype is null ||
            parent.Dimension is null ||
            parent.TargetProfile is null)
        {
            throw new ArgumentException("The parent must be a validated recipe version.", nameof(parent));
        }

        var draft = new RecipeDraft(
            "human-edit-" + Guid.NewGuid().ToString("N"),
            accepted.RecipeJson!,
            accepted.CanonicalSha256!,
            parent.RecipeId,
            parent.Archetype,
            parent.Dimension,
            parent.TargetProfile,
            HumanEditPromptTemplateVersion,
            RecipeTemplateCatalogSnapshot.Default.TemplateCatalogVersion);
        return new RecipeDraftRevision(draft, RecipeDraftOrigin.HumanEdit, requestCount: 0);
    }

    /// <summary>The editor's addressing path of one declared parameter.</summary>
    public static string ParameterPath(string stageId, string moduleId, string parameterName) =>
        ModulePath(stageId, moduleId) + ".parameters." + parameterName;

    private static string ModulePath(string? stageId, string? moduleId) =>
        "stages[" + (string.IsNullOrEmpty(stageId) ? InvalidStagePlaceholder : stageId) + "].modules[" +
        (string.IsNullOrEmpty(moduleId) ? InvalidModulePlaceholder : moduleId) + "]";

    private static void DescribeModule(
        JsonElement module,
        string? stageId,
        RecipeTemplateCatalogSnapshot snapshot,
        List<RecipeParameterPanelModule> modules,
        List<RecipeParameterPanelWarning> warnings)
    {
        var moduleId = ReadString(module, "id");
        var modulePath = ModulePath(stageId, moduleId);
        if (string.IsNullOrEmpty(stageId) || string.IsNullOrEmpty(moduleId))
        {
            warnings.Add(new RecipeParameterPanelWarning(
                RecipeParameterPanelWarningKind.ModuleUnaddressable,
                modulePath,
                string.IsNullOrEmpty(stageId) ? InvalidStagePlaceholder : InvalidModulePlaceholder,
                valueLiteral: null));
            return;
        }

        var templateId = ReadString(module, "templateId");
        if (templateId is null || !snapshot.TryGetTemplate(templateId, out var template))
        {
            warnings.Add(new RecipeParameterPanelWarning(
                RecipeParameterPanelWarningKind.TemplateUnknown,
                modulePath + ".templateId",
                templateId ?? string.Empty,
                valueLiteral: null));
            return;
        }

        var hasParameters = module.TryGetProperty("parameters", out var parameters) &&
            parameters.ValueKind == JsonValueKind.Object;
        if (hasParameters)
        {
            foreach (var property in parameters.EnumerateObject())
            {
                if (!template.TryGetParameter(property.Name, out _))
                {
                    warnings.Add(new RecipeParameterPanelWarning(
                        RecipeParameterPanelWarningKind.ParameterUndeclared,
                        modulePath + ".parameters." + property.Name,
                        property.Name,
                        Truncate(property.Value.GetRawText())));
                }
            }
        }

        var rows = new List<RecipeParameterPanelParameter>(template.Parameters.Count);
        foreach (var declaration in template.Parameters)
        {
            string? current = null;
            if (hasParameters && parameters.TryGetProperty(declaration.Name, out var value))
            {
                current = Truncate(value.GetRawText());
            }

            rows.Add(new RecipeParameterPanelParameter(stageId, moduleId, declaration, current));
        }

        modules.Add(new RecipeParameterPanelModule(
            stageId,
            moduleId,
            templateId,
            ReadString(module, "kind") ?? template.Kind,
            rows));
    }

    private static JsonObject? FindModule(JsonArray stages, string stageId, string moduleId)
    {
        foreach (var stage in stages)
        {
            if (stage is not JsonObject stageObject ||
                !TryReadString(stageObject["id"], out var id) ||
                !string.Equals(id, stageId, StringComparison.Ordinal) ||
                stageObject["modules"] is not JsonArray modules)
            {
                continue;
            }

            foreach (var module in modules)
            {
                if (module is JsonObject moduleObject &&
                    TryReadString(moduleObject["id"], out var candidate) &&
                    string.Equals(candidate, moduleId, StringComparison.Ordinal))
                {
                    return moduleObject;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Type discipline (REQ-004-42) and inclusive bounds (REQ-004-43). Surrounding whitespace is tolerated; nothing
    /// about the number itself is corrected. The value is returned as a JSON node ready to be written.
    /// </summary>
    private static JsonNode? ParseValue(
        string rawText,
        RecipeTemplateCatalogSnapshot.TemplateParameterSnapshot declaration,
        string path,
        List<RecipeValidationIssue> issues)
    {
        var text = rawText.Trim();
        double number;
        JsonNode node;
        if (declaration.IsInteger)
        {
            if (!long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var integer))
            {
                issues.Add(Issue(RecipeParameterEditCodes.ValueNotInteger, path, text, declaration.Type + " in " + declaration.RangeLiteral));
                return null;
            }

            number = integer;
            node = JsonValue.Create(integer);
        }
        else
        {
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out number) || !double.IsFinite(number))
            {
                issues.Add(Issue(RecipeParameterEditCodes.ValueNotFinite, path, text, declaration.Type + " in " + declaration.RangeLiteral));
                return null;
            }

            node = JsonValue.Create(number);
        }

        if (number < declaration.Minimum || number > declaration.Maximum)
        {
            issues.Add(Issue(RecipeParameterEditCodes.ValueOutOfRange, path, text, declaration.RangeLiteral));
            return null;
        }

        return node;
    }

    private static string? ReadString(JsonElement objectElement, string name) =>
        objectElement.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static bool TryReadString(JsonNode? node, out string value)
    {
        if (node is JsonValue jsonValue && jsonValue.TryGetValue<string>(out var text))
        {
            value = text;
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static RecipeValidationIssue Issue(string code, string path, string? actualValue = null, string? allowedRange = null) =>
        new(
            code,
            RecipeValidationSeverity.Error,
            path.Length <= MaximumPathCharacters ? path : path[..MaximumPathCharacters],
            RecipeParameterEditCodes.MessageOf(code),
            actualValue is null ? null : Truncate(actualValue),
            allowedRange);

    private static string Truncate(string value) =>
        value.Length <= MaximumLiteralCharacters ? value : value[..MaximumLiteralCharacters] + "…";
}

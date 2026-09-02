using System.Collections.Frozen;
using System.Collections.ObjectModel;
using VFXComposer.AI.Contracts.Recipes;

namespace VFXComposer.AI.Providers.Recipes;

/// <summary>
/// Closed code set of the hand-edit editor (REQ-004 §9.2). L1 (<c>E1xx</c>/<c>E3xx</c>) and L1.5 (<c>VFXP</c>)
/// keep their own closed sets; an edit the editor itself refuses carries one of these codes so the Desktop can
/// render a bilingual sentence around the machine-readable path, value and range.
/// </summary>
public static class RecipeParameterEditCodes
{
    /// <summary>The edit set is empty or changes no parameter value; no version is created for no change.</summary>
    public const string NoChanges = "VFXE0001";

    /// <summary>The stage, module or declared parameter named by the edit does not exist in the document.</summary>
    public const string TargetNotFound = "VFXE0002";

    /// <summary>An <c>integer</c> parameter received text that is not an integer literal.</summary>
    public const string ValueNotInteger = "VFXE0003";

    /// <summary>A <c>float</c> parameter received text that is not a finite real number.</summary>
    public const string ValueNotFinite = "VFXE0004";

    /// <summary>The value lies outside the inclusive committed bounds; it is never clamped or rounded.</summary>
    public const string ValueOutOfRange = "VFXE0005";

    /// <summary>The document is not a parseable recipe object with a stage array, so nothing in it can be located.</summary>
    public const string DocumentNotEditable = "VFXE0006";

    /// <summary>Two edits name the same parameter; the editor refuses to pick one.</summary>
    public const string DuplicateTarget = "VFXE0007";

    private static readonly FrozenDictionary<string, string> Messages = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [NoChanges] = "The edit set changes no parameter value.",
        [TargetNotFound] = "No declared parameter exists at the edited location.",
        [ValueNotInteger] = "The parameter is declared as integer and only accepts an integer literal.",
        [ValueNotFinite] = "The parameter only accepts a finite real number.",
        [ValueOutOfRange] = "The value is outside the inclusive range declared by the template.",
        [DocumentNotEditable] = "The recipe document cannot be parsed into editable stages.",
        [DuplicateTarget] = "The same parameter is edited more than once.",
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>The closed code set.</summary>
    public static IReadOnlySet<string> All { get; } = Messages.Keys.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>The fixed English diagnostic of one code; a code outside the set is a programming error.</summary>
    public static string MessageOf(string code) =>
        Messages.TryGetValue(code, out var message) ? message : throw new ArgumentOutOfRangeException(nameof(code));
}

/// <summary>One declared parameter of one module as the panel renders it: the snapshot declaration plus the current value.</summary>
public sealed class RecipeParameterPanelParameter
{
    internal RecipeParameterPanelParameter(
        string stageId,
        string moduleId,
        RecipeTemplateCatalogSnapshot.TemplateParameterSnapshot declaration,
        string? currentValueLiteral)
    {
        StageId = stageId;
        ModuleId = moduleId;
        Name = declaration.Name;
        Type = declaration.Type;
        IsInteger = declaration.IsInteger;
        MinLiteral = declaration.MinLiteral;
        DefaultLiteral = declaration.DefaultLiteral;
        MaxLiteral = declaration.MaxLiteral;
        RangeLiteral = declaration.RangeLiteral;
        Minimum = declaration.Minimum;
        Maximum = declaration.Maximum;
        Default = declaration.Default;
        CurrentValueLiteral = currentValueLiteral;
        Path = RecipeParameterEditor.ParameterPath(stageId, moduleId, declaration.Name);
    }

    public string StageId { get; }
    public string ModuleId { get; }
    public string Name { get; }
    public string Type { get; }
    public bool IsInteger { get; }
    public string MinLiteral { get; }
    public string DefaultLiteral { get; }
    public string MaxLiteral { get; }

    /// <summary>The inclusive bounds rendered as <c>[min, max]</c> from the exact committed literals.</summary>
    public string RangeLiteral { get; }

    public double Minimum { get; }
    public double Maximum { get; }
    public double Default { get; }

    /// <summary>The value's JSON text as it stands in the draft, or null when the declared key is absent.</summary>
    public string? CurrentValueLiteral { get; }

    /// <summary>True when the draft omits this declared parameter (an L1.5 <c>ParameterMissing</c> finding).</summary>
    public bool IsMissing => CurrentValueLiteral is null;

    /// <summary>The editor's addressing path: <c>stages[stage].modules[module].parameters.name</c>.</summary>
    public string Path { get; }

    public override string ToString() => "RecipeParameterPanelParameter(" + Path + ")";
}

/// <summary>One module with a known template, listing every parameter the template declares.</summary>
public sealed class RecipeParameterPanelModule
{
    internal RecipeParameterPanelModule(
        string stageId,
        string moduleId,
        string templateId,
        string kind,
        IReadOnlyList<RecipeParameterPanelParameter> parameters)
    {
        StageId = stageId;
        ModuleId = moduleId;
        TemplateId = templateId;
        Kind = kind;
        Parameters = parameters;
    }

    public string StageId { get; }
    public string ModuleId { get; }
    public string TemplateId { get; }
    public string Kind { get; }

    /// <summary>The declared parameters in catalog order; the set is exhaustive for the template.</summary>
    public IReadOnlyList<RecipeParameterPanelParameter> Parameters { get; }

    public override string ToString() => "RecipeParameterPanelModule(" + StageId + "," + ModuleId + ")";
}

/// <summary>What a warning row of the panel points at. Warning rows are never editable.</summary>
public enum RecipeParameterPanelWarningKind
{
    /// <summary>The module names a template the committed snapshot does not declare, so its parameters have no bounds.</summary>
    TemplateUnknown,

    /// <summary>The module carries a parameter key its template does not declare.</summary>
    ParameterUndeclared,

    /// <summary>The stage or module has no string identifier, so no edit can address it.</summary>
    ModuleUnaddressable,
}

/// <summary>One non-editable warning row: the kind, the addressing path and the offending identifier or key.</summary>
public sealed class RecipeParameterPanelWarning
{
    internal RecipeParameterPanelWarning(RecipeParameterPanelWarningKind kind, string path, string subject, string? valueLiteral)
    {
        Kind = kind;
        Path = path;
        Subject = subject;
        ValueLiteral = valueLiteral;
    }

    public RecipeParameterPanelWarningKind Kind { get; }
    public string Path { get; }

    /// <summary>The unknown template id, the undeclared key, or the placeholder of the missing identifier.</summary>
    public string Subject { get; }

    /// <summary>The undeclared key's JSON text (bounded), when the warning concerns a value.</summary>
    public string? ValueLiteral { get; }

    public override string ToString() => "RecipeParameterPanelWarning(" + Kind + "," + Path + ")";
}

/// <summary>
/// The read model of the parameter panel for one recipe document (REQ-004-41): modules in stage → module document
/// order, each with the snapshot-declared parameters, plus the warning rows for everything the snapshot does not
/// declare. An unparseable document or one without a stage array describes as empty; that verdict belongs to L1.
/// </summary>
public sealed class RecipeParameterPanel
{
    internal RecipeParameterPanel(
        IReadOnlyList<RecipeParameterPanelModule> modules,
        IReadOnlyList<RecipeParameterPanelWarning> warnings)
    {
        Modules = modules;
        Warnings = warnings;
    }

    public static RecipeParameterPanel Empty { get; } = new(
        Array.Empty<RecipeParameterPanelModule>(),
        Array.Empty<RecipeParameterPanelWarning>());

    public IReadOnlyList<RecipeParameterPanelModule> Modules { get; }
    public IReadOnlyList<RecipeParameterPanelWarning> Warnings { get; }

    /// <summary>The number of editable rows across every module.</summary>
    public int ParameterCount => Modules.Sum(static module => module.Parameters.Count);

    public override string ToString() => "RecipeParameterPanel(" + Modules.Count + " modules)";
}

/// <summary>
/// One requested value change, addressed by stage id, module id and declared parameter name. The API shape carries
/// no way to add or remove stages, modules or keys, or to touch <c>id</c>/<c>kind</c>/<c>templateId</c>/<c>attachTo</c>
/// (REQ-004-42). <see cref="RawText"/> is the user's text verbatim; the editor decides whether it is a value.
/// </summary>
public sealed class RecipeParameterEdit
{
    public RecipeParameterEdit(string stageId, string moduleId, string parameterName, string rawText)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stageId, nameof(stageId));
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId, nameof(moduleId));
        ArgumentException.ThrowIfNullOrWhiteSpace(parameterName, nameof(parameterName));
        ArgumentNullException.ThrowIfNull(rawText);
        StageId = stageId;
        ModuleId = moduleId;
        ParameterName = parameterName;
        RawText = rawText;
    }

    public string StageId { get; }
    public string ModuleId { get; }
    public string ParameterName { get; }
    public string RawText { get; }

    public override string ToString() => "RecipeParameterEdit(" + RecipeParameterEditor.ParameterPath(StageId, ModuleId, ParameterName) + ")";
}

/// <summary>
/// Typed verdict of <see cref="RecipeParameterEditor.Apply"/>. Accepted carries the canonical new document, its
/// hash and the non-blocking findings on it: any L1 finding below Error severity followed by the L1.5 warnings
/// (never blocking, F8a1 ruling); Rejected carries the error issues and no document.
/// </summary>
public sealed class RecipeParameterEditResult
{
    private RecipeParameterEditResult(string? recipeJson, string? canonicalSha256, IReadOnlyList<RecipeValidationIssue> issues)
    {
        RecipeJson = recipeJson;
        CanonicalSha256 = canonicalSha256;
        Issues = issues;
    }

    /// <summary>
    /// True when a new document was produced and passed L1 without an Error finding. <see cref="Issues"/> may still
    /// carry L1 findings below Error severity and L1.5 warnings; they inform, they do not block.
    /// </summary>
    public bool IsAccepted => RecipeJson is not null;

    /// <summary>The canonical new recipe document; null when rejected.</summary>
    public string? RecipeJson { get; }

    /// <summary>The SHA-256 of <see cref="RecipeJson"/>; null when rejected.</summary>
    public string? CanonicalSha256 { get; }

    /// <summary>
    /// Rejected: error issues (editor codes or L1). Accepted: the L1 findings below Error severity on the new
    /// document (none under today's L1 rules, which emit errors only), then the L1.5 warnings; possibly none.
    /// </summary>
    public IReadOnlyList<RecipeValidationIssue> Issues { get; }

    public override string ToString() => "RecipeParameterEditResult(" + (IsAccepted ? "Accepted" : "Rejected") + ")";

    internal static RecipeParameterEditResult Accepted(string recipeJson, IReadOnlyList<RecipeValidationIssue> prevalidationIssues) =>
        new(recipeJson, RecipeCanonicalJson.ComputeSha256(recipeJson), Copy(prevalidationIssues));

    internal static RecipeParameterEditResult Rejected(IReadOnlyList<RecipeValidationIssue> issues)
    {
        var copied = Copy(issues);
        if (copied.Count == 0)
        {
            throw new ArgumentException("A rejected edit carries at least one issue.", nameof(issues));
        }

        return new RecipeParameterEditResult(recipeJson: null, canonicalSha256: null, copied);
    }

    private static IReadOnlyList<RecipeValidationIssue> Copy(IReadOnlyList<RecipeValidationIssue> issues) =>
        new ReadOnlyCollection<RecipeValidationIssue>(issues.ToArray());
}

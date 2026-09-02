using CommunityToolkit.Mvvm.ComponentModel;
using VFXComposer.AI.Contracts.Recipes;
using VFXComposer.AI.Providers.Recipes;
using VFXComposer.Desktop.Localization;

namespace VFXComposer.Desktop.ViewModels;

/// <summary>
/// The parameter panel of the Create page (REQ-004 §9.1): the current head draft described against the committed
/// catalog snapshot, one editable row per declared parameter and one warning row per undeclared key or unknown
/// template. The panel is pure presentation: it collects edits and renders verdicts, while the owning page runs the
/// editor and the store. Nothing here ever reaches a gateway.
/// </summary>
public sealed class ParameterPanelViewModel : ObservableObject
{
    private readonly LocalizationService _localization;
    private IReadOnlyList<RecipeValidationIssue> _issues = [];
    private IReadOnlyList<ParameterModuleViewModel> _modules = [];
    private IReadOnlyList<ParameterWarningViewModel> _warnings = [];
    private bool _hasHead;

    internal ParameterPanelViewModel(LocalizationService localization)
    {
        _localization = localization;
    }

    /// <summary>True while a validated head draft is displayed; the panel is hidden otherwise.</summary>
    public bool HasHead
    {
        get => _hasHead;
        private set => SetProperty(ref _hasHead, value);
    }

    public IReadOnlyList<ParameterModuleViewModel> Modules
    {
        get => _modules;
        private set
        {
            if (SetProperty(ref _modules, value))
            {
                OnPropertyChanged(nameof(HasModules));
            }
        }
    }

    public IReadOnlyList<ParameterWarningViewModel> Warnings
    {
        get => _warnings;
        private set
        {
            if (SetProperty(ref _warnings, value))
            {
                OnPropertyChanged(nameof(HasWarnings));
            }
        }
    }

    public bool HasModules => Modules.Count > 0;

    public bool HasWarnings => Warnings.Count > 0;

    /// <summary>The number of editable rows across every module.</summary>
    public int ParameterCount => Modules.Sum(static module => module.Parameters.Count);

    /// <summary>The last verdict's issues (editor rejections, L1 errors or L1.5 warnings), re-rendered on a language switch.</summary>
    public string IssueReport => RecipeIssueReport.Render(_localization, _issues);

    public override string ToString() => "ParameterPanelViewModel(" + Modules.Count + " modules)";

    /// <summary>Re-describes the panel for a new head; a null or unvalidated record hides the panel.</summary>
    internal void Load(RecipeDraftRecord? head)
    {
        var panel = head is { CanonicalSha256: not null }
            ? RecipeParameterEditor.Describe(head.RecipeJson)
            : RecipeParameterPanel.Empty;
        Modules = panel.Modules.Select(module => new ParameterModuleViewModel(_localization, module)).ToArray();
        Warnings = panel.Warnings.Select(warning => new ParameterWarningViewModel(_localization, warning)).ToArray();
        HasHead = head is { CanonicalSha256: not null };
        OnPropertyChanged(nameof(ParameterCount));
        PresentIssues([]);
    }

    /// <summary>Every row whose text departs from the head value, in panel order.</summary>
    internal IReadOnlyList<RecipeParameterEdit> CollectEdits() => Modules
        .SelectMany(static module => module.Parameters)
        .Where(static row => row.HasPendingEdit)
        .Select(static row => row.ToEdit())
        .ToArray();

    internal void PresentIssues(IReadOnlyList<RecipeValidationIssue> issues)
    {
        _issues = issues;
        OnPropertyChanged(nameof(IssueReport));
    }

    internal void RefreshLocalizedText()
    {
        OnPropertyChanged(nameof(IssueReport));
        foreach (var module in Modules)
        {
            module.RefreshLocalizedText();
        }

        foreach (var warning in Warnings)
        {
            warning.RefreshLocalizedText();
        }
    }
}

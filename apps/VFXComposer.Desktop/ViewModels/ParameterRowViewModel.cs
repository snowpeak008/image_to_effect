using CommunityToolkit.Mvvm.ComponentModel;
using VFXComposer.AI.Providers.Recipes;
using VFXComposer.Desktop.Localization;

namespace VFXComposer.Desktop.ViewModels;

/// <summary>
/// One editable parameter row: the snapshot declaration (type, inclusive bounds, default), the value currently in
/// the head draft and the user's pending text. The row never interprets the text; the editor does (REQ-004-42/43).
/// </summary>
public sealed class ParameterRowViewModel : ObservableObject
{
    private readonly LocalizationService _localization;
    private readonly RecipeParameterPanelParameter _parameter;
    private string _editText;

    internal ParameterRowViewModel(LocalizationService localization, RecipeParameterPanelParameter parameter)
    {
        _localization = localization;
        _parameter = parameter;
        _editText = parameter.CurrentValueLiteral ?? string.Empty;
    }

    public string StageId => _parameter.StageId;

    public string ModuleId => _parameter.ModuleId;

    public string Name => _parameter.Name;

    /// <summary>The editor's addressing path; also the path a rejection reports.</summary>
    public string Path => _parameter.Path;

    public string Type => _parameter.Type;

    /// <summary>The inclusive bounds exactly as the catalog snapshot commits them.</summary>
    public string RangeLiteral => _parameter.RangeLiteral;

    public string DefaultLiteral => _parameter.DefaultLiteral;

    /// <summary>The value's JSON text in the head draft; null when the declared key is absent.</summary>
    public string? CurrentValueLiteral => _parameter.CurrentValueLiteral;

    public bool IsMissing => _parameter.IsMissing;

    /// <summary>The user's text, verbatim. Only rows whose text departs from the current value become edits.</summary>
    public string EditText
    {
        get => _editText;
        set => SetProperty(ref _editText, value ?? string.Empty);
    }

    /// <summary>Type, inclusive range and default, rendered through the catalog with the literals as arguments.</summary>
    public string BoundsHint => _localization.Format(UiStringKeys.CreateParameterBoundsHint, Type, RangeLiteral, DefaultLiteral);

    /// <summary>The current value, or the "not set" hint for a declared key the draft omits.</summary>
    public string ValueHint => IsMissing
        ? _localization[UiStringKeys.CreateParameterMissingHint]
        : _localization.Format(UiStringKeys.CreateParameterCurrentValue, CurrentValueLiteral);

    /// <summary>True when the text departs from the head value (an absent key counts as empty text).</summary>
    public bool HasPendingEdit => !string.Equals(EditText.Trim(), CurrentValueLiteral ?? string.Empty, StringComparison.Ordinal);

    public override string ToString() => "ParameterRowViewModel(" + Path + ")";

    internal RecipeParameterEdit ToEdit() => new(StageId, ModuleId, Name, EditText);

    internal void RefreshLocalizedText()
    {
        OnPropertyChanged(nameof(BoundsHint));
        OnPropertyChanged(nameof(ValueHint));
    }
}

using CommunityToolkit.Mvvm.ComponentModel;
using VFXComposer.AI.Providers.Recipes;
using VFXComposer.Desktop.Localization;

namespace VFXComposer.Desktop.ViewModels;

/// <summary>
/// One non-editable warning row of the parameter panel (REQ-004-41): an undeclared key, an unknown template or an
/// unaddressable module. The path and identifier stay verbatim inside the bilingual sentence.
/// </summary>
public sealed class ParameterWarningViewModel : ObservableObject
{
    private readonly LocalizationService _localization;
    private readonly RecipeParameterPanelWarning _warning;

    internal ParameterWarningViewModel(LocalizationService localization, RecipeParameterPanelWarning warning)
    {
        _localization = localization;
        _warning = warning;
    }

    public RecipeParameterPanelWarningKind Kind => _warning.Kind;

    public string Path => _warning.Path;

    public string Subject => _warning.Subject;

    public string Text => _localization.Format(CatalogKey, Path, Subject);

    public override string ToString() => "ParameterWarningViewModel(" + Kind + ")";

    internal void RefreshLocalizedText() => OnPropertyChanged(nameof(Text));

    private string CatalogKey => Kind switch
    {
        RecipeParameterPanelWarningKind.TemplateUnknown => UiStringKeys.CreateParameterWarningTemplateUnknown,
        RecipeParameterPanelWarningKind.ParameterUndeclared => UiStringKeys.CreateParameterWarningParameterUndeclared,
        _ => UiStringKeys.CreateParameterWarningModuleUnaddressable,
    };
}

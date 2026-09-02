using CommunityToolkit.Mvvm.ComponentModel;
using VFXComposer.AI.Providers.Recipes;
using VFXComposer.Desktop.Localization;

namespace VFXComposer.Desktop.ViewModels;

/// <summary>One module of the parameter panel: its identity line and the declared parameter rows in catalog order.</summary>
public sealed class ParameterModuleViewModel : ObservableObject
{
    private readonly LocalizationService _localization;
    private readonly RecipeParameterPanelModule _module;

    internal ParameterModuleViewModel(LocalizationService localization, RecipeParameterPanelModule module)
    {
        _localization = localization;
        _module = module;
        Parameters = module.Parameters
            .Select(parameter => new ParameterRowViewModel(localization, parameter))
            .ToArray();
    }

    public string StageId => _module.StageId;

    public string ModuleId => _module.ModuleId;

    public string TemplateId => _module.TemplateId;

    public string Kind => _module.Kind;

    public IReadOnlyList<ParameterRowViewModel> Parameters { get; }

    /// <summary>Stage, module, template and kind; identifiers stay verbatim inside the catalog sentence.</summary>
    public string Header => _localization.Format(UiStringKeys.CreateParameterModuleHeader, StageId, ModuleId, TemplateId, Kind);

    public override string ToString() => "ParameterModuleViewModel(" + StageId + "," + ModuleId + ")";

    internal void RefreshLocalizedText()
    {
        OnPropertyChanged(nameof(Header));
        foreach (var row in Parameters)
        {
            row.RefreshLocalizedText();
        }
    }
}

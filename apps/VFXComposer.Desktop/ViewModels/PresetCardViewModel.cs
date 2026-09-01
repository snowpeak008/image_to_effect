using CommunityToolkit.Mvvm.ComponentModel;
using VFXComposer.AI.Providers.Recipes;
using VFXComposer.Desktop.Localization;

namespace VFXComposer.Desktop.ViewModels;

/// <summary>
/// One simple-mode example card: bilingual display copy over a committed preset skeleton. The card itself is
/// pure presentation; applying it is the owning page's command, and no AI request is ever involved.
/// </summary>
public sealed class PresetCardViewModel : ObservableObject
{
    private readonly LocalizationService _localization;
    private readonly string _titleKey;
    private readonly string _descriptionKey;

    internal PresetCardViewModel(
        LocalizationService localization,
        RecipePresetSkeleton skeleton,
        string titleKey,
        string descriptionKey)
    {
        _localization = localization;
        Skeleton = skeleton;
        _titleKey = titleKey;
        _descriptionKey = descriptionKey;
    }

    public RecipePresetSkeleton Skeleton { get; }

    public string Title => _localization[_titleKey];

    public string Description => _localization[_descriptionKey];

    public override string ToString() => "PresetCardViewModel(" + Skeleton.PresetId + ")";

    internal void RefreshLocalizedText()
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Description));
    }
}

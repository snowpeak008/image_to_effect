using CommunityToolkit.Mvvm.ComponentModel;
using VFXComposer.Desktop.Localization;

namespace VFXComposer.Desktop.ViewModels;

public abstract class WorkspacePageViewModel : ObservableObject
{
    private readonly string _titleKey;
    private readonly string _descriptionKey;
    private readonly string _emptyStateMessageKey;

    protected WorkspacePageViewModel(
        LocalizationService localization,
        string key,
        string titleKey,
        string descriptionKey,
        string emptyStateMessageKey)
    {
        Localization = localization ?? throw new ArgumentNullException(nameof(localization));
        Key = key;
        _titleKey = titleKey;
        _descriptionKey = descriptionKey;
        _emptyStateMessageKey = emptyStateMessageKey;
        // Pages live as long as the shell that owns the localization service, so the subscription needs no teardown.
        localization.LanguageChanged += OnLanguageChanged;
    }

    /// <summary>Bound by every page view through the string indexer, e.g. <c>{Binding Localization[DashboardTitle]}</c>.</summary>
    public LocalizationService Localization { get; }

    public string Key { get; }

    public string Title => Localization[_titleKey];

    public string Description => Localization[_descriptionKey];

    public string EmptyStateMessage => Localization[_emptyStateMessageKey];

    /// <summary>Notifies page text that this view model derives from the catalog instead of binding to the indexer.</summary>
    protected virtual void RefreshLocalizedText()
    {
    }

    /// <summary>
    /// Renders a status line a page stored as a catalog key plus arguments, so a language switch re-renders it. A null
    /// key is the absent state (an empty banner), not a missing translation.
    /// </summary>
    protected string Localized(string? key, object?[] arguments) => key is null
        ? string.Empty
        : arguments.Length == 0
            ? Localization[key]
            : Localization.Format(key, arguments);

    private void OnLanguageChanged(object? sender, EventArgs eventArgs)
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Description));
        OnPropertyChanged(nameof(EmptyStateMessage));
        RefreshLocalizedText();
    }
}

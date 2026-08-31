using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace VFXComposer.Desktop.ViewModels;

public sealed class NavigationItemViewModel : ObservableObject
{
    public NavigationItemViewModel(WorkspacePageViewModel page)
    {
        Page = page ?? throw new ArgumentNullException(nameof(page));
        // The navigation label is the page title, so it has to follow the page's own language refresh.
        Page.PropertyChanged += OnPagePropertyChanged;
    }

    public string Key => Page.Key;

    public string Title => Page.Title;

    public WorkspacePageViewModel Page { get; }

    private void OnPagePropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (string.Equals(eventArgs.PropertyName, nameof(WorkspacePageViewModel.Title), StringComparison.Ordinal))
        {
            OnPropertyChanged(nameof(Title));
        }
    }
}

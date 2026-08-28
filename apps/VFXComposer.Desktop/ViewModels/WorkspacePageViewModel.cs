using CommunityToolkit.Mvvm.ComponentModel;

namespace VFXComposer.Desktop.ViewModels;

public abstract class WorkspacePageViewModel : ObservableObject
{
    protected WorkspacePageViewModel(
        string key,
        string title,
        string description,
        string emptyStateMessage)
    {
        Key = key;
        Title = title;
        Description = description;
        EmptyStateMessage = emptyStateMessage;
    }

    public string Key { get; }

    public string Title { get; }

    public string Description { get; }

    public string EmptyStateMessage { get; }
}

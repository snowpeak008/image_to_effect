namespace VFXComposer.Desktop.ViewModels;

public sealed class NavigationItemViewModel
{
    public NavigationItemViewModel(WorkspacePageViewModel page)
    {
        Page = page ?? throw new ArgumentNullException(nameof(page));
    }

    public string Key => Page.Key;

    public string Title => Page.Title;

    public WorkspacePageViewModel Page { get; }
}

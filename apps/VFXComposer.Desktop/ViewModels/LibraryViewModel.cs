namespace VFXComposer.Desktop.ViewModels;

public sealed class LibraryViewModel : WorkspacePageViewModel
{
    public LibraryViewModel()
        : base(
            "library",
            "Library",
            "Read-only Recipe, Manifest, Contract and Trace projections arrive in Phase 2.",
            "No registered project")
    {
    }
}

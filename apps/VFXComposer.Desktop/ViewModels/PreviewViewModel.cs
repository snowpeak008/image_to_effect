namespace VFXComposer.Desktop.ViewModels;

public sealed class PreviewViewModel : WorkspacePageViewModel
{
    public PreviewViewModel()
        : base(
            "preview",
            "Preview",
            "Worker-produced images, video and timeline data arrive in Phase 4.",
            "No preview job is available")
    {
    }
}

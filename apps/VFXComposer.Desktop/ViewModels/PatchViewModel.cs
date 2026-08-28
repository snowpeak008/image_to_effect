namespace VFXComposer.Desktop.ViewModels;

public sealed class PatchViewModel : WorkspacePageViewModel
{
    public PatchViewModel()
        : base(
            "patch",
            "Patch",
            "Patch validation, diff and transactional apply arrive in Phase 3.",
            "No patch is selected")
    {
    }
}

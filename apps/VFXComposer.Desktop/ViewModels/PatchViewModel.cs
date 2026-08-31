using VFXComposer.Desktop.Localization;

namespace VFXComposer.Desktop.ViewModels;

public sealed class PatchViewModel : WorkspacePageViewModel
{
    public PatchViewModel(LocalizationService localization)
        : base(
            localization,
            "patch",
            UiStringKeys.PatchTitle,
            UiStringKeys.PatchDescription,
            UiStringKeys.PatchEmptyState)
    {
    }
}

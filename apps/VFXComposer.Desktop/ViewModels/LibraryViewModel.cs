using VFXComposer.Desktop.Localization;

namespace VFXComposer.Desktop.ViewModels;

public sealed class LibraryViewModel : WorkspacePageViewModel
{
    public LibraryViewModel(LocalizationService localization)
        : base(
            localization,
            "library",
            UiStringKeys.LibraryTitle,
            UiStringKeys.LibraryDescription,
            UiStringKeys.LibraryEmptyState)
    {
    }
}

using VFXComposer.Desktop.Localization;

namespace VFXComposer.Desktop.ViewModels;

/// <summary>
/// Presentation-only authority domains. No command on this type can mint or elevate authority.
/// </summary>
public sealed class ReviewViewModel : WorkspacePageViewModel
{
    public ReviewViewModel(LocalizationService localization)
        : base(
            localization,
            "review",
            UiStringKeys.ReviewTitle,
            UiStringKeys.ReviewDescription,
            UiStringKeys.ReviewEmptyState)
    {
    }

    public string MachineStatus => Localization[UiStringKeys.ReviewMachineStatus];

    public string VisualStatus => Localization[UiStringKeys.ReviewVisualStatus];

    public string UserVerdictStatus => Localization[UiStringKeys.ReviewUserVerdictStatus];

    public string L3Status => Localization[UiStringKeys.ReviewL3Status];

    public string L4Status => Localization[UiStringKeys.ReviewL4Status];

    public string AuthorityNotice => Localization[UiStringKeys.ReviewAuthorityNotice];

    protected override void RefreshLocalizedText()
    {
        OnPropertyChanged(nameof(MachineStatus));
        OnPropertyChanged(nameof(VisualStatus));
        OnPropertyChanged(nameof(UserVerdictStatus));
        OnPropertyChanged(nameof(L3Status));
        OnPropertyChanged(nameof(L4Status));
        OnPropertyChanged(nameof(AuthorityNotice));
    }
}

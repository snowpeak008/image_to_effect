using VFXComposer.Desktop.Localization;

namespace VFXComposer.Desktop.ViewModels;

public sealed class DashboardViewModel : WorkspacePageViewModel
{
    public DashboardViewModel(LocalizationService localization)
        : base(
            localization,
            "dashboard",
            UiStringKeys.DashboardTitle,
            UiStringKeys.DashboardDescription,
            UiStringKeys.DashboardEmptyState)
    {
    }

    public string MachineStatus => Localization[UiStringKeys.DashboardMachineStatus];

    public string VisualStatus => Localization[UiStringKeys.DashboardVisualStatus];

    public string UserVerdictStatus => Localization[UiStringKeys.DashboardUserVerdictStatus];

    public string L3Status => Localization[UiStringKeys.DashboardL3Status];

    public string L4Status => Localization[UiStringKeys.DashboardL4Status];

    protected override void RefreshLocalizedText()
    {
        OnPropertyChanged(nameof(MachineStatus));
        OnPropertyChanged(nameof(VisualStatus));
        OnPropertyChanged(nameof(UserVerdictStatus));
        OnPropertyChanged(nameof(L3Status));
        OnPropertyChanged(nameof(L4Status));
    }
}

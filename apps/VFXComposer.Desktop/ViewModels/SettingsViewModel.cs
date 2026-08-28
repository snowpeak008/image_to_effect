namespace VFXComposer.Desktop.ViewModels;

public sealed class SettingsViewModel : WorkspacePageViewModel
{
    public SettingsViewModel()
        : base(
            "settings",
            "Settings",
            "Diagnostics and broker-owned project registration status.",
            "Connection unavailable — no registered project")
    {
    }

    public string SecurityNotice =>
        "Project paths and trust roots cannot be entered here. Registration is broker-owned.";
}

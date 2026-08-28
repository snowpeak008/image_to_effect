namespace VFXComposer.Desktop.ViewModels;

public sealed class DashboardViewModel : WorkspacePageViewModel
{
    public DashboardViewModel()
        : base(
            "dashboard",
            "Dashboard",
            "Project connection and independently sourced status domains.",
            "No registered project")
    {
    }

    public string MachineStatus => "Machine: Not evaluated";

    public string VisualStatus => "Visual: VISUAL_PENDING";

    public string UserVerdictStatus => "User verdict: Not signed";

    public string L3Status => "L3: Not granted";

    public string L4Status => "L4: Not granted";
}

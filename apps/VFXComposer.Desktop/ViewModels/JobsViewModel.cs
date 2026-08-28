namespace VFXComposer.Desktop.ViewModels;

public sealed class JobsViewModel : WorkspacePageViewModel
{
    public JobsViewModel()
        : base(
            "jobs",
            "Jobs",
            "Structured worker progress, logs and cancellation arrive in Phase 3.",
            "No jobs are running")
    {
    }
}

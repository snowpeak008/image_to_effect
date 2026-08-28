namespace VFXComposer.Desktop.ViewModels;

/// <summary>
/// Presentation-only authority domains. No command on this type can mint or elevate authority.
/// </summary>
public sealed class ReviewViewModel : WorkspacePageViewModel
{
    public ReviewViewModel()
        : base(
            "review",
            "Review",
            "Evidence and authority remain separate, explicit and provenance-bound.",
            "No evidence is available")
    {
    }

    public string MachineStatus => "Machine: Not evaluated";

    public string VisualStatus => "Visual: VISUAL_PENDING";

    public string UserVerdictStatus => "User verdict: Not signed";

    public string L3Status => "L3: Not granted";

    public string L4Status => "L4: Not granted";

    public string AuthorityNotice =>
        "Displayed state is not an authority grant. Visual verdicts and L3/L4 require their independent issuers.";
}

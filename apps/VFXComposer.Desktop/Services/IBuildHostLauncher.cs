namespace VFXComposer.Desktop.Services;

/// <summary>Outcome of one launch attempt: started, or refused with a stable diagnostic code.</summary>
public sealed record BuildHostLaunchOutcome(bool Started, string? DiagnosticCode)
{
    public static BuildHostLaunchOutcome Launched { get; } = new(true, null);

    public static BuildHostLaunchOutcome Refused(string diagnosticCode) => new(false, diagnosticCode);
}

/// <summary>
/// Launch seam for the private build host (ADR-008 §2.1). View models depend on this surface so
/// they stay free of process and filesystem types; the one production implementation is the
/// exempted <see cref="BuildHostLauncher"/>, and tests substitute a recording fake.
/// </summary>
public interface IBuildHostLauncher
{
    /// <summary>
    /// Starts one host process carrying exactly the draft identity: no recipe bytes, no output
    /// path, no project path. The host re-verifies the identity against the shared store, so this
    /// call grants nothing. A refusal names its stable code and starts nothing.
    /// </summary>
    BuildHostLaunchOutcome TryLaunch(string draftId, string canonicalSha256, Action<int>? exited = null);
}

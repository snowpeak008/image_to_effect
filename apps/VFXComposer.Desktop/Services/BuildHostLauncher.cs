using System.Diagnostics;

namespace VFXComposer.Desktop.Services;

/// <summary>
/// Starts the private build host for one explicit build action (ADR-008 §2.3). Its whole authority
/// is: locate the fixed-name host executable inside this shell's own deployment directory, start it
/// with exactly the two identity arguments, and register the child's exit code as fallback
/// diagnostics. It holds no pipe to the child, reads no output stream, and never terminates it —
/// the shell observes build state only through the shared job and draft stores, and a shell exit
/// leaves the child running (ADR-008 §5 last row).
/// </summary>
/// <remarks>
/// This is the second Desktop type allowed to touch the filesystem, and the only one allowed to
/// start a process. The exemption is the launcher's own deployment directory plus its own child —
/// never a project location: no constructor or method accepts any location, and the access-surface
/// scan keeps the allowance closed to exactly this type.
/// </remarks>
public sealed class BuildHostLauncher : IBuildHostLauncher
{
    /// <summary>Fixed deployment name; the launcher never searches anywhere else for it.</summary>
    public const string HostExecutableName = "VFXComposer.BuildHost.exe";

    public const string HostMissingDiagnosticCode = "BUILD_HOST_MISSING";
    public const string HostStartFailedDiagnosticCode = "BUILD_HOST_START_FAILED";
    public const string HostStartedDiagnosticCode = "BUILD_HOST_STARTED";
    public const string HostExitedDiagnosticCode = "BUILD_HOST_EXITED";

    private readonly IInMemoryDiagnosticSink _diagnostics;

    public BuildHostLauncher(IInMemoryDiagnosticSink diagnostics)
    {
        _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
    }

    public override string ToString() => "BuildHostLauncher";

    /// <summary>
    /// Launches one host process carrying the draft identity. The arguments are identity, not
    /// authorization: the host re-verifies them against the shared store and refuses drift, so a
    /// stale click costs one short-lived process and zero writes. A missing executable refuses
    /// with a stable code and starts nothing.
    /// </summary>
    public BuildHostLaunchOutcome TryLaunch(string draftId, string canonicalSha256, Action<int>? exited = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(draftId);
        ArgumentException.ThrowIfNullOrWhiteSpace(canonicalSha256);

        var deployedHost = Path.Combine(AppContext.BaseDirectory, HostExecutableName);
        if (!File.Exists(deployedHost))
        {
            _diagnostics.Record(
                HostMissingDiagnosticCode,
                "The build host executable is not deployed beside this shell; nothing was built.");
            return BuildHostLaunchOutcome.Refused(HostMissingDiagnosticCode);
        }

        var startInfo = new ProcessStartInfo(deployedHost)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = AppContext.BaseDirectory,
        };
        startInfo.ArgumentList.Add(draftId);
        startInfo.ArgumentList.Add(canonicalSha256);

        Process? child = null;
        try
        {
            child = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
            // Registration only: the handler records the exit code and releases the handle. The
            // launcher keeps no other tie to the child, so disposing this shell never touches it.
            child.Exited += (sender, _) =>
            {
                if (sender is not Process completed)
                {
                    return;
                }

                var exitCode = completed.ExitCode;
                completed.Dispose();
                _diagnostics.Record(
                    HostExitedDiagnosticCode,
                    "The build host process exited.",
                    exitCode.ToString(System.Globalization.CultureInfo.InvariantCulture));
                exited?.Invoke(exitCode);
            };
            if (!child.Start())
            {
                child.Dispose();
                _diagnostics.Record(
                    HostStartFailedDiagnosticCode,
                    "The build host executable could not be started; nothing was built.");
                return BuildHostLaunchOutcome.Refused(HostStartFailedDiagnosticCode);
            }
        }
        catch (Exception exception) when (
            exception is System.ComponentModel.Win32Exception or InvalidOperationException or PlatformNotSupportedException)
        {
            child?.Dispose();
            _diagnostics.Record(
                HostStartFailedDiagnosticCode,
                "The build host executable could not be started; nothing was built.",
                exception.GetType().Name);
            return BuildHostLaunchOutcome.Refused(HostStartFailedDiagnosticCode);
        }

        _diagnostics.Record(HostStartedDiagnosticCode, "The build host process was started for one confirmed draft.");
        return BuildHostLaunchOutcome.Launched;
    }
}

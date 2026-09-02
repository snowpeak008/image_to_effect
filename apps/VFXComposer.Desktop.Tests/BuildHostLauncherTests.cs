using Microsoft.VisualStudio.TestTools.UnitTesting;
using VFXComposer.Desktop.Services;

namespace VFXComposer.Desktop.Tests;

/// <summary>
/// The production launcher's own missing-host branch (F8c, ADR-008 §2.3): this test runs the real
/// <see cref="BuildHostLauncher"/>, not the recording fake. The test process deploys no
/// VFXComposer.BuildHost.exe beside itself, so the launcher must refuse with its stable code,
/// record the refusal in the diagnostic sink, and start no process — the refusal happens before
/// any ProcessStartInfo exists.
/// </summary>
[TestClass]
public sealed class BuildHostLauncherTests
{
    [TestMethod]
    public void AMissingHostExecutableRefusesWithTheStableCodeAndStartsNothing()
    {
        // Precondition, not cleanup: Desktop.Tests references no BuildHost project, so its output
        // directory must not carry the host executable. If this ever fails, a project or copy
        // target started leaking the host artifact into the shell's test deployment — fix that
        // reference instead of deleting the file here.
        var deployedHost = Path.Combine(AppContext.BaseDirectory, BuildHostLauncher.HostExecutableName);
        Assert.IsFalse(
            File.Exists(deployedHost),
            $"Test precondition: '{BuildHostLauncher.HostExecutableName}' must not be deployed into the "
            + "Desktop.Tests output directory; this test exercises the undeployed-host refusal.");

        var diagnostics = new InMemoryDiagnosticSink();
        var launcher = new BuildHostLauncher(diagnostics);
        var exitCallbackInvoked = false;

        var outcome = launcher.TryLaunch("draft-x", "hash-y", _ => exitCallbackInvoked = true);

        Assert.IsFalse(outcome.Started, "A missing host executable must refuse the launch.");
        Assert.AreEqual(BuildHostLauncher.HostMissingDiagnosticCode, outcome.DiagnosticCode);

        var recorded = diagnostics.Snapshot.Single();
        Assert.AreEqual(BuildHostLauncher.HostMissingDiagnosticCode, recorded.Code);

        Assert.IsFalse(
            diagnostics.Snapshot.Any(entry => entry.Code == BuildHostLauncher.HostStartedDiagnosticCode),
            "No diagnostic may claim a process start that never happened.");
        Assert.IsFalse(exitCallbackInvoked, "No process started, so no exit may ever be reported.");
    }
}

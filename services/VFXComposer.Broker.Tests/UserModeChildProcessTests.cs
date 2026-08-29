using System.Diagnostics;
using System.Runtime.Versioning;
using VFXComposer.Broker.Ipc;

namespace VFXComposer.Broker.Tests;

[TestClass]
[SupportedOSPlatform("windows")]
public sealed class UserModeChildProcessTests
{
    [TestMethod]
    public async Task LaunchPinsExactReleasePidEpochSidAndMandatoryJob()
    {
        await using var child = Launch("no-connect");

        Assert.IsTrue(child.ProcessId > 0);
        Assert.IsTrue(child.ProcessEpoch.StartsWith($"winproc-{child.ProcessId}-", StringComparison.Ordinal));
        Assert.AreEqual(16, child.ProcessEpoch[^16..].Length);
        Assert.IsTrue(child.UserSid.StartsWith("S-1-", StringComparison.Ordinal));
        Assert.AreEqual(UserModeSessionTestChild.ExpectedExecutablePath, child.ExpectedExecutablePath);
        Assert.IsTrue(child.ProcessHandle is { IsInvalid: false, IsClosed: false });
        Assert.IsTrue(child.HasActiveContainment);
        Assert.IsTrue(child.IsExactProcessActive);
        Assert.IsTrue(child.Matches(child.ProcessId, child.ProcessEpoch));
    }

    [TestMethod]
    public async Task PidOrEpochMismatchRejectsReuse()
    {
        await using var child = Launch("no-connect");

        Assert.IsFalse(child.Matches(child.ProcessId + 1, child.ProcessEpoch));
        var replacement = child.ProcessEpoch[^1] == '0' ? '1' : '0';
        Assert.IsFalse(child.Matches(child.ProcessId, child.ProcessEpoch[..^1] + replacement));
        Assert.IsFalse(child.Matches(child.ProcessId, null));
    }

    [TestMethod]
    public async Task WaitTimeoutDoesNotPromoteChildExit()
    {
        await using var child = Launch("no-connect");
        Assert.IsFalse(await child.WaitForExitAsync(TimeSpan.FromMilliseconds(100)));
        Assert.IsTrue(child.IsExactProcessActive);
    }

    [TestMethod]
    public async Task DisposeTerminatesChildAndIsConcurrentIdempotent()
    {
        var child = Launch("no-connect");
        var processId = child.ProcessId;

        await Task.WhenAll(
            child.DisposeAsync().AsTask(),
            child.DisposeAsync().AsTask(),
            Task.Run(child.Dispose));

        Assert.IsFalse(child.IsExactProcessActive);
        AssertNoProcess(processId);
    }

    [TestMethod]
    public void ExpectedPathIsCanonicalizedButRequestedImageMustAlreadyBeCanonical()
    {
        var canonical = UserModeSessionTestChild.ExpectedExecutablePath;
        var directory = Path.GetDirectoryName(canonical)!;
        var noncanonicalExpected = Path.Combine(directory, ".", Path.GetFileName(canonical));
        Assert.AreEqual(
            canonical,
            UserModeChildProcess.CanonicalizeExpectedExecutablePath(noncanonicalExpected));

        var info = UserModeSessionTestChild.Create("no-connect");
        info.FileName = noncanonicalExpected;
        Assert.ThrowsExactly<ArgumentException>(() =>
            UserModeChildProcess.Launch(canonical, info));
    }

    [TestMethod]
    public void WrongExecutableCannotBePairedWithTrustedReleasePath()
    {
        var commandProcessor = Path.Combine(Environment.SystemDirectory, "cmd.exe");
        var info = UserModeSessionTestChild.Create("no-connect");
        info.FileName = commandProcessor;
        Assert.ThrowsExactly<ArgumentException>(() =>
            UserModeChildProcess.Launch(UserModeSessionTestChild.ExpectedExecutablePath, info));

        Assert.ThrowsExactly<ArgumentException>(() =>
            UserModeChildProcess.Launch(commandProcessor, info));
    }

    [TestMethod]
    public void MissingOrNonExecutableExpectedPathIsRejected()
    {
        var info = UserModeSessionTestChild.Create("no-connect");
        Assert.ThrowsExactly<ArgumentException>(() =>
            UserModeChildProcess.Launch(
                Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".exe"),
                info));
        Assert.ThrowsExactly<ArgumentException>(() =>
            UserModeChildProcess.Launch(
                typeof(UserModeChildProcessTests).Assembly.Location,
                info));
    }

    [TestMethod]
    public void SuspendedCreateFailurePointTerminatesBeforeAssignment()
    {
        AssertLaunchFailureLeavesNoChild(UserModeChildLaunchFailurePoint.AfterSuspendedCreate);
    }

    [TestMethod]
    public void MandatoryJobAssignmentFailureTerminatesSuspendedChild()
    {
        AssertLaunchFailureLeavesNoChild(UserModeChildLaunchFailurePoint.JobAssignment);
    }

    [TestMethod]
    public void ResumeFailureTerminatesAssignedChildAndClosesJob()
    {
        AssertLaunchFailureLeavesNoChild(UserModeChildLaunchFailurePoint.Resume);
    }

    [TestMethod]
    public void ShellLaunchAndOutputHandleExpansionAreRejectedBeforeCreation()
    {
        var shell = UserModeSessionTestChild.Create("no-connect");
        shell.UseShellExecute = true;
        Assert.ThrowsExactly<ArgumentException>(() =>
            UserModeChildProcess.Launch(UserModeSessionTestChild.ExpectedExecutablePath, shell));

        var output = UserModeSessionTestChild.Create("no-connect");
        output.RedirectStandardOutput = true;
        Assert.ThrowsExactly<ArgumentException>(() =>
            UserModeChildProcess.Launch(UserModeSessionTestChild.ExpectedExecutablePath, output));
    }

    [TestMethod]
    public void MissingBootstrapChannelOrAlternateUserIsRejectedBeforeCreation()
    {
        var noInput = UserModeSessionTestChild.Create("no-connect");
        noInput.RedirectStandardInput = false;
        Assert.ThrowsExactly<ArgumentException>(() =>
            UserModeChildProcess.Launch(UserModeSessionTestChild.ExpectedExecutablePath, noInput));

        var alternateUser = UserModeSessionTestChild.Create("no-connect");
        alternateUser.UserName = "other-user";
        Assert.ThrowsExactly<ArgumentException>(() =>
            UserModeChildProcess.Launch(UserModeSessionTestChild.ExpectedExecutablePath, alternateUser));
    }

    private static UserModeChildProcess Launch(string mode) =>
        UserModeChildProcess.Launch(
            UserModeSessionTestChild.ExpectedExecutablePath,
            UserModeSessionTestChild.Create(mode));

    private static void AssertLaunchFailureLeavesNoChild(
        UserModeChildLaunchFailurePoint failurePoint)
    {
        var exception = Assert.ThrowsExactly<UserModeChildLaunchException>(() =>
            UserModeChildProcess.LaunchForTest(
                UserModeSessionTestChild.ExpectedExecutablePath,
                UserModeSessionTestChild.Create("no-connect"),
                failurePoint));
        Assert.AreEqual(failurePoint, exception.FailurePoint);
        Assert.IsNotNull(exception.ProcessId);
        AssertNoProcess(exception.ProcessId.Value);
    }

    private static void AssertNoProcess(int processId) =>
        Assert.ThrowsExactly<ArgumentException>(() => Process.GetProcessById(processId));
}

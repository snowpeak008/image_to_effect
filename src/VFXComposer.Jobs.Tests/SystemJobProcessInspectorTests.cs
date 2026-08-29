using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace VFXComposer.Jobs.Tests;

/// <summary>
/// Exercises the anti-PID-reuse discipline of REQ-003-08 against real child processes: the
/// synthetic inspector used by the queue tests cannot prove that the start-time guard actually
/// spares a foreign process.
/// </summary>
[TestClass]
public sealed class SystemJobProcessInspectorTests
{
    [TestMethod]
    public void TerminateExactSparesARealProcessWhoseRecordedStartTimeDoesNotMatch()
    {
        using var child = StartLongLivedChild();
        try
        {
            var inspector = new SystemJobProcessInspector();
            // The recorded PID is live but was created by a different process: exactly the shape
            // a recycled PID has after the original child died.
            var foreignStartUtc = ReadStartUtc(child).AddMinutes(-5);

            inspector.TerminateExact(child.Id, foreignStartUtc);

            Assert.IsFalse(
                child.WaitForExit(1_000),
                "A live PID whose start time does not match must never be terminated.");
        }
        finally
        {
            KillIfAlive(child);
        }
    }

    [TestMethod]
    public void TerminateExactTerminatesARealProcessWhenPidAndStartTimeBothMatch()
    {
        using var child = StartLongLivedChild();
        try
        {
            var inspector = new SystemJobProcessInspector();

            inspector.TerminateExact(child.Id, ReadStartUtc(child));

            Assert.IsTrue(
                child.WaitForExit(15_000),
                "An exact PID plus start-time match is the one case that must be terminated.");
        }
        finally
        {
            KillIfAlive(child);
        }
    }

    private static Process StartLongLivedChild()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("The child-process discipline mirrors Invoke-Unity.ps1 and is Windows-only.");
        }

        var startInfo = new ProcessStartInfo("powershell")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-NonInteractive");
        startInfo.ArgumentList.Add("-Command");
        startInfo.ArgumentList.Add("Start-Sleep -Seconds 120");
        return Process.Start(startInfo)
            ?? throw new AssertFailedException("The child process under test did not start.");
    }

    private static DateTimeOffset ReadStartUtc(Process process) => new(process.StartTime.ToUniversalTime());

    private static void KillIfAlive(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(15_000);
            }
        }
        catch (InvalidOperationException)
        {
            // The child exited on its own between the check and the kill; nothing to clean up.
        }
    }
}

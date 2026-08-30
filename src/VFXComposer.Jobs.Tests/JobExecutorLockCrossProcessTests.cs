using System.Diagnostics;
using System.Globalization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VFXComposer.Jobs;

namespace VFXComposer.Jobs.Tests;

/// <summary>
/// Cross-process proof of the executor single-writer lock, the case the same-process test cannot
/// reach (F3 audit 3): a real second process holds the lock, a real host in this process is refused
/// while it lives, and — the point — the holder is <em>killed</em> rather than disposed, after which
/// a fresh host takes the lock over with no lock-file cleanup. This is what makes the OS-released
/// lease claim real rather than argued.
/// </summary>
[TestClass]
public sealed class JobExecutorLockCrossProcessTests
{
    [TestMethod]
    public async Task AKilledCrossProcessHolderReleasesTheLockForAFreshHost()
    {
        var storeDirectory = JobQueueTestHarness.CreateStoreDirectory();
        var readyPath = Path.Combine(storeDirectory, "holder.ready");

        using var holder = StartHost("hold-executor", storeDirectory, readyPath);
        try
        {
            WaitForFile(readyPath, holder);

            // While the out-of-process holder is alive, a real host here is refused, fail-closed.
            var contended = new JobQueueHost(
                new JobStore(storeDirectory),
                [new DelegateJobExecutor("test.job", (_, _) => Task.CompletedTask)],
                JobQueueTestHarness.FastOptions);
            var refusal = Assert.ThrowsExactly<JobQueueException>(contended.Start);
            Assert.AreEqual(JobQueueDiagnosticCodes.ExecutorLockUnavailable, refusal.Code);
            await contended.DisposeAsync();

            // Kill — not dispose — the holder, then a fresh host takes over without any cleanup race.
            holder.Kill(entireProcessTree: true);
            WaitForExit(holder);

            var successor = new JobQueueHost(
                new JobStore(storeDirectory),
                [new DelegateJobExecutor("test.job", (_, _) => Task.CompletedTask)],
                JobQueueTestHarness.FastOptions);
            successor.Start();
            Assert.IsTrue(successor.IsExecuting, "The successor must hold the lease the killed holder left behind.");
            await successor.DisposeAsync();
        }
        finally
        {
            KillIfRunning(holder);
        }
    }

    private static Process StartHost(params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add(HostAssemblyPath());
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return Process.Start(startInfo) ?? throw new AssertFailedException("Executor-lock child host did not start.");
    }

    private static string HostAssemblyPath()
    {
        var configuration = new DirectoryInfo(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, ".."))).Name;
        var testProjectDirectory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));
        var hostPath = Path.Combine(
            testProjectDirectory,
            "JobExecutorLockHost",
            "bin",
            configuration,
            "net8.0",
            "VFXComposer.Jobs.Tests.JobExecutorLockHost.dll");
        Assert.IsTrue(File.Exists(hostPath), "Executor-lock host was not built with the test project.");
        return hostPath;
    }

    private static void WaitForFile(string path, Process process)
    {
        var stopwatch = Stopwatch.StartNew();
        while (!File.Exists(path))
        {
            if (process.HasExited)
            {
                throw new AssertFailedException(
                    "Executor-lock child exited before its barrier: " + process.ExitCode.ToString(CultureInfo.InvariantCulture));
            }

            if (stopwatch.Elapsed >= TimeSpan.FromSeconds(30))
            {
                throw new AssertFailedException("Executor-lock child did not reach its barrier.");
            }

            Thread.Sleep(20);
        }
    }

    private static void WaitForExit(Process process)
    {
        if (!process.WaitForExit(milliseconds: 30000))
        {
            throw new AssertFailedException("Executor-lock child timed out.");
        }
    }

    private static void KillIfRunning(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // The process already exited between the check and the kill.
        }
    }
}

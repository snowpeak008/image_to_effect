using System.Diagnostics;
using System.Globalization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VFXComposer.AI.Contracts;
using VFXComposer.AI.Providers;

namespace VFXComposer.AI.Tests;

[TestClass]
[DoNotParallelize]
public sealed class ProviderConfigurationRevisionLockTests
{
    private const int ConfigurationInvalidExitCode = 12;

    [TestMethod]
    public void TwoRealChildrenAtRevisionTwo_HaveExactlyOneWinner()
    {
        using var directory = new A1TestDirectory();
        var configurationPath = Path.Combine(directory.Path, "providers.json");
        new ProviderConfigurationStore(configurationPath).Save(A1TestSupport.Settings(revision: 1));

        var releasePath = Path.Combine(directory.Path, "release");
        using var first = StartHost("save-after-barrier", configurationPath, "2", Path.Combine(directory.Path, "first.ready"), releasePath);
        using var second = StartHost("save-after-barrier", configurationPath, "2", Path.Combine(directory.Path, "second.ready"), releasePath);
        try
        {
            WaitForFile(Path.Combine(directory.Path, "first.ready"), first, second);
            WaitForFile(Path.Combine(directory.Path, "second.ready"), first, second);
            File.WriteAllText(releasePath, "release");

            WaitForExit(first);
            WaitForExit(second);
            var exitCodes = new[] { first.ExitCode, second.ExitCode };
            Assert.AreEqual(1, exitCodes.Count(code => code == 0));
            Assert.AreEqual(1, exitCodes.Count(code => code == ConfigurationInvalidExitCode));
            Assert.AreEqual(2L, new ProviderConfigurationStore(configurationPath).Load().Configuration.Settings.Revision);
            AssertStableAnchorAndNoResidue(directory.Path, configurationPath);
        }
        finally
        {
            KillIfRunning(first);
            KillIfRunning(second);
        }
    }

    [TestMethod]
    public void KilledLockHolder_ReleasesAnchorForAThirdRealChild()
    {
        using var directory = new A1TestDirectory();
        var configurationPath = Path.Combine(directory.Path, "providers.json");
        new ProviderConfigurationStore(configurationPath).Save(A1TestSupport.Settings(revision: 1));

        using var holder = StartHost("hold-lock", configurationPath, Path.Combine(directory.Path, "holder.ready"));
        try
        {
            WaitForFile(Path.Combine(directory.Path, "holder.ready"), holder);
            Assert.IsTrue(File.Exists(configurationPath + ".lock"));
            holder.Kill(entireProcessTree: true);
            WaitForExit(holder);

            using var third = StartHost("save", configurationPath, "2");
            WaitForExit(third);
            Assert.AreEqual(0, third.ExitCode);
            Assert.AreEqual(2L, new ProviderConfigurationStore(configurationPath).Load().Configuration.Settings.Revision);
            AssertStableAnchorAndNoResidue(directory.Path, configurationPath);
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

        return Process.Start(startInfo) ?? throw new AssertFailedException("Revision-lock child host did not start.");
    }

    private static string HostAssemblyPath()
    {
        var configuration = new DirectoryInfo(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, ".."))).Name;
        var testProjectDirectory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));
        var hostPath = Path.Combine(
            testProjectDirectory,
            "RevisionLockHost",
            "bin",
            configuration,
            "net8.0",
            "VFXComposer.AI.Tests.RevisionLockHost.dll");
        Assert.IsTrue(File.Exists(hostPath), "Revision-lock host was not built with the test project.");
        return hostPath;
    }

    private static void WaitForFile(string path, params Process[] processes)
    {
        var stopwatch = Stopwatch.StartNew();
        while (!File.Exists(path))
        {
            foreach (var process in processes)
            {
                if (process.HasExited)
                {
                    throw new AssertFailedException("Revision-lock child exited before reaching its barrier: " + process.ExitCode.ToString(CultureInfo.InvariantCulture));
                }
            }

            if (stopwatch.Elapsed >= TimeSpan.FromSeconds(30))
            {
                throw new AssertFailedException("Revision-lock child did not reach its barrier.");
            }

            Thread.Sleep(20);
        }
    }

    private static void WaitForExit(Process process)
    {
        if (!process.WaitForExit(milliseconds: 30000))
        {
            throw new AssertFailedException("Revision-lock child timed out.");
        }
    }

    private static void AssertStableAnchorAndNoResidue(string directory, string configurationPath)
    {
        var lockPath = configurationPath + ".lock";
        Assert.IsTrue(File.Exists(lockPath), "The stable lock anchor must remain after a lease.");
        using (new FileStream(lockPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
        }

        Assert.AreEqual(0, Directory.EnumerateFiles(directory, "*.tmp", SearchOption.TopDirectoryOnly).Count());
    }

    private static void KillIfRunning(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                _ = process.WaitForExit(milliseconds: 30000);
            }
        }
        catch (InvalidOperationException)
        {
            // A process that has already exited needs no cleanup.
        }
    }
}

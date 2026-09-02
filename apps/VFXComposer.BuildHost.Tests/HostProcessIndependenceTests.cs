using System.Diagnostics;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VFXComposer.AI.Contracts.Recipes;
using VFXComposer.Batch.Core;
using VFXComposer.Jobs;

namespace VFXComposer.BuildHost.Tests;

/// <summary>
/// ADR-008 §5 last row — a Desktop exit never interrupts a build — verified in three parts, since
/// a full cross-process chain would have to touch the real current-user stores the production
/// composition root is pinned to. (1) The deployed executable is a real standalone process that
/// needs no Desktop and refuses bad usage before touching any store. (2) The host run reaches its
/// durable verdict with no observer attached anywhere: nothing Desktop-shaped exists in the run,
/// so nothing Desktop-shaped can be missed. (3) The host's environment seam offers no interrupt
/// surface a launcher could reach — the only cancellation is the host process's own lifetime.
/// </summary>
[TestClass]
public sealed class HostProcessIndependenceTests
{
    private string _root = string.Empty;

    [TestInitialize]
    public void CreateRoot() => _root = BuildHostTestHarness.CreateDirectory();

    [TestCleanup]
    public void DeleteRoot() => BuildHostTestHarness.DeleteDirectory(_root);

    [TestMethod]
    public async Task TheDeployedHostExecutableRunsStandaloneAndRefusesBadUsageBeforeAnyStore()
    {
        // The argument check is the first statement of the run, so this real-process smoke proves
        // the executable starts and exits on its own without opening any store or queue.
        var hostPath = Path.Combine(AppContext.BaseDirectory, "VFXComposer.BuildHost.exe");
        Assert.IsTrue(File.Exists(hostPath), "The host executable deploys beside its test assembly.");

        var startInfo = new ProcessStartInfo(hostPath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
        };
        startInfo.ArgumentList.Add("only-one-argument");

        using var process = Process.Start(startInfo)!;
        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        Assert.AreEqual(BuildHostExitCodes.UsageError, process.ExitCode);
        StringAssert.Contains(output, BuildHostDiagnosticCodes.UsageInvalid);
    }

    [TestMethod]
    public async Task TheBuildReachesItsDurableVerdictWithNoObserverAttachedAnywhere()
    {
        // Equivalent simulation of "Desktop exited mid-build": the run below has no callback, no
        // event subscription and no channel back to any launcher — the launcher fired the process
        // and forgot it. If the outcome lands in the two durable stores under these conditions, a
        // Desktop exit (which only removes an observer that was never required) cannot change it.
        var store = new InMemoryRecipeDraftStore();
        var saved = store.Save(BuildHostTestHarness.Draft());
        var confirmed = store.Confirm(saved.DraftId, saved.CanonicalSha256!);
        var queue = new JobStore(Path.Combine(_root, "jobs"));
        var hold = new SemaphoreSlim(0);
        var runner = new FakeUnityRecipeBuildRunner(
            UnityRecipeBuildExitCodes.Succeeded,
            _ => BuildHostTestHarness.SuccessResult(confirmed.CanonicalSha256!))
        {
            HoldUntilReleased = hold,
        };

        var run = BuildHostRunner.RunAsync(
            [confirmed.DraftId, confirmed.CanonicalSha256!],
            BuildHostTestHarness.Environment(
                new StringWriter(),
                new TestDraftSession(store),
                queue,
                BuildHostTestHarness.CreateBuildHost(_root),
                runner),
            CancellationToken.None);

        // Mid-build: the faked batchmode process is running and nobody is watching it.
        await runner.Started.Task;
        Assert.IsFalse(run.IsCompleted);

        hold.Release();
        Assert.AreEqual(BuildHostExitCodes.BuildSucceeded, await run);
        Assert.AreEqual(RecipeDraftStatus.Built, store.TryGet(confirmed.DraftId)!.Status);
        Assert.AreEqual(
            Protocol.Jobs.JobStatusStates.Succeeded,
            queue.ReadSnapshot().Jobs.Single().State,
            "Both durable stores carry the verdict for any later reader — Desktop restart included.");
    }

    [TestMethod]
    public void TheHostEnvironmentSeamOffersNoInterruptSurfaceALauncherCouldReach()
    {
        // The environment is everything a composition root can hand the run. None of its members
        // may carry a CancellationTokenSource, a process handle, or any other remote-stop shape:
        // the run's only cancellation token comes from the host process's own Main.
        var properties = typeof(BuildHostEnvironment).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        foreach (var property in properties)
        {
            Assert.AreNotEqual(typeof(CancellationTokenSource), property.PropertyType, property.Name);
            Assert.AreNotEqual(typeof(CancellationToken), property.PropertyType, property.Name);
            Assert.IsFalse(
                property.PropertyType.FullName!.StartsWith("System.Diagnostics.Process", StringComparison.Ordinal),
                property.Name);
        }

        CollectionAssert.AreEquivalent(
            new[]
            {
                "CreateProjectLockProbe",
                "CreateRunner",
                "HostOptions",
                "LocateBuildHost",
                "OpenDrafts",
                "OpenQueue",
                "Output",
                "PollInterval",
            },
            properties.Select(static property => property.Name).ToArray(),
            "The environment surface is a closed set; a new member must be reviewed against ADR-008 §5.");
    }
}

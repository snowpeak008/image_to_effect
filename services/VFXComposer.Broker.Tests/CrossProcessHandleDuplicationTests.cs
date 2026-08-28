using System.Runtime.InteropServices;
using System.Text;
using VFXComposer.Broker.Configuration;
using VFXComposer.Broker.HandleProbe;
using VFXComposer.Broker.Ipc;
using VFXComposer.Broker.Native;
using VFXComposer.Broker.Registration;
using VFXComposer.Broker.Security;
using VFXComposer.Protocol.Hashing;
using VFXComposer.Protocol.Ipc;

namespace VFXComposer.Broker.Tests;

[TestClass]
public sealed class CrossProcessHandleDuplicationTests
{
    [TestMethod]
    public async Task DuplicatesNonInheritableDirectoryHandlesOnlyIntoExactLiveWorkerProcess()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("Windows handle duplication gate is Windows-only.");
        }

        var scratch = Path.Combine(Path.GetTempPath(), "vfxcomposer-cross-process-" + Guid.NewGuid().ToString("N"));
        if (Directory.Exists(scratch))
        {
            Assert.Fail("Unique scratch root already exists.");
        }

        var repository = Path.Combine(scratch, "repository");
        var projectRoot = Path.Combine(repository, "project");
        Directory.CreateDirectory(projectRoot);
        System.Diagnostics.Process? probe = null;
        try
        {
            var probeAssembly = typeof(ProbeMarker).Assembly.Location;
            probe = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"\"{probeAssembly}\"",
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            }) ?? throw new InvalidOperationException("Handle probe did not start.");
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var ready = await probe.StandardOutput.ReadLineAsync(timeout.Token);
            Assert.AreEqual($"READY {probe.Id}", ready);
            var workerFacts = WindowsNamedPipePeerFactsSource.ObserveProcess(
                probe.Id,
                allowHandleDuplication: true);
            var desktopFacts = WindowsNamedPipePeerFactsSource.ObserveProcess(
                System.Diagnostics.Process.GetCurrentProcess().Id);
            var definition = CreateDefinition(scratch, repository);
            var policy = BrokerTestFactory.CreatePolicy(
                "vfxcomposer-cross-process-test",
                "broker-01",
                1,
                desktopFacts.UserSidIdentity,
                desktopFacts.ImageIdentity,
                workerFacts.ImageIdentity,
                [definition]);
            using var sessions = new PeerSessionRegistry(policy);
            using var registrations = new ProjectRegistrationStore(policy, sessions);
            var worker = Authenticate(
                sessions,
                PeerRoles.Worker,
                workerFacts,
                [
                    PeerCapabilityIds.PeerSessionV1,
                    PeerCapabilityIds.ReadOnlyQueryV1,
                    PeerCapabilityIds.ProjectRegistrationV1,
                    PeerCapabilityIds.WorkerHandleLifecycleV1,
                ]);
            var desktop = Authenticate(
                sessions,
                PeerRoles.Desktop,
                desktopFacts,
                [PeerCapabilityIds.PeerSessionV1, PeerCapabilityIds.ReadOnlyQueryV1]);
            Assert.IsTrue(registrations.TryRegisterPinned(
                worker,
                definition.RegisteredProjectId,
                out _,
                out _));
            Assert.IsTrue(registrations.TryAcquirePinnedLease(
                desktop,
                worker,
                definition.RegisteredProjectId,
                "lease-request-01",
                out var lease,
                out _,
                out _));
            var handles = lease!.WorkerHandles;
            Assert.IsNotNull(handles);
            Assert.AreEqual(probe.Id, handles.TargetProcessId);
            Assert.IsTrue(registrations.TryCreateWorkerHandleGrant(
                worker,
                lease,
                "worker-grant-cross-process-01",
                out var grant,
                out _));
            Assert.IsNotNull(grant);
            Assert.AreEqual(WorkerHandleLeaseState.GrantPublished, lease.HandleState);

            await probe.StandardInput.WriteLineAsync(
                $"VERIFY_HOLD {handles.VolumeHandle.ToInt64()} {handles.RepositoryHandle.ToInt64()} {handles.ProjectRootHandle.ToInt64()}");
            await probe.StandardInput.FlushAsync();
            Assert.AreEqual("PASS", await probe.StandardOutput.ReadLineAsync(timeout.Token));
            Assert.IsTrue(sessions.Revoke(worker.SessionId));
            Assert.AreEqual(WorkerHandleLeaseState.RevocationPending, lease.HandleState);
            Assert.IsFalse(registrations.RevokeLease(lease.LeaseId),
                "Session revocation must already remove the lease from the active registry.");
            Assert.IsFalse(registrations.TryCreateWorkerHandleRevoke(
                worker,
                lease.LeaseId,
                "stale-session-revoke",
                out _,
                out _), "A revoked session cannot acknowledge or receive lifecycle messages.");

            await probe.StandardInput.WriteLineAsync("CLOSE");
            await probe.StandardInput.FlushAsync();
            await probe.WaitForExitAsync(timeout.Token);
            Assert.AreEqual(0, probe.ExitCode, await probe.StandardError.ReadToEndAsync(timeout.Token));
            Assert.IsFalse(sessions.IsCurrent(worker, PeerRoles.Worker));
            Assert.IsFalse(registrations.IsCurrent(lease));
            var finalized = 0;
            for (var attempt = 0; attempt < 100 && finalized == 0; attempt++)
            {
                finalized = registrations.FinalizeExitedWorkerRevocations();
                if (finalized == 0)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(10), timeout.Token);
                }
            }

            Assert.AreEqual(1, finalized,
                "The duplicated kernel process object must become signaled after exact process exit.");
            Assert.AreEqual(WorkerHandleLeaseState.Revoked, lease.HandleState,
                "Exact process termination is the only no-ACK fallback for published handles.");
            Assert.AreEqual(0, registrations.FinalizeExitedWorkerRevocations());
        }
        finally
        {
            if (probe is { HasExited: false })
            {
                probe.Kill(entireProcessTree: true);
                await probe.WaitForExitAsync();
            }

            probe?.Dispose();
            DeleteScratch(projectRoot, repository, scratch);
        }
    }

    private static void DeleteScratch(string project, string repository, string scratch)
        => PinnedScratchTreeCleanup.DeleteExactEmptyTree(project, repository, scratch);

    private static AuthenticatedPeerSession Authenticate(
        PeerSessionRegistry sessions,
        string role,
        ObservedPeerFacts facts,
        IReadOnlyList<string> capabilities)
    {
        var hello = new PeerHello(
            $"hello-{facts.ProcessId}",
            role,
            $"peer-{facts.ProcessId}",
            facts.ProcessId,
            facts.ProcessEpoch,
            capabilities,
            facts.ImageIdentity);
        Assert.IsTrue(sessions.TryAuthenticate(
            hello,
            facts,
            out var session,
            out _,
            out _));
        return session!;
    }

    private static BrokerRegistrationDefinition CreateDefinition(string scratch, string repository)
    {
        var driveRoot = Path.GetPathRoot(scratch)
            ?? throw new InvalidOperationException("Scratch drive root is missing.");
        var volumeGuid = new StringBuilder(64);
        if (!GetVolumeNameForVolumeMountPoint(driveRoot, volumeGuid, volumeGuid.Capacity))
        {
            throw new InvalidOperationException("Volume GUID lookup failed.");
        }

        return new BrokerRegistrationDefinition(
            "project-cross-process-01",
            volumeGuid.ToString(),
            repository[driveRoot.Length..].Split(
                Path.DirectorySeparatorChar,
                StringSplitOptions.RemoveEmptyEntries),
            ["project"]);
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetVolumeNameForVolumeMountPoint(
        string volumeMountPoint,
        StringBuilder volumeName,
        int bufferLength);
}

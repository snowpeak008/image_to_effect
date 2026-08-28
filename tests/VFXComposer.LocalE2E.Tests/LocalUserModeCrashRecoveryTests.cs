using Microsoft.VisualStudio.TestTools.UnitTesting;
using VFXComposer.Client;
using VFXComposer.Protocol.Queries;

namespace VFXComposer.LocalE2E.Tests;

[TestClass]
[DoNotParallelize]
public sealed class LocalUserModeCrashRecoveryTests
{
    [TestMethod]
    public async Task WorkerCrashTransitionsPublicSessionToRecoveryAndRestartCreatesFreshEpochs()
    {
        await using var fixture = new LocalUserModeE2EFixture();
        var project = fixture.CreateUnityProject();
        await using var session = await LocalUserModeE2EFixture.ConnectDesktopSessionAsync();
        await session.SelectAsync(project.Root);
        var initialBroker = await LocalUserModeE2EFixture.WaitForRuntimeProcessAsync("VFXComposer.Broker");
        var initialWorker = await LocalUserModeE2EFixture.WaitForRuntimeProcessAsync("VFXComposer.UnityWorker");

        await LocalUserModeE2EFixture.KillRuntimeProcessAsync(initialWorker);
        await AssertFailsAsync(() => session.ReadAsync(DocumentKinds.LibraryIndex, "project").AsTask());
        Assert.AreEqual(UserModeDesktopSessionState.RecoveryRequired, session.State);

        await session.RestartAsync();
        await session.SelectAsync(project.Root);
        var recovered = await session.ReadAsync(DocumentKinds.LibraryIndex, "project");
        var restartedBroker = await LocalUserModeE2EFixture.WaitForRuntimeProcessAsync("VFXComposer.Broker");
        var restartedWorker = await LocalUserModeE2EFixture.WaitForRuntimeProcessAsync("VFXComposer.UnityWorker");

        Assert.IsTrue(recovered.Accepted);
        Assert.AreNotEqual(initialBroker.Epoch, restartedBroker.Epoch);
        Assert.AreNotEqual(initialWorker.Epoch, restartedWorker.Epoch);
        Assert.AreEqual(2L, session.Generation);

        await session.DisposeAsync();
        await LocalUserModeE2EFixture.AssertNoRuntimeResidueAsync([initialBroker, initialWorker, restartedBroker, restartedWorker]);
    }

    [TestMethod]
    public async Task BrokerCrashTransitionsPublicSessionToRecoveryAndRestartRestoresRead()
    {
        await using var fixture = new LocalUserModeE2EFixture();
        var project = fixture.CreateUnityProject();
        await using var session = await LocalUserModeE2EFixture.ConnectDesktopSessionAsync();
        await session.SelectAsync(project.Root);
        var broker = await LocalUserModeE2EFixture.WaitForRuntimeProcessAsync("VFXComposer.Broker");
        var worker = await LocalUserModeE2EFixture.WaitForRuntimeProcessAsync("VFXComposer.UnityWorker");

        await LocalUserModeE2EFixture.KillRuntimeProcessAsync(broker);
        await AssertFailsAsync(() => session.ReadAsync(DocumentKinds.LibraryIndex, "project").AsTask());
        Assert.AreEqual(UserModeDesktopSessionState.RecoveryRequired, session.State);

        await session.RestartAsync();
        await session.SelectAsync(project.Root);
        var result = await session.ReadAsync(DocumentKinds.Manifest, "sample");
        var recoveredBroker = await LocalUserModeE2EFixture.WaitForRuntimeProcessAsync("VFXComposer.Broker");
        var recoveredWorker = await LocalUserModeE2EFixture.WaitForRuntimeProcessAsync("VFXComposer.UnityWorker");

        Assert.IsTrue(result.Accepted);
        Assert.AreNotEqual(broker.Epoch, recoveredBroker.Epoch);
        Assert.AreNotEqual(worker.Epoch, recoveredWorker.Epoch);

        await session.DisposeAsync();
        await LocalUserModeE2EFixture.AssertNoRuntimeResidueAsync([broker, worker, recoveredBroker, recoveredWorker]);
    }

    [TestMethod]
    public async Task CancellationDoesNotPublishProjectContentAndTheSelectedSessionRemainsUsable()
    {
        await using var fixture = new LocalUserModeE2EFixture();
        var project = fixture.CreateUnityProject();
        await using var session = await LocalUserModeE2EFixture.ConnectDesktopSessionAsync();
        await session.SelectAsync(project.Root);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        try
        {
            await session.ReadAsync(DocumentKinds.LibraryIndex, "project", cancelled.Token);
            Assert.Fail("A pre-cancelled read unexpectedly completed.");
        }
        catch (OperationCanceledException)
        {
            // Cancellation occurs before the session enters the recovery-wrapped exchange path.
        }
        Assert.AreEqual(UserModeDesktopSessionState.Selected, session.State);
        Assert.IsNull(session.LastRead);

        var result = await session.ReadAsync(DocumentKinds.LibraryIndex, "project");
        Assert.IsTrue(result.Accepted);

        await session.DisposeAsync();
        await LocalUserModeE2EFixture.AssertNoRuntimeResidueAsync();
    }

    [TestMethod]
    public async Task ActualWorkerRejectsPartialVfxcFrameAndLeavesNoProcessOrPipeResidue()
    {
        await using var fixture = new LocalUserModeE2EFixture();
        var project = fixture.CreateUnityProject();
        LocalUserModeE2EFixture.RuntimeProcessIdentity worker;
        {
            await using var peer = await LocalUserModeE2EFixture.LocalWorkerPeer.StartAsync(project);
            worker = new LocalUserModeE2EFixture.RuntimeProcessIdentity(
                "VFXComposer.UnityWorker",
                peer.ProcessId,
                peer.WorkerProcessEpoch);
            var locator = LocalUserModeE2EFixture.CreateLocator(
                project,
                peer.Generation,
                peer.SessionId,
                peer.WorkerProcessEpoch);
            await peer.SendLocatorAsync(locator);
            var acknowledgement = await peer.ReadLocatorAcknowledgementAsync();
            Assert.AreEqual(locator.SelfHash, acknowledgement.LocatorSelfHash);

            await peer.SendPartialFrameAndCloseAsync();
            Assert.AreEqual(31, await peer.WaitForExitAsync());
        }

        await LocalUserModeE2EFixture.AssertNoRuntimeResidueAsync([worker]);
    }

    private static async Task AssertFailsAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception exception) when (exception is InvalidDataException or EndOfStreamException or
            IOException or InvalidOperationException or OperationCanceledException)
        {
            return;
        }

        Assert.Fail("The public session unexpectedly succeeded after the child crash.");
    }
}

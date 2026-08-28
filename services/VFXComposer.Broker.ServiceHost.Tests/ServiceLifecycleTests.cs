using Microsoft.VisualStudio.TestTools.UnitTesting;
using VFXComposer.Broker.ServiceHost;

namespace VFXComposer.Broker.ServiceHost.Tests;

[TestClass]
public sealed class ServiceLifecycleTests
{
    [TestMethod]
    public void LegalGraphProducesOnlyBoundedStatuses()
    {
        var lifecycle = new ServiceLifecycle();

        var startPending = lifecycle.BeginStart();
        var running = lifecycle.MarkRunning();
        var stopPending = lifecycle.RequestStop();
        var stopped = lifecycle.CompleteStop(ServiceHostDiagnostics.ServiceSpecificExitCode);
        var startPendingStatus = startPending.Status.GetValueOrDefault();
        var stoppedStatus = stopped.Status.GetValueOrDefault();

        Assert.AreEqual(ServiceLifecycleState.Stopped, startPending.Previous);
        Assert.AreEqual(ServiceLifecycleState.StartPending, startPending.Current);
        Assert.AreEqual(ServiceLifecycleState.Running, running.Current);
        Assert.AreEqual(ServiceLifecycleState.StopPending, stopPending.Current);
        Assert.AreEqual(ServiceLifecycleState.Stopped, stopped.Current);
        Assert.AreEqual(ServiceLifecycleState.Stopped, lifecycle.State);

        foreach (var status in new[] { startPending.Status, running.Status, stopPending.Status, stopped.Status })
        {
            Assert.IsTrue(status.HasValue);
            ServiceStatusSnapshot.Validate(status.GetValueOrDefault());
        }

        Assert.AreEqual(ServiceStatusSnapshot.PendingCheckpoint, startPendingStatus.Checkpoint);
        Assert.IsTrue(startPendingStatus.WaitHintMilliseconds <= ServiceStatusSnapshot.MaximumWaitHintMilliseconds);
        Assert.AreEqual(ServiceHostDiagnostics.ErrorServiceSpecificError, stoppedStatus.Win32ExitCode);
        Assert.AreEqual(ServiceHostDiagnostics.ServiceSpecificExitCode, stoppedStatus.ServiceSpecificExitCode);
    }

    [TestMethod]
    public void IllegalTransitionsAreRejected()
    {
        var lifecycle = new ServiceLifecycle();

        Assert.ThrowsExactly<InvalidOperationException>(() =>
        {
            _ = lifecycle.MarkRunning();
        });
        Assert.IsFalse(lifecycle.CompleteStop(0).Changed);

        lifecycle.BeginStart();
        Assert.ThrowsExactly<InvalidOperationException>(() => lifecycle.BeginStart());
        Assert.ThrowsExactly<InvalidOperationException>(() => lifecycle.CompleteStop(0));
    }

    [TestMethod]
    public async Task ConcurrentStopAndShutdownHaveOneStateChangingWinner()
    {
        var lifecycle = new ServiceLifecycle();
        lifecycle.BeginStart();
        lifecycle.MarkRunning();

        var results = await Task.WhenAll(Enumerable.Range(0, 64).Select(index => Task.Run(() =>
        {
            var control = index % 2 == 0 ? 1U : 5U;
            var disposition = lifecycle.HandleControl(control, out var transition);
            return (disposition, transition);
        })));

        Assert.AreEqual(64, results.Count(result => result.disposition == ServiceControlDisposition.Accepted));
        Assert.AreEqual(1, results.Count(result => result.transition.Changed));
        Assert.AreEqual(ServiceLifecycleState.StopPending, lifecycle.State);

        var stopped = lifecycle.CompleteStop(ServiceHostDiagnostics.ServiceSpecificExitCode);
        Assert.IsTrue(stopped.Changed);
        Assert.IsFalse(lifecycle.RequestStop().Changed);
        Assert.IsFalse(lifecycle.CompleteStop(ServiceHostDiagnostics.ServiceSpecificExitCode).Changed);
    }

    [TestMethod]
    public void UnsupportedControlCannotChangeState()
    {
        var lifecycle = new ServiceLifecycle();
        lifecycle.BeginStart();

        var disposition = lifecycle.HandleControl(128, out var transition);

        Assert.AreEqual(ServiceControlDisposition.Unsupported, disposition);
        Assert.IsFalse(transition.Changed);
        Assert.AreEqual(ServiceLifecycleState.StartPending, lifecycle.State);
    }

    [TestMethod]
    public void StatusValidationRejectsUnboundedOrInconsistentValues()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => ServiceStatusSnapshot.Validate(new ServiceStatusSnapshot(
            ServiceLifecycleState.StartPending,
            0,
            0,
            0,
            1,
            ServiceStatusSnapshot.MaximumWaitHintMilliseconds + 1)));

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => ServiceStatusSnapshot.Validate(new ServiceStatusSnapshot(
            ServiceLifecycleState.Stopped,
            0,
            0,
            ServiceHostDiagnostics.ServiceSpecificExitCode,
            0,
            0)));
    }
}

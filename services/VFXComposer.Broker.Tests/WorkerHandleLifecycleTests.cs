using Microsoft.Win32.SafeHandles;
using VFXComposer.Broker.Native;

namespace VFXComposer.Broker.Tests;

[TestClass]
public sealed class WorkerHandleLifecycleTests
{
    [TestMethod]
    public void UnpublishedHandlesAreBrokerClosedButPublishedNumbersAreNeverClosedByBroker()
    {
        var unpublishedClosed = new List<IntPtr>();
        using (var unpublished = CreateHandleSet(unpublishedClosed))
        {
            Assert.IsFalse(unpublished.IsPublished);
        }

        CollectionAssert.AreEqual(
            new[] { new IntPtr(0x108), new IntPtr(0x104), new IntPtr(0x100) },
            unpublishedClosed);

        var publishedClosed = new List<IntPtr>();
        using (var published = CreateHandleSet(publishedClosed))
        {
            Assert.IsTrue(published.TryMarkPublished());
            Assert.IsFalse(published.TryMarkPublished());
            published.ConfirmWorkerClosed();
            Assert.IsTrue(published.WorkerConfirmedClosed);
        }

        Assert.AreEqual(0, publishedClosed.Count,
            "A published handle number may have been reused and must never be duplicate-closed by Broker cleanup.");
    }

    [TestMethod]
    public void WorkerCloseConfirmationCannotBeForgedBeforePublication()
    {
        using var handles = CreateHandleSet([]);
        Assert.ThrowsExactly<InvalidOperationException>(handles.ConfirmWorkerClosed);
    }

    [TestMethod]
    public void PublicationAndCleanupRaceHasOnlyTwoSafeOutcomes()
    {
        for (var iteration = 0; iteration < 256; iteration++)
        {
            var closed = new List<IntPtr>();
            var gate = new Barrier(2);
            var handles = CreateHandleSet(closed);
            var published = false;
            var publishTask = Task.Run(() =>
            {
                gate.SignalAndWait();
                published = handles.TryMarkPublished();
            });
            var disposeTask = Task.Run(() =>
            {
                gate.SignalAndWait();
                handles.Dispose();
            });
            Task.WaitAll(publishTask, disposeTask);

            Assert.AreEqual(published ? 0 : 3, closed.Count,
                "Cleanup may close only an unpublished set; a published set must be abandoned without raw-number close.");
            Assert.AreEqual(published, handles.IsPublished);
        }
    }

    private static DuplicatedProjectHandleSet CreateHandleSet(ICollection<IntPtr> closed) =>
        new(
            new SafeProcessHandle(new IntPtr(0x400), ownsHandle: false),
            targetProcessId: 42,
            targetProcessEpoch: "epoch-01",
            brokerGeneration: 1,
            volumeHandle: new IntPtr(0x100),
            repositoryHandle: new IntPtr(0x104),
            projectRootHandle: new IntPtr(0x108),
            (_, handle) => closed.Add(handle));
}

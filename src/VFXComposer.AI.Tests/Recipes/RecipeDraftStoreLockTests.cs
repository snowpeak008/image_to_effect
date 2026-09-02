using System.Diagnostics;
using VFXComposer.AI.Contracts.Recipes;
using VFXComposer.AI.Providers.Recipes;
using static VFXComposer.AI.Tests.Recipes.RecipeDraftTestData;

namespace VFXComposer.AI.Tests.Recipes;

/// <summary>
/// REQ-004 RG-6 / O-5: the draft store's cross-process conflict behaviour. A foreign holder of the durable lock
/// anchor makes every member wait a bounded time and then fail closed with <see cref="RecipeDraftStoreErrorCode.StoreBusy"/>
/// without reading or writing; once the anchor is released the same call succeeds.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class RecipeDraftStoreLockTests
{
    private static readonly TimeSpan ShortTimeout = TimeSpan.FromMilliseconds(250);

    [TestMethod]
    public void EveryMemberFailsClosedWithStoreBusyWhileAForeignProcessHoldsTheAnchorAndLeavesTheFileUntouched()
    {
        using var directory = new A1TestDirectory();
        var path = StorePath(directory);
        var store = new RecipeDraftStore(path, ShortTimeout);
        var root = store.Save(Root(RecipeDraftOrigin.AiDraft));
        var head = Append(store, root).Record;
        var before = File.ReadAllBytes(path);
        var backupBefore = File.ReadAllBytes(path + ".bak");
        var lockPath = new RecipeDraftStoreLock(path).LockPath;
        Assert.IsTrue(File.Exists(lockPath), "The anchor is created by the first lease.");

        var attempts = new (string Name, Action Attempt)[]
        {
            ("Save", () => store.Save(Root(RecipeDraftOrigin.Preset))),
            ("SaveVersion", () => store.SaveVersion(Root(RecipeDraftOrigin.AiDraft))),
            ("AppendVersion", () => Append(store, head, variant: 2)),
            ("Confirm", () => store.Confirm(head.DraftId, head.CanonicalSha256!)),
            ("MarkBuilt", () => store.MarkBuilt(head.DraftId, head.CanonicalSha256!)),
            ("MarkBuildFailed", () => store.MarkBuildFailed(head.DraftId, head.CanonicalSha256!)),
            ("TruncateAfter", () => store.TruncateAfter(root.DraftId)),
            ("TryGet", () => store.TryGet(root.DraftId)),
            ("ListConfirmedAwaitingBuild", () => store.ListConfirmedAwaitingBuild()),
            ("ListLineage", () => store.ListLineage(root.LineageId)),
        };

        // Holding the anchor from a second lock instance bypasses the in-process gate exactly as another process would.
        using (new RecipeDraftStoreLock(path).Acquire())
        {
            foreach (var (name, attempt) in attempts)
            {
                var stopwatch = Stopwatch.StartNew();
                Throws(RecipeDraftStoreErrorCode.StoreBusy, attempt, name);
                Assert.IsTrue(stopwatch.Elapsed >= ShortTimeout, name + " must wait out the bounded timeout before failing.");
                Assert.IsTrue(stopwatch.Elapsed < TimeSpan.FromSeconds(10), name + " must not wait unboundedly.");
            }

            CollectionAssert.AreEqual(before, File.ReadAllBytes(path), "A busy store is never written.");
            CollectionAssert.AreEqual(backupBefore, File.ReadAllBytes(path + ".bak"), "A busy store never rotates the backup either.");
            Assert.AreEqual(0, Directory.EnumerateFiles(directory.Path, "*.tmp").Count());
        }

        var afterRelease = store.Confirm(head.DraftId, head.CanonicalSha256!);
        Assert.AreEqual(RecipeDraftStatus.ConfirmedAwaitingBuild, afterRelease.Status);
        Assert.AreEqual(1, store.ListConfirmedAwaitingBuild().Count);
        Assert.IsTrue(Append(store, afterRelease, variant: 3).SupersededDraftIds.Contains(head.DraftId));
        Assert.IsTrue(File.Exists(lockPath), "The anchor is durable: it survives every lease.");
    }

    [TestMethod]
    public void ABusyStoreDoesNotEvenReadSoAnUnreadableFileStaysUndiagnosedUntilTheLeaseIsFree()
    {
        using var directory = new A1TestDirectory();
        var path = StorePath(directory);
        new RecipeDraftStore(path).Save(Root(RecipeDraftOrigin.AiDraft));
        File.WriteAllText(path, "{ corrupt");
        var store = new RecipeDraftStore(path, ShortTimeout);

        using (new RecipeDraftStoreLock(path).Acquire())
        {
            Throws(RecipeDraftStoreErrorCode.StoreBusy, () => store.ListConfirmedAwaitingBuild(), "The lease comes before the read.");
        }

        Throws(RecipeDraftStoreErrorCode.StorageFailed, () => store.ListConfirmedAwaitingBuild());
    }

    [TestMethod]
    public void AMissingStoreAnswersReadsWithoutTakingTheLease()
    {
        using var directory = new A1TestDirectory();
        var path = StorePath(directory);
        var store = new RecipeDraftStore(path, ShortTimeout);

        using (new RecipeDraftStoreLock(path).Acquire())
        {
            Assert.IsNull(store.TryGet("draft-anything"));
            Assert.AreEqual(0, store.ListConfirmedAwaitingBuild().Count);
            Assert.AreEqual(0, store.ListLineage("lineage-anything").Count);
            Throws(RecipeDraftStoreErrorCode.StoreBusy, () => store.Save(Root(RecipeDraftOrigin.AiDraft)), "The first write still needs the lease.");
        }

        Assert.IsFalse(File.Exists(path));
    }

    [TestMethod]
    public void ConcurrentWritersFromSeparateStoreInstancesSerializeAndLoseNoRecords()
    {
        using var directory = new A1TestDirectory();
        var path = StorePath(directory);
        var seed = new RecipeDraftStore(path);
        var left = seed.Save(Root(RecipeDraftOrigin.AiDraft));
        var right = seed.Save(Root(RecipeDraftOrigin.Preset));
        const int appendsPerWriter = 6;

        using var start = new ManualResetEventSlim(false);
        var leftTask = Task.Run(() => Writer(path, left, start, appendsPerWriter));
        var rightTask = Task.Run(() => Writer(path, right, start, appendsPerWriter));
        start.Set();
        Task.WaitAll(leftTask, rightTask);

        var reopened = new RecipeDraftStore(path);
        var leftLineage = reopened.ListLineage(left.LineageId);
        var rightLineage = reopened.ListLineage(right.LineageId);
        Assert.AreEqual(1 + appendsPerWriter, leftLineage.Count, "Every append of the first writer is retained.");
        Assert.AreEqual(1 + appendsPerWriter, rightLineage.Count, "Every append of the second writer is retained.");
        AssertLinear(leftLineage);
        AssertLinear(rightLineage);
        Assert.AreEqual(1 + appendsPerWriter, leftLineage[^1].RevisionOrdinal);
        Assert.AreEqual(1 + appendsPerWriter, rightLineage[^1].RevisionOrdinal);
        Assert.IsTrue(File.Exists(path + ".bak"), "Sanity: repeated replaces leave a backup.");
        Assert.AreEqual(0, Directory.EnumerateFiles(directory.Path, "*.tmp").Count());
    }

    [TestMethod]
    public void TheLockRejectsANonPositiveTimeoutAndNamesTheAnchorNextToTheStore()
    {
        using var directory = new A1TestDirectory();
        var path = StorePath(directory);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new RecipeDraftStore(path, TimeSpan.Zero));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new RecipeDraftStoreLock(path, TimeSpan.FromSeconds(-1)));
        Assert.AreEqual(Path.GetFullPath(path) + ".lock", new RecipeDraftStoreLock(path).LockPath);
        Assert.AreEqual("RecipeDraftStore(<redacted>)", new RecipeDraftStore(path).ToString());
    }

    private static void Writer(string path, RecipeDraftRecord root, ManualResetEventSlim start, int appends)
    {
        var store = new RecipeDraftStore(path, TimeSpan.FromSeconds(30));
        start.Wait();
        var head = root;
        for (var index = 0; index < appends; index++)
        {
            head = Append(store, head, variant: index + 1).Record;
        }
    }
}

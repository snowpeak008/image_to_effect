using System.Text;
using System.Text.Json.Nodes;
using VFXComposer.AI.Contracts;
using VFXComposer.AI.Contracts.Recipes;
using VFXComposer.AI.Providers.Recipes;
using static VFXComposer.AI.Tests.Recipes.RecipeDraftTestData;

namespace VFXComposer.AI.Tests.Recipes;

/// <summary>
/// REQ-004 §7.4 / §8: the store reads only its own format version. A version mismatch and corruption are
/// different stable codes, and neither ever rewrites, renames, migrates or backs up the offending file.
/// </summary>
[TestClass]
public sealed class RecipeDraftStoreVersionTests
{
    [TestMethod]
    public void TheStoreWritesFormatVersionTwoWithLineageWatermarks()
    {
        using var directory = new A1TestDirectory();
        var path = StorePath(directory);
        var store = new RecipeDraftStore(path);
        var root = store.Save(Root(RecipeDraftOrigin.AiDraft));
        Append(store, root);

        var file = JsonNode.Parse(File.ReadAllBytes(path))!.AsObject();
        Assert.AreEqual(AiContractVersions.RecipeDraftRecordFormatVersion, file["formatVersion"]!.GetValue<int>());
        var lineages = file["lineages"]!.AsArray();
        Assert.AreEqual(1, lineages.Count);
        Assert.AreEqual(root.LineageId, lineages[0]!["lineageId"]!.GetValue<string>());
        Assert.AreEqual(2, lineages[0]!["revisionWatermark"]!.GetValue<int>());
        var records = file["records"]!.AsArray();
        Assert.AreEqual(2, records.Count);
        Assert.AreEqual("ai_draft", records[0]!["origin"]!.GetValue<string>());
        Assert.AreEqual("human_edit", records[1]!["origin"]!.GetValue<string>());
        Assert.AreEqual(root.DraftId, records[1]!["parentDraftId"]!.GetValue<string>());
    }

    [TestMethod]
    public void AVersionOneFileIsRefusedAsUnsupportedOnEveryMemberAndIsNeverTouched()
    {
        using var directory = new A1TestDirectory();
        var path = StorePath(directory);
        var seed = SeedStore(path, out var root);
        WriteVersionOneShape(path);
        var before = Snapshot(directory);

        foreach (var attempt in EveryMember(new RecipeDraftStore(path), root))
        {
            Throws(RecipeDraftStoreErrorCode.UnsupportedVersion, attempt);
        }

        Throws(RecipeDraftStoreErrorCode.UnsupportedVersion, () => new RecipeDraftStore(path).TryGet(root.DraftId));
        AssertUntouched(directory, path, before);
        Assert.AreNotEqual(seed, File.ReadAllBytes(path).Length, "Sanity: the file under test is the rewritten v1 shape.");
    }

    [TestMethod]
    public void TheUnsupportedVersionRemedyNamesOnlyTheRelativeLocation()
    {
        using var directory = new A1TestDirectory();
        var path = StorePath(directory);
        SeedStore(path, out var root);
        WriteVersionOneShape(path);

        var exception = Throws(RecipeDraftStoreErrorCode.UnsupportedVersion, () => new RecipeDraftStore(path).TryGet(root.DraftId));

        Assert.IsTrue(exception.Message.Contains(RecipeDraftStoreException.UnsupportedVersionRemedyPath, StringComparison.Ordinal));
        Assert.IsFalse(exception.Message.Contains(directory.Path, StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(exception.ToString().Contains(directory.Path, StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void AFutureIntegerVersionIsUnsupportedButANonIntegerVersionIsCorruption()
    {
        using var directory = new A1TestDirectory();
        var path = StorePath(directory);
        SeedStore(path, out var root);

        RewriteVersion(path, node => node["formatVersion"] = 3);
        var before = Snapshot(directory);
        Throws(RecipeDraftStoreErrorCode.UnsupportedVersion, () => new RecipeDraftStore(path).TryGet(root.DraftId));
        AssertUntouched(directory, path, before);

        RewriteVersion(path, node => node["formatVersion"] = 0);
        Throws(RecipeDraftStoreErrorCode.UnsupportedVersion, () => new RecipeDraftStore(path).TryGet(root.DraftId));

        RewriteVersion(path, node => node["formatVersion"] = "2");
        before = Snapshot(directory);
        Throws(RecipeDraftStoreErrorCode.StorageFailed, () => new RecipeDraftStore(path).TryGet(root.DraftId));
        AssertUntouched(directory, path, before);

        RewriteVersion(path, node => node["formatVersion"] = 2.5);
        Throws(RecipeDraftStoreErrorCode.StorageFailed, () => new RecipeDraftStore(path).TryGet(root.DraftId));

        RewriteVersion(path, node => node.Remove("formatVersion"));
        Throws(RecipeDraftStoreErrorCode.StorageFailed, () => new RecipeDraftStore(path).TryGet(root.DraftId));
    }

    [TestMethod]
    public void CorruptAndEmptyFilesAreStorageFailuresThatLeaveTheFileAlone()
    {
        using var directory = new A1TestDirectory();
        var path = StorePath(directory);
        SeedStore(path, out var root);

        File.WriteAllText(path, "{ this is no longer json");
        var before = Snapshot(directory);
        foreach (var attempt in EveryMember(new RecipeDraftStore(path), root))
        {
            Throws(RecipeDraftStoreErrorCode.StorageFailed, attempt);
        }

        AssertUntouched(directory, path, before);

        File.WriteAllBytes(path, []);
        before = Snapshot(directory);
        Throws(RecipeDraftStoreErrorCode.StorageFailed, () => new RecipeDraftStore(path).ListConfirmedAwaitingBuild());
        AssertUntouched(directory, path, before);

        File.WriteAllText(path, "[1,2,3]");
        Throws(RecipeDraftStoreErrorCode.StorageFailed, () => new RecipeDraftStore(path).ListConfirmedAwaitingBuild());
    }

    [TestMethod]
    public void AVersionTwoFileWithAnUnknownOriginFailsClosed()
    {
        using var directory = new A1TestDirectory();
        var path = StorePath(directory);
        SeedStore(path, out var root);

        RewriteVersion(path, node => node["records"]![0]!["origin"] = "ai_dream");
        var before = Snapshot(directory);

        Throws(RecipeDraftStoreErrorCode.StorageFailed, () => new RecipeDraftStore(path).TryGet(root.DraftId));
        Throws(RecipeDraftStoreErrorCode.StorageFailed, () => new RecipeDraftStore(path).Save(Root(RecipeDraftOrigin.AiDraft)));
        AssertUntouched(directory, path, before);

        RewriteVersion(path, node => node["records"]![0]!["origin"] = "Preset");
        Throws(RecipeDraftStoreErrorCode.StorageFailed, () => new RecipeDraftStore(path).TryGet(root.DraftId));

        RewriteVersion(path, node => node["records"]![0]!.AsObject().Remove("origin"));
        Throws(RecipeDraftStoreErrorCode.StorageFailed, () => new RecipeDraftStore(path).TryGet(root.DraftId));
    }

    [TestMethod]
    public void AVersionTwoFileWithOriginConditionalFieldViolationsFailsClosed()
    {
        using var directory = new A1TestDirectory();
        var path = StorePath(directory);
        SeedStore(path, out var root);

        RewriteVersion(path, node => node["records"]![0]!["feedbackText"] = "smuggled");
        Throws(RecipeDraftStoreErrorCode.StorageFailed, () => new RecipeDraftStore(path).TryGet(root.DraftId));

        RewriteVersion(path, node => node["records"]![0]!["presetId"] = "spark");
        Throws(RecipeDraftStoreErrorCode.StorageFailed, () => new RecipeDraftStore(path).TryGet(root.DraftId));

        RewriteVersion(path, node => node["records"]![0]!["guardRestorationCount"] = 1);
        Throws(RecipeDraftStoreErrorCode.StorageFailed, () => new RecipeDraftStore(path).TryGet(root.DraftId));

        RewriteVersion(path, node => node["records"]![0]!["status"] = "Archived");
        Throws(RecipeDraftStoreErrorCode.StorageFailed, () => new RecipeDraftStore(path).TryGet(root.DraftId));
    }

    [TestMethod]
    public void AVersionTwoFileWhoseChainIsBrokenFailsClosed()
    {
        using var directory = new A1TestDirectory();
        var path = StorePath(directory);
        var store = new RecipeDraftStore(path);
        var root = store.Save(Root(RecipeDraftOrigin.AiDraft));
        var second = Append(store, root).Record;
        var other = store.Save(Root(RecipeDraftOrigin.Preset));
        var pristine = File.ReadAllBytes(path);
        // The backup holds an older intact copy; without it every mutation below must surface as a failure.
        File.Delete(path + ".bak");

        Rewrite(path, pristine, node => node["records"]![1]!["parentDraftId"] = "draft-ghost");
        var before = Snapshot(directory);
        Throws(RecipeDraftStoreErrorCode.StorageFailed, () => new RecipeDraftStore(path).TryGet(second.DraftId));
        AssertUntouched(directory, path, before);

        Rewrite(path, pristine, node => node["records"]![1]!["parentDraftId"] = other.DraftId);
        Throws(RecipeDraftStoreErrorCode.StorageFailed, () => new RecipeDraftStore(path).TryGet(second.DraftId));

        Rewrite(path, pristine, node => node["records"]![1]!["parentDraftId"] = null);
        Throws(RecipeDraftStoreErrorCode.StorageFailed, () => new RecipeDraftStore(path).TryGet(second.DraftId));

        Rewrite(path, pristine, node => node["records"]![0]!["parentDraftId"] = second.DraftId);
        Throws(RecipeDraftStoreErrorCode.StorageFailed, () => new RecipeDraftStore(path).TryGet(second.DraftId));

        Rewrite(path, pristine, node => node["records"]![1]!["revisionOrdinal"] = 1);
        Throws(RecipeDraftStoreErrorCode.StorageFailed, () => new RecipeDraftStore(path).TryGet(second.DraftId));

        Rewrite(path, pristine, node => node["records"]![1]!["revisionOrdinal"] = 99);
        Throws(RecipeDraftStoreErrorCode.StorageFailed, () => new RecipeDraftStore(path).TryGet(second.DraftId));

        Rewrite(path, pristine, node => node["records"]![1]!["draftId"] = root.DraftId);
        Throws(RecipeDraftStoreErrorCode.StorageFailed, () => new RecipeDraftStore(path).TryGet(second.DraftId));

        Rewrite(path, pristine, node => node["records"]![1]!["lineageId"] = "lineage-unlisted");
        Throws(RecipeDraftStoreErrorCode.StorageFailed, () => new RecipeDraftStore(path).TryGet(second.DraftId));

        Rewrite(path, pristine, node => node["lineages"]!.AsArray().RemoveAt(0));
        Throws(RecipeDraftStoreErrorCode.StorageFailed, () => new RecipeDraftStore(path).TryGet(second.DraftId));

        Rewrite(path, pristine, node => node.Remove("lineages"));
        Throws(RecipeDraftStoreErrorCode.StorageFailed, () => new RecipeDraftStore(path).TryGet(second.DraftId));

        Rewrite(path, pristine, _ => { });
        Assert.IsNotNull(new RecipeDraftStore(path).TryGet(second.DraftId), "Sanity: the pristine file still reads.");
    }

    [TestMethod]
    public void AVersionMismatchNeverFallsBackToTheBackupCopy()
    {
        using var directory = new A1TestDirectory();
        var path = StorePath(directory);
        var store = new RecipeDraftStore(path);
        var root = store.Save(Root(RecipeDraftOrigin.AiDraft));
        store.Save(Root(RecipeDraftOrigin.Preset));
        Assert.IsTrue(File.Exists(path + ".bak"));

        WriteVersionOneShape(path);
        var before = Snapshot(directory);
        Throws(RecipeDraftStoreErrorCode.UnsupportedVersion, () => new RecipeDraftStore(path).TryGet(root.DraftId));
        AssertUntouched(directory, path, before);

        File.WriteAllText(path, "{ corrupt");
        WriteVersionOneShape(path + ".bak");
        before = Snapshot(directory);
        Throws(RecipeDraftStoreErrorCode.UnsupportedVersion, () => new RecipeDraftStore(path).TryGet(root.DraftId));
        AssertUntouched(directory, path, before);
    }

    private static int SeedStore(string path, out RecipeDraftRecord root)
    {
        root = new RecipeDraftStore(path).Save(Root(RecipeDraftOrigin.AiDraft));
        Assert.IsFalse(File.Exists(path + ".bak"), "A first save has nothing to back up.");
        return (int)new FileInfo(path).Length;
    }

    /// <summary>Every public member, so a refused file blocks the whole surface rather than one entry point.</summary>
    private static IEnumerable<Action> EveryMember(RecipeDraftStore store, RecipeDraftRecord root)
    {
        yield return () => store.TryGet(root.DraftId);
        yield return () => store.ListConfirmedAwaitingBuild();
        yield return () => store.ListLineage(root.LineageId);
        yield return () => store.Save(Root(RecipeDraftOrigin.AiDraft));
        yield return () => store.SaveVersion(Root(RecipeDraftOrigin.Preset));
        yield return () => store.Confirm(root.DraftId, root.CanonicalSha256!);
        yield return () => store.MarkBuilt(root.DraftId, root.CanonicalSha256!);
        yield return () => store.MarkBuildFailed(root.DraftId, root.CanonicalSha256!);
        yield return () => store.AppendVersion(root.DraftId, root.CanonicalSha256!, Revision(RecipeDraftOrigin.HumanEdit), DateTimeOffset.UtcNow);
        yield return () => store.TruncateAfter(root.DraftId);
    }

    /// <summary>Rewrites a v2 file into the shape the v1 codec produced: no lineages, no chain fields.</summary>
    private static void WriteVersionOneShape(string path)
    {
        var node = JsonNode.Parse(File.ReadAllBytes(path))!.AsObject();
        node["formatVersion"] = 1;
        node.Remove("lineages");
        foreach (var record in node["records"]!.AsArray())
        {
            var recordObject = record!.AsObject();
            foreach (var name in new[] { "lineageId", "parentDraftId", "revisionOrdinal", "origin", "feedbackText", "guardRestorations", "guardRestorationCount", "presetId" })
            {
                recordObject.Remove(name);
            }
        }

        File.WriteAllBytes(path, Encoding.UTF8.GetBytes(node.ToJsonString()));
    }

    private static void RewriteVersion(string path, Action<JsonObject> mutate) =>
        Rewrite(path, File.ReadAllBytes(path), mutate);

    private static void Rewrite(string path, byte[] source, Action<JsonObject> mutate)
    {
        var node = JsonNode.Parse(source)!.AsObject();
        mutate(node);
        File.WriteAllBytes(path, Encoding.UTF8.GetBytes(node.ToJsonString()));
    }

    private static Dictionary<string, byte[]> Snapshot(A1TestDirectory directory) =>
        Directory.EnumerateFiles(directory.Path)
            .Where(static file => !file.EndsWith(".lock", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(static file => Path.GetFileName(file), File.ReadAllBytes, StringComparer.OrdinalIgnoreCase);

    /// <summary>The offending file keeps its name and bytes; no backup, temp file or migrated copy appears.</summary>
    private static void AssertUntouched(A1TestDirectory directory, string path, Dictionary<string, byte[]> before)
    {
        var after = Snapshot(directory);
        CollectionAssert.AreEquivalent(before.Keys.ToArray(), after.Keys.ToArray(), "No file may appear or disappear.");
        foreach (var (name, bytes) in before)
        {
            CollectionAssert.AreEqual(bytes, after[name], "File bytes must be unchanged: " + name);
        }

        Assert.IsTrue(File.Exists(path));
        Assert.AreEqual(0, Directory.EnumerateFiles(directory.Path, "*.tmp").Count());
        Assert.AreEqual(0, Directory.EnumerateFiles(directory.Path, "*.v2*").Count());
    }
}

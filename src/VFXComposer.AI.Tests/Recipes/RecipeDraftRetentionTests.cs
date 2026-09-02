using System.Globalization;
using VFXComposer.AI.Contracts.Recipes;
using VFXComposer.AI.Providers.Recipes;
using static VFXComposer.AI.Tests.Recipes.RecipeDraftTestData;

namespace VFXComposer.AI.Tests.Recipes;

/// <summary>REQ-004 §7.5: the two-level cap, protected records, visible trim results and the read ceiling.</summary>
[TestClass]
public sealed class RecipeDraftRetentionTests
{
    private static readonly DateTimeOffset Epoch = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void LevelOneTrimsTheOldestUnprotectedVersionAndKeepsTheChainLinear()
    {
        using var directory = new A1TestDirectory();
        var path = StorePath(directory);
        var store = new RecipeDraftStore(path);
        var versions = Grow(store, store.Save(Root(RecipeDraftOrigin.AiDraft)), RecipeDraftLineageLimits.MaximumVersionsPerLineage);
        var lineageId = versions[0].LineageId;

        var seventeenth = Append(store, versions[^1], variant: 17);

        CollectionAssert.AreEqual(new[] { versions[0].DraftId }, seventeenth.TrimmedDraftIds.ToArray());
        Assert.IsFalse(seventeenth.RetainedEverything);
        Assert.AreEqual(17, seventeenth.Record.RevisionOrdinal);
        var reopened = new RecipeDraftStore(path);
        var lineage = reopened.ListLineage(lineageId);
        Assert.AreEqual(RecipeDraftLineageLimits.MaximumVersionsPerLineage, lineage.Count);
        Assert.AreEqual(versions[1].DraftId, lineage[0].DraftId, "The second version became the retained root.");
        Assert.IsNull(lineage[0].ParentDraftId);
        Assert.AreEqual(2, lineage[0].RevisionOrdinal, "Ordinals survive the splice.");
        AssertLinear(lineage);
        Assert.IsNull(reopened.TryGet(versions[0].DraftId));

        var eighteenth = Append(reopened, seventeenth.Record, variant: 18);
        CollectionAssert.AreEqual(new[] { versions[1].DraftId }, eighteenth.TrimmedDraftIds.ToArray());
        Assert.AreEqual(18, eighteenth.Record.RevisionOrdinal);
        AssertLinear(reopened.ListLineage(lineageId));
    }

    [TestMethod]
    public void LevelOneSkipsProtectedVersionsAndSplicesAroundThem()
    {
        using var directory = new A1TestDirectory();
        var path = StorePath(directory);
        var store = new RecipeDraftStore(path);
        var versions = Grow(store, store.Save(Root(RecipeDraftOrigin.AiDraft)), RecipeDraftLineageLimits.MaximumVersionsPerLineage);
        var lineageId = versions[0].LineageId;
        store.Confirm(versions[1].DraftId, versions[1].CanonicalSha256!);
        store.Confirm(versions[3].DraftId, versions[3].CanonicalSha256!);
        store.MarkBuilt(versions[3].DraftId, versions[3].CanonicalSha256!);

        var seventeenth = Append(store, versions[^1], variant: 17);
        CollectionAssert.AreEqual(new[] { versions[1].DraftId }, seventeenth.SupersededDraftIds.ToArray());
        CollectionAssert.AreEqual(new[] { versions[0].DraftId }, seventeenth.TrimmedDraftIds.ToArray());

        var eighteenth = Append(store, seventeenth.Record, variant: 18);
        CollectionAssert.AreEqual(new[] { versions[2].DraftId }, eighteenth.TrimmedDraftIds.ToArray(),
            "The superseded version is protected, so the trim skips it and takes the next unprotected one.");

        var nineteenth = Append(store, eighteenth.Record, variant: 19);
        CollectionAssert.AreEqual(new[] { versions[4].DraftId }, nineteenth.TrimmedDraftIds.ToArray(),
            "The built version is protected too.");

        var lineage = new RecipeDraftStore(path).ListLineage(lineageId);
        AssertLinear(lineage);
        Assert.AreEqual(RecipeDraftLineageLimits.MaximumVersionsPerLineage, lineage.Count);
        Assert.AreEqual(versions[1].DraftId, lineage[0].DraftId);
        Assert.AreEqual(RecipeDraftStatus.Superseded, lineage[0].Status);
        Assert.IsNull(lineage[0].ParentDraftId);
        Assert.AreEqual(versions[3].DraftId, lineage[1].DraftId);
        Assert.AreEqual(RecipeDraftStatus.Built, lineage[1].Status);
        Assert.AreEqual(versions[1].DraftId, lineage[1].ParentDraftId, "The built version was re-parented onto the superseded root.");
        Assert.AreEqual(versions[5].DraftId, lineage[2].DraftId);
        Assert.AreEqual(versions[3].DraftId, lineage[2].ParentDraftId);
    }

    [TestMethod]
    public void LevelOneByteCapTrimsBeforeTheVersionCountIsReached()
    {
        using var directory = new A1TestDirectory();
        var path = StorePath(directory);
        var store = new RecipeDraftStore(path);
        const int paddedCharacters = 120 * 1024;
        var head = store.Save(Root(RecipeDraftOrigin.AiDraft, recipeJson: PaddedRecipeJson(paddedCharacters, 0)));
        var root = head;
        for (var variant = 1; variant < 8; variant++)
        {
            var outcome = Append(store, head, recipeJson: PaddedRecipeJson(paddedCharacters, variant), variant: variant);
            Assert.IsTrue(outcome.RetainedEverything, "Eight padded versions stay under 1 MiB.");
            head = outcome.Record;
        }

        var ninth = Append(store, head, recipeJson: PaddedRecipeJson(paddedCharacters, 8), variant: 8);

        CollectionAssert.AreEqual(new[] { root.DraftId }, ninth.TrimmedDraftIds.ToArray());
        var lineage = new RecipeDraftStore(path).ListLineage(root.LineageId);
        Assert.AreEqual(8, lineage.Count, "The byte cap fired well below the 16-version cap.");
        Assert.IsTrue(lineage.Count < RecipeDraftLineageLimits.MaximumVersionsPerLineage);
        Assert.IsTrue(lineage.Sum(RecipeDraftCodec.PersistedRecipeJsonBytes) <= RecipeDraftLineageLimits.MaximumLineageRecipeJsonBytes);
        AssertLinear(lineage);
        Assert.AreEqual(9, ninth.Record.RevisionOrdinal);
    }

    [TestMethod]
    public void AFullyProtectedLineageRefusesNewVersionsAndLeavesNoResidue()
    {
        using var directory = new A1TestDirectory();
        var path = StorePath(directory);
        var store = new RecipeDraftStore(path);
        var versions = Grow(store, store.Save(Root(RecipeDraftOrigin.AiDraft)), RecipeDraftLineageLimits.MaximumVersionsPerLineage);
        foreach (var version in versions)
        {
            store.Confirm(version.DraftId, version.CanonicalSha256!);
        }

        store.MarkBuilt(versions[0].DraftId, versions[0].CanonicalSha256!);
        store.MarkBuildFailed(versions[1].DraftId, versions[1].CanonicalSha256!);
        var before = File.ReadAllBytes(path);

        Throws(RecipeDraftStoreErrorCode.LineageCapacityExhausted, () => Append(store, versions[^1], variant: 17));

        CollectionAssert.AreEqual(before, File.ReadAllBytes(path), "A refused version leaves the file byte-identical.");
        var reopened = new RecipeDraftStore(path);
        var lineage = reopened.ListLineage(versions[0].LineageId);
        Assert.AreEqual(RecipeDraftLineageLimits.MaximumVersionsPerLineage, lineage.Count);
        Assert.AreEqual(RecipeDraftStatus.Built, lineage[0].Status);
        Assert.AreEqual(RecipeDraftStatus.BuildFailed, lineage[1].Status);
        Assert.IsTrue(lineage.Skip(2).All(static version => version.Status == RecipeDraftStatus.ConfirmedAwaitingBuild),
            "The in-memory supersession of the refused append must not leak: every confirmation is still awaiting build.");
        Assert.AreEqual(14, reopened.ListConfirmedAwaitingBuild().Count);
        AssertLinear(lineage);

        var fresh = store.SaveVersion(Root(RecipeDraftOrigin.AiDraft));
        Assert.IsTrue(fresh.RetainedEverything, "The way out is a new lineage.");
    }

    [TestMethod]
    public void LevelTwoEvictsTheLeastRecentlyActiveWholeLineageAndReportsIt()
    {
        using var directory = new A1TestDirectory();
        var path = StorePath(directory);
        var store = new RecipeDraftStore(path);
        var roots = new List<RecipeDraftRecord>();
        for (var index = 0; index < RecipeDraftLineageLimits.MaximumLineages; index++)
        {
            var outcome = store.SaveVersion(Root(RecipeDraftOrigin.AiDraft, Epoch.AddHours(index)));
            Assert.IsTrue(outcome.RetainedEverything);
            roots.Add(outcome.Record);
        }

        // Lineage 0 is the oldest root but the most recently active: two versions land on it much later.
        var lineageZeroHead = Append(store, roots[0], createdUtc: Epoch.AddHours(20), variant: 1).Record;
        lineageZeroHead = Append(store, lineageZeroHead, createdUtc: Epoch.AddHours(21), variant: 2).Record;
        var lineageThreeHead = Append(store, roots[3], createdUtc: Epoch.AddHours(3).AddMinutes(1), variant: 1).Record;

        var ninth = store.SaveVersion(Root(RecipeDraftOrigin.Preset, Epoch.AddHours(30)));

        CollectionAssert.AreEqual(new[] { roots[1].LineageId }, ninth.EvictedLineageIds.ToArray());
        Assert.AreEqual(1, ninth.EvictedVersionCount);
        Assert.IsFalse(ninth.RetainedEverything);
        var reopened = new RecipeDraftStore(path);
        Assert.IsNull(reopened.TryGet(roots[1].DraftId));
        Assert.AreEqual(0, reopened.ListLineage(roots[1].LineageId).Count);
        Assert.AreEqual(3, reopened.ListLineage(roots[0].LineageId).Count, "The most recently active lineage survives despite its old root.");
        Assert.IsNotNull(reopened.TryGet(ninth.Record.DraftId));

        var tenth = store.SaveVersion(Root(RecipeDraftOrigin.AiDraft, Epoch.AddHours(31)));
        CollectionAssert.AreEqual(new[] { roots[2].LineageId }, tenth.EvictedLineageIds.ToArray());

        var eleventh = store.SaveVersion(Root(RecipeDraftOrigin.AiDraft, Epoch.AddHours(32)));
        CollectionAssert.AreEqual(new[] { roots[3].LineageId }, eleventh.EvictedLineageIds.ToArray());
        Assert.AreEqual(2, eleventh.EvictedVersionCount, "Eviction removes the whole chain, root and appended version alike.");
        Assert.IsNull(reopened.TryGet(lineageThreeHead.DraftId));

        // A confirmation is activity too: it stamps the record with the current time, which is later than every
        // synthetic timestamp above, so lineage 4 jumps to the front and lineage 5 is evicted instead.
        store.Confirm(roots[4].DraftId, roots[4].CanonicalSha256!);
        var twelfth = store.SaveVersion(Root(RecipeDraftOrigin.AiDraft, Epoch.AddHours(33)));
        CollectionAssert.AreEqual(new[] { roots[5].LineageId }, twelfth.EvictedLineageIds.ToArray());
        Assert.AreEqual(RecipeDraftStatus.ConfirmedAwaitingBuild, reopened.TryGet(roots[4].DraftId)!.Status);

        var retained = new[] { roots[0], roots[4], roots[6], roots[7], ninth.Record, tenth.Record, eleventh.Record, twelfth.Record };
        foreach (var record in retained)
        {
            Assert.IsNotNull(reopened.TryGet(record.DraftId), "Retained lineage: " + record.LineageId);
        }
    }

    [TestMethod]
    public void AppendingNeverEvictsALineageBecauseTheLineageCountDoesNotChange()
    {
        using var directory = new A1TestDirectory();
        var store = new RecipeDraftStore(StorePath(directory));
        var roots = Enumerable.Range(0, RecipeDraftLineageLimits.MaximumLineages)
            .Select(index => store.Save(Root(RecipeDraftOrigin.AiDraft, Epoch.AddHours(index))))
            .ToArray();

        var outcome = Append(store, roots[0], createdUtc: Epoch.AddDays(1));

        Assert.AreEqual(0, outcome.EvictedLineageIds.Count);
        Assert.AreEqual(0, outcome.EvictedVersionCount);
        foreach (var root in roots)
        {
            Assert.IsNotNull(store.TryGet(root.DraftId));
        }
    }

    [TestMethod]
    public void AFullStoreRoundTripsThroughANewInstanceWithinTheReadCeiling()
    {
        var ceiling = ReadCeiling();
        Assert.IsTrue(ceiling <= 32 * 1024 * 1024, "REQ-004-35: the read ceiling is at most 32 MiB.");
        Assert.IsTrue(
            ceiling >= RecipeDraftLineageLimits.MaximumLineages * RecipeDraftLineageLimits.MaximumLineageRecipeJsonBytes,
            "The ceiling must admit every lineage at its recipe-JSON cap.");

        using var directory = new A1TestDirectory();
        var path = StorePath(directory);
        var store = new RecipeDraftStore(path);
        var lineageIds = new List<string>();
        var sampled = new List<RecipeDraftRecord>();
        for (var lineageIndex = 0; lineageIndex < RecipeDraftLineageLimits.MaximumLineages; lineageIndex++)
        {
            var root = lineageIndex % 2 == 0
                ? store.Save(RecipePresetSkeletons.All[lineageIndex % RecipePresetSkeletons.All.Count].CreateDraftRecord(Epoch.AddHours(lineageIndex)))
                : store.Save(RecipeDraftRecord.Create(DraftedResult(lineageIndex), Epoch.AddHours(lineageIndex)));
            var head = root;
            for (var ordinal = 2; ordinal <= RecipeDraftLineageLimits.MaximumVersionsPerLineage; ordinal++)
            {
                var origin = ordinal % 3 == 0 ? RecipeDraftOrigin.AiRefine : RecipeDraftOrigin.HumanEdit;
                var outcome = Append(store, head, origin, createdUtc: Epoch.AddHours(lineageIndex).AddMinutes(ordinal), variant: lineageIndex * 16 + ordinal);
                Assert.IsTrue(outcome.RetainedEverything, "A full store fits without trimming.");
                head = outcome.Record;
            }

            lineageIds.Add(root.LineageId);
            sampled.Add(head);
        }

        var length = new FileInfo(path).Length;
        Assert.IsTrue(length <= ceiling, "File length " + length.ToString(CultureInfo.InvariantCulture) + " exceeds the read ceiling.");

        var reopened = new RecipeDraftStore(path);
        foreach (var lineageId in lineageIds)
        {
            var lineage = reopened.ListLineage(lineageId);
            Assert.AreEqual(RecipeDraftLineageLimits.MaximumVersionsPerLineage, lineage.Count);
            AssertLinear(lineage);
            Assert.AreEqual(RecipeDraftLineageLimits.MaximumVersionsPerLineage, lineage[^1].RevisionOrdinal);
        }

        foreach (var head in sampled)
        {
            var reloaded = reopened.TryGet(head.DraftId)!;
            Assert.AreEqual(head.CanonicalSha256, reloaded.CanonicalSha256);
            Assert.AreEqual(head.Origin, reloaded.Origin);
        }

        Assert.AreEqual(0, reopened.ListConfirmedAwaitingBuild().Count);
    }

    [TestMethod]
    public void PersistRefusesADocumentTheReaderWouldNotAcceptAndKeepsTheLastGoodFile()
    {
        using var directory = new A1TestDirectory();
        var path = StorePath(directory);
        var store = new RecipeDraftStore(path);
        var saved = new List<RecipeDraftRecord>();
        while (!File.Exists(path) || new FileInfo(path).Length + OversizedFailedRootBytes() <= ReadCeiling())
        {
            saved.Add(store.Save(RecipeDraftRecord.Create(OversizedFailedResult(), DateTimeOffset.UtcNow)));
        }

        var before = File.ReadAllBytes(path);

        Throws(RecipeDraftStoreErrorCode.StorageFailed, () => store.Save(RecipeDraftRecord.Create(OversizedFailedResult(), DateTimeOffset.UtcNow)));

        CollectionAssert.AreEqual(before, File.ReadAllBytes(path), "The last readable file stays authoritative.");
        Assert.IsTrue(saved.Count >= 2, "Sanity: the ceiling admits more than one oversized record.");
        Assert.IsNotNull(new RecipeDraftStore(path).TryGet(saved[^1].DraftId), "Everything written before the refusal is still readable.");
    }

    /// <summary>Read through a call so the assertions compare a runtime value, not a folded constant.</summary>
    private static int ReadCeiling() => RecipeDraftStore.MaximumFileBytes;

    /// <summary>A validation-failed root carrying the contract-maximum issue list; several of them exceed the ceiling.</summary>
    private static RecipeGenerationResult OversizedFailedResult()
    {
        var issues = Enumerable.Range(0, 1024).Select(index => new RecipeValidationIssue(
            "E101",
            RecipeValidationSeverity.Error,
            "/" + new string('p', 1023),
            new string('m', 512),
            new string('a', 4096),
            new string('r', 4096)));
        return RecipeGenerationResult.ValidationFailed(
            "corr-" + Guid.NewGuid().ToString("N"),
            "{}",
            issues,
            [new RecipeGenerationAttempt(1, ["E101"])],
            PromptVersion,
            CatalogVersion);
    }

    private static int OversizedFailedRootBytes() => 1024 * (16 + 1024 + 512 + 4096 + 4096);
}

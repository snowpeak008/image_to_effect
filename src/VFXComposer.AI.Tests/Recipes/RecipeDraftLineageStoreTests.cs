using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using VFXComposer.AI.Contracts.Recipes;
using VFXComposer.AI.Providers.Recipes;
using static VFXComposer.AI.Tests.Recipes.RecipeDraftTestData;

namespace VFXComposer.AI.Tests.Recipes;

/// <summary>REQ-004 §7.1–§7.3: linear chains, the four origins, supersession, truncation and the cross-entry backlog.</summary>
[TestClass]
public sealed class RecipeDraftLineageStoreTests
{
    [TestMethod]
    public void EveryOriginLandsWithItsChainFieldsAndSurvivesARestart()
    {
        using var directory = new A1TestDirectory();
        var path = StorePath(directory);
        var store = new RecipeDraftStore(path);
        var created = new DateTimeOffset(2026, 9, 1, 8, 0, 0, TimeSpan.Zero);

        var preset = store.SaveVersion(RecipePresetSkeletons.All[2].CreateDraftRecord(created));
        Assert.IsTrue(preset.RetainedEverything);
        Assert.AreEqual(RecipeDraftOrigin.Preset, preset.Record.Origin);
        Assert.AreEqual(RecipePresetSkeletons.All[2].PresetId, preset.Record.PresetId);
        Assert.AreEqual("preset-" + RecipePresetSkeletons.All[2].PresetId, preset.Record.CorrelationId);
        Assert.AreEqual(RecipePresetSkeleton.PresetPromptTemplateVersion, preset.Record.PromptTemplateVersion);

        var aiRoot = store.SaveVersion(RecipeDraftRecord.Create(DraftedResult(), created)).Record;
        Assert.AreEqual(RecipeDraftOrigin.AiDraft, aiRoot.Origin);
        Assert.AreEqual(1, aiRoot.RevisionOrdinal);
        Assert.IsNull(aiRoot.ParentDraftId);

        var humanEdit = store.AppendVersion(
            aiRoot.DraftId,
            aiRoot.CanonicalSha256!,
            new RecipeDraftRevision(Draft(RealRecipeJson(1)), RecipeDraftOrigin.HumanEdit),
            created.AddMinutes(1));
        Assert.IsTrue(humanEdit.RetainedEverything);
        Assert.AreEqual(RecipeDraftOrigin.HumanEdit, humanEdit.Record.Origin);
        Assert.AreEqual(aiRoot.LineageId, humanEdit.Record.LineageId);
        Assert.AreEqual(aiRoot.DraftId, humanEdit.Record.ParentDraftId);
        Assert.AreEqual(2, humanEdit.Record.RevisionOrdinal);
        Assert.AreEqual(RecipeDraftStatus.PendingConfirmation, humanEdit.Record.Status);
        Assert.AreEqual(0, humanEdit.Record.RequestCount);
        Assert.AreEqual(created.AddMinutes(1), humanEdit.Record.CreatedUtc);

        var restorations = new[]
        {
            new RecipeGuardRestoration("/stages/0/modules/0/parameters/lifetime", humanEdit.Record.DraftId),
            new RecipeGuardRestoration("/stages/0/modules/0/parameters/size", humanEdit.Record.DraftId),
        };
        var refine = store.AppendVersion(
            humanEdit.Record.DraftId,
            humanEdit.Record.CanonicalSha256!,
            new RecipeDraftRevision(Draft(RealRecipeJson(2)), RecipeDraftOrigin.AiRefine, requestCount: 2, "shorter trail", restorations),
            created.AddMinutes(2));
        Assert.AreEqual(RecipeDraftOrigin.AiRefine, refine.Record.Origin);
        Assert.AreEqual(3, refine.Record.RevisionOrdinal);
        Assert.AreEqual(humanEdit.Record.DraftId, refine.Record.ParentDraftId);
        Assert.AreEqual("shorter trail", refine.Record.FeedbackText);
        Assert.AreEqual(2, refine.Record.GuardRestorationCount);
        Assert.AreEqual(2, refine.Record.RequestCount);

        var reopened = new RecipeDraftStore(path);
        var lineage = reopened.ListLineage(aiRoot.LineageId);
        CollectionAssert.AreEqual(
            new[] { aiRoot.DraftId, humanEdit.Record.DraftId, refine.Record.DraftId },
            lineage.Select(static record => record.DraftId).ToArray());
        AssertLinear(lineage);
        var reloadedRefine = reopened.TryGet(refine.Record.DraftId)!;
        Assert.AreEqual("shorter trail", reloadedRefine.FeedbackText);
        Assert.AreEqual(2, reloadedRefine.GuardRestorations.Count);
        Assert.AreEqual("/stages/0/modules/0/parameters/size", reloadedRefine.GuardRestorations[1].ParameterPath);
        Assert.AreEqual(humanEdit.Record.DraftId, reloadedRefine.GuardRestorations[1].SourceDraftId);
        Assert.AreEqual(RealRecipeJson(2), reloadedRefine.RecipeJson);
        var reloadedPreset = reopened.TryGet(preset.Record.DraftId)!;
        Assert.AreEqual(RecipeDraftOrigin.Preset, reloadedPreset.Origin);
        Assert.AreEqual(RecipePresetSkeletons.All[2].PresetId, reloadedPreset.PresetId);
        Assert.AreEqual(1, reopened.ListLineage(preset.Record.LineageId).Count);
        Assert.AreEqual(0, reopened.ListLineage("lineage-unknown").Count);
    }

    [TestMethod]
    public void ARefinementKeepsOnlyTheBoundedRestorationListButTheFullCount()
    {
        using var directory = new A1TestDirectory();
        var store = new RecipeDraftStore(StorePath(directory));
        var root = store.Save(Root(RecipeDraftOrigin.AiDraft));
        var restorations = Enumerable.Range(0, 70)
            .Select(index => new RecipeGuardRestoration("/p/" + index.ToString(CultureInfo.InvariantCulture), root.DraftId));

        var refine = store.AppendVersion(
            root.DraftId,
            root.CanonicalSha256!,
            new RecipeDraftRevision(Draft(RealRecipeJson(1)), RecipeDraftOrigin.AiRefine, 1, "less glow", restorations),
            DateTimeOffset.UtcNow).Record;

        Assert.AreEqual(RecipeDraftLineageLimits.MaximumGuardRestorations, refine.GuardRestorations.Count);
        Assert.AreEqual(70, refine.GuardRestorationCount);
        var reloaded = new RecipeDraftStore(StorePath(directory)).TryGet(refine.DraftId)!;
        Assert.AreEqual(64, reloaded.GuardRestorations.Count);
        Assert.AreEqual(70, reloaded.GuardRestorationCount);
    }

    [TestMethod]
    public void AppendRefusesMissingNonHeadFailedAndStaleParentsWithoutWriting()
    {
        using var directory = new A1TestDirectory();
        var path = StorePath(directory);
        var store = new RecipeDraftStore(path);
        var root = store.Save(Root(RecipeDraftOrigin.AiDraft));
        var head = Append(store, root).Record;
        var failed = store.Save(RecipeDraftRecord.Create(FailedResult(), DateTimeOffset.UtcNow));
        var before = File.ReadAllBytes(path);

        Throws(RecipeDraftStoreErrorCode.NotFound, () => Append(store, Root(RecipeDraftOrigin.AiDraft)), "An unsaved parent does not exist.");
        Throws(RecipeDraftStoreErrorCode.NotLineageHead, () => Append(store, root));
        Throws(RecipeDraftStoreErrorCode.InvalidStatus, () => store.AppendVersion(
            failed.DraftId, new string('a', 64), Revision(RecipeDraftOrigin.HumanEdit), DateTimeOffset.UtcNow));
        Throws(RecipeDraftStoreErrorCode.HashMismatch, () => store.AppendVersion(
            head.DraftId, new string('0', 64), Revision(RecipeDraftOrigin.HumanEdit), DateTimeOffset.UtcNow));
        Assert.ThrowsExactly<ArgumentException>(() => store.AppendVersion(
            head.DraftId, head.CanonicalSha256!, Revision(RecipeDraftOrigin.HumanEdit), new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(8))));

        CollectionAssert.AreEqual(before, File.ReadAllBytes(path), "A refused append never touches the file.");
        Assert.AreEqual(2, store.ListLineage(root.LineageId).Count);
    }

    [TestMethod]
    public void AppendFailsClosedWhenTheLineageWatermarkCannotAdvance()
    {
        using var directory = new A1TestDirectory();
        var path = StorePath(directory);
        var store = new RecipeDraftStore(path);
        var root = store.Save(Root(RecipeDraftOrigin.AiDraft));
        var head = Append(store, root).Record;
        var file = JsonNode.Parse(File.ReadAllBytes(path))!.AsObject();
        file["lineages"]![0]!["revisionWatermark"] = int.MaxValue;
        File.WriteAllBytes(path, Encoding.UTF8.GetBytes(file.ToJsonString()));
        var before = File.ReadAllBytes(path);

        Throws(RecipeDraftStoreErrorCode.StorageFailed, () => Append(store, head, variant: 2),
            "A spent ordinal space is a storage fault with a stable code, not an unencoded overflow.");

        CollectionAssert.AreEqual(before, File.ReadAllBytes(path), "The refused append never touches the file.");
        var lineage = new RecipeDraftStore(path).ListLineage(root.LineageId);
        Assert.AreEqual(2, lineage.Count, "Sanity: the synthesized file still reads with both versions.");
        AssertLinear(lineage);
    }

    [TestMethod]
    public void SaveVersionAcceptsOnlyFreshRootsAssignedByTheStore()
    {
        using var directory = new A1TestDirectory();
        var path = StorePath(directory);
        var store = new RecipeDraftStore(path);
        var root = store.SaveVersion(Root(RecipeDraftOrigin.AiDraft)).Record;
        var before = File.ReadAllBytes(path);

        var withParent = Root(RecipeDraftOrigin.AiDraft).WithParentDraftId(root.DraftId);
        Throws(RecipeDraftStoreErrorCode.RecordInvalid, () => store.SaveVersion(withParent));
        Throws(RecipeDraftStoreErrorCode.RecordInvalid, () => store.Save(withParent));
        Throws(RecipeDraftStoreErrorCode.RecordInvalid, () => store.SaveVersion(root), "The draft identifier already exists.");
        Throws(RecipeDraftStoreErrorCode.RecordInvalid, () => store.SaveVersion(SameLineage(root)), "The lineage identifier already exists.");
        Throws(RecipeDraftStoreErrorCode.RecordInvalid, () => store.SaveVersion(OrdinalTwo()));

        CollectionAssert.AreEqual(before, File.ReadAllBytes(path));
        Assert.AreEqual(1, store.ListLineage(root.LineageId).Count);
    }

    [TestMethod]
    public void ANewVersionSupersedesTheLineagesConfirmedVersion()
    {
        using var directory = new A1TestDirectory();
        var path = StorePath(directory);
        var store = new RecipeDraftStore(path);
        var root = store.Save(Root(RecipeDraftOrigin.Preset));
        var otherLineage = store.Save(Root(RecipeDraftOrigin.AiDraft));
        store.Confirm(root.DraftId, root.CanonicalSha256!);
        store.Confirm(otherLineage.DraftId, otherLineage.CanonicalSha256!);
        var appendedAt = new DateTimeOffset(2026, 9, 2, 9, 0, 0, TimeSpan.Zero);

        var outcome = Append(store, root, createdUtc: appendedAt);

        CollectionAssert.AreEqual(new[] { root.DraftId }, outcome.SupersededDraftIds.ToArray());
        Assert.IsFalse(outcome.RetainedEverything);
        var superseded = new RecipeDraftStore(path).TryGet(root.DraftId)!;
        Assert.AreEqual(RecipeDraftStatus.Superseded, superseded.Status);
        Assert.AreEqual(root.CanonicalSha256, superseded.CanonicalSha256, "Supersession changes the state, never the content.");
        Assert.AreEqual(appendedAt, superseded.UpdatedUtc);
        Assert.AreEqual(1, superseded.RevisionOrdinal);
        Assert.AreEqual(RecipeDraftStatus.PendingConfirmation, outcome.Record.Status);

        var backlog = store.ListConfirmedAwaitingBuild();
        CollectionAssert.AreEqual(new[] { otherLineage.DraftId }, backlog.Select(static record => record.DraftId).ToArray(),
            "The superseded version leaves the backlog; the other lineage's confirmation is untouched.");

        Throws(RecipeDraftStoreErrorCode.InvalidStatus, () => store.Confirm(root.DraftId, root.CanonicalSha256!));
        Throws(RecipeDraftStoreErrorCode.InvalidStatus, () => store.MarkBuilt(root.DraftId, root.CanonicalSha256!));
        Throws(RecipeDraftStoreErrorCode.InvalidStatus, () => store.MarkBuildFailed(root.DraftId, root.CanonicalSha256!));
        Assert.AreEqual(RecipeDraftStatus.Superseded, store.TryGet(root.DraftId)!.Status);

        var confirmedHead = store.Confirm(outcome.Record.DraftId, outcome.Record.CanonicalSha256!);
        Assert.AreEqual(RecipeDraftStatus.ConfirmedAwaitingBuild, confirmedHead.Status);
        Assert.AreEqual(2, store.ListConfirmedAwaitingBuild().Count);
    }

    [TestMethod]
    public void BuiltBuildFailedAndFailedVersionsAreNeverSuperseded()
    {
        using var directory = new A1TestDirectory();
        var store = new RecipeDraftStore(StorePath(directory));

        var built = store.Save(Root(RecipeDraftOrigin.AiDraft));
        store.Confirm(built.DraftId, built.CanonicalSha256!);
        store.MarkBuilt(built.DraftId, built.CanonicalSha256!);
        var afterBuilt = Append(store, built);
        Assert.AreEqual(0, afterBuilt.SupersededDraftIds.Count);
        Assert.AreEqual(RecipeDraftStatus.Built, store.TryGet(built.DraftId)!.Status);
        Assert.AreEqual(built.DraftId, afterBuilt.Record.ParentDraftId);

        var buildFailed = store.Save(Root(RecipeDraftOrigin.AiDraft));
        store.Confirm(buildFailed.DraftId, buildFailed.CanonicalSha256!);
        store.MarkBuildFailed(buildFailed.DraftId, buildFailed.CanonicalSha256!);
        var afterBuildFailed = Append(store, buildFailed);
        Assert.AreEqual(0, afterBuildFailed.SupersededDraftIds.Count);
        Assert.AreEqual(RecipeDraftStatus.BuildFailed, store.TryGet(buildFailed.DraftId)!.Status);

        var failed = store.Save(RecipeDraftRecord.Create(FailedResult(), DateTimeOffset.UtcNow));
        Assert.AreEqual(RecipeDraftStatus.Failed, store.TryGet(failed.DraftId)!.Status);
        Assert.AreEqual(0, store.ListConfirmedAwaitingBuild().Count);
    }

    [TestMethod]
    public void TruncationDeletesLaterVersionsAndNeverReusesOrdinals()
    {
        using var directory = new A1TestDirectory();
        var path = StorePath(directory);
        var store = new RecipeDraftStore(path);
        var versions = Grow(store, store.Save(Root(RecipeDraftOrigin.AiDraft)), 5);

        var outcome = store.TruncateAfter(versions[1].DraftId);

        Assert.AreEqual(versions[1].DraftId, outcome.Head.DraftId);
        CollectionAssert.AreEqual(
            versions.Skip(2).Select(static version => version.DraftId).ToArray(),
            outcome.RemovedDraftIds.ToArray());
        var reopened = new RecipeDraftStore(path);
        var lineage = reopened.ListLineage(versions[0].LineageId);
        CollectionAssert.AreEqual(new[] { versions[0].DraftId, versions[1].DraftId }, lineage.Select(static version => version.DraftId).ToArray());
        AssertLinear(lineage);
        Assert.IsNull(reopened.TryGet(versions[2].DraftId));
        Assert.IsNull(reopened.TryGet(versions[4].DraftId));

        var resumed = Append(reopened, versions[1]).Record;
        Assert.AreEqual(6, resumed.RevisionOrdinal, "Ordinals 3..5 were used once and are never reused.");
        Assert.AreEqual(versions[1].DraftId, resumed.ParentDraftId);
        AssertLinear(reopened.ListLineage(versions[0].LineageId));

        var before = File.ReadAllBytes(path);
        var noOp = store.TruncateAfter(resumed.DraftId);
        Assert.AreEqual(0, noOp.RemovedDraftIds.Count);
        Assert.AreEqual(resumed.DraftId, noOp.Head.DraftId);
        CollectionAssert.AreEqual(before, File.ReadAllBytes(path), "Truncating at the head writes nothing.");

        Throws(RecipeDraftStoreErrorCode.NotFound, () => store.TruncateAfter("draft-missing"));
        Throws(RecipeDraftStoreErrorCode.NotFound, () => store.TruncateAfter(versions[3].DraftId), "A deleted version is gone.");
    }

    [TestMethod]
    public void TruncationIsRefusedWhenItWouldDeleteAnAuditRecord()
    {
        using var directory = new A1TestDirectory();
        var path = StorePath(directory);
        var store = new RecipeDraftStore(path);
        var versions = Grow(store, store.Save(Root(RecipeDraftOrigin.AiDraft)), 4);
        store.Confirm(versions[2].DraftId, versions[2].CanonicalSha256!);
        var before = File.ReadAllBytes(path);

        Throws(RecipeDraftStoreErrorCode.TruncationBlocked, () => store.TruncateAfter(versions[0].DraftId));
        Throws(RecipeDraftStoreErrorCode.TruncationBlocked, () => store.TruncateAfter(versions[1].DraftId));
        CollectionAssert.AreEqual(before, File.ReadAllBytes(path));
        Assert.AreEqual(4, store.ListLineage(versions[0].LineageId).Count);

        store.MarkBuilt(versions[2].DraftId, versions[2].CanonicalSha256!);
        Throws(RecipeDraftStoreErrorCode.TruncationBlocked, () => store.TruncateAfter(versions[1].DraftId));

        var allowed = store.TruncateAfter(versions[2].DraftId);
        CollectionAssert.AreEqual(new[] { versions[3].DraftId }, allowed.RemovedDraftIds.ToArray(),
            "Deleting only pending versions above the built one is allowed.");

        var other = Grow(store, store.Save(Root(RecipeDraftOrigin.Preset)), 3);
        store.Confirm(other[2].DraftId, other[2].CanonicalSha256!);
        store.MarkBuildFailed(other[2].DraftId, other[2].CanonicalSha256!);
        Throws(RecipeDraftStoreErrorCode.TruncationBlocked, () => store.TruncateAfter(other[0].DraftId));

        // REQ-004 §7.3 rule 3 names exactly three blocking states; a superseded version is retained as history by
        // the level-1 trim but is not an audit record, so it may be truncated away.
        var superseded = Grow(store, store.Save(Root(RecipeDraftOrigin.AiDraft)), 2);
        store.Confirm(superseded[1].DraftId, superseded[1].CanonicalSha256!);
        Append(store, superseded[1]);
        Assert.AreEqual(RecipeDraftStatus.Superseded, store.TryGet(superseded[1].DraftId)!.Status);
        Assert.AreEqual(2, store.TruncateAfter(superseded[0].DraftId).RemovedDraftIds.Count);
    }

    [TestMethod]
    public void TheBacklogSpansEveryOriginInConfirmationOrderAndBuildsRegardlessOfOrigin()
    {
        using var directory = new A1TestDirectory();
        var store = new RecipeDraftStore(StorePath(directory));
        var preset = store.Save(RecipePresetSkeletons.All[1].CreateDraftRecord(DateTimeOffset.UtcNow));
        var aiDraft = store.Save(RecipeDraftRecord.Create(DraftedResult(), DateTimeOffset.UtcNow));
        var humanEdit = Append(store, store.Save(Root(RecipeDraftOrigin.AiDraft)), RecipeDraftOrigin.HumanEdit).Record;
        var aiRefine = Append(store, store.Save(Root(RecipeDraftOrigin.AiDraft)), RecipeDraftOrigin.AiRefine).Record;

        foreach (var record in new[] { humanEdit, preset, aiRefine, aiDraft })
        {
            store.Confirm(record.DraftId, record.CanonicalSha256!);
            Thread.Sleep(5);
        }

        var backlog = store.ListConfirmedAwaitingBuild();
        CollectionAssert.AreEqual(
            new[] { humanEdit.DraftId, preset.DraftId, aiRefine.DraftId, aiDraft.DraftId },
            backlog.Select(static record => record.DraftId).ToArray(),
            "Oldest confirmation first, whatever the origin.");
        CollectionAssert.AreEqual(
            new[] { RecipeDraftOrigin.HumanEdit, RecipeDraftOrigin.Preset, RecipeDraftOrigin.AiRefine, RecipeDraftOrigin.AiDraft },
            backlog.Select(static record => record.Origin).ToArray());

        Assert.AreEqual(RecipeDraftStatus.Built, store.MarkBuilt(humanEdit.DraftId, humanEdit.CanonicalSha256!).Status);
        Assert.AreEqual(RecipeDraftStatus.Built, store.MarkBuilt(preset.DraftId, preset.CanonicalSha256!).Status);
        Assert.AreEqual(RecipeDraftStatus.BuildFailed, store.MarkBuildFailed(aiRefine.DraftId, aiRefine.CanonicalSha256!).Status);
        Assert.AreEqual(RecipeDraftStatus.Built, store.MarkBuilt(aiDraft.DraftId, aiDraft.CanonicalSha256!).Status);
        Assert.AreEqual(0, store.ListConfirmedAwaitingBuild().Count);
    }

    private static RecipeDraftRecord SameLineage(RecipeDraftRecord existing) => new(
        RecipeDraftRecord.NewDraftId(),
        RecipeDraftStatus.PendingConfirmation,
        existing.CreatedUtc,
        existing.UpdatedUtc,
        existing.CorrelationId,
        existing.PromptTemplateVersion,
        existing.TemplateCatalogVersion,
        existing.RecipeJson,
        existing.CanonicalSha256,
        existing.RecipeId,
        existing.Archetype,
        existing.Dimension,
        existing.TargetProfile,
        existing.Issues,
        existing.RequestCount,
        RecipeDraftProvenance.Root(existing.LineageId, RecipeDraftOrigin.AiDraft));

    private static RecipeDraftRecord OrdinalTwo()
    {
        var draft = Draft(RealRecipeJson(3));
        return new RecipeDraftRecord(
            RecipeDraftRecord.NewDraftId(),
            RecipeDraftStatus.PendingConfirmation,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            draft.CorrelationId,
            draft.PromptTemplateVersion,
            draft.TemplateCatalogVersion,
            draft.RecipeJson,
            draft.CanonicalSha256,
            draft.RecipeId,
            draft.Archetype,
            draft.Dimension,
            draft.TargetProfile,
            Array.Empty<RecipeValidationIssue>(),
            1,
            new RecipeDraftProvenance(RecipeDraftProvenance.NewLineageId(), null, 2, RecipeDraftOrigin.AiDraft));
    }
}

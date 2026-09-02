using System.Globalization;
using VFXComposer.AI.Contracts;
using VFXComposer.AI.Contracts.Recipes;

namespace VFXComposer.AI.Tests.Recipes;

/// <summary>REQ-004 §7.2 field contract: the four-value origin closed set and its conditional fields.</summary>
[TestClass]
public sealed class RecipeDraftProvenanceContractTests
{
    [TestMethod]
    public void TheOriginWireNamesRoundTripAsAClosedSet()
    {
        var names = Enum.GetValues<RecipeDraftOrigin>().Select(RecipeDraftOriginNames.Of).ToArray();
        CollectionAssert.AreEquivalent(new[] { "preset", "ai_draft", "ai_refine", "human_edit" }, names);
        foreach (var origin in Enum.GetValues<RecipeDraftOrigin>())
        {
            Assert.IsTrue(RecipeDraftOriginNames.TryParse(RecipeDraftOriginNames.Of(origin), out var parsed));
            Assert.AreEqual(origin, parsed);
        }

        Assert.IsFalse(RecipeDraftOriginNames.TryParse("Preset", out _), "Wire names are case-sensitive.");
        Assert.IsFalse(RecipeDraftOriginNames.TryParse("ai-draft", out _));
        Assert.IsFalse(RecipeDraftOriginNames.TryParse(null, out _));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => RecipeDraftOriginNames.Of((RecipeDraftOrigin)42));
    }

    [TestMethod]
    public void EachOriginCarriesExactlyItsConditionalFields()
    {
        var preset = RecipeDraftProvenance.Root("lineage-a", RecipeDraftOrigin.Preset, "spark_projectile");
        Assert.AreEqual("spark_projectile", preset.PresetId);
        Assert.IsNull(preset.FeedbackText);
        Assert.AreEqual(0, preset.GuardRestorations.Count);
        Assert.AreEqual(1, preset.RevisionOrdinal);
        Assert.IsNull(preset.ParentDraftId);

        var aiDraft = RecipeDraftProvenance.Root("lineage-b", RecipeDraftOrigin.AiDraft);
        Assert.IsNull(aiDraft.PresetId);
        Assert.IsNull(aiDraft.FeedbackText);

        var restoration = new RecipeGuardRestoration("/stages/0/modules/0/parameters/lifetime", "draft-parent");
        var refine = new RecipeDraftProvenance(
            "lineage-c",
            "draft-parent",
            revisionOrdinal: 3,
            RecipeDraftOrigin.AiRefine,
            feedbackText: "shorter trail",
            guardRestorations: [restoration],
            guardRestorationCount: 5);
        Assert.AreEqual("shorter trail", refine.FeedbackText);
        Assert.AreEqual(1, refine.GuardRestorations.Count);
        Assert.AreEqual(5, refine.GuardRestorationCount, "The count may exceed the retained list.");
        Assert.AreEqual("draft-parent", refine.ParentDraftId);

        var humanEdit = new RecipeDraftProvenance("lineage-d", "draft-parent", 2, RecipeDraftOrigin.HumanEdit);
        Assert.IsNull(humanEdit.FeedbackText);
        Assert.IsNull(humanEdit.PresetId);
    }

    [TestMethod]
    public void MismatchedConditionalFieldsAreRefused()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => RecipeDraftProvenance.Root("lineage-a", RecipeDraftOrigin.Preset),
            "A preset root needs its preset identifier.");
        Assert.ThrowsExactly<ArgumentException>(() => RecipeDraftProvenance.Root("lineage-a", RecipeDraftOrigin.AiDraft, "spark"),
            "Only presets carry a preset identifier.");
        Assert.ThrowsExactly<ArgumentNullException>(() => new RecipeDraftProvenance("lineage-a", "draft-p", 2, RecipeDraftOrigin.AiRefine),
            "An AI refinement needs its feedback text.");
        Assert.ThrowsExactly<ArgumentException>(() => new RecipeDraftProvenance("lineage-a", "draft-p", 2, RecipeDraftOrigin.AiRefine, "   "));
        Assert.ThrowsExactly<ArgumentException>(() => new RecipeDraftProvenance("lineage-a", "draft-p", 2, RecipeDraftOrigin.HumanEdit, "feedback"),
            "Only AI refinements carry feedback.");
        Assert.ThrowsExactly<ArgumentException>(() => new RecipeDraftProvenance(
            "lineage-a", "draft-p", 2, RecipeDraftOrigin.HumanEdit, guardRestorations: [new RecipeGuardRestoration("/p", "draft-s")]));
        Assert.ThrowsExactly<ArgumentException>(() => new RecipeDraftProvenance(
            "lineage-a", "draft-p", 2, RecipeDraftOrigin.HumanEdit, guardRestorationCount: 1));
        Assert.ThrowsExactly<ArgumentException>(() => new RecipeDraftProvenance(
            "lineage-a", "draft-p", 2, RecipeDraftOrigin.AiRefine, "ok", [new RecipeGuardRestoration("/p", "draft-s")], guardRestorationCount: 0),
            "The count can never be below the retained list.");
    }

    [TestMethod]
    public void BoundsAndIdentifiersFollowTheContractGuards()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new RecipeDraftProvenance("lineage-a", null, 0, RecipeDraftOrigin.AiDraft));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new RecipeDraftProvenance("lineage-a", null, 1, (RecipeDraftOrigin)9));
        Assert.ThrowsExactly<ArgumentException>(() => RecipeDraftProvenance.Root("Lineage-A", RecipeDraftOrigin.AiDraft));
        Assert.ThrowsExactly<ArgumentException>(() => RecipeDraftProvenance.Root("lineage a", RecipeDraftOrigin.AiDraft));
        Assert.ThrowsExactly<ArgumentException>(() => new RecipeDraftProvenance("lineage-a", "Draft/1", 2, RecipeDraftOrigin.HumanEdit));
        Assert.ThrowsExactly<ArgumentException>(() => RecipeDraftProvenance.Root("lineage-a", RecipeDraftOrigin.Preset, "Spark!"));
        Assert.ThrowsExactly<ArgumentException>(() => new RecipeGuardRestoration(new string('p', 257), "draft-s"));
        Assert.ThrowsExactly<ArgumentException>(() => new RecipeGuardRestoration("/p\n", "draft-s"));
        Assert.ThrowsExactly<ArgumentException>(() => new RecipeGuardRestoration("/p", "DRAFT"));

        var atLimit = new string('f', RecipeDraftLineageLimits.MaximumFeedbackTextUtf8Bytes);
        Assert.AreEqual(atLimit, new RecipeDraftProvenance("lineage-a", "draft-p", 2, RecipeDraftOrigin.AiRefine, atLimit).FeedbackText);
        Assert.ThrowsExactly<ArgumentException>(() => new RecipeDraftProvenance(
            "lineage-a", "draft-p", 2, RecipeDraftOrigin.AiRefine, atLimit + "f"));
        Assert.ThrowsExactly<ArgumentException>(() => new RecipeDraftProvenance(
            "lineage-a", "draft-p", 2, RecipeDraftOrigin.AiRefine, new string('é', RecipeDraftLineageLimits.MaximumFeedbackTextUtf8Bytes)),
            "The feedback bound is measured in UTF-8 bytes.");
        Assert.ThrowsExactly<ArgumentException>(() => new RecipeDraftProvenance(
            "lineage-a", "draft-p", 2, RecipeDraftOrigin.AiRefine, "a\0b"));

        var tooMany = Enumerable.Range(0, RecipeDraftLineageLimits.MaximumGuardRestorations + 1)
            .Select(index => new RecipeGuardRestoration("/p/" + index.ToString(CultureInfo.InvariantCulture), "draft-s"));
        Assert.ThrowsExactly<ArgumentException>(() => new RecipeDraftProvenance(
            "lineage-a", "draft-p", 2, RecipeDraftOrigin.AiRefine, "ok", tooMany, RecipeDraftLineageLimits.MaximumGuardRestorations + 1),
            "A persisted provenance never carries more than the bounded list; the codec fails closed instead.");
    }

    [TestMethod]
    public void ARevisionBoundsAnUnboundedRestorationListAndKeepsTheTotalCount()
    {
        var restorations = Enumerable.Range(0, 100)
            .Select(index => new RecipeGuardRestoration("/p/" + index.ToString(CultureInfo.InvariantCulture), "draft-s"))
            .ToArray();
        var revision = new RecipeDraftRevision(
            RecipeDraftTestData.Draft(RecipeDraftTestData.RealRecipeJson(1)),
            RecipeDraftOrigin.AiRefine,
            requestCount: 2,
            feedbackText: "less glow",
            guardRestorations: restorations);

        Assert.AreEqual(RecipeDraftLineageLimits.MaximumGuardRestorations, revision.GuardRestorations.Count);
        Assert.AreEqual(100, revision.GuardRestorationCount);
        Assert.AreEqual("/p/0", revision.GuardRestorations[0].ParameterPath);
        Assert.AreEqual("/p/63", revision.GuardRestorations[^1].ParameterPath, "The first entries are kept, the tail is dropped.");
        Assert.AreEqual("less glow", revision.FeedbackText);
        Assert.AreEqual(2, revision.RequestCount);
    }

    [TestMethod]
    public void ARevisionEnforcesOriginRulesBeforeItReachesTheStore()
    {
        var draft = RecipeDraftTestData.Draft(RecipeDraftTestData.RealRecipeJson(1));
        Assert.ThrowsExactly<ArgumentException>(() => new RecipeDraftRevision(draft, RecipeDraftOrigin.Preset),
            "A preset always starts a lineage.");
        Assert.ThrowsExactly<ArgumentNullException>(() => new RecipeDraftRevision(draft, RecipeDraftOrigin.AiRefine),
            "An AI refinement needs feedback.");
        Assert.ThrowsExactly<ArgumentException>(() => new RecipeDraftRevision(draft, RecipeDraftOrigin.HumanEdit, feedbackText: "x"));
        Assert.ThrowsExactly<ArgumentException>(() => new RecipeDraftRevision(
            draft, RecipeDraftOrigin.HumanEdit, guardRestorations: [new RecipeGuardRestoration("/p", "draft-s")]));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new RecipeDraftRevision(draft, RecipeDraftOrigin.HumanEdit, requestCount: 7));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new RecipeDraftRevision(draft, (RecipeDraftOrigin)5));
        Assert.ThrowsExactly<ArgumentNullException>(() => new RecipeDraftRevision(null!, RecipeDraftOrigin.HumanEdit));

        var humanEdit = new RecipeDraftRevision(draft, RecipeDraftOrigin.HumanEdit);
        Assert.AreEqual(0, humanEdit.RequestCount);
        Assert.IsNull(humanEdit.FeedbackText);
        Assert.AreEqual(0, humanEdit.GuardRestorationCount);
    }

    [TestMethod]
    public void RecordsExposeTheChainFieldsAndRefuseSelfParenting()
    {
        var aiRoot = RecipeDraftRecord.Create(RecipeDraftTestData.DraftedResult(), DateTimeOffset.UtcNow);
        Assert.AreEqual(RecipeDraftOrigin.AiDraft, aiRoot.Origin);
        Assert.IsNull(aiRoot.ParentDraftId);
        Assert.AreEqual(1, aiRoot.RevisionOrdinal);
        Assert.IsTrue(aiRoot.LineageId.StartsWith("lineage-", StringComparison.Ordinal));
        Assert.AreNotEqual(aiRoot.LineageId, RecipeDraftRecord.Create(RecipeDraftTestData.DraftedResult(), DateTimeOffset.UtcNow).LineageId);

        var failedRoot = RecipeDraftRecord.Create(RecipeDraftTestData.FailedResult(), DateTimeOffset.UtcNow);
        Assert.AreEqual(RecipeDraftOrigin.AiDraft, failedRoot.Origin);
        Assert.AreEqual(RecipeDraftStatus.Failed, failedRoot.Status);

        var legacy = new RecipeDraftRecord(
            "draft-legacy",
            RecipeDraftStatus.PendingConfirmation,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            "corr",
            RecipeDraftTestData.PromptVersion,
            RecipeDraftTestData.CatalogVersion,
            "{}",
            new string('a', 64),
            null,
            null,
            null,
            null,
            Array.Empty<RecipeValidationIssue>(),
            requestCount: 1);
        Assert.AreEqual("draft-legacy", legacy.LineageId, "The lineage-unaware constructor roots a lineage named after the draft.");
        Assert.AreEqual(RecipeDraftOrigin.AiDraft, legacy.Origin);

        var withStatus = legacy.WithStatus(RecipeDraftStatus.ConfirmedAwaitingBuild, DateTimeOffset.UtcNow);
        Assert.AreEqual(legacy.CanonicalSha256, withStatus.CanonicalSha256);
        Assert.AreEqual(legacy.Provenance, withStatus.Provenance, "A status change never moves the version in its chain.");

        Assert.ThrowsExactly<ArgumentException>(() => new RecipeDraftRecord(
            "draft-self",
            RecipeDraftStatus.PendingConfirmation,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            "corr",
            RecipeDraftTestData.PromptVersion,
            RecipeDraftTestData.CatalogVersion,
            "{}",
            new string('a', 64),
            null,
            null,
            null,
            null,
            Array.Empty<RecipeValidationIssue>(),
            1,
            new RecipeDraftProvenance("lineage-a", "draft-self", 2, RecipeDraftOrigin.HumanEdit)));
        Assert.IsTrue(Enum.IsDefined(RecipeDraftStatus.Superseded));
    }

    [TestMethod]
    public void OutcomesAndTheUnsupportedVersionRemedyStayRedacted()
    {
        var record = RecipeDraftTestData.Root(RecipeDraftOrigin.AiDraft);
        var outcome = new RecipeDraftSaveOutcome(record, ["draft-a"], ["draft-b", "draft-c"], ["lineage-x"], evictedVersionCount: 3);
        Assert.IsFalse(outcome.RetainedEverything);
        Assert.AreEqual(3, outcome.EvictedVersionCount);
        Assert.IsTrue(new RecipeDraftSaveOutcome(record, [], [], [], 0).RetainedEverything);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new RecipeDraftSaveOutcome(record, [], [], ["lineage-x"], 0),
            "An evicted lineage always removes at least one version.");
        Assert.ThrowsExactly<ArgumentException>(() => new RecipeDraftSaveOutcome(record, [" "], [], [], 0));
        Assert.AreEqual(0, new RecipeDraftTruncateOutcome(record, []).RemovedDraftIds.Count);

        var unsupported = new RecipeDraftStoreException(RecipeDraftStoreErrorCode.UnsupportedVersion);
        Assert.IsFalse(Path.IsPathRooted(RecipeDraftStoreException.UnsupportedVersionRemedyPath));
        Assert.IsTrue(unsupported.Message.Contains(RecipeDraftStoreException.UnsupportedVersionRemedyPath, StringComparison.Ordinal));
        Assert.IsTrue(unsupported.Message.Contains(".bak", StringComparison.Ordinal));
        Assert.IsFalse(unsupported.Message.Contains(":\\", StringComparison.Ordinal), "The remedy never spells an absolute path.");
        Assert.IsFalse(unsupported.Message.Contains(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), StringComparison.OrdinalIgnoreCase));
        Assert.AreEqual("RecipeDraftStoreException(UnsupportedVersion)", unsupported.ToString());
        Assert.AreNotEqual(
            new RecipeDraftStoreException(RecipeDraftStoreErrorCode.StorageFailed).Message,
            unsupported.Message,
            "Corruption and version mismatch are different remedies.");
    }
}

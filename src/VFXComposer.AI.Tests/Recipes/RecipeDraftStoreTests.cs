using Microsoft.VisualStudio.TestTools.UnitTesting;
using VFXComposer.AI.Contracts.Recipes;
using VFXComposer.AI.Providers.Recipes;

namespace VFXComposer.AI.Tests.Recipes;

[TestClass]
public sealed class RecipeDraftStoreTests
{
    private string _root = string.Empty;

    [TestInitialize]
    public void CreateRoot() =>
        _root = Directory.CreateTempSubdirectory("vfxcomposer-recipe-drafts-").FullName;

    [TestCleanup]
    public void DeleteRoot()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [TestMethod]
    public void SaveThenConfirmFlipsTheStatusAndSurvivesAStoreRestart()
    {
        var path = StorePath();
        var record = RecipeDraftRecord.Create(DraftedResult(), DateTimeOffset.UtcNow);
        new RecipeDraftStore(path).Save(record);

        var confirmed = new RecipeDraftStore(path).Confirm(record.DraftId, record.CanonicalSha256!);

        Assert.AreEqual(RecipeDraftStatus.ConfirmedAwaitingBuild, confirmed.Status);
        Assert.AreEqual(record.CanonicalSha256, confirmed.CanonicalSha256);
        Assert.IsTrue(confirmed.UpdatedUtc >= record.UpdatedUtc);

        var reloaded = new RecipeDraftStore(path).TryGet(record.DraftId);
        Assert.IsNotNull(reloaded);
        Assert.AreEqual(RecipeDraftStatus.ConfirmedAwaitingBuild, reloaded.Status);
        Assert.AreEqual(record.RecipeJson, reloaded.RecipeJson);
    }

    [TestMethod]
    public void ConfirmationFailsClosedForUnknownStaleOrAlreadyDecidedDrafts()
    {
        var store = new RecipeDraftStore(StorePath());
        var record = store.Save(RecipeDraftRecord.Create(DraftedResult(), DateTimeOffset.UtcNow));

        var notFound = Throws(() => store.Confirm("draft-missing", record.CanonicalSha256!));
        Assert.AreEqual(RecipeDraftStoreErrorCode.NotFound, notFound.Code);

        var mismatch = Throws(() => store.Confirm(record.DraftId, new string('0', 64)));
        Assert.AreEqual(RecipeDraftStoreErrorCode.HashMismatch, mismatch.Code);

        store.Confirm(record.DraftId, record.CanonicalSha256!);
        var decided = Throws(() => store.Confirm(record.DraftId, record.CanonicalSha256!));
        Assert.AreEqual(RecipeDraftStoreErrorCode.InvalidStatus, decided.Code);
    }

    [TestMethod]
    public void AFailedFinalStateIsRetainedWithItsReportAndCanNeverBeConfirmed()
    {
        var store = new RecipeDraftStore(StorePath());
        var failed = RecipeDraftRecord.Create(FailedResult(), DateTimeOffset.UtcNow);
        store.Save(failed);

        var reloaded = store.TryGet(failed.DraftId);
        Assert.IsNotNull(reloaded);
        Assert.AreEqual(RecipeDraftStatus.Failed, reloaded.Status);
        Assert.IsNull(reloaded.CanonicalSha256);
        Assert.AreEqual(1, reloaded.Issues.Count);
        Assert.AreEqual("E101", reloaded.Issues[0].Code);

        var refused = Throws(() => store.Confirm(failed.DraftId, new string('a', 64)));
        Assert.AreEqual(RecipeDraftStoreErrorCode.InvalidStatus, refused.Code);
    }

    [TestMethod]
    public void ACorruptPrimaryIsRecoveredFromTheBackupCopy()
    {
        var path = StorePath();
        var store = new RecipeDraftStore(path);
        var first = store.Save(RecipeDraftRecord.Create(DraftedResult(), DateTimeOffset.UtcNow));
        store.Save(RecipeDraftRecord.Create(DraftedResult(), DateTimeOffset.UtcNow));
        Assert.IsTrue(File.Exists(path + ".bak"));

        File.WriteAllText(path, "{ this is no longer json");

        var recovered = new RecipeDraftStore(path).TryGet(first.DraftId);
        Assert.IsNotNull(recovered);
        Assert.AreEqual(RecipeDraftStatus.PendingConfirmation, recovered.Status);
    }

    [TestMethod]
    public void AMissingStoreReadsAsEmptyWithoutCreatingFiles()
    {
        var path = StorePath();
        Assert.IsNull(new RecipeDraftStore(path).TryGet("draft-anything"));
        Assert.IsFalse(File.Exists(path));
        Assert.IsFalse(File.Exists(path + ".bak"));
    }

    private string StorePath() => Path.Combine(_root, "recipe-drafts.json");

    private static RecipeDraftStoreException Throws(Action action)
    {
        try
        {
            action();
        }
        catch (RecipeDraftStoreException exception)
        {
            return exception;
        }

        Assert.Fail("Expected a RecipeDraftStoreException.");
        throw new InvalidOperationException("Unreachable.");
    }

    private static RecipeGenerationResult DraftedResult()
    {
        var recipeJson = RecipeTemplateCatalogSnapshot.Default.CanonicalExampleJson;
        var draft = new RecipeDraft(
            Guid.NewGuid().ToString("N"),
            recipeJson,
            RecipeCanonicalJson.ComputeSha256(recipeJson),
            "fireball_2d",
            "projectile",
            "2d",
            "mobile_medium",
            RecipePromptTemplate.Version,
            RecipeTemplateCatalogSnapshot.Default.TemplateCatalogVersion);
        return RecipeGenerationResult.Drafted(draft, [new RecipeGenerationAttempt(1, [])]);
    }

    private static RecipeGenerationResult FailedResult() =>
        RecipeGenerationResult.ValidationFailed(
            Guid.NewGuid().ToString("N"),
            "{}",
            [
                new RecipeValidationIssue(
                    "E101",
                    RecipeValidationSeverity.Error,
                    "/stages",
                    "Missing required field: stages"),
            ],
            [new RecipeGenerationAttempt(1, ["E101"]), new RecipeGenerationAttempt(2, ["E101"])],
            RecipePromptTemplate.Version,
            RecipeTemplateCatalogSnapshot.Default.TemplateCatalogVersion);
}

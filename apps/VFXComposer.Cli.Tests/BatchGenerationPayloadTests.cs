using Microsoft.VisualStudio.TestTools.UnitTesting;
using VFXComposer.Batch.Core;
using VFXComposer.Jobs;

namespace VFXComposer.Cli.Tests;

[TestClass]
public sealed class BatchGenerationPayloadTests
{
    [TestMethod]
    public void PayloadIsCanonicalAndRoundTrips()
    {
        var item = PromptItem("alpha", "a calm blue spark", new BatchConstraints("projectile", "2d", "water", null, "mobile_medium", 42));

        var payload = BatchGenerationPayload.Create(item);

        Assert.AreEqual(
            "{\"constraints\":{\"archetype\":\"projectile\",\"dimension\":\"2d\",\"element\":\"water\"," +
            "\"randomSeed\":42,\"targetProfile\":\"mobile_medium\"}," +
            "\"prompt\":\"a calm blue spark\",\"schemaVersion\":\"vfxcomposer.generate-payload/1\"}",
            payload,
            "The canonical payload must be byte-stable.");
        var parsed = BatchGenerationPayload.Parse(payload);
        Assert.AreEqual("a calm blue spark", parsed.Prompt);
        Assert.AreEqual("projectile", parsed.Constraints.Archetype);
        Assert.AreEqual(42, parsed.Constraints.RandomSeed);
    }

    [TestMethod]
    public void PayloadDoesNotDependOnWhereConstraintsCameFrom()
    {
        var fromItem = PromptItem("alpha", "same text", new BatchConstraints(null, "2d", null, null, "mobile_medium", null));
        var fromDefaults = PromptItem(
            "alpha",
            "same text",
            BatchConstraints.Empty.InheritFrom(new BatchConstraints(null, "2d", null, null, "mobile_medium", null)));

        Assert.AreEqual(BatchGenerationPayload.Create(fromItem), BatchGenerationPayload.Create(fromDefaults));
    }

    [TestMethod]
    public void EntryKeyIsStableForContentAndChangesWithIt()
    {
        var manifest = Manifest("batch-a", PromptItem("alpha", "text", BatchConstraints.Empty));
        var baseline = BatchSubmissionService.CreateRequest(manifest, JobBatchPolicies.Continue, manifest.Items[0]);

        var repeat = BatchSubmissionService.CreateRequest(manifest, JobBatchPolicies.Continue, manifest.Items[0]);
        var otherPolicy = BatchSubmissionService.CreateRequest(manifest, JobBatchPolicies.Abort, manifest.Items[0]);
        var otherPrompt = BatchSubmissionService.CreateRequest(
            manifest,
            JobBatchPolicies.Continue,
            PromptItem("alpha", "different text", BatchConstraints.Empty));
        var otherItemId = BatchSubmissionService.CreateRequest(
            manifest,
            JobBatchPolicies.Continue,
            PromptItem("beta", "text", BatchConstraints.Empty));
        var otherBatch = BatchSubmissionService.CreateRequest(
            Manifest("batch-b", manifest.Items[0]),
            JobBatchPolicies.Continue,
            manifest.Items[0]);

        Assert.AreEqual(baseline.EntryIdempotencyKey, repeat.EntryIdempotencyKey);
        Assert.AreEqual(
            baseline.EntryIdempotencyKey,
            otherPolicy.EntryIdempotencyKey,
            "The failure policy is not part of the entry content.");
        Assert.AreNotEqual(baseline.EntryIdempotencyKey, otherPrompt.EntryIdempotencyKey);
        Assert.AreNotEqual(baseline.EntryIdempotencyKey, otherItemId.EntryIdempotencyKey);
        Assert.AreNotEqual(baseline.EntryIdempotencyKey, otherBatch.EntryIdempotencyKey);
    }

    [TestMethod]
    public void ComposedDescriptionRendersConstraintsInAFixedOrder()
    {
        var description = BatchGenerationPayload.ComposeDescription(
            "a bigger fireball",
            new BatchConstraints("projectile", "2d", "fire", "stylised", "mobile_medium", 7));

        Assert.AreEqual(
            "a bigger fireball\n\nConstraints:\n- archetype: projectile\n- dimension: 2d\n- element: fire\n" +
            "- style: stylised\n- targetProfile: mobile_medium\n- randomSeed: 7",
            description);
    }

    [TestMethod]
    public void ComposedDescriptionIsThePromptWhenNoConstraintIsSet()
    {
        Assert.AreEqual(
            "a bigger fireball",
            BatchGenerationPayload.ComposeDescription("a bigger fireball", BatchConstraints.Empty));
    }

    [TestMethod]
    public void RecipeEntriesHaveNoGenerationPayload()
    {
        var recipeItem = new BatchManifestItem("alpha", BatchItemKinds.Recipe, null, "r.json", BatchConstraints.Empty);

        Assert.ThrowsExactly<ArgumentException>(() => BatchGenerationPayload.Create(recipeItem));
        Assert.ThrowsExactly<ArgumentException>(() =>
            BatchSubmissionService.CreateRequest(Manifest("b", recipeItem), JobBatchPolicies.Continue, recipeItem));
    }

    [TestMethod]
    public void UnknownPayloadSchemaFailsClosed()
    {
        Assert.ThrowsExactly<InvalidDataException>(() =>
            BatchGenerationPayload.Parse("{\"schemaVersion\":\"vfxcomposer.generate-payload/2\",\"prompt\":\"x\"}"));
        Assert.ThrowsExactly<InvalidDataException>(() =>
            BatchGenerationPayload.Parse("{\"schemaVersion\":\"vfxcomposer.generate-payload/1\"}"));
    }

    private static BatchManifestItem PromptItem(string itemId, string prompt, BatchConstraints constraints) =>
        new(itemId, BatchItemKinds.Prompt, prompt, null, constraints);

    private static BatchManifest Manifest(string batchId, BatchManifestItem item) =>
        new(BatchManifestLimits.SchemaVersion, batchId, BatchFailurePolicies.Continue, [item]);
}

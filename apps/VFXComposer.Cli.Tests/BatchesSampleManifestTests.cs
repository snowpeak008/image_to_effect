using Microsoft.VisualStudio.TestTools.UnitTesting;
using VFXComposer.Batch.Core;

namespace VFXComposer.Cli.Tests;

/// <summary>
/// Guards the checked-in <c>batches/</c> fixture: the sample manifest the F6 end-to-end batch flow
/// consumes is parsed through the authoritative <see cref="BatchManifestParser"/> on every build, so
/// it cannot silently drift out of validity. The JSON Schema beside it is a tooling aid; this test,
/// not the schema, is what keeps the sample honest.
/// </summary>
[TestClass]
public sealed class BatchesSampleManifestTests
{
    [TestMethod]
    public void TheCheckedInSampleManifestParsesAsAValidThreeItemBatch()
    {
        var json = File.ReadAllText(TestRepository.SampleManifestPath());

        var result = BatchManifestParser.Parse(
            json,
            new StubRecipeProbe(BatchRecipeProbeResult.JsonObject),
            BatchCapabilityProfile.GenerationOnly);

        Assert.IsTrue(
            result.IsValid,
            string.Join("; ", result.Issues.Select(static issue => issue.Code + " " + issue.Path)));

        var manifest = result.Manifest!;
        Assert.AreEqual("sample-fire-pack", manifest.BatchId);
        Assert.AreEqual(BatchFailurePolicies.Continue, manifest.FailurePolicy);
        CollectionAssert.AreEqual(
            new[] { "fireball-big-slow", "frost-nova-burst", "spark-hit-3d" },
            manifest.Items.Select(static item => item.ItemId).ToArray());

        // The first two items inherit the manifest's 2d default; the third overrides it with 3d.
        Assert.AreEqual("2d", manifest.Items[0].Constraints.Dimension, "The item inherits the manifest default.");
        Assert.AreEqual("2d", manifest.Items[1].Constraints.Dimension, "The item inherits the manifest default.");
        Assert.AreEqual("3d", manifest.Items[2].Constraints.Dimension, "The item overrides the manifest default.");
        Assert.AreEqual("mobile_medium", manifest.Items[0].Constraints.TargetProfile, "The item inherits the manifest default.");
    }

    [TestMethod]
    public void TheCheckedInRecipeKindSampleResolvesItsBundledStrictRecipe()
    {
        // The recipe this references is validated for real by a Unity batchmode build (F6 flow one,
        // evidence in docs/stage-notes/F6_E2E_EVIDENCE.md): it satisfies the F2 strict budget
        // (one render-module stage, no attachTo chain, all three stage roots). Here we prove the
        // manifest resolves it through the real filesystem probe under a build-capable profile.
        var batchesDirectory = Path.GetDirectoryName(TestRepository.SampleManifestPath())!;
        var json = File.ReadAllText(Path.Combine(batchesDirectory, "sample-batch-recipe.manifest.json"));

        var result = BatchManifestParser.Parse(
            json,
            new FileSystemBatchRecipeProbe(batchesDirectory),
            BatchCapabilityProfile.GenerationAndRecipeBuild);

        Assert.IsTrue(
            result.IsValid,
            string.Join("; ", result.Issues.Select(static issue => issue.Code + " " + issue.Path)));

        var item = result.Manifest!.Items.Single();
        Assert.AreEqual("sample-recipe-pack", result.Manifest.BatchId);
        Assert.AreEqual(BatchFailurePolicies.Abort, result.Manifest.FailurePolicy);
        Assert.AreEqual(BatchItemKinds.Recipe, item.Kind);
        Assert.AreEqual("recipes/spark_projectile_2d.json", item.RecipePath);
    }
}

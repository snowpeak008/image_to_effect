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
}

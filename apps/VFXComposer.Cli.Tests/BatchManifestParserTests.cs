using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VFXComposer.AI.Contracts.Recipes;
using VFXComposer.Batch.Core;

namespace VFXComposer.Cli.Tests;

[TestClass]
public sealed class BatchManifestParserTests
{
    private const string ValidManifest =
        """
        {
          "schemaVersion": "vfxcomposer.batch-manifest/1",
          "batchId": "fire-pack-w35",
          "onFailure": "abort",
          "defaults": { "dimension": "2d", "targetProfile": "mobile_medium" },
          "items": [
            {
              "itemId": "fireball-big-slow",
              "kind": "prompt",
              "prompt": "a bigger slower fireball",
              "constraints": { "archetype": "projectile", "element": "fire", "randomSeed": 42 }
            },
            { "itemId": "second-entry", "kind": "prompt", "prompt": "a small spark" }
          ]
        }
        """;

    [TestMethod]
    public void ValidManifestParsesWithDefaultsInherited()
    {
        var result = Parse(ValidManifest);

        Assert.IsTrue(result.IsValid, Describe(result));
        var manifest = result.Manifest!;
        Assert.AreEqual("fire-pack-w35", manifest.BatchId);
        Assert.AreEqual(BatchFailurePolicies.Abort, manifest.FailurePolicy);
        CollectionAssert.AreEqual(
            new[] { "fireball-big-slow", "second-entry" },
            manifest.Items.Select(static item => item.ItemId).ToArray());
        Assert.AreEqual("projectile", manifest.Items[0].Constraints.Archetype);
        Assert.AreEqual(42, manifest.Items[0].Constraints.RandomSeed);
        Assert.AreEqual("2d", manifest.Items[0].Constraints.Dimension, "The item inherits the manifest default.");
        Assert.AreEqual("mobile_medium", manifest.Items[1].Constraints.TargetProfile);
    }

    [TestMethod]
    public void MissingOnFailureDefaultsToContinue()
    {
        var result = Parse(ValidManifest.Replace("\"onFailure\": \"abort\",", string.Empty, StringComparison.Ordinal));

        Assert.IsTrue(result.IsValid, Describe(result));
        Assert.AreEqual(BatchFailurePolicies.Continue, result.Manifest!.FailurePolicy);
    }

    [TestMethod]
    public void MalformedDocumentIsRejected()
    {
        var result = Parse("{ not json");

        AssertSingleError(result, BatchDiagnosticCodes.MalformedJson, "$");
    }

    [TestMethod]
    public void OversizedManifestIsRejectedBeforeParsing()
    {
        var result = Parse("\"" + new string('x', BatchManifestLimits.MaximumManifestBytes) + "\"");

        AssertSingleError(result, BatchDiagnosticCodes.ManifestTooLarge, "$");
    }

    [TestMethod]
    public void UnknownSchemaVersionIsRejected()
    {
        var result = Parse(ValidManifest.Replace(
            "vfxcomposer.batch-manifest/1",
            "vfxcomposer.batch-manifest/2",
            StringComparison.Ordinal));

        AssertSingleError(result, BatchDiagnosticCodes.UnsupportedSchemaVersion, "$.schemaVersion");
    }

    [TestMethod]
    public void UnknownRootFieldIsRejectedWithItsPath()
    {
        var result = Parse(ValidManifest.Replace(
            "\"batchId\":",
            "\"authority\": \"admin\", \"batchId\":",
            StringComparison.Ordinal));

        Assert.IsFalse(result.IsValid);
        var issue = result.Issues.Single(i => i.Code == BatchDiagnosticCodes.UnknownField);
        Assert.AreEqual("$.authority", issue.Path);
    }

    [TestMethod]
    public void UnknownItemFieldAndUnknownConstraintKeyAreRejected()
    {
        var result = Parse(ValidManifest
            .Replace("\"itemId\": \"second-entry\"", "\"approvalToken\": \"x\", \"itemId\": \"second-entry\"", StringComparison.Ordinal)
            .Replace("\"randomSeed\": 42", "\"randomSeed\": 42, \"skipValidation\": true", StringComparison.Ordinal));

        var paths = result.Issues
            .Where(i => i.Code == BatchDiagnosticCodes.UnknownField)
            .Select(i => i.Path)
            .ToArray();
        CollectionAssert.AreEquivalent(
            new[] { "$.items[1].approvalToken", "$.items[0].constraints.skipValidation" },
            paths);
    }

    [TestMethod]
    public void MissingRequiredFieldsAreReported()
    {
        var result = Parse(
            """
            { "schemaVersion": "vfxcomposer.batch-manifest/1" }
            """);

        var codes = result.Issues.Select(i => i.Code + " " + i.Path).ToArray();
        CollectionAssert.Contains(codes, BatchDiagnosticCodes.MissingRequiredField + " $.batchId");
        CollectionAssert.Contains(codes, BatchDiagnosticCodes.MissingRequiredField + " $.items");
    }

    [TestMethod]
    public void EmptyItemArrayIsOutOfRange()
    {
        var result = Parse(
            """
            { "schemaVersion": "vfxcomposer.batch-manifest/1", "batchId": "b", "items": [] }
            """);

        AssertSingleError(result, BatchDiagnosticCodes.ValueOutOfRange, "$.items");
    }

    [TestMethod]
    public void ItemCountAboveTheBoundIsRejected()
    {
        var items = string.Join(',', Enumerable.Range(0, BatchManifestLimits.MaximumItemCount + 1).Select(index =>
            "{\"itemId\":\"item-" + index + "\",\"kind\":\"prompt\",\"prompt\":\"p\"}"));
        var result = Parse(
            "{\"schemaVersion\":\"vfxcomposer.batch-manifest/1\",\"batchId\":\"b\",\"items\":[" + items + "]}");

        Assert.IsFalse(result.IsValid);
        var issue = result.Issues.Single(i => i.Code == BatchDiagnosticCodes.ValueOutOfRange && i.Path == "$.items");
        Assert.AreEqual("65", issue.ActualValue);
        Assert.AreEqual("1..64", issue.AllowedRange);
    }

    [TestMethod]
    public void DuplicateItemIdIsRejected()
    {
        var result = Parse(ValidManifest.Replace("second-entry", "fireball-big-slow", StringComparison.Ordinal));

        Assert.IsFalse(result.IsValid);
        Assert.AreEqual(
            "$.items[1].itemId",
            result.Issues.Single(i => i.Code == BatchDiagnosticCodes.DuplicateItemId).Path);
    }

    [TestMethod]
    public void UnknownKindAndUnknownDimensionAreRejected()
    {
        var kindResult = Parse(
            """
            {
              "schemaVersion": "vfxcomposer.batch-manifest/1",
              "batchId": "b",
              "items": [ { "itemId": "a", "kind": "build", "prompt": "x" } ]
            }
            """);
        var dimensionResult = Parse(ValidManifest.Replace("\"dimension\": \"2d\"", "\"dimension\": \"4d\"", StringComparison.Ordinal));

        Assert.IsTrue(kindResult.Issues.Any(i =>
            i.Code == BatchDiagnosticCodes.UnknownEnumValue && i.Path == "$.items[0].kind"));
        Assert.IsTrue(dimensionResult.Issues.Any(i =>
            i.Code == BatchDiagnosticCodes.UnknownEnumValue && i.Path == "$.defaults.dimension"));
    }

    [TestMethod]
    public void UnknownFailurePolicyIsRejected()
    {
        var result = Parse(ValidManifest.Replace("\"onFailure\": \"abort\"", "\"onFailure\": \"retry\"", StringComparison.Ordinal));

        Assert.IsTrue(result.Issues.Any(i =>
            i.Code == BatchDiagnosticCodes.UnknownEnumValue && i.Path == "$.onFailure"));
    }

    [TestMethod]
    public void FieldsThatDoNotBelongToTheItemKindAreRejected()
    {
        var promptOnRecipe = Parse(
            """
            {
              "schemaVersion": "vfxcomposer.batch-manifest/1",
              "batchId": "b",
              "items": [ { "itemId": "a", "kind": "recipe", "recipePath": "r.json", "prompt": "x" } ]
            }
            """);
        var pathOnPrompt = Parse(
            """
            {
              "schemaVersion": "vfxcomposer.batch-manifest/1",
              "batchId": "b",
              "items": [ { "itemId": "a", "kind": "prompt", "prompt": "x", "recipePath": "r.json" } ]
            }
            """);

        Assert.IsTrue(promptOnRecipe.Issues.Any(i =>
            i.Code == BatchDiagnosticCodes.FieldNotAllowedForKind && i.Path == "$.items[0].prompt"));
        Assert.IsTrue(pathOnPrompt.Issues.Any(i =>
            i.Code == BatchDiagnosticCodes.FieldNotAllowedForKind && i.Path == "$.items[0].recipePath"));
    }

    [TestMethod]
    [DataRow("..\\..\\ProjectSettings\\x.json")]
    [DataRow("../../ProjectSettings/x.json")]
    [DataRow("/etc/x.json")]
    [DataRow("C:/temp/x.json")]
    [DataRow("//server/share/x.json")]
    [DataRow("recipes/x.json:stream")]
    [DataRow("recipes/con.json")]
    [DataRow("recipes/x.txt")]
    [DataRow("recipes//x.json")]
    [DataRow("~/x.json")]
    public void EscapingRecipePathsAreRejected(string recipePath)
    {
        var result = Parse(RecipeManifest(recipePath));

        Assert.IsTrue(
            result.Issues.Any(i =>
                i.Code == BatchDiagnosticCodes.UnsafeRecipePath && i.Path == "$.items[0].recipePath"),
            Describe(result));
    }

    [TestMethod]
    public void ContainedRecipePathPassesStructuralValidation()
    {
        var result = Parse(RecipeManifest("recipes/frost_impact_2d.default.json"));

        Assert.IsFalse(
            result.Issues.Any(i => i.Code == BatchDiagnosticCodes.UnsafeRecipePath),
            Describe(result));
    }

    [TestMethod]
    public void MissingOrNonObjectRecipeFileIsASemanticError()
    {
        var missing = Parse(RecipeManifest("recipes/a.json"), new StubRecipeProbe(BatchRecipeProbeResult.Missing));
        var notObject = Parse(RecipeManifest("recipes/a.json"), new StubRecipeProbe(BatchRecipeProbeResult.NotJsonObject));

        Assert.IsTrue(missing.Issues.Any(i =>
            i.Code == BatchDiagnosticCodes.RecipeFileMissing && i.Path == "$.items[0].recipePath"));
        Assert.IsTrue(notObject.Issues.Any(i =>
            i.Code == BatchDiagnosticCodes.RecipeFileNotJsonObject && i.Path == "$.items[0].recipePath"));
    }

    [TestMethod]
    public void RecipeEntriesAreRefusedByTheCapabilityGate()
    {
        var result = Parse(RecipeManifest("recipes/a.json"));

        Assert.IsFalse(result.IsValid);
        var issue = result.Issues.Single(i => i.Code == BatchDiagnosticCodes.RecipeBuildNotSupported);
        Assert.AreEqual("$.items[0].kind", issue.Path);
        Assert.AreEqual(BatchItemKinds.Prompt, issue.AllowedRange);
    }

    [TestMethod]
    public void PromptEntriesAreRefusedWholesaleWhenTheChannelIsUnbound()
    {
        var result = BatchManifestParser.Parse(
            ValidManifest,
            new StubRecipeProbe(BatchRecipeProbeResult.JsonObject),
            BatchCapabilityProfile.GenerationUnavailable);

        Assert.IsFalse(result.IsValid);
        Assert.IsNull(result.Manifest, "A fail-closed manifest must not be usable for submission.");
        Assert.AreEqual(
            "$.items",
            result.Issues.Single(i => i.Code == BatchDiagnosticCodes.PromptGenerationUnavailable).Path);
    }

    [TestMethod]
    public void OversizedPromptIsRejectedWithoutEchoingIt()
    {
        var secret = new string('S', BatchManifestLimits.MaximumPromptUtf8Bytes + 10);
        var result = Parse(
            "{\"schemaVersion\":\"vfxcomposer.batch-manifest/1\",\"batchId\":\"b\"," +
            "\"items\":[{\"itemId\":\"a\",\"kind\":\"prompt\",\"prompt\":\"" + secret + "\"}]}");

        var issue = result.Issues.Single(i =>
            i.Code == BatchDiagnosticCodes.ValueOutOfRange && i.Path == "$.items[0].prompt");
        Assert.AreEqual("8202 bytes", issue.ActualValue);
        foreach (var candidate in result.Issues)
        {
            Assert.IsFalse(
                (candidate.ActualValue ?? string.Empty).Contains("SSSS", StringComparison.Ordinal),
                "A finding must never echo prompt content.");
        }
    }

    [TestMethod]
    public void BadTokenShapesAreRejected()
    {
        var result = Parse(ValidManifest.Replace("fire-pack-w35", "Fire Pack", StringComparison.Ordinal));

        Assert.IsTrue(result.Issues.Any(i =>
            i.Code == BatchDiagnosticCodes.ValueOutOfRange && i.Path == "$.batchId"));
    }

    [TestMethod]
    public void WrongJsonTypesAreReportedWithTheExpectedType()
    {
        var result = Parse(
            """
            { "schemaVersion": "vfxcomposer.batch-manifest/1", "batchId": "b", "items": { "a": 1 } }
            """);

        var issue = result.Issues.Single(i => i.Code == BatchDiagnosticCodes.UnexpectedFieldType);
        Assert.AreEqual("$.items", issue.Path);
        Assert.AreEqual("array", issue.AllowedRange);
    }

    [TestMethod]
    public void NonIntegerRandomSeedIsRejected()
    {
        var result = Parse(ValidManifest.Replace("\"randomSeed\": 42", "\"randomSeed\": 1.5", StringComparison.Ordinal));

        Assert.IsTrue(result.Issues.Any(i =>
            i.Code == BatchDiagnosticCodes.ValueOutOfRange && i.Path == "$.items[0].constraints.randomSeed"));
    }

    [TestMethod]
    public void ManifestPromptBoundStaysBelowTheGenerationChannelBound()
    {
        var prompt = new string('x', BatchManifestLimits.MaximumPromptUtf8Bytes);
        var constraints = new BatchConstraints(
            new string('a', BatchManifestLimits.MaximumConstraintValueLength),
            "2d",
            new string('e', BatchManifestLimits.MaximumConstraintValueLength),
            new string('s', BatchManifestLimits.MaximumConstraintValueLength),
            new string('t', BatchManifestLimits.MaximumConstraintValueLength),
            int.MinValue);

        var composed = Encoding.UTF8.GetByteCount(BatchGenerationPayload.ComposeDescription(prompt, constraints));

        Assert.IsTrue(
            composed <= BatchGenerationPayload.MaximumComposedDescriptionBytes,
            "The worst-case composed description must stay inside the channel bound.");
        Assert.AreEqual(
            RecipeChannelLimits.MaximumDescriptionUtf8Bytes,
            BatchGenerationPayload.MaximumComposedDescriptionBytes);
    }

    private static string RecipeManifest(string recipePath) =>
        "{\"schemaVersion\":\"vfxcomposer.batch-manifest/1\",\"batchId\":\"b\"," +
        "\"items\":[{\"itemId\":\"a\",\"kind\":\"recipe\",\"recipePath\":" +
        System.Text.Json.JsonSerializer.Serialize(recipePath) + "}]}";

    private static BatchManifestParseResult Parse(string json, IBatchRecipeProbe? probe = null) =>
        BatchManifestParser.Parse(
            json,
            probe ?? new StubRecipeProbe(BatchRecipeProbeResult.JsonObject),
            BatchCapabilityProfile.GenerationOnly);

    private static void AssertSingleError(BatchManifestParseResult result, string code, string path)
    {
        Assert.IsFalse(result.IsValid, Describe(result));
        Assert.AreEqual(1, result.Issues.Count, Describe(result));
        Assert.AreEqual(code, result.Issues[0].Code);
        Assert.AreEqual(path, result.Issues[0].Path);
    }

    private static string Describe(BatchManifestParseResult result) =>
        string.Join("; ", result.Issues.Select(static issue => issue.Code + " " + issue.Path));
}

/// <summary>Recipe probe with a fixed verdict; manifest validation stays a pure function.</summary>
internal sealed class StubRecipeProbe : IBatchRecipeProbe
{
    private readonly BatchRecipeProbeResult _result;

    public StubRecipeProbe(BatchRecipeProbeResult result)
    {
        _result = result;
    }

    public BatchRecipeProbeResult Probe(string relativePath) => _result;
}

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VFXComposer.AI.Contracts.Recipes;
using VFXComposer.AI.Providers.Recipes;

namespace VFXComposer.AI.Tests.Recipes;

/// <summary>
/// The hand-edit editor (F8b3): Describe renders exactly the snapshot declarations for every committed skeleton,
/// Apply enforces type discipline and inclusive bounds without ever correcting a value, structure is immutable by
/// construction (an accepted document differs from its input only at the edited values), L1 failures reject,
/// L1.5 findings ride along as warnings, and the whole pipeline is deterministic and canonical.
/// </summary>
[TestClass]
public sealed class RecipeParameterEditorTests
{
    private static RecipePresetSkeleton FireBolt => RecipePresetSkeletons.All.Single(static skeleton => skeleton.PresetId == "fire-bolt");

    private static RecipePresetSkeleton BurstingFireball =>
        RecipePresetSkeletons.All.Single(static skeleton => skeleton.PresetId == "bursting-fireball");

    private const string ScalePath = "stages[travel].modules[core].parameters.scale";

    [TestMethod]
    public void EveryCommittedSkeletonDescribesExactlyTheSnapshotDeclarations()
    {
        var snapshot = RecipeTemplateCatalogSnapshot.Default;
        foreach (var skeleton in RecipePresetSkeletons.All)
        {
            var panel = RecipeParameterEditor.Describe(skeleton.RecipeJson);
            Assert.AreEqual(0, panel.Warnings.Count, skeleton.PresetId + " must describe without warnings.");

            using var document = JsonDocument.Parse(skeleton.RecipeJson);
            var expected = new List<(string StageId, string ModuleId, string TemplateId)>();
            foreach (var stage in document.RootElement.GetProperty("stages").EnumerateArray())
            {
                foreach (var module in stage.GetProperty("modules").EnumerateArray())
                {
                    expected.Add((
                        stage.GetProperty("id").GetString()!,
                        module.GetProperty("id").GetString()!,
                        module.GetProperty("templateId").GetString()!));
                }
            }

            CollectionAssert.AreEqual(
                expected,
                panel.Modules.Select(static module => (module.StageId, module.ModuleId, module.TemplateId)).ToList(),
                skeleton.PresetId + " must list every module in stage → module order.");

            foreach (var module in panel.Modules)
            {
                Assert.IsTrue(snapshot.TryGetTemplate(module.TemplateId, out var template));
                Assert.AreEqual(template.Kind, module.Kind);
                CollectionAssert.AreEqual(
                    template.Parameters.Select(static parameter => parameter.Name).ToArray(),
                    module.Parameters.Select(static parameter => parameter.Name).ToArray(),
                    skeleton.PresetId + "/" + module.ModuleId + " must carry exactly the declared parameter set.");
                foreach (var row in module.Parameters)
                {
                    Assert.IsTrue(template.TryGetParameter(row.Name, out var declaration));
                    Assert.AreEqual(declaration.Type, row.Type);
                    Assert.AreEqual(declaration.IsInteger, row.IsInteger);
                    Assert.AreEqual(declaration.RangeLiteral, row.RangeLiteral);
                    Assert.AreEqual(declaration.MinLiteral, row.MinLiteral);
                    Assert.AreEqual(declaration.MaxLiteral, row.MaxLiteral);
                    Assert.AreEqual(declaration.DefaultLiteral, row.DefaultLiteral);
                    Assert.AreEqual(declaration.Default, row.Default);
                    Assert.IsFalse(row.IsMissing, row.Path + " is present in every skeleton.");
                    Assert.AreEqual(
                        RecipeParameterEditor.ParameterPath(module.StageId, module.ModuleId, row.Name),
                        row.Path);
                    Assert.AreEqual(
                        double.Parse(row.CurrentValueLiteral!, CultureInfo.InvariantCulture),
                        double.Parse(declaration.DefaultLiteral, CultureInfo.InvariantCulture),
                        "Skeleton values are the catalog defaults.");
                }
            }
        }
    }

    [TestMethod]
    public void DescribeReportsTheCurrentValueLiteralAndTheTotalRowCount()
    {
        var panel = RecipeParameterEditor.Describe(BurstingFireball.RecipeJson);

        Assert.AreEqual(3, panel.ParameterCount);
        var count = panel.Modules.Single(static module => module.ModuleId == "burst").Parameters.Single(static row => row.Name == "count");
        Assert.AreEqual("24", count.CurrentValueLiteral);
        Assert.IsTrue(count.IsInteger);
        Assert.AreEqual("[8, 40]", count.RangeLiteral);
    }

    [TestMethod]
    public void AnUndeclaredKeyBecomesAWarningRowAndIsNotEditable()
    {
        var recipe = Mutate(FireBolt.RecipeJson, root => Parameters(root)["turbulence"] = 1.0);

        var panel = RecipeParameterEditor.Describe(recipe);

        Assert.AreEqual(1, panel.Modules.Count, "The module itself stays editable.");
        CollectionAssert.AreEqual(new[] { "scale" }, panel.Modules[0].Parameters.Select(static row => row.Name).ToArray());
        var warning = panel.Warnings.Single();
        Assert.AreEqual(RecipeParameterPanelWarningKind.ParameterUndeclared, warning.Kind);
        Assert.AreEqual("stages[travel].modules[core].parameters.turbulence", warning.Path);
        Assert.AreEqual("turbulence", warning.Subject);
        Assert.AreEqual("1", warning.ValueLiteral);

        var result = RecipeParameterEditor.Apply(recipe, [new RecipeParameterEdit("travel", "core", "turbulence", "2")]);
        AssertRejected(result, RecipeParameterEditCodes.TargetNotFound, "stages[travel].modules[core].parameters.turbulence");
    }

    [TestMethod]
    public void AnUnknownTemplateListsTheWholeModuleAsAWarning()
    {
        var recipe = Mutate(FireBolt.RecipeJson, root => Module(root)["templateId"] = "PFT_2D_Nonexistent");

        var panel = RecipeParameterEditor.Describe(recipe);

        Assert.AreEqual(0, panel.Modules.Count);
        var warning = panel.Warnings.Single();
        Assert.AreEqual(RecipeParameterPanelWarningKind.TemplateUnknown, warning.Kind);
        Assert.AreEqual("stages[travel].modules[core].templateId", warning.Path);
        Assert.AreEqual("PFT_2D_Nonexistent", warning.Subject);
        AssertRejected(
            RecipeParameterEditor.Apply(recipe, [new RecipeParameterEdit("travel", "core", "scale", "1.5")]),
            RecipeParameterEditCodes.TargetNotFound,
            ScalePath);
    }

    [TestMethod]
    public void AMissingDeclaredParameterDescribesAsMissingAndCanBeSupplied()
    {
        var recipe = Mutate(FireBolt.RecipeJson, root => Module(root)["parameters"] = new JsonObject());
        var row = RecipeParameterEditor.Describe(recipe).Modules.Single().Parameters.Single();
        Assert.IsTrue(row.IsMissing);
        Assert.IsNull(row.CurrentValueLiteral);

        var result = RecipeParameterEditor.Apply(recipe, [new RecipeParameterEdit("travel", "core", "scale", "1.0")]);

        Assert.IsTrue(result.IsAccepted, Render(result.Issues));
        Assert.AreEqual(0, result.Issues.Count, "Supplying the one missing key clears the L1.5 finding.");
        CollectionAssert.AreEqual(new[] { "/stages/1/modules/0/parameters/scale" }, Diff(recipe, result.RecipeJson!));
    }

    [TestMethod]
    public void UnparseableOrStagelessDocumentsDescribeAsEmptyAndRejectEdits()
    {
        Assert.AreEqual(0, RecipeParameterEditor.Describe("not json").Modules.Count);
        Assert.AreEqual(0, RecipeParameterEditor.Describe("{\"id\":\"x\"}").ParameterCount);
        Assert.AreEqual(0, RecipeParameterEditor.Describe("[]").Warnings.Count);

        AssertRejected(
            RecipeParameterEditor.Apply("not json", [new RecipeParameterEdit("travel", "core", "scale", "1.5")]),
            RecipeParameterEditCodes.DocumentNotEditable,
            "/");
        AssertRejected(
            RecipeParameterEditor.Apply("{\"id\":\"x\"}", [new RecipeParameterEdit("travel", "core", "scale", "1.5")]),
            RecipeParameterEditCodes.DocumentNotEditable,
            "stages");
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("  ")]
    [DataRow("\r\n\t")]
    public void AnEmptyOrWhitespaceDocumentDescribesAsEmptyAndRejectsEditsTyped(string recipeJson)
    {
        // Canonicalize throws ArgumentException (not JsonException) for blank text; neither entry point may leak it.
        var panel = RecipeParameterEditor.Describe(recipeJson);
        Assert.AreEqual(0, panel.Modules.Count);
        Assert.AreEqual(0, panel.Warnings.Count);
        Assert.AreEqual(0, panel.ParameterCount);

        AssertRejected(
            RecipeParameterEditor.Apply(recipeJson, [new RecipeParameterEdit("travel", "core", "scale", "1.5")]),
            RecipeParameterEditCodes.DocumentNotEditable,
            "/");
    }

    [TestMethod]
    public void AnInRangeEditIsAcceptedAndChangesOnlyThatValue()
    {
        var result = RecipeParameterEditor.Apply(FireBolt.RecipeJson, [new RecipeParameterEdit("travel", "core", "scale", "1.5")]);

        Assert.IsTrue(result.IsAccepted, Render(result.Issues));
        Assert.AreEqual(0, result.Issues.Count, Render(result.Issues));
        CollectionAssert.AreEqual(new[] { "/stages/1/modules/0/parameters/scale" }, Diff(FireBolt.RecipeJson, result.RecipeJson!));
        Assert.AreEqual(1.5, ReadScale(result.RecipeJson!));
        Assert.AreEqual(RecipeCanonicalJson.Canonicalize(result.RecipeJson!), result.RecipeJson, "The output is canonical.");
        Assert.AreEqual(RecipeCanonicalJson.ComputeSha256(result.RecipeJson!), result.CanonicalSha256);
        Assert.AreNotEqual(FireBolt.CanonicalSha256, result.CanonicalSha256);
        Assert.AreEqual(0, RecipeCatalogPrevalidator.Prevalidate(result.RecipeJson!).Count);
    }

    [TestMethod]
    public void TwoEditsOnTwoModulesChangeExactlyTwoValues()
    {
        var result = RecipeParameterEditor.Apply(
            BurstingFireball.RecipeJson,
            [
                new RecipeParameterEdit("travel", "core", "scale", "0.9"),
                new RecipeParameterEdit("impact", "burst", "count", "30"),
            ]);

        Assert.IsTrue(result.IsAccepted, Render(result.Issues));
        CollectionAssert.AreEqual(
            new[] { "/stages/1/modules/0/parameters/scale", "/stages/2/modules/0/parameters/count" },
            Diff(BurstingFireball.RecipeJson, result.RecipeJson!));
        StringAssert.Contains(result.RecipeJson, "\"count\":30", "Integer parameters are written as integer tokens.");
    }

    [TestMethod]
    [DataRow("3.0")]
    [DataRow("0.5")]
    [DataRow("2.4000001")]
    public void AValueOutsideTheInclusiveBoundsIsRejectedWithPathAndRangeAndIsNeverClamped(string rawText)
    {
        var result = RecipeParameterEditor.Apply(FireBolt.RecipeJson, [new RecipeParameterEdit("travel", "core", "scale", rawText)]);

        var issue = AssertRejected(result, RecipeParameterEditCodes.ValueOutOfRange, ScalePath);
        Assert.AreEqual("[0.6, 2.4]", issue.AllowedRange);
        Assert.AreEqual(rawText, issue.ActualValueJson, "The offending text is reported verbatim, not a corrected value.");
        Assert.IsNull(result.RecipeJson);
        Assert.IsNull(result.CanonicalSha256);
    }

    [TestMethod]
    [DataRow("0.6", 0.6)]
    [DataRow("2.4", 2.4)]
    [DataRow(" 1.8 ", 1.8)]
    public void AValueExactlyOnABoundOrWithSurroundingWhitespaceIsAccepted(string rawText, double expected)
    {
        var result = RecipeParameterEditor.Apply(FireBolt.RecipeJson, [new RecipeParameterEdit("travel", "core", "scale", rawText)]);

        Assert.IsTrue(result.IsAccepted, Render(result.Issues));
        Assert.AreEqual(expected, ReadScale(result.RecipeJson!));
    }

    [TestMethod]
    [DataRow("1.5")]
    [DataRow("1e2")]
    [DataRow("")]
    [DataRow("NaN")]
    [DataRow("24.0")]
    [DataRow("1,5")]
    public void AnIntegerParameterOnlyAcceptsAnIntegerLiteral(string rawText)
    {
        var result = RecipeParameterEditor.Apply(BurstingFireball.RecipeJson, [new RecipeParameterEdit("impact", "burst", "count", rawText)]);

        var issue = AssertRejected(result, RecipeParameterEditCodes.ValueNotInteger, "stages[impact].modules[burst].parameters.count");
        Assert.AreEqual("integer in [8, 40]", issue.AllowedRange);
    }

    [TestMethod]
    [DataRow("8", 8L)]
    [DataRow("40", 40L)]
    [DataRow(" 12 ", 12L)]
    public void AnIntegerExactlyOnABoundOrWithSurroundingWhitespaceIsAccepted(string rawText, long expected)
    {
        // Same whitespace tolerance as the float path: the text is trimmed, the number itself is never corrected.
        var result = RecipeParameterEditor.Apply(BurstingFireball.RecipeJson, [new RecipeParameterEdit("impact", "burst", "count", rawText)]);

        Assert.IsTrue(result.IsAccepted, Render(result.Issues));
        using var document = JsonDocument.Parse(result.RecipeJson!);
        Assert.AreEqual(
            expected,
            document.RootElement.GetProperty("stages")[2].GetProperty("modules")[0].GetProperty("parameters").GetProperty("count").GetInt64());
    }

    [TestMethod]
    [DataRow("NaN")]
    [DataRow("Infinity")]
    [DataRow("-Infinity")]
    [DataRow("abc")]
    [DataRow("")]
    [DataRow("1,5")]
    public void AFloatParameterRejectsNonFiniteOrNonNumericText(string rawText)
    {
        var result = RecipeParameterEditor.Apply(FireBolt.RecipeJson, [new RecipeParameterEdit("travel", "core", "scale", rawText)]);

        var issue = AssertRejected(result, RecipeParameterEditCodes.ValueNotFinite, ScalePath);
        Assert.AreEqual("float in [0.6, 2.4]", issue.AllowedRange);
    }

    [TestMethod]
    [DataRow("nowhere", "core", "scale", "stages[nowhere].modules[core].parameters.scale")]
    [DataRow("travel", "ghost", "scale", "stages[travel].modules[ghost].parameters.scale")]
    [DataRow("travel", "core", "width", "stages[travel].modules[core].parameters.width")]
    public void AnUnlocatableStageModuleOrParameterIsRejected(string stageId, string moduleId, string parameterName, string expectedPath)
    {
        var result = RecipeParameterEditor.Apply(FireBolt.RecipeJson, [new RecipeParameterEdit(stageId, moduleId, parameterName, "1.0")]);

        AssertRejected(result, RecipeParameterEditCodes.TargetNotFound, expectedPath);
    }

    [TestMethod]
    public void EveryOffendingEditIsReportedInOneRejection()
    {
        var result = RecipeParameterEditor.Apply(
            BurstingFireball.RecipeJson,
            [
                new RecipeParameterEdit("travel", "core", "scale", "9"),
                new RecipeParameterEdit("impact", "burst", "count", "x"),
                new RecipeParameterEdit("impact", "burst", "speed", "2.0"),
            ]);

        Assert.IsFalse(result.IsAccepted);
        CollectionAssert.AreEqual(
            new[] { RecipeParameterEditCodes.ValueOutOfRange, RecipeParameterEditCodes.ValueNotInteger },
            result.Issues.Select(static issue => issue.Code).ToArray(),
            "The valid third edit produces no issue and nothing is applied.");
    }

    [TestMethod]
    public void ZeroEditsAndNoOpEditsAreRejectedSoNoVersionLandsForNoChange()
    {
        AssertRejected(RecipeParameterEditor.Apply(FireBolt.RecipeJson, []), RecipeParameterEditCodes.NoChanges, "stages");
        AssertRejected(
            RecipeParameterEditor.Apply(FireBolt.RecipeJson, [new RecipeParameterEdit("travel", "core", "scale", "1.20")]),
            RecipeParameterEditCodes.NoChanges,
            "stages");
    }

    [TestMethod]
    public void TheSameTargetEditedTwiceIsRejected()
    {
        var result = RecipeParameterEditor.Apply(
            FireBolt.RecipeJson,
            [
                new RecipeParameterEdit("travel", "core", "scale", "1.5"),
                new RecipeParameterEdit("travel", "core", "scale", "1.6"),
            ]);

        AssertRejected(result, RecipeParameterEditCodes.DuplicateTarget, ScalePath);
    }

    [TestMethod]
    public void AnL1FailureRejectsTheEditWithTheL1Issues()
    {
        var recipe = Mutate(FireBolt.RecipeJson, root => root.Remove("metadata"));

        var result = RecipeParameterEditor.Apply(recipe, [new RecipeParameterEdit("travel", "core", "scale", "1.5")]);

        Assert.IsFalse(result.IsAccepted);
        Assert.IsNull(result.RecipeJson);
        var issue = result.Issues.Single(static issue => issue.Path == "/metadata");
        Assert.AreEqual("E101", issue.Code);
        Assert.AreEqual(RecipeValidationSeverity.Error, issue.Severity);
    }

    [TestMethod]
    public void L15FindingsRideAlongAnAcceptedEditAsWarnings()
    {
        // Three modules exceed the strict budget but every module is L1-valid, so the edit lands with a warning.
        var recipe = Mutate(BurstingFireball.RecipeJson, root =>
            ((JsonArray)root["stages"]![0]!["modules"]!).Add(new JsonObject
            {
                ["id"] = "flash",
                ["kind"] = "impact_flash",
                ["templateId"] = "PFT_2D_LaunchFlash",
                ["parameters"] = new JsonObject { ["lifetime"] = 0.12, ["size"] = 1.0 },
                ["enabled"] = true,
            }));

        var result = RecipeParameterEditor.Apply(recipe, [new RecipeParameterEdit("launch", "flash", "size", "1.5")]);

        Assert.IsTrue(result.IsAccepted, Render(result.Issues));
        var warning = result.Issues.Single();
        Assert.AreEqual(RecipePrevalidationCodes.ModuleBudgetExceeded, warning.Code);
        Assert.AreEqual(RecipeValidationSeverity.Warning, warning.Severity);
        CollectionAssert.AreEqual(new[] { "/stages/0/modules/0/parameters/size" }, Diff(recipe, result.RecipeJson!));
    }

    [TestMethod]
    public void ApplyIsDeterministicAndItsOutputIsCanonicalForANonCanonicalInput()
    {
        var indented = JsonNode.Parse(FireBolt.RecipeJson)!.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        RecipeParameterEdit[] edits = [new("travel", "core", "scale", "0.75")];

        var first = RecipeParameterEditor.Apply(indented, edits);
        var second = RecipeParameterEditor.Apply(indented, edits);

        Assert.IsTrue(first.IsAccepted, Render(first.Issues));
        Assert.AreEqual(first.RecipeJson, second.RecipeJson);
        Assert.AreEqual(first.CanonicalSha256, second.CanonicalSha256);
        Assert.AreEqual(RecipeCanonicalJson.Canonicalize(first.RecipeJson!), first.RecipeJson);
    }

    [TestMethod]
    public void AnAcceptedEditBecomesAHumanEditRevisionInheritingTheParentSummary()
    {
        var parent = FireBolt.CreateDraftRecord(DateTimeOffset.UtcNow);
        var result = RecipeParameterEditor.Apply(parent.RecipeJson, [new RecipeParameterEdit("travel", "core", "scale", "1.5")]);

        var revision = RecipeParameterEditor.CreateHumanEditRevision(parent, result);

        Assert.AreEqual(RecipeDraftOrigin.HumanEdit, revision.Origin);
        Assert.AreEqual(0, revision.RequestCount);
        Assert.IsNull(revision.FeedbackText);
        Assert.AreEqual(result.RecipeJson, revision.Draft.RecipeJson);
        Assert.AreEqual(result.CanonicalSha256, revision.Draft.CanonicalSha256);
        Assert.AreEqual(parent.RecipeId, revision.Draft.RecipeId);
        Assert.AreEqual(parent.Archetype, revision.Draft.Archetype);
        Assert.AreEqual(parent.Dimension, revision.Draft.Dimension);
        Assert.AreEqual(parent.TargetProfile, revision.Draft.TargetProfile);
        Assert.AreEqual(RecipeParameterEditor.HumanEditPromptTemplateVersion, revision.Draft.PromptTemplateVersion);
        Assert.AreEqual(RecipeTemplateCatalogSnapshot.Default.TemplateCatalogVersion, revision.Draft.TemplateCatalogVersion);
        Assert.AreNotEqual(parent.CorrelationId, revision.Draft.CorrelationId);
    }

    [TestMethod]
    public void ARejectedResultOrAFailedParentCannotBecomeARevision()
    {
        var parent = FireBolt.CreateDraftRecord(DateTimeOffset.UtcNow);
        var rejected = RecipeParameterEditor.Apply(parent.RecipeJson, []);
        var accepted = RecipeParameterEditor.Apply(parent.RecipeJson, [new RecipeParameterEdit("travel", "core", "scale", "1.5")]);
        var failed = RecipeDraftRecord.Create(RecipeDraftTestData.FailedResult(), DateTimeOffset.UtcNow);

        Assert.ThrowsExactly<ArgumentException>(() => RecipeParameterEditor.CreateHumanEditRevision(parent, rejected));
        Assert.ThrowsExactly<ArgumentException>(() => RecipeParameterEditor.CreateHumanEditRevision(failed, accepted));
    }

    [TestMethod]
    public void TheEditorCodeSetIsClosedAndEveryCodeCarriesAMessage()
    {
        CollectionAssert.AreEquivalent(
            new[]
            {
                RecipeParameterEditCodes.NoChanges,
                RecipeParameterEditCodes.TargetNotFound,
                RecipeParameterEditCodes.ValueNotInteger,
                RecipeParameterEditCodes.ValueNotFinite,
                RecipeParameterEditCodes.ValueOutOfRange,
                RecipeParameterEditCodes.DocumentNotEditable,
                RecipeParameterEditCodes.DuplicateTarget,
            },
            RecipeParameterEditCodes.All.ToArray());
        foreach (var code in RecipeParameterEditCodes.All)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(RecipeParameterEditCodes.MessageOf(code)));
            Assert.IsFalse(RecipePrevalidationCodes.All.Contains(code), "Editor codes never collide with L1.5 codes.");
        }

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => RecipeParameterEditCodes.MessageOf("VFXE9999"));
    }

    [TestMethod]
    public void EditConstructionGuardsItsAddressAndText()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new RecipeParameterEdit(" ", "core", "scale", "1"));
        Assert.ThrowsExactly<ArgumentException>(() => new RecipeParameterEdit("travel", "", "scale", "1"));
        Assert.ThrowsExactly<ArgumentException>(() => new RecipeParameterEdit("travel", "core", "", "1"));
        Assert.ThrowsExactly<ArgumentNullException>(() => new RecipeParameterEdit("travel", "core", "scale", null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => RecipeParameterEditor.Describe(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => RecipeParameterEditor.Apply(FireBolt.RecipeJson, null!));
    }

    private static RecipeValidationIssue AssertRejected(RecipeParameterEditResult result, string code, string path)
    {
        Assert.IsFalse(result.IsAccepted, "Expected a rejection but the edit was accepted.");
        Assert.IsNull(result.RecipeJson);
        var issue = result.Issues.Single();
        Assert.AreEqual(code, issue.Code);
        Assert.AreEqual(path, issue.Path);
        Assert.AreEqual(RecipeValidationSeverity.Error, issue.Severity);
        Assert.AreEqual(RecipeParameterEditCodes.MessageOf(code), issue.Message);
        return issue;
    }

    private static double ReadScale(string recipeJson)
    {
        using var document = JsonDocument.Parse(recipeJson);
        return document.RootElement.GetProperty("stages")[1].GetProperty("modules")[0].GetProperty("parameters").GetProperty("scale").GetDouble();
    }

    private static string Mutate(string recipeJson, Action<JsonObject> mutate)
    {
        var root = JsonNode.Parse(recipeJson)!.AsObject();
        mutate(root);
        return root.ToJsonString();
    }

    private static JsonObject Module(JsonObject root) => root["stages"]![1]!["modules"]![0]!.AsObject();

    private static JsonObject Parameters(JsonObject root) => Module(root)["parameters"]!.AsObject();

    private static string Render(IEnumerable<RecipeValidationIssue> issues) =>
        string.Join("; ", issues.Select(static issue => issue.Code + " " + issue.Path));

    /// <summary>Every JSON pointer at which the two documents differ (values or structure), in document order.</summary>
    private static string[] Diff(string leftJson, string rightJson)
    {
        using var left = JsonDocument.Parse(leftJson);
        using var right = JsonDocument.Parse(rightJson);
        var differences = new List<string>();
        Diff(left.RootElement, right.RootElement, string.Empty, differences);
        return differences.ToArray();
    }

    private static void Diff(JsonElement left, JsonElement right, string pointer, List<string> differences)
    {
        if (left.ValueKind != right.ValueKind)
        {
            differences.Add(pointer.Length == 0 ? "/" : pointer);
            return;
        }

        switch (left.ValueKind)
        {
            case JsonValueKind.Object:
                var leftNames = left.EnumerateObject().Select(static property => property.Name).Order(StringComparer.Ordinal).ToArray();
                var rightNames = right.EnumerateObject().Select(static property => property.Name).Order(StringComparer.Ordinal).ToArray();
                foreach (var name in leftNames.Union(rightNames, StringComparer.Ordinal).Order(StringComparer.Ordinal))
                {
                    var child = pointer + "/" + name;
                    if (!left.TryGetProperty(name, out var leftChild) || !right.TryGetProperty(name, out var rightChild))
                    {
                        differences.Add(child);
                        continue;
                    }

                    Diff(leftChild, rightChild, child, differences);
                }

                break;
            case JsonValueKind.Array:
                var leftItems = left.EnumerateArray().ToArray();
                var rightItems = right.EnumerateArray().ToArray();
                if (leftItems.Length != rightItems.Length)
                {
                    differences.Add(pointer);
                    break;
                }

                for (var index = 0; index < leftItems.Length; index++)
                {
                    Diff(leftItems[index], rightItems[index], pointer + "/" + index.ToString(CultureInfo.InvariantCulture), differences);
                }

                break;
            default:
                if (!string.Equals(
                        RecipeCanonicalJson.Canonicalize(left),
                        RecipeCanonicalJson.Canonicalize(right),
                        StringComparison.Ordinal))
                {
                    differences.Add(pointer);
                }

                break;
        }
    }
}

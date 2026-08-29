using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VFXComposer.AI.Contracts.Recipes;
using VFXComposer.AI.Providers.Recipes;

namespace VFXComposer.AI.Tests.Recipes;

[TestClass]
public sealed class RecipeL1ValidationTests
{
    [TestMethod]
    public void MarkdownFencesAndSurroundingProseAreStripped()
    {
        var text = "Sure! Here it is:\n```json\n{\"a\":1}\n```\nHope this helps.";
        Assert.IsTrue(RecipeOutputParser.TryExtractJson(text, out var json, out _));
        Assert.AreEqual("{\"a\":1}", json);
    }

    [TestMethod]
    public void ABareObjectInsideProseIsExtractedByBraceBalancing()
    {
        var text = "prefix text {\"a\":{\"b\":\"}\"}} suffix";
        Assert.IsTrue(RecipeOutputParser.TryExtractJson(text, out var json, out _));
        Assert.AreEqual("{\"a\":{\"b\":\"}\"}}", json);
    }

    [TestMethod]
    public void TextWithoutAnyObjectReportsE104()
    {
        Assert.IsFalse(RecipeOutputParser.TryExtractJson("no json here", out _, out var issue));
        Assert.AreEqual("E104", issue!.Code);
    }

    [TestMethod]
    public void AMissingRequiredFieldReportsE101AtItsPath()
    {
        var recipe = LoadCanonical();
        recipe.Remove("stages");
        var issues = RecipeL1Validator.Validate(recipe.ToJsonString());
        Assert.IsTrue(issues.Any(static issue => issue.Code == "E101" && issue.Path.Contains("stages", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void AnUnknownTopLevelFieldReportsE100()
    {
        var recipe = LoadCanonical();
        recipe["intensity"] = 5;
        var issues = RecipeL1Validator.Validate(recipe.ToJsonString());
        Assert.IsTrue(issues.Any(static issue => issue.Code == "E100" && issue.Path.Contains("intensity", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void AWrongValueTypeReportsE102()
    {
        var recipe = LoadCanonical();
        recipe["randomSeed"] = "not-a-number";
        var issues = RecipeL1Validator.Validate(recipe.ToJsonString());
        Assert.IsTrue(issues.Any(static issue => issue.Code == "E102"));
    }

    [TestMethod]
    public void AnUnsupportedRecipeVersionReportsE301()
    {
        var recipe = LoadCanonical();
        recipe["recipeVersion"] = 2;
        var issues = RecipeL1Validator.Validate(recipe.ToJsonString());
        Assert.IsTrue(issues.Any(static issue => issue.Code == "E301"));
    }

    [TestMethod]
    public void DuplicateStageIdsReportE303()
    {
        var recipe = LoadCanonical();
        var stages = recipe["stages"]!.AsArray();
        stages[1]!["id"] = stages[0]!["id"]!.GetValue<string>();
        var issues = RecipeL1Validator.Validate(recipe.ToJsonString());
        Assert.IsTrue(issues.Any(static issue => issue.Code == "E303"));
    }

    [TestMethod]
    public void ValidationIssuesAreErrorsWithStablePaths()
    {
        var issues = RecipeL1Validator.Validate("{}");
        Assert.IsTrue(issues.Count > 0);
        Assert.IsTrue(issues.All(static issue => issue.Severity == RecipeValidationSeverity.Error));
        Assert.IsTrue(issues.All(static issue => issue.Path.StartsWith('/')));
    }

    [TestMethod]
    public void CanonicalizationIsStableAcrossKeyOrderAndRejectsDuplicates()
    {
        var left = RecipeCanonicalJson.Canonicalize("{\"b\":2,\"a\":1}");
        var right = RecipeCanonicalJson.Canonicalize("{\"a\":1,\"b\":2}");
        Assert.AreEqual(left, right);
        Assert.AreEqual("{\"a\":1,\"b\":2}", left);
        Assert.AreEqual(
            RecipeCanonicalJson.ComputeSha256("{\"b\":2,\"a\":1}"),
            RecipeCanonicalJson.ComputeSha256("{ \"a\" : 1, \"b\" : 2 }"));
        var rejectedDuplicate = false;
        try
        {
            RecipeCanonicalJson.Canonicalize("{\"a\":1,\"a\":2}");
        }
        catch (System.Text.Json.JsonException)
        {
            rejectedDuplicate = true;
        }

        Assert.IsTrue(rejectedDuplicate, "Duplicate JSON properties must be rejected by canonicalization.");
    }

    private static JsonObject LoadCanonical() =>
        JsonNode.Parse(RecipeTemplateCatalogSnapshot.Default.CanonicalExampleJson)!.AsObject();
}

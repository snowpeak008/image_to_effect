using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VFXComposer.AI.Contracts.Recipes;
using VFXComposer.AI.Providers.Recipes;

namespace VFXComposer.AI.Tests.Recipes;

/// <summary>
/// L1.5 coverage matrix: the strict-compliant shapes yield zero findings, every defect class yields exactly its own
/// code at the exact path, and every finding stays a warning so concatenation can never flip an error-driven verdict.
/// The mutation base is the prompt's own reference recipe: if that recipe ever stops being the canonical compliant
/// shape these tests fail before the prompt does.
/// </summary>
[TestClass]
public sealed class RecipeCatalogPrevalidatorTests
{
    [TestMethod]
    public void ThePromptReferenceRecipeYieldsNoFindings()
    {
        var issues = RecipeCatalogPrevalidator.Prevalidate(RecipePromptTemplate.ReferenceRecipeJson);
        Assert.AreEqual(0, issues.Count, Render(issues));
    }

    [TestMethod]
    public void TheCommittedSampleRecipeYieldsNoFindings()
    {
        // The machine-verified E2E sample must stay strict-compliant; walk up from the test base directory to the
        // repository checkout, failing (not skipping) when the file cannot be found.
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null &&
               !File.Exists(Path.Combine(directory.FullName, "batches", "recipes", "spark_projectile_2d.json")))
        {
            directory = directory.Parent;
        }

        Assert.IsNotNull(directory, "The repository root carrying batches/recipes was not found.");
        var sample = File.ReadAllText(
            Path.Combine(directory.FullName, "batches", "recipes", "spark_projectile_2d.json"));

        var issues = RecipeCatalogPrevalidator.Prevalidate(sample);
        Assert.AreEqual(0, issues.Count, Render(issues));
    }

    [TestMethod]
    public void AFullBudgetRecipeWithAnIntegerParameterYieldsNoFindings()
    {
        // Two modules is exactly the strict budget; PFT_2D_FireImpact carries the integer-typed "count".
        var recipe = MutateReference(recipe =>
        {
            recipe["stages"]![2]!["modules"] = new JsonArray(new JsonObject
            {
                ["id"] = "burst",
                ["kind"] = "impact_burst",
                ["templateId"] = "PFT_2D_FireImpact",
                ["parameters"] = new JsonObject { ["count"] = 24, ["speed"] = 3.5 },
                ["enabled"] = true,
            });
        });

        var issues = RecipeCatalogPrevalidator.Prevalidate(recipe);
        Assert.AreEqual(0, issues.Count, Render(issues));
    }

    [TestMethod]
    public void AnUnknownTemplateIsReportedWithTheDeclaredCandidates()
    {
        var issue = Single(
            MutateReference(recipe => Module(recipe)["templateId"] = "PFT_2D_Nonexistent"),
            RecipePrevalidationCodes.TemplateUnknown);

        Assert.AreEqual("/stages/travel/modules/core/templateId", issue.Path);
        StringAssert.Contains(issue.AllowedRange, "PFT_2D_FireCore");
    }

    [TestMethod]
    public void AKindDisagreeingWithTheTemplateIsReported()
    {
        var issue = Single(
            MutateReference(recipe => Module(recipe)["kind"] = "motion_trail"),
            RecipePrevalidationCodes.TemplateKindMismatch);

        Assert.AreEqual("/stages/travel/modules/core/kind", issue.Path);
        Assert.AreEqual("[energy_body]", issue.AllowedRange);
    }

    [TestMethod]
    public void AMissingDeclaredParameterIsReported()
    {
        var issue = Single(
            MutateReference(recipe => Module(recipe)["parameters"] = new JsonObject()),
            RecipePrevalidationCodes.ParameterMissing);

        Assert.AreEqual("/stages/travel/modules/core/parameters/scale", issue.Path);
    }

    [TestMethod]
    public void AnUndeclaredParameterKeyIsReported()
    {
        var issue = Single(
            MutateReference(recipe => Parameters(recipe)["turbulence"] = 1.0),
            RecipePrevalidationCodes.ParameterUnknown);

        Assert.AreEqual("/stages/travel/modules/core/parameters/turbulence", issue.Path);
        StringAssert.Contains(issue.AllowedRange, "scale");
    }

    [TestMethod]
    [DataRow(0.5)]
    [DataRow(2.5)]
    public void AValueOutsideTheInclusiveBoundsIsReported(double value)
    {
        var issue = Single(
            MutateReference(recipe => Parameters(recipe)["scale"] = value),
            RecipePrevalidationCodes.ParameterOutOfRange);

        Assert.AreEqual("/stages/travel/modules/core/parameters/scale", issue.Path);
        StringAssert.Contains(issue.AllowedRange, "0.6");
        StringAssert.Contains(issue.AllowedRange, "2.4");
    }

    [TestMethod]
    public void TheInclusiveBoundsThemselvesYieldNoFinding()
    {
        foreach (var boundary in new[] { 0.6, 2.4 })
        {
            var issues = RecipeCatalogPrevalidator.Prevalidate(
                MutateReference(recipe => Parameters(recipe)["scale"] = boundary));
            Assert.AreEqual(0, issues.Count, Render(issues));
        }
    }

    [TestMethod]
    public void ANonNumericParameterValueIsReported()
    {
        var issue = Single(
            MutateReference(recipe => Parameters(recipe)["scale"] = "big"),
            RecipePrevalidationCodes.ParameterTypeMismatch);

        Assert.AreEqual("/stages/travel/modules/core/parameters/scale", issue.Path);
    }

    [TestMethod]
    public void AFractionalValueForAnIntegerParameterIsReported()
    {
        var recipe = MutateReference(recipe =>
        {
            var module = Module(recipe);
            module["kind"] = "impact_burst";
            module["templateId"] = "PFT_2D_FireImpact";
            module["parameters"] = new JsonObject { ["count"] = 8.5, ["speed"] = 3.5 };
        });

        var issue = Single(recipe, RecipePrevalidationCodes.ParameterTypeMismatch);
        Assert.AreEqual("/stages/travel/modules/core/parameters/count", issue.Path);
        StringAssert.Contains(issue.AllowedRange, "integer");
    }

    [TestMethod]
    public void AMissingStageRootIsReportedAtItsOwnPath()
    {
        var issue = Single(
            MutateReference(recipe => recipe["stages"]!.AsArray().RemoveAt(2)),
            RecipePrevalidationCodes.StageRootMissing);

        Assert.AreEqual("/stages/impact", issue.Path);
    }

    [TestMethod]
    public void StageRootsInTheWrongOrderAreReportedOnce()
    {
        var recipe = MutateReference(recipe =>
        {
            var stages = recipe["stages"]!.AsArray();
            var launch = stages[0]!.DeepClone();
            stages.RemoveAt(0);
            stages.Insert(1, launch);
        });

        var issue = Single(recipe, RecipePrevalidationCodes.StageRootOutOfOrder);
        Assert.AreEqual("/stages", issue.Path);
    }

    [TestMethod]
    public void AThirdModuleExceedsTheStrictBudget()
    {
        var recipe = MutateReference(recipe =>
        {
            static JsonObject Ember(string id) => new()
            {
                ["id"] = id,
                ["kind"] = "secondary_particles",
                ["templateId"] = "PFT_2D_Embers",
                ["parameters"] = new JsonObject { ["lifetime"] = 0.55, ["rate"] = 18 },
                ["enabled"] = true,
            };
            recipe["stages"]![0]!["modules"] = new JsonArray(Ember("flash"));
            recipe["stages"]![2]!["modules"] = new JsonArray(Ember("sparks"));
        });

        var issue = Single(recipe, RecipePrevalidationCodes.ModuleBudgetExceeded);
        Assert.AreEqual("/stages", issue.Path);
        Assert.AreEqual("3", issue.ActualValueJson);
    }

    [TestMethod]
    public void AnAttachToEdgeIsReportedEvenWhenTheTemplateIsValid()
    {
        var issue = Single(
            MutateReference(recipe => Module(recipe)["attachTo"] = "core"),
            RecipePrevalidationCodes.AttachmentNotAllowed);

        Assert.AreEqual("/stages/travel/modules/core/attachTo", issue.Path);
    }

    [TestMethod]
    public void ANonBuildableArchetypeIsReported()
    {
        var issue = Single(
            MutateReference(recipe => recipe["archetype"] = "beam"),
            RecipePrevalidationCodes.ArchetypeNotBuildable);

        Assert.AreEqual("/archetype", issue.Path);
        StringAssert.Contains(issue.AllowedRange, "projectile");
    }

    [TestMethod]
    public void ANonBuildableDimensionIsReported()
    {
        var issue = Single(
            MutateReference(recipe => recipe["dimension"] = "3d"),
            RecipePrevalidationCodes.DimensionNotBuildable);

        Assert.AreEqual("/dimension", issue.Path);
        StringAssert.Contains(issue.AllowedRange, "2d");
    }

    [TestMethod]
    public void EveryFindingIsAWarningAndNeverAnError()
    {
        // One recipe tripping many defect classes at once: severities must all stay Warning (the §7 ruling makes
        // L1.5 a presentation-layer surface, so it must be impossible for it to flip an error-driven decision).
        var recipe = MutateReference(recipe =>
        {
            recipe["archetype"] = "beam";
            recipe["dimension"] = "3d";
            var module = Module(recipe);
            module["attachTo"] = "core";
            Parameters(recipe)["scale"] = 99.0;
            recipe["stages"]!.AsArray().RemoveAt(0);
        });

        var issues = RecipeCatalogPrevalidator.Prevalidate(recipe);
        Assert.IsTrue(issues.Count >= 5, Render(issues));
        foreach (var issue in issues)
        {
            Assert.AreEqual(RecipeValidationSeverity.Warning, issue.Severity, issue.Code);
        }
    }

    [TestMethod]
    public void AnUnparseableDocumentBelongsToL1AndYieldsNoFindings()
    {
        Assert.AreEqual(0, RecipeCatalogPrevalidator.Prevalidate("not json").Count);
        Assert.AreEqual(0, RecipeCatalogPrevalidator.Prevalidate("[1, 2]").Count);
    }

    [TestMethod]
    public void EveryPrevalidationCodeCarriesASuggestionKey()
    {
        foreach (var code in RecipePrevalidationCodes.All)
        {
            Assert.IsTrue(
                RecipeIssueSuggestions.TryGetSuggestionKey(code, out var key),
                code + " carries no suggestion key.");
            Assert.IsTrue(RecipeSuggestionKeys.All.Contains(key!), code + " maps outside the closed key set.");
        }
    }

    [TestMethod]
    public void EverySuggestionKeyConstantSpellsItsOwnName()
    {
        foreach (var field in typeof(RecipeSuggestionKeys).GetFields().Where(static field => field.IsLiteral))
        {
            Assert.AreEqual("RecipeSuggestion" + field.Name, (string)field.GetRawConstantValue()!);
        }
    }

    [TestMethod]
    public void EveryPrevalidationMessageIsASingleFixedSentenceWithoutPaths()
    {
        foreach (var definition in RecipePrevalidationCatalog.All.Values)
        {
            Assert.IsFalse(definition.Message.Contains('\n'), definition.Code);
            Assert.IsTrue(definition.Message.EndsWith('.'), definition.Code);
            Assert.IsFalse(definition.Message.Contains(":\\", StringComparison.Ordinal), definition.Code);
            Assert.IsFalse(definition.Message.Contains("C:", StringComparison.OrdinalIgnoreCase), definition.Code);
        }

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => RecipePrevalidationCatalog.Require("VFXP9999"));
    }

    [TestMethod]
    public void TheBoundsQueryAnswersForEveryCommittedParameter()
    {
        var snapshot = RecipeTemplateCatalogSnapshot.Default;
        foreach (var template in snapshot.Templates)
        {
            foreach (var parameter in template.Parameters)
            {
                Assert.IsTrue(
                    snapshot.TryGetParameter(template.TemplateId, parameter.Name, out var found),
                    template.TemplateId + "/" + parameter.Name);
                Assert.IsTrue(found!.Minimum <= found.Default && found.Default <= found.Maximum);
            }
        }

        Assert.IsFalse(snapshot.TryGetParameter("PFT_2D_FireCore", "no_such_parameter", out _));
        Assert.IsFalse(snapshot.TryGetParameter("PFT_2D_Nonexistent", "scale", out _));
    }

    private static JsonObject Module(JsonNode recipe) =>
        recipe["stages"]![1]!["modules"]![0]!.AsObject();

    private static JsonObject Parameters(JsonNode recipe) => Module(recipe)["parameters"]!.AsObject();

    private static string MutateReference(Action<JsonNode> mutate)
    {
        var recipe = JsonNode.Parse(RecipePromptTemplate.ReferenceRecipeJson)!;
        mutate(recipe);
        return recipe.ToJsonString();
    }

    private static RecipeValidationIssue Single(string recipeJson, string expectedCode)
    {
        var issues = RecipeCatalogPrevalidator.Prevalidate(recipeJson);
        var matches = issues.Where(issue => issue.Code == expectedCode).ToArray();
        Assert.AreEqual(1, matches.Length, "expected exactly one " + expectedCode + " but got: " + Render(issues));
        Assert.AreEqual(issues.Count, matches.Length, "unexpected extra findings: " + Render(issues));
        return matches[0];
    }

    private static string Render(IReadOnlyList<RecipeValidationIssue> issues) =>
        issues.Count == 0 ? "(none)" : string.Join("; ", issues.Select(static issue => issue.Code + " " + issue.Path));
}

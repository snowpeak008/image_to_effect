using System.Globalization;
using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VFXComposer.AI.Contracts.Recipes;
using VFXComposer.AI.Providers.Recipes;

namespace VFXComposer.AI.Tests.Recipes;

/// <summary>
/// The recipe prompt may only teach a shape that the project build audit accepts under the strict simple-profile
/// budget: three stage roots, at most two modules in the whole recipe, and no attachTo. The structure check below
/// is test-side on purpose; the production pre-validation layer is a separate task.
/// </summary>
[TestClass]
public sealed class RecipePromptComplianceTests
{
    /// <summary>Mirrors the private per-message bound of <see cref="RecipePromptTemplate"/>.</summary>
    private const int MaximumMessageCharacters = 16 * 1024;

    private const int MaximumModules = 2;

    private static readonly (string Id, string Trigger)[] RequiredStageRoots =
    [
        ("launch", "on_launch"),
        ("travel", "after_previous"),
        ("impact", "on_hit"),
    ];

    [TestMethod]
    public void TheInjectedReferenceRecipePassesL1Validation()
    {
        var issues = RecipeL1Validator.Validate(RecipePromptTemplate.ReferenceRecipeJson);
        Assert.AreEqual(0, issues.Count, string.Join("; ", issues.Select(static issue => issue.Code + " " + issue.Path)));
    }

    [TestMethod]
    public void TheInjectedReferenceRecipeSatisfiesTheStrictStructureBudget()
    {
        var violations = StrictStructureViolations(RecipePromptTemplate.ReferenceRecipeJson);
        Assert.AreEqual(0, violations.Count, string.Join("; ", violations));
    }

    [TestMethod]
    public void TheInjectedReferenceRecipeAgreesWithTheCommittedCatalogSnapshot()
    {
        var snapshot = RecipeTemplateCatalogSnapshot.Default;
        var reference = JsonNode.Parse(RecipePromptTemplate.ReferenceRecipeJson)!.AsObject();

        Assert.AreEqual(
            snapshot.TemplateCatalogVersion,
            reference["metadata"]?["templateCatalogVersion"]?.GetValue<string>(),
            "A catalog re-export must be followed into the reference recipe the prompt teaches.");
        CollectionAssert.Contains(snapshot.BuildableDimensions.ToList(), reference["dimension"]?.GetValue<string>());
        CollectionAssert.Contains(snapshot.BuildableArchetypes.ToList(), reference["archetype"]?.GetValue<string>());
    }

    [TestMethod]
    public void TheExportedCanonicalExampleStillViolatesTheStrictStructureBudget()
    {
        // The reason this task exists: the exported canonical example is legacy-exempt, so it must never return
        // to the prompt. If a future re-export makes it strict-compliant, revisit which example the prompt shows.
        var violations = StrictStructureViolations(RecipeTemplateCatalogSnapshot.Default.CanonicalExampleJson);

        Assert.IsTrue(violations.Any(static violation => violation.Contains("attachTo", StringComparison.Ordinal)));
        Assert.IsTrue(violations.Any(static violation => violation.Contains("at most two", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void TheSystemPromptStatesTheStrictBudgetRedLines()
    {
        var prompt = RecipePromptTemplate.SystemPrompt;

        foreach (var redLine in new[]
        {
            "All three stage roots must be present",
            "at most two modules in total across all three stages",
            "\"modules\": []",
            "Never emit attachTo",
            "inside the inclusive [min, max] of the table below",
        })
        {
            StringAssert.Contains(prompt, redLine);
        }
    }

    [TestMethod]
    public void TheSystemPromptDropsTheMisleadingThreeStageSentenceAndTheLegacyExample()
    {
        var prompt = RecipePromptTemplate.SystemPrompt;

        Assert.IsFalse(prompt.Contains("use exactly three stages", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(prompt.Contains("fireball", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(prompt.Contains("\"attachTo\"", StringComparison.Ordinal));
        StringAssert.Contains(prompt, RecipePromptTemplate.ReferenceRecipeJson);
    }

    [TestMethod]
    public void TheSystemPromptIsDeterministicAndStaysInsideTheMessageBound()
    {
        var first = RecipePromptTemplate.CreateInitialMessages("a short blue spark bolt");
        var second = RecipePromptTemplate.CreateInitialMessages("a short blue spark bolt");

        CollectionAssert.AreEqual(
            first.Select(static message => message.Role.ToString() + "|" + message.Content).ToArray(),
            second.Select(static message => message.Role.ToString() + "|" + message.Content).ToArray());
        Assert.IsTrue(
            first[0].Content.Length <= MaximumMessageCharacters,
            "The system prompt is " + first[0].Content.Length.ToString(CultureInfo.InvariantCulture) + " characters.");
    }

    /// <summary>
    /// Minimal structure check against the strict simple-profile budget: the three fixed stage roots, at most
    /// <see cref="MaximumModules"/> modules with at least one empty stage, no attachTo, and every module bound to a
    /// snapshot template with its declared parameter set inside the committed bounds.
    /// </summary>
    private static List<string> StrictStructureViolations(string recipeJson)
    {
        var violations = new List<string>();
        var recipe = JsonNode.Parse(recipeJson)!.AsObject();
        var dimension = recipe["dimension"]?.GetValue<string>();
        var templates = RecipeTemplateCatalogSnapshot.Default.Templates
            .ToDictionary(static template => template.TemplateId, StringComparer.Ordinal);

        var stages = recipe["stages"]?.AsArray();
        if (stages is null || stages.Count != RequiredStageRoots.Length)
        {
            violations.Add("the recipe must declare exactly the three stage roots launch, travel, impact");
            return violations;
        }

        for (var index = 0; index < RequiredStageRoots.Length; index++)
        {
            var stage = stages[index]!.AsObject();
            var (expectedId, expectedTrigger) = RequiredStageRoots[index];
            var id = stage["id"]?.GetValue<string>();
            if (!string.Equals(id, expectedId, StringComparison.Ordinal))
            {
                violations.Add("stage " + index.ToString(CultureInfo.InvariantCulture) + " must be the '" + expectedId + "' root");
            }

            if (!string.Equals(stage["trigger"]?.GetValue<string>(), expectedTrigger, StringComparison.Ordinal))
            {
                violations.Add("stage '" + expectedId + "' must use the trigger '" + expectedTrigger + "'");
            }
        }

        var moduleCount = 0;
        var emptyStages = 0;
        foreach (var stageNode in stages)
        {
            var modules = stageNode!.AsObject()["modules"]?.AsArray();
            if (modules is null)
            {
                violations.Add("every stage must declare a modules array");
                continue;
            }

            if (modules.Count == 0)
            {
                emptyStages++;
            }

            foreach (var moduleNode in modules)
            {
                moduleCount++;
                CheckModule(moduleNode!.AsObject(), dimension, templates, violations);
            }
        }

        if (moduleCount > MaximumModules)
        {
            violations.Add(
                "the recipe declares " + moduleCount.ToString(CultureInfo.InvariantCulture) +
                " modules; the strict budget allows at most two");
        }

        if (emptyStages == 0)
        {
            violations.Add("at least one stage must declare an empty modules array");
        }

        return violations;
    }

    private static void CheckModule(
        JsonObject module,
        string? dimension,
        Dictionary<string, RecipeTemplateCatalogSnapshot.TemplateSnapshot> templates,
        List<string> violations)
    {
        var moduleId = module["id"]?.GetValue<string>() ?? "<unnamed>";
        if (module.ContainsKey("attachTo"))
        {
            violations.Add("module '" + moduleId + "' uses attachTo");
        }

        var templateId = module["templateId"]?.GetValue<string>();
        if (templateId is null || !templates.TryGetValue(templateId, out var template))
        {
            violations.Add("module '" + moduleId + "' names a templateId that the catalog snapshot does not declare");
            return;
        }

        if (!string.Equals(module["kind"]?.GetValue<string>(), template.Kind, StringComparison.Ordinal))
        {
            violations.Add("module '" + moduleId + "' does not carry the template kind '" + template.Kind + "'");
        }

        if (!string.Equals(template.Dimension, dimension, StringComparison.Ordinal))
        {
            violations.Add("module '" + moduleId + "' uses a template of another dimension");
        }

        var parameters = module["parameters"]?.AsObject();
        if (parameters is null)
        {
            violations.Add("module '" + moduleId + "' must declare a parameters object");
            return;
        }

        var declared = template.Parameters.Select(static parameter => parameter.Name).ToHashSet(StringComparer.Ordinal);
        foreach (var name in parameters.Select(static parameter => parameter.Key).Where(name => !declared.Contains(name)))
        {
            violations.Add("module '" + moduleId + "' declares the undeclared parameter '" + name + "'");
        }

        foreach (var parameter in template.Parameters)
        {
            if (!parameters.TryGetPropertyValue(parameter.Name, out var valueNode) ||
                valueNode is not JsonValue numeric ||
                !numeric.TryGetValue<double>(out var value))
            {
                violations.Add("module '" + moduleId + "' must declare the numeric parameter '" + parameter.Name + "'");
                continue;
            }

            var minimum = double.Parse(parameter.MinLiteral, CultureInfo.InvariantCulture);
            var maximum = double.Parse(parameter.MaxLiteral, CultureInfo.InvariantCulture);
            if (value < minimum || value > maximum)
            {
                violations.Add("module '" + moduleId + "' puts '" + parameter.Name + "' outside its committed bounds");
            }

            if (string.Equals(parameter.Type, "integer", StringComparison.Ordinal) && value != Math.Floor(value))
            {
                violations.Add("module '" + moduleId + "' must keep '" + parameter.Name + "' integral");
            }
        }
    }
}

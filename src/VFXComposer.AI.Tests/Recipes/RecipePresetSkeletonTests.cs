using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VFXComposer.AI.Contracts.Recipes;
using VFXComposer.AI.Providers.Recipes;

namespace VFXComposer.AI.Tests.Recipes;

/// <summary>
/// Build-time compliance for the committed simple-mode preset skeletons (REQ-004-02): every card's skeleton
/// must clear L1, produce zero L1.5 findings and keep the strict structure the F8-0 prompt red line teaches.
/// A card click is a zero-AI action, so a defective skeleton would put an unbuildable draft in front of the
/// user with no repair loop — these tests make that impossible to commit.
/// </summary>
[TestClass]
public sealed class RecipePresetSkeletonTests
{
    private const int MaximumModules = RecipeCatalogPrevalidator.MaximumModules;

    private static readonly (string Id, string Trigger)[] RequiredStageRoots =
    [
        ("launch", "on_launch"),
        ("travel", "after_previous"),
        ("impact", "on_hit"),
    ];

    [TestMethod]
    public void TheCardCountStaysInsideTheRequiredFourToSixRange()
    {
        Assert.IsTrue(
            RecipePresetSkeletons.All.Count is >= 4 and <= 6,
            "REQ-004-02 fixes the simple-mode card count between four and six.");
    }

    [TestMethod]
    public void PresetIdentitiesAndDescriptionsAreUniqueAndWellFormed()
    {
        var presets = RecipePresetSkeletons.All;

        Assert.AreEqual(
            presets.Count,
            presets.Select(static preset => preset.PresetId).Distinct(StringComparer.Ordinal).Count(),
            "Preset ids key the Desktop copy catalog and must be unique.");
        Assert.AreEqual(
            presets.Count,
            presets.Select(static preset => preset.RecipeId).Distinct(StringComparer.Ordinal).Count(),
            "Recipe ids become distinct build outputs and must be unique.");

        foreach (var preset in presets)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(preset.EnglishDescription), preset.PresetId + " has no description.");
            Assert.IsTrue(
                preset.EnglishDescription.All(static character => character < 128),
                preset.PresetId + " must keep its lineage-origin description language-neutral English.");
            Assert.IsTrue(preset.TemplateIds.Count > 0, preset.PresetId + " exercises no template.");
        }
    }

    [TestMethod]
    public void EverySkeletonPassesL1Validation()
    {
        foreach (var preset in RecipePresetSkeletons.All)
        {
            var issues = RecipeL1Validator.Validate(preset.RecipeJson);
            Assert.AreEqual(
                0,
                issues.Count,
                preset.PresetId + ": " + string.Join("; ", issues.Select(static issue => issue.Code + " " + issue.Path)));
        }
    }

    [TestMethod]
    public void EverySkeletonPassesCatalogPrevalidationWithZeroFindings()
    {
        foreach (var preset in RecipePresetSkeletons.All)
        {
            var issues = RecipeCatalogPrevalidator.Prevalidate(preset.RecipeJson);
            Assert.AreEqual(
                0,
                issues.Count,
                preset.PresetId + ": " + string.Join("; ", issues.Select(static issue => issue.Code + " " + issue.Path)));
        }
    }

    [TestMethod]
    public void EverySkeletonKeepsTheStrictRedLineStructure()
    {
        foreach (var preset in RecipePresetSkeletons.All)
        {
            var recipe = JsonNode.Parse(preset.RecipeJson)!.AsObject();
            var stages = recipe["stages"]!.AsArray();
            Assert.AreEqual(RequiredStageRoots.Length, stages.Count, preset.PresetId);

            var moduleCount = 0;
            var emptyStages = 0;
            for (var index = 0; index < RequiredStageRoots.Length; index++)
            {
                var stage = stages[index]!.AsObject();
                Assert.AreEqual(RequiredStageRoots[index].Id, stage["id"]?.GetValue<string>(), preset.PresetId);
                Assert.AreEqual(RequiredStageRoots[index].Trigger, stage["trigger"]?.GetValue<string>(), preset.PresetId);

                var modules = stage["modules"]!.AsArray();
                if (modules.Count == 0)
                {
                    emptyStages++;
                }

                foreach (var module in modules)
                {
                    moduleCount++;
                    Assert.IsFalse(
                        module!.AsObject().ContainsKey("attachTo"),
                        preset.PresetId + " uses attachTo.");
                }
            }

            Assert.IsTrue(
                moduleCount is >= 1 and <= MaximumModules,
                preset.PresetId + " declares " + moduleCount.ToString(CultureInfo.InvariantCulture) + " modules.");
            Assert.IsTrue(emptyStages > 0, preset.PresetId + " must keep at least one stage empty.");
        }
    }

    [TestMethod]
    public void EverySkeletonAgreesWithTheCommittedCatalogSnapshot()
    {
        var snapshot = RecipeTemplateCatalogSnapshot.Default;
        foreach (var preset in RecipePresetSkeletons.All)
        {
            Assert.AreEqual(snapshot.TemplateCatalogVersion, preset.TemplateCatalogVersion, preset.PresetId);
            CollectionAssert.Contains(snapshot.BuildableArchetypes.ToList(), preset.Archetype, preset.PresetId);
            CollectionAssert.Contains(snapshot.BuildableDimensions.ToList(), preset.Dimension, preset.PresetId);
            foreach (var templateId in preset.TemplateIds)
            {
                Assert.IsTrue(
                    snapshot.TryGetTemplate(templateId, out _),
                    preset.PresetId + " references the undeclared template " + templateId + ".");
            }
        }
    }

    [TestMethod]
    public void TheCardSetCoversEveryCatalogTemplate()
    {
        // Six cards over six templates: every template the catalog can build appears on at least one card, so the
        // simple mode honestly demonstrates the whole current expressive range.
        var exercised = RecipePresetSkeletons.All
            .SelectMany(static preset => preset.TemplateIds)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var template in RecipeTemplateCatalogSnapshot.Default.Templates)
        {
            Assert.IsTrue(
                exercised.Contains(template.TemplateId),
                "No preset card exercises " + template.TemplateId + ".");
        }
    }

    [TestMethod]
    public void CreateDraftRecordProducesAFreshPendingDraftBoundToThePrecomputedHash()
    {
        var preset = RecipePresetSkeletons.All[0];
        var createdUtc = DateTimeOffset.UtcNow;

        var first = preset.CreateDraftRecord(createdUtc);
        var second = preset.CreateDraftRecord(createdUtc);

        Assert.AreNotEqual(first.DraftId, second.DraftId, "Every card click must create a new draft identity.");
        Assert.AreEqual(RecipeDraftStatus.PendingConfirmation, first.Status);
        Assert.AreEqual(preset.RecipeJson, first.RecipeJson);
        Assert.AreEqual(RecipeCanonicalJson.ComputeSha256(preset.RecipeJson), first.CanonicalSha256);
        Assert.AreEqual(preset.CanonicalSha256, first.CanonicalSha256);
        Assert.AreEqual(RecipePresetSkeleton.PresetPromptTemplateVersion, first.PromptTemplateVersion);
        Assert.AreEqual(preset.RecipeId, first.RecipeId);
        Assert.AreEqual(preset.Archetype, first.Archetype);
        Assert.AreEqual(preset.Dimension, first.Dimension);
        Assert.AreEqual(preset.TargetProfile, first.TargetProfile);
        Assert.AreEqual(0, first.RequestCount, "A preset draft consumes no request budget.");
        Assert.AreEqual(0, first.Issues.Count);
    }

    [TestMethod]
    public void SkeletonJsonIsCanonicalAndParameterValuesAreTheCatalogDefaults()
    {
        var snapshot = RecipeTemplateCatalogSnapshot.Default;
        foreach (var preset in RecipePresetSkeletons.All)
        {
            Assert.AreEqual(
                RecipeCanonicalJson.Canonicalize(preset.RecipeJson),
                preset.RecipeJson,
                preset.PresetId + " must store the canonical serialization it hashes.");

            using var document = JsonDocument.Parse(preset.RecipeJson);
            foreach (var stage in document.RootElement.GetProperty("stages").EnumerateArray())
            {
                foreach (var module in stage.GetProperty("modules").EnumerateArray())
                {
                    var templateId = module.GetProperty("templateId").GetString()!;
                    foreach (var parameter in module.GetProperty("parameters").EnumerateObject())
                    {
                        Assert.IsTrue(
                            snapshot.TryGetParameter(templateId, parameter.Name, out var declared),
                            preset.PresetId + " declares the unknown parameter " + parameter.Name + ".");
                        Assert.AreEqual(
                            declared!.Default,
                            parameter.Value.GetDouble(),
                            preset.PresetId + " must ship the committed default for " + parameter.Name + ".");
                    }
                }
            }
        }
    }
}

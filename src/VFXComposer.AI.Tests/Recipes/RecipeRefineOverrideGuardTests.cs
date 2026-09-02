using System.Globalization;
using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VFXComposer.AI.Contracts.Recipes;
using VFXComposer.AI.Providers.Recipes;

namespace VFXComposer.AI.Tests.Recipes;

/// <summary>
/// REQ-004 §9.3 guard semantics: AC-9 (an unnamed hand edit is restored, a named change stays), AC-10 (naming
/// releases the hand edit), Chinese aliases participate in naming (O-3), the newest human edit wins, numeric
/// equality is by parsed value, structural differences stay outside the guard domain, and the quadruple
/// determinism snapshot (REQ-004-50).
/// </summary>
[TestClass]
public sealed class RecipeRefineOverrideGuardTests
{
    private const string WidthPath = "stages[travel].modules[trail].parameters.width";

    // ---- fixtures: a two-module strict-budget recipe family (core + trail) ----

    /// <summary>The lineage root: core scale 1.2, trail width 0.42, trail time 0.22.</summary>
    private static string BaseRecipeJson => RecipeCanonicalJson.Canonicalize("""
        {
          "recipeVersion": 1,
          "revision": 1,
          "id": "guard_fixture_2d",
          "name": "Guard Fixture 2D",
          "dimension": "2d",
          "archetype": "projectile",
          "targetProfile": "mobile_medium",
          "randomSeed": 20260902,
          "stages": [
            { "id": "launch", "trigger": "on_launch", "duration": 0.1, "enabled": true, "modules": [] },
            { "id": "travel", "trigger": "after_previous", "duration": 1.0, "enabled": true, "modules": [
              { "id": "core", "kind": "energy_body", "templateId": "PFT_2D_FireCore", "parameters": { "scale": 1.2 }, "enabled": true },
              { "id": "trail", "kind": "motion_trail", "templateId": "PFT_2D_FireTrail", "parameters": { "time": 0.22, "width": 0.42 }, "enabled": true }
            ] },
            { "id": "impact", "trigger": "on_hit", "duration": 0.2, "enabled": true, "modules": [] }
          ],
          "metadata": { "createdBy": "vfxcomposer.ai", "templateCatalogVersion": "1.0.0" }
        }
        """);

    private static string WithParameter(string recipeJson, string moduleId, string parameterName, string literal)
    {
        var root = JsonNode.Parse(recipeJson)!.AsObject();
        foreach (var stage in root["stages"]!.AsArray())
        {
            foreach (var module in stage!["modules"]!.AsArray())
            {
                if (string.Equals(module!["id"]!.GetValue<string>(), moduleId, StringComparison.Ordinal))
                {
                    module["parameters"]!.AsObject()[parameterName] = JsonNode.Parse(literal);
                }
            }
        }

        return RecipeCanonicalJson.Canonicalize(root.ToJsonString());
    }

    private static RecipeDraftRecord Version(
        string draftId,
        string recipeJson,
        RecipeDraftOrigin origin,
        string? parentDraftId,
        int ordinal)
    {
        return new RecipeDraftRecord(
            draftId,
            RecipeDraftStatus.PendingConfirmation,
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch,
            "corr-guard-tests",
            RecipeDraftTestData.PromptVersion,
            RecipeDraftTestData.CatalogVersion,
            recipeJson,
            RecipeCanonicalJson.ComputeSha256(recipeJson),
            "guard_fixture_2d",
            "projectile",
            "2d",
            "mobile_medium",
            Array.Empty<RecipeValidationIssue>(),
            requestCount: origin == RecipeDraftOrigin.HumanEdit ? 0 : 1,
            new RecipeDraftProvenance(
                "lineage-guard-fixture",
                parentDraftId,
                ordinal,
                origin,
                origin == RecipeDraftOrigin.AiRefine ? "synthetic earlier feedback" : null));
    }

    /// <summary>v1 ai_draft (base) → v2 human_edit (width 0.42 → 0.20); the chain runs head-first.</summary>
    private static List<RecipeDraftRecord> HandTunedChain()
    {
        var v1 = Version("draft-v1", BaseRecipeJson, RecipeDraftOrigin.AiDraft, parentDraftId: null, ordinal: 1);
        var v2Json = WithParameter(BaseRecipeJson, "trail", "width", "0.20");
        var v2 = Version("draft-v2", v2Json, RecipeDraftOrigin.HumanEdit, v1.DraftId, ordinal: 2);
        return [v2, v1];
    }

    /// <summary>The AI writes width back to 0.42 and raises scale 1.2 → 1.8.</summary>
    private static string AiOutputJson()
    {
        var json = WithParameter(HandTunedChain()[0].RecipeJson, "trail", "width", "0.42");
        return WithParameter(json, "core", "scale", "1.8");
    }

    // ---- AC-9: the unnamed hand edit is restored, the named-direction change stays ----

    [TestMethod]
    public void UnnamedHandTunedWidthIsRestoredWhileTheNamedScaleChangeStays()
    {
        var outcome = RecipeRefineOverrideGuard.Apply(
            HandTunedChain(),
            AiOutputJson(),
            "make the fire core bigger",
            RecipeRefineKnowledge.Default);

        Assert.AreEqual(1, outcome.Restorations.Count, "Exactly the width restoration.");
        var restoration = outcome.Restorations[0];
        Assert.AreEqual(WidthPath, restoration.ParameterPath);
        Assert.AreEqual("draft-v2", restoration.SourceDraftId);
        Assert.AreEqual("0.42", restoration.AiValueLiteral);
        Assert.AreEqual("0.2", restoration.RestoredValueLiteral, "Literals come from the canonical hand-edited document.");

        var guarded = JsonNode.Parse(outcome.GuardedRecipeJson)!.AsObject();
        Assert.AreEqual(0.20, ReadParameter(guarded, "trail", "width"), 1e-12, "width returns to the hand-tuned value.");
        Assert.AreEqual(1.8, ReadParameter(guarded, "core", "scale"), 1e-12, "scale keeps the AI value.");

        var contractList = outcome.ToGuardRestorations();
        Assert.AreEqual(1, contractList.Count);
        Assert.AreEqual(WidthPath, contractList[0].ParameterPath);
        Assert.AreEqual("draft-v2", contractList[0].SourceDraftId);
    }

    // ---- AC-10: naming the trail releases the hand edit ----

    [TestMethod]
    public void NamingTheTrailKeepsTheAiWidthAndProducesNoRestoration()
    {
        var outcome = RecipeRefineOverrideGuard.Apply(
            HandTunedChain(),
            AiOutputJson(),
            "shorten the trail and make it thinner",
            RecipeRefineKnowledge.Default);

        Assert.AreEqual(0, outcome.Restorations.Count);
        var guarded = JsonNode.Parse(outcome.GuardedRecipeJson)!.AsObject();
        Assert.AreEqual(0.42, ReadParameter(guarded, "trail", "width"), 1e-12);
        Assert.AreEqual(1.8, ReadParameter(guarded, "core", "scale"), 1e-12);
    }

    [TestMethod]
    public void ChineseFeedbackNamingTheTrailAlsoReleasesTheHandEdit()
    {
        // O-3: aliasesZh participate in the deterministic naming match against verbatim feedback.
        var outcome = RecipeRefineOverrideGuard.Apply(
            HandTunedChain(),
            AiOutputJson(),
            "把拖尾改细一点",
            RecipeRefineKnowledge.Default);

        Assert.AreEqual(0, outcome.Restorations.Count);
    }

    [TestMethod]
    public void ChineseFeedbackNotNamingTheTrailStillRestoresTheHandEdit()
    {
        var outcome = RecipeRefineOverrideGuard.Apply(
            HandTunedChain(),
            AiOutputJson(),
            "火核再大一点",
            RecipeRefineKnowledge.Default);

        Assert.AreEqual(1, outcome.Restorations.Count);
        Assert.AreEqual(WidthPath, outcome.Restorations[0].ParameterPath);
    }

    [TestMethod]
    public void EnglishAliasMatchingIsTokenBasedNotSubstringBased()
    {
        // "detail" contains "tail" as a substring; token matching must not treat that as naming the trail.
        var outcome = RecipeRefineOverrideGuard.Apply(
            HandTunedChain(),
            AiOutputJson(),
            "add more detail to the fire core",
            RecipeRefineKnowledge.Default);

        Assert.AreEqual(1, outcome.Restorations.Count, "The 'detail' substring must not count as the 'tail' alias.");
    }

    // ---- the newest human edit wins ----

    [TestMethod]
    public void ALaterHumanEditOverridesTheOlderValueAndTheGuardUsesTheNewest()
    {
        var v1 = Version("draft-v1", BaseRecipeJson, RecipeDraftOrigin.AiDraft, parentDraftId: null, ordinal: 1);
        var v2Json = WithParameter(BaseRecipeJson, "trail", "width", "0.20");
        var v2 = Version("draft-v2", v2Json, RecipeDraftOrigin.HumanEdit, v1.DraftId, ordinal: 2);
        var v3Json = WithParameter(v2Json, "trail", "width", "0.30");
        var v3 = Version("draft-v3", v3Json, RecipeDraftOrigin.HumanEdit, v2.DraftId, ordinal: 3);
        var chain = new List<RecipeDraftRecord> { v3, v2, v1 };
        var aiOutput = WithParameter(v3Json, "trail", "width", "0.42");

        var outcome = RecipeRefineOverrideGuard.Apply(
            chain,
            aiOutput,
            "make the fire core bigger",
            RecipeRefineKnowledge.Default);

        Assert.AreEqual(1, outcome.Restorations.Count);
        Assert.AreEqual("draft-v3", outcome.Restorations[0].SourceDraftId, "The newest human edit owns the value.");
        Assert.AreEqual("0.3", outcome.Restorations[0].RestoredValueLiteral);
        Assert.AreEqual(
            0.30,
            ReadParameter(JsonNode.Parse(outcome.GuardedRecipeJson)!.AsObject(), "trail", "width"),
            1e-12);
    }

    // ---- numeric equality: 0.20 vs 0.2 is the same setting; no restoration, no list entry ----

    [TestMethod]
    public void AnAiValueNumericallyEqualToTheHandEditProducesNoRestoration()
    {
        var aiOutput = WithParameter(HandTunedChain()[0].RecipeJson, "trail", "width", "0.2000");

        var outcome = RecipeRefineOverrideGuard.Apply(
            HandTunedChain(),
            aiOutput,
            "make the fire core bigger",
            RecipeRefineKnowledge.Default);

        Assert.AreEqual(0, outcome.Restorations.Count, "1.2-versus-1.20 style differences are not a changed value.");
    }

    // ---- structural differences are outside the guard domain ----

    [TestMethod]
    public void ARemovedModuleIsAStructuralDifferenceAndIsNotGuarded()
    {
        var root = JsonNode.Parse(AiOutputJson())!.AsObject();
        foreach (var stage in root["stages"]!.AsArray())
        {
            var modules = stage!["modules"]!.AsArray();
            for (var index = modules.Count - 1; index >= 0; index--)
            {
                if (string.Equals(modules[index]!["id"]!.GetValue<string>(), "trail", StringComparison.Ordinal))
                {
                    modules.RemoveAt(index);
                }
            }
        }

        var aiOutput = RecipeCanonicalJson.Canonicalize(root.ToJsonString());

        var outcome = RecipeRefineOverrideGuard.Apply(
            HandTunedChain(),
            aiOutput,
            "make the fire core bigger",
            RecipeRefineKnowledge.Default);

        Assert.AreEqual(0, outcome.Restorations.Count, "A deleted module is the AI's structural call, not a guarded scalar.");
        Assert.AreEqual(aiOutput, outcome.GuardedRecipeJson, "The document passes through canonical and untouched.");
    }

    [TestMethod]
    public void AChangedTemplateIdIsAStructuralDifferenceAndIsNotGuarded()
    {
        var root = JsonNode.Parse(AiOutputJson())!.AsObject();
        foreach (var stage in root["stages"]!.AsArray())
        {
            foreach (var module in stage!["modules"]!.AsArray())
            {
                if (string.Equals(module!["id"]!.GetValue<string>(), "trail", StringComparison.Ordinal))
                {
                    module["templateId"] = "PFT_2D_Embers";
                    module["kind"] = "secondary_particles";
                    module["parameters"] = new JsonObject { ["rate"] = 18, ["lifetime"] = 0.55 };
                }
            }
        }

        var aiOutput = RecipeCanonicalJson.Canonicalize(root.ToJsonString());

        var outcome = RecipeRefineOverrideGuard.Apply(
            HandTunedChain(),
            aiOutput,
            "make the fire core bigger",
            RecipeRefineKnowledge.Default);

        Assert.AreEqual(0, outcome.Restorations.Count);
    }

    // ---- no human edit in the chain: nothing to protect ----

    [TestMethod]
    public void AChainWithoutHumanEditsRestoresNothing()
    {
        var v1 = Version("draft-v1", BaseRecipeJson, RecipeDraftOrigin.AiDraft, parentDraftId: null, ordinal: 1);

        var outcome = RecipeRefineOverrideGuard.Apply(
            [v1],
            AiOutputJson(),
            "make the fire core bigger",
            RecipeRefineKnowledge.Default);

        Assert.AreEqual(0, outcome.Restorations.Count);
    }

    // ---- determinism: the same quadruple always produces the same restoration list (REQ-004-50) ----

    [TestMethod]
    public void TheSameQuadrupleProducesTheSameRestorationListAndDocument()
    {
        string Run()
        {
            var outcome = RecipeRefineOverrideGuard.Apply(
                HandTunedChain(),
                AiOutputJson(),
                "make the fire core bigger",
                RecipeRefineKnowledge.Default);
            return string.Join(
                ";",
                outcome.Restorations.Select(static restoration =>
                    restoration.ParameterPath + "|" + restoration.SourceDraftId + "|" +
                    restoration.AiValueLiteral + "|" + restoration.RestoredValueLiteral)) +
                "#" + RecipeCanonicalJson.ComputeSha256(outcome.GuardedRecipeJson);
        }

        var first = Run();
        var second = Run();

        Assert.AreEqual(first, second);
        StringAssert.StartsWith(
            first,
            WidthPath + "|draft-v2|0.42|0.2#",
            "The restoration snapshot for the fixed quadruple is itself pinned.");
    }

    [TestMethod]
    public void TheGuardedDocumentIsCanonicalSoOneShapeIsHashedAndPersisted()
    {
        var outcome = RecipeRefineOverrideGuard.Apply(
            HandTunedChain(),
            AiOutputJson(),
            "make the fire core bigger",
            RecipeRefineKnowledge.Default);

        Assert.AreEqual(RecipeCanonicalJson.Canonicalize(outcome.GuardedRecipeJson), outcome.GuardedRecipeJson);
    }

    // ---- rejection paths ----

    [TestMethod]
    public void AMalformedChainOrMissingInputIsRejected()
    {
        var chain = HandTunedChain();
        var broken = new List<RecipeDraftRecord> { chain[1], chain[0] }; // reversed: parents no longer link.

        Assert.ThrowsExactly<ArgumentException>(() =>
            RecipeRefineOverrideGuard.Apply(broken, AiOutputJson(), "feedback", RecipeRefineKnowledge.Default));
        Assert.ThrowsExactly<ArgumentException>(() =>
            RecipeRefineOverrideGuard.Apply([], AiOutputJson(), "feedback", RecipeRefineKnowledge.Default));
        Assert.ThrowsExactly<ArgumentException>(() =>
            RecipeRefineOverrideGuard.Apply(chain, " ", "feedback", RecipeRefineKnowledge.Default));
        Assert.ThrowsExactly<ArgumentException>(() =>
            RecipeRefineOverrideGuard.Apply(chain, AiOutputJson(), " ", RecipeRefineKnowledge.Default));
    }

    private static double ReadParameter(JsonObject recipe, string moduleId, string parameterName)
    {
        foreach (var stage in recipe["stages"]!.AsArray())
        {
            foreach (var module in stage!["modules"]!.AsArray())
            {
                if (string.Equals(module!["id"]!.GetValue<string>(), moduleId, StringComparison.Ordinal))
                {
                    return double.Parse(
                        module["parameters"]![parameterName]!.ToJsonString(),
                        CultureInfo.InvariantCulture);
                }
            }
        }

        throw new AssertFailedException("The module '" + moduleId + "' is missing from the guarded document.");
    }
}

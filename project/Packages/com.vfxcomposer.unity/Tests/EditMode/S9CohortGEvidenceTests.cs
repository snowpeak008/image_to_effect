using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using VFXComposer.Editor.Build;
using VFXComposer.Editor.Domain;
using VFXComposer.Editor.Patch;
using VFXComposer.Editor.Workflow;

namespace VFXComposer.Tests.EditMode
{
    // This test is deliberately strict. Before dispatch it fails for missing G artifacts; do not count that
    // expected absence as a product regression. Run it only after the fixed 5 Recipe + 3 Patch batch is complete.
    [Explicit("Historical Cohort G evidence is invalid/noncountable and is retained only for manual audit.")]
    public sealed class S9CohortGEvidenceTests
    {
        private static readonly string[] RecipeKeys = { "G1", "G2", "G3", "G4", "G5" };
        private static readonly string[] PatchKeys = { "P1", "P2", "P3" };
        private const string TemporaryRecipe = "Assets/VFX/Recipes/s9_cohort_g_patch_base.json";

        [Test]
        public void CohortG_HasFrozenPreregistrationAndContinuousRawEvidenceChains()
        {
            string hash;
            Assert.That(VfxAiWorkflowContractSnapshot.VerifyExisting("cohort-g", out hash), Is.True, "The G contract snapshot must exist and verify its registered SHA-256.");
            Assert.That(hash, Does.Match("^[0-9A-F]{64}$"));
            var snapshot = Normalize(File.ReadAllText(Evidence("contract-snapshot.md")));
            var repositoryRoot = Directory.GetParent(Directory.GetParent(Application.dataPath).FullName).FullName;
            var canonicalPath = Path.Combine(repositoryRoot, VfxAiWorkflowExporter.CanonicalRecipeRelativePath.Replace('/', Path.DirectorySeparatorChar));
            var canonical = Normalize(File.ReadAllText(canonicalPath));
            StringAssert.Contains("<!-- BEGIN canonical-recipe.generated.json -->\n" + canonical + "\n<!-- END canonical-recipe.generated.json -->", snapshot, "The frozen contract must contain the complete canonical Recipe provided to G agents.");
            var spec = JObject.Parse(File.ReadAllText(Evidence("acceptance-spec.json")));
            CollectionAssert.AreEquivalent(RecipeKeys, ((JObject)spec["recipes"]).Properties().Select(value => value.Name));
            CollectionAssert.AreEquivalent(PatchKeys, ((JObject)spec["patches"]).Properties().Select(value => value.Name));
            foreach (var key in RecipeKeys) AssertChain(key, "recipe.json");
            foreach (var key in PatchKeys) AssertChain(key, "patch.json");
        }

        [Test]
        public void CohortG_RecipesMeetRealBuildAndFrozenSemanticThreshold()
        {
            var successes = 0;
            var outcomes = new JArray();
            foreach (var key in RecipeKeys)
            {
                var final = Evidence(key + ".final.recipe.json");
                var succeeded = false; var detail = string.Empty; VfxBuildResult build = null; JObject recipe = null;
                try
                {
                    recipe = JObject.Parse(File.ReadAllText(final));
                    build = new VfxCompiler().Build(recipe.ToString());
                    succeeded = build.Succeeded && SatisfiesRecipeSpec(key, recipe);
                    detail = build.Succeeded ? (succeeded ? "build and semantic assertion passed" : "build passed but frozen semantic assertion failed") : Describe(build.Plan.Report);
                }
                catch (Exception exception) { detail = exception.ToString(); }
                finally { if (build != null && build.Succeeded && recipe != null) AssetDatabase.DeleteAsset(VfxCompiler.OutputFolder(VfxDomainParser.ParseRecipe(recipe.ToString()).Value)); }
                if (succeeded) successes++;
                outcomes.Add(new JObject { ["key"] = key, ["succeeded"] = succeeded, ["detail"] = detail });
            }
            VfxCohortGProtocol.WriteReportOnce(Evidence("recipe-results.generated.json"), new JObject { ["successes"] = successes, ["total"] = RecipeKeys.Length, ["outcomes"] = outcomes }.ToString(Newtonsoft.Json.Formatting.Indented).Replace("\r\n", "\n") + "\n");
            Assert.That(successes, Is.GreaterThanOrEqualTo(4), "M6 requires at least four of five fixed Cohort G Recipe successes.");
            AssertGeneratedClean();
        }

        [Test]
        public void CohortG_PatchesApplyWithRevisionTwoHistoryAndFrozenEffects()
        {
            foreach (var key in PatchKeys)
            {
                try
                {
                    File.WriteAllText(Absolute(TemporaryRecipe), File.ReadAllText(Absolute(VfxAiWorkflowExporter.FormalDefaultRecipePath)).Replace("\"id\": \"fireball_2d\"", "\"id\": \"s9_cohort_g_patch_base\""));
                    AssetDatabase.ImportAsset(TemporaryRecipe, ImportAssetOptions.ForceUpdate);
                    var result = new VfxPatchService().ApplyToAsset(TemporaryRecipe, File.ReadAllText(Evidence(key + ".final.patch.json")), 1);
                    Assert.That(result.IsValid && result.AfterRevision == 2, Is.True, key + ": " + Describe(result.Report));
                    var after = JObject.Parse(File.ReadAllText(Absolute(TemporaryRecipe)));
                    Assert.That(SatisfiesPatchSpec(key, after), Is.True, key + " effect assertion failed.");
                    var history = JArray.Parse(File.ReadAllText(Absolute(TemporaryRecipe + VfxPatchService.HistorySuffix)));
                    var last = (JObject)history.Last;
                    Assert.That((int)last["beforeRevision"], Is.EqualTo(1)); Assert.That((int)last["afterRevision"], Is.EqualTo(2));
                }
                finally { CleanupPatchAssets(); }
            }
            AssertGeneratedClean();
        }

        [Test]
        public void CohortG_ReportWriter_IsWriteOnceWithExplicitNewlineNormalization()
        {
            var path = Path.Combine(Path.GetTempPath(), "vfx-cohort-g-report-" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                VfxCohortGProtocol.WriteReportOnce(path, "{\r\n  \"entries\": []\r\n}\r\n");
                var initial = File.ReadAllBytes(path);
                Assert.DoesNotThrow(() => VfxCohortGProtocol.WriteReportOnce(path, "{\n  \"entries\": []\n}\n"));
                CollectionAssert.AreEqual(initial, File.ReadAllBytes(path), "Equivalent normalized text must not rewrite an existing raw report.");
                Assert.Throws<InvalidOperationException>(() => VfxCohortGProtocol.WriteReportOnce(path, "{\"entries\":[{\"code\":\"E999\"}]}\n"));
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }

        private static void AssertChain(string key, string extension)
        {
            var attempts = Enumerable.Range(0, 3).Where(number => File.Exists(Evidence(key + ".attempt" + number + "." + extension))).ToArray();
            Assert.That(attempts, Is.Not.Empty, key + " needs an immediate persisted attempt0.");
            CollectionAssert.AreEqual(Enumerable.Range(0, attempts.Length).ToArray(), attempts, key + " chain has a gap.");
            Assert.That(File.Exists(Evidence(key + ".attempt3." + extension)), Is.False, key + " exceeds the two-repair limit.");
            var glob = Directory.GetFiles(VfxCohortGProtocol.EvidenceDirectory(), key + ".attempt*." + extension, SearchOption.TopDirectoryOnly).Select(Path.GetFileName).OrderBy(value => value, StringComparer.Ordinal).ToArray();
            CollectionAssert.AreEqual(attempts.Select(number => key + ".attempt" + number + "." + extension).OrderBy(value => value, StringComparer.Ordinal).ToArray(), glob, key + " has unexpected or empty attempt glob results.");
            foreach (var attempt in attempts)
            {
                var report = Evidence(key + ".attempt" + attempt + ".report.json"); Assert.That(File.Exists(report), Is.True, key + " missing machine report for attempt " + attempt); AssertReportShape(report);
                if (attempt == 0) continue;
                var prompt = Evidence(key + ".repair" + attempt + ".prompt.md"); Assert.That(File.Exists(prompt), Is.True, key + " missing persisted repair prompt.");
                var previous = Normalize(File.ReadAllText(Evidence(key + ".attempt" + (attempt - 1) + ".report.json")));
                var repairText = Normalize(File.ReadAllText(prompt));
                StringAssert.Contains(previous, repairText, key + " repair prompt must contain the complete prior report verbatim after newline normalization.");
                StringAssert.Contains(Normalize(File.ReadAllText(Evidence(key + ".attempt" + (attempt - 1) + "." + extension))), repairText, key + " repair prompt must contain the complete prior artifact.");
                StringAssert.Contains(Normalize(File.ReadAllText(Evidence("contract-snapshot.md"))), repairText, key + " repair prompt must contain the frozen contract snapshot.");
                StringAssert.Contains("## " + key, repairText, key + " repair prompt must retain the original preregistered request.");
            }
            var final = Evidence(key + ".final." + extension); Assert.That(File.Exists(final), Is.True, key + " missing final artifact.");
            CollectionAssert.AreEqual(File.ReadAllBytes(Evidence(key + ".attempt" + attempts.Last() + "." + extension)), File.ReadAllBytes(final), key + " final must be byte-identical to its last attempt.");
        }

        private static void AssertReportShape(string path)
        {
            var report = JObject.Parse(File.ReadAllText(path));
            foreach (var field in new[] { "succeeded", "detail", "entries" }) Assert.That(report.ContainsKey(field), Is.True, path + " lacks " + field);
            foreach (var entry in (JArray)report["entries"]) foreach (var field in new[] { "code", "severity", "path", "message", "actualValue", "allowedRange" }) Assert.That(((JObject)entry).ContainsKey(field), Is.True, path + " entry lacks " + field);
        }

        private static bool SatisfiesRecipeSpec(string key, JObject recipe)
        {
            var spec = (JObject)JObject.Parse(File.ReadAllText(Evidence("acceptance-spec.json")))["recipes"][key];
            if ((string)recipe["id"] != (string)spec["id"] || (string)recipe["targetProfile"] != (string)spec["profile"]) return false;
            var stages = recipe["stages"] == null ? new List<JObject>() : recipe["stages"].Children<JObject>().ToList();
            if (!Stage(stages, "launch", "on_launch") || !Stage(stages, "travel", "after_previous") || !Stage(stages, "impact", "on_hit")) return false;
            foreach (var kind in spec["travel"].Values<string>()) if (Module(stages, "travel", kind) == null) return false;
            foreach (var kind in spec["forbidTravel"] == null ? Enumerable.Empty<string>() : spec["forbidTravel"].Values<string>()) if (Module(stages, "travel", kind) != null) return false;
            foreach (var pair in ((JObject)spec["compare"]).Properties()) if (!Compare(Parameter(stages, pair.Name), Default(pair.Name), (string)pair.Value)) return false;
            return Module(stages, "impact", "impact_burst") != null && Module(stages, "impact", "shockwave") != null;
        }
        private static bool SatisfiesPatchSpec(string key, JObject recipe)
        {
            var modules = recipe["stages"].Children<JObject>().Single(stage => (string)stage["id"] == "travel")["modules"].Children<JObject>().ToList();
            if (key == "P1") return (double)modules.Single(module => (string)module["id"] == "embers")["parameters"]["rate"] == 9;
            if (key == "P2") return !(bool)modules.Single(module => (string)module["id"] == "embers")["enabled"];
            var added = modules.SingleOrDefault(module => (string)module["id"] == "lighter_embers"); return added != null && (string)added["kind"] == "secondary_particles" && (string)added["templateId"] == "PFT_2D_Embers" && (string)added["attachTo"] == "core";
        }
        private static bool Stage(List<JObject> stages, string id, string trigger) { var stage = stages.SingleOrDefault(value => (string)value["id"] == id); return stage != null && (string)stage["trigger"] == trigger; }
        private static JObject Module(List<JObject> stages, string stage, string kind) { var value = stages.SingleOrDefault(item => (string)item["id"] == stage); return value == null ? null : value["modules"].Children<JObject>().SingleOrDefault(item => (string)item["kind"] == kind); }
        private static double Parameter(List<JObject> stages, string key) { var part = key.Split('.'); var module = Module(stages, part[0] == "core" || part[0] == "trail" || part[0] == "embers" ? "travel" : "impact", part[0] == "core" ? "energy_body" : part[0] == "trail" ? "motion_trail" : part[0] == "embers" ? "secondary_particles" : part[0] == "burst" ? "impact_burst" : "shockwave"); return module == null ? double.NaN : (double)module["parameters"][part[1]]; }
        // Baselines are read from the frozen canonical document, never copied into a second C# default table.
        private static double Default(string key) { return Parameter(SnapshotCanonicalRecipe()["stages"].Children<JObject>().ToList(), key); }
        private static JObject SnapshotCanonicalRecipe()
        {
            var snapshot = Normalize(File.ReadAllText(Evidence("contract-snapshot.md")));
            const string begin = "<!-- BEGIN canonical-recipe.generated.json -->\n";
            const string end = "\n<!-- END canonical-recipe.generated.json -->";
            var start = snapshot.IndexOf(begin, StringComparison.Ordinal); Assert.That(start, Is.GreaterThanOrEqualTo(0), "Frozen canonical begin marker is missing."); start += begin.Length;
            var finish = snapshot.IndexOf(end, start, StringComparison.Ordinal); Assert.That(finish, Is.GreaterThanOrEqualTo(start), "Frozen canonical end marker is missing.");
            return JObject.Parse(snapshot.Substring(start, finish - start));
        }
        private static bool Compare(double value, double baseline, string comparison) { return comparison == "<" ? value < baseline : comparison == "<=" ? value <= baseline : comparison == ">" ? value > baseline : comparison == ">=" && value >= baseline; }
        private static string Evidence(string file) { return Path.Combine(VfxCohortGProtocol.EvidenceDirectory(), file); }
        private static string Absolute(string assetPath) { return Path.Combine(Application.dataPath, assetPath.Substring("Assets/".Length)); }
        private static string Normalize(string text) { return text.Replace("\r\n", "\n").Replace("\r", "\n"); }
        private static string Describe(ValidationReport report) { return string.Join(" | ", report.Entries.Select(entry => entry.Code + " " + entry.Path + " " + entry.Message)); }
        private static void CleanupPatchAssets() { if (AssetDatabase.IsValidFolder(VfxCompiler.GeneratedRoot + "/s9_cohort_g_patch_base")) AssetDatabase.DeleteAsset(VfxCompiler.GeneratedRoot + "/s9_cohort_g_patch_base"); if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(TemporaryRecipe) != null) AssetDatabase.DeleteAsset(TemporaryRecipe); var history = TemporaryRecipe + VfxPatchService.HistorySuffix; if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(history) != null) AssetDatabase.DeleteAsset(history); AssetDatabase.Refresh(); }
        private static void AssertGeneratedClean() { Assert.That(AssetDatabase.GetSubFolders(VfxCompiler.GeneratedRoot).Where(path => path != VfxCompiler.GeneratedRoot + "/fireball_2d"), Is.Empty); }
    }
}

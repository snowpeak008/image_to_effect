using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using VFXComposer.Editor.Build;
using VFXComposer.Editor.Patch;
using VFXComposer.Editor.Workflow;

namespace VFXComposer.Tests.EditMode
{
    [Ignore("Cohorts A-F are immutable historical evidence and are not part of the final G score.")]
    public sealed class S9AiWorkflowEvidenceTests
    {
        private const string TemporaryRecipe = "Assets/VFX/Recipes/s9_ai_workflow_evidence.json";

        [Test]
        public void CohortE_ContractSnapshot_IsGeneratedBeforeDispatch()
        {
            string hash;
            Assert.That(VfxAiWorkflowContractSnapshot.VerifyExisting(out hash), Is.True);
            Assert.That(hash, Is.EqualTo("BAC0A949EE43E806905F8D963980FF9C9792303CEB76B5469A94379C61C0D181"));
        }

        [Test]
        public void IsolatedAiRecipes_ValidateAndBuild_ThenLeaveNoGeneratedAssets()
        {
            foreach (var round in new[] { "R1.initial-output.json", "R5.initial-output.json" })
            {
                var json = File.ReadAllText(Evidence("recipes", round));
                var result = new VfxCompiler().Build(json);
                Write(Evidence("recipes", round.Replace("output.json", "actual-report.json")), Report(result.Plan.Report, result.Succeeded, result.PrefabPath));
                Assert.That(result.Succeeded, Is.True, round + " failed: " + Report(result.Plan.Report, false, null));
                var recipeId = result.PrefabPath.Split('/').Reverse().Skip(1).First();
                Assert.That(AssetDatabase.DeleteAsset(VfxCompiler.GeneratedRoot + "/" + recipeId), Is.True);
            }
            AssetDatabase.Refresh();
        }

        [Test]
        public void IsolatedAiRecipeFailures_ReturnTheRawCatalogErrorsWithoutWrites()
        {
            foreach (var round in new[] { "R2.final-output.json", "R3.final-output.json", "R4.final-output.json" })
            {
                var result = new VfxCompiler().Build(File.ReadAllText(Evidence("recipes", round)));
                Write(Evidence("recipes", round.Replace("output.json", "actual-report.json")), Report(result.Plan.Report, result.Succeeded, result.PrefabPath));
                Assert.That(result.Succeeded, Is.False);
                Assert.That(result.Plan.Report.HasErrors, Is.True);
            }
        }

        [Test]
        public void CohortC_WritesActualReportForEveryPersistedAttempt()
        {
            var directory = Evidence("cohort-c", "");
            foreach (var file in Directory.GetFiles(directory, "*.recipe.json", SearchOption.TopDirectoryOnly))
            {
                var result = new VfxCompiler().Build(File.ReadAllText(file));
                Write(file.Replace(".recipe.json", ".report.json"), Report(result.Plan.Report, result.Succeeded, result.PrefabPath));
                if (result.Succeeded && !string.IsNullOrEmpty(result.PrefabPath))
                {
                    var recipeId = result.PrefabPath.Split('/').Reverse().Skip(1).First();
                    AssetDatabase.DeleteAsset(VfxCompiler.GeneratedRoot + "/" + recipeId);
                }
            }
            AssetDatabase.Refresh();
        }

        [Test]
        public void CohortD_BuildAndFrozenSemanticAssertions()
        {
            var directory = Evidence("cohort-d", "");
            foreach (var file in Directory.GetFiles(directory, "D*.recipe.json", SearchOption.TopDirectoryOnly))
            {
                var json = File.ReadAllText(file);
                var result = new VfxCompiler().Build(json);
                Write(file.Replace(".recipe.json", ".report.json"), Report(result.Plan.Report, result.Succeeded, result.PrefabPath));
                if (file.EndsWith(".final.recipe.json", StringComparison.Ordinal))
                {
                    Assert.That(result.Succeeded, Is.True, file);
                    Assert.That(SatisfiesFrozenSemanticSpec(Path.GetFileName(file).Substring(0, 2), JObject.Parse(json)), Is.True, file);
                }
                if (result.Succeeded && !string.IsNullOrEmpty(result.PrefabPath))
                {
                    var recipeId = result.PrefabPath.Split('/').Reverse().Skip(1).First();
                    AssetDatabase.DeleteAsset(VfxCompiler.GeneratedRoot + "/" + recipeId);
                }
            }
            AssetDatabase.Refresh();
        }

        [Test]
        public void CohortE_RequiresExactlyFiveFrozenFinalRecipes()
        {
            var directory = Evidence("cohort-e", "");
            var finals = Directory.GetFiles(directory, "E*.final.recipe.json", SearchOption.TopDirectoryOnly).OrderBy(path => path, StringComparer.Ordinal).ToArray();
            CollectionAssert.AreEqual(new[] { "E1.final.recipe.json", "E2.final.recipe.json", "E3.final.recipe.json", "E4.final.recipe.json", "E5.final.recipe.json" }, finals.Select(Path.GetFileName).ToArray());
            var successes = 0;
            foreach (var file in finals)
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var parsed = JObject.Parse(json);
                    var build = new VfxCompiler().Build(json);
                    Write(file.Replace(".recipe.json", ".report.json"), Report(build.Plan.Report, build.Succeeded, build.PrefabPath));
                    if (build.Succeeded && SatisfiesCohortESpec(Path.GetFileName(file).Substring(0, 2), parsed)) successes++;
                    if (build.Succeeded && !string.IsNullOrEmpty(build.PrefabPath)) AssetDatabase.DeleteAsset(VfxCompiler.GeneratedRoot + "/" + build.PrefabPath.Split('/').Reverse().Skip(1).First());
                }
                catch (Exception exception) { Write(file.Replace(".recipe.json", ".report.json"), new JObject { ["succeeded"] = false, ["detail"] = exception.Message, ["entries"] = new JArray() }.ToString(Formatting.Indented)); }
            }
            Assert.That(successes, Is.GreaterThanOrEqualTo(4));
            Assert.That(AssetDatabase.GetSubFolders(VfxCompiler.GeneratedRoot).Where(path => path != VfxCompiler.GeneratedRoot + "/fireball_2d"), Is.Empty);
        }

        [Test]
        public void CohortE_RequiresExactlyThreeFrozenFinalPatches()
        {
            var directory = Evidence("cohort-e", "");
            var finals = Directory.GetFiles(directory, "P*.final.patch.json", SearchOption.TopDirectoryOnly).OrderBy(path => path, StringComparer.Ordinal).ToArray();
            CollectionAssert.AreEqual(new[] { "P1.final.patch.json", "P2.final.patch.json", "P3.final.patch.json" }, finals.Select(Path.GetFileName).ToArray());
            foreach (var patch in finals)
            {
                try
                {
                    File.WriteAllText(Absolute(TemporaryRecipe), PatchBase()); AssetDatabase.ImportAsset(TemporaryRecipe, ImportAssetOptions.ForceUpdate);
                    var result = new VfxPatchService().ApplyToAsset(TemporaryRecipe, File.ReadAllText(patch), 1);
                    Write(patch.Replace(".patch.json", ".report.json"), Report(result.Report, result.IsValid, "revision " + result.BeforeRevision + "->" + result.AfterRevision));
                    Assert.That(result.IsValid && result.AfterRevision == 2, Is.True, patch);
                    var after = JObject.Parse(File.ReadAllText(Absolute(TemporaryRecipe)));
                    var travel = after["stages"].Children<JObject>().Single(stage => (string)stage["id"] == "travel");
                    var modules = travel["modules"].Children<JObject>().ToList();
                    if (Path.GetFileName(patch).StartsWith("P1.")) Assert.That((double)modules.Single(module => (string)module["id"] == "embers")["parameters"]["rate"], Is.EqualTo(9));
                    if (Path.GetFileName(patch).StartsWith("P2.")) Assert.That((bool)modules.Single(module => (string)module["id"] == "embers")["enabled"], Is.False);
                    if (Path.GetFileName(patch).StartsWith("P3.")) { var module = modules.Single(value => (string)value["id"] == "lighter_embers"); Assert.That((string)module["templateId"], Is.EqualTo("PFT_2D_Embers")); Assert.That((string)module["kind"], Is.EqualTo("secondary_particles")); Assert.That((string)module["attachTo"], Is.EqualTo("core")); }
                    var historyJson = JArray.Parse(File.ReadAllText(Absolute(TemporaryRecipe + VfxPatchService.HistorySuffix))); var last = (JObject)historyJson.Last;
                    Assert.That((int)last["beforeRevision"], Is.EqualTo(1)); Assert.That((int)last["afterRevision"], Is.EqualTo(2));
                }
                finally { if (AssetDatabase.IsValidFolder(VfxCompiler.GeneratedRoot + "/s9_patch_base")) AssetDatabase.DeleteAsset(VfxCompiler.GeneratedRoot + "/s9_patch_base"); if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(TemporaryRecipe) != null) AssetDatabase.DeleteAsset(TemporaryRecipe); var history = TemporaryRecipe + VfxPatchService.HistorySuffix; if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(history) != null) AssetDatabase.DeleteAsset(history); AssetDatabase.Refresh(); }
            }
            AssetDatabase.Refresh();
        }

        [Test]
        public void CohortE_RequiresContinuousEvidenceChain()
        {
            var directory = Evidence("cohort-e", "");
            foreach (var key in new[] { "E1", "E2", "E3", "E4", "E5", "P1", "P2", "P3" })
            {
                var extension = key[0] == 'E' ? "recipe.json" : "patch.json";
                var attempts = Enumerable.Range(0, 3).Where(attempt => File.Exists(Path.Combine(directory, key + ".attempt" + attempt + "." + extension))).ToArray();
                CollectionAssert.AreEqual(Enumerable.Range(0, attempts.Length).ToArray(), attempts, key + " attempts must be continuous.");
                Assert.That(File.Exists(Path.Combine(directory, key + ".attempt3." + extension)), Is.False, key + " exceeds two repairs.");
                Assert.That(attempts.Length, Is.GreaterThan(0), key + " initial output missing.");
                for (var attempt = 0; attempt < attempts.Length; attempt++)
                {
                    var output = Path.Combine(directory, key + ".attempt" + attempt + "." + extension);
                    var report = output.Replace("." + extension, ".report.json"); Assert.That(File.Exists(report), Is.True, key + " report missing for attempt " + attempt);
                    var parsed = JObject.Parse(File.ReadAllText(report)); foreach (var entry in (JArray)parsed["entries"]) foreach (var field in new[] { "code", "severity", "path", "message", "actualValue", "allowedRange" }) Assert.That(((JObject)entry).ContainsKey(field), Is.True, key + " report field missing: " + field);
                    if (attempt > 0) { var prompt = Path.Combine(directory, key + ".repair" + attempt + ".prompt.md"); Assert.That(File.Exists(prompt), Is.True, key + " repair prompt missing for attempt " + attempt); StringAssert.Contains(File.ReadAllText(output.Replace(".attempt" + attempt + "." + extension, ".attempt" + (attempt - 1) + ".report.json")), File.ReadAllText(prompt)); }
                }
                var final = Path.Combine(directory, key + ".final." + extension); Assert.That(File.Exists(final), Is.True, key + " final missing."); Assert.That(File.ReadAllBytes(final), Is.EqualTo(File.ReadAllBytes(Path.Combine(directory, key + ".attempt" + attempts.Last() + "." + extension))));
            }
        }

        [Test]
        public void CohortE_WritesActualAttemptReports()
        {
            var directory = Evidence("cohort-e", "");
            foreach (var file in Directory.GetFiles(directory, "E*.attempt*.recipe.json", SearchOption.TopDirectoryOnly))
            {
                var build = new VfxCompiler().Build(File.ReadAllText(file)); Write(file.Replace(".recipe.json", ".report.json"), Report(build.Plan.Report, build.Succeeded, build.PrefabPath));
                if (build.Succeeded && !string.IsNullOrEmpty(build.PrefabPath)) AssetDatabase.DeleteAsset(VfxCompiler.GeneratedRoot + "/" + build.PrefabPath.Split('/').Reverse().Skip(1).First());
            }
            AssetDatabase.Refresh();
        }

        [Test]
        public void CohortF_ContractAndPreregistration_AreFrozen()
        {
            string hash;
            Assert.That(VfxAiWorkflowContractSnapshot.VerifyExisting("cohort-f", out hash), Is.True, "The F contract must be exported once before dispatch and never regenerated by this test.");
            Assert.That(hash, Is.EqualTo("31AEE5ACF67C7C4A2EA4AFD0442E53DE6238957EEFD5F8AE03862C668D8FEFED"));
            var spec = JObject.Parse(File.ReadAllText(Evidence("cohort-f", "acceptance-spec.json")));
            CollectionAssert.AreEquivalent(new[] { "F1", "F2", "F3", "F4", "F5" }, ((JObject)spec["recipes"]).Properties().Select(value => value.Name));
            CollectionAssert.AreEquivalent(new[] { "P1", "P2", "P3" }, ((JObject)spec["patches"]).Properties().Select(value => value.Name));
        }

        [Test]
        public void CohortF_RequiresFiveFinalRecipes_AndAtLeastFourBuildAndSemanticSuccesses()
        {
            var directory = Evidence("cohort-f", "");
            var finals = Directory.GetFiles(directory, "F*.final.recipe.json", SearchOption.TopDirectoryOnly).OrderBy(path => path, StringComparer.Ordinal).ToArray();
            CollectionAssert.AreEqual(new[] { "F1.final.recipe.json", "F2.final.recipe.json", "F3.final.recipe.json", "F4.final.recipe.json", "F5.final.recipe.json" }, finals.Select(Path.GetFileName).ToArray());
            var successes = 0;
            foreach (var file in finals)
            {
                var success = false;
                try { var json = File.ReadAllText(file); var parsed = JObject.Parse(json); var build = new VfxCompiler().Build(json); Write(file.Replace(".recipe.json", ".report.json"), Report(build.Plan.Report, build.Succeeded, build.PrefabPath)); success = build.Succeeded && SatisfiesCohortFSpec(Path.GetFileName(file).Substring(0, 2), parsed); if (build.Succeeded && !string.IsNullOrEmpty(build.PrefabPath)) AssetDatabase.DeleteAsset(VfxCompiler.GeneratedRoot + "/" + build.PrefabPath.Split('/').Reverse().Skip(1).First()); }
                catch (System.Exception exception) { Write(file.Replace(".recipe.json", ".report.json"), new JObject { ["succeeded"] = false, ["detail"] = exception.ToString(), ["entries"] = new JArray() }.ToString(Formatting.Indented) + "\n"); }
                if (success) successes++;
            }
            Assert.That(successes, Is.GreaterThanOrEqualTo(4));
            Assert.That(AssetDatabase.GetSubFolders(VfxCompiler.GeneratedRoot).Where(path => path != VfxCompiler.GeneratedRoot + "/fireball_2d"), Is.Empty);
        }

        [Test]
        public void CohortF_RequiresThreeFinalPatches_WithHistoryAndEffects()
        {
            var directory = Evidence("cohort-f", "");
            var finals = Directory.GetFiles(directory, "P*.final.patch.json", SearchOption.TopDirectoryOnly).OrderBy(path => path, StringComparer.Ordinal).ToArray();
            CollectionAssert.AreEqual(new[] { "P1.final.patch.json", "P2.final.patch.json", "P3.final.patch.json" }, finals.Select(Path.GetFileName).ToArray());
            foreach (var patch in finals)
            {
                try
                {
                    File.WriteAllText(Absolute(TemporaryRecipe), PatchBase()); AssetDatabase.ImportAsset(TemporaryRecipe, ImportAssetOptions.ForceUpdate);
                    var result = new VfxPatchService().ApplyToAsset(TemporaryRecipe, File.ReadAllText(patch), 1); Write(patch.Replace(".patch.json", ".report.json"), Report(result.Report, result.IsValid, "revision " + result.BeforeRevision + "->" + result.AfterRevision));
                    Assert.That(result.IsValid && result.AfterRevision == 2, Is.True, patch);
                    var after = JObject.Parse(File.ReadAllText(Absolute(TemporaryRecipe))); Assert.That(SatisfiesCohortFPatch(Path.GetFileName(patch).Substring(0, 2), after), Is.True, patch);
                    var history = JArray.Parse(File.ReadAllText(Absolute(TemporaryRecipe + VfxPatchService.HistorySuffix))); var last = (JObject)history.Last; Assert.That((int)last["beforeRevision"], Is.EqualTo(1)); Assert.That((int)last["afterRevision"], Is.EqualTo(2));
                }
                finally { CleanupTemporaryPatchAssets(); }
            }
        }

        [Test]
        public void CohortF_EvidenceChains_AreContinuousAndRaw()
        {
            var directory = Evidence("cohort-f", "");
            foreach (var key in new[] { "F1", "F2", "F3", "F4", "F5", "P1", "P2", "P3" })
            {
                var extension = key[0] == 'F' ? "recipe.json" : "patch.json";
                var found = Enumerable.Range(0, 3).Where(number => File.Exists(Path.Combine(directory, key + ".attempt" + number + "." + extension))).ToArray();
                CollectionAssert.Contains(new[] { "0", "0,1", "0,1,2" }, string.Join(",", found)); Assert.That(File.Exists(Path.Combine(directory, key + ".attempt3." + extension)), Is.False);
                foreach (var number in found)
                {
                    var output = Path.Combine(directory, key + ".attempt" + number + "." + extension); var report = output.Replace("." + extension, ".report.json"); Assert.That(File.Exists(report), Is.True); var parsed = JObject.Parse(File.ReadAllText(report)); foreach (var entry in (JArray)parsed["entries"]) foreach (var field in new[] { "code", "severity", "path", "message", "actualValue", "allowedRange" }) Assert.That(((JObject)entry).ContainsKey(field), Is.True);
                    if (number > 0) { var prompt = Path.Combine(directory, key + ".repair" + number + ".prompt.md"); Assert.That(File.Exists(prompt), Is.True); StringAssert.Contains(File.ReadAllText(report.Replace(".attempt" + number + ".report.json", ".attempt" + (number - 1) + ".report.json")), File.ReadAllText(prompt)); }
                }
                Assert.That(found.Length, Is.GreaterThan(0)); var final = Path.Combine(directory, key + ".final." + extension); Assert.That(File.Exists(final), Is.True); Assert.That(File.ReadAllBytes(final), Is.EqualTo(File.ReadAllBytes(Path.Combine(directory, key + ".attempt" + found.Last() + "." + extension))));
            }
        }

        [Test]
        public void CohortF_WritesActualReportForEveryPersistedRecipeAttempt()
        {
            foreach (var file in Directory.GetFiles(Evidence("cohort-f", ""), "F*.attempt*.recipe.json", SearchOption.TopDirectoryOnly))
            {
                var build = new VfxCompiler().Build(File.ReadAllText(file)); Write(file.Replace(".recipe.json", ".report.json"), Report(build.Plan.Report, build.Succeeded, build.PrefabPath));
                if (build.Succeeded && !string.IsNullOrEmpty(build.PrefabPath)) AssetDatabase.DeleteAsset(VfxCompiler.GeneratedRoot + "/" + build.PrefabPath.Split('/').Reverse().Skip(1).First());
            }
            AssetDatabase.Refresh();
        }

        [Test]
        public void IsolatedAiPatches_ValidateApplyAndBuild_ThenLeaveNoAssets()
        {
            foreach (var round in new[] { "P1.initial-output.json", "P2.initial-output.json", "P3.initial-output.json" })
            {
                File.WriteAllText(Absolute(TemporaryRecipe), PatchBase());
                AssetDatabase.ImportAsset(TemporaryRecipe, ImportAssetOptions.ForceUpdate);
                var result = new VfxPatchService().ApplyToAsset(TemporaryRecipe, File.ReadAllText(Evidence("patches", round)), 1);
                Write(Evidence("patches", round.Replace("output.json", "actual-report.json")), Report(result.Report, result.IsValid, "revision " + result.BeforeRevision + "->" + result.AfterRevision));
                Assert.That(result.IsValid, Is.True, round + " failed: " + Report(result.Report, false, null));
                Assert.That(AssetDatabase.DeleteAsset(VfxCompiler.GeneratedRoot + "/s9_patch_base"), Is.True);
                Assert.That(AssetDatabase.DeleteAsset(TemporaryRecipe), Is.True);
                var history = TemporaryRecipe + VfxPatchService.HistorySuffix;
                if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(history) != null) AssetDatabase.DeleteAsset(history);
            }
            AssetDatabase.Refresh();
        }

        private static string PatchBase() { return File.ReadAllText(Absolute("Assets/VFX/Recipes/fireball-2d.default.json")).Replace("\"id\": \"fireball_2d\"", "\"id\": \"s9_patch_base\""); }
        private static bool SatisfiesCohortFSpec(string key, JObject recipe)
        {
            var spec = (JObject)JObject.Parse(File.ReadAllText(Evidence("cohort-f", "acceptance-spec.json")))["recipes"][key];
            if ((string)recipe["id"] != (string)spec["id"] || (string)recipe["targetProfile"] != (string)spec["profile"]) return false;
            var stages = recipe["stages"] == null ? new List<JObject>() : recipe["stages"].Children<JObject>().ToList();
            if (!Stage(stages, "launch", "on_launch") || !Stage(stages, "travel", "after_previous") || !Stage(stages, "impact", "on_hit")) return false;
            var required = (JObject)spec["require"];
            foreach (var property in required.Properties()) foreach (var kind in ((string)property.Value).Split(',')) if (Module(stages, property.Name, kind) == null) return false;
            var forbidden = (JObject)spec["forbid"];
            if (forbidden != null) foreach (var property in forbidden.Properties()) foreach (var kind in ((string)property.Value).Split(',')) if (Module(stages, property.Name, kind) != null) return false;
            foreach (var property in ((JObject)spec["compare"]).Properties()) { var value = Parameter(stages, property.Name); if (!value.HasValue || !FCompare(value.Value, Default(property.Name), (string)property.Value)) return false; }
            return true;
        }
        private static bool Stage(List<JObject> stages, string id, string trigger) { var stage = stages.FirstOrDefault(value => (string)value["id"] == id); return stage != null && (string)stage["trigger"] == trigger; }
        private static JObject Module(List<JObject> stages, string stageId, string kind) { var stage = stages.FirstOrDefault(value => (string)value["id"] == stageId); return stage == null || stage["modules"] == null ? null : stage["modules"].Children<JObject>().FirstOrDefault(value => (string)value["kind"] == kind); }
        private static double? Parameter(List<JObject> stages, string key)
        {
            var tokens = key.Split('.'); var module = Module(stages, tokens[0] == "core" || tokens[0] == "trail" || tokens[0] == "embers" ? "travel" : tokens[0] == "launch" ? "launch" : "impact", tokens[0] == "core" ? "energy_body" : tokens[0] == "trail" ? "motion_trail" : tokens[0] == "embers" ? "secondary_particles" : tokens[0] == "launch" ? "impact_flash" : tokens[0] == "burst" ? "impact_burst" : "shockwave");
            return module == null || module["parameters"]?[tokens[1]] == null ? (double?)null : (double)module["parameters"][tokens[1]];
        }
        private static double Default(string key) { switch (key) { case "core.scale": return 1.2; case "launch.lifetime": return .12; case "launch.size": return 1; case "trail.time": return .22; case "trail.width": return .42; case "embers.rate": return 18; case "embers.lifetime": return .55; case "burst.count": return 24; case "burst.speed": return 3.5; case "shockwave.endSize": return 2.8; default: throw new System.ArgumentOutOfRangeException("key", key, "Unknown preregistered parameter."); } }
        private static bool FCompare(double value, double @default, string comparison) { switch (comparison) { case "<default": return value < @default; case "<=default": return value <= @default; case ">default": return value > @default; case ">=default": return value >= @default; default: return false; } }
        private static bool SatisfiesCohortFPatch(string key, JObject after)
        {
            var travel = after["stages"].Children<JObject>().First(value => (string)value["id"] == "travel"); var modules = travel["modules"].Children<JObject>().ToList();
            switch (key) { case "P1": return (double)modules.Single(value => (string)value["id"] == "embers")["parameters"]["rate"] == 9; case "P2": return !(bool)modules.Single(value => (string)value["id"] == "embers")["enabled"]; case "P3": var added = modules.SingleOrDefault(value => (string)value["id"] == "lighter_embers"); return added != null && (string)added["templateId"] == "PFT_2D_Embers" && (string)added["kind"] == "secondary_particles" && (string)added["attachTo"] == "core"; default: return false; }
        }
        private static void CleanupTemporaryPatchAssets() { if (AssetDatabase.IsValidFolder(VfxCompiler.GeneratedRoot + "/s9_patch_base")) AssetDatabase.DeleteAsset(VfxCompiler.GeneratedRoot + "/s9_patch_base"); if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(TemporaryRecipe) != null) AssetDatabase.DeleteAsset(TemporaryRecipe); var history = TemporaryRecipe + VfxPatchService.HistorySuffix; if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(history) != null) AssetDatabase.DeleteAsset(history); AssetDatabase.Refresh(); }
        private static string Evidence(string folder, string file) { return Path.Combine(RepositoryRoot(), "docs", "ai-workflow", "evidence", folder, file); }
        private static string RepositoryRoot() { return Directory.GetParent(Directory.GetParent(Application.dataPath).FullName).FullName; }
        private static string Absolute(string assetPath) { return Path.Combine(Application.dataPath, assetPath.Substring("Assets/".Length)); }
        private static void Write(string path, string text) { File.WriteAllText(path, text, new UTF8Encoding(false)); }
        private static string Report(VFXComposer.Editor.Domain.ValidationReport report, bool succeeded, string detail)
        {
            var entries = new JArray(report.Entries.Select(entry => new JObject {
                ["code"] = entry.Code, ["severity"] = entry.Severity.ToString().ToLowerInvariant(), ["path"] = entry.Path,
                ["message"] = entry.Message, ["actualValue"] = entry.ActualValue == null ? JValue.CreateNull() : entry.ActualValue.DeepClone(),
                ["allowedRange"] = entry.AllowedRange == null ? JValue.CreateNull() : new JValue(entry.AllowedRange)
            }));
            return new JObject { ["succeeded"] = succeeded, ["detail"] = detail == null ? JValue.CreateNull() : new JValue(detail), ["entries"] = entries }.ToString(Formatting.Indented).Replace("\r\n", "\n") + "\n";
        }
        private static bool SatisfiesFrozenSemanticSpec(string key, JObject recipe)
        {
            var travel = recipe["stages"].Children<JObject>().FirstOrDefault(stage => (string)stage["id"] == "travel");
            var impact = recipe["stages"].Children<JObject>().FirstOrDefault(stage => (string)stage["id"] == "impact");
            if (travel == null || impact == null) return false;
            var modules = travel["modules"].Children<JObject>().ToList();
            Func<string, JObject> travelKind = kind => modules.FirstOrDefault(module => (string)module["kind"] == kind);
            var core = travelKind("energy_body"); var trail = travelKind("motion_trail"); var embers = travelKind("secondary_particles");
            var burst = impact["modules"].Children<JObject>().FirstOrDefault(module => (string)module["kind"] == "impact_burst");
            var wave = impact["modules"].Children<JObject>().FirstOrDefault(module => (string)module["kind"] == "shockwave");
            double Value(JObject module, string parameter) { return module == null ? double.NaN : (double)module["parameters"][parameter]; }
            if (key == "D1") return (string)recipe["targetProfile"] == "pc_editor" && Value(core,"scale") < 1.2 && Value(trail,"width") < .42 && Value(embers,"rate") < 18 && Value(burst,"count") <= 24 && Value(wave,"endSize") <= 2.8;
            if (key == "D2") return (string)recipe["targetProfile"] == "mobile_medium" && Value(core,"scale") > 1.2 && Value(trail,"width") > .42 && Value(embers,"rate") >= 18 && Value(burst,"count") > 24;
            if (key == "D3") return (string)recipe["targetProfile"] == "pc_editor" && Value(core,"scale") < 1.2 && trail == null && Value(burst,"count") <= 24 && Value(wave,"endSize") <= 2.8;
            if (key == "D4") return (string)recipe["targetProfile"] == "mobile_medium" && Value(core,"scale") >= 1.2 && Value(trail,"width") < .42 && embers == null && Value(burst,"count") <= 24 && Value(wave,"endSize") <= 2.8;
            return key == "D5" && (string)recipe["targetProfile"] == "pc_editor" && Value(core,"scale") >= 1.2 && Value(trail,"width") >= .42 && Value(embers,"rate") >= 18 && Value(burst,"count") > 24 && Value(wave,"endSize") > 2.8;
        }
        private static bool SatisfiesCohortESpec(string key, JObject recipe)
        {
            var spec = JObject.Parse(File.ReadAllText(Evidence("cohort-e", "acceptance-spec.json")));
            var recipeSpec = (JObject)spec["recipes"][key];
            if ((string)recipe["id"] != (string)recipeSpec["id"] || (string)recipe["targetProfile"] != (string)recipeSpec["profile"]) return false;
            var stages = recipe["stages"].Children<JObject>().ToDictionary(stage => (string)stage["id"]);
            if (!stages.ContainsKey("launch") || !stages.ContainsKey("travel") || !stages.ContainsKey("impact") || (string)stages["launch"]["trigger"] != "on_launch" || (string)stages["travel"]["trigger"] != "after_previous" || (string)stages["impact"]["trigger"] != "on_hit") return false;
            var travel = stages["travel"]["modules"].Children<JObject>().ToList(); var impact = stages["impact"]["modules"].Children<JObject>().ToList();
            foreach (var forbidden in recipeSpec["forbidKinds"] == null ? Enumerable.Empty<JToken>() : recipeSpec["forbidKinds"]) if (travel.Any(module => (string)module["kind"] == (string)forbidden)) return false;
            var values = new System.Collections.Generic.Dictionary<string, double> {
                ["coreScale"] = Param(travel,"energy_body","scale"), ["trailTime"] = Param(travel,"motion_trail","time"), ["trailWidth"] = Param(travel,"motion_trail","width"), ["embersRate"] = Param(travel,"secondary_particles","rate"), ["embersLifetime"] = Param(travel,"secondary_particles","lifetime"), ["burstCount"] = Param(impact,"impact_burst","count"), ["shockwaveEndSize"] = Param(impact,"shockwave","endSize"), ["launchLifetime"] = Param(stages["launch"]["modules"].Children<JObject>().ToList(),"impact_flash","lifetime") };
            foreach (var property in ((JObject)recipeSpec["comparisons"]).Properties()) if (!Compare(values[property.Name], (double)spec["defaults"][property.Name], (string)property.Value)) return false;
            return true;
        }
        private static double Param(System.Collections.Generic.List<JObject> modules, string kind, string parameter) { var module = modules.FirstOrDefault(value => (string)value["kind"] == kind); return module == null ? double.NaN : (double)module["parameters"][parameter]; }
        private static bool Compare(double actual, double baseline, string comparison)
        {
            switch (comparison) { case "< default": return actual < baseline; case "<= default": return actual <= baseline; case "> default": return actual > baseline; case ">= default": return actual >= baseline; default: return false; }
        }
    }
}

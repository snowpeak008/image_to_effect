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
    // Do not run this before dispatch: it is the non-optional complete-evidence gate for the post-batch EditMode run.
    [Explicit("Historical Cohort H final evidence is invalid/noncountable and is retained only for manual audit.")]
    public sealed class S9CohortHEvidenceTests
    {
        private const string PatchBase = "Assets/VFX/Recipes/s9_cohort_h_final_patch_base.json";

        [Test]
        public void CohortH_CompleteEvidenceMeetsFrozenBuildPatchAndTransportGate()
        {
            foreach (var key in VfxCohortHProtocol.RecipeKeys.Concat(VfxCohortHProtocol.PatchKeys)) AssertChain(key);
            var successes = 0; var outcomes = new JArray();
            foreach (var key in VfxCohortHProtocol.RecipeKeys)
            {
                VfxBuildResult build = null; JObject recipe = null; var succeeded = false; var detail = string.Empty;
                try
                {
                    recipe = JObject.Parse(File.ReadAllText(FinalPath(key))); build = new VfxCompiler().Build(recipe.ToString()); succeeded = build.Succeeded && SatisfiesRecipe(key, recipe); detail = build.Succeeded ? (succeeded ? "build and frozen semantic assertion passed" : "build passed but frozen semantic assertion failed") : Describe(build.Plan.Report);
                }
                catch (Exception exception) { detail = exception.ToString(); }
                finally { if (build != null && build.Succeeded && recipe != null) AssetDatabase.DeleteAsset(VfxCompiler.OutputFolder(VfxDomainParser.ParseRecipe(recipe.ToString()).Value)); }
                if (succeeded) successes++; outcomes.Add(new JObject { ["key"] = key, ["succeeded"] = succeeded, ["detail"] = detail });
            }
            VfxCohortHProtocol.WriteOnce(Evidence("recipe-results.generated.json"), new JObject { ["successes"] = successes, ["total"] = VfxCohortHProtocol.RecipeKeys.Length, ["outcomes"] = outcomes }.ToString(Newtonsoft.Json.Formatting.Indented).Replace("\r\n", "\n") + "\n");
            Assert.That(successes, Is.GreaterThanOrEqualTo(4), "M6 requires at least four of the exactly five H Recipe attempts.");
            var patchFailures = new List<string>(); foreach (var key in VfxCohortHProtocol.PatchKeys) { string detail; if (!TryPatch(key, out detail)) patchFailures.Add(key + ": " + detail); }
            Assert.That(patchFailures, Is.Empty, "Every frozen Patch requires real Apply/revision/history/effect success. " + string.Join(" | ", patchFailures));
            Assert.That(AssetDatabase.GetSubFolders(VfxCompiler.GeneratedRoot).Where(path => path != VfxCompiler.GeneratedRoot + "/fireball_2d"), Is.Empty, "H verification must leave Generated with only fireball_2d.");
        }

        private static void AssertChain(string key)
        {
            var attempts = Enumerable.Range(0, 3).Where(number => File.Exists(VfxCohortHProtocol.AttemptPath(key, number))).ToArray();
            CollectionAssert.IsNotEmpty(attempts, key + " needs persisted attempt0."); CollectionAssert.AreEqual(Enumerable.Range(0, attempts.Length).ToArray(), attempts, key + " has a discontinuous attempt chain."); Assert.That(File.Exists(VfxCohortHProtocol.AttemptPath(key, 3)), Is.False);
            string initialThread = null;
            foreach (var attempt in attempts)
            {
                var report = VfxCohortHProtocol.ReportPath(key, attempt); var transport = VfxCohortHProtocol.TransportPath(key, attempt); Assert.That(File.Exists(report), Is.True, key + " lacks machine report " + attempt); Assert.That(File.Exists(transport), Is.True, key + " lacks transport witness " + attempt); AssertReport(report);
                var witness = JObject.Parse(File.ReadAllText(transport)); foreach (var field in new[] { "question", "attempt", "agentName", "model", "reasoningEffort", "forkTurns", "threadId", "transport", "payloadFile", "payloadSha256", "continuity", "wireReadback" }) Assert.That(witness.ContainsKey(field), Is.True, key + " witness missing " + field);
                Assert.That((string)witness["model"], Is.EqualTo("gpt-5.6-terra")); Assert.That((string)witness["reasoningEffort"], Is.EqualTo("high")); Assert.That((string)witness["forkTurns"], Is.EqualTo("none")); Assert.That((string)witness["transport"], Is.EqualTo(attempt == 0 ? "spawn_agent" : "followup_task"));
                var payload = attempt == 0 ? VfxCohortHProtocol.InitialPayloadPath(key) : VfxCohortHProtocol.RepairPayloadPath(key, attempt); Assert.That((string)witness["payloadFile"], Is.EqualTo(Path.GetFileName(payload))); Assert.That((string)witness["payloadSha256"], Is.EqualTo(Hash(File.ReadAllBytes(payload))));
                if (attempt == 0) initialThread = (string)witness["threadId"];
                else
                {
                    Assert.That((string)witness["threadId"], Is.EqualTo(initialThread), key + " repair must retain initial thread continuity.");
                    var repair = VfxCohortHProtocol.NormalizeNewlines(File.ReadAllText(payload)); var prior = VfxCohortHProtocol.NormalizeNewlines(File.ReadAllText(VfxCohortHProtocol.ReportPath(key, attempt - 1))); StringAssert.Contains(prior, repair, key + " repair payload must include the complete preceding report."); Assert.That(repair, Does.Not.Contain("FROZEN CONTRACT SNAPSHOT:"));
                    var prepared = JObject.Parse(File.ReadAllText(VfxCohortHProtocol.PreparedPath(key, attempt)));
                    Assert.That((string)prepared["PromptSha256"], Is.EqualTo(Hash(File.ReadAllBytes(payload))));
                    var priorReportBytes = File.ReadAllBytes(VfxCohortHProtocol.ReportPath(key, attempt - 1));
                    Assert.That((string)prepared["PriorReportSha256"], Is.EqualTo(Hash(priorReportBytes)));
                }
            }
            CollectionAssert.AreEqual(File.ReadAllBytes(VfxCohortHProtocol.AttemptPath(key, attempts.Last())), File.ReadAllBytes(FinalPath(key)), key + " final must be byte-identical to its last attempt.");
        }

        private static bool TryPatch(string key, out string detail)
        {
            detail = string.Empty;
            try
            {
                File.WriteAllText(Absolute(PatchBase), File.ReadAllText(Absolute(VfxAiWorkflowExporter.FormalDefaultRecipePath)).Replace("\"id\": \"fireball_2d\"", "\"id\": \"s9_cohort_h_final_patch_base\"")); AssetDatabase.ImportAsset(PatchBase, ImportAssetOptions.ForceUpdate);
                var result = new VfxPatchService().ApplyToAsset(PatchBase, File.ReadAllText(FinalPath(key)), 1); if (!result.IsValid || result.AfterRevision != 2) { detail = Describe(result.Report); return false; }
                var after = JObject.Parse(File.ReadAllText(Absolute(PatchBase))); if (!SatisfiesPatch(key, after)) { detail = "frozen effect assertion failed"; return false; } var history = JArray.Parse(File.ReadAllText(Absolute(PatchBase + VfxPatchService.HistorySuffix))); var last = (JObject)history.Last; if ((int)last["beforeRevision"] != 1 || (int)last["afterRevision"] != 2) { detail = "revision/history assertion failed"; return false; } return true;
            }
            catch (Exception exception) { detail = exception.ToString(); return false; }
            finally { CleanupPatch(); }
        }

        private static bool SatisfiesRecipe(string key, JObject recipe)
        {
            var spec = (JObject)JObject.Parse(File.ReadAllText(Evidence("acceptance-spec.json")))["recipes"][key]; if ((string)recipe["id"] != (string)spec["id"] || (string)recipe["targetProfile"] != (string)spec["profile"]) return false;
            var stages = recipe["stages"].Children<JObject>().ToList(); if (!Stage(stages, "launch", "on_launch") || !Stage(stages, "travel", "after_previous") || !Stage(stages, "impact", "on_hit")) return false;
            foreach (var kind in spec["travel"].Values<string>()) if (Module(stages, "travel", kind) == null) return false; foreach (var kind in (spec["forbidTravel"] ?? new JArray()).Values<string>()) if (Module(stages, "travel", kind) != null) return false; foreach (var kind in spec["impact"].Values<string>()) if (Module(stages, "impact", kind) == null) return false;
            foreach (var comparison in ((JObject)spec["compare"]).Properties()) if (!Compare(Parameter(stages, comparison.Name), Default(comparison.Name), (string)comparison.Value)) return false; return true;
        }
        private static bool SatisfiesPatch(string key, JObject recipe)
        {
            var modules = recipe["stages"].Children<JObject>().Single(stage => (string)stage["id"] == "travel")["modules"].Children<JObject>().ToList(); if (key == "P1") return (double)modules.Single(module => (string)module["id"] == "embers")["parameters"]["rate"] == 10; if (key == "P2") return !(bool)modules.Single(module => (string)module["id"] == "embers")["enabled"]; var added = modules.SingleOrDefault(module => (string)module["id"] == "echo_embers"); return added != null && (string)added["kind"] == "secondary_particles" && (string)added["templateId"] == "PFT_2D_Embers" && (string)added["attachTo"] == "core";
        }
        private static bool Stage(List<JObject> stages, string id, string trigger) { var stage = stages.SingleOrDefault(value => (string)value["id"] == id); return stage != null && (string)stage["trigger"] == trigger; }
        private static JObject Module(List<JObject> stages, string stage, string kind) { var value = stages.SingleOrDefault(item => (string)item["id"] == stage); return value == null ? null : value["modules"].Children<JObject>().SingleOrDefault(item => (string)item["kind"] == kind); }
        private static double Parameter(List<JObject> stages, string key) { var bits = key.Split('.'); var kind = bits[0] == "core" ? "energy_body" : bits[0] == "trail" ? "motion_trail" : bits[0] == "embers" ? "secondary_particles" : bits[0] == "burst" ? "impact_burst" : "shockwave"; var stage = bits[0] == "core" || bits[0] == "trail" || bits[0] == "embers" ? "travel" : "impact"; var module = Module(stages, stage, kind); return module == null || module["parameters"][bits[1]] == null ? double.NaN : (double)module["parameters"][bits[1]]; }
        private static double Default(string key) { return Parameter(SnapshotCanonical()["stages"].Children<JObject>().ToList(), key); }
        private static JObject SnapshotCanonical()
        {
            var snapshot = VfxCohortHProtocol.NormalizeNewlines(File.ReadAllText(Evidence("contract-snapshot.md"))); const string begin = "<!-- BEGIN canonical-recipe.generated.json -->\n"; const string end = "\n<!-- END canonical-recipe.generated.json -->"; var start = snapshot.IndexOf(begin, StringComparison.Ordinal) + begin.Length; var finish = snapshot.IndexOf(end, start, StringComparison.Ordinal); return JObject.Parse(snapshot.Substring(start, finish - start));
        }
        private static bool Compare(double actual, double baseline, string operation) { return operation == "<" ? actual < baseline : operation == "<=" ? actual <= baseline : operation == "==" ? Math.Abs(actual - baseline) < 0.000001 : operation == ">=" ? actual >= baseline : operation == ">" && actual > baseline; }
        private static void AssertReport(string path) { var report = JObject.Parse(File.ReadAllText(path)); foreach (var field in new[] { "succeeded", "detail", "entries" }) Assert.That(report.ContainsKey(field), Is.True); foreach (var entry in (JArray)report["entries"]) foreach (var field in new[] { "code", "severity", "path", "message", "actualValue", "allowedRange" }) Assert.That(((JObject)entry).ContainsKey(field), Is.True); }
        private static string FinalPath(string key) { return Evidence(key + ".final." + (VfxCohortHProtocol.RecipeKeys.Contains(key) ? "recipe.json" : "patch.json")); }
        private static string Evidence(string file) { return Path.Combine(VfxCohortHProtocol.EvidenceDirectory(), file); }
        private static string Absolute(string path) { return Path.Combine(Application.dataPath, path.Substring("Assets/".Length)); }
        private static string Describe(ValidationReport report) { return report == null ? string.Empty : string.Join(" | ", report.Entries.Select(entry => entry.Code + " " + entry.Path)); }
        private static string Hash(byte[] bytes) { using (var sha = System.Security.Cryptography.SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty); }
        private static void CleanupPatch() { if (AssetDatabase.IsValidFolder(VfxCompiler.GeneratedRoot + "/s9_cohort_h_final_patch_base")) AssetDatabase.DeleteAsset(VfxCompiler.GeneratedRoot + "/s9_cohort_h_final_patch_base"); if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(PatchBase) != null) AssetDatabase.DeleteAsset(PatchBase); var history = PatchBase + VfxPatchService.HistorySuffix; if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(history) != null) AssetDatabase.DeleteAsset(history); AssetDatabase.Refresh(); }
    }
}

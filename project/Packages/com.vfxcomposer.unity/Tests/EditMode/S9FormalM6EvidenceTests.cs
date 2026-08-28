using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using VFXComposer.Editor.Build;
using VFXComposer.Editor.Domain;
using VFXComposer.Editor.Workflow;

namespace VFXComposer.Tests.EditMode
{
    // Non-Explicit formal recovery gate: historical I Recipe evidence plus only the new K Patch batch.
    public sealed class S9FormalM6EvidenceTests
    {
        [Test]
        public void M6_RequiresCohortIRecipeFourOfFiveAndCohortKPatchThreeOfThree()
        {
            var iSuccesses = VfxCohortIProtocol.RecipeKeys.Count(VerifyIRecipe); var kSuccesses = S9CohortKFinalEvidenceTests.VerifyCompletedEvidence();
            Assert.That(iSuccesses >= 4 && kSuccesses == 3, Is.True, "Formal M6 requires I Recipe >=4/5 and K Patch exactly 3/3; historical I Patch 1/3 and J Patch 2/3 are intentionally excluded.");
        }

        private static bool VerifyIRecipe(string key)
        {
            try
            {
                VerifyIChain(key); var json = File.ReadAllText(I(key + ".final.recipe.json")); var recipe = JObject.Parse(json); var build = new VfxCompiler().Build(recipe.ToString());
                try { return build.Succeeded && Semantic(key, recipe); }
                finally { if (build.Succeeded) AssetDatabase.DeleteAsset(VfxCompiler.OutputFolder(VfxDomainParser.ParseRecipe(recipe.ToString()).Value)); }
            }
            catch (Exception exception) { TestContext.Progress.WriteLine(key + ": " + exception.Message); return false; }
        }

        private static void VerifyIChain(string key)
        {
            var attempts = Enumerable.Range(0, 3).Where(number => File.Exists(VfxCohortIProtocol.AttemptPath(key, number))).ToArray(); Need(attempts.SequenceEqual(Enumerable.Range(0, attempts.Length)) && attempts.Length > 0 && !File.Exists(VfxCohortIProtocol.AttemptPath(key, 3)), key + " attempt chain is invalid."); string thread = null;
            foreach (var attempt in attempts)
            {
                var report = VfxCohortIProtocol.ReportPath(key, attempt); Need(File.Exists(report), key + " lacks a report."); VerifyIReport(report); var witness = JObject.Parse(File.ReadAllText(VfxCohortIProtocol.TransportPath(key, attempt))); var envelope = attempt == 0 ? VfxCohortIProtocol.InitialEnvelopePath(key) : VfxCohortIProtocol.RepairEnvelopePath(key, attempt); var payload = attempt == 0 ? VfxCohortIProtocol.InitialPayloadPath(key) : VfxCohortIProtocol.RepairPayloadPath(key, attempt); var temp = attempt == 0 ? VfxCohortIProtocol.TempInitialPayloadPath(key) : VfxCohortIProtocol.TempRepairPayloadPath(key, attempt);
                foreach (var field in new[] { "question", "attempt", "agentName", "model", "reasoningEffort", "forkTurns", "threadId", "transport", "disclosure", "envelopeSha256", "payloadSha256", "tempPayloadSha256" }) Need(witness.ContainsKey(field), key + " witness lacks " + field + ".");
                Need((string)witness["question"] == key && (int)witness["attempt"] == attempt && (string)witness["agentName"] == "s9_i_" + key.ToLowerInvariant() && (string)witness["model"] == "gpt-5.6-terra" && (string)witness["reasoningEffort"] == "high" && (string)witness["forkTurns"] == "none" && !string.IsNullOrWhiteSpace((string)witness["threadId"]) && !string.IsNullOrWhiteSpace((string)witness["disclosure"]), key + " witness identity/disclosure is invalid."); Need((string)witness["transport"] == (attempt == 0 ? "spawn_agent" : "followup_task") && (string)witness["envelopeSha256"] == Hash(File.ReadAllBytes(envelope)) && (string)witness["payloadSha256"] == Hash(File.ReadAllBytes(payload)) && (string)witness["tempPayloadSha256"] == Hash(File.ReadAllBytes(temp)) && ByteEqual(File.ReadAllBytes(payload), File.ReadAllBytes(temp)), key + " witness transport/hashes are invalid.");
                if (attempt == 0) thread = (string)witness["threadId"]; else { Need((string)witness["threadId"] == thread && VfxCohortIProtocol.Normalize(File.ReadAllText(payload)).Contains(VfxCohortIProtocol.Normalize(File.ReadAllText(VfxCohortIProtocol.ReportPath(key, attempt - 1)))), key + " repair continuity is invalid."); var prepared = JObject.Parse(File.ReadAllText(VfxCohortIProtocol.PreparedPath(key, attempt))); Need((string)prepared["EnvelopeSha256"] == Hash(File.ReadAllBytes(envelope)) && (string)prepared["PayloadSha256"] == Hash(File.ReadAllBytes(payload)) && (string)prepared["TempPayloadSha256"] == Hash(File.ReadAllBytes(temp)) && (string)prepared["PriorReportSha256"] == Hash(File.ReadAllBytes(VfxCohortIProtocol.ReportPath(key, attempt - 1))), key + " prepared repair hashes are invalid."); }
            }
            Need(ByteEqual(File.ReadAllBytes(VfxCohortIProtocol.AttemptPath(key, attempts.Last())), File.ReadAllBytes(I(key + ".final.recipe.json"))), key + " final bytes differ from last attempt.");
        }

        private static bool Semantic(string key, JObject recipe)
        {
            var spec = (JObject)JObject.Parse(File.ReadAllText(I("acceptance-spec.json")))["recipes"][key]; if ((string)recipe["id"] != (string)spec["id"] || (string)recipe["targetProfile"] != (string)spec["profile"]) return false;
            var stages = recipe["stages"].Children<JObject>().ToList(); if (!Stage(stages, "launch", "on_launch") || !Stage(stages, "travel", "after_previous") || !Stage(stages, "impact", "on_hit")) return false;
            foreach (var kind in spec["travel"].Values<string>()) if (Module(stages, "travel", kind) == null) return false; foreach (var kind in (spec["forbidTravel"] ?? new JArray()).Values<string>()) if (Module(stages, "travel", kind) != null) return false; foreach (var kind in spec["impact"].Values<string>()) if (Module(stages, "impact", kind) == null) return false;
            return ((JObject)spec["compare"]).Properties().All(item => Compare(Value(stages, item.Name), Value(Canonical()["stages"].Children<JObject>().ToList(), item.Name), (string)item.Value));
        }

        private static void VerifyIReport(string path)
        {
            var report = JObject.Parse(File.ReadAllText(path)); foreach (var field in new[] { "succeeded", "detail", "entries" }) Need(report.ContainsKey(field), "I machine report lacks " + field + "."); Need(report["succeeded"].Type == JTokenType.Boolean && report["entries"] is JArray, "I machine report has invalid root types.");
            foreach (var entry in ((JArray)report["entries"]).Children<JObject>()) foreach (var field in new[] { "code", "severity", "path", "message", "actualValue", "allowedRange" }) Need(entry.ContainsKey(field), "I machine report entry lacks " + field + ".");
        }

        private static JObject Canonical() { var text = VfxCohortIProtocol.Normalize(File.ReadAllText(I("contract-snapshot.md"))); const string begin = "<!-- BEGIN canonical-recipe.generated.json -->\n", end = "\n<!-- END canonical-recipe.generated.json -->"; var start = text.IndexOf(begin, StringComparison.Ordinal) + begin.Length; return JObject.Parse(text.Substring(start, text.IndexOf(end, start, StringComparison.Ordinal) - start)); }
        private static bool Stage(System.Collections.Generic.List<JObject> stages, string id, string trigger) { var stage = stages.SingleOrDefault(item => (string)item["id"] == id); return stage != null && (string)stage["trigger"] == trigger; }
        private static JObject Module(System.Collections.Generic.List<JObject> stages, string stageId, string kind) { var stage = stages.SingleOrDefault(item => (string)item["id"] == stageId); return stage == null ? null : stage["modules"].Children<JObject>().SingleOrDefault(item => (string)item["kind"] == kind); }
        private static double Value(System.Collections.Generic.List<JObject> stages, string key) { var parts = key.Split('.'); var kind = parts[0] == "core" ? "energy_body" : parts[0] == "trail" ? "motion_trail" : parts[0] == "embers" ? "secondary_particles" : parts[0] == "burst" ? "impact_burst" : "shockwave"; var module = Module(stages, parts[0] == "core" || parts[0] == "trail" || parts[0] == "embers" ? "travel" : "impact", kind); return module == null ? double.NaN : (double)module["parameters"][parts[1]]; }
        private static bool Compare(double left, double right, string op) { return op == "<" ? left < right : op == "<=" ? left <= right : op == "==" ? Math.Abs(left - right) < .000001 : op == ">=" ? left >= right : op == ">" && left > right; }
        private static string I(string file) { return Path.Combine(VfxCohortIProtocol.EvidenceDirectory(), file); }
        private static void Need(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private static bool ByteEqual(byte[] left, byte[] right) { return left.Length == right.Length && !left.Where((value, index) => value != right[index]).Any(); }
        private static string Hash(byte[] bytes) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty); }
    }
}

using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using VFXComposer.Editor.Build;
using VFXComposer.Editor.Patch;
using VFXComposer.Editor.Workflow;

namespace VFXComposer.Tests.EditMode
{
    // Non-Explicit by design: after approved dispatch, this is the cumulative 3/3 J Patch evidence gate.
    [Explicit("Historical Cohort J final evidence remains 2/3 and is noncountable for recovered M6.")]
    public sealed class S9CohortJFinalEvidenceTests
    {
        [Test]
        public void CohortJ_ThreePatchEvidenceChainsApplyWithRevisionHistoryAndEffect()
        {
            Assert.That(VerifyCompletedEvidence(), Is.EqualTo(3), "J final Patch gate requires exactly 3/3 complete chains, real applies, revision/history transitions, and effects.");
        }

        internal static int VerifyCompletedEvidence()
        {
            var successes = 0;
            foreach (var key in VfxCohortJProtocol.PatchKeys)
            {
                try { Chain(key); ApplyAndVerifyEffect(key); successes++; }
                catch (Exception exception) { TestContext.Progress.WriteLine(key + ": " + exception.Message); }
            }
            return successes;
        }

        private static void Chain(string key)
        {
            var attempts = Enumerable.Range(0, 3).Where(number => File.Exists(VfxCohortJProtocol.AttemptPath(key, number))).ToArray();
            Require(attempts.SequenceEqual(Enumerable.Range(0, attempts.Length)), key + " has a discontinuous attempt chain."); Require(attempts.Length > 0, key + " has no persisted attempt."); Require(!File.Exists(VfxCohortJProtocol.AttemptPath(key, 3)), key + " exceeded the maximum attempt count.");
            string thread = null;
            foreach (var attempt in attempts)
            {
                var report = VfxCohortJProtocol.ReportPath(key, attempt); Require(File.Exists(report), key + " lacks its machine report."); var machine = AssertReport(report); Require((bool)machine["succeeded"] == (attempt == attempts.Last()), key + " report succeeded sequence must be false before the final attempt and true at the final attempt.");
                var witness = JObject.Parse(File.ReadAllText(VfxCohortJProtocol.TransportPath(key, attempt))); var envelope = attempt == 0 ? VfxCohortJProtocol.InitialEnvelopePath(key) : VfxCohortJProtocol.RepairEnvelopePath(key, attempt); var payload = attempt == 0 ? VfxCohortJProtocol.InitialPayloadPath(key) : VfxCohortJProtocol.RepairPayloadPath(key, attempt); var temp = attempt == 0 ? VfxCohortJProtocol.TempInitialPayloadPath(key) : VfxCohortJProtocol.TempRepairPayloadPath(key, attempt);
                foreach (var field in new[] { "question", "attempt", "agentName", "model", "reasoningEffort", "forkTurns", "threadId", "disclosure", "envelopeSha256", "payloadSha256", "tempPayloadSha256" }) Require(witness.ContainsKey(field), key + " witness lacks " + field + ".");
                Require((string)witness["question"] == key && (int)witness["attempt"] == attempt && (string)witness["agentName"] == "s9_j_" + key.ToLowerInvariant(), key + " witness identity mismatch."); Require((string)witness["model"] == "gpt-5.6-terra" && (string)witness["reasoningEffort"] == "high" && (string)witness["forkTurns"] == "none", key + " witness model isolation mismatch."); Require(!string.IsNullOrWhiteSpace((string)witness["threadId"]), key + " witness has no thread ID.");
                Require((string)witness["envelopeSha256"] == Hash(File.ReadAllBytes(envelope)) && (string)witness["payloadSha256"] == Hash(File.ReadAllBytes(payload)) && (string)witness["tempPayloadSha256"] == Hash(File.ReadAllBytes(temp)), key + " witness hash mismatch."); Require(ByteEqual(File.ReadAllBytes(payload), File.ReadAllBytes(temp)), key + " payload/temp mismatch."); Require((string)witness["transport"] == (attempt == 0 ? "spawn_agent" : "followup_task"), key + " transport mismatch.");
                if (attempt == 0) thread = (string)witness["threadId"];
                else
                {
                    Require((string)witness["threadId"] == thread, key + " repair changed threads."); Require(VfxCohortJProtocol.Normalize(File.ReadAllText(payload)).Contains(VfxCohortJProtocol.Normalize(File.ReadAllText(VfxCohortJProtocol.ReportPath(key, attempt - 1)))), key + " repair lacks its complete preceding report.");
                    var prepared = JObject.Parse(File.ReadAllText(VfxCohortJProtocol.PreparedPath(key, attempt))); Require((string)prepared["EnvelopeSha256"] == Hash(File.ReadAllBytes(envelope)) && (string)prepared["PayloadSha256"] == Hash(File.ReadAllBytes(payload)) && (string)prepared["TempPayloadSha256"] == Hash(File.ReadAllBytes(temp)) && (string)prepared["PriorReportSha256"] == Hash(File.ReadAllBytes(VfxCohortJProtocol.ReportPath(key, attempt - 1))), key + " prepared repair hash mismatch.");
                }
            }
            Require(ByteEqual(File.ReadAllBytes(VfxCohortJProtocol.AttemptPath(key, attempts.Last())), File.ReadAllBytes(VfxCohortJProtocol.FinalPath(key))), key + " final must byte-match the last attempt.");
        }

        private static void ApplyAndVerifyEffect(string key)
        {
            var spec = (JObject)JObject.Parse(File.ReadAllText(E("acceptance-spec.json")))["patches"][key]; var path = "Assets/VFX/Recipes/s9_cohort_j_final_" + key.ToLowerInvariant() + ".json";
            try
            {
                File.WriteAllText(A(path), File.ReadAllText(A(VfxAiWorkflowExporter.FormalDefaultRecipePath)).Replace("\"id\": \"fireball_2d\"", "\"id\": \"s9_cohort_j_final_" + key.ToLowerInvariant() + "\"")); AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                var result = new VfxPatchService().ApplyToAsset(path, File.ReadAllText(VfxCohortJProtocol.FinalPath(key)), (int)spec["expectedRevision"]); Require(result.IsValid && result.BeforeRevision == 1 && result.AfterRevision == 2, key + " did not apply revision 1->2.");
                var history = JArray.Parse(File.ReadAllText(A(path + VfxPatchService.HistorySuffix))); var last = (JObject)history.Last; Require((int)last["beforeRevision"] == 1 && (int)last["afterRevision"] == 2, key + " did not persist history 1->2.");
                var recipe = JObject.Parse(File.ReadAllText(A(path))); var parts = ((string)spec["path"]).Split('/'); var stage = recipe["stages"].Children<JObject>().Single(item => (string)item["id"] == parts[2]); var module = stage["modules"].Children<JObject>().SingleOrDefault(item => (string)item["id"] == parts[4]);
                if (key == "J1") Require(module != null && JToken.DeepEquals(module["parameters"][parts[6]], spec["value"]), key + " replace effect mismatch.");
                else if (key == "J2") Require(module != null && (bool)module["enabled"] == (bool)spec["enabled"], key + " disable effect mismatch.");
                else Require(module != null && JToken.DeepEquals(module, spec["module"]), key + " add effect mismatch.");
            }
            finally { Clean(path); }
        }

        private static JObject AssertReport(string path)
        {
            var report = JObject.Parse(File.ReadAllText(path)); foreach (var field in new[] { "succeeded", "detail", "entries" }) Require(report.ContainsKey(field), "Machine report lacks " + field + "."); Require(report["succeeded"].Type == JTokenType.Boolean, "Machine report succeeded must be boolean.");
            foreach (var entry in ((JArray)report["entries"]).Children<JObject>()) foreach (var field in new[] { "code", "severity", "path", "message", "actualValue", "allowedRange" }) Require(entry.ContainsKey(field), "Machine report entry lacks " + field + ".");
            return report;
        }
        private static void Clean(string path) { var recipe = A(path); if (File.Exists(recipe)) { var parsed = VFXComposer.Editor.Domain.VfxDomainParser.ParseRecipe(File.ReadAllText(recipe)); if (parsed.Value != null && AssetDatabase.IsValidFolder(VfxCompiler.OutputFolder(parsed.Value))) AssetDatabase.DeleteAsset(VfxCompiler.OutputFolder(parsed.Value)); } if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path) != null) AssetDatabase.DeleteAsset(path); var history = path + VfxPatchService.HistorySuffix; if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(history) != null) AssetDatabase.DeleteAsset(history); AssetDatabase.Refresh(); }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private static bool ByteEqual(byte[] left, byte[] right) { return left.Length == right.Length && !left.Where((value, index) => value != right[index]).Any(); }
        private static string E(string file) { return Path.Combine(VfxCohortJProtocol.EvidenceDirectory(), file); }
        private static string A(string assetPath) { return Path.Combine(Application.dataPath, assetPath.Substring("Assets/".Length)); }
        private static string Hash(byte[] bytes) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty); }
    }
}

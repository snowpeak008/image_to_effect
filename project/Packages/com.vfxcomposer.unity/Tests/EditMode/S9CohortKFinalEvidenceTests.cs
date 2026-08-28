using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using VFXComposer.Editor.Build;
using VFXComposer.Editor.Patch;
using VFXComposer.Editor.Workflow;

namespace VFXComposer.Tests.EditMode
{
    // Non-Explicit by design: after approved K dispatch, exact 3/3 complete Patch chains are required.
    public sealed class S9CohortKFinalEvidenceTests
    {
        private static string diagnostics;
        [Test] public void CohortK_RequiresExactThreeOfThreeChainsWithRealApplyHistoryAndEffect() { Assert.That(VerifyCompletedEvidence(), Is.EqualTo(3), "K final gate requires exact 3/3. " + diagnostics); var generated = Directory.GetDirectories(Path.Combine(UnityEngine.Application.dataPath, "VFX", "Generated")).Select(Path.GetFileName).ToArray(); CollectionAssert.IsSubsetOf(new[] { "fireball_2d", "fireball_3d", "slash_3d_stylized" }, generated, "Historical baselines must remain; later approved output batches may coexist."); Assert.That(generated.Where(name => name.StartsWith("s9_cohort_k_final_", StringComparison.Ordinal)).ToArray(), Is.Empty, "Cohort K verification must clean only its own transient outputs."); }
        internal static int VerifyCompletedEvidence()
        {
            diagnostics = string.Empty; var success = 0; foreach (var key in VfxCohortKProtocol.PatchKeys) { try { Chain(key); Apply(key); success++; } catch (Exception error) { diagnostics += key + ": " + error.Message + " "; TestContext.Progress.WriteLine(key + ": " + error.Message); } } return success;
        }
        private static void Chain(string key)
        {
            var attempts = Enumerable.Range(0, 3).Where(x => File.Exists(VfxCohortKProtocol.AttemptPath(key, x))).ToArray(); Need(attempts.SequenceEqual(Enumerable.Range(0, attempts.Length)) && attempts.Length > 0 && !File.Exists(VfxCohortKProtocol.AttemptPath(key, 3)), key + " attempt chain invalid."); string thread = null;
            foreach (var attempt in attempts)
            {
                var report = JObject.Parse(File.ReadAllText(VfxCohortKProtocol.ReportPath(key, attempt))); Need(report.ContainsKey("succeeded") && report.ContainsKey("detail") && report.ContainsKey("entries") && report["entries"] is JArray && (bool)report["succeeded"] == (attempt == attempts.Last()), key + " report/succeeded sequence invalid."); foreach (var entry in ((JArray)report["entries"]).Children<JObject>()) foreach (var field in new[] { "code", "severity", "path", "message", "actualValue", "allowedRange" }) Need(entry.ContainsKey(field), key + " report entry missing " + field); var witness = JObject.Parse(File.ReadAllText(VfxCohortKProtocol.TransportPath(key, attempt))); var envelope = attempt == 0 ? VfxCohortKProtocol.InitialEnvelopePath(key) : VfxCohortKProtocol.RepairEnvelopePath(key, attempt); var payload = attempt == 0 ? VfxCohortKProtocol.InitialPayloadPath(key) : VfxCohortKProtocol.RepairPayloadPath(key, attempt); var temp = attempt == 0 ? VfxCohortKProtocol.TempInitialPayloadPath(key) : VfxCohortKProtocol.TempRepairPayloadPath(key, attempt);
                foreach (var field in new[] { "question", "attempt", "agentName", "model", "reasoningEffort", "forkTurns", "threadId", "transport", "disclosure", "envelopeSha256", "payloadSha256", "tempPayloadSha256" }) Need(witness.ContainsKey(field), key + " witness missing " + field); Need((string)witness["question"] == key && (int)witness["attempt"] == attempt && (string)witness["agentName"] == "s9_k_" + key.ToLowerInvariant() && (string)witness["model"] == "gpt-5.6-terra" && (string)witness["reasoningEffort"] == "high" && (string)witness["forkTurns"] == "none" && !string.IsNullOrWhiteSpace((string)witness["threadId"]) && !string.IsNullOrWhiteSpace((string)witness["disclosure"]) && (string)witness["transport"] == (attempt == 0 ? "spawn_agent" : "followup_task"), key + " identity/transport invalid."); Need((string)witness["envelopeSha256"] == Hash(File.ReadAllBytes(envelope)) && (string)witness["payloadSha256"] == Hash(File.ReadAllBytes(payload)) && (string)witness["tempPayloadSha256"] == Hash(File.ReadAllBytes(temp)) && ByteEqual(File.ReadAllBytes(payload), File.ReadAllBytes(temp)), key + " hash pair invalid.");
                if (attempt == 0) thread = (string)witness["threadId"]; else { var repairText = VfxCohortKProtocol.Normalize(File.ReadAllText(payload)); Need((string)witness["threadId"] == thread, key + " repair changed thread."); Need(repairText.Contains(VfxCohortKProtocol.Normalize(File.ReadAllText(VfxCohortKProtocol.ReportPath(key, attempt - 1)))), key + " repair lacks full prior report."); Need(repairText.Contains(VfxCohortKProtocol.Normalize(VfxCohortKProtocol.FrozenAcceptanceOperation(key))), key + " repair lacks authoritative operation."); var prepared = JObject.Parse(File.ReadAllText(VfxCohortKProtocol.PreparedPath(key, attempt))); Need((string)prepared["EnvelopeSha256"] == Hash(File.ReadAllBytes(envelope)) && (string)prepared["PayloadSha256"] == Hash(File.ReadAllBytes(payload)) && (string)prepared["TempPayloadSha256"] == Hash(File.ReadAllBytes(temp)) && (string)prepared["PriorReportSha256"] == Hash(File.ReadAllBytes(VfxCohortKProtocol.ReportPath(key, attempt - 1))), key + " prepared repair hashes invalid."); }
            }
            Need(ByteEqual(File.ReadAllBytes(VfxCohortKProtocol.AttemptPath(key, attempts.Last())), File.ReadAllBytes(VfxCohortKProtocol.FinalPath(key))), key + " final must byte-match last attempt.");
        }
        private static void Apply(string key)
        {
            var spec = (JObject)JObject.Parse(File.ReadAllText(Path.Combine(VfxCohortKProtocol.EvidenceDirectory(), "acceptance-spec.json")))["patches"][key]; var path = "Assets/VFX/Recipes/s9_cohort_k_final_" + key.ToLowerInvariant() + ".json";
            try { File.WriteAllText(A(path), File.ReadAllText(A(VfxAiWorkflowExporter.FormalDefaultRecipePath)).Replace("\"id\": \"fireball_2d\"", "\"id\": \"s9_cohort_k_final_" + key.ToLowerInvariant() + "\"")); AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate); var result = new VfxPatchService().ApplyToAsset(path, File.ReadAllText(VfxCohortKProtocol.FinalPath(key)), (int)spec["expectedRevision"]); Need(result.IsValid && result.BeforeRevision == 1 && result.AfterRevision == 2, key + " real apply revision invalid."); var history = JArray.Parse(File.ReadAllText(A(path + VfxPatchService.HistorySuffix))); Need((int)((JObject)history.Last)["beforeRevision"] == 1 && (int)((JObject)history.Last)["afterRevision"] == 2, key + " history invalid."); JToken actual; string allowed; Need(VfxCohortKProtocol.MeetsFrozenAcceptance(key, File.ReadAllText(VfxCohortKProtocol.FinalPath(key)), File.ReadAllText(A(path)), out actual, out allowed), key + " effect invalid."); }
            finally { var recipePath = A(path); if (File.Exists(recipePath)) { var parsed = VFXComposer.Editor.Domain.VfxDomainParser.ParseRecipe(File.ReadAllText(recipePath)); if (parsed.Value != null && AssetDatabase.IsValidFolder(VfxCompiler.OutputFolder(parsed.Value))) AssetDatabase.DeleteAsset(VfxCompiler.OutputFolder(parsed.Value)); } if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path) != null) AssetDatabase.DeleteAsset(path); var h = path + VfxPatchService.HistorySuffix; if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(h) != null) AssetDatabase.DeleteAsset(h); AssetDatabase.Refresh(); }
        }
        private static string A(string asset) { return Path.Combine(UnityEngine.Application.dataPath, asset.Substring("Assets/".Length)); } private static void Need(bool value, string message) { if (!value) throw new InvalidOperationException(message); } private static bool ByteEqual(byte[] a, byte[] b) { return a.Length == b.Length && !a.Where((x, i) => x != b[i]).Any(); } private static string Hash(byte[] bytes) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty); }
    }
}

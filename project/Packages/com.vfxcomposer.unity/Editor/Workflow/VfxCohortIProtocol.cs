using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using VFXComposer.Editor.Build;
using VFXComposer.Editor.Domain;
using VFXComposer.Editor.Patch;

namespace VFXComposer.Editor.Workflow
{
    /// <summary>Short-envelope, file-backed I protocol. It records local evidence, not unavailable wire/tool-trace capture.</summary>
    public static class VfxCohortIProtocol
    {
        public static readonly string[] RecipeKeys = { "I1", "I2", "I3", "I4", "I5" };
        public static readonly string[] PatchKeys = { "P1", "P2", "P3" };
        private const string PatchBase = "Assets/VFX/Recipes/s9_cohort_i_patch_base.json";
        public static string TempRoot { get { return Path.Combine(Path.GetTempPath(), "vfxcomposer-s9-cohort-i"); } }

        [MenuItem("Tools/VFX Composer/AI Workflow/Freeze Cohort I File-backed Payloads (one-time)")]
        public static void Freeze()
        {
            var table = VfxAiWorkflowExporter.ExportFormalCatalog(); var canonical = VfxAiWorkflowExporter.ExportCanonicalRecipe(); if (table.Report.HasErrors || canonical.Report.HasErrors) throw new InvalidOperationException("Generated authoring bundle must pass before I freeze.");
            VfxAiWorkflowContractSnapshot.ExportOnce("cohort-i", "Cohort I");
            foreach (var key in RecipeKeys.Concat(PatchKeys))
            {
                var payload = InitialPayload(key); WriteExactPair(InitialPayloadPath(key), TempInitialPayloadPath(key), payload);
                WriteOnce(InitialEnvelopePath(key), Envelope(key, TempInitialPayloadPath(key), Hash(File.ReadAllBytes(InitialPayloadPath(key))), IsRecipe(key)));
            }
            WriteOnce(Evidence("initial-payloads.generated.json"), InitialManifest()); AssetDatabase.Refresh();
        }

        public static void VerifyPreDispatch()
        {
            string snapshotHash; if (!VfxAiWorkflowContractSnapshot.VerifyExisting("cohort-i", out snapshotHash)) throw new InvalidOperationException("I snapshot missing or invalid.");
            foreach (var key in RecipeKeys.Concat(PatchKeys))
            {
                if (!ByteEqual(File.ReadAllBytes(InitialPayloadPath(key)), File.ReadAllBytes(TempInitialPayloadPath(key)))) throw new InvalidOperationException("I temp/evidence initial payload differs: " + key);
                if (File.Exists(AttemptPath(key, 0)) || File.Exists(ReportPath(key, 0)) || File.Exists(TransportPath(key, 0))) throw new InvalidOperationException("I has evidence before approval: " + key);
            }
        }

        [MenuItem("Tools/VFX Composer/AI Workflow/Record Persisted Cohort I Initial Witnesses and Reports")]
        public static void RecordPersistedInitialWitnessesAndReports()
        {
            foreach (var key in RecipeKeys.Concat(PatchKeys))
            {
                if (!File.Exists(AttemptPath(key, 0)) || File.Exists(TransportPath(key, 0))) continue;
                RecordWitness(key, 0, "/root/s9_h_recovery_developer/" + AgentName(key), File.ReadAllText(InitialEnvelopePath(key)));
                RecordPersistedAttemptReport(key, 0);
            }
        }
        [MenuItem("Tools/VFX Composer/AI Workflow/Prepare Cohort I5 Repair 1 and Pause")]
        public static void PrepareI5Repair1AndPause() { PrepareRepairAndPause("I5", 1); }
        [MenuItem("Tools/VFX Composer/AI Workflow/Record I5 Repair 1 Witness and Report")]
        public static void RecordI5Repair1WitnessAndReport() { RecordWitness("I5", 1, "/root/s9_h_recovery_developer/s9_i_i5", File.ReadAllText(RepairEnvelopePath("I5", 1))); RecordPersistedAttemptReport("I5", 1); }
        [MenuItem("Tools/VFX Composer/AI Workflow/Prepare Cohort I5 Repair 2 and Pause")]
        public static void PrepareI5Repair2AndPause() { PrepareRepairAndPause("I5", 2); }
        [MenuItem("Tools/VFX Composer/AI Workflow/Record I5 Repair 2 Witness and Report")]
        public static void RecordI5Repair2WitnessAndReport() { RecordWitness("I5", 2, "/root/s9_h_recovery_developer/s9_i_i5", File.ReadAllText(RepairEnvelopePath("I5", 2))); RecordPersistedAttemptReport("I5", 2); }
        [MenuItem("Tools/VFX Composer/AI Workflow/Prepare Cohort I P1 Repair 1 and Pause")]
        public static void PrepareP1Repair1AndPause() { PrepareRepairAndPause("P1", 1); }
        [MenuItem("Tools/VFX Composer/AI Workflow/Record P1 Repair 1 and Prepare Repair 2")]
        public static void RecordP1Repair1AndPrepareRepair2() { RecordWitness("P1", 1, "/root/s9_h_recovery_developer/s9_i_p1", File.ReadAllText(RepairEnvelopePath("P1", 1))); RecordPersistedAttemptReport("P1", 1); PrepareRepairAndPause("P1", 2); }
        [MenuItem("Tools/VFX Composer/AI Workflow/Record P1 Repair 2 Witness and Report")]
        public static void RecordP1Repair2WitnessAndReport() { RecordWitness("P1", 2, "/root/s9_h_recovery_developer/s9_i_p1", File.ReadAllText(RepairEnvelopePath("P1", 2))); RecordPersistedAttemptReport("P1", 2); }
        [MenuItem("Tools/VFX Composer/AI Workflow/Record P2 Initial Witness and Report")]
        public static void RecordP2InitialWitnessAndReport() { RecordWitness("P2", 0, "/root/s9_h_recovery_developer/s9_i_p2", File.ReadAllText(InitialEnvelopePath("P2"))); RecordPersistedAttemptReport("P2", 0); }
        [MenuItem("Tools/VFX Composer/AI Workflow/Record P3 Initial Witness and Report")]
        public static void RecordP3InitialWitnessAndReport() { RecordWitness("P3", 0, "/root/s9_i_patch_finisher/s9_i_p3", File.ReadAllText(InitialEnvelopePath("P3"))); RecordPersistedAttemptReport("P3", 0); }
        [MenuItem("Tools/VFX Composer/AI Workflow/Prepare Cohort I P3 Repair 1 and Pause")]
        public static void PrepareP3Repair1AndPause() { PrepareRepairAndPause("P3", 1); }
        [MenuItem("Tools/VFX Composer/AI Workflow/Record P3 Repair 1 Witness and Report")]
        public static void RecordP3Repair1WitnessAndReport() { RecordWitness("P3", 1, "/root/s9_i_patch_finisher/s9_i_p3", File.ReadAllText(RepairEnvelopePath("P3", 1))); RecordPersistedAttemptReport("P3", 1); }
        [MenuItem("Tools/VFX Composer/AI Workflow/Prepare Cohort I P3 Repair 2 and Pause")]
        public static void PrepareP3Repair2AndPause() { PrepareRepairAndPause("P3", 2); }
        [MenuItem("Tools/VFX Composer/AI Workflow/Record P3 Repair 2 Witness and Report")]
        public static void RecordP3Repair2WitnessAndReport() { RecordWitness("P3", 2, "/root/s9_i_patch_finisher/s9_i_p3", File.ReadAllText(RepairEnvelopePath("P3", 2))); RecordPersistedAttemptReport("P3", 2); }

        public static string InitialEnvelopeForDispatch(string key) { return File.ReadAllText(InitialEnvelopePath(key)); }

        public static PreparedIRepair PrepareRepairAndPause(string key, int repairAttempt)
        {
            if (repairAttempt < 1 || repairAttempt > 2) throw new ArgumentOutOfRangeException("repairAttempt"); var previousReport = ReportPath(key, repairAttempt - 1); if (!File.Exists(previousReport)) throw new FileNotFoundException("Machine report required before repair.", previousReport);
            var payload = "Continue in this same agent/thread. Return only a complete corrected " + (IsRecipe(key) ? "Recipe JSON object" : "Patch JSON array") + ". Beyond the single payload-read exec_command authorized by the envelope, do not use any tools or workspace; do not inspect other resources. You retain the frozen contract, original requirement, and previous output; do not repeat, compress, or summarize them. Correct the reported issue(s) only.\n\nCOMPLETE PREVIOUS MACHINE REPORT (LF normalized):\n" + Normalize(File.ReadAllText(previousReport));
            var evidencePayload = RepairPayloadPath(key, repairAttempt); var tempPayload = TempRepairPayloadPath(key, repairAttempt); WriteExactPair(evidencePayload, tempPayload, payload); var hash = Hash(File.ReadAllBytes(evidencePayload)); var envelope = RepairEnvelopePath(key, repairAttempt); WriteOnce(envelope, Envelope(key, tempPayload, hash, IsRecipe(key)));
            var result = new PreparedIRepair { Key = key, Attempt = repairAttempt, AgentName = AgentName(key), EnvelopePath = envelope, EnvelopeSha256 = Hash(File.ReadAllBytes(envelope)), PayloadPath = evidencePayload, PayloadSha256 = hash, TempPayloadPath = tempPayload, TempPayloadSha256 = Hash(File.ReadAllBytes(tempPayload)), PriorReportPath = previousReport, PriorReportSha256 = Hash(File.ReadAllBytes(previousReport)) };
            WriteOnce(PreparedPath(key, repairAttempt), JObject.FromObject(result).ToString(Formatting.Indented).Replace("\r\n", "\n") + "\n"); return result;
        }

        public static void RecordWitness(string key, int attempt, string threadId, string envelopeMessage)
        {
            if (string.IsNullOrEmpty(threadId)) throw new ArgumentException("Actual thread ID is required.", "threadId");
            var envelope = attempt == 0 ? InitialEnvelopePath(key) : RepairEnvelopePath(key, attempt); var payload = attempt == 0 ? InitialPayloadPath(key) : RepairPayloadPath(key, attempt); var temp = attempt == 0 ? TempInitialPayloadPath(key) : TempRepairPayloadPath(key, attempt);
            if (!ByteEqual(File.ReadAllBytes(envelope), new UTF8Encoding(false).GetBytes(envelopeMessage ?? string.Empty))) throw new InvalidOperationException("Witnessed envelope differs from frozen file."); if (!ByteEqual(File.ReadAllBytes(payload), File.ReadAllBytes(temp))) throw new InvalidOperationException("Payload/temp bytes differ.");
            if (attempt > 0 && (string)JObject.Parse(File.ReadAllText(TransportPath(key, 0)))["threadId"] != threadId) throw new InvalidOperationException("Repair must continue same thread.");
            var record = new JObject { ["question"] = key, ["attempt"] = attempt, ["agentName"] = AgentName(key), ["model"] = "gpt-5.6-terra", ["reasoningEffort"] = "high", ["forkTurns"] = "none", ["threadId"] = threadId, ["transport"] = attempt == 0 ? "spawn_agent" : "followup_task", ["envelopeFile"] = Path.GetFileName(envelope), ["envelopeSha256"] = Hash(File.ReadAllBytes(envelope)), ["payloadFile"] = Path.GetFileName(payload), ["payloadSha256"] = Hash(File.ReadAllBytes(payload)), ["tempPayloadPath"] = temp, ["tempPayloadSha256"] = Hash(File.ReadAllBytes(temp)), ["continuity"] = attempt == 0 ? "initial isolated thread" : "same threadId as attempt 0", ["disclosure"] = "No wire payload or child tool trace readback; primary Agent witnessed the short envelope." };
            WriteOnce(TransportPath(key, attempt), record.ToString(Formatting.Indented).Replace("\r\n", "\n") + "\n");
        }

        public static void RecordPersistedAttemptReport(string key, int attempt)
        {
            if (IsRecipe(key)) RecordRecipe(key, attempt); else RecordPatch(key, attempt); AssetDatabase.Refresh();
        }

        public static string EvidenceDirectory() { return Path.Combine(RepositoryRoot(), "docs", "ai-workflow", "evidence", "cohort-i"); }
        public static string AttemptPath(string key, int attempt) { return Evidence(key + ".attempt" + attempt + "." + (IsRecipe(key) ? "recipe.json" : "patch.json")); }
        public static string ReportPath(string key, int attempt) { return Evidence(key + ".attempt" + attempt + ".report.json"); }
        public static string TransportPath(string key, int attempt) { return Evidence(key + ".attempt" + attempt + ".transport.json"); }
        public static string InitialPayloadPath(string key) { return Evidence(key + ".initial.payload.md"); }
        public static string TempInitialPayloadPath(string key) { return Path.Combine(TempRoot, key, key + ".initial.payload.md"); }
        public static string InitialEnvelopePath(string key) { return Evidence(key + ".initial.envelope.txt"); }
        public static string RepairPayloadPath(string key, int attempt) { return Evidence(key + ".repair" + attempt + ".payload.md"); }
        public static string TempRepairPayloadPath(string key, int attempt) { return Path.Combine(TempRoot, key, key + ".repair" + attempt + ".payload.md"); }
        public static string RepairEnvelopePath(string key, int attempt) { return Evidence(key + ".repair" + attempt + ".envelope.txt"); }
        public static string PreparedPath(string key, int attempt) { return Evidence(key + ".repair" + attempt + ".prepared.json"); }
        public static string Normalize(string text) { return (text ?? string.Empty).Replace("\r\n", "\n").Replace("\r", "\n"); }
        public static void WriteOnce(string path, string text) { if (File.Exists(path)) { if (Normalize(File.ReadAllText(path)) == Normalize(text)) return; throw new InvalidOperationException("Write-once collision: " + path); } Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllText(path, text, new UTF8Encoding(false)); }

        private static void RecordRecipe(string key, int attempt) { VfxBuildResult build = null; try { build = new VfxCompiler().Build(File.ReadAllText(AttemptPath(key, attempt))); WriteOnce(ReportPath(key, attempt), MachineReport(build.Plan.Report, build.Succeeded, build.PrefabPath)); } catch (Exception ex) { WriteOnce(ReportPath(key, attempt), MachineReport(new ValidationReport(), false, ex.ToString())); } finally { if (build != null && build.Succeeded) AssetDatabase.DeleteAsset(VfxCompiler.OutputFolder(VfxDomainParser.ParseRecipe(File.ReadAllText(AttemptPath(key, attempt))).Value)); } }
        private static void RecordPatch(string key, int attempt) { try { File.WriteAllText(Absolute(PatchBase), File.ReadAllText(Absolute(VfxAiWorkflowExporter.FormalDefaultRecipePath)).Replace("\"id\": \"fireball_2d\"", "\"id\": \"s9_cohort_i_patch_base\"")); AssetDatabase.ImportAsset(PatchBase, ImportAssetOptions.ForceUpdate); var result = new VfxPatchService().ApplyToAsset(PatchBase, File.ReadAllText(AttemptPath(key, attempt)), 1); WriteOnce(ReportPath(key, attempt), MachineReport(result.Report, result.IsValid, "revision " + result.BeforeRevision + "->" + result.AfterRevision)); } catch (Exception ex) { WriteOnce(ReportPath(key, attempt), MachineReport(new ValidationReport(), false, ex.ToString())); } finally { CleanupPatch(); } }
        private static string InitialPayload(string key) { return "ISOLATION AND OUTPUT REQUIREMENTS:\nYou are an isolated authoring agent. Beyond the single payload-read exec_command authorized by the envelope, do not use any tools or workspace; do not inspect other resources. Return only one complete raw " + (IsRecipe(key) ? "Recipe JSON object" : "Patch JSON array") + "; do not use Markdown, prose, comments, or a fence.\n\nFROZEN CONTRACT SNAPSHOT:\n" + File.ReadAllText(Evidence("contract-snapshot.md")) + "\n\nORIGINAL PREREGISTERED REQUIREMENT:\n" + PromptFor(key) + "\n"; }
        private static string Envelope(string key, string tempPayload, string hash, bool recipe) { return "You are isolated `gpt-5.6-terra` at `high` reasoning. Use exactly one `exec_command` tool call and no other tool. That one command may read only the exact absolute file `" + tempPayload + "` and verify its SHA-256 equals `" + hash + "`; do not read any other file, directory, workspace, or network resource. After verification, return only the raw " + (recipe ? "Recipe JSON object" : "Patch JSON array") + " contained by that payload's instructions: no prose or Markdown.\n"; }
        private static string InitialManifest() { var entries = new JObject(); foreach (var key in RecipeKeys.Concat(PatchKeys)) entries[key] = new JObject { ["envelopeFile"] = Path.GetFileName(InitialEnvelopePath(key)), ["envelopeSha256"] = Hash(File.ReadAllBytes(InitialEnvelopePath(key))), ["payloadFile"] = Path.GetFileName(InitialPayloadPath(key)), ["payloadSha256"] = Hash(File.ReadAllBytes(InitialPayloadPath(key))), ["tempPayloadPath"] = TempInitialPayloadPath(key), ["tempPayloadSha256"] = Hash(File.ReadAllBytes(TempInitialPayloadPath(key))) }; string snapshot; VfxAiWorkflowContractSnapshot.VerifyExisting("cohort-i", out snapshot); return new JObject { ["contractSha256"] = snapshot, ["tempRoot"] = TempRoot, ["initialPayloads"] = entries }.ToString(Formatting.Indented).Replace("\r\n", "\n") + "\n"; }
        private static string PromptFor(string key) { var text = File.ReadAllText(Evidence("prompts.md")); var marker = "## " + key + "\n\n"; var start = text.IndexOf(marker, StringComparison.Ordinal) + marker.Length; var end = text.IndexOf("\n## ", start, StringComparison.Ordinal); return (end < 0 ? text.Substring(start) : text.Substring(start, end - start)).TrimEnd(); }
        private static void WriteExactPair(string evidence, string temp, string content) { WriteOnce(evidence, content); WriteOnce(temp, content); if (!ByteEqual(File.ReadAllBytes(evidence), File.ReadAllBytes(temp))) throw new InvalidOperationException("Temp/evidence pair mismatch."); }
        private static bool IsRecipe(string key) { return RecipeKeys.Contains(key, StringComparer.Ordinal); }
        private static string AgentName(string key) { return "s9_i_" + key.ToLowerInvariant(); }
        private static string Evidence(string file) { return Path.Combine(EvidenceDirectory(), file); }
        private static string Absolute(string assetPath) { return Path.Combine(UnityEngine.Application.dataPath, assetPath.Substring("Assets/".Length)); }
        private static string RepositoryRoot() { return Directory.GetParent(Directory.GetParent(UnityEngine.Application.dataPath).FullName).FullName; }
        private static bool ByteEqual(byte[] a, byte[] b) { return a.Length == b.Length && !a.Where((value, index) => value != b[index]).Any(); }
        private static string Hash(byte[] bytes) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty); }
        private static string MachineReport(ValidationReport report, bool succeeded, string detail) { var entries = new JArray(report.Entries.Select(item => new JObject { ["code"] = item.Code, ["severity"] = item.Severity.ToString().ToLowerInvariant(), ["path"] = item.Path, ["message"] = item.Message, ["actualValue"] = item.ActualValue == null ? JValue.CreateNull() : item.ActualValue.DeepClone(), ["allowedRange"] = item.AllowedRange == null ? JValue.CreateNull() : new JValue(item.AllowedRange) })); return new JObject { ["succeeded"] = succeeded, ["detail"] = detail, ["entries"] = entries }.ToString(Formatting.Indented).Replace("\r\n", "\n") + "\n"; }
        private static void CleanupPatch() { if (AssetDatabase.IsValidFolder(VfxCompiler.GeneratedRoot + "/s9_cohort_i_patch_base")) AssetDatabase.DeleteAsset(VfxCompiler.GeneratedRoot + "/s9_cohort_i_patch_base"); if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(PatchBase) != null) AssetDatabase.DeleteAsset(PatchBase); var history = PatchBase + VfxPatchService.HistorySuffix; if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(history) != null) AssetDatabase.DeleteAsset(history); AssetDatabase.Refresh(); }
    }
    [Serializable] public sealed class PreparedIRepair { public string Key; public int Attempt; public string AgentName; public string EnvelopePath; public string EnvelopeSha256; public string PayloadPath; public string PayloadSha256; public string TempPayloadPath; public string TempPayloadSha256; public string PriorReportPath; public string PriorReportSha256; }
}

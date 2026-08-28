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
    /// <summary>Patch-only J preregistration. Freeze prepares documents and transport inputs; it never creates runtime evidence.</summary>
    public static class VfxCohortJProtocol
    {
        public static readonly string[] PatchKeys = { "J1", "J2", "J3" };
        public static string TempRoot { get { return Path.Combine(Path.GetTempPath(), "vfxcomposer-s9-cohort-j"); } }

        [MenuItem("Tools/VFX Composer/AI Workflow/Freeze Cohort J Patch-only Payloads (one-time)")]
        public static void Freeze()
        {
            var table = VfxAiWorkflowExporter.ExportFormalCatalog(); var recipe = VfxAiWorkflowExporter.ExportCanonicalRecipe(); var patches = VfxAiWorkflowExporter.ExportCanonicalPatches();
            if (table.Report.HasErrors || recipe.Report.HasErrors || patches.Report.HasErrors) throw new InvalidOperationException("Generated Patch-only authoring bundle must pass before J freeze.");
            foreach (var key in PatchKeys) { var payload = InitialPayload(key); WriteExactPair(InitialPayloadPath(key), TempInitialPayloadPath(key), payload); WriteOnce(InitialEnvelopePath(key), Envelope(TempInitialPayloadPath(key), Hash(File.ReadAllBytes(InitialPayloadPath(key))))); }
            WriteOnce(Evidence("initial-payloads.generated.json"), InitialManifest()); VerifyPreDispatch(); AssetDatabase.Refresh();
        }

        public static void VerifyPreDispatch()
        {
            foreach (var key in PatchKeys)
            {
                var payload = InitialPayloadPath(key); var temp = TempInitialPayloadPath(key);
                if (!File.Exists(payload) || !File.Exists(temp) || !ByteEqual(File.ReadAllBytes(payload), File.ReadAllBytes(temp))) throw new InvalidOperationException("J initial payload pair is missing or differs: " + key);
                var files = Directory.GetFiles(Path.GetDirectoryName(temp)); if (files.Length != 1 || files[0] != temp) throw new InvalidOperationException("J temp directory must contain only its initial payload before dispatch: " + key);
                if (File.Exists(AttemptPath(key, 0)) || File.Exists(ReportPath(key, 0)) || File.Exists(FinalPath(key)) || File.Exists(TransportPath(key, 0))) throw new InvalidOperationException("J has runtime evidence before dispatch: " + key);
            }
        }

        public static void RecordInitialWitnessAndReport(string key, string threadId, string envelopeMessage)
        {
            RecordWitness(key, 0, threadId, envelopeMessage); RecordPersistedAttemptReport(key, 0);
        }

        public static PreparedJRepair PrepareRepairAndPause(string key, int repairAttempt)
        {
            if (!PatchKeys.Contains(key, StringComparer.Ordinal) || repairAttempt < 1 || repairAttempt > 2) throw new ArgumentOutOfRangeException("repairAttempt");
            var priorReport = ReportPath(key, repairAttempt - 1); if (!File.Exists(priorReport)) throw new FileNotFoundException("Machine report required before repair.", priorReport);
            var prior = JObject.Parse(File.ReadAllText(priorReport)); if (!prior.ContainsKey("succeeded") || prior["succeeded"].Type != JTokenType.Boolean) throw new InvalidOperationException("Machine report must declare succeeded before a repair can be prepared."); if ((bool)prior["succeeded"]) throw new InvalidOperationException("Patch already succeeded; a repair must not be prepared.");
            var payload = "Continue in this same agent/thread. Return only one complete corrected Patch JSON array. Beyond the one payload-read exec_command authorized by the envelope, do not use tools, the workspace, or any other resource. Correct only the reported issue(s).\n\nCOMPLETE PREVIOUS MACHINE REPORT (LF normalized):\n" + Normalize(File.ReadAllText(priorReport)) + "\nFROZEN ACCEPTANCE OPERATION (authoritative complete bare array):\n" + FrozenAcceptanceOperation(key) + "\nReturn that authoritative complete array for the stated requirement; do not copy a canonical example.\n";
            WriteExactPair(RepairPayloadPath(key, repairAttempt), TempRepairPayloadPath(key, repairAttempt), payload); var envelope = RepairEnvelopePath(key, repairAttempt); WriteOnce(envelope, Envelope(TempRepairPayloadPath(key, repairAttempt), Hash(File.ReadAllBytes(RepairPayloadPath(key, repairAttempt)))));
            var prepared = new PreparedJRepair { Key = key, Attempt = repairAttempt, AgentName = AgentName(key), EnvelopePath = envelope, EnvelopeSha256 = Hash(File.ReadAllBytes(envelope)), PayloadPath = RepairPayloadPath(key, repairAttempt), PayloadSha256 = Hash(File.ReadAllBytes(RepairPayloadPath(key, repairAttempt))), TempPayloadPath = TempRepairPayloadPath(key, repairAttempt), TempPayloadSha256 = Hash(File.ReadAllBytes(TempRepairPayloadPath(key, repairAttempt))), PriorReportPath = priorReport, PriorReportSha256 = Hash(File.ReadAllBytes(priorReport)) };
            WriteOnce(PreparedPath(key, repairAttempt), JObject.FromObject(prepared).ToString(Formatting.Indented).Replace("\r\n", "\n") + "\n"); return prepared;
        }

        public static void RecordRepairWitnessAndReport(string key, int attempt, string threadId, string envelopeMessage)
        {
            RecordWitness(key, attempt, threadId, envelopeMessage); RecordPersistedAttemptReport(key, attempt);
        }

        public static void RecordWitness(string key, int attempt, string threadId, string envelopeMessage)
        {
            if (!PatchKeys.Contains(key, StringComparer.Ordinal) || string.IsNullOrWhiteSpace(threadId)) throw new ArgumentException("Known J key and actual thread ID are required.");
            var envelope = attempt == 0 ? InitialEnvelopePath(key) : RepairEnvelopePath(key, attempt); var payload = attempt == 0 ? InitialPayloadPath(key) : RepairPayloadPath(key, attempt); var temp = attempt == 0 ? TempInitialPayloadPath(key) : TempRepairPayloadPath(key, attempt);
            if (!ByteEqual(File.ReadAllBytes(envelope), new UTF8Encoding(false).GetBytes(envelopeMessage ?? string.Empty))) throw new InvalidOperationException("Witnessed envelope differs from its frozen file.");
            if (!ByteEqual(File.ReadAllBytes(payload), File.ReadAllBytes(temp))) throw new InvalidOperationException("J payload/temp bytes differ.");
            if (attempt > 0 && (string)JObject.Parse(File.ReadAllText(TransportPath(key, 0)))["threadId"] != threadId) throw new InvalidOperationException("J repair must continue in the initial thread.");
            var witness = new JObject { ["question"] = key, ["attempt"] = attempt, ["agentName"] = AgentName(key), ["model"] = "gpt-5.6-terra", ["reasoningEffort"] = "high", ["forkTurns"] = "none", ["threadId"] = threadId, ["transport"] = attempt == 0 ? "spawn_agent" : "followup_task", ["envelopeFile"] = Path.GetFileName(envelope), ["envelopeSha256"] = Hash(File.ReadAllBytes(envelope)), ["payloadFile"] = Path.GetFileName(payload), ["payloadSha256"] = Hash(File.ReadAllBytes(payload)), ["tempPayloadPath"] = temp, ["tempPayloadSha256"] = Hash(File.ReadAllBytes(temp)), ["continuity"] = attempt == 0 ? "initial isolated thread" : "same threadId as attempt 0", ["disclosure"] = "No wire payload or child tool trace readback; primary Agent witnessed the short envelope." };
            WriteOnce(TransportPath(key, attempt), witness.ToString(Formatting.Indented).Replace("\r\n", "\n") + "\n");
        }

        public static void RecordPersistedAttemptReport(string key, int attempt)
        {
            const string basePath = "Assets/VFX/Recipes/s9_cohort_j_patch_base.json";
            try
            {
                File.WriteAllText(Absolute(basePath), File.ReadAllText(Absolute(VfxAiWorkflowExporter.FormalDefaultRecipePath)).Replace("\"id\": \"fireball_2d\"", "\"id\": \"s9_cohort_j_patch_base\"")); AssetDatabase.ImportAsset(basePath, ImportAssetOptions.ForceUpdate);
                var patch = File.ReadAllText(AttemptPath(key, attempt)); var result = new VfxPatchService().ApplyToAsset(basePath, patch, 1); var applied = result.IsValid; JToken actual = null; string allowed = null; var accepted = applied && MeetsFrozenAcceptance(key, patch, File.ReadAllText(Absolute(basePath)), out actual, out allowed);
                if (applied && !accepted) result.Report.Add("E720", ValidationSeverity.Error, (string)Acceptance(key)["path"], "Patch applied but does not satisfy the frozen Cohort J acceptance operation and effect.", actual, allowed);
                WriteOnce(ReportPath(key, attempt), MachineReport(result.Report, applied && accepted, "revision " + result.BeforeRevision + "->" + result.AfterRevision + (applied && !accepted ? "; frozen acceptance mismatch" : string.Empty)));
            }
            catch (Exception exception) { WriteOnce(ReportPath(key, attempt), MachineReport(new ValidationReport(), false, exception.ToString())); }
            finally { CleanupPatchBase(basePath); AssetDatabase.Refresh(); }
        }

        /// <summary>Checks the frozen operation and persisted effect, so a legal but wrong Patch remains repairable evidence.</summary>
        public static bool MeetsFrozenAcceptance(string key, string patchJson, string patchedRecipeJson, out JToken actualValue, out string allowedRange)
        {
            var acceptance = Acceptance(key); var expected = ExpectedOperation(acceptance); allowedRange = expected.ToString(Formatting.None); actualValue = null;
            JArray actualPatch;
            try { actualPatch = JArray.Parse(patchJson); }
            catch { actualValue = new JValue(patchJson ?? string.Empty); return false; }
            actualValue = actualPatch;
            if (!JToken.DeepEquals(actualPatch, expected)) return false;
            try
            {
                var recipe = JObject.Parse(patchedRecipeJson); var path = ((string)acceptance["path"]).Split('/'); var stage = recipe["stages"].Children<JObject>().Single(item => (string)item["id"] == path[2]); var module = stage["modules"].Children<JObject>().SingleOrDefault(item => (string)item["id"] == path[4]);
                if ((string)acceptance["operation"] == "replace") return module != null && JToken.DeepEquals(module["parameters"][path[6]], acceptance["value"]);
                if ((string)acceptance["operation"] == "disable") return module != null && JToken.DeepEquals(module["enabled"], acceptance["enabled"]);
                return module != null && JToken.DeepEquals(module, acceptance["module"]);
            }
            catch { return false; }
        }

        public static string FrozenAcceptanceOperation(string key) { return ExpectedOperation(Acceptance(key)).ToString(Formatting.Indented); }

        public static string EvidenceDirectory() { return Path.Combine(RepositoryRoot(), "docs", "ai-workflow", "evidence", "cohort-j"); }
        public static string AttemptPath(string key, int attempt) { return Evidence(key + ".attempt" + attempt + ".patch.json"); }
        public static string ReportPath(string key, int attempt) { return Evidence(key + ".attempt" + attempt + ".report.json"); }
        public static string TransportPath(string key, int attempt) { return Evidence(key + ".attempt" + attempt + ".transport.json"); }
        public static string FinalPath(string key) { return Evidence(key + ".final.patch.json"); }
        public static string InitialPayloadPath(string key) { return Evidence(key + ".initial.payload.md"); }
        public static string TempInitialPayloadPath(string key) { return Path.Combine(TempRoot, key, key + ".initial.payload.md"); }
        public static string InitialEnvelopePath(string key) { return Evidence(key + ".initial.envelope.txt"); }
        public static string RepairPayloadPath(string key, int attempt) { return Evidence(key + ".repair" + attempt + ".payload.md"); }
        public static string TempRepairPayloadPath(string key, int attempt) { return Path.Combine(TempRoot, key, key + ".repair" + attempt + ".payload.md"); }
        public static string RepairEnvelopePath(string key, int attempt) { return Evidence(key + ".repair" + attempt + ".envelope.txt"); }
        public static string PreparedPath(string key, int attempt) { return Evidence(key + ".repair" + attempt + ".prepared.json"); }
        public static string Normalize(string text) { return (text ?? string.Empty).Replace("\r\n", "\n").Replace("\r", "\n"); }
        public static void WriteOnce(string path, string text) { if (File.Exists(path)) { if (Normalize(File.ReadAllText(path)) == Normalize(text)) return; throw new InvalidOperationException("Write-once collision: " + path); } Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllText(path, text, new UTF8Encoding(false)); }

        private static string InitialPayload(string key)
        {
            return "ISOLATION AND OUTPUT REQUIREMENTS:\nYou are an isolated Patch-only authoring agent. Beyond the one payload-read exec_command authorized by the envelope, do not use any tools or workspace; do not inspect other resources. Return only one complete raw Patch JSON array; no Markdown, prose, comments, or fence.\n\nPATCH-ONLY AUTHORING BUNDLE:\n<!-- BEGIN patch-authoring.md -->\n" + File.ReadAllText(Workflow("patch-authoring.md")) + "\n<!-- END patch-authoring.md -->\n<!-- BEGIN canonical-recipe.generated.json -->\n" + File.ReadAllText(Workflow("canonical-recipe.generated.json")) + "\n<!-- END canonical-recipe.generated.json -->\n<!-- BEGIN canonical-patches.generated.md -->\n" + File.ReadAllText(Workflow("canonical-patches.generated.md")) + "\n<!-- END canonical-patches.generated.md -->\n<!-- BEGIN template-parameters.generated.md -->\n" + File.ReadAllText(Workflow("template-parameters.generated.md")) + "\n<!-- END template-parameters.generated.md -->\n\nORIGINAL PREREGISTERED REQUIREMENT:\n" + PromptFor(key) + "\n";
        }
        private static string Envelope(string tempPayload, string hash) { return "You are isolated `gpt-5.6-terra` at `high` reasoning. Use exactly one `exec_command` tool call and no other tool. That one command may read only the exact absolute file `" + tempPayload + "` and verify its SHA-256 equals `" + hash + "`; do not read any other file, directory, workspace, or network resource. After verification, return only the raw Patch JSON array contained by that payload's instructions: no prose or Markdown.\n"; }
        private static string InitialManifest()
        {
            var entries = new JObject(); foreach (var key in PatchKeys) entries[key] = new JObject { ["envelopeFile"] = Path.GetFileName(InitialEnvelopePath(key)), ["envelopeSha256"] = Hash(File.ReadAllBytes(InitialEnvelopePath(key))), ["payloadFile"] = Path.GetFileName(InitialPayloadPath(key)), ["payloadSha256"] = Hash(File.ReadAllBytes(InitialPayloadPath(key))), ["tempPayloadPath"] = TempInitialPayloadPath(key), ["tempPayloadSha256"] = Hash(File.ReadAllBytes(TempInitialPayloadPath(key))) };
            return new JObject { ["protocol"] = "cohort-j-patch-only", ["tempRoot"] = TempRoot, ["initialPayloads"] = entries }.ToString(Formatting.Indented).Replace("\r\n", "\n") + "\n";
        }
        private static string PromptFor(string key) { var text = File.ReadAllText(Evidence("prompts.md")); var marker = "## " + key + "\n\n"; var start = text.IndexOf(marker, StringComparison.Ordinal) + marker.Length; var end = text.IndexOf("\n## ", start, StringComparison.Ordinal); return (end < 0 ? text.Substring(start) : text.Substring(start, end - start)).TrimEnd(); }
        private static JObject Acceptance(string key) { if (!PatchKeys.Contains(key, StringComparer.Ordinal)) throw new ArgumentException("Known J key is required.", "key"); return (JObject)JObject.Parse(File.ReadAllText(Evidence("acceptance-spec.json")))["patches"][key]; }
        private static JArray ExpectedOperation(JObject acceptance) { var operation = new JObject { ["op"] = acceptance["operation"], ["path"] = acceptance["path"] }; if ((string)acceptance["operation"] == "replace") operation["value"] = acceptance["value"]; else if ((string)acceptance["operation"] == "add") operation["value"] = acceptance["module"]; return new JArray(operation); }
        private static void WriteExactPair(string evidence, string temp, string content) { WriteOnce(evidence, content); WriteOnce(temp, content); if (!ByteEqual(File.ReadAllBytes(evidence), File.ReadAllBytes(temp))) throw new InvalidOperationException("J temp/evidence pair mismatch."); }
        private static string AgentName(string key) { return "s9_j_" + key.ToLowerInvariant(); }
        private static string Evidence(string file) { return Path.Combine(EvidenceDirectory(), file); }
        private static string Workflow(string file) { return Path.Combine(RepositoryRoot(), "docs", "ai-workflow", file); }
        private static string Absolute(string assetPath) { return Path.Combine(UnityEngine.Application.dataPath, assetPath.Substring("Assets/".Length)); }
        private static string RepositoryRoot() { return Directory.GetParent(Directory.GetParent(UnityEngine.Application.dataPath).FullName).FullName; }
        private static bool ByteEqual(byte[] a, byte[] b) { return a.Length == b.Length && !a.Where((value, index) => value != b[index]).Any(); }
        private static string Hash(byte[] bytes) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty); }
        private static string MachineReport(ValidationReport report, bool succeeded, string detail) { var entries = new JArray(report.Entries.Select(item => new JObject { ["code"] = item.Code, ["severity"] = item.Severity.ToString().ToLowerInvariant(), ["path"] = item.Path, ["message"] = item.Message, ["actualValue"] = item.ActualValue == null ? JValue.CreateNull() : item.ActualValue.DeepClone(), ["allowedRange"] = item.AllowedRange == null ? JValue.CreateNull() : new JValue(item.AllowedRange) })); return new JObject { ["succeeded"] = succeeded, ["detail"] = detail, ["entries"] = entries }.ToString(Formatting.Indented).Replace("\r\n", "\n") + "\n"; }
        private static void CleanupPatchBase(string path) { if (AssetDatabase.IsValidFolder(VfxCompiler.GeneratedRoot + "/s9_cohort_j_patch_base")) AssetDatabase.DeleteAsset(VfxCompiler.GeneratedRoot + "/s9_cohort_j_patch_base"); if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path) != null) AssetDatabase.DeleteAsset(path); var history = path + VfxPatchService.HistorySuffix; if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(history) != null) AssetDatabase.DeleteAsset(history); }
    }
    [Serializable] public sealed class PreparedJRepair { public string Key; public int Attempt; public string AgentName; public string EnvelopePath; public string EnvelopeSha256; public string PayloadPath; public string PayloadSha256; public string TempPayloadPath; public string TempPayloadSha256; public string PriorReportPath; public string PriorReportSha256; }
}

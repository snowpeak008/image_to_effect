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
    /// <summary>File-first protocol for H. Local evidence is auditable; the host offers no wire-message readback.</summary>
    public static class VfxCohortHProtocol
    {
        public static readonly string[] RecipeKeys = { "H1", "H2", "H3", "H4", "H5" };
        public static readonly string[] PatchKeys = { "P1", "P2", "P3" };
        private const string PatchBase = "Assets/VFX/Recipes/s9_cohort_h_patch_base.json";

        [MenuItem("Tools/VFX Composer/AI Workflow/Freeze Cohort H Contract and Initial Payloads (one-time)")]
        public static void FreezeContractAndInitialPayloads()
        {
            var table = VfxAiWorkflowExporter.ExportFormalCatalog(); var canonical = VfxAiWorkflowExporter.ExportCanonicalRecipe();
            if (table.Report.HasErrors || canonical.Report.HasErrors) throw new InvalidOperationException("Cannot freeze Cohort H before the generated canonical Recipe and parameter table are valid.");
            VfxAiWorkflowContractSnapshot.ExportOnce("cohort-h", "Cohort H");
            foreach (var key in RecipeKeys.Concat(PatchKeys)) WriteOnce(InitialPayloadPath(key), InitialPayload(key));
            WriteOnce(Evidence("initial-payloads.generated.json"), InitialPayloadManifest());
            AssetDatabase.Refresh();
        }

        [MenuItem("Tools/VFX Composer/AI Workflow/Verify Cohort H Preregistration")]
        public static void VerifyPreregistration()
        {
            string hash;
            if (!VfxAiWorkflowContractSnapshot.VerifyExisting("cohort-h", out hash)) throw new InvalidOperationException("Cohort H contract snapshot is missing or invalid.");
            foreach (var key in RecipeKeys.Concat(PatchKeys))
            {
                if (!File.Exists(InitialPayloadPath(key))) throw new InvalidOperationException("H initial payload is missing: " + key);
                if (File.Exists(AttemptPath(key, 0)) || File.Exists(ReportPath(key, 0)) || File.Exists(TransportPath(key, 0))) throw new InvalidOperationException("H attempt=0 must not exist before approval: " + key);
            }
            UnityEngine.Debug.Log("Cohort H preregistration verified: " + hash);
        }

        public static string InitialPayloadForDispatch(string key) { return File.ReadAllText(InitialPayloadPath(key)); }

        /// <summary>Write this file and its hashes before asking the primary Agent to send a same-thread follow-up.</summary>
        public static PreparedRepair PrepareRepairAndPause(string key, int repairAttempt)
        {
            if (repairAttempt < 1 || repairAttempt > 2) throw new ArgumentOutOfRangeException("repairAttempt");
            var reportPath = ReportPath(key, repairAttempt - 1); if (!File.Exists(reportPath)) throw new FileNotFoundException("Previous machine report is required before repair preparation.", reportPath);
            var prompt = BuildRepairPayload(IsRecipe(key), File.ReadAllText(reportPath));
            var promptPath = RepairPayloadPath(key, repairAttempt); WriteOnce(promptPath, prompt);
            var prepared = new PreparedRepair { Key = key, Attempt = repairAttempt, AgentName = AgentName(key), PromptPath = promptPath, PromptSha256 = Hash(File.ReadAllBytes(promptPath)), PriorReportPath = reportPath, PriorReportSha256 = Hash(File.ReadAllBytes(reportPath)) };
            WriteOnce(PreparedPath(key, repairAttempt), JObject.FromObject(prepared).ToString(Formatting.Indented).Replace("\r\n", "\n") + "\n");
            return prepared;
        }

        /// <summary>
        /// After the primary Agent witnesses the dispatch, record its asserted local equivalence. This deliberately
        /// does not claim wire capture; outgoingMessage must still byte-equal the prepared local payload.
        /// </summary>
        public static void RecordWitnessedTransport(string key, int attempt, string threadId, string outgoingMessage)
        {
            if (string.IsNullOrEmpty(threadId)) throw new ArgumentException("Actual thread ID is required.", "threadId");
            var payload = attempt == 0 ? InitialPayloadPath(key) : RepairPayloadPath(key, attempt);
            if (!File.Exists(payload) || !ByteEqual(File.ReadAllBytes(payload), new UTF8Encoding(false).GetBytes(outgoingMessage ?? string.Empty))) throw new InvalidOperationException("The asserted message does not byte-equal the prepared local payload.");
            if (attempt > 0)
            {
                var initial = JObject.Parse(File.ReadAllText(TransportPath(key, 0)));
                if (!string.Equals((string)initial["threadId"], threadId, StringComparison.Ordinal)) throw new InvalidOperationException("A repair must use the exact initial thread ID.");
            }
            var record = new JObject { ["question"] = key, ["attempt"] = attempt, ["agentName"] = AgentName(key), ["model"] = "gpt-5.6-terra", ["reasoningEffort"] = "high", ["forkTurns"] = "none", ["threadId"] = threadId, ["transport"] = attempt == 0 ? "spawn_agent" : "followup_task", ["payloadFile"] = Path.GetFileName(payload), ["payloadSha256"] = Hash(File.ReadAllBytes(payload)), ["continuity"] = attempt == 0 ? "initial isolated agent/thread" : "same threadId as attempt 0", ["wireReadback"] = "unavailable; primary Agent witnessed local payload before dispatch" };
            WriteOnce(TransportPath(key, attempt), record.ToString(Formatting.Indented).Replace("\r\n", "\n") + "\n");
        }

        /// <summary>Run only after an agent response is already persisted. Recipe records call real Build; Patch records call real Apply on an isolated revision-1 asset.</summary>
        public static void RecordPersistedAttemptReport(string key, int attempt)
        {
            if (IsRecipe(key)) RecordRecipe(key, attempt); else RecordPatch(key, attempt); AssetDatabase.Refresh();
        }

        public static string EvidenceDirectory() { return Path.Combine(RepositoryRoot(), "docs", "ai-workflow", "evidence", "cohort-h"); }
        public static string AttemptPath(string key, int attempt) { return Evidence(key + ".attempt" + attempt + "." + (IsRecipe(key) ? "recipe.json" : "patch.json")); }
        public static string ReportPath(string key, int attempt) { return Evidence(key + ".attempt" + attempt + ".report.json"); }
        public static string TransportPath(string key, int attempt) { return Evidence(key + ".attempt" + attempt + ".transport.json"); }
        public static string RepairPayloadPath(string key, int attempt) { return Evidence(key + ".repair" + attempt + ".prompt.md"); }
        public static string PreparedPath(string key, int attempt) { return Evidence(key + ".repair" + attempt + ".prepared.json"); }
        public static string InitialPayloadPath(string key) { return Evidence(key + ".initial.prompt.md"); }
        public static string NormalizeNewlines(string text) { return (text ?? string.Empty).Replace("\r\n", "\n").Replace("\r", "\n"); }
        public static string BuildRepairPayload(bool recipe, string previousMachineReport)
        {
            return "Continue in this same agent/thread. Return only a complete corrected " + (recipe ? "Recipe JSON object" : "Patch JSON array") + ". Do not use tools or workspace. You already retain the frozen contract, original requirement, and previous output in this thread; do not repeat, compress, or summarize them. Correct the reported issue(s) only.\n\nCOMPLETE PREVIOUS MACHINE REPORT (LF normalized):\n" + NormalizeNewlines(previousMachineReport);
        }
        public static void WriteOnce(string path, string text)
        {
            if (File.Exists(path))
            {
                if (string.Equals(NormalizeNewlines(File.ReadAllText(path)), NormalizeNewlines(text), StringComparison.Ordinal)) return;
                throw new InvalidOperationException("Cohort H evidence is write-once and differs: " + path);
            }
            Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllText(path, text, new UTF8Encoding(false));
        }

        private static void RecordRecipe(string key, int attempt)
        {
            VfxBuildResult build = null;
            try { build = new VfxCompiler().Build(File.ReadAllText(AttemptPath(key, attempt))); WriteOnce(ReportPath(key, attempt), MachineReport(build.Plan.Report, build.Succeeded, build.PrefabPath)); }
            catch (Exception exception) { WriteOnce(ReportPath(key, attempt), MachineReport(new ValidationReport(), false, exception.ToString())); }
            finally { if (build != null && build.Succeeded && !string.IsNullOrEmpty(build.PrefabPath)) AssetDatabase.DeleteAsset(VfxCompiler.OutputFolder(VfxDomainParser.ParseRecipe(File.ReadAllText(AttemptPath(key, attempt))).Value)); }
        }
        private static void RecordPatch(string key, int attempt)
        {
            try
            {
                File.WriteAllText(Absolute(PatchBase), File.ReadAllText(Absolute(VfxAiWorkflowExporter.FormalDefaultRecipePath)).Replace("\"id\": \"fireball_2d\"", "\"id\": \"s9_cohort_h_patch_base\"")); AssetDatabase.ImportAsset(PatchBase, ImportAssetOptions.ForceUpdate);
                var result = new VfxPatchService().ApplyToAsset(PatchBase, File.ReadAllText(AttemptPath(key, attempt)), 1); WriteOnce(ReportPath(key, attempt), MachineReport(result.Report, result.IsValid, "revision " + result.BeforeRevision + "->" + result.AfterRevision));
            }
            catch (Exception exception) { WriteOnce(ReportPath(key, attempt), MachineReport(new ValidationReport(), false, exception.ToString())); }
            finally { CleanupPatch(); }
        }
        private static string InitialPayload(string key)
        {
            return "ISOLATION AND OUTPUT REQUIREMENTS:\nYou are an isolated authoring agent. Do not use tools or workspace. Return only one complete raw " + (IsRecipe(key) ? "Recipe JSON object" : "Patch JSON array") + "; do not use Markdown, prose, comments, or a fence.\n\nFROZEN CONTRACT SNAPSHOT:\n" + File.ReadAllText(Evidence("contract-snapshot.md")) + "\n\nORIGINAL PREREGISTERED REQUIREMENT:\n" + PromptFor(key) + "\n";
        }
        private static string InitialPayloadManifest()
        {
            var entries = new JObject(); foreach (var key in RecipeKeys.Concat(PatchKeys)) entries[key] = new JObject { ["payloadFile"] = Path.GetFileName(InitialPayloadPath(key)), ["payloadSha256"] = Hash(File.ReadAllBytes(InitialPayloadPath(key))), ["agentName"] = AgentName(key), ["transport"] = "spawn_agent" };
            return new JObject { ["contractSha256"] = SnapshotHash(), ["initialPayloads"] = entries }.ToString(Formatting.Indented).Replace("\r\n", "\n") + "\n";
        }
        private static string PromptFor(string key)
        {
            var text = File.ReadAllText(Evidence("prompts.md")); var marker = "## " + key + "\n\n"; var start = text.IndexOf(marker, StringComparison.Ordinal); if (start < 0) throw new InvalidOperationException("Missing preregistered prompt " + key); start += marker.Length;
            var end = text.IndexOf("\n## ", start, StringComparison.Ordinal); return (end < 0 ? text.Substring(start) : text.Substring(start, end - start)).TrimEnd();
        }
        private static string SnapshotHash() { string hash; if (!VfxAiWorkflowContractSnapshot.VerifyExisting("cohort-h", out hash)) throw new InvalidOperationException("Invalid H snapshot."); return hash; }
        private static bool IsRecipe(string key) { return RecipeKeys.Contains(key, StringComparer.Ordinal); }
        private static string AgentName(string key) { return "s9_h_" + key.ToLowerInvariant(); }
        private static string Evidence(string file) { return Path.Combine(EvidenceDirectory(), file); }
        private static string Absolute(string assetPath) { return Path.Combine(UnityEngine.Application.dataPath, assetPath.Substring("Assets/".Length)); }
        private static string RepositoryRoot() { return Directory.GetParent(Directory.GetParent(UnityEngine.Application.dataPath).FullName).FullName; }
        private static bool ByteEqual(byte[] left, byte[] right) { return left.Length == right.Length && !left.Where((value, index) => value != right[index]).Any(); }
        private static string Hash(byte[] bytes) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty); }
        private static string MachineReport(ValidationReport report, bool succeeded, string detail)
        {
            var entries = new JArray(report.Entries.Select(entry => new JObject { ["code"] = entry.Code, ["severity"] = entry.Severity.ToString().ToLowerInvariant(), ["path"] = entry.Path, ["message"] = entry.Message, ["actualValue"] = entry.ActualValue == null ? JValue.CreateNull() : entry.ActualValue.DeepClone(), ["allowedRange"] = entry.AllowedRange == null ? JValue.CreateNull() : new JValue(entry.AllowedRange) }));
            return new JObject { ["succeeded"] = succeeded, ["detail"] = detail == null ? JValue.CreateNull() : new JValue(detail), ["entries"] = entries }.ToString(Formatting.Indented).Replace("\r\n", "\n") + "\n";
        }
        private static void CleanupPatch()
        {
            if (AssetDatabase.IsValidFolder(VfxCompiler.GeneratedRoot + "/s9_cohort_h_patch_base")) AssetDatabase.DeleteAsset(VfxCompiler.GeneratedRoot + "/s9_cohort_h_patch_base");
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(PatchBase) != null) AssetDatabase.DeleteAsset(PatchBase); var history = PatchBase + VfxPatchService.HistorySuffix; if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(history) != null) AssetDatabase.DeleteAsset(history); AssetDatabase.Refresh();
        }
    }

    [Serializable]
    public sealed class PreparedRepair
    {
        public string Key;
        public int Attempt;
        public string AgentName;
        public string PromptPath;
        public string PromptSha256;
        public string PriorReportPath;
        public string PriorReportSha256;
    }
}

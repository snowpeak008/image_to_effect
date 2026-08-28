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
    /// <summary>Compact, source-derived K Patch preregistration. Freeze writes preparation only, never runtime evidence.</summary>
    public static class VfxCohortKProtocol
    {
        public static readonly string[] PatchKeys = { "K1", "K2", "K3" };
        public const int MaximumPayloadBytes = 3500;
        // A fresh preparation root replaces the pre-dispatch r1 draft; no K runtime evidence was ever written.
        public static string TempRoot { get { return Path.Combine(Path.GetTempPath(), "vfxcomposer-s9-cohort-k-r2"); } }

        [MenuItem("Tools/VFX Composer/AI Workflow/Freeze Cohort K Patch-only Payloads (one-time)")]
        public static void Freeze()
        {
            var catalog = VfxAiWorkflowExporter.ExportFormalCatalog(); var recipe = VfxAiWorkflowExporter.ExportCanonicalRecipe();
            if (catalog.Report.HasErrors || recipe.Report.HasErrors) throw new InvalidOperationException("K source exports must validate before freeze.");
            foreach (var key in PatchKeys)
            {
                var payload = InitialPayload(key); WriteExactPair(InitialPayloadPath(key), TempInitialPayloadPath(key), payload);
                WriteOnce(InitialEnvelopePath(key), Envelope(TempInitialPayloadPath(key), Hash(File.ReadAllBytes(InitialPayloadPath(key)))));
            }
            WriteOnce(Evidence("initial-payloads.generated.json"), InitialManifest()); VerifyPreDispatch(); AssetDatabase.Refresh();
        }

        public static void VerifyPreDispatch()
        {
            foreach (var key in PatchKeys)
            {
                var payload = InitialPayloadPath(key); var temp = TempInitialPayloadPath(key); var bytes = File.ReadAllBytes(payload);
                if (!File.Exists(temp) || !ByteEqual(bytes, File.ReadAllBytes(temp))) throw new InvalidOperationException("K payload pair differs: " + key);
                if (bytes.Length >= MaximumPayloadBytes || Encoding.UTF8.GetString(bytes).IndexOf("TASK\n", StringComparison.Ordinal) != 0 || Array.IndexOf(bytes, (byte)'T') > 511) throw new InvalidOperationException("K payload size or TASK position is invalid: " + key);
                if (Directory.GetFiles(Path.GetDirectoryName(temp)).Length != 1) throw new InvalidOperationException("K temp directory must contain only its payload: " + key);
                if (File.Exists(AttemptPath(key, 0)) || File.Exists(ReportPath(key, 0)) || File.Exists(TransportPath(key, 0)) || File.Exists(FinalPath(key))) throw new InvalidOperationException("K runtime evidence exists before dispatch: " + key);
                VerifyPayload(key, Encoding.UTF8.GetString(bytes));
            }
        }

        public static PreparedKRepair PrepareRepairAndPause(string key, int repairAttempt)
        {
            if (!PatchKeys.Contains(key, StringComparer.Ordinal) || repairAttempt < 1 || repairAttempt > 2) throw new ArgumentOutOfRangeException("repairAttempt");
            var priorPath = ReportPath(key, repairAttempt - 1); if (!File.Exists(priorPath)) throw new FileNotFoundException("Machine report required before repair.", priorPath);
            var prior = JObject.Parse(File.ReadAllText(priorPath)); if ((bool?)prior["succeeded"] != false) throw new InvalidOperationException("Patch already succeeded or report is invalid; a repair must not be prepared.");
            var payload = "TASK\nContinue in the same thread. Return only the authoritative bare Patch array.\n\nPRIOR_REPORT\n" + Normalize(File.ReadAllText(priorPath)) + "\nAUTHORITATIVE_OPERATION\n" + FrozenAcceptanceOperation(key) + "\nOUTPUT=bare array\n";
            WriteExactPair(RepairPayloadPath(key, repairAttempt), TempRepairPayloadPath(key, repairAttempt), payload); WriteOnce(RepairEnvelopePath(key, repairAttempt), Envelope(TempRepairPayloadPath(key, repairAttempt), Hash(File.ReadAllBytes(RepairPayloadPath(key, repairAttempt)))));
            var result = new PreparedKRepair { Key = key, Attempt = repairAttempt, EnvelopePath = RepairEnvelopePath(key, repairAttempt), EnvelopeSha256 = Hash(File.ReadAllBytes(RepairEnvelopePath(key, repairAttempt))), PayloadPath = RepairPayloadPath(key, repairAttempt), PayloadSha256 = Hash(File.ReadAllBytes(RepairPayloadPath(key, repairAttempt))), TempPayloadPath = TempRepairPayloadPath(key, repairAttempt), TempPayloadSha256 = Hash(File.ReadAllBytes(TempRepairPayloadPath(key, repairAttempt))), PriorReportPath = priorPath, PriorReportSha256 = Hash(File.ReadAllBytes(priorPath)) };
            WriteOnce(PreparedPath(key, repairAttempt), JObject.FromObject(result).ToString(Formatting.Indented).Replace("\r\n", "\n") + "\n"); return result;
        }

        public static void RecordInitialWitnessAndReport(string key, string threadId, string envelope) { RecordWitness(key, 0, threadId, envelope); RecordPersistedAttemptReport(key, 0); }
        public static void RecordRepairWitnessAndReport(string key, int attempt, string threadId, string envelope) { RecordWitness(key, attempt, threadId, envelope); RecordPersistedAttemptReport(key, attempt); }
        public static void RecordWitness(string key, int attempt, string threadId, string envelopeMessage)
        {
            var envelope = attempt == 0 ? InitialEnvelopePath(key) : RepairEnvelopePath(key, attempt); var payload = attempt == 0 ? InitialPayloadPath(key) : RepairPayloadPath(key, attempt); var temp = attempt == 0 ? TempInitialPayloadPath(key) : TempRepairPayloadPath(key, attempt);
            if (string.IsNullOrWhiteSpace(threadId) || !ByteEqual(File.ReadAllBytes(envelope), new UTF8Encoding(false).GetBytes(envelopeMessage ?? string.Empty)) || !ByteEqual(File.ReadAllBytes(payload), File.ReadAllBytes(temp))) throw new InvalidOperationException("K witness is not bound to frozen preparation.");
            if (attempt > 0 && (string)JObject.Parse(File.ReadAllText(TransportPath(key, 0)))["threadId"] != threadId) throw new InvalidOperationException("K repair changed thread.");
            var witness = new JObject { ["question"] = key, ["attempt"] = attempt, ["agentName"] = "s9_k_" + key.ToLowerInvariant(), ["model"] = "gpt-5.6-terra", ["reasoningEffort"] = "high", ["forkTurns"] = "none", ["threadId"] = threadId, ["transport"] = attempt == 0 ? "spawn_agent" : "followup_task", ["disclosure"] = "No wire payload or child tool trace readback; primary Agent witnessed the short envelope.", ["envelopeSha256"] = Hash(File.ReadAllBytes(envelope)), ["payloadSha256"] = Hash(File.ReadAllBytes(payload)), ["tempPayloadSha256"] = Hash(File.ReadAllBytes(temp)) };
            WriteOnce(TransportPath(key, attempt), witness.ToString(Formatting.Indented).Replace("\r\n", "\n") + "\n");
        }

        public static void RecordPersistedAttemptReport(string key, int attempt)
        {
            const string basePath = "Assets/VFX/Recipes/s9_cohort_k_patch_base.json";
            try
            {
                File.WriteAllText(Absolute(basePath), File.ReadAllText(Absolute(VfxAiWorkflowExporter.FormalDefaultRecipePath)).Replace("\"id\": \"fireball_2d\"", "\"id\": \"s9_cohort_k_patch_base\"")); AssetDatabase.ImportAsset(basePath, ImportAssetOptions.ForceUpdate);
                var result = new VfxPatchService().ApplyToAsset(basePath, File.ReadAllText(AttemptPath(key, attempt)), 1); JToken actual = null; string allowed = null; var accepted = result.IsValid && MeetsFrozenAcceptance(key, File.ReadAllText(AttemptPath(key, attempt)), File.ReadAllText(Absolute(basePath)), out actual, out allowed);
                if (result.IsValid && !accepted) result.Report.Add("E720", ValidationSeverity.Error, (string)Acceptance(key)["path"], "Patch applied but does not satisfy frozen K acceptance.", actual, allowed);
                WriteOnce(ReportPath(key, attempt), MachineReport(result.Report, accepted, "revision " + result.BeforeRevision + "->" + result.AfterRevision));
            }
            catch (Exception exception) { WriteOnce(ReportPath(key, attempt), MachineReport(new ValidationReport(), false, exception.ToString())); }
            finally { Cleanup(basePath); AssetDatabase.Refresh(); }
        }

        public static bool MeetsFrozenAcceptance(string key, string patchJson, string recipeJson, out JToken actual, out string allowed)
        {
            var spec = Acceptance(key); var expected = ExpectedOperation(spec); allowed = expected.ToString(Formatting.None); actual = null; JArray patch;
            try { patch = JArray.Parse(patchJson); } catch { actual = new JValue(patchJson ?? string.Empty); return false; }
            actual = patch; if (!JToken.DeepEquals(patch, expected)) return false;
            try { var parts = ((string)spec["path"]).Split('/'); var recipe = JObject.Parse(recipeJson); var stage = recipe["stages"].Children<JObject>().Single(x => (string)x["id"] == parts[2]); var module = stage["modules"].Children<JObject>().SingleOrDefault(x => (string)x["id"] == parts[4]); return (string)spec["operation"] == "replace" ? module != null && JToken.DeepEquals(module["parameters"][parts[6]], spec["value"]) : (string)spec["operation"] == "disable" ? module != null && JToken.DeepEquals(module["enabled"], spec["enabled"]) : module != null && JToken.DeepEquals(module, spec["module"]); } catch { return false; }
        }

        public static string FrozenAcceptanceOperation(string key) { return ExpectedOperation(Acceptance(key)).ToString(Formatting.Indented); }
        public static string EvidenceDirectory() { return Path.Combine(RepositoryRoot(), "docs", "ai-workflow", "evidence", "cohort-k"); }
        public static string AttemptPath(string key, int attempt) { return Evidence(key + ".attempt" + attempt + ".patch.json"); } public static string ReportPath(string key, int attempt) { return Evidence(key + ".attempt" + attempt + ".report.json"); } public static string TransportPath(string key, int attempt) { return Evidence(key + ".attempt" + attempt + ".transport.json"); } public static string FinalPath(string key) { return Evidence(key + ".final.patch.json"); }
        public static string InitialPayloadPath(string key) { return Evidence(key + ".initial.payload.md"); } public static string TempInitialPayloadPath(string key) { return Path.Combine(TempRoot, key, key + ".initial.payload.md"); } public static string InitialEnvelopePath(string key) { return Evidence(key + ".initial.envelope.txt"); }
        public static string RepairPayloadPath(string key, int attempt) { return Evidence(key + ".repair" + attempt + ".payload.md"); } public static string TempRepairPayloadPath(string key, int attempt) { return Path.Combine(TempRoot, key, key + ".repair" + attempt + ".payload.md"); } public static string RepairEnvelopePath(string key, int attempt) { return Evidence(key + ".repair" + attempt + ".envelope.txt"); } public static string PreparedPath(string key, int attempt) { return Evidence(key + ".repair" + attempt + ".prepared.json"); }
        public static string Normalize(string text) { return (text ?? string.Empty).Replace("\r\n", "\n").Replace("\r", "\n"); }
        public static void WriteOnce(string path, string text) { if (File.Exists(path)) { if (Normalize(File.ReadAllText(path)) == Normalize(text)) return; throw new InvalidOperationException("Write-once collision: " + path); } Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllText(path, text, new UTF8Encoding(false)); }

        private static string InitialPayload(string key)
        {
            var spec = Acceptance(key); var recipe = JObject.Parse(File.ReadAllText(Workflow("canonical-recipe.generated.json"))); var stageId = ((string)spec["path"]).Split('/')[2]; var moduleId = ((string)spec["path"]).Split('/')[4]; var stage = (JObject)recipe["stages"].Children<JObject>().Single(x => (string)x["id"] == stageId); var modules = new JArray(); var target = stage["modules"].Children<JObject>().SingleOrDefault(x => (string)x["id"] == moduleId); if (target != null) modules.Add(target.DeepClone()); if (key == "K3") modules.Add(stage["modules"].Children<JObject>().Single(x => (string)x["id"] == "core").DeepClone());
            var context = new JObject { ["stage"] = new JObject { ["id"] = stage["id"], ["trigger"] = stage["trigger"] }, ["modules"] = modules };
            var text = "TASK\n" + PromptFor(key) + "\n\nPATCH_OPERATION_SYNTAX\nReturn one JSON array with exactly one object. Allowed forms: replace {op,path,value}; disable {op,path}; add {op,path,value}.\n\nPATH_RULES\nreplace /stages/{stageId}/modules/{moduleId}/parameters/{parameter}; disable /stages/{stageId}/modules/{moduleId}; add /stages/{stageId}/modules/{newModuleId}, value is a complete module object and value.id equals newModuleId. Never use array indexes, wrappers, Markdown, prose, or fences.\n\nRECIPE_CONTEXT\n" + context.ToString(Formatting.None) + "\n";
            if (key != "K2") text += "\nCATALOG_FACTS\n" + CatalogFacts(key, spec).ToString(Formatting.None) + "\n";
            return text + "\nOUTPUT=bare array\n";
        }
        private static JObject CatalogFacts(string key, JObject spec)
        {
            var canonical = JObject.Parse(File.ReadAllText(Workflow("canonical-recipe.generated.json"))); var parts = ((string)spec["path"]).Split('/'); var stage = canonical["stages"].Children<JObject>().Single(x => (string)x["id"] == parts[2]); var existing = stage["modules"].Children<JObject>().SingleOrDefault(x => (string)x["id"] == parts[4]); var templateId = key == "K3" ? (string)spec["module"]["templateId"] : (string)existing["templateId"]; var catalog = VfxCompiler.LoadFormalCatalog(); if (catalog.Report.HasErrors) throw new InvalidOperationException("Live catalog is invalid."); var manifest = catalog.ByTemplateId[templateId]; var names = key == "K3" ? new[] { "rate", "lifetime" } : new[] { parts[6] }; var parameters = new JObject(); foreach (var name in names) { var p = manifest.Parameters[name]; parameters[name] = new JObject { ["type"] = p.Type.ToString().ToLowerInvariant(), ["min"] = p.Min, ["max"] = p.Max }; } return new JObject { ["templateId"] = templateId, ["parameters"] = parameters };
        }
        private static void VerifyPayload(string key, string payload)
        {
            var permitted = new[] { "TASK", "PATCH_OPERATION_SYNTAX", "PATH_RULES", "RECIPE_CONTEXT", "CATALOG_FACTS", "OUTPUT=bare array" }; var headings = payload.Split('\n').Where(x => permitted.Contains(x) || x.StartsWith("OUTPUT=", StringComparison.Ordinal)).ToArray(); if (!headings.All(x => permitted.Contains(x)) || headings.Length != (key == "K2" ? 5 : 6)) throw new InvalidOperationException("K payload section whitelist failed: " + key);
            var rules = "replace /stages/{stageId}/modules/{moduleId}/parameters/{parameter}; disable /stages/{stageId}/modules/{moduleId}; add /stages/{stageId}/modules/{newModuleId}, value is a complete module object and value.id equals newModuleId. Never use array indexes, wrappers, Markdown, prose, or fences."; if (!payload.Contains("PATH_RULES\n" + rules)) throw new InvalidOperationException("K stable PATH_RULES are missing: " + key);
            if (payload.Contains("canonical-patches.generated") || payload.Contains("patch-authoring.md") || payload.Contains("recipe-v1.schema") || payload.Contains("template-parameters.generated") || payload.Contains("linger_embers") || payload.Contains("sample_embers")) throw new InvalidOperationException("K payload leaked prohibited source material: " + key);
            var expected = InitialPayload(key); if (!string.Equals(payload, expected, StringComparison.Ordinal)) throw new InvalidOperationException("K payload is not source-derived: " + key);
        }
        private static string Envelope(string temp, string hash) { return "You are isolated `gpt-5.6-terra` at `high`. Use exactly one `exec_command` to read only `" + temp + "` and verify SHA-256 `" + hash + "`; use no other resource. Then return only the required bare Patch JSON array.\n"; }
        private static string InitialManifest() { var result = new JObject(); foreach (var key in PatchKeys) result[key] = new JObject { ["envelopeSha256"] = Hash(File.ReadAllBytes(InitialEnvelopePath(key))), ["payloadSha256"] = Hash(File.ReadAllBytes(InitialPayloadPath(key))), ["payloadBytes"] = File.ReadAllBytes(InitialPayloadPath(key)).Length, ["tempPayloadSha256"] = Hash(File.ReadAllBytes(TempInitialPayloadPath(key))) }; return new JObject { ["protocol"] = "cohort-k-patch-only", ["runtimeEvidence"] = 0, ["initialPayloads"] = result }.ToString(Formatting.Indented).Replace("\r\n", "\n") + "\n"; }
        private static string PromptFor(string key) { var text = File.ReadAllText(Evidence("prompts.md")); var mark = "## " + key + "\n\n"; var start = text.IndexOf(mark, StringComparison.Ordinal) + mark.Length; var end = text.IndexOf("\n## ", start, StringComparison.Ordinal); return (end < 0 ? text.Substring(start) : text.Substring(start, end - start)).TrimEnd(); }
        private static JObject Acceptance(string key) { if (!PatchKeys.Contains(key, StringComparer.Ordinal)) throw new ArgumentException("Known K key required."); return (JObject)JObject.Parse(File.ReadAllText(Evidence("acceptance-spec.json")))["patches"][key]; }
        private static JArray ExpectedOperation(JObject spec) { var op = new JObject { ["op"] = spec["operation"], ["path"] = spec["path"] }; if ((string)spec["operation"] == "replace") op["value"] = spec["value"]; if ((string)spec["operation"] == "add") op["value"] = spec["module"]; return new JArray(op); }
        private static void WriteExactPair(string evidence, string temp, string text) { WriteOnce(evidence, text); WriteOnce(temp, text); if (!ByteEqual(File.ReadAllBytes(evidence), File.ReadAllBytes(temp))) throw new InvalidOperationException("K byte pair differs."); }
        private static string Evidence(string file) { return Path.Combine(EvidenceDirectory(), file); } private static string Workflow(string file) { return Path.Combine(RepositoryRoot(), "docs", "ai-workflow", file); } private static string Absolute(string assetPath) { return Path.Combine(UnityEngine.Application.dataPath, assetPath.Substring("Assets/".Length)); } private static string RepositoryRoot() { return Directory.GetParent(Directory.GetParent(UnityEngine.Application.dataPath).FullName).FullName; }
        private static void Cleanup(string assetPath) { if (AssetDatabase.IsValidFolder(VfxCompiler.GeneratedRoot + "/s9_cohort_k_patch_base")) AssetDatabase.DeleteAsset(VfxCompiler.GeneratedRoot + "/s9_cohort_k_patch_base"); if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath) != null) AssetDatabase.DeleteAsset(assetPath); var history = assetPath + VfxPatchService.HistorySuffix; if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(history) != null) AssetDatabase.DeleteAsset(history); }
        private static string MachineReport(ValidationReport report, bool succeeded, string detail) { return new JObject { ["succeeded"] = succeeded, ["detail"] = detail, ["entries"] = new JArray(report.Entries.Select(x => new JObject { ["code"] = x.Code, ["severity"] = x.Severity.ToString().ToLowerInvariant(), ["path"] = x.Path, ["message"] = x.Message, ["actualValue"] = x.ActualValue ?? JValue.CreateNull(), ["allowedRange"] = x.AllowedRange ?? (JToken)JValue.CreateNull() })) }.ToString(Formatting.Indented).Replace("\r\n", "\n") + "\n"; }
        private static bool ByteEqual(byte[] a, byte[] b) { return a.Length == b.Length && !a.Where((x, i) => x != b[i]).Any(); } private static string Hash(byte[] bytes) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty); }
    }
    [Serializable] public sealed class PreparedKRepair { public string Key; public int Attempt; public string EnvelopePath; public string EnvelopeSha256; public string PayloadPath; public string PayloadSha256; public string TempPayloadPath; public string TempPayloadSha256; public string PriorReportPath; public string PriorReportSha256; }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using VFXComposer;
using VFXComposer.Editor.Build;
using VFXComposer.Editor.Domain;
using VFXComposer.Editor.Patch;
using VFXComposer.Editor.SlashV2;
using VFXComposer.Editor.Validation;

namespace VFXComposer.Editor.Workflow
{
    /// <summary>Write-once evidence for three already-dispatched S12 Patch responses; this class never dispatches AI.</summary>
    public static class S12C3PatchEvidence
    {
        public const string EvidenceRelative = "docs/stage-notes/s12c3-evidence";
        private const string CanonicalAsset = "Assets/VFX/Recipes/Slash/slash-3d-stylized.default.v2.json";
        private static readonly Entry[] Entries = { new Entry("primaryWidth", "patch-primary-width.attempt0.json", "/root/s12_patch_width"), new Entry("sparkCount", "patch-spark-count.attempt0.json", "/root/s12_patch_sparks"), new Entry("afterimageAlpha", "patch-afterimage-alpha.attempt0.json", "/root/s12_patch_alpha") };

        public static void EnsureRecorded()
        {
            if (Entries.All(entry => File.Exists(Evidence(entry.Name + ".report.json")))) { if (!VerifyExisting()) throw new InvalidOperationException("Completed S12C3 evidence is inconsistent and write-once."); return; }
            if (Directory.Exists(Evidence(string.Empty))) Directory.Delete(Evidence(string.Empty), true);
            var canonical = File.ReadAllText(Absolute(CanonicalAsset));
            try { foreach (var entry in Entries) Record(entry, canonical); if (!VerifyExisting()) throw new InvalidOperationException("S12C3 evidence self-verification failed."); }
            catch { if (Directory.Exists(Evidence(string.Empty))) Directory.Delete(Evidence(string.Empty), true); throw; }
            finally { RestoreCanonical(canonical); AssertNoResidue(); }
        }

        public static bool VerifyExisting()
        {
            try
            {
                var frozen = JObject.Parse(File.ReadAllText(Repository("docs/ai-workflow/s12-slash-v3/frozen/acceptance-spec.generated.json"))); if ((int)frozen["runtimeEvidence"] != 0) return false;
                if (!Directory.Exists(Evidence(string.Empty)) || Directory.GetFiles(Evidence(string.Empty), "*.report.json", SearchOption.TopDirectoryOnly).Length != Entries.Length) return false;
                foreach (var entry in Entries)
                {
                    var report = JObject.Parse(File.ReadAllText(Evidence(entry.Name + ".report.json"))); var raw = Runtime(entry.File);
                    if (!(bool)report["succeeded"] || (string)report["patchRawBytesSha256"] != HashFile(raw) || (string)report["agent"]["model"] != "gpt-5.6-terra" || (string)report["agent"]["reasoning"] != "high" || (string)report["agent"]["fork"] != "none" || (string)report["agent"]["thread"] != entry.Thread) return false;
                    if (((JObject)report["errors"]).Properties().Any(property => ((JArray)property.Value).Count != 0) || (int)report["beforeRevision"] != 1 || (int)report["afterRevision"] != 2 || string.IsNullOrEmpty((string)report["buildHash"]) || string.IsNullOrEmpty((string)report["prefabGuid"])) return false;
                    ValidateFrozen(entry, File.ReadAllText(raw));
                    var op = (JObject)JArray.Parse(File.ReadAllText(raw))[0]; var history = (JObject)report["history"]; if ((string)report["patchAttemptPath"] != "docs/ai-workflow/s12-slash-v3/runtime/" + entry.File || (string)report["affectedPath"] != (string)op["path"] || !JToken.DeepEquals(report["actualValue"], op["value"]) || (int)history["beforeRevision"] != 1 || (int)history["afterRevision"] != 2 || ((JArray)history["affectedPaths"]).Count != 1 || (string)((JArray)history["affectedPaths"])[0] != (string)op["path"]) return false;
                }
                return true;
            }
            catch { return false; }
        }

        public static void AssertNoResidue()
        {
            var recipeRoot = Absolute("Assets/VFX/Recipes/Slash"); var generated = Absolute(S12SlashCompiler.GeneratedRoot);
            Require(!Directory.GetFiles(recipeRoot, "s12c3_*", SearchOption.TopDirectoryOnly).Any(), "S12C3 left a temporary Recipe, meta, or history."); Require(!Directory.GetFiles(recipeRoot, "s12c3_*.pending", SearchOption.TopDirectoryOnly).Any(), "S12C3 left a pending Recipe."); Require(!Directory.GetDirectories(generated, "s12btmp_*", SearchOption.TopDirectoryOnly).Any(), "S12C3 left a generated temp."); Require(!Directory.GetFiles(generated, "*.pending", SearchOption.AllDirectories).Any(), "S12C3 left a pending Generated file."); Require(!Directory.GetDirectories(Path.GetTempPath(), "vfxcomposer_s12c1_backup_*", SearchOption.TopDirectoryOnly).Any(), "S12C3 left a Patch backup."); Require(!Directory.GetDirectories(Path.GetTempPath(), "vfxcomposer_s12b_*", SearchOption.TopDirectoryOnly).Any(), "S12C3 left a compiler backup.");
        }

        private static void Record(Entry entry, string canonical)
        {
            var patchPath = Runtime(entry.File); var patch = File.ReadAllText(patchPath); ValidateFrozen(entry, patch);
            var asset = "Assets/VFX/Recipes/Slash/s12c3_" + entry.Name + ".v2.json"; var absolute = Absolute(asset); var history = absolute + S12SlashPatchService.HistorySuffix;
            try
            {
                File.WriteAllText(absolute, canonical, new UTF8Encoding(false)); AssetDatabase.ImportAsset(asset, ImportAssetOptions.ForceUpdate);
                var result = new S12SlashPatchService().ApplyToAsset(asset, patch, 1); Require(result.IsValid && result.BeforeRevision == 1 && result.AfterRevision == 2, "Real S12 Patch Apply failed for " + entry.Name + ".");
                var written = JObject.Parse(File.ReadAllText(absolute)); var op = (JObject)JArray.Parse(patch)[0]; var actual = Value(written, (string)op["path"]); Require(JToken.DeepEquals(actual, op["value"]), "Written Recipe does not contain the Patch value.");
                var recordedHistory = (JObject)JArray.Parse(File.ReadAllText(history)).Last; Require((int)recordedHistory["beforeRevision"] == 1 && (int)recordedHistory["afterRevision"] == 2 && (string)((JArray)recordedHistory["affectedPaths"])[0] == (string)op["path"], "Patch history is not auditable.");
                var manifest = JObject.Parse(File.ReadAllText(Absolute(S12SlashCompiler.ManifestPath))); Require((int)manifest["recipeRevision"] == 2 && (string)manifest["recipeHash"] == RecipeCanonicalizer.ComputeSha256(File.ReadAllText(absolute)), "Patched build manifest is not the applied Recipe."); VerifyBinding(entry.Name, (JToken)op["value"]);
                Directory.CreateDirectory(Evidence(string.Empty)); var report = new JObject { ["schema"] = "s12c3-real-patch-evidence/v1", ["succeeded"] = true, ["agent"] = new JObject { ["model"] = "gpt-5.6-terra", ["reasoning"] = "high", ["fork"] = "none", ["thread"] = entry.Thread }, ["patchAttemptPath"] = "docs/ai-workflow/s12-slash-v3/runtime/" + entry.File, ["patchRawBytesSha256"] = HashFile(patchPath), ["beforeRevision"] = result.BeforeRevision, ["afterRevision"] = result.AfterRevision, ["affectedPath"] = (string)op["path"], ["actualValue"] = actual.DeepClone(), ["buildHash"] = (string)manifest["buildHash"], ["prefabGuid"] = AssetDatabase.AssetPathToGUID(S12SlashCompiler.OutputPrefabPath), ["errors"] = new JObject { ["apply"] = Errors(result.Report) }, ["history"] = new JObject { ["beforeRevision"] = (int)recordedHistory["beforeRevision"], ["afterRevision"] = (int)recordedHistory["afterRevision"], ["affectedPaths"] = recordedHistory["affectedPaths"].DeepClone() } };
                File.WriteAllText(Evidence(entry.Name + ".report.json"), report.ToString(Formatting.Indented), new UTF8Encoding(false));
            }
            finally { DeleteTemporary(asset, absolute, history); RestoreCanonical(canonical); AssertNoResidue(); }
        }

        private static void ValidateFrozen(Entry entry, string patch)
        {
            Require(S12SlashAiExporter.VerifyExisting(), "Frozen S12 contract hash verification failed."); var acceptance = JObject.Parse(File.ReadAllText(Repository("docs/ai-workflow/s12-slash-v3/frozen/acceptance-spec.generated.json"))); var required = ((JArray)acceptance["patches"]).Children<JObject>().Single(item => (string)item["name"] == entry.Name); var array = JToken.Parse(patch) as JArray; Require(array != null && array.Count == 1, "Patch must be one bare operation."); var op = array[0] as JObject; Require(op != null && op.Properties().Count() == 3 && (string)op["op"] == "replace" && (string)op["path"] == (string)required["path"] && op["value"] != null, "Patch does not exactly match frozen operation grammar/path.");
            var canonical = JObject.Parse(File.ReadAllText(Absolute(CanonicalAsset))); var old = Value(canonical, (string)op["path"]); var catalog = JObject.Parse(File.ReadAllText(Repository("docs/ai-workflow/s12-slash-v3/frozen/contract.generated.json")))["catalog"]; var parts = ((string)op["path"]).Split('/'); var phase = ((JArray)canonical["phases"]).Children<JObject>().Single(item => (string)item["id"] == parts[2]); var module = ((JArray)phase["modules"]).Children<JObject>().Single(item => (string)item["id"] == parts[4]); var declaration = catalog[(string)module["templateId"]][parts[6]]; var value = (double)op["value"]; Require(value > (double)old, "Patch must be > canonical default."); if (((string)required["comparison"]).StartsWith("integer", StringComparison.Ordinal)) Require(op["value"].Type == JTokenType.Integer, "Patch must retain integer type."); var comparison = (string)required["comparison"]; var ceiling = comparison.Contains("<= max") ? (double)declaration["max"] : double.Parse(comparison.Substring(comparison.LastIndexOf("<=", StringComparison.Ordinal) + 2).Trim(), System.Globalization.CultureInfo.InvariantCulture); Require(value <= ceiling, "Patch exceeds frozen upper bound.");
        }

        private static void VerifyBinding(string name, JToken expected)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(S12SlashCompiler.OutputPrefabPath); Require(prefab != null && !prefab.GetComponentsInChildren<MonoBehaviour>(true).Any(item => item == null), "Generated prefab is missing or has missing scripts.");
            if (name == "primaryWidth") Require(Mathf.Abs(prefab.transform.Find("Primary_arc/Arc_sweep/RibbonWidthControl").localScale.x - (float)expected / .24f) < .0001f, "Width binding was not serialized.");
            else if (name == "sparkCount") Require(prefab.transform.Find("Sparks/Slash_sparks").GetComponent<ParticleSystem>().emission.GetBurst(0).maxCount == (short)(int)expected, "Spark count binding was not serialized.");
            else Require(Mathf.Abs(prefab.GetComponentInChildren<SlashAfterimageAlpha>(true).Alpha - (float)expected) < .0001f, "Afterimage alpha binding was not serialized.");
        }

        private static JToken Value(JObject recipe, string path) { var p = path.Split('/'); var phase = ((JArray)recipe["phases"]).Children<JObject>().Single(item => (string)item["id"] == p[2]); return ((JArray)phase["modules"]).Children<JObject>().Single(item => (string)item["id"] == p[4])["parameters"][p[6]]; }
        private static void RestoreCanonical(string text) { var result = new S12SlashCompiler().Build(text); Require(result.Succeeded, "Could not restore canonical Generated Slash."); Require((string)JObject.Parse(File.ReadAllText(Absolute(S12SlashCompiler.ManifestPath)))["recipeHash"] == RecipeCanonicalizer.ComputeSha256(text), "Canonical Generated Slash does not match canonical Recipe."); }
        private static void DeleteTemporary(string asset, string absolute, string history) { if (File.Exists(absolute)) File.Delete(absolute); if (File.Exists(absolute + ".meta")) File.Delete(absolute + ".meta"); if (File.Exists(history)) File.Delete(history); if (File.Exists(history + ".meta")) File.Delete(history + ".meta"); AssetDatabase.Refresh(); }
        private static JArray Errors(ValidationReport report) { return new JArray(report.Entries.Where(item => item.Severity == ValidationSeverity.Error).Select(item => new JObject { ["code"] = item.Code, ["path"] = item.Path, ["message"] = item.Message })); }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private static string Runtime(string file) { return Repository("docs/ai-workflow/s12-slash-v3/runtime/" + file); }
        private static string Evidence(string file) { return Repository(EvidenceRelative + "/" + file); }
        private static string Repository(string relative) { return Path.Combine(Directory.GetParent(Directory.GetParent(Application.dataPath).FullName).FullName, relative.Replace('/', Path.DirectorySeparatorChar)); }
        private static string Absolute(string asset) { return Path.Combine(Application.dataPath, asset.Substring("Assets/".Length)); }
        private static string HashFile(string path) { using (var sha = SHA256.Create()) return string.Concat(sha.ComputeHash(File.ReadAllBytes(path)).Select(item => item.ToString("X2"))); }
        private sealed class Entry { public readonly string Name; public readonly string File; public readonly string Thread; public Entry(string name, string file, string thread) { Name = name; File = file; Thread = thread; } }
    }
}

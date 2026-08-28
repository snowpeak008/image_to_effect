using System;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using VFXComposer.Editor.Build;
using VFXComposer.Editor.Domain;
using VFXComposer.Editor.Patch;

namespace VFXComposer.Editor.Workflow
{
    /// <summary>Writes raw machine reports for already-persisted Cohort G attempts. It never creates or edits an AI response.</summary>
    public static class VfxCohortGProtocol
    {
        [MenuItem("Tools/VFX Composer/AI Workflow/Record Persisted Cohort G Attempt Reports")]
        public static void RecordAllPersistedAttempts()
        {
            var directory = EvidenceDirectory();
            foreach (var key in new[] { "G1", "G2", "G3", "G4", "G5" }) RecordRecipes(directory, key);
            foreach (var key in new[] { "P1", "P2", "P3" }) RecordPatches(directory, key);
            AssetDatabase.Refresh();
        }

        [MenuItem("Tools/VFX Composer/AI Workflow/Prepare Missing Cohort G Repair Prompts")]
        public static void PrepareMissingRepairPrompts()
        {
            foreach (var key in new[] { "G1", "G2", "G3", "G4", "G5", "P1", "P2", "P3" })
            {
                var extension = key[0] == 'G' ? "recipe.json" : "patch.json";
                var attempt = Path.Combine(EvidenceDirectory(), key + ".attempt0." + extension);
                var report = Path.Combine(EvidenceDirectory(), key + ".attempt0.report.json");
                var repair = Path.Combine(EvidenceDirectory(), key + ".repair1.prompt.md");
                if (!File.Exists(attempt) || !File.Exists(report) || File.Exists(repair)) continue;
                var parsed = JObject.Parse(File.ReadAllText(report)); if ((bool?)parsed["succeeded"] == true) continue;
                var prompt = "Return a complete corrected " + (key[0] == 'G' ? "Recipe JSON object" : "Patch JSON array") + " only. Do not use tools or workspace. Correct only the report issues.\n\nFROZEN CONTRACT SNAPSHOT:\n" + File.ReadAllText(Path.Combine(EvidenceDirectory(), "contract-snapshot.md")) + "\n\nORIGINAL PREREGISTERED PROMPTS:\n" + File.ReadAllText(Path.Combine(EvidenceDirectory(), "prompts.md")) + "\n\nPREVIOUS RAW ARTIFACT:\n" + File.ReadAllText(attempt) + "\n\nCOMPLETE PREVIOUS MACHINE REPORT:\n" + File.ReadAllText(report);
                WriteReportOnce(repair, prompt);
            }
        }

        private static void RecordRecipes(string directory, string key)
        {
            for (var attempt = 0; attempt <= 2; attempt++)
            {
                var file = Path.Combine(directory, key + ".attempt" + attempt + ".recipe.json");
                if (!File.Exists(file)) continue;
                var reportPath = file.Replace(".recipe.json", ".report.json");
                try
                {
                    var build = new VfxCompiler().Build(File.ReadAllText(file));
                    Write(reportPath, Report(build.Plan.Report, build.Succeeded, build.PrefabPath));
                    if (build.Succeeded && !string.IsNullOrEmpty(build.PrefabPath)) AssetDatabase.DeleteAsset(VfxCompiler.OutputFolder(VfxDomainParser.ParseRecipe(File.ReadAllText(file)).Value));
                }
                catch (Exception exception) { Write(reportPath, Report(new ValidationReport(), false, exception.ToString())); }
            }
        }

        private static void RecordPatches(string directory, string key)
        {
            var source = AbsoluteAssetPath(VfxAiWorkflowExporter.FormalDefaultRecipePath);
            foreach (var attempt in Enumerable.Range(0, 3))
            {
                var file = Path.Combine(directory, key + ".attempt" + attempt + ".patch.json");
                if (!File.Exists(file)) continue;
                var result = new VfxPatchService().Validate(File.ReadAllText(source), File.ReadAllText(file), 1);
                Write(file.Replace(".patch.json", ".report.json"), Report(result.Report, result.IsValid, "validation " + result.BeforeRevision + "->" + result.AfterRevision));
            }
        }

        public static string EvidenceDirectory() { return Path.Combine(RepositoryRoot(), "docs", "ai-workflow", "evidence", "cohort-g"); }
        public static string Report(ValidationReport report, bool succeeded, string detail)
        {
            var entries = new JArray(report.Entries.Select(entry => new JObject {
                ["code"] = entry.Code, ["severity"] = entry.Severity.ToString().ToLowerInvariant(), ["path"] = entry.Path,
                ["message"] = entry.Message, ["actualValue"] = entry.ActualValue == null ? JValue.CreateNull() : entry.ActualValue.DeepClone(),
                ["allowedRange"] = entry.AllowedRange == null ? JValue.CreateNull() : new JValue(entry.AllowedRange)
            }));
            return new JObject { ["succeeded"] = succeeded, ["detail"] = detail == null ? JValue.CreateNull() : new JValue(detail), ["entries"] = entries }.ToString(Formatting.Indented).Replace("\r\n", "\n") + "\n";
        }
        /// <summary>Raw machine reports are immutable: equivalent CRLF/LF text is accepted without rewriting; any other collision fails.</summary>
        public static void WriteReportOnce(string path, string text)
        {
            if (File.Exists(path))
            {
                if (string.Equals(NormalizeNewlines(File.ReadAllText(path)), NormalizeNewlines(text), StringComparison.Ordinal)) return;
                throw new InvalidOperationException("Cohort G report already exists with different content and must not be overwritten: " + path);
            }
            File.WriteAllText(path, text, new UTF8Encoding(false));
        }
        private static void Write(string path, string text) { WriteReportOnce(path, text); }
        private static string NormalizeNewlines(string text) { return (text ?? string.Empty).Replace("\r\n", "\n").Replace("\r", "\n"); }
        private static string AbsoluteAssetPath(string assetPath) { return Path.Combine(UnityEngine.Application.dataPath, assetPath.Substring("Assets/".Length)); }
        private static string RepositoryRoot() { return Directory.GetParent(Directory.GetParent(UnityEngine.Application.dataPath).FullName).FullName; }
    }
}

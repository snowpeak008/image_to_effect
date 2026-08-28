using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using VFXComposer.Editor.Catalog;
using VFXComposer.Editor.Build;
using VFXComposer.Editor.Domain;
using VFXComposer.Editor.Patch;
using VFXComposer.Editor.Validation;

namespace VFXComposer.Editor.Workflow
{
    /// <summary>Exports the AI-facing template table from the same resolved Catalog used by validation and Build.</summary>
    public static class VfxAiWorkflowExporter
    {
        public const string ManifestRoot = "Assets/VFX/Templates/2D/Manifests";
        public const string OutputRelativePath = "docs/ai-workflow/template-parameters.generated.md";
        public const string CanonicalRecipeRelativePath = "docs/ai-workflow/canonical-recipe.generated.json";
        public const string CanonicalPatchesRelativePath = "docs/ai-workflow/canonical-patches.generated.md";
        public const string FormalDefaultRecipePath = "Assets/VFX/Recipes/fireball-2d.default.json";

        [MenuItem("Tools/VFX Composer/AI Workflow/Export Template Parameter Table")]
        public static void ExportFromMenu()
        {
            var result = ExportFormalCatalog();
            if (result.Report.HasErrors)
            {
                Debug.LogError("VFX Composer AI workflow export failed:\n" + Describe(result.Report));
                return;
            }
            Debug.Log("VFX Composer AI workflow parameter table is current: " + result.OutputPath);
        }

        [MenuItem("Tools/VFX Composer/AI Workflow/Export Formal Authoring Bundle")]
        public static void ExportFormalAuthoringBundleFromMenu()
        {
            var table = ExportFormalCatalog();
            var canonical = ExportCanonicalRecipe();
            var patches = ExportCanonicalPatches();
            if (table.Report.HasErrors || canonical.Report.HasErrors || patches.Report.HasErrors)
            {
                Debug.LogError("VFX Composer AI authoring bundle export failed:\n" + Describe(table.Report) + "\n" + Describe(canonical.Report) + "\n" + Describe(patches.Report));
                return;
            }
            Debug.Log("VFX Composer AI authoring bundle is current: " + table.OutputPath + ", " + canonical.OutputPath + " and " + patches.OutputPath);
        }

        [MenuItem("Tools/VFX Composer/AI Workflow/Freeze Cohort G Contract Snapshot (one-time)")]
        public static void FreezeCohortGContractSnapshot()
        {
            try { Debug.Log("VFX Composer Cohort G contract snapshot: " + VfxAiWorkflowContractSnapshot.ExportOnce("cohort-g", "Cohort G")); }
            catch (InvalidOperationException exception) { Debug.LogError(exception.Message); }
        }

        [MenuItem("Tools/VFX Composer/AI Workflow/Verify Frozen Cohort G Contract Snapshot")]
        public static void VerifyFrozenCohortGContractSnapshot()
        {
            string hash;
            if (VfxAiWorkflowContractSnapshot.VerifyExisting("cohort-g", out hash)) Debug.Log("VFX Composer Cohort G contract snapshot verified: " + hash);
            else Debug.LogError("VFX Composer Cohort G contract snapshot is missing or its SHA-256 does not match its frozen body.");
        }

        public static VfxAiWorkflowExportResult ExportFormalCatalog()
        {
            var catalog = TemplateCatalog.LoadFromDirectory(AbsoluteAssetPath(ManifestRoot), new UnityAssetReferenceResolver());
            var content = GenerateMarkdown(catalog);
            var outputPath = Path.Combine(RepositoryRoot(), OutputRelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!catalog.Report.HasErrors)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
                if (!File.Exists(outputPath) || !string.Equals(File.ReadAllText(outputPath), content, StringComparison.Ordinal))
                    File.WriteAllText(outputPath, content, new UTF8Encoding(false));
            }
            return new VfxAiWorkflowExportResult { Catalog = catalog, Report = catalog.Report, OutputPath = outputPath, Content = content };
        }

        [MenuItem("Tools/VFX Composer/AI Workflow/Export Canonical Recipe Example")]
        public static void ExportCanonicalRecipeFromMenu()
        {
            var result = ExportCanonicalRecipe();
            if (result.Report.HasErrors) { Debug.LogError("VFX Composer canonical Recipe export failed:\n" + Describe(result.Report)); return; }
            Debug.Log("VFX Composer canonical Recipe example is current: " + result.OutputPath);
        }

        /// <summary>
        /// The canonical example is a byte-stable copy of the formal default Recipe, never a second
        /// hand-maintained sample. Validation and Dry Run use the exact resolved Catalog before write.
        /// Build confirmation belongs to the EditMode exporter test so exporting documentation does not
        /// mutate the preserved generated fireball asset.
        /// </summary>
        public static VfxAiWorkflowExportResult ExportCanonicalRecipe()
        {
            var catalog = VfxCompiler.LoadFormalCatalog();
            var sourcePath = AbsoluteAssetPath(FormalDefaultRecipePath);
            var content = File.ReadAllText(sourcePath).Replace("\r\n", "\n");
            var report = RecipeValidator.Validate(content, catalog);
            if (!report.HasErrors) report.AddRange(new VfxCompiler().DryRun(content, catalog).Report);
            var outputPath = Path.Combine(RepositoryRoot(), CanonicalRecipeRelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!report.HasErrors)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
                if (!File.Exists(outputPath) || !string.Equals(File.ReadAllText(outputPath), content, StringComparison.Ordinal)) File.WriteAllText(outputPath, content, new UTF8Encoding(false));
            }
            return new VfxAiWorkflowExportResult { Catalog = catalog, Report = report, OutputPath = outputPath, Content = content };
        }

        /// <summary>Exports bare Patch arrays derived from the canonical Recipe and validates each by applying it to an isolated asset.</summary>
        public static VfxAiWorkflowExportResult ExportCanonicalPatches()
        {
            var catalog = VfxCompiler.LoadFormalCatalog();
            var report = new ValidationReport(); report.AddRange(catalog.Report);
            var source = File.ReadAllText(AbsoluteAssetPath(FormalDefaultRecipePath)).Replace("\r\n", "\n");
            JObject canonical = null;
            try { canonical = JObject.Parse(source); }
            catch (Exception exception) { report.Add("E900", ValidationSeverity.Error, "/", "Formal canonical Recipe could not be read for Patch example export: " + exception.Message); }
            var examples = new JArray();
            if (!report.HasErrors)
            {
                try
                {
                    var embers = Module(canonical, "travel", "embers");
                    var replace = new JArray(new JObject { ["op"] = "replace", ["path"] = "/stages/travel/modules/" + (string)embers["id"] + "/parameters/rate", ["value"] = 9 });
                    var disable = new JArray(new JObject { ["op"] = "disable", ["path"] = "/stages/travel/modules/" + (string)embers["id"] });
                    var addedModule = (JObject)embers.DeepClone(); addedModule["id"] = "sample_embers"; addedModule["parameters"]["rate"] = 6; addedModule["parameters"]["lifetime"] = 0.4; addedModule["enabled"] = true;
                    var add = new JArray(new JObject { ["op"] = "add", ["path"] = "/stages/travel/modules/" + (string)addedModule["id"], ["value"] = addedModule });
                    examples.Add(Example("replace", replace)); examples.Add(Example("disable", disable)); examples.Add(Example("add", add));
                    VerifyCanonicalPatch(source, replace, "replace", report); VerifyCanonicalPatch(source, disable, "disable", report); VerifyCanonicalPatch(source, add, "add", report);
                }
                catch (Exception exception) { report.Add("E900", ValidationSeverity.Error, "/", "Canonical Patch example export failed: " + exception.Message); }
            }
            var content = GenerateCanonicalPatchesMarkdown(examples);
            var outputPath = Path.Combine(RepositoryRoot(), CanonicalPatchesRelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!report.HasErrors)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
                if (!File.Exists(outputPath) || !string.Equals(File.ReadAllText(outputPath), content, StringComparison.Ordinal)) File.WriteAllText(outputPath, content, new UTF8Encoding(false));
            }
            return new VfxAiWorkflowExportResult { Catalog = catalog, Report = report, OutputPath = outputPath, Content = content };
        }

        public static string GenerateMarkdown(TemplateCatalog catalog)
        {
            var builder = new StringBuilder();
            builder.Append("# Template parameter table (generated)\n\n");
            builder.Append("> Generated by `Tools/VFX Composer/AI Workflow/Export Template Parameter Table` from the resolved `TemplateCatalog` and formal Manifest assets. **Do not edit this file by hand.** Re-export after a Manifest change.\n\n");
            builder.Append("> This table is the only AI-facing source for template IDs, parameter names, types, defaults, ranges, bindings, and cost facts. The C# Validator and Compiler remain authoritative.\n\n");
            builder.Append("| Template ID | Version | Kind | Dimension | Parameter | Type | Min | Default | Max | Binding | Cost (peak particles/materials/trails) |\n");
            builder.Append("|---|---|---|---|---|---|---:|---:|---:|---|---|\n");
            if (catalog != null)
            {
                foreach (var manifest in catalog.ByTemplateId.Values.OrderBy(value => value.TemplateId, StringComparer.Ordinal))
                {
                    foreach (var parameter in manifest.Parameters.OrderBy(value => value.Key, StringComparer.Ordinal))
                    {
                        var declaration = parameter.Value;
                        builder.Append("| ").Append(Cell(manifest.TemplateId)).Append(" | ").Append(Cell(manifest.TemplateVersion)).Append(" | ")
                            .Append(Cell(EnumText(manifest.Kind))).Append(" | ").Append(Cell(EnumText(manifest.Dimension))).Append(" | ")
                            .Append(Cell(parameter.Key)).Append(" | ").Append(Cell(EnumText(declaration.Type))).Append(" | ")
                            .Append(Cell(TokenText(declaration.Min))).Append(" | ").Append(Cell(TokenText(declaration.Default))).Append(" | ")
                            .Append(Cell(TokenText(declaration.Max))).Append(" | ").Append(Cell(declaration.Binding)).Append(" | ")
                            .Append(manifest.Cost.EstimatedPeakParticles.ToString(CultureInfo.InvariantCulture)).Append(" / ")
                            .Append(manifest.Cost.Materials.ToString(CultureInfo.InvariantCulture)).Append(" / ")
                            .Append(manifest.Cost.Trails.ToString(CultureInfo.InvariantCulture)).Append(" |\n");
                    }
                }
            }
            return builder.ToString();
        }

        private static string GenerateCanonicalPatchesMarkdown(JArray examples)
        {
            var builder = new StringBuilder();
            builder.Append("# Canonical Patch examples (generated)\n\n");
            builder.Append("> Generated from the formal canonical Recipe and live Catalog. Each bare array is applied through the real `VfxPatchService` to an isolated revision-1 Recipe; the exporter verifies revision `1->2`, history, and the requested effect. **Do not edit this file by hand.**\n\n");
            foreach (var example in examples.Children<JObject>()) builder.Append("## ").Append((string)example["label"]).Append("\n\n```json\n").Append(example["patch"].ToString(Newtonsoft.Json.Formatting.Indented)).Append("\n```\n\n");
            return builder.ToString();
        }

        private static JObject Example(string label, JArray patch) { return new JObject { ["label"] = label, ["patch"] = patch }; }
        private static JObject Module(JObject recipe, string stageId, string moduleId)
        {
            var stage = recipe["stages"].Children<JObject>().Single(item => (string)item["id"] == stageId);
            return stage["modules"].Children<JObject>().Single(item => (string)item["id"] == moduleId);
        }
        private static void VerifyCanonicalPatch(string source, JArray patch, string label, ValidationReport report)
        {
            const string basePath = "Assets/VFX/Recipes/s9_canonical_patch_export_base.json";
            var absolute = AbsoluteAssetPath(basePath); var history = absolute + VfxPatchService.HistorySuffix;
            try
            {
                File.WriteAllText(absolute, source.Replace("\"id\": \"fireball_2d\"", "\"id\": \"s9_canonical_patch_export_base\"")); AssetDatabase.ImportAsset(basePath, ImportAssetOptions.ForceUpdate);
                var result = new VfxPatchService().ApplyToAsset(basePath, patch.ToString(Newtonsoft.Json.Formatting.None), 1);
                if (!result.IsValid || result.BeforeRevision != 1 || result.AfterRevision != 2) throw new InvalidOperationException(label + " did not apply revision 1->2.");
                var entries = JArray.Parse(File.ReadAllText(history)); var last = (JObject)entries.Last; if ((int)last["beforeRevision"] != 1 || (int)last["afterRevision"] != 2) throw new InvalidOperationException(label + " did not record history 1->2.");
                var actual = JObject.Parse(File.ReadAllText(absolute)); var operation = (JObject)patch[0]; var path = ((string)operation["path"]).Split('/');
                if ((string)operation["op"] == "replace" && !JToken.DeepEquals(Module(actual, path[2], path[4])["parameters"][path[6]], operation["value"])) throw new InvalidOperationException(label + " effect was not persisted.");
                if ((string)operation["op"] == "disable" && (bool)Module(actual, path[2], path[4])["enabled"]) throw new InvalidOperationException(label + " effect was not persisted.");
                if ((string)operation["op"] == "add" && !JToken.DeepEquals(Module(actual, path[2], path[4]), operation["value"])) throw new InvalidOperationException(label + " effect was not persisted.");
            }
            catch (Exception exception) { report.Add("E900", ValidationSeverity.Error, "/" + label, exception.Message); }
            finally { CleanupCanonicalPatchExport(basePath); }
        }
        private static void CleanupCanonicalPatchExport(string assetPath)
        {
            var recipe = AbsoluteAssetPath(assetPath); var history = assetPath + VfxPatchService.HistorySuffix;
            var parsed = VfxDomainParser.ParseRecipe(File.Exists(recipe) ? File.ReadAllText(recipe) : string.Empty);
            if (parsed.Value != null && AssetDatabase.IsValidFolder(VfxCompiler.OutputFolder(parsed.Value))) AssetDatabase.DeleteAsset(VfxCompiler.OutputFolder(parsed.Value));
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath) != null) AssetDatabase.DeleteAsset(assetPath);
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(history) != null) AssetDatabase.DeleteAsset(history); AssetDatabase.Refresh();
        }

        private static string RepositoryRoot() { return Directory.GetParent(Directory.GetParent(Application.dataPath).FullName).FullName; }
        private static string AbsoluteAssetPath(string assetPath) { return Path.Combine(Application.dataPath, assetPath.Substring("Assets/".Length)); }
        private static string TokenText(JToken token) { return token == null ? string.Empty : token.ToString(Newtonsoft.Json.Formatting.None); }
        private static string Cell(string text) { return (text ?? string.Empty).Replace("|", "\\|").Replace("\r", " ").Replace("\n", " "); }
        private static string EnumText(object value)
        {
            if (value is RecipeDimension) return (RecipeDimension)value == RecipeDimension.TwoD ? "2d" : "3d";
            if (value is ManifestParameterType) return value.ToString().ToLowerInvariant();
            switch (value.ToString())
            {
                case "EnergyBody": return "energy_body";
                case "SpriteEmitter": return "sprite_emitter";
                case "SecondaryParticles": return "secondary_particles";
                case "MotionTrail": return "motion_trail";
                case "ImpactFlash": return "impact_flash";
                case "ImpactBurst": return "impact_burst";
                case "Shockwave": return "shockwave";
                case "SubEffect": return "sub_effect";
                default: return value.ToString().ToLowerInvariant();
            }
        }
        private static string Describe(ValidationReport report) { return string.Join("\n", report.Entries.Select(entry => entry.Code + " " + entry.Path + " " + entry.Message)); }
    }

    public sealed class VfxAiWorkflowExportResult
    {
        public TemplateCatalog Catalog;
        public ValidationReport Report;
        public string OutputPath;
        public string Content;
    }
}

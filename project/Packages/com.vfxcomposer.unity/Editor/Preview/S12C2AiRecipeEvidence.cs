using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VFXComposer;
using VFXComposer.Editor.Build;
using VFXComposer.Editor.Domain;
using VFXComposer.Editor.SlashV2;
using VFXComposer.Editor.Validation;
using VFXComposer.Editor.Workflow;

namespace VFXComposer.Editor.Preview
{
    /// <summary>One-time, auditable capture of the supplied S12 AI recipe. It never dispatches an agent or a Patch operation.</summary>
    public static class S12C2AiRecipeEvidence
    {
        public const string AttemptRelative = "docs/ai-workflow/s12-slash-v3/runtime/recipe.attempt0.json";
        public const string FrozenAcceptanceRelative = "docs/ai-workflow/s12-slash-v3/frozen/acceptance-spec.generated.json";
        public const string FrozenContractRelative = "docs/ai-workflow/s12-slash-v3/frozen/contract.generated.json";
        public const string EvidenceRelative = "docs/stage-notes/s12c2-evidence";
        public const string LocalRoot = "Assets/VFX/Preview/S12_AI_ValidatedSlash";
        public const string LocalPrefabPath = LocalRoot + "/VFX_Slash_3D_Stylized_AI_Validated.prefab";
        public const string LocalScenePath = LocalRoot + "/S12_AI_ValidatedSlashPreview.unity";
        private const string ReportName = "runtime-report.json";
        private const int Width = 960;
        private const int Height = 540;
        private const int EvidenceLayer = 31;

        [MenuItem("VFX Composer/S12C2/Record AI Recipe Runtime Evidence (write once)")]
        public static void RecordFromMenu() { EnsureRecorded(); }

        [MenuItem("VFX Composer/S12C2/Discard Rejected Pre-Isolation Capture")]
        public static void DiscardRejectedUnisolatedFirstCapture()
        {
            var metadataPath = EvidencePath("metadata.json"); Require(File.Exists(EvidencePath(ReportName)) && File.Exists(metadataPath), "There is no completed S12C2 recording to assess.");
            Require((string)JObject.Parse(File.ReadAllText(metadataPath))["cameraIsolation"] != "dedicated-layer-31", "A completed isolated S12C2 recording is immutable.");
            CleanupPartialFirstRecording(true);
        }

        /// <summary>Records once when absent; later calls are strictly verification-only.</summary>
        public static void EnsureRecorded()
        {
            var reportPath = EvidencePath(ReportName);
            if (File.Exists(reportPath))
            {
                if (!VerifyExistingEvidence()) throw new InvalidOperationException("Existing S12C2 report/evidence is incomplete or inconsistent; it is write-once and will not be replaced.");
                return;
            }
            var evidenceDirectory = EvidencePath(string.Empty);
            if ((Directory.Exists(evidenceDirectory) && Directory.EnumerateFileSystemEntries(evidenceDirectory).Any()) || AssetDatabase.IsValidFolder(LocalRoot)) CleanupPartialFirstRecording();

            var canonical = File.ReadAllText(Absolute("Assets/VFX/Recipes/Slash/slash-3d-stylized.default.v2.json"));
            var attempt = File.ReadAllText(RepositoryPath(AttemptRelative));
            try
            {
                JArray parserErrors; JArray validatorErrors; JArray budgetErrors; var recipe = ValidateAttemptAgainstFrozenContract(attempt, out parserErrors, out validatorErrors, out budgetErrors);
                var compiler = new S12SlashCompiler();
                var dryRun = compiler.DryRun(attempt);
                Require(!dryRun.IsBlocked, "Real S12 Slash DryRun was blocked: " + Describe(dryRun));
                var built = compiler.Build(attempt);
                Require(built.Succeeded, "Real S12 Slash Build failed: " + Describe(built.Plan));

                var manifest = JObject.Parse(File.ReadAllText(Absolute(S12SlashCompiler.ManifestPath)));
                var sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(S12SlashCompiler.OutputPrefabPath);
                Require(sourcePrefab != null, "AI Recipe Build did not produce its managed generated prefab.");
                SnapshotLocally(sourcePrefab);
                CaptureGeneratedAiFrames(sourcePrefab, manifest, recipe);
                WriteOnceReport(attempt, manifest, recipe, dryRun, parserErrors, validatorErrors, budgetErrors);
                Require(VerifyExistingEvidence(), "S12C2 write-once evidence did not pass self-verification.");
            }
            catch
            {
                CleanupPartialFirstRecording(true);
                throw;
            }
            finally
            {
                var restored = new S12SlashCompiler().Build(canonical);
                Require(restored.Succeeded, "Could not restore canonical S12 generated output: " + Describe(restored.Plan));
                var manifest = JObject.Parse(File.ReadAllText(Absolute(S12SlashCompiler.ManifestPath)));
                Require((string)manifest["recipeHash"] == RecipeCanonicalizer.ComputeSha256(canonical), "Canonical generated output does not match the canonical Recipe after S12C2.");
                AssertNoBuildResidue();
            }
        }

        public static bool VerifyExistingEvidence()
        {
            try
            {
                if (!S12SlashAiExporter.VerifyExisting()) return false;
                var reportPath = EvidencePath(ReportName); var metadataPath = EvidencePath("metadata.json");
                if (!File.Exists(reportPath) || !File.Exists(metadataPath) || !File.Exists(Absolute(LocalPrefabPath)) || !File.Exists(Absolute(LocalScenePath))) return false;
                var report = JObject.Parse(File.ReadAllText(reportPath)); var metadata = JObject.Parse(File.ReadAllText(metadataPath));
                if ((string)report["agent"]["model"] != "gpt-5.6-terra" || (string)report["agent"]["reasoning"] != "high" || (string)report["agent"]["fork"] != "none" || (string)report["agent"]["thread"] != "/root/s12_ai_recipe") return false;
                if (!(bool)report["succeeded"] || (string)report["source"]["attemptPath"] != AttemptRelative || (string)report["source"]["frozenAcceptancePath"] != FrozenAcceptanceRelative || (string)report["source"]["frozenContractPath"] != FrozenContractRelative) return false;
                if ((string)report["attemptFileBytesSha256"] != HashFile(RepositoryPath(AttemptRelative)) || (string)report["recipeCanonicalSha256"] != RecipeCanonicalizer.ComputeSha256(File.ReadAllText(RepositoryPath(AttemptRelative)))) return false;
                var errors = (JObject)report["validationReportErrors"]; if (errors == null || errors.Properties().Any(property => ((JArray)property.Value).Count != 0)) return false;
                if ((string)report["localSnapshot"]["prefabPath"] != LocalPrefabPath || (string)report["localSnapshot"]["prefabGuid"] != AssetDatabase.AssetPathToGUID(LocalPrefabPath)) return false;
                var expected = (JObject)report["actualParameters"];
                if (Math.Abs((float)expected["width"] - .3f) > .0001f || (int)expected["sparkCount"] != 18 || Math.Abs((float)expected["afterimageAlpha"] - .4f) > .0001f) return false;
                if (!VerifyLocalSnapshot(expected)) return false;
                var frames = (JArray)metadata["timelineFrames"];
                if (frames == null || frames.Count != 4) return false;
                foreach (var frame in frames.Children<JObject>()) { var png = EvidencePath((string)frame["file"]); if (!File.Exists(png) || HashFile(png) != (string)frame["sha256"]) return false; }
                var primary = WarmPixels(EvidencePath("time_primary_overlap.png")); var afterimage = WarmPixels(EvidencePath("time_afterimage.png")); var dissipation = WarmPixels(EvidencePath("time_dissipation.png"));
                return (string)metadata["cameraIsolation"] == "dedicated-layer-31" && primary > afterimage && afterimage > dissipation && dissipation > 0 && WarmPixels(EvidencePath("time_complete.png")) == 0 && (string)metadata["recipeCanonicalSha256"] == (string)report["recipeCanonicalSha256"] && (string)metadata["buildHash"] == (string)report["buildHash"] && (string)metadata["sourcePrefabGuid"] == (string)report["generatedPrefabGuid"];
            }
            catch { return false; }
        }

        public static void AssertNoBuildResidue()
        {
            var generated = Absolute(S12SlashCompiler.GeneratedRoot);
            Require(!Directory.GetDirectories(generated, "s12btmp_*", SearchOption.TopDirectoryOnly).Any(), "S12C2 left a generated temporary folder.");
            Require(!Directory.GetFiles(generated, "*.pending", SearchOption.AllDirectories).Any(), "S12C2 left a pending generated file.");
            Require(!Directory.GetDirectories(Path.GetTempPath(), "vfxcomposer_s12b_*", SearchOption.TopDirectoryOnly).Any(), "S12C2 left an OS Slash build backup.");
        }

        private static S12SlashRecipe ValidateAttemptAgainstFrozenContract(string attempt, out JArray parserErrors, out JArray validatorErrors, out JArray budgetErrors)
        {
            Require(S12SlashAiExporter.VerifyExisting(), "Frozen S12 AI contract hash manifest failed verification.");
            var dispatch = S12RecipeDispatcher.Parse(attempt); parserErrors = Errors(dispatch.Report);
            Require(!dispatch.Report.HasErrors && dispatch.RecipeVersion == 2 && dispatch.SlashV2 != null, "Real S12 recipe parser rejected attempt0.");
            var catalog = S12SlashCompiler.LoadFormalCatalog(); Require(!catalog.Report.HasErrors, "Formal S12 catalog is invalid.");
            var validator = S12SlashV2Validator.Validate(attempt, catalog); validatorErrors = Errors(validator); Require(!validator.HasErrors, "Real S12 recipe validator rejected attempt0.");
            var budget = S12SlashBudgetCalculator.Evaluate(dispatch.SlashV2, catalog); budgetErrors = Errors(budget); Require(!budget.HasErrors, "Real S12 budget calculator rejected attempt0.");

            var root = JObject.Parse(attempt); var acceptance = JObject.Parse(File.ReadAllText(RepositoryPath(FrozenAcceptanceRelative))); var contract = JObject.Parse(File.ReadAllText(RepositoryPath(FrozenContractRelative)));
            Require((int)acceptance["runtimeEvidence"] == 0 && (int)contract["runtimeEvidence"] == 0, "Frozen preregistration must remain runtimeEvidence:0.");
            foreach (var property in ((JObject)acceptance["recipe"]["exact"]).Properties()) Require(JToken.DeepEquals(root[property.Name], property.Value), "Attempt does not exactly match frozen acceptance field " + property.Name + ".");
            var phases = (JArray)root["phases"]; CollectionAssertExact(phases.Select(item => (string)item["id"]), ((JArray)acceptance["recipe"]["phaseIds"]).Values<string>(), "frozen phase order");
            foreach (var change in ((JArray)acceptance["recipe"]["requiredChanges"]).Children<JObject>())
            {
                var parts = ((string)change["path"]).Split('/'); Require(parts.Length == 7 && parts[1] == "phases" && parts[3] == "modules" && parts[5] == "parameters", "Frozen acceptance path grammar changed: " + change["path"]);
                var phase = phases.Children<JObject>().Single(item => (string)item["id"] == parts[2]); var module = ((JArray)phase["modules"]).Children<JObject>().Single(item => (string)item["id"] == parts[4]); var actual = module["parameters"][parts[6]]; var declaration = contract["catalog"][(string)module["templateId"]][parts[6]];
                Require(actual != null && declaration != null, "Frozen acceptance refers to an undeclared parameter: " + change["path"]);
                var comparison = (string)change["comparison"]; var value = (double)actual; var defaultValue = (double)declaration["default"]; var max = (double)declaration["max"];
                Require(value > defaultValue, "Frozen > default rule failed for " + change["path"]);
                if (comparison.StartsWith("integer", StringComparison.Ordinal)) Require(actual.Type == JTokenType.Integer, "Frozen integer rule failed for " + change["path"]);
                var ceiling = comparison.Contains("<= max") ? max : NumericCeiling(comparison); Require(value <= ceiling + .0000001d, "Frozen upper-bound rule failed for " + change["path"]);
            }
            return dispatch.SlashV2;
        }

        private static double NumericCeiling(string comparison)
        {
            var marker = comparison.LastIndexOf("<=", StringComparison.Ordinal); Require(marker >= 0, "Frozen comparison lacks an upper bound: " + comparison); double value;
            Require(double.TryParse(comparison.Substring(marker + 2).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value), "Frozen comparison upper bound is not numeric: " + comparison); return value;
        }

        private static void SnapshotLocally(GameObject sourcePrefab)
        {
            Require(!AssetDatabase.IsValidFolder(LocalRoot), "AI local snapshot already exists; it is write-once.");
            EnsureFolder(LocalRoot); EnsureFolder(LocalRoot + "/Materials");
            Require(AssetDatabase.CopyAsset(S12SlashCompiler.OutputPrefabPath, LocalPrefabPath), "Could not deep-copy AI generated Prefab locally.");
            var sourceMaterials = sourcePrefab.GetComponentsInChildren<Renderer>(true).SelectMany(renderer => renderer.sharedMaterials).Where(material => material != null).Select(AssetDatabase.GetAssetPath).Distinct(StringComparer.Ordinal).OrderBy(path => path, StringComparer.Ordinal).ToArray();
            var localMaterials = new Dictionary<string, Material>(StringComparer.Ordinal);
            foreach (var source in sourceMaterials)
            {
                var destination = LocalRoot + "/Materials/" + Path.GetFileName(source); Require(AssetDatabase.CopyAsset(source, destination), "Could not deep-copy AI material dependency: " + source); var local = AssetDatabase.LoadAssetAtPath<Material>(destination); Require(local != null, "Local AI material dependency did not import: " + destination); localMaterials.Add(source, local);
            }
            var contents = PrefabUtility.LoadPrefabContents(LocalPrefabPath);
            try
            {
                foreach (var renderer in contents.GetComponentsInChildren<Renderer>(true)) { var materials = renderer.sharedMaterials; for (var i = 0; i < materials.Length; i++) if (materials[i] != null) materials[i] = localMaterials[AssetDatabase.GetAssetPath(materials[i])]; renderer.sharedMaterials = materials; }
                PrefabUtility.SaveAsPrefabAsset(contents, LocalPrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh(); CreateLocalScene();
        }

        private static void CreateLocalScene()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LocalPrefabPath); Require(prefab != null, "Local AI prefab cannot be loaded for the runtime validation scene.");
            if (Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).Any(item => string.IsNullOrEmpty(item.path))) EditorSceneManager.OpenScene("Assets/VFX/Preview/S12_SlashGoldSample.unity", OpenSceneMode.Single);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try { var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject; SceneManager.MoveGameObjectToScene(instance, scene); instance.name = "S12_AI_ValidatedSlash"; EditorSceneManager.SaveScene(scene, LocalScenePath); }
            finally { EditorSceneManager.CloseScene(scene, true); }
            if (EditorBuildSettings.scenes.All(item => !string.Equals(item.path, LocalScenePath, StringComparison.Ordinal))) EditorBuildSettings.scenes = EditorBuildSettings.scenes.Concat(new[] { new EditorBuildSettingsScene(LocalScenePath, true) }).ToArray();
        }

        private static void CaptureGeneratedAiFrames(GameObject prefab, JObject manifest, S12SlashRecipe recipe)
        {
#if false // Historical one-shot evidence used the rejected seek sampler and is not regenerable.
            Directory.CreateDirectory(EvidencePath(string.Empty)); var cameraGo = new GameObject("S12C2_AiRecipeEvidenceCamera"); var camera = cameraGo.AddComponent<Camera>(); camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.16f, .17f, .19f); camera.allowHDR = false; camera.allowMSAA = false; camera.fieldOfView = 60f; camera.cullingMask = 1 << EvidenceLayer; camera.transform.position = new Vector3(0f, 2.4f, -7.6f); camera.transform.LookAt(new Vector3(0f, .38f, 0f)); var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject; SetLayerRecursively(instance, EvidenceLayer);
            try
            {
                var controller = instance.GetComponent<SlashEffectController>(); Require(controller != null, "AI generated prefab lacks SlashEffectController."); controller.PlaySlash(Vector3.zero, Quaternion.identity); var frames = new[] { new Frame("primary_overlap", .18f), new Frame("afterimage", .24f), new Frame("dissipation", .38f), new Frame("complete", .451f) }; var result = new JArray();
                foreach (var frame in frames) { controller.SampleForPreview(frame.Time); var file = "time_" + frame.Name + ".png"; var path = EvidencePath(file); Require(!File.Exists(path), "S12C2 screenshot is write-once: " + file); Capture(camera, path); result.Add(new JObject { ["phase"] = frame.Name, ["time"] = frame.Time, ["file"] = file, ["sha256"] = HashFile(path), ["particles"] = new JArray(instance.GetComponentsInChildren<ParticleSystem>(true).Select(ParticleFacts)) }); }
                var metadata = new JObject { ["capture"] = "real generated AI Recipe Prefab instantiated; SlashEffectController.PlaySlash then deterministic controller sampling with natural template particle simulation; Camera.Render; HDR false; Bloom off; no ParticleSystem.Emit", ["cameraIsolation"] = "dedicated-layer-31", ["sourcePrefabPath"] = S12SlashCompiler.OutputPrefabPath, ["sourcePrefabGuid"] = AssetDatabase.AssetPathToGUID(S12SlashCompiler.OutputPrefabPath), ["recipeCanonicalSha256"] = (string)manifest["recipeHash"], ["buildHash"] = (string)manifest["buildHash"], ["actualParameters"] = ActualParameters(recipe), ["fov"] = 60, ["timelineFrames"] = result };
                WriteNew(EvidencePath("metadata.json"), metadata.ToString(Formatting.Indented));
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); UnityEngine.Object.DestroyImmediate(cameraGo); }
#endif
            throw new InvalidOperationException("S12C2 seek-sampled frames are historical evidence and cannot be regenerated. Use continuous serialized-camera capture.");
        }

        private static void WriteOnceReport(string attempt, JObject manifest, S12SlashRecipe recipe, VfxBuildPlan dryRun, JArray parserErrors, JArray validatorErrors, JArray budgetErrors)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LocalPrefabPath); var localMaterials = prefab.GetComponentsInChildren<Renderer>(true).SelectMany(renderer => renderer.sharedMaterials).Where(material => material != null).Select(AssetDatabase.GetAssetPath).Distinct(StringComparer.Ordinal).OrderBy(path => path, StringComparer.Ordinal).ToArray();
            var report = new JObject { ["schema"] = "s12c2-ai-recipe-runtime-evidence/v1", ["succeeded"] = true, ["agent"] = new JObject { ["model"] = "gpt-5.6-terra", ["reasoning"] = "high", ["fork"] = "none", ["thread"] = "/root/s12_ai_recipe" }, ["source"] = new JObject { ["attemptPath"] = AttemptRelative, ["frozenAcceptancePath"] = FrozenAcceptanceRelative, ["frozenContractPath"] = FrozenContractRelative }, ["attemptFileBytesSha256"] = HashFile(RepositoryPath(AttemptRelative)), ["recipeCanonicalSha256"] = (string)manifest["recipeHash"], ["buildHash"] = (string)manifest["buildHash"], ["generatedPrefabPath"] = S12SlashCompiler.OutputPrefabPath, ["generatedPrefabGuid"] = AssetDatabase.AssetPathToGUID(S12SlashCompiler.OutputPrefabPath), ["actualParameters"] = ActualParameters(recipe), ["realOperations"] = new JObject { ["parser"] = true, ["validator"] = true, ["budget"] = true, ["dryRun"] = !dryRun.IsBlocked, ["build"] = true }, ["validationReportErrors"] = new JObject { ["parser"] = parserErrors, ["validator"] = validatorErrors, ["budget"] = budgetErrors, ["dryRun"] = Errors(dryRun.Report) }, ["localSnapshot"] = new JObject { ["prefabPath"] = LocalPrefabPath, ["prefabGuid"] = AssetDatabase.AssetPathToGUID(LocalPrefabPath), ["scenePath"] = LocalScenePath, ["materialPaths"] = new JArray(localMaterials), ["materialGuids"] = new JArray(localMaterials.Select(AssetDatabase.AssetPathToGUID)) } };
            WriteNew(EvidencePath(ReportName), report.ToString(Formatting.Indented));
        }

        private static JObject ActualParameters(S12SlashRecipe recipe)
        {
            var primary = recipe.Phases.Single(item => item.Id == "primary_arc").Modules.Single(); var sparks = recipe.Phases.Single(item => item.Id == "sparks").Modules.Single(); var after = recipe.Phases.Single(item => item.Id == "afterimage").Modules.Single();
            return new JObject { ["width"] = primary.Parameters["width"].DeepClone(), ["sparkCount"] = sparks.Parameters["count"].DeepClone(), ["afterimageAlpha"] = after.Parameters["alpha"].DeepClone() };
        }

        private static bool VerifyLocalSnapshot(JObject expected)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LocalPrefabPath); if (prefab == null || prefab.GetComponentsInChildren<MonoBehaviour>(true).Any(component => component == null)) return false;
            var materials = prefab.GetComponentsInChildren<Renderer>(true).SelectMany(renderer => renderer.sharedMaterials).Where(material => material != null).ToArray(); if (!materials.Any() || materials.Any(material => !AssetDatabase.GetAssetPath(material).StartsWith(LocalRoot + "/Materials/", StringComparison.Ordinal) || string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(material))))) return false;
            var width = prefab.transform.Find("Primary_arc/Arc_sweep/RibbonWidthControl"); var sparks = prefab.transform.Find("Sparks/Slash_sparks"); var alpha = prefab.GetComponentInChildren<SlashAfterimageAlpha>(true); if (width == null || sparks == null || alpha == null) return false;
            var particle = sparks.GetComponent<ParticleSystem>(); if (particle == null) return false; var emission = particle.emission; var burst = emission.GetBurst(0);
            return Mathf.Abs(width.localScale.x - ((float)expected["width"] / .24f)) < .0001f && burst.maxCount == (short)(int)expected["sparkCount"] && Mathf.Abs(alpha.Alpha - (float)expected["afterimageAlpha"]) < .0001f;
        }

        private static JObject ParticleFacts(ParticleSystem particle)
        {
            var values = new ParticleSystem.Particle[particle.particleCount]; var count = particle.GetParticles(values); var distinct = values.Take(count).Select(value => value.position.ToString("F4")).Distinct(StringComparer.Ordinal).Count(); return new JObject { ["name"] = particle.name, ["particleCount"] = count, ["distinctPositions"] = distinct };
        }
        private static void Capture(Camera camera, string path) { var render = RenderTexture.GetTemporary(Width, Height, 24, RenderTextureFormat.ARGB32); var old = RenderTexture.active; try { camera.targetTexture = render; camera.Render(); RenderTexture.active = render; var texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false); texture.ReadPixels(new Rect(0, 0, Width, Height), 0, 0); texture.Apply(false); File.WriteAllBytes(path, texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture); } finally { camera.targetTexture = null; RenderTexture.active = old; RenderTexture.ReleaseTemporary(render); } }
        private static int WarmPixels(string path) { var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false); try { texture.LoadImage(File.ReadAllBytes(path)); return texture.GetPixels32().Count(color => color.r > 100 && color.r > color.g + 20 && color.r > color.b + 10); } finally { UnityEngine.Object.DestroyImmediate(texture); } }
        private static void SetLayerRecursively(GameObject value, int layer) { value.layer = layer; foreach (Transform child in value.transform) SetLayerRecursively(child.gameObject, layer); }
        private static void EnsureFolder(string path) { if (AssetDatabase.IsValidFolder(path)) return; var parent = Path.GetDirectoryName(path).Replace('\\', '/'); EnsureFolder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(path)); }
        private static void WriteNew(string path, string text) { if (File.Exists(path)) throw new InvalidOperationException("S12C2 report/evidence is write-once: " + Path.GetFileName(path)); File.WriteAllText(path, text, new UTF8Encoding(false)); }
        private static JArray Errors(ValidationReport report) { return new JArray(report.Entries.Where(entry => entry.Severity == ValidationSeverity.Error).Select(entry => new JObject { ["code"] = entry.Code, ["path"] = entry.Path, ["message"] = entry.Message, ["actualValue"] = entry.ActualValue == null ? null : entry.ActualValue.DeepClone(), ["allowedRange"] = entry.AllowedRange })); }
        private static void CleanupPartialFirstRecording(bool force = false) { if (!force && File.Exists(EvidencePath(ReportName))) return; if (AssetDatabase.IsValidFolder(LocalRoot)) AssetDatabase.DeleteAsset(LocalRoot); var evidence = EvidencePath(string.Empty); if (Directory.Exists(evidence)) Directory.Delete(evidence, true); AssetDatabase.Refresh(); }
        private static void CollectionAssertExact(IEnumerable<string> actual, IEnumerable<string> expected, string description) { if (!actual.SequenceEqual(expected, StringComparer.Ordinal)) throw new InvalidOperationException("S12C2 " + description + " does not match frozen acceptance."); }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private static string Describe(VfxBuildPlan plan) { return string.Join(" | ", plan.Report.Entries.Select(item => item.Code + " " + item.Path)); }
        private static string HashFile(string path) { using (var sha = SHA256.Create()) return string.Concat(sha.ComputeHash(File.ReadAllBytes(path)).Select(value => value.ToString("X2"))); }
        private static string RepositoryPath(string relative) { return Path.Combine(RepositoryRoot(), relative.Replace('/', Path.DirectorySeparatorChar)); }
        private static string EvidencePath(string file) { return Path.Combine(RepositoryPath(EvidenceRelative), file); }
        private static string Absolute(string assetPath) { return Path.Combine(Application.dataPath, assetPath.Substring("Assets/".Length)); }
        private static string RepositoryRoot() { return Directory.GetParent(Directory.GetParent(Application.dataPath).FullName).FullName; }
        private struct Frame { public readonly string Name; public readonly float Time; public Frame(string name, float time) { Name = name; Time = time; } }
    }
}

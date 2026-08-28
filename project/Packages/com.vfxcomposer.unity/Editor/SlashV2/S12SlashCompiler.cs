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
using UnityEngine;
using VFXComposer;
using VFXComposer.Editor.Build;
using VFXComposer.Editor.Catalog;
using VFXComposer.Editor.Domain;
using VFXComposer.Editor.Rules;
using VFXComposer.Editor.Validation;

namespace VFXComposer.Editor.SlashV2
{
    internal interface IS12SlashBuildHook { void AfterPrefabAndMaterialsSaved(string outputFolder); }

    [Serializable]
    internal sealed class S12SlashBuildManifest
    {
        public string RecipeId; public int RecipeRevision; public string RecipeHash; public string BuildHash; public string CompilerVersion; public string UnityVersion; public string OutputPrefabPath; public List<VfxBuildTemplate> Templates = new List<VfxBuildTemplate>(); public List<string> OutputMaterialPaths = new List<string>();
    }

    /// <summary>Isolated v2 Slash compiler. It does not call, alter, or write through the v1 compiler path.</summary>
    public sealed class S12SlashCompiler
    {
        public const string CompilerVersion = "s12b-3";
        public const string GeneratedRoot = "Assets/VFX/Generated";
        public const string OutputFolderPath = GeneratedRoot + "/slash_3d_stylized";
        public const string OutputPrefabPath = OutputFolderPath + "/VFX_Slash_3D_Stylized.prefab";
        public const string ManifestPath = OutputFolderPath + "/BuildManifest.json";
        private readonly S12SlashBindingRegistry bindings;
        private readonly ITemplateDependencyHashProvider dependencyHashes;
        private readonly IS12SlashBuildHook hook;

        public S12SlashCompiler() : this(null, null, null) { }
        internal S12SlashCompiler(S12SlashBindingRegistry bindings, IS12SlashBuildHook hook, ITemplateDependencyHashProvider dependencyHashes)
        {
            this.bindings = bindings ?? S12SlashBindingRegistry.CreateFormal(); this.hook = hook; this.dependencyHashes = dependencyHashes ?? new UnityTemplateDependencyHashProvider();
        }

        public static S12SlashTemplateCatalog LoadFormalCatalog()
        {
            return S12SlashTemplateCatalog.Load(Path.Combine(Application.dataPath, "VFX", "Templates", "3D", "SlashManifests"), new UnityAssetReferenceResolver());
        }

        public VfxBuildPlan Validate(string recipeJson, S12SlashTemplateCatalog catalog = null) { return DryRun(recipeJson, catalog); }

        public VfxBuildPlan DryRun(string recipeJson, S12SlashTemplateCatalog catalog = null)
        {
            catalog = catalog ?? LoadFormalCatalog(); var plan = new VfxBuildPlan(); var dispatch = S12RecipeDispatcher.Parse(recipeJson);
            plan.Report.AddRange(dispatch.Report);
            if (!plan.Report.HasErrors && dispatch.RecipeVersion != 2) plan.Report.Add("E1250", ValidationSeverity.Error, "/recipeVersion", "S12 Slash compiler accepts Recipe v2 only; v1 is owned by VfxCompiler.");
            if (!plan.Report.HasErrors && !string.Equals(dispatch.SlashV2.Id, "slash_3d_stylized", StringComparison.Ordinal)) plan.Report.Add("E1253", ValidationSeverity.Error, "/id", "This S12 compiler currently owns only the managed output slash_3d_stylized; another v2 id cannot overwrite it.", new JValue(dispatch.SlashV2.Id), "slash_3d_stylized");
            plan.Report.AddRange(catalog.Report);
            if (!plan.Report.HasErrors) plan.Report.AddRange(S12SlashV2Validator.Validate(recipeJson, catalog));
            if (!plan.Report.HasErrors) plan.Report.AddRange(S12SlashBudgetCalculator.Evaluate(dispatch.SlashV2, catalog));
            if (plan.Report.HasErrors) { plan.Items.Add(new VfxBuildItem { State = VfxBuildItemState.Blocked, AssetPath = OutputPrefabPath, Reason = First(plan.Report) }); return plan; }
            plan.RecipeHash = RecipeCanonicalizer.ComputeSha256(recipeJson); plan.RecipeRevision = dispatch.SlashV2.Revision; plan.BuildHash = Hash(plan.RecipeHash, dispatch.SlashV2, catalog);
            var current = LoadManifest(); var exists = AssetDatabase.LoadAssetAtPath<GameObject>(OutputPrefabPath) != null;
            var complete = current != null && exists && current.OutputMaterialPaths != null && current.OutputMaterialPaths.Count > 0 && current.OutputMaterialPaths.All(path => AssetDatabase.LoadAssetAtPath<Material>(path) != null);
            plan.Items.Add(new VfxBuildItem { State = complete && current.BuildHash == plan.BuildHash ? VfxBuildItemState.Unchanged : exists ? VfxBuildItemState.Update : VfxBuildItemState.Create, AssetPath = OutputPrefabPath, Reason = complete && current.BuildHash == plan.BuildHash ? "Canonical v2 inputs and template dependencies are unchanged." : "Managed v2 Slash output differs from the complete recorded build input set." });
            return plan;
        }

        public VfxBuildResult Build(string recipeJson, S12SlashTemplateCatalog catalog = null)
        {
            catalog = catalog ?? LoadFormalCatalog(); var plan = DryRun(recipeJson, catalog); var result = new VfxBuildResult { Plan = plan, PrefabPath = OutputPrefabPath };
            if (plan.IsBlocked) return result; if (plan.Items.All(item => item.State == VfxBuildItemState.Unchanged))
            {
                var unchangedRecipe = S12RecipeDispatcher.Parse(recipeJson).SlashV2;
                var compliance = VfxProductionRules.EnforceAndWriteManifest(unchangedRecipe.Id, "slash", 2, unchangedRecipe.Revision, plan.RecipeHash, plan.BuildHash, CompilerVersion, OutputPrefabPath, OutputFolderPath, unchangedRecipe.Timeline.Duration);
                plan.Report.AddRange(compliance.Report); result.Succeeded = !plan.Report.HasErrors; return result;
            }
            var recipe = S12RecipeDispatcher.Parse(recipeJson).SlashV2; var temp = CreateTempFolder(); var byteBackup = Path.Combine(Path.GetTempPath(), "vfxcomposer_s12b_" + Guid.NewGuid().ToString("N"));
            try { var prefab = BuildTemporary(recipe, catalog, temp); ValidatePrefab(prefab, recipe, plan.Report); if (plan.Report.HasErrors) throw new InvalidOperationException("Temporary Slash prefab validation failed."); Commit(recipe, catalog, plan, temp, byteBackup, prefab); result.Succeeded = true; }
            catch (Exception exception) { if (!plan.Report.HasErrors) plan.Report.Add("E1251", ValidationSeverity.Error, "/build", "Slash build failed: " + exception.Message); }
            finally { if (Directory.Exists(byteBackup)) Directory.Delete(byteBackup, true); if (AssetDatabase.IsValidFolder(temp)) AssetDatabase.DeleteAsset(temp); AssetDatabase.SaveAssets(); AssetDatabase.Refresh(); }
            return result;
        }

        private GameObject BuildTemporary(S12SlashRecipe recipe, S12SlashTemplateCatalog catalog, string temp)
        {
            var root = new GameObject("VFX_Slash_3D_Stylized");
            try
            {
                var controller = root.AddComponent<SlashEffectController>(); var ordinal = 0; var materialCopies = new Dictionary<string, Material>(StringComparer.Ordinal);
                foreach (var phase in recipe.Phases)
                {
                    var phaseRoot = new GameObject(StableName(phase.Id)); phaseRoot.transform.SetParent(root.transform, false); phaseRoot.SetActive(false);
                    var module = phase.Modules.Single(); S12SlashManifest manifest; if (!catalog.TryGet(module.TemplateId, out manifest)) throw new InvalidOperationException("Missing Slash template: " + module.TemplateId);
                    var template = AssetDatabase.LoadAssetAtPath<GameObject>(manifest.AssetPath); if (template == null) throw new InvalidOperationException("Missing Slash prefab: " + manifest.AssetPath);
                    var instance = PrefabUtility.InstantiatePrefab(template) as GameObject; if (instance == null) throw new InvalidOperationException("Could not instantiate Slash prefab: " + manifest.AssetPath);
                    PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction); instance.name = StableName(module.Id); instance.transform.SetParent(phaseRoot.transform, false); instance.SetActive(module.Enabled);
                    foreach (var parameter in module.Parameters.OrderBy(pair => pair.Key, StringComparer.Ordinal)) bindings.Apply(manifest.Parameters[parameter.Key].Binding, instance, parameter.Value);
                    if (module.Kind == "arc_afterimage") instance.AddComponent<SlashAfterimageAlpha>().Alpha = module.Parameters["alpha"].Value<float>();
                    foreach (var particle in instance.GetComponentsInChildren<ParticleSystem>(true)) { particle.useAutoRandomSeed = false; particle.randomSeed = recipe.RandomSeed + (uint)ordinal; var emission = particle.emission; emission.enabled = true; } ordinal++;
                    CloneMaterials(instance, temp, materialCopies);
                }
                Wire(controller, root.transform, recipe);
                return PrefabUtility.SaveAsPrefabAsset(root, temp + "/VFX_Slash_3D_Stylized.prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static void Wire(SlashEffectController controller, Transform root, S12SlashRecipe recipe)
        {
            var serialized = new SerializedObject(controller); serialized.FindProperty("timelineDuration").floatValue = (float)recipe.Timeline.Duration; var phases = serialized.FindProperty("phases"); phases.arraySize = recipe.Phases.Count;
            for (var index = 0; index < recipe.Phases.Count; index++) { var phase = recipe.Phases[index]; var target = phases.GetArrayElementAtIndex(index); target.FindPropertyRelative("phaseId").stringValue = phase.Id; target.FindPropertyRelative("root").objectReferenceValue = root.Find(StableName(phase.Id)).gameObject; target.FindPropertyRelative("startTime").floatValue = (float)phase.StartTime; target.FindPropertyRelative("duration").floatValue = (float)phase.Duration; }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CloneMaterials(GameObject instance, string folder, Dictionary<string, Material> copies)
        {
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials; for (var item = 0; item < materials.Length; item++) { if (materials[item] == null) continue; var sourcePath = AssetDatabase.GetAssetPath(materials[item]); Material copy; if (!copies.TryGetValue(sourcePath, out copy)) { copy = new Material(materials[item]) { name = "S12_" + Path.GetFileNameWithoutExtension(sourcePath) }; AssetDatabase.CreateAsset(copy, folder + "/" + copy.name + ".mat"); copies.Add(sourcePath, copy); } materials[item] = copy; } renderer.sharedMaterials = materials;
            }
        }

        private static void ValidatePrefab(GameObject prefab, S12SlashRecipe recipe, ValidationReport report)
        {
            if (prefab == null) { report.Add("E1252", ValidationSeverity.Error, "/build", "Temporary Slash prefab was not saved."); return; }
            var controller = prefab.GetComponent<SlashEffectController>(); if (controller == null || controller.Phases.Length != 5) report.Add("E1252", ValidationSeverity.Error, "/build", "Generated Slash prefab lacks five serialized Runtime phase bindings.");
            foreach (var phase in recipe.Phases) { var root = prefab.transform.Find(StableName(phase.Id)); if (root == null || root.childCount != 1) report.Add("E1252", ValidationSeverity.Error, "/phases/" + phase.Id, "Generated Slash phase hierarchy is not auditable."); }
            foreach (var renderer in prefab.GetComponentsInChildren<Renderer>(true)) if (renderer.sharedMaterials.Any(material => material == null)) report.Add("E1252", ValidationSeverity.Error, "/build", "Generated Slash renderer has a missing material.");
        }

        private void Commit(S12SlashRecipe recipe, S12SlashTemplateCatalog catalog, VfxBuildPlan plan, string temp, string byteBackup, GameObject tempPrefab)
        {
            var outputAbsolute = Absolute(OutputFolderPath); var backupAbsolute = byteBackup; var hadOutput = Directory.Exists(outputAbsolute); if (hadOutput) CopyDirectory(outputAbsolute, backupAbsolute); EnsureFolder(OutputFolderPath); var priorRulesManifest = VfxProductionRules.CaptureManifest(recipe.Id);
            var finalMaterials = new Dictionary<string, Material>(StringComparer.Ordinal);
            try
            {
                foreach (var renderer in tempPrefab.GetComponentsInChildren<Renderer>(true))
                {
                    var materials = renderer.sharedMaterials; for (var index = 0; index < materials.Length; index++) { var source = materials[index]; var sourcePath = AssetDatabase.GetAssetPath(source); Material target; if (!finalMaterials.TryGetValue(sourcePath, out target)) { var destination = OutputFolderPath + "/" + Path.GetFileName(sourcePath); target = AssetDatabase.LoadAssetAtPath<Material>(destination); if (target == null) { if (!AssetDatabase.CopyAsset(sourcePath, destination)) throw new InvalidOperationException("Could not create Slash material."); target = AssetDatabase.LoadAssetAtPath<Material>(destination); } else EditorUtility.CopySerialized(source, target); finalMaterials.Add(sourcePath, target); } materials[index] = target; } renderer.sharedMaterials = materials;
                }
                var finalPrefab = PrefabUtility.SaveAsPrefabAsset(tempPrefab, OutputPrefabPath); if (finalPrefab == null) throw new InvalidOperationException("Could not save managed Slash prefab."); if (hook != null) hook.AfterPrefabAndMaterialsSaved(OutputFolderPath);
                var finalPaths = finalPrefab.GetComponentsInChildren<Renderer>(true).SelectMany(renderer => renderer.sharedMaterials).Where(material => material != null).Select(AssetDatabase.GetAssetPath).Distinct(StringComparer.Ordinal).OrderBy(path => path, StringComparer.Ordinal).ToList();
                var manifest = new S12SlashBuildManifest { RecipeId = recipe.Id, RecipeRevision = recipe.Revision, RecipeHash = plan.RecipeHash, BuildHash = plan.BuildHash, CompilerVersion = CompilerVersion, UnityVersion = Application.unityVersion, OutputPrefabPath = OutputPrefabPath, Templates = Templates(recipe, catalog), OutputMaterialPaths = finalPaths }; WriteManifest(manifest); AssetDatabase.SaveAssets(); var compliance = VfxProductionRules.EnforceAndWriteManifest(recipe.Id, "slash", 2, recipe.Revision, plan.RecipeHash, plan.BuildHash, CompilerVersion, OutputPrefabPath, OutputFolderPath, recipe.Timeline.Duration); plan.Report.AddRange(compliance.Report); if (compliance.Report.HasErrors) throw new InvalidOperationException("Production rules rejected the Slash Runtime Entry.");
            }
            catch
            {
                var pending = Absolute(ManifestPath) + ".pending"; if (File.Exists(pending)) File.Delete(pending); RestoreDirectory(outputAbsolute, backupAbsolute, hadOutput); VfxProductionRules.RestoreManifest(recipe.Id, priorRulesManifest); AssetDatabase.Refresh(); throw;
            }
        }

        private List<VfxBuildTemplate> Templates(S12SlashRecipe recipe, S12SlashTemplateCatalog catalog)
        {
            return recipe.Phases.SelectMany(phase => phase.Modules).Select(module => module.TemplateId).Distinct().OrderBy(id => id, StringComparer.Ordinal).Select(id => { var template = catalog.ByTemplateId[id]; return new VfxBuildTemplate { TemplateId = template.TemplateId, TemplateVersion = template.TemplateVersion, AssetGuid = template.AssetGuid, AssetPath = template.AssetPath, DependencyHash = dependencyHashes.GetDependencyHash(template.AssetPath) }; }).ToList();
        }
        private string Hash(string recipeHash, S12SlashRecipe recipe, S12SlashTemplateCatalog catalog)
        {
            var input = new StringBuilder(recipeHash).Append('|').Append(recipe.Revision).Append('|').Append(CompilerVersion).Append('|').Append(Application.unityVersion); foreach (var template in Templates(recipe, catalog)) input.Append('|').Append(template.TemplateId).Append('|').Append(template.TemplateVersion).Append('|').Append(template.AssetGuid).Append('|').Append(template.DependencyHash); using (var sha = SHA256.Create()) return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(input.ToString())).Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));
        }
        private static void WriteManifest(S12SlashBuildManifest manifest) { var absolute = Absolute(ManifestPath); var pending = absolute + ".pending"; try { File.WriteAllText(pending, JsonConvert.SerializeObject(manifest, Formatting.Indented, new JsonSerializerSettings { ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver() }), new UTF8Encoding(false)); if (File.Exists(absolute)) File.Replace(pending, absolute, null); else File.Move(pending, absolute); AssetDatabase.ImportAsset(ManifestPath, ImportAssetOptions.ForceUpdate); } finally { if (File.Exists(pending)) File.Delete(pending); } }
        private static S12SlashBuildManifest LoadManifest() { var path = Absolute(ManifestPath); try { return File.Exists(path) ? JsonConvert.DeserializeObject<S12SlashBuildManifest>(File.ReadAllText(path)) : null; } catch { return null; } }
        private static string StableName(string value) { return string.IsNullOrEmpty(value) ? "Invalid" : char.ToUpperInvariant(value[0]) + value.Substring(1); }
        private static string First(ValidationReport report) { var error = report.Entries.First(entry => entry.Severity == ValidationSeverity.Error); return error.Code + " " + error.Path + " " + error.Message; }
        private static string Absolute(string assetPath) { return Path.Combine(Application.dataPath, assetPath.Substring("Assets/".Length)); }
        private static string CreateTempFolder() { EnsureFolder(GeneratedRoot); var name = "s12btmp_" + Guid.NewGuid().ToString("N").Substring(0, 8); var guid = AssetDatabase.CreateFolder(GeneratedRoot, name); var path = AssetDatabase.GUIDToAssetPath(guid); if (string.IsNullOrEmpty(path) || !AssetDatabase.IsValidFolder(path) || Path.GetDirectoryName(path).Replace('\\', '/') != GeneratedRoot) throw new InvalidOperationException("Could not create safe direct Slash temp folder."); return path; }
        private static void EnsureFolder(string path) { if (AssetDatabase.IsValidFolder(path)) return; var parent = Path.GetDirectoryName(path).Replace('\\', '/'); EnsureFolder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(path)); }
        private static bool IsEmpty(string path) { var full = Absolute(path); return Directory.Exists(full) && Directory.GetFileSystemEntries(full).Length == 0; }
        private static void CopyDirectory(string source, string destination) { Directory.CreateDirectory(destination); foreach (var directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories)) Directory.CreateDirectory(Path.Combine(destination, directory.Substring(source.Length).TrimStart('\\', '/'))); foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories)) { var target = Path.Combine(destination, file.Substring(source.Length).TrimStart('\\', '/')); Directory.CreateDirectory(Path.GetDirectoryName(target)); File.Copy(file, target, true); } }
        private static void RestoreDirectory(string output, string backup, bool hadOutput) { if (Directory.Exists(output)) Directory.Delete(output, true); if (hadOutput) CopyDirectory(backup, output); else { var meta = output + ".meta"; if (File.Exists(meta)) File.Delete(meta); } }
    }

    /// <summary>Explicit compiler dispatcher: v1 remains VfxCompiler; v2 is accepted only by S12SlashCompiler.</summary>
    public sealed class S12CompilerDispatcher
    {
        public VfxBuildPlan Validate(string recipeJson) { return DryRun(recipeJson); }
        public VfxBuildPlan DryRun(string recipeJson) { var dispatch = S12RecipeDispatcher.Parse(recipeJson); if (dispatch.RecipeVersion == 1) return new VfxCompiler().DryRun(recipeJson); return new S12SlashCompiler().DryRun(recipeJson); }
        public VfxBuildResult Build(string recipeJson) { var dispatch = S12RecipeDispatcher.Parse(recipeJson); if (dispatch.RecipeVersion == 1) return new VfxCompiler().Build(recipeJson); return new S12SlashCompiler().Build(recipeJson); }
    }
}

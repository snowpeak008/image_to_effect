using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using VFXComposer.Editor.Domain;

namespace VFXComposer.Editor.Rules
{
    public sealed class VfxOutputAuditResult
    {
        public ValidationReport Report = new ValidationReport();
        public VfxOutputCostRecord Cost = new VfxOutputCostRecord();
        public List<VfxOwnedOutputRecord> OwnedOutputs = new List<VfxOwnedOutputRecord>();
        public List<VfxDependencyRecord> Dependencies = new List<VfxDependencyRecord>();
    }

    public static class VfxOutputAuditor
    {
        public static VfxOutputAuditResult Audit(string effectId, string archetype, string runtimePrefabPath, string outputFolder)
        {
            var result = new VfxOutputAuditResult();
            VfxRulesEnforcement enforcement;
            try { enforcement = VfxProjectRules.EnforcementFor(effectId); }
            catch (Exception exception) { result.Report.Add("E8000", ValidationSeverity.Error, "/rules", exception.Message); return result; }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(runtimePrefabPath);
            if (prefab == null) { result.Report.Add("E8001", ValidationSeverity.Error, "/runtimeEntry/path", "Runtime Entry prefab is missing.", new JValue(runtimePrefabPath)); return result; }
            if (!IsWithin(runtimePrefabPath, outputFolder)) result.Report.Add("E8002", ValidationSeverity.Error, "/runtimeEntry/path", "Runtime Entry must be owned by its effect output folder.", new JValue(runtimePrefabPath), outputFolder + "/");
            AddPolicy(result.Report, enforcement, "R8014", "/runtimeEntry/path", "Runtime Prefab name must be VFX_<effectId>.", string.Equals(Path.GetFileNameWithoutExtension(runtimePrefabPath), "VFX_" + effectId, StringComparison.Ordinal) ? 0 : 1, 0);

            var transforms = prefab.GetComponentsInChildren<Transform>(true);
            var renderers = prefab.GetComponentsInChildren<Renderer>(true);
            var particles = prefab.GetComponentsInChildren<ParticleSystem>(true);
            var trails = prefab.GetComponentsInChildren<TrailRenderer>(true);
            var materials = renderers.SelectMany(value => value.sharedMaterials).Where(value => value != null).Distinct().ToArray();
            var textures = AssetDatabase.GetDependencies(runtimePrefabPath, true).Select(path => AssetDatabase.LoadAssetAtPath<Texture>(path)).Where(value => value != null).Distinct().ToArray();
            result.Cost.GameObjects = transforms.Length;
            result.Cost.MaxDepth = transforms.Max(value => Depth(value, prefab.transform));
            result.Cost.Renderers = renderers.Length;
            result.Cost.ParticleSystems = particles.Length;
            result.Cost.Particles = particles.Sum(value => value.main.maxParticles);
            result.Cost.Trails = trails.Length;
            result.Cost.Materials = materials.Length;
            result.Cost.LocalTextureBytes = textures.Where(value => IsWithin(AssetDatabase.GetAssetPath(value), outputFolder)).Sum(value => Profiler.GetRuntimeMemorySizeLong(value));
            result.Cost.DependencyResidentTextureBytes = textures.Sum(value => Profiler.GetRuntimeMemorySizeLong(value));

            var entries = prefab.GetComponents<MonoBehaviour>().Where(value => value is IVfxRuntimeEntry).ToArray();
            AddPolicy(result.Report, enforcement, "R8020", "/runtimeEntry", "Runtime Prefab root must contain exactly one IVfxRuntimeEntry controller.", entries.Length == 1 ? 0 : 1, 0);

            VfxStructureBudget budget;
            try { budget = VfxProjectRules.BudgetFor(archetype); }
            catch (Exception exception) { result.Report.Add("E8019", ValidationSeverity.Error, "/archetype", exception.Message, new JValue(archetype)); return result; }
            AddPolicy(result.Report, enforcement, "R8003", "/structure/gameObjects", "Runtime Entry exceeds the configured GameObject budget.", transforms.Length, budget.MaxGameObjects);
            AddPolicy(result.Report, enforcement, "R8004", "/structure/maxDepth", "Runtime Entry exceeds the configured hierarchy depth.", result.Cost.MaxDepth, budget.MaxDepth);

            foreach (var renderer in renderers)
            {
                if (renderer.sharedMaterials.Length == 0 || renderer.sharedMaterials.Any(value => value == null)) result.Report.Add("E8005", ValidationSeverity.Error, TransformPath(renderer.transform, prefab.transform) + "/materials", "Renderer has a missing Material.");
                foreach (var material in renderer.sharedMaterials.Where(value => value != null)) if (material.shader == null) result.Report.Add("E8006", ValidationSeverity.Error, TransformPath(renderer.transform, prefab.transform) + "/shader", "Renderer has a missing Shader.", new JValue(material.name));
            }

            var names = transforms.GroupBy(value => value.name, StringComparer.Ordinal).Where(group => group.Count() > 1).Select(group => group.Key).OrderBy(value => value, StringComparer.Ordinal).ToArray();
            foreach (var name in names) AddPolicy(result.Report, enforcement, "R8007", "/structure/names", "GameObject names must be unique inside a Runtime Entry: " + name, 1, 0);
            foreach (var transform in transforms)
                foreach (var token in VfxProjectRules.Load().ForbiddenProductionNameTokens.Where(token => transform.name.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0))
                    AddPolicy(result.Report, enforcement, "R8017", TransformPath(transform, prefab.transform), "Production GameObject name contains a forbidden development token: " + token, 1, 0);
            foreach (var transform in transforms)
            {
                var gameObject = transform.gameObject;
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(gameObject) > 0) result.Report.Add("E8008", ValidationSeverity.Error, TransformPath(transform, prefab.transform), "Runtime Entry contains a missing MonoBehaviour script.");
                foreach (var component in gameObject.GetComponents<Component>().Where(value => value != null))
                {
                    var type = component.GetType(); var assembly = type.Assembly.GetName().Name;
                    if (assembly.IndexOf("Editor", StringComparison.OrdinalIgnoreCase) >= 0 || type.Namespace != null && type.Namespace.StartsWith("UnityEditor", StringComparison.Ordinal)) result.Report.Add("E8009", ValidationSeverity.Error, TransformPath(transform, prefab.transform), "Editor component is forbidden in a Runtime Entry: " + type.FullName);
                    if (VfxProjectRules.Load().ForbiddenRuntimeComponentTypeNames.Contains(type.Name)) result.Report.Add("E8010", ValidationSeverity.Error, TransformPath(transform, prefab.transform), "Preview/evidence component is forbidden in a Runtime Entry: " + type.Name);
                }
            }

            result.OwnedOutputs = BuildOwnedOutputs(outputFolder);
            foreach (var output in result.OwnedOutputs)
                foreach (var token in VfxProjectRules.Load().ForbiddenProductionNameTokens.Where(token => Path.GetFileName(output.Path).IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0))
                    AddPolicy(result.Report, enforcement, "R8018", "/ownedOutputs", "Production asset name contains a forbidden development token: " + Path.GetFileName(output.Path), 1, 0);
            var owned = new HashSet<string>(result.OwnedOutputs.Select(value => value.Path), StringComparer.Ordinal);
            result.Dependencies = BuildDependencies(runtimePrefabPath, owned, result.Report);
            var reachable = new HashSet<string>(AssetDatabase.GetDependencies(runtimePrefabPath, true), StringComparer.Ordinal);
            foreach (var stale in result.OwnedOutputs.Where(value => !string.Equals(value.Path, runtimePrefabPath, StringComparison.Ordinal) && !reachable.Contains(value.Path))) AddPolicy(result.Report, enforcement, "R8015", "/ownedOutputs", "Owned output is not reachable from the Runtime Entry and must be removed through a rollback-safe stale cleanup: " + stale.Path, 1, 0);
            var localMaterials = result.OwnedOutputs.Count(value => value.AssetType == "Material");
            var localTextures = result.OwnedOutputs.Count(value => value.AssetType == "Texture2D" || value.AssetType == "Texture");
            var localShaders = result.OwnedOutputs.Count(value => value.AssetType == "Shader");
            AddPolicy(result.Report, enforcement, "R8011", "/ownedOutputs/materials", "Effect exceeds the recommended local Material count.", localMaterials, budget.MaxLocalMaterials);
            AddPolicy(result.Report, enforcement, "R8012", "/ownedOutputs/textures", "Effect exceeds the recommended local Texture count.", localTextures, budget.MaxLocalTextures);
            AddPolicy(result.Report, enforcement, "R8016", "/ownedOutputs/shaders", "Local Shader assets are forbidden; move the Shader family to Assets/VFX/Shared/Shaders.", localShaders, 0);
            return result;
        }

        private static List<VfxOwnedOutputRecord> BuildOwnedOutputs(string outputFolder)
        {
            var absolute = AssetAbsolute(outputFolder);
            if (!Directory.Exists(absolute)) return new List<VfxOwnedOutputRecord>();
            return Directory.GetFiles(absolute, "*", SearchOption.AllDirectories)
                .Where(path => !path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase) && !string.Equals(Path.GetFileName(path), "BuildManifest.json", StringComparison.Ordinal))
                .Select(path => ToAssetPath(path)).Where(path => !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path)))
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(path => new VfxOwnedOutputRecord { Path = path, Guid = AssetDatabase.AssetPathToGUID(path), AssetType = AssetType(path), Sha256 = Sha256(AssetAbsolute(path)) }).ToList();
        }

        private static List<VfxDependencyRecord> BuildDependencies(string runtimePath, HashSet<string> owned, ValidationReport report)
        {
            var allowed = VfxProjectRules.Load().AllowedDependencyRoots;
            var records = new List<VfxDependencyRecord>();
            foreach (var path in AssetDatabase.GetDependencies(runtimePath, true).Where(path => !owned.Contains(path)).OrderBy(path => path, StringComparer.Ordinal))
            {
                if (path.StartsWith("Assets/", StringComparison.Ordinal) || path.StartsWith("Packages/", StringComparison.Ordinal))
                {
                    if (!allowed.Any(root => path.StartsWith(root, StringComparison.Ordinal))) report.Add("E8013", ValidationSeverity.Error, "/dependencies", "Runtime Entry has an out-of-policy dependency.", new JValue(path), string.Join(", ", allowed.ToArray()));
                    records.Add(new VfxDependencyRecord { Path = path, Guid = AssetDatabase.AssetPathToGUID(path), AssetType = AssetType(path), Version = null, DependencyHash = AssetDatabase.GetAssetDependencyHash(path).ToString() });
                }
            }
            return records;
        }

        private static void AddPolicy(ValidationReport report, VfxRulesEnforcement enforcement, string code, string path, string message, int actual, int maximum)
        {
            if (actual <= maximum) return;
            report.Add(code, enforcement == VfxRulesEnforcement.Strict ? ValidationSeverity.Error : ValidationSeverity.Warning, path, message, new JValue(actual), "<= " + maximum);
        }

        private static int Depth(Transform value, Transform root) { var depth = 0; while (value != null && value != root) { depth++; value = value.parent; } return depth; }
        private static string TransformPath(Transform value, Transform root) { var names = new List<string>(); while (value != null) { names.Add(value.name); if (value == root) break; value = value.parent; } names.Reverse(); return "/runtimeEntry/" + string.Join("/", names.ToArray()); }
        private static bool IsWithin(string path, string folder) { return path != null && (string.Equals(path, folder, StringComparison.Ordinal) || path.StartsWith(folder.TrimEnd('/') + "/", StringComparison.Ordinal)); }
        private static string AssetAbsolute(string assetPath) { return Path.GetFullPath(Path.Combine(Directory.GetParent(Application.dataPath).FullName, assetPath.Replace('/', Path.DirectorySeparatorChar))); }
        private static string ToAssetPath(string absolute) { var root = Directory.GetParent(Application.dataPath).FullName.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar; return Path.GetFullPath(absolute).Substring(root.Length).Replace('\\', '/'); }
        private static string AssetType(string path) { var type = AssetDatabase.GetMainAssetTypeAtPath(path); return type == null ? "Unknown" : type.Name; }
        private static string Sha256(string path) { using (var stream = File.OpenRead(path)) using (var sha = SHA256.Create()) return string.Concat(sha.ComputeHash(stream).Select(value => value.ToString("x2"))); }
    }
}

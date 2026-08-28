using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using VFXComposer.Editor.Build;
using VFXComposer.Editor.Domain;
using VFXComposer.Editor.Rules;
using VFXComposer.Editor.Validation;

namespace VFXComposer.Editor.Area2D
{
    public static class Area2DSharedLibrary
    {
        public const string FamilyRoot = "Assets/VFX/Shared/Fire";
        public const string TextureRoot = FamilyRoot + "/Textures";
        public const string MaterialRoot = FamilyRoot + "/Materials";
        public const string MeshRoot = FamilyRoot + "/Meshes";
        public const string ShaderPath = "Assets/VFX/Shared/Shaders/Area2DFireUnlit.shader";
        public const string ShaderName = "Universal Render Pipeline/VFXComposer Area 2D Fire Unlit";
        public const string MaskAtlas = TextureRoot + "/T_Fire_MaskAtlas_A_v1.png";
        public const string RingMesh = MeshRoot + "/M_Fire_ClosedBand_v1.asset";
        public const string DiscMesh = MeshRoot + "/M_Fire_VortexDisc_v1.asset";
        public const string BodyMaterial = MaterialRoot + "/MAT_Fire_AreaBody_Alpha_v1.mat";
        public const string HotMaterial = MaterialRoot + "/MAT_Fire_AreaHot_Additive_v1.mat";
        public static readonly string[] Dependencies = { ShaderPath, MaskAtlas, RingMesh, DiscMesh, BodyMaterial, HotMaterial };

        public static readonly Vector4 FlameA = new Vector4(0f, .5f, .5f, .5f);
        public static readonly Vector4 FlameB = new Vector4(.5f, .5f, .5f, .5f);
        public static readonly Vector4 Ember = new Vector4(0f, 0f, .5f, .5f);
        public static readonly Vector4 Breakup = new Vector4(.5f, 0f, .5f, .5f);

        public static void Ensure()
        {
            EnsureFolder(TextureRoot); EnsureFolder(MaterialRoot); EnsureFolder(MeshRoot);
            ConfigureMaskAtlas();
            CreateOrUpdateRingMesh();
            CreateOrUpdateDiscMesh();
            CreateMaterial(BodyMaterial, BlendMode.OneMinusSrcAlpha, 3000, .64f);
            CreateMaterial(HotMaterial, BlendMode.One, 3010, .82f);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void ConfigureMaskAtlas()
        {
            var absolute = Absolute(MaskAtlas);
            if (!File.Exists(absolute)) throw new InvalidOperationException("Missing compact Fire mask atlas: " + MaskAtlas);
            if (new FileInfo(absolute).Length > 64 * 1024) throw new InvalidOperationException("Fire mask atlas exceeds the 64 KiB source budget.");
            AssetDatabase.ImportAsset(MaskAtlas, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(MaskAtlas) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Fire mask atlas must import as Texture2D.");
            importer.textureType = TextureImporterType.Default;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.sRGBTexture = false;
            importer.alphaIsTransparency = false;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 256;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.crunchedCompression = false;
            importer.isReadable = false;
            var standalone = importer.GetPlatformTextureSettings("Standalone");
            standalone.overridden = true; standalone.maxTextureSize = 256; standalone.format = TextureImporterFormat.BC4; standalone.compressionQuality = 100; importer.SetPlatformTextureSettings(standalone);
            var android = importer.GetPlatformTextureSettings("Android");
            android.overridden = true; android.maxTextureSize = 256; android.format = TextureImporterFormat.ASTC_8x8; android.compressionQuality = 100; importer.SetPlatformTextureSettings(android);
            importer.SaveAndReimport();
        }

        private static void CreateMaterial(string path, BlendMode destinationBlend, int renderQueue, float intensity)
        {
            var shader = Shader.Find(ShaderName);
            if (shader == null) throw new InvalidOperationException("Area 2D Fire shader is required at " + ShaderPath + ".");
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) };
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                var clean = new Material(shader) { name = material.name };
                EditorUtility.CopySerialized(clean, material);
                UnityEngine.Object.DestroyImmediate(clean);
            }
            material.shader = shader;
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(MaskAtlas));
            material.SetVector("_UVRect", FlameA);
            var additive = destinationBlend == BlendMode.One;
            material.SetColor("_ColorLow", additive ? new Color(.22f, .004f, .001f, .76f) : new Color(.055f, .001f, 0f, .68f));
            material.SetColor("_ColorMid", additive ? new Color(1f, .085f, .003f, 1f) : new Color(.64f, .018f, .001f, .94f));
            material.SetColor("_ColorHigh", additive ? new Color(1f, .62f, .045f, 1f) : new Color(1f, .19f, .005f, 1f));
            material.SetFloat("_FlowSpeed", 1f);
            material.SetFloat("_Intensity", intensity);
            material.SetFloat("_GlobalAlpha", 1f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)destinationBlend);
            material.renderQueue = renderQueue;
            material.SetOverrideTag("RenderType", "Transparent");
            EditorUtility.SetDirty(material);
        }

        private static void CreateOrUpdateRingMesh()
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(RingMesh);
            if (mesh == null) { mesh = new Mesh { name = "M_Fire_ClosedBand_v1" }; AssetDatabase.CreateAsset(mesh, RingMesh); }
            const int samples = 96;
            var vertices = new List<Vector3>((samples + 1) * 2);
            var uv = new List<Vector2>((samples + 1) * 2);
            var triangles = new List<int>(samples * 6);
            for (var index = 0; index <= samples; index++)
            {
                var wrapped = index % samples;
                var t = index / (float)samples;
                var angle = t * Mathf.PI * 2f;
                var inner = .68f + .025f * Mathf.Sin(wrapped * Mathf.PI * 2f * 5f / samples) + .012f * Mathf.Sin(wrapped * Mathf.PI * 2f * 13f / samples);
                var outer = 1f + .055f * Mathf.Sin(wrapped * Mathf.PI * 2f * 7f / samples + .4f) + .022f * Mathf.Sin(wrapped * Mathf.PI * 2f * 17f / samples);
                var direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                vertices.Add(direction * inner); uv.Add(new Vector2(t, 0f));
                vertices.Add(direction * outer); uv.Add(new Vector2(t, 1f));
                if (index == samples) { vertices[vertices.Count - 2] = vertices[0]; vertices[vertices.Count - 1] = vertices[1]; }
            }
            for (var index = 0; index < samples; index++)
            {
                var first = index * 2;
                triangles.Add(first); triangles.Add(first + 1); triangles.Add(first + 3);
                triangles.Add(first); triangles.Add(first + 3); triangles.Add(first + 2);
            }
            mesh.Clear(); mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds(); EditorUtility.SetDirty(mesh);
        }

        private static void CreateOrUpdateDiscMesh()
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(DiscMesh);
            if (mesh == null) { mesh = new Mesh { name = "M_Fire_VortexDisc_v1" }; AssetDatabase.CreateAsset(mesh, DiscMesh); }
            mesh.Clear();
            mesh.vertices = new[] { new Vector3(-1f, -1f, 0f), new Vector3(-1f, 1f, 0f), new Vector3(1f, 1f, 0f), new Vector3(1f, -1f, 0f) };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); EditorUtility.SetDirty(mesh);
        }

        internal static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private static string Absolute(string path) { return Path.GetFullPath(Path.Combine(Directory.GetParent(Application.dataPath).FullName, path.Replace('/', Path.DirectorySeparatorChar))); }
    }

    public sealed class Area2DCompiler
    {
        public const string CompilerVersion = "area2d-10";
        public const string GeneratedRoot = "Assets/VFX/Generated";
        public const string DefaultRecipePath = "Assets/VFX/Recipes/Area/inferno_vortex_area_2d.default.json";

        public VfxBuildPlan DryRun(string recipeJson)
        {
            var parsed = Area2DRecipeParser.Parse(recipeJson); var plan = new VfxBuildPlan(); plan.Report.AddRange(parsed.Report);
            if (plan.Report.HasErrors) { plan.Items.Add(new VfxBuildItem { State = VfxBuildItemState.Blocked, AssetPath = "/", Reason = "Area Recipe validation failed." }); return plan; }
            plan.RecipeRevision = parsed.Value.Revision; plan.RecipeHash = RecipeCanonicalizer.ComputeSha256(recipeJson); plan.BuildHash = BuildHash(plan.RecipeHash);
            var prefabPath = PrefabPath(parsed.Value.Id); var exists = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null; var manifest = LoadRulesManifest(parsed.Value.Id);
            var dependenciesExist = Area2DSharedLibrary.Dependencies.All(path => AssetDatabase.LoadMainAssetAtPath(path) != null);
            var unchanged = exists && dependenciesExist && manifest != null && string.Equals(manifest.BuildHash, plan.BuildHash, StringComparison.Ordinal);
            plan.Items.Add(new VfxBuildItem { State = unchanged ? VfxBuildItemState.Unchanged : exists ? VfxBuildItemState.Update : VfxBuildItemState.Create, AssetPath = prefabPath, Reason = unchanged ? "Recipe and Fire shared dependency hashes are unchanged." : "Area Runtime Entry or its managed input set differs." });
            return plan;
        }

        public VfxBuildResult Build(string recipeJson)
        {
            var parsed = Area2DRecipeParser.Parse(recipeJson); var early = new VfxBuildResult { Plan = new VfxBuildPlan(), Succeeded = false };
            early.Plan.Report.AddRange(parsed.Report); if (early.Plan.Report.HasErrors) return early;
            try { Area2DSharedLibrary.Ensure(); }
            catch (Exception exception) { early.Plan.Report.Add("E1710", ValidationSeverity.Error, "/shared", "Could not prepare Area shared library: " + exception.Message); return early; }
            var plan = DryRun(recipeJson); var recipe = parsed.Value; var result = new VfxBuildResult { Plan = plan, PrefabPath = PrefabPath(recipe.Id) };
            if (plan.IsBlocked) return result;
            if (plan.Items.Single().State == VfxBuildItemState.Unchanged)
            {
                var audit = VfxProductionRules.EnforceAndWriteManifest(recipe.Id, "area", recipe.RecipeVersion, recipe.Revision, plan.RecipeHash, plan.BuildHash, CompilerVersion, result.PrefabPath, OutputFolder(recipe.Id), recipe.LoopDuration);
                plan.Report.AddRange(audit.Report); result.Succeeded = !plan.Report.HasErrors; return result;
            }
            Area2DSharedLibrary.EnsureFolder(GeneratedRoot);
            var tempName = "areatmp_" + Guid.NewGuid().ToString("N").Substring(0, 8); var guid = AssetDatabase.CreateFolder(GeneratedRoot, tempName); var temp = AssetDatabase.GUIDToAssetPath(guid);
            try
            {
                var tempPrefab = BuildTemporary(recipe, temp);
                Commit(recipe, plan, temp, tempPrefab);
                result.Succeeded = true;
            }
            catch (Exception exception) { if (!plan.Report.HasErrors) plan.Report.Add("E1711", ValidationSeverity.Error, "/build", "Area build failed: " + exception.Message); }
            finally { if (AssetDatabase.IsValidFolder(temp)) AssetDatabase.DeleteAsset(temp); AssetDatabase.SaveAssets(); AssetDatabase.Refresh(); }
            return result;
        }

        private static GameObject BuildTemporary(Area2DRecipe recipe, string temp)
        {
            var root = new GameObject("VFX_" + recipe.Id);
            try
            {
                var radius = (float)recipe.Radius;
                var ringMesh = AssetDatabase.LoadAssetAtPath<Mesh>(Area2DSharedLibrary.RingMesh);
                var discMesh = AssetDatabase.LoadAssetAtPath<Mesh>(Area2DSharedLibrary.DiscMesh);
                var body = AssetDatabase.LoadAssetAtPath<Material>(Area2DSharedLibrary.BodyMaterial);
                var hot = AssetDatabase.LoadAssetAtPath<Material>(Area2DSharedLibrary.HotMaterial);
                var outerA = MeshLayer("OuterFlameBandA", root.transform, discMesh, body, 10, radius * 1.02f, 0f);
                var outerB = MeshLayer("OuterFlameBandB", root.transform, discMesh, hot, 12, radius * .97f, 13f);
                var core = MeshLayer("MoltenCore", root.transform, discMesh, hot, 16, radius * .36f, 11f);
                var vortex = MeshLayer("InnerSixArmVortex", root.transform, discMesh, hot, 20, radius * .68f, -8f);
                var pulse = MeshLayer("TickPulse", root.transform, ringMesh, hot, 24, radius, 0f);
                var tongues = FlameTongues(root.transform, recipe, hot);
                var embers = Embers(root.transform, recipe, hot);
                var renderers = new Renderer[] { outerA, outerB, core, vortex, pulse, tongues.GetComponent<ParticleSystemRenderer>(), embers.GetComponent<ParticleSystemRenderer>() };
                var systems = new[] { tongues, embers };
                var rotating = new[] { outerA.transform, outerB.transform, vortex.transform };
                var controller = root.AddComponent<InfernoAreaVfxController>();
                var serialized = new SerializedObject(controller);
                SetObjects(serialized.FindProperty("animatedRenderers"), renderers);
                SetObjects(serialized.FindProperty("systems"), systems);
                SetObjects(serialized.FindProperty("rotatingLayers"), rotating);
                SetFloats(serialized.FindProperty("rotationSpeeds"), new[] { 13f, -21f, 32f });
                SetVectors(serialized.FindProperty("maskRects"), new[] { Area2DSharedLibrary.FlameA, Area2DSharedLibrary.FlameB, Area2DSharedLibrary.Breakup, Area2DSharedLibrary.FlameB, Area2DSharedLibrary.Breakup, Area2DSharedLibrary.Ember, Area2DSharedLibrary.Ember });
                SetFloats(serialized.FindProperty("flowSpeeds"), new[] { .72f, -.58f, .31f, 1.08f, .44f, .82f, 1.26f });
                SetFloats(serialized.FindProperty("intensities"), new[] { .85f, .66f, .76f, .92f, .86f, .48f, .92f });
                SetFloats(serialized.FindProperty("geometryModes"), new[] { 3f, 4f, 4f, 4f, 7f, 5f, 6f });
                serialized.FindProperty("pulseRenderer").objectReferenceValue = pulse;
                serialized.FindProperty("pulseTransform").objectReferenceValue = pulse.transform;
                serialized.FindProperty("establishDuration").floatValue = .6f;
                serialized.FindProperty("loopDuration").floatValue = (float)recipe.LoopDuration;
                serialized.FindProperty("tickInterval").floatValue = (float)recipe.TickInterval;
                serialized.FindProperty("stopDuration").floatValue = .35f;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                foreach (var renderer in renderers) if (renderer != null) renderer.enabled = false;
                return PrefabUtility.SaveAsPrefabAsset(root, temp + "/VFX_" + recipe.Id + ".prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static MeshRenderer MeshLayer(string name, Transform parent, Mesh mesh, Material material, int order, float scale, float rotation)
        {
            var gameObject = new GameObject(name); gameObject.transform.SetParent(parent, false); gameObject.transform.localScale = Vector3.one * scale; gameObject.transform.localRotation = Quaternion.Euler(0f, 0f, rotation);
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh; var renderer = gameObject.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material; renderer.sortingLayerName = "Default"; renderer.sortingOrder = order; return renderer;
        }

        private static ParticleSystem FlameTongues(Transform parent, Area2DRecipe recipe, Material material)
        {
            var ps = Particle("OrbitingFlameMotes", parent, material, 30, recipe.RandomSeed + 1, Mathf.Min(24, recipe.FlameCount));
            var main = ps.main; main.startLifetime = new ParticleSystem.MinMaxCurve(.44f, .82f); main.startSpeed = new ParticleSystem.MinMaxCurve(.025f, .11f); main.startSize = new ParticleSystem.MinMaxCurve(.045f, .12f); main.startRotation = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI); main.startColor = new Color(1f, .35f, .04f, .72f);
            var emission = ps.emission; emission.rateOverTime = 6f;
            var shape = ps.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Circle; shape.radius = (float)recipe.Radius * .73f; shape.radiusThickness = .16f; shape.arc = 360f; shape.randomDirectionAmount = .18f;
            var velocity = ps.velocityOverLifetime; velocity.enabled = true; velocity.space = ParticleSystemSimulationSpace.Local; velocity.orbitalZ = new ParticleSystem.MinMaxCurve(.58f); velocity.radial = new ParticleSystem.MinMaxCurve(-.03f, .055f);
            var rotation = ps.rotationOverLifetime; rotation.enabled = true; rotation.z = new ParticleSystem.MinMaxCurve(-.65f, .65f);
            var size = ps.sizeOverLifetime; size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, .18f), new Keyframe(.22f, 1f), new Keyframe(.72f, .84f), new Keyframe(1f, .05f)));
            SetColor(ps, new[] { new GradientColorKey(new Color(1f, .72f, .1f), 0f), new GradientColorKey(new Color(1f, .18f, .01f), .58f), new GradientColorKey(new Color(.32f, .015f, .002f), 1f) }, new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(.74f, .18f), new GradientAlphaKey(.58f, .68f), new GradientAlphaKey(0f, 1f) });
            return ps;
        }

        private static ParticleSystem Embers(Transform parent, Area2DRecipe recipe, Material material)
        {
            var max = recipe.TargetProfile == "mobile_medium" ? 18 : 24;
            var ps = Particle("DirectionalEmbers", parent, material, 34, recipe.RandomSeed + 2, max);
            var main = ps.main; main.startLifetime = new ParticleSystem.MinMaxCurve(.32f, .72f); main.startSpeed = new ParticleSystem.MinMaxCurve(.34f, .92f); main.startSize = new ParticleSystem.MinMaxCurve(.022f, .065f); main.startRotation = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI); main.startColor = new Color(1f, .55f, .08f, .9f);
            var emission = ps.emission; emission.rateOverTime = recipe.TargetProfile == "mobile_medium" ? 8f : 13f;
            var shape = ps.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Circle; shape.radius = (float)recipe.Radius * .86f; shape.radiusThickness = .14f; shape.arc = 360f; shape.randomDirectionAmount = .85f;
            var velocity = ps.velocityOverLifetime; velocity.enabled = true; velocity.space = ParticleSystemSimulationSpace.Local; velocity.orbitalZ = new ParticleSystem.MinMaxCurve(.28f); velocity.radial = new ParticleSystem.MinMaxCurve(.12f, .32f);
            var size = ps.sizeOverLifetime; size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, .25f), new Keyframe(.18f, 1f), new Keyframe(1f, 0f)));
            SetColor(ps, new[] { new GradientColorKey(new Color(1f, .84f, .18f), 0f), new GradientColorKey(new Color(1f, .16f, .01f), 1f) }, new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(.9f, .12f), new GradientAlphaKey(.55f, .68f), new GradientAlphaKey(0f, 1f) });
            return ps;
        }

        private static ParticleSystem Particle(string name, Transform parent, Material material, int order, uint seed, int maxParticles)
        {
            var gameObject = new GameObject(name); gameObject.transform.SetParent(parent, false); var ps = gameObject.AddComponent<ParticleSystem>(); ps.useAutoRandomSeed = false; ps.randomSeed = seed;
            var main = ps.main; main.loop = true; main.duration = 1.6f; main.playOnAwake = false; main.simulationSpace = ParticleSystemSimulationSpace.Local; main.maxParticles = maxParticles; main.startSpeed = 0f;
            var emission = ps.emission; emission.rateOverTime = 0f; var shape = ps.shape; shape.enabled = false;
            var renderer = ps.GetComponent<ParticleSystemRenderer>(); renderer.renderMode = ParticleSystemRenderMode.Billboard; renderer.sharedMaterial = material; renderer.sortingLayerName = "Default"; renderer.sortingOrder = order;
            return ps;
        }

        private static void SetColor(ParticleSystem ps, GradientColorKey[] colors, GradientAlphaKey[] alpha)
        {
            var gradient = new Gradient(); gradient.SetKeys(colors, alpha); var module = ps.colorOverLifetime; module.enabled = true; module.color = new ParticleSystem.MinMaxGradient(gradient);
        }

        private static void SetObjects(SerializedProperty property, UnityEngine.Object[] values) { property.arraySize = values.Length; for (var index = 0; index < values.Length; index++) property.GetArrayElementAtIndex(index).objectReferenceValue = values[index]; }
        private static void SetFloats(SerializedProperty property, float[] values) { property.arraySize = values.Length; for (var index = 0; index < values.Length; index++) property.GetArrayElementAtIndex(index).floatValue = values[index]; }
        private static void SetVectors(SerializedProperty property, Vector4[] values) { property.arraySize = values.Length; for (var index = 0; index < values.Length; index++) property.GetArrayElementAtIndex(index).vector4Value = values[index]; }

        private static void Commit(Area2DRecipe recipe, VfxBuildPlan plan, string temp, GameObject tempPrefab)
        {
            var folder = OutputFolder(recipe.Id); var existed = AssetDatabase.IsValidFolder(folder); Area2DSharedLibrary.EnsureFolder(folder); var prefabPath = PrefabPath(recipe.Id); var prior = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath); var backup = temp + "/prior.prefab"; if (prior != null && !AssetDatabase.CopyAsset(prefabPath, backup)) throw new InvalidOperationException("Could not snapshot existing Area Prefab."); var priorManifest = VfxProductionRules.CaptureManifest(recipe.Id);
            try
            {
                if (PrefabUtility.SaveAsPrefabAsset(tempPrefab, prefabPath) == null) throw new InvalidOperationException("Could not save Area Runtime Prefab.");
                AssetDatabase.SaveAssets();
                var audit = VfxProductionRules.EnforceAndWriteManifest(recipe.Id, "area", recipe.RecipeVersion, recipe.Revision, plan.RecipeHash, plan.BuildHash, CompilerVersion, prefabPath, folder, recipe.LoopDuration); plan.Report.AddRange(audit.Report); if (audit.Report.HasErrors) throw new InvalidOperationException("Production rules rejected Area output.");
            }
            catch
            {
                if (prior != null) { var saved = AssetDatabase.LoadAssetAtPath<GameObject>(backup); if (saved != null) PrefabUtility.SaveAsPrefabAsset(saved, prefabPath); }
                else if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null) AssetDatabase.DeleteAsset(prefabPath);
                VfxProductionRules.RestoreManifest(recipe.Id, priorManifest);
                if (!existed && AssetDatabase.IsValidFolder(folder) && Directory.GetFileSystemEntries(Absolute(folder)).Length == 0) AssetDatabase.DeleteAsset(folder);
                throw;
            }
        }

        private static VfxOutputManifest LoadRulesManifest(string effectId) { var path = VfxProjectRules.ManifestAbsolutePath(effectId); try { return File.Exists(path) ? JsonConvert.DeserializeObject<VfxOutputManifest>(File.ReadAllText(path)) : null; } catch { return null; } }
        private static string BuildHash(string recipeHash) { var input = new StringBuilder(recipeHash).Append('|').Append(CompilerVersion).Append('|').Append(Application.unityVersion); foreach (var path in Area2DSharedLibrary.Dependencies.OrderBy(value => value, StringComparer.Ordinal)) input.Append('|').Append(path).Append('|').Append(AssetDatabase.LoadMainAssetAtPath(path) == null ? "missing" : AssetDatabase.GetAssetDependencyHash(path).ToString()); using (var sha = SHA256.Create()) return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(input.ToString())).Select(value => value.ToString("x2", CultureInfo.InvariantCulture))); }
        public static string OutputFolder(string id) { return GeneratedRoot + "/" + VfxProjectRules.SanitizeId(id); }
        public static string PrefabPath(string id) { return OutputFolder(id) + "/VFX_" + VfxProjectRules.SanitizeId(id) + ".prefab"; }
        private static string Absolute(string path) { return Path.GetFullPath(Path.Combine(Directory.GetParent(Application.dataPath).FullName, path.Replace('/', Path.DirectorySeparatorChar))); }
    }
}

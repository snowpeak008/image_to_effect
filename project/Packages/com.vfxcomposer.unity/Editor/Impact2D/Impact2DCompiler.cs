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

namespace VFXComposer.Editor.Impact2D
{
    public static class Impact2DSharedLibrary
    {
        public const string FamilyRoot = "Assets/VFX/Shared/Frost";
        public const string TextureRoot = FamilyRoot + "/Textures";
        public const string MaterialRoot = FamilyRoot + "/Materials";
        public const string MeshRoot = FamilyRoot + "/Meshes";
        public const string ShaderPath = "Assets/VFX/Shared/Shaders/Impact2DAdditiveUnlit.shader";
        public const string ShaderName = "Universal Render Pipeline/VFXComposer Impact 2D Additive Unlit";
        public const string ProceduralShaderPath = "Assets/VFX/Shared/Shaders/Impact2DProceduralUnlit.shader";
        public const string ProceduralShaderName = "Universal Render Pipeline/VFXComposer Impact 2D Procedural Unlit";
        public const string LegacyImpactAtlas = TextureRoot + "/T_Frost_ImpactAtlas_A_v1.png";
        public const string ShardAtlas = TextureRoot + "/T_Frost_ShardAtlas_A_v1.png";
        public const string RingMesh = MeshRoot + "/M_Frost_BrokenRingSegments_v1.asset";
        public const string CoreMaterial = MaterialRoot + "/MAT_Frost_ImpactCore_Additive.mat";
        public const string ShardMaterial = MaterialRoot + "/MAT_Frost_Shard_Additive.mat";
        public const string RingMaterial = MaterialRoot + "/MAT_Frost_BrokenRing_Additive.mat";
        public const string MistMaterial = MaterialRoot + "/MAT_Frost_MistRing_Additive.mat";
        public const string MoteMaterial = MaterialRoot + "/MAT_Frost_Mote_Additive.mat";
        public static readonly string[] Dependencies = { ShaderPath, ProceduralShaderPath, ShardAtlas, RingMesh, CoreMaterial, ShardMaterial, RingMaterial, MistMaterial, MoteMaterial };

        public static void Ensure()
        {
            EnsureFolder(TextureRoot); EnsureFolder(MaterialRoot); EnsureFolder(MeshRoot);
            // Keep the authored 256px source (four clean 128px cells) for editability, but
            // import a 128px runtime copy.  The effect reuses this atlas on every shard, so the
            // smaller resident representation preserves the visual MVP while keeping the
            // dependency budget below the formal 100 KB ceiling.
            RequireTexture(ShardAtlas, 128);
            CreateRingMesh();
            CreateProceduralMaterial(CoreMaterial, 0f, new Color(.78f, .94f, 1f, 1f), BlendMode.One);
            CreateTexturedMaterial(ShardMaterial, ShardAtlas, new Vector4(0f, 0f, 1f, 1f), new Color(.84f, .97f, 1f, 1f), BlendMode.One);
            CreateProceduralMaterial(RingMaterial, 1f, new Color(.66f, .9f, 1f, 1f), BlendMode.One);
            CreateProceduralMaterial(MistMaterial, 2f, new Color(.48f, .72f, .94f, 1f), BlendMode.OneMinusSrcAlpha);
            CreateProceduralMaterial(MoteMaterial, 3f, new Color(.62f, .9f, 1f, 1f), BlendMode.One);
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        }

        private static void RequireTexture(string path, int size)
        {
            if (!File.Exists(Absolute(path))) throw new InvalidOperationException("Missing exported Frost family runtime atlas: " + path);
            ConfigureTexture(path, size);
        }

        private static void ConfigureTexture(string path, int size)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Default; importer.alphaSource = TextureImporterAlphaSource.FromInput; importer.sRGBTexture = true; importer.alphaIsTransparency = true; importer.mipmapEnabled = false; importer.wrapMode = TextureWrapMode.Clamp; importer.filterMode = FilterMode.Bilinear; importer.npotScale = TextureImporterNPOTScale.None; importer.maxTextureSize = size; importer.textureCompression = TextureImporterCompression.CompressedHQ; importer.crunchedCompression = false; importer.isReadable = false;
                importer.SaveAndReimport();
            }
        }

        private static void CreateTexturedMaterial(string path, string texturePath, Vector4 uvRect, Color tint, BlendMode destinationBlend)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader = Shader.Find(ShaderName);
                if (shader == null) throw new InvalidOperationException("Impact 2D additive shader is required at " + ShaderPath + ".");
                material = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) };
                AssetDatabase.CreateAsset(material, path);
            }
            var requiredShader = Shader.Find(ShaderName);
            if (requiredShader == null) throw new InvalidOperationException("Impact 2D additive shader is required at " + ShaderPath + ".");
            material.shader = requiredShader;
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", tint);
            if (material.HasProperty("_UVRect")) material.SetVector("_UVRect", uvRect);
            if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)destinationBlend);
            material.renderQueue = (int)RenderQueue.Transparent;
            material.SetOverrideTag("RenderType", "Transparent");
            EditorUtility.SetDirty(material);
        }

        private static void CreateProceduralMaterial(string path, float shapeMode, Color tint, BlendMode destinationBlend)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find(ProceduralShaderName);
            if (shader == null) throw new InvalidOperationException("Impact 2D procedural shader is required at " + ProceduralShaderPath + ".");
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
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", tint);
            if (material.HasProperty("_ShapeMode")) material.SetFloat("_ShapeMode", shapeMode);
            if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)destinationBlend);
            material.renderQueue = (int)RenderQueue.Transparent;
            material.SetOverrideTag("RenderType", "Transparent");
            EditorUtility.SetDirty(material);
        }

        private static void CreateRingMesh()
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(RingMesh);
            if (mesh == null)
            {
                mesh = new Mesh { name = "M_Frost_BrokenRingSegments_v1" };
                AssetDatabase.CreateAsset(mesh, RingMesh);
            }
            const int segmentCount = 16;
            const int subdivisions = 6;
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var localUv = new List<Vector2>(); var triangles = new List<int>();
            for (var segment = 0; segment < segmentCount; segment++)
            {
                var step = Mathf.PI * 2f / segmentCount;
                const float gap = 0f;
                var startJitter = Mathf.Deg2Rad * Mathf.Sin(segment * 2.17f) * .55f;
                var endBoundary = (segment + 1) % segmentCount;
                var endJitter = Mathf.Deg2Rad * Mathf.Sin(endBoundary * 2.17f) * .55f;
                var start = segment * step + gap * .5f + startJitter;
                var end = (segment + 1) * step - gap * .5f + endJitter;
                var first = vertices.Count;
                for (var point = 0; point <= subdivisions; point++)
                {
                    var t = point / (float)subdivisions;
                    var angle = Mathf.Lerp(start, end, t);
                    var globalSample = (segment * subdivisions + point) % (segmentCount * subdivisions);
                    var inner = .395f + .011f * Mathf.Sin(globalSample * 1.137f);
                    var outer = .505f + .013f * Mathf.Sin(globalSample * 1.79f) + (globalSample % 17 == 0 ? .022f : 0f);
                    var globalU = (segment + t) / segmentCount;
                    vertices.Add(new Vector3(Mathf.Cos(angle) * inner, Mathf.Sin(angle) * inner, 0f)); uv.Add(new Vector2(globalU, 0f)); localUv.Add(new Vector2(t, 0f));
                    vertices.Add(new Vector3(Mathf.Cos(angle) * outer, Mathf.Sin(angle) * outer, 0f)); uv.Add(new Vector2(globalU, 1f)); localUv.Add(new Vector2(t, 1f));
                }
                for (var point = 0; point < subdivisions; point++)
                {
                    var index = first + point * 2;
                    triangles.Add(index); triangles.Add(index + 1); triangles.Add(index + 3);
                    triangles.Add(index); triangles.Add(index + 3); triangles.Add(index + 2);
                }
            }
            vertices[vertices.Count - 2] = vertices[0];
            vertices[vertices.Count - 1] = vertices[1];
            mesh.Clear(); mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetUVs(1, localUv); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds(); EditorUtility.SetDirty(mesh);
        }

        private static string Absolute(string path) { return Path.GetFullPath(Path.Combine(Directory.GetParent(Application.dataPath).FullName, path.Replace('/', Path.DirectorySeparatorChar))); }
        internal static void EnsureFolder(string path) { if (AssetDatabase.IsValidFolder(path)) return; var parent = Path.GetDirectoryName(path).Replace('\\', '/'); EnsureFolder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(path)); }
    }

    public sealed class Impact2DCompiler
    {
        public const string CompilerVersion = "impact2d-29";
        public const string GeneratedRoot = "Assets/VFX/Generated";
        public const string DefaultRecipePath = "Assets/VFX/Recipes/Impact/frost_impact_2d.default.json";

        public VfxBuildPlan DryRun(string recipeJson)
        {
            var parsed = Impact2DRecipeParser.Parse(recipeJson); var plan = new VfxBuildPlan(); plan.Report.AddRange(parsed.Report);
            if (plan.Report.HasErrors) { plan.Items.Add(new VfxBuildItem { State = VfxBuildItemState.Blocked, AssetPath = "/", Reason = "Impact Recipe validation failed." }); return plan; }
            plan.RecipeRevision = parsed.Value.Revision; plan.RecipeHash = RecipeCanonicalizer.ComputeSha256(recipeJson); plan.BuildHash = BuildHash(plan.RecipeHash);
            var prefabPath = PrefabPath(parsed.Value.Id); var exists = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null; var manifest = LoadRulesManifest(parsed.Value.Id);
            var dependenciesExist = Impact2DSharedLibrary.Dependencies.All(path => AssetDatabase.LoadMainAssetAtPath(path) != null);
            var unchanged = exists && dependenciesExist && manifest != null && string.Equals(manifest.BuildHash, plan.BuildHash, StringComparison.Ordinal);
            plan.Items.Add(new VfxBuildItem { State = unchanged ? VfxBuildItemState.Unchanged : exists ? VfxBuildItemState.Update : VfxBuildItemState.Create, AssetPath = prefabPath, Reason = unchanged ? "Recipe and shared dependency hashes are unchanged." : "Impact Runtime Entry or its managed input set differs." });
            return plan;
        }

        public VfxBuildResult Build(string recipeJson)
        {
            var parsed = Impact2DRecipeParser.Parse(recipeJson); var early = new VfxBuildResult { Plan = new VfxBuildPlan(), Succeeded = false };
            early.Plan.Report.AddRange(parsed.Report); if (early.Plan.Report.HasErrors) return early;
            try { Impact2DSharedLibrary.Ensure(); }
            catch (Exception exception) { early.Plan.Report.Add("E1610", ValidationSeverity.Error, "/shared", "Could not prepare Impact shared library: " + exception.Message); return early; }
            var plan = DryRun(recipeJson); var recipe = parsed.Value; var result = new VfxBuildResult { Plan = plan, PrefabPath = PrefabPath(recipe.Id) };
            if (plan.IsBlocked) return result;
            if (plan.Items.Single().State == VfxBuildItemState.Unchanged)
            {
                var audit = VfxProductionRules.EnforceAndWriteManifest(recipe.Id, "impact", recipe.RecipeVersion, recipe.Revision, plan.RecipeHash, plan.BuildHash, CompilerVersion, result.PrefabPath, OutputFolder(recipe.Id), recipe.Duration);
                plan.Report.AddRange(audit.Report); result.Succeeded = !plan.Report.HasErrors; return result;
            }
            Impact2DSharedLibrary.EnsureFolder(GeneratedRoot);
            var tempName = "impacttmp_" + Guid.NewGuid().ToString("N").Substring(0, 8); var guid = AssetDatabase.CreateFolder(GeneratedRoot, tempName); var temp = AssetDatabase.GUIDToAssetPath(guid);
            try
            {
                var tempPrefab = BuildTemporary(recipe, temp);
                Commit(recipe, plan, temp, tempPrefab);
                result.Succeeded = true;
            }
            catch (Exception exception) { if (!plan.Report.HasErrors) plan.Report.Add("E1611", ValidationSeverity.Error, "/build", "Impact build failed: " + exception.Message); }
            finally { if (AssetDatabase.IsValidFolder(temp)) AssetDatabase.DeleteAsset(temp); AssetDatabase.SaveAssets(); AssetDatabase.Refresh(); }
            return result;
        }

        private static GameObject BuildTemporary(Impact2DRecipe recipe, string temp)
        {
            var root = new GameObject("VFX_" + recipe.Id);
            try
            {
                var shardCountA = recipe.ShardCount / 2;
                var shardCountB = recipe.ShardCount - shardCountA;
                var systems = new[]
                {
                    Mist(root.transform, recipe),
                    Ring(root.transform, recipe),
                    Shards(root.transform, recipe, "IceShards_Large", recipe.RandomSeed + 1, shardCountA, 4f, new Vector2(4.15f, 5.75f), new Vector2(.44f, .72f)),
                    Shards(root.transform, recipe, "IceShards_Small", recipe.RandomSeed + 5, shardCountB, 31f, new Vector2(4.55f, 6.15f), new Vector2(.24f, .44f)),
                    Motes(root.transform, recipe),
                    Core(root.transform, recipe)
                };
                var controller = root.AddComponent<TimedImpactVfxController>(); var serialized = new SerializedObject(controller); var array = serialized.FindProperty("systems"); array.arraySize = systems.Length; for (var index = 0; index < systems.Length; index++) array.GetArrayElementAtIndex(index).objectReferenceValue = systems[index]; serialized.FindProperty("duration").floatValue = (float)recipe.Duration; serialized.ApplyModifiedPropertiesWithoutUndo();
                return PrefabUtility.SaveAsPrefabAsset(root, temp + "/VFX_" + recipe.Id + ".prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static ParticleSystem Core(Transform parent, Impact2DRecipe recipe)
        {
            var ps = Particle("CoreFlash", parent, Impact2DSharedLibrary.CoreMaterial, 40, recipe.RandomSeed, 2); var main = ps.main; main.startLifetime = new ParticleSystem.MinMaxCurve(.12f, .17f); main.startSize = new ParticleSystem.MinMaxCurve(.88f, 1.18f); main.startRotation = new ParticleSystem.MinMaxCurve(-.22f, .22f); main.startColor = new Color(.86f, .98f, 1f, .72f); var emission = ps.emission; emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 2) }); var size = ps.sizeOverLifetime; size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, .06f), new Keyframe(.13f, .78f), new Keyframe(.34f, 1f), new Keyframe(1f, .46f))); SetColorOverLifetime(ps, new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(.68f, .92f, 1f), 1f) }, new[] { new GradientAlphaKey(.28f, 0f), new GradientAlphaKey(.76f, .1f), new GradientAlphaKey(.34f, .48f), new GradientAlphaKey(0f, 1f) }); return ps;
        }

        private static ParticleSystem Shards(Transform parent, Impact2DRecipe recipe, string name, uint seed, int count, float angleOffset, Vector2 speedRange, Vector2 sizeRange)
        {
            var ps = Particle(name, parent, Impact2DSharedLibrary.ShardMaterial, 30, seed, Mathf.Max(1, count));
            ps.transform.localRotation = Quaternion.Euler(0f, 0f, angleOffset);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.22f, .35f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speedRange.x, speedRange.y);
            main.startSize = new ParticleSystem.MinMaxCurve(sizeRange.x, sizeRange.y);
            main.startRotation = new ParticleSystem.MinMaxCurve(-.09f, .09f);
            main.startColor = new Color(.86f, .98f, 1f, .86f);
            var emission = ps.emission;
            emission.SetBursts(new[] { new ParticleSystem.Burst(.012f, (short)count) });
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = .025f;
            shape.arc = 360f;
            shape.arcMode = ParticleSystemShapeMultiModeValue.BurstSpread;
            shape.arcSpread = 1f / count;
            shape.randomDirectionAmount = .035f;
            shape.randomPositionAmount = .018f;
            shape.alignToDirection = false;
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = .03f;
            renderer.lengthScale = .84f;
            var sheet = ps.textureSheetAnimation;
            sheet.enabled = true;
            sheet.mode = ParticleSystemAnimationMode.Grid;
            sheet.numTilesX = 2;
            sheet.numTilesY = 2;
            sheet.animation = ParticleSystemAnimationType.WholeSheet;
            sheet.timeMode = ParticleSystemAnimationTimeMode.Lifetime;
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f);
            sheet.startFrame = new ParticleSystem.MinMaxCurve(0f, .999f);
            sheet.cycleCount = 1;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, .12f), new Keyframe(.14f, 1f), new Keyframe(.62f, .84f), new Keyframe(1f, .02f)));
            SetColorOverLifetime(ps, new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(.72f, .9f, 1f), .72f), new GradientColorKey(new Color(.55f, .82f, 1f), 1f) }, new[] { new GradientAlphaKey(.08f, 0f), new GradientAlphaKey(.86f, .12f), new GradientAlphaKey(.82f, .64f), new GradientAlphaKey(.56f, .82f), new GradientAlphaKey(0f, 1f) });
            return ps;
        }

        private static ParticleSystem Ring(Transform parent, Impact2DRecipe recipe)
        {
            var ps = Particle("BrokenIceRingSegments", parent, Impact2DSharedLibrary.RingMaterial, 20, recipe.RandomSeed + 2, 1); var main = ps.main; main.startLifetime = .32f; main.startSize = 1.48f; main.startRotation = .018f; main.startColor = new Color(.82f, .96f, 1f, .72f); var emission = ps.emission; emission.SetBursts(new[] { new ParticleSystem.Burst(.025f, 1) }); var renderer = ps.GetComponent<ParticleSystemRenderer>(); renderer.renderMode = ParticleSystemRenderMode.Mesh; renderer.mesh = AssetDatabase.LoadAssetAtPath<Mesh>(Impact2DSharedLibrary.RingMesh); renderer.alignment = ParticleSystemRenderSpace.View; var size = ps.sizeOverLifetime; size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, .18f), new Keyframe(.2f, .92f), new Keyframe(.56f, 1.28f), new Keyframe(1f, (float)recipe.RingScale))); SetColorOverLifetime(ps, new[] { new GradientColorKey(new Color(.88f, 1f, 1f), 0f), new GradientColorKey(new Color(.48f, .78f, 1f), 1f) }, new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(.76f, .15f), new GradientAlphaKey(.51f, .54f), new GradientAlphaKey(.15f, .82f), new GradientAlphaKey(0f, 1f) }); return ps;
        }

        private static ParticleSystem Mist(Transform parent, Impact2DRecipe recipe)
        {
            var ps = Particle("FrostMistSegments", parent, Impact2DSharedLibrary.MistMaterial, 12, recipe.RandomSeed + 4, 8); var main = ps.main; main.startLifetime = new ParticleSystem.MinMaxCurve(.32f, .4f); main.startSpeed = new ParticleSystem.MinMaxCurve(.035f, .09f); main.startSize = new ParticleSystem.MinMaxCurve(.19f, .3f); main.startRotation = new ParticleSystem.MinMaxCurve(-.3f, .3f); main.startColor = new Color(.68f, .86f, 1f, .44f); var emission = ps.emission; emission.SetBursts(new[] { new ParticleSystem.Burst(.035f, 8) }); var shape = ps.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Circle; shape.radius = .41f; shape.arc = 360f; shape.arcMode = ParticleSystemShapeMultiModeValue.BurstSpread; shape.arcSpread = 1f / 8f; shape.randomPositionAmount = .045f; shape.randomDirectionAmount = .12f; var size = ps.sizeOverLifetime; size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, .45f), new Keyframe(.25f, 1f), new Keyframe(.72f, 1.32f), new Keyframe(1f, .4f))); SetColorOverLifetime(ps, new[] { new GradientColorKey(new Color(.7f, .88f, 1f), 0f), new GradientColorKey(new Color(.46f, .72f, .94f), 1f) }, new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(.42f, .22f), new GradientAlphaKey(.28f, .6f), new GradientAlphaKey(.08f, .86f), new GradientAlphaKey(0f, 1f) }); return ps;
        }

        private static ParticleSystem Motes(Transform parent, Impact2DRecipe recipe)
        {
            var ps = Particle("SnowMotes", parent, Impact2DSharedLibrary.MoteMaterial, 34, recipe.RandomSeed + 3, 10); var main = ps.main; main.startLifetime = new ParticleSystem.MinMaxCurve(.26f, .43f); main.startSpeed = new ParticleSystem.MinMaxCurve(.38f, 1.05f); main.startSize = new ParticleSystem.MinMaxCurve(.026f, .064f); main.startColor = new Color(.7f, .94f, 1f, .56f); var emission = ps.emission; emission.SetBursts(new[] { new ParticleSystem.Burst(.055f, 7) }); var shape = ps.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Circle; shape.radius = .14f; var velocity = ps.velocityOverLifetime; velocity.enabled = true; velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f); velocity.y = new ParticleSystem.MinMaxCurve(.1f, .34f); velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f); var size = ps.sizeOverLifetime; size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, .35f), new Keyframe(.24f, 1f), new Keyframe(1f, 0f))); SetColorOverLifetime(ps, new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(.58f, .86f, 1f), 1f) }, new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(.72f, .2f), new GradientAlphaKey(.5f, .62f), new GradientAlphaKey(0f, 1f) }); return ps;
        }

        private static ParticleSystem Particle(string name, Transform parent, string materialPath, int sortingOrder, uint seed, int maxParticles)
        {
            var gameObject = new GameObject(name); gameObject.transform.SetParent(parent, false); var ps = gameObject.AddComponent<ParticleSystem>(); ps.useAutoRandomSeed = false; ps.randomSeed = seed; var main = ps.main; main.loop = false; main.duration = .5f; main.playOnAwake = false; main.simulationSpace = ParticleSystemSimulationSpace.Local; main.maxParticles = maxParticles; main.startSpeed = 0f; var emission = ps.emission; emission.rateOverTime = 0f; var shape = ps.shape; shape.enabled = false; var renderer = ps.GetComponent<ParticleSystemRenderer>(); renderer.renderMode = ParticleSystemRenderMode.Billboard; renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath); renderer.sortingLayerName = "Default"; renderer.sortingOrder = sortingOrder; return ps;
        }

        private static void SetColorOverLifetime(ParticleSystem ps, GradientColorKey[] colors, GradientAlphaKey[] alpha)
        {
            var gradient = new Gradient(); gradient.SetKeys(colors, alpha); var module = ps.colorOverLifetime; module.enabled = true; module.color = new ParticleSystem.MinMaxGradient(gradient);
        }

        private static void Commit(Impact2DRecipe recipe, VfxBuildPlan plan, string temp, GameObject tempPrefab)
        {
            var folder = OutputFolder(recipe.Id); var existed = AssetDatabase.IsValidFolder(folder); Impact2DSharedLibrary.EnsureFolder(folder); var prefabPath = PrefabPath(recipe.Id); var prior = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath); var backup = temp + "/prior.prefab"; if (prior != null && !AssetDatabase.CopyAsset(prefabPath, backup)) throw new InvalidOperationException("Could not snapshot existing Impact Prefab."); var priorManifest = VfxProductionRules.CaptureManifest(recipe.Id);
            try
            {
                if (PrefabUtility.SaveAsPrefabAsset(tempPrefab, prefabPath) == null) throw new InvalidOperationException("Could not save Impact Runtime Prefab.");
                AssetDatabase.SaveAssets();
                var audit = VfxProductionRules.EnforceAndWriteManifest(recipe.Id, "impact", recipe.RecipeVersion, recipe.Revision, plan.RecipeHash, plan.BuildHash, CompilerVersion, prefabPath, folder, recipe.Duration); plan.Report.AddRange(audit.Report); if (audit.Report.HasErrors) throw new InvalidOperationException("Production rules rejected Impact output.");
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
        private static string BuildHash(string recipeHash) { var input = new StringBuilder(recipeHash).Append('|').Append(CompilerVersion).Append('|').Append(Application.unityVersion); foreach (var path in Impact2DSharedLibrary.Dependencies.OrderBy(value => value, StringComparer.Ordinal)) input.Append('|').Append(path).Append('|').Append(AssetDatabase.LoadMainAssetAtPath(path) == null ? "missing" : AssetDatabase.GetAssetDependencyHash(path).ToString()); using (var sha = SHA256.Create()) return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(input.ToString())).Select(value => value.ToString("x2", CultureInfo.InvariantCulture))); }
        public static string OutputFolder(string id) { return GeneratedRoot + "/" + VfxProjectRules.SanitizeId(id); }
        public static string PrefabPath(string id) { return OutputFolder(id) + "/VFX_" + VfxProjectRules.SanitizeId(id) + ".prefab"; }
        private static string Absolute(string path) { return Path.GetFullPath(Path.Combine(Directory.GetParent(Application.dataPath).FullName, path.Replace('/', Path.DirectorySeparatorChar))); }
    }
}

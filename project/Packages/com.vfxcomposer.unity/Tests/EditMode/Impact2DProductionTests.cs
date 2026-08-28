using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using VFXComposer.Editor.Impact2D;
using VFXComposer.Editor.Rules;

namespace VFXComposer.Tests.EditMode
{
    public sealed class Impact2DProductionTests
    {
        [Test]
        public void ImpactRecipe_IsStrictAndRejectsUnknownOrUnsafeValues()
        {
            var valid = Text(); var parsed = Impact2DRecipeParser.Parse(valid); Assert.That(parsed.Report.HasErrors, Is.False, Describe(parsed.Report)); Assert.That(parsed.Value.Archetype, Is.EqualTo("impact"));
            var unknown = JObject.Parse(valid); unknown["unityProperty"] = "m_Something"; Assert.That(Impact2DRecipeParser.Parse(unknown.ToString()).Report.Contains("E1601", "/unityProperty"), Is.True);
            var unsafeCount = JObject.Parse(valid); unsafeCount["shardCount"] = 200; Assert.That(Impact2DRecipeParser.Parse(unsafeCount.ToString()).Report.Contains("E1605", "/shardCount"), Is.True);
        }

        [Test]
        public void FrostImpact_BuildsStrictSinglePrefabWithSharedDependenciesAndExternalManifest()
        {
            var compiler = new Impact2DCompiler(); var first = compiler.Build(Text()); Assert.That(first.Succeeded, Is.True, Describe(first.Plan.Report));
            var prefabPath = Impact2DCompiler.PrefabPath("frost_impact_2d"); var folder = Impact2DCompiler.OutputFolder("frost_impact_2d"); var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath); Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(7));
            Assert.That(MaxDepth(prefab), Is.EqualTo(1));
            Assert.That(prefab.GetComponentsInChildren<ParticleSystem>(true).Length, Is.EqualTo(6));
            Assert.That(prefab.GetComponentsInChildren<ParticleSystem>(true).Sum(system => system.main.maxParticles), Is.LessThanOrEqualTo(40));
            var segmentedRing = prefab.transform.Find("BrokenIceRingSegments").GetComponent<ParticleSystem>(); Assert.That(segmentedRing.GetComponent<ParticleSystemRenderer>().renderMode, Is.EqualTo(ParticleSystemRenderMode.Mesh)); Assert.That(segmentedRing.GetComponent<ParticleSystemRenderer>().mesh.vertexCount, Is.EqualTo(224), "Sixteen deterministic ring segments must be combined into one compact mesh and one renderer.");
            foreach (var shardName in new[] { "IceShards_Large", "IceShards_Small" })
            {
                var shards = prefab.transform.Find(shardName).GetComponent<ParticleSystem>();
                Assert.That(shards.shape.arcMode, Is.EqualTo(ParticleSystemShapeMultiModeValue.BurstSpread));
                Assert.That(shards.shape.arcSpread, Is.EqualTo(1f / shards.main.maxParticles).Within(.0001f));
                Assert.That(shards.shape.randomDirectionAmount, Is.GreaterThan(0f));
                Assert.That(shards.shape.alignToDirection, Is.False);
                Assert.That(shards.GetComponent<ParticleSystemRenderer>().renderMode, Is.EqualTo(ParticleSystemRenderMode.Stretch), "Every +X shard variant must align to radial velocity through Stretch billboard rendering.");
                Assert.That(shards.textureSheetAnimation.enabled, Is.True);
                Assert.That(shards.textureSheetAnimation.numTilesX, Is.EqualTo(2));
                Assert.That(shards.textureSheetAnimation.numTilesY, Is.EqualTo(2));
            }
            Assert.That(prefab.GetComponent<TimedImpactVfxController>(), Is.Not.Null);
            Assert.That(prefab.GetComponents<MonoBehaviour>().Count(value => value is IVfxRuntimeEntry), Is.EqualTo(1));
            var files = Directory.GetFiles(Absolute(folder), "*", SearchOption.AllDirectories).Where(path => !path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)).ToArray(); Assert.That(files.Length, Is.EqualTo(1), "Strict effect output owns only its Runtime Prefab."); Assert.That(Path.GetFileName(files[0]), Is.EqualTo("VFX_frost_impact_2d.prefab"));
            var materials = prefab.GetComponentsInChildren<Renderer>(true).SelectMany(renderer => renderer.sharedMaterials).Where(material => material != null).Select(AssetDatabase.GetAssetPath).Distinct().ToArray(); Assert.That(materials.Length, Is.EqualTo(5)); Assert.That(materials.All(path => path.StartsWith("Assets/VFX/Shared/Frost/Materials/", StringComparison.Ordinal)), Is.True); Assert.That(materials.Any(path => path.StartsWith(folder + "/", StringComparison.Ordinal)), Is.False);
            var manifestPath = VfxProjectRules.ManifestAbsolutePath("frost_impact_2d"); var manifest = JObject.Parse(File.ReadAllText(manifestPath)); Assert.That((string)manifest["enforcement"], Is.EqualTo("strict")); Assert.That((string)manifest["archetype"], Is.EqualTo("impact")); Assert.That((string)manifest["sourceRecipePath"], Is.EqualTo(Impact2DCompiler.DefaultRecipePath)); Assert.That(((JArray)manifest["ownedOutputs"]).Count, Is.EqualTo(1)); Assert.That(((JArray)manifest["dependencies"]).Any(value => ((string)value["path"]).StartsWith("Assets/VFX/Shared/Frost/", StringComparison.Ordinal)), Is.True); Assert.That(((JArray)manifest["dependencies"]).Any(value => string.Equals((string)value["path"], Impact2DSharedLibrary.LegacyImpactAtlas, StringComparison.Ordinal)), Is.False, "The whole-ring Impact atlas must no longer enter the Runtime dependency graph."); Assert.That((int)manifest["cost"]["dependencyResidentTextureBytes"], Is.LessThan(100000)); Assert.That((int)manifest["cost"]["gameObjects"], Is.EqualTo(7)); Assert.That((int)manifest["cost"]["maxDepth"], Is.EqualTo(1));
            var before = File.ReadAllBytes(manifestPath); var second = compiler.Build(Text()); Assert.That(second.Succeeded, Is.True, Describe(second.Plan.Report)); Assert.That(second.Plan.Items.Single().State.ToString(), Is.EqualTo("Unchanged")); CollectionAssert.AreEqual(before, File.ReadAllBytes(manifestPath), "Unchanged Build must not rewrite the external Manifest.");
        }

        [Test]
        public void FrostImpact_PreviewSceneUsesFormalPrefabAndOnlySceneOwnsReplayDriver()
        {
            Impact2DPreviewScene.BuildForBatch(); Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(Impact2DPreviewScene.ScenePath), Is.Not.Null); var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Impact2DCompiler.PrefabPath("frost_impact_2d")); Assert.That(prefab.GetComponentInChildren<ImpactPreviewPlaybackDriver>(true), Is.Null, "Preview driver must never enter the Runtime Prefab.");
        }

        [Test]
        public void FrostImpact_CompactRuntimeKeepsOnlyShardAtlasAndProceduralSharedShapes()
        {
            Impact2DSharedLibrary.Ensure();
            var shard = LoadPng(Impact2DSharedLibrary.ShardAtlas);
            try
            {
                Assert.That(shard.width, Is.EqualTo(256)); Assert.That(shard.height, Is.EqualTo(256));
                for (var y = 0; y < 2; y++) for (var x = 0; x < 2; x++) Assert.That(CountAlpha(shard, x * 128, y * 128, 128, 128), Is.GreaterThan(120), "Every shard variant cell must contain an isolated +Y silhouette.");
            }
            finally { UnityEngine.Object.DestroyImmediate(shard); }
            Assert.That(AssetDatabase.LoadAssetAtPath<Material>(Impact2DSharedLibrary.ShardMaterial).shader.name, Is.EqualTo(Impact2DSharedLibrary.ShaderName));
            foreach (var path in new[] { Impact2DSharedLibrary.CoreMaterial, Impact2DSharedLibrary.RingMaterial, Impact2DSharedLibrary.MistMaterial, Impact2DSharedLibrary.MoteMaterial }) Assert.That(AssetDatabase.LoadAssetAtPath<Material>(path).shader.name, Is.EqualTo(Impact2DSharedLibrary.ProceduralShaderName));
            foreach (var path in new[] { Impact2DSharedLibrary.CoreMaterial, Impact2DSharedLibrary.ShardMaterial, Impact2DSharedLibrary.RingMaterial, Impact2DSharedLibrary.MistMaterial, Impact2DSharedLibrary.MoteMaterial }) Assert.That(AssetDatabase.LoadAssetAtPath<Material>(path).GetTag("RenderType", false), Is.EqualTo("Transparent"));
            var importer = AssetImporter.GetAtPath(Impact2DSharedLibrary.ShardAtlas) as TextureImporter; Assert.That(importer, Is.Not.Null); Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.CompressedHQ)); Assert.That(importer.mipmapEnabled, Is.False); Assert.That(importer.isReadable, Is.False);
            var ringMesh = AssetDatabase.LoadAssetAtPath<Mesh>(Impact2DSharedLibrary.RingMesh); Assert.That(ringMesh, Is.Not.Null); Assert.That(ringMesh.vertexCount, Is.EqualTo(224)); Assert.That(ringMesh.triangles.Length / 3, Is.EqualTo(192));
            var ringVertices = ringMesh.vertices;
            Assert.That(ringVertices[ringVertices.Length - 2], Is.EqualTo(ringVertices[0]), "The final logical sector must reuse the exact first inner-ring position.");
            Assert.That(ringVertices[ringVertices.Length - 1], Is.EqualTo(ringVertices[1]), "The final logical sector must reuse the exact first outer-ring position.");
            Assert.That(AssetDatabase.LoadMainAssetAtPath("Assets/VFX/Shared/Textures/T_ImpactFrost_Shard_v1.png"), Is.Null, "Deprecated 637KB single-shard runtime source must not survive the migration.");
        }

        [Test]
        public void FrostImpact_UsesPurposeSpecificBlendModesAndLifetimeCurves()
        {
            var result = new Impact2DCompiler().Build(Text()); Assert.That(result.Succeeded, Is.True, Describe(result.Plan.Report));
            Assert.That(AssetDatabase.LoadAssetAtPath<Material>(Impact2DSharedLibrary.CoreMaterial).GetFloat("_DstBlend"), Is.EqualTo((float)BlendMode.One));
            Assert.That(AssetDatabase.LoadAssetAtPath<Material>(Impact2DSharedLibrary.ShardMaterial).GetFloat("_DstBlend"), Is.EqualTo((float)BlendMode.One));
            Assert.That(AssetDatabase.LoadAssetAtPath<Material>(Impact2DSharedLibrary.MoteMaterial).GetFloat("_DstBlend"), Is.EqualTo((float)BlendMode.One));
            Assert.That(AssetDatabase.LoadAssetAtPath<Material>(Impact2DSharedLibrary.RingMaterial).GetFloat("_DstBlend"), Is.EqualTo((float)BlendMode.One));
            Assert.That(AssetDatabase.LoadAssetAtPath<Material>(Impact2DSharedLibrary.MistMaterial).GetFloat("_DstBlend"), Is.EqualTo((float)BlendMode.OneMinusSrcAlpha));

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Impact2DCompiler.PrefabPath("frost_impact_2d"));
            var core = prefab.transform.Find("CoreFlash").GetComponent<ParticleSystem>().colorOverLifetime.color.gradient;
            var shard = prefab.transform.Find("IceShards_Large").GetComponent<ParticleSystem>().colorOverLifetime.color.gradient;
            var ring = prefab.transform.Find("BrokenIceRingSegments").GetComponent<ParticleSystem>().colorOverLifetime.color.gradient;
            var mistSystem = prefab.transform.Find("FrostMistSegments").GetComponent<ParticleSystem>(); var mist = mistSystem.colorOverLifetime.color.gradient;
            Assert.That(core.Evaluate(.46f).a, Is.LessThan(core.Evaluate(.12f).a), "Core must release exposure quickly after its short peak.");
            Assert.That(shard.Evaluate(.64f).a, Is.GreaterThan(.75f), "Shards must retain readable facet energy through most of their flight.");
            Assert.That(shard.Evaluate(.82f).a, Is.GreaterThan(ring.Evaluate(.82f).a), "Late shards must remain more readable than the subordinate ring.");
            Assert.That(ring.Evaluate(.82f).a, Is.LessThan(.2f), "Ring must not persist as a bright plate.");
            Assert.That(mist.Evaluate(.25f).a * mistSystem.main.startColor.color.a, Is.LessThan(.25f), "Mist effective alpha must remain a low-energy spatial layer after Main and Color over Lifetime are combined.");
        }

        private static int MaxDepth(GameObject root) { return root.GetComponentsInChildren<Transform>(true).Max(value => { var depth = 0; while (value != root.transform) { depth++; value = value.parent; } return depth; }); }
        private static byte Alpha(Texture2D texture, int x, int y) { return texture.GetPixels32()[y * texture.width + x].a; }
        private static int CountAlpha(Texture2D texture, int x, int y, int width, int height) { return texture.GetPixels32().Where((pixel, index) => { var px = index % texture.width; var py = index / texture.width; return px >= x && px < x + width && py >= y && py < y + height && pixel.a > 8; }).Count(); }
        private static Texture2D LoadPng(string assetPath) { var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false); Assert.That(texture.LoadImage(File.ReadAllBytes(Absolute(assetPath))), Is.True, assetPath); return texture; }
        private static string Text() { return File.ReadAllText(Absolute(Impact2DCompiler.DefaultRecipePath)); }
        private static string Absolute(string assetPath) { return Path.Combine(Application.dataPath, assetPath.Substring("Assets/".Length)); }
        private static string Describe(VFXComposer.Editor.Domain.ValidationReport report) { return string.Join(" | ", report.Entries.Select(entry => entry.Code + " " + entry.Path + " " + entry.Message)); }
    }
}

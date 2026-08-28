using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using VFXComposer.Editor.Area2D;
using VFXComposer.Editor.Rules;

namespace VFXComposer.Tests.EditMode
{
    public sealed class Area2DProductionTests
    {
        [Test]
        public void AreaRecipe_IsStrictAndRejectsUnknownOrUnsafeValues()
        {
            var valid = Text(); var parsed = Area2DRecipeParser.Parse(valid); Assert.That(parsed.Report.HasErrors, Is.False, Describe(parsed.Report)); Assert.That(parsed.Value.Archetype, Is.EqualTo("area")); Assert.That(parsed.Value.Lifecycle, Is.EqualTo("sustained"));
            var unknown = JObject.Parse(valid); unknown["unityProperty"] = "m_Something"; Assert.That(Area2DRecipeParser.Parse(unknown.ToString()).Report.Contains("E1701", "/unityProperty"), Is.True);
            var unsafeCount = JObject.Parse(valid); unsafeCount["flameCount"] = 200; Assert.That(Area2DRecipeParser.Parse(unsafeCount.ToString()).Report.Contains("E1705", "/flameCount"), Is.True);
            var wrongType = JObject.Parse(valid); wrongType["archetype"] = "impact"; Assert.That(Area2DRecipeParser.Parse(wrongType.ToString()).Report.Contains("E1706", "/archetype"), Is.True);
        }

        [Test]
        public void InfernoArea_BuildsStrictSinglePrefabWithCompactSharedDependencies()
        {
            var compiler = new Area2DCompiler(); var first = compiler.Build(Text()); Assert.That(first.Succeeded, Is.True, Describe(first.Plan.Report));
            var prefabPath = Area2DCompiler.PrefabPath("inferno_vortex_area_2d"); var folder = Area2DCompiler.OutputFolder("inferno_vortex_area_2d"); var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath); Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(8)); Assert.That(MaxDepth(prefab), Is.EqualTo(1));
            Assert.That(prefab.GetComponentsInChildren<Renderer>(true).Length, Is.EqualTo(7)); Assert.That(prefab.GetComponentsInChildren<ParticleSystem>(true).Length, Is.EqualTo(2)); Assert.That(prefab.GetComponentsInChildren<ParticleSystem>(true).Sum(system => system.main.maxParticles), Is.LessThanOrEqualTo(48));
            Assert.That(prefab.GetComponentsInChildren<Renderer>(true).All(renderer => !renderer.enabled), Is.True, "Idle Prefab renderers must be serialized disabled so raw Quad geometry never leaks into Edit Mode.");
            Assert.That(prefab.GetComponent<InfernoAreaVfxController>(), Is.Not.Null); Assert.That(prefab.GetComponents<MonoBehaviour>().Count(value => value is IVfxRuntimeEntry), Is.EqualTo(1)); Assert.That(prefab.GetComponentInChildren<AreaPreviewPlaybackDriver>(true), Is.Null);
            var files = Directory.GetFiles(Absolute(folder), "*", SearchOption.AllDirectories).Where(path => !path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)).ToArray(); Assert.That(files.Length, Is.EqualTo(1)); Assert.That(Path.GetFileName(files[0]), Is.EqualTo("VFX_inferno_vortex_area_2d.prefab"));
            var materials = prefab.GetComponentsInChildren<Renderer>(true).SelectMany(renderer => renderer.sharedMaterials).Where(material => material != null).Distinct().Select(AssetDatabase.GetAssetPath).OrderBy(value => value).ToArray(); Assert.That(materials, Is.EquivalentTo(new[] { Area2DSharedLibrary.BodyMaterial, Area2DSharedLibrary.HotMaterial }));
            var textures = AssetDatabase.GetDependencies(prefabPath, true).Where(path => path.EndsWith(".png", StringComparison.OrdinalIgnoreCase)).ToArray(); Assert.That(textures, Is.EqualTo(new[] { Area2DSharedLibrary.MaskAtlas }), "The complete fire effect must depend on exactly one compact Runtime PNG.");
            var manifestPath = VfxProjectRules.ManifestAbsolutePath("inferno_vortex_area_2d"); var manifest = JObject.Parse(File.ReadAllText(manifestPath)); Assert.That((string)manifest["enforcement"], Is.EqualTo("strict")); Assert.That((string)manifest["archetype"], Is.EqualTo("area")); Assert.That((string)manifest["sourceRecipePath"], Is.EqualTo(Area2DCompiler.DefaultRecipePath)); Assert.That(((JArray)manifest["ownedOutputs"]).Count, Is.EqualTo(1)); Assert.That((int)manifest["cost"]["gameObjects"], Is.EqualTo(8)); Assert.That((int)manifest["cost"]["particleSystems"], Is.EqualTo(2));
            var before = File.ReadAllBytes(manifestPath); var second = compiler.Build(Text()); Assert.That(second.Succeeded, Is.True, Describe(second.Plan.Report)); Assert.That(second.Plan.Items.Single().State.ToString(), Is.EqualTo("Unchanged")); CollectionAssert.AreEqual(before, File.ReadAllBytes(manifestPath));
        }

        [Test]
        public void FireMaskAtlas_IsOneTinyFourCellTextureWithExplicitPlatformFormats()
        {
            Area2DSharedLibrary.Ensure(); var absolute = Absolute(Area2DSharedLibrary.MaskAtlas); Assert.That(new FileInfo(absolute).Length, Is.LessThanOrEqualTo(64 * 1024));
            var texture = LoadPng(Area2DSharedLibrary.MaskAtlas);
            try
            {
                Assert.That(texture.width, Is.EqualTo(256)); Assert.That(texture.height, Is.EqualTo(256));
                for (var y = 0; y < 2; y++) for (var x = 0; x < 2; x++) Assert.That(CountAlpha(texture, x * 128, y * 128, 128, 128), Is.GreaterThan(1500), "Every compact mask cell must contain usable alpha.");
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
            var importer = AssetImporter.GetAtPath(Area2DSharedLibrary.MaskAtlas) as TextureImporter; Assert.That(importer, Is.Not.Null); Assert.That(importer.mipmapEnabled, Is.False); Assert.That(importer.isReadable, Is.False); Assert.That(importer.sRGBTexture, Is.False); Assert.That(importer.GetPlatformTextureSettings("Standalone").format, Is.EqualTo(TextureImporterFormat.BC4)); Assert.That(importer.GetPlatformTextureSettings("Android").format, Is.EqualTo(TextureImporterFormat.ASTC_8x8));
        }

        [Test]
        public void FireRing_IsExactlyClosedAndShaderUsesPeriodicAngularTerms()
        {
            Area2DSharedLibrary.Ensure(); var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(Area2DSharedLibrary.RingMesh); Assert.That(mesh, Is.Not.Null); Assert.That(mesh.vertexCount, Is.EqualTo(194)); Assert.That(mesh.triangles.Length / 3, Is.EqualTo(192));
            var vertices = mesh.vertices; var uv = mesh.uv; Assert.That(vertices[vertices.Length - 2], Is.EqualTo(vertices[0])); Assert.That(vertices[vertices.Length - 1], Is.EqualTo(vertices[1])); Assert.That(uv[0].x, Is.EqualTo(0f)); Assert.That(uv[uv.Length - 2].x, Is.EqualTo(1f));
            var shader = File.ReadAllText(Absolute(Area2DSharedLibrary.ShaderPath)); StringAssert.Contains("uv.x * 7.0", shader); StringAssert.Contains("uv.x * 13.0", shader); StringAssert.Contains("const float tau", shader);
        }

        [Test]
        public void InfernoArea_PreviewUsesFormalPrefabAndSceneOnlyDriver()
        {
            Area2DPreviewScene.BuildForBatch(); Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(Area2DPreviewScene.ScenePath), Is.Not.Null); var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Area2DCompiler.PrefabPath("inferno_vortex_area_2d")); Assert.That(prefab.GetComponentInChildren<AreaPreviewPlaybackDriver>(true), Is.Null);
        }

        private static int MaxDepth(GameObject root) { return root.GetComponentsInChildren<Transform>(true).Max(value => { var depth = 0; while (value != root.transform) { depth++; value = value.parent; } return depth; }); }
        private static int CountAlpha(Texture2D texture, int x, int y, int width, int height) { var pixels = texture.GetPixels32(); var count = 0; for (var py = y; py < y + height; py++) for (var px = x; px < x + width; px++) if (pixels[py * texture.width + px].a > 8) count++; return count; }
        private static Texture2D LoadPng(string assetPath) { var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false); Assert.That(texture.LoadImage(File.ReadAllBytes(Absolute(assetPath))), Is.True); return texture; }
        private static string Text() { return File.ReadAllText(Absolute(Area2DCompiler.DefaultRecipePath)); }
        private static string Absolute(string assetPath) { return Path.Combine(Application.dataPath, assetPath.Substring("Assets/".Length)); }
        private static string Describe(VFXComposer.Editor.Domain.ValidationReport report) { return string.Join(" | ", report.Entries.Select(entry => entry.Code + " " + entry.Path + " " + entry.Message)); }
    }
}

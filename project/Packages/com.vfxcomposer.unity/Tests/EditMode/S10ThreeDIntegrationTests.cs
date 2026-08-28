using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VFXComposer;
using VFXComposer.Editor.Build;
using VFXComposer.Editor.Catalog;
using VFXComposer.Editor.Domain;
using VFXComposer.Editor.Preview;
using VFXComposer.Editor.UI;
using VFXComposer.Editor.Validation;

namespace VFXComposer.Tests.EditMode
{
    public sealed class S10ThreeDIntegrationTests
    {
        private const string RecipePath = "Assets/VFX/Recipes/fireball-3d.default.json";
        private const string Output = "Assets/VFX/Generated/fireball_3d_s10test";
        private const string TemplateRoot = "Assets/VFX/Templates/3D";
        private static readonly string[] Ids = { "PFT_3D_FireCore", "PFT_3D_Embers", "PFT_3D_FireTrail", "PFT_3D_LaunchFlash", "PFT_3D_FireImpact", "PFT_3D_Shockwave" };

        [SetUp] public void SetUp() { DeleteTestOutput(); }
        [TearDown] public void TearDown() { DeleteTestOutput(); }

        [Test]
        public void Formal3DTemplates_HaveStableGuidManifestMeshBillboardAndDimensionContracts()
        {
            var catalog = VfxCompiler.LoadFormalCatalog();
            Assert.That(catalog.Report.HasErrors, Is.False, Report(catalog.Report));
            foreach (var id in Ids)
            {
                TemplateManifest manifest;
                Assert.That(catalog.TryGet(id, out manifest), Is.True, id);
                Assert.That(manifest.Dimension, Is.EqualTo(RecipeDimension.ThreeD));
                Assert.That(AssetDatabase.GUIDToAssetPath(manifest.AssetGuid), Is.EqualTo(manifest.AssetPath));
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(manifest.AssetPath);
                Assert.That(prefab, Is.Not.Null, manifest.AssetPath);
                Assert.That(AssetDatabase.GetDependencies(manifest.AssetPath, true).Where(path => path.StartsWith("Assets/", StringComparison.Ordinal)).All(path => path.StartsWith(TemplateRoot, StringComparison.Ordinal)), Is.True, id + " must stay within its protected template inputs.");
            }
            var core = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/VFX/Templates/3D/Prefabs/PFT_3D_FireCore.prefab");
            Assert.That(core.GetComponentInChildren<MeshFilter>(true), Is.Not.Null);
            Assert.That(core.GetComponentInChildren<CameraFacingBillboard>(true), Is.Not.Null);
            var shockwave = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/VFX/Templates/3D/Prefabs/PFT_3D_Shockwave.prefab");
            Assert.That(shockwave.GetComponent<ParticleSystemRenderer>().mesh, Is.Not.Null, "Shockwave must use the retained Ring Mesh asset.");
        }

        [Test]
        public void Formal3DParameters_ApplyAtManifestMinimumDefaultAndMaximum()
        {
            var catalog = VfxCompiler.LoadFormalCatalog();
            var registry = VfxBindingHandlerRegistry.CreateFormal();
            foreach (var id in Ids)
            {
                var manifest = catalog.ByTemplateId[id];
                foreach (var parameter in manifest.Parameters)
                foreach (var value in new[] { parameter.Value.Min, parameter.Value.Default, parameter.Value.Max })
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(manifest.AssetPath);
                    var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                    try
                    {
                        registry.Apply(parameter.Value.Binding, instance, value);
                        AssertReadback(id, parameter.Key, instance, Convert.ToSingle(((Newtonsoft.Json.Linq.JValue)value).Value, System.Globalization.CultureInfo.InvariantCulture));
                    }
                    finally { UnityEngine.Object.DestroyImmediate(instance); }
                }
            }
        }

        [Test]
        public void ThreeDCompiler_BuildsIdempotentlyPreservesGuidAndDoesNotMutateTemplates()
        {
            var beforeTemplates = Hashes(TemplateRoot);
            var compiler = new VfxCompiler();
            var first = compiler.Build(Recipe());
            Assert.That(first.Succeeded, Is.True, Report(first.Plan.Report));
            var prefabPath = first.PrefabPath;
            var guid = AssetDatabase.AssetPathToGUID(prefabPath);
            var files = Hashes(Output);
            var second = compiler.Build(Recipe().Replace("\n", "\n  ").Replace(": ", " : "));
            Assert.That(second.Succeeded, Is.True, Report(second.Plan.Report));
            Assert.That(second.Plan.Items.Single().State, Is.EqualTo(VfxBuildItemState.Unchanged));
            Assert.That(AssetDatabase.AssetPathToGUID(prefabPath), Is.EqualTo(guid));
            CollectionAssert.AreEquivalent(files, Hashes(Output));
            CollectionAssert.AreEquivalent(beforeTemplates, Hashes(TemplateRoot));
            var output = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(output.GetComponent<GeneratedVfxController>(), Is.Not.Null);
            Assert.That(output.transform.Find("Travel/Core/BillboardFlame"), Is.Not.Null);
            Assert.That(output.transform.Find("Impact/Shockwave").GetComponent<ParticleSystemRenderer>().mesh, Is.Not.Null);
        }

        [Test]
        public void DimensionMismatchAndUnknown3DBinding_AreExplicitlyBlocked()
        {
            var badDimension = Recipe().Replace("\"dimension\": \"3d\"", "\"dimension\": \"2d\"");
            var plan = new VfxCompiler().DryRun(badDimension);
            Assert.That(plan.Report.Contains("E310", "/stages/launch/modules/launchFlash/templateId"), Is.True, Report(plan.Report));
            Assert.That(plan.Items.Single().State, Is.EqualTo(VfxBuildItemState.Blocked));
            var badBinding = "{ 'manifestVersion':1, 'templateId':'T_Bad3D', 'templateVersion':'1', 'kind':'energy_body', 'dimension':'3d', 'assetGuid':'39025b13b4ad89b4a8aa54833fba71c1', 'assetPath':'Assets/VFX/Templates/3D/Prefabs/PFT_3D_FireCore.prefab', 'tags':[], 'parameters':{'scale':{'type':'float','min':.6,'max':2.4,'default':1.2,'binding':'3d.nope'}}, 'cost':{'estimatedPeakParticles':0,'materials':1,'trails':0} }";
            var badRecipe = "{ 'recipeVersion':1, 'id':'bad_3d', 'dimension':'3d', 'archetype':'projectile', 'targetProfile':'pc_editor', 'randomSeed':1, 'stages':[{'id':'travel','trigger':'on_launch','duration':1,'enabled':true,'modules':[{'id':'core','kind':'energy_body','templateId':'T_Bad3D','parameters':{'scale':1},'enabled':true}]}], 'metadata':{'createdBy':'test','templateCatalogVersion':'1'} }";
            var custom = TemplateCatalog.FromManifestJson(new[] { badBinding.Replace('\'', '\"') });
            var blocked = new VfxCompiler().DryRun(badRecipe.Replace('\'', '\"'), custom);
            Assert.That(blocked.Report.Contains("E500", "/stages/travel/modules/core/parameters/scale"), Is.True, Report(blocked.Report));
        }

        [Test]
        public void FivePerspectiveEvidenceFiles_ArePresentAndDistinct()
        {
            var root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "docs", "s10-evidence"));
            var files = new[] { "front.png", "side.png", "oblique_top.png", "close.png", "game_distance.png" }.Select(name => Path.Combine(root, name)).ToArray();
            Assert.That(files.All(File.Exists), Is.True);
            Assert.That(files.Select(Hash).Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(files.Length), "Each declared perspective must contain a separately rendered image.");
            StringAssert.Contains("hidden-graphics-device batch Camera.Render", File.ReadAllText(Path.Combine(root, "views.json")));
        }

        [Test]
        public void GoldPreviewComposition_UsesThreeInstancesOfTheOfficialGenerated3DPrefab()
        {
            const string previewPath = "Assets/VFX/Preview/S10_3D_FireballPreview.unity";
            // The preview may already be the sole scene after another S10 test.
            // Keep a sandbox scene loaded while inspecting it and only unload a
            // preview scene this test opened, so teardown is order independent.
            var priorActive = SceneManager.GetActiveScene();
            var sandbox = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            EditorSceneManager.SetActiveScene(sandbox);
            var scene = SceneManager.GetSceneByPath(previewPath);
            var openedForTest = !scene.IsValid() || !scene.isLoaded;
            if (openedForTest) scene = EditorSceneManager.OpenScene(previewPath, OpenSceneMode.Additive);
            try
            {
                foreach (var name in new[] { "Launch_Gold", "Travel_Gold", "Impact_Gold" })
                {
                    var instance = scene.GetRootGameObjects().Single(root => root.name == name);
                    var source = PrefabUtility.GetCorrespondingObjectFromSource(instance);
                    Assert.That(source, Is.Not.Null, name + " must be an actual generated Prefab instance.");
                    Assert.That(AssetDatabase.GetAssetPath(source), Is.EqualTo("Assets/VFX/Generated/fireball_3d/VFX_Fireball_3D.prefab"));
                }
                var travel = scene.GetRootGameObjects().Single(root => root.name == "Travel_Gold");
                Assert.That(travel.transform.Find("Travel/Core/Embers").GetComponent<ParticleSystem>(), Is.Not.Null);
                var impact = scene.GetRootGameObjects().Single(root => root.name == "Impact_Gold");
                Assert.That(impact.transform.Find("Impact/Burst").GetComponent<ParticleSystem>(), Is.Not.Null);
                Assert.That(impact.transform.Find("Impact/Shockwave").GetComponent<ParticleSystemRenderer>().mesh, Is.Not.Null);
            }
            finally
            {
                if (openedForTest && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
                if (priorActive.IsValid() && priorActive.isLoaded) EditorSceneManager.SetActiveScene(priorActive);
                if (sandbox.IsValid() && sandbox.isLoaded && SceneManager.sceneCount > 1) EditorSceneManager.CloseScene(sandbox, true);
            }
        }

        [Test]
        public void PerspectivePreview_OpensS10SceneAndAppliesAllFiveDistinctSharedCameraPoses()
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (S10PreviewView view in Enum.GetValues(typeof(S10PreviewView)))
            {
                var camera = S10PreviewScene.ApplyView(view);
                var pose = S10PreviewScene.GetPose(view);
                Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(S10PreviewScene.ScenePath));
                Assert.That(camera.orthographic, Is.False, view.ToString());
                Assert.That(camera.transform.position, Is.EqualTo(pose.Position));
                Assert.That(camera.fieldOfView, Is.EqualTo(pose.FieldOfView));
                Assert.That(seen.Add(pose.Position + "/" + pose.FieldOfView), Is.True, "Each S10 view must use a distinct pose/FOV tuple.");
            }
        }

        [Test]
        public void CompilerPreviewDispatch_UsesS10PerspectiveSceneForAThreeDRecipe()
        {
            var recipe = AssetDatabase.LoadAssetAtPath<TextAsset>(RecipePath);
            string status;
            Assert.That(VfxCompilerWindow.PreviewSelectedRecipe(recipe, out status), Is.True, status);
            StringAssert.Contains("S10 3D perspective", status);
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(S10PreviewScene.ScenePath));
            Assert.That(UnityEngine.Object.FindObjectsOfType<Camera>().Single(camera => camera.name == S10PreviewScene.CameraName).orthographic, Is.False);
        }

        private static string Recipe() { return File.ReadAllText(RecipePath).Replace("\"id\": \"fireball_3d\"", "\"id\": \"fireball_3d_s10test\""); }
        private static void DeleteTestOutput() { if (AssetDatabase.IsValidFolder(Output)) AssetDatabase.DeleteAsset(Output); AssetDatabase.SaveAssets(); }
        private static void AssertReadback(string id, string parameter, GameObject target, float expected)
        {
            if (id == "PFT_3D_FireCore") { Assert.That(target.transform.localScale.x, Is.EqualTo(expected).Within(.0001f)); return; }
            if (id == "PFT_3D_FireTrail") { var trail = target.GetComponent<TrailRenderer>(); Assert.That(parameter == "time" ? trail.time : trail.widthMultiplier, Is.EqualTo(expected).Within(.0001f)); return; }
            var particle = target.GetComponent<ParticleSystem>();
            if (parameter == "rate") { Assert.That(particle.emission.rateOverTime.constant, Is.EqualTo(expected).Within(.0001f)); return; }
            if (parameter == "count") { var bursts = new ParticleSystem.Burst[particle.emission.burstCount]; particle.emission.GetBursts(bursts); Assert.That(bursts[0].count.constant, Is.EqualTo(expected).Within(.0001f)); return; }
            if (parameter == "speed") { Assert.That(particle.main.startSpeed.constant, Is.EqualTo(expected).Within(.0001f)); return; }
            if (parameter == "lifetime") { Assert.That(particle.main.startLifetime.constant, Is.EqualTo(expected).Within(.0001f)); return; }
            if (parameter == "size") { Assert.That(particle.main.startSize.constant, Is.EqualTo(expected).Within(.0001f)); return; }
            var curve = particle.sizeOverLifetime.size.curve; Assert.That(curve.keys[curve.length - 1].value, Is.EqualTo(expected).Within(.0001f));
        }
        private static Dictionary<string, string> Hashes(string assetPath)
        {
            var root = Path.Combine(Application.dataPath, assetPath.Substring("Assets/".Length));
            return Directory.GetFiles(root, "*", SearchOption.AllDirectories).OrderBy(path => path, StringComparer.Ordinal).ToDictionary(path => path.Substring(root.Length).Replace('\\', '/'), Hash, StringComparer.Ordinal);
        }
        private static string Hash(string path) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", string.Empty); }
        private static string Report(ValidationReport report) { return string.Join(" | ", report.Entries.Select(entry => entry.Code + " " + entry.Path + " " + entry.Message)); }
    }
}

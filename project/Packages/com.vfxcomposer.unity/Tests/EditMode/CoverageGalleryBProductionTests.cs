using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using VFXComposer.Editor.Rules;
using VFXComposer.Editor.ValidationGallery;

namespace VFXComposer.Tests.EditMode
{
    public sealed class CoverageGalleryBProductionTests
    {
        [Test]
        public void NineRecipesAndRuntimeEntries_CoverFrozenProfilesAndLifecycle()
        {
            Assert.That(CoverageGalleryBCompiler.Definitions.Length,Is.EqualTo(9));var sustained=0;
            foreach(var definition in CoverageGalleryBCompiler.Definitions)
            {
                var recipe=CoverageGalleryBCompiler.Parse(File.ReadAllText(Absolute(definition.RecipePath)),definition);Assert.That(recipe.Id,Is.EqualTo(definition.Id));Assert.That(recipe.Dimension,Is.EqualTo(definition.Dimension));if(recipe.Sustained)sustained++;
                var path="Assets/VFX/Generated/"+definition.Id+"/VFX_"+definition.Id+".prefab";var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);Assert.That(prefab,Is.Not.Null,definition.Id);var controller=prefab.GetComponent<CoverageGalleryVfxController>();Assert.That(controller,Is.Not.Null);Assert.That(controller.Profile,Is.EqualTo(definition.Profile));Assert.That(controller.Sustained,Is.EqualTo(recipe.Sustained));Assert.That(prefab.GetComponents<MonoBehaviour>().Count(value=>value is IVfxRuntimeEntry),Is.EqualTo(1));Assert.That(prefab.GetComponentsInChildren<Renderer>(true).All(value=>!value.enabled),Is.True,"Idle renderers: "+definition.Id);
                var manifest=JObject.Parse(File.ReadAllText(VfxProjectRules.ManifestAbsolutePath(definition.Id)));Assert.That((string)manifest["runtimeEntry"]["path"],Is.EqualTo(path));Assert.That(((JArray)manifest["ownedOutputs"]).Count,Is.EqualTo(1));
            }
            Assert.That(sustained,Is.EqualTo(7));
        }

        [Test]
        public void SharedAssets_AreSingleDependenciesAndScreenEntryUsesCanvas()
        {
            foreach(var path in new[]{CoverageGalleryBCompiler.QuadPath,CoverageGalleryBCompiler.RingPath,CoverageGalleryBCompiler.BurstPath,CoverageGalleryBCompiler.CloudPath,CoverageGalleryBCompiler.AlphaMaterialPath,CoverageGalleryBCompiler.AdditiveMaterialPath,CoverageGalleryBCompiler.ParticlePrefabPath})Assert.That(AssetDatabase.LoadMainAssetAtPath(path),Is.Not.Null,path);
            Assert.That(AssetDatabase.FindAssets("t:Prefab",new[]{CoverageGalleryBCompiler.SharedRoot+"/Prefabs"}).Length,Is.EqualTo(1),"All coverage profiles must share one serialized ParticleSystem prefab.");
            var screen=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/VFX/Generated/damage_warning_ui/VFX_damage_warning_ui.prefab");Assert.That(screen.GetComponent<Canvas>(),Is.Not.Null);Assert.That(screen.GetComponent<CoverageGalleryVfxController>().Profile,Is.EqualTo(CoverageGalleryProfile.ScreenUi));
            var dependency=JObject.Parse(File.ReadAllText(VfxProjectRules.ManifestAbsolutePath("damage_warning_ui")));Assert.That(((JArray)dependency["dependencies"]).Any(value=>((string)value["path"]).StartsWith("Packages/com.unity.ugui/",StringComparison.Ordinal)),Is.True);
        }

        [Test]
        public void PreviewScene_HasNineIndependentEntriesOneCameraAndPreviewOnlyDriver()
        {
            try{var scene=UnityEditor.SceneManagement.EditorSceneManager.OpenScene(CoverageGalleryBScene.ScenePath,UnityEditor.SceneManagement.OpenSceneMode.Single);var roots=scene.GetRootGameObjects();Assert.That(roots.SelectMany(root=>root.GetComponentsInChildren<Camera>(true)).Count(),Is.EqualTo(1));Assert.That(roots.SelectMany(root=>root.GetComponentsInChildren<CoverageGalleryVfxController>(true)).Count(),Is.EqualTo(9));Assert.That(roots.SelectMany(root=>root.GetComponentsInChildren<CoverageGalleryBPlaybackDriver>(true)).Count(),Is.EqualTo(1));}
            finally{UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,UnityEditor.SceneManagement.NewSceneMode.Single);}
        }

        [Test]
        public void ScreenUiFormalPrefab_IsFullscreenAndHasDedicatedPreview()
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/VFX/Generated/damage_warning_ui/VFX_damage_warning_ui.prefab");var safe=prefab.transform.Find("ScreenFeedbackSafeArea") as RectTransform;Assert.That(safe,Is.Not.Null);Assert.That(safe.anchorMin,Is.EqualTo(Vector2.zero));Assert.That(safe.anchorMax,Is.EqualTo(Vector2.one),"Formal Screen/UI Runtime Entry must not serialize the Gallery cell anchors.");
            try{var scene=UnityEditor.SceneManagement.EditorSceneManager.OpenScene(CoverageGalleryBScene.ScreenUiScenePath,UnityEditor.SceneManagement.OpenSceneMode.Single);var roots=scene.GetRootGameObjects();Assert.That(roots.SelectMany(root=>root.GetComponentsInChildren<Camera>(true)).Count(),Is.EqualTo(1));Assert.That(roots.SelectMany(root=>root.GetComponentsInChildren<CoverageGalleryVfxController>(true)).Single().Profile,Is.EqualTo(CoverageGalleryProfile.ScreenUi));}
            finally{UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,UnityEditor.SceneManagement.NewSceneMode.Single);}
        }

        [Test]
        public void RejectedVisualRoots_AreReplacedByArchetypeSpecificDominantShapes()
        {
            var aura=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/VFX/Generated/astral_aura_3d/VFX_astral_aura_3d.prefab");
            var area=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/VFX/Generated/toxic_field_3d/VFX_toxic_field_3d.prefab");
            var shield=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/VFX/Generated/prismatic_shield_3d/VFX_prismatic_shield_3d.prefab");
            var environment=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/VFX/Generated/snow_weather_volume/VFX_snow_weather_volume.prefab");
            var screen=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/VFX/Generated/damage_warning_ui/VFX_damage_warning_ui.prefab");
            Assert.That(aura.transform.Find("BodyEnvelope"),Is.Not.Null);Assert.That(aura.transform.Find("VerticalWispA"),Is.Not.Null);
            Assert.That(area.transform.Find("GroundPool"),Is.Not.Null);Assert.That(area.transform.Find("Boundary"),Is.Null);
            Assert.That(shield.transform.Find("OuterShell"),Is.Not.Null);Assert.That(shield.transform.Find("Equator"),Is.Null);
            Assert.That(environment.transform.Find("WeatherVolume"),Is.Null,"Environment must not read as another ring archetype.");
            Assert.That(screen.GetComponentInChildren<CoverageScreenFeedbackGraphic>(true),Is.Not.Null,"Screen/UI needs a soft screen-space vignette rather than a hard debug frame.");
        }

        private static string Absolute(string path){return Path.GetFullPath(Path.Combine(Directory.GetParent(Application.dataPath).FullName,path.Replace('/',Path.DirectorySeparatorChar)));}
    }
}

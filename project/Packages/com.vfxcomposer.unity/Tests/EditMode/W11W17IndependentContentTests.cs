using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VFXComposer.Editor.Build;
using VFXComposer.Editor.Domain;
using VFXComposer.Editor.Independent;
using VFXComposer.Editor.Patch;
using VFXComposer.Editor.Rules;
using VFXComposer.Editor.Validation;

namespace VFXComposer.Tests.EditMode
{
    public sealed class W11W17IndependentContentTests
    {
        [Test]
        public void ThirtyRecipes_AreExactLegalAndRejectFixedArrayFamilyAndRangeDrift()
        {
            IndependentContentAuthoring.WriteAllRecipes();AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);var catalog=VfxCompiler.LoadFormalCatalog();Assert.That(IndependentContentCatalog.All.Length,Is.EqualTo(30));foreach(var entry in IndependentContentCatalog.All){var json=File.ReadAllText(Absolute(IndependentContentAuthoring.RecipePath(entry)));var report=RecipeValidator.Validate(json,catalog);Assert.That(report.HasErrors,Is.False,entry.Id+": "+Describe(report));ContentDefinition definition;Assert.That(ContentParameterRegistry.TryGet(entry.Id,out definition),Is.True);var recipe=VfxDomainParser.ParseRecipe(json).Value;Assert.That(recipe.Content.Family,Is.EqualTo(entry.Family));CollectionAssert.AreEquivalent(definition.Parameters.Keys,recipe.Content.Parameters.Keys);}
            var gacha=JObject.Parse(File.ReadAllText(Absolute(IndependentContentAuthoring.RecipePath(IndependentContentCatalog.All.Single(v=>v.Id=="gacha_ten_sequence_ui")))));gacha["content"]["parameters"]["rarities"]=new JArray(1,2);Assert.That(RecipeValidator.Validate(gacha.ToString(),catalog).Contains("E1822","/content/parameters/rarities"),Is.True);var rain=JObject.Parse(File.ReadAllText(Absolute(IndependentContentAuthoring.RecipePath(IndependentContentCatalog.All[0]))));rain["content"]["family"]="screen_ui";Assert.That(RecipeValidator.Validate(rain.ToString(),catalog).Contains("E1820","/content/family"),Is.True);rain=JObject.Parse(File.ReadAllText(Absolute(IndependentContentAuthoring.RecipePath(IndependentContentCatalog.All[0]))));rain["content"]["parameters"]["intensity"]=2;Assert.That(RecipeValidator.Validate(rain.ToString(),catalog).Contains("E1822","/content/parameters/intensity"),Is.True);
        }

        [Test]
        public void ThirtyRuntimeEntries_AreIdempotentStrictAndUseCorrectWorldOrCanvasSemantics()
        {
            IndependentContentAuthoring.BuildEntries();foreach(var entry in IndependentContentCatalog.All){var result=IndependentContentCompiler.BuildAsset(IndependentContentAuthoring.RecipePath(entry));Assert.That(result.Succeeded,Is.True,entry.Id+": "+Describe(result.Report));Assert.That(result.Unchanged,Is.True,entry.Id);var prefabPath="Assets/VFX/Generated/"+entry.Id+"/VFX_"+entry.Id+".prefab";var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);Assert.That(prefab,Is.Not.Null);Assert.That(prefab.GetComponents<MonoBehaviour>().Count(v=>v is IVfxRuntimeEntry),Is.EqualTo(1));var controller=prefab.GetComponent<PlannedContentVfxController>();Assert.That(controller,Is.Not.Null);Assert.That(controller.ContentId,Is.EqualTo(entry.Id));var ui=entry.Family=="screen_ui"||entry.Family=="game_ui";Assert.That(prefab.GetComponentsInChildren<ParticleSystem>(true).Length,Is.EqualTo(ui?0:entry.Family=="environment"?3:1));Assert.That(prefab.GetComponentsInChildren<Canvas>(true).Length,Is.EqualTo(ui?1:0));Assert.That(prefab.GetComponentsInChildren<Graphic>(true).Length,Is.EqualTo(ui?(entry.Id=="gacha_ten_sequence_ui"?8:6):0));if(ui)Assert.That(prefab.GetComponentsInChildren<Graphic>(true).All(v=>v is CoverageScreenFeedbackGraphic),Is.True,entry.Id+" must preserve a center-safe procedural UI mask");if(entry.Id=="lifesteal_link_beam_2d")Assert.That(prefab.GetComponentsInChildren<LineRenderer>(true).Single().positionCount,Is.EqualTo(16));if(entry.Id=="parry_spark_impact_3d"){var collision=prefab.GetComponentInChildren<ParticleSystem>(true).collision;Assert.That(collision.enabled,Is.True);Assert.That(collision.type,Is.EqualTo(ParticleSystemCollisionType.World));Assert.That(collision.bounce.constant,Is.GreaterThan(0));}var manifest=JObject.Parse(File.ReadAllText(VfxProjectRules.ManifestAbsolutePath(entry.Id)));Assert.That((string)manifest["enforcement"],Is.EqualTo("strict"));Assert.That((string)manifest["compilerVersion"],Is.EqualTo(IndependentContentCompiler.CompilerVersion));Assert.That((int)manifest["cost"]["localTextureBytes"],Is.EqualTo(0));Assert.That(((JArray)manifest["ownedOutputs"]).Count,Is.EqualTo(1));if(entry.Family=="environment")Assert.That(controller.ParticleCapacity,Is.LessThanOrEqualTo(160));if(ui)Assert.That(controller.ActiveUiElementCount,Is.LessThanOrEqualTo(entry.Id.StartsWith("gacha_",StringComparison.Ordinal)?48:24));}
        }

        [Test]
        public void ThirtySemanticPatches_UseStableRegisteredContentPaths()
        {
            var service=new VfxPatchService();foreach(var entry in IndependentContentCatalog.All){var recipe=File.ReadAllText(Absolute(IndependentContentAuthoring.RecipePath(entry)));var patchPath=IndependentContentAuthoring.PatchRoot+"/"+entry.Id+".semantic.patch.json";var patch=File.ReadAllText(Absolute(patchPath));var operation=JArray.Parse(patch).Single();Assert.That((string)operation["op"],Is.EqualTo("set_content_param"));Assert.That(((string)operation["path"]).StartsWith("/content/parameters/",StringComparison.Ordinal),Is.True);var result=service.Validate(recipe,patch,1);Assert.That(result.IsValid,Is.True,entry.Id+": "+Describe(result.Report));Assert.That(result.AfterRevision,Is.EqualTo(2));}
        }

        [Test]
        public void IndependentPatchTransaction_RebuildsWithOwningCompilerAndRestoresFixture()
        {
            var entry=IndependentContentCatalog.All.Single(v=>v.Id=="heal_glow_ui");var recipePath=IndependentContentAuthoring.RecipePath(entry);var absolute=Absolute(recipePath);var historyPath=recipePath+VfxPatchService.HistorySuffix;var before=File.ReadAllText(absolute);var beforeHistory=File.Exists(Absolute(historyPath))?File.ReadAllText(Absolute(historyPath)):null;try{var patch=File.ReadAllText(Absolute(IndependentContentAuthoring.PatchRoot+"/"+entry.Id+".semantic.patch.json"));var result=new VfxPatchService().ApplyToAsset(recipePath,patch,1);Assert.That(result.IsValid,Is.True,Describe(result.Report));var after=JObject.Parse(File.ReadAllText(absolute));Assert.That((int)after["revision"],Is.EqualTo(2));var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/VFX/Generated/"+entry.Id+"/VFX_"+entry.Id+".prefab");Assert.That(prefab.GetComponent<PlannedContentVfxController>(),Is.Not.Null);var manifest=JObject.Parse(File.ReadAllText(VfxProjectRules.ManifestAbsolutePath(entry.Id)));Assert.That((string)manifest["compilerVersion"],Is.EqualTo(IndependentContentCompiler.CompilerVersion));}
            finally{File.WriteAllText(absolute,before);AssetDatabase.ImportAsset(recipePath,ImportAssetOptions.ForceUpdate);if(beforeHistory==null){if(AssetDatabase.LoadAssetAtPath<TextAsset>(historyPath)!=null)AssetDatabase.DeleteAsset(historyPath);else if(File.Exists(Absolute(historyPath)))File.Delete(Absolute(historyPath));}else File.WriteAllText(Absolute(historyPath),beforeHistory);var restore=IndependentContentCompiler.BuildAsset(recipePath);Assert.That(restore.Succeeded,Is.True,Describe(restore.Report));}
        }

        [Test]
        public void FourPreviewScenes_HaveExactCellsOneCameraAndExternalOnlyDriver()
        {
            var groups=new[]{new object[]{"environment",7},new object[]{"hit_feedback",7},new object[]{"screen_ui",6},new object[]{"game_ui",10}};foreach(var pair in groups){var group=(string)pair[0];var expected=(int)pair[1];var path=IndependentContentAuthoring.PreviewPath(group);Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(path),Is.Not.Null,group+" scene");var scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Single);var roots=scene.GetRootGameObjects();Assert.That(roots.Count(v=>v.name.StartsWith("Cell_",StringComparison.Ordinal)),Is.EqualTo(expected),group+" cells");Assert.That(roots.SelectMany(v=>v.GetComponentsInChildren<PlannedContentVfxController>(true)).Count(),Is.EqualTo(expected),group+" controllers");Assert.That(roots.SelectMany(v=>v.GetComponentsInChildren<Camera>(true)).Count(),Is.EqualTo(1),group+" camera");Assert.That(roots.SelectMany(v=>v.GetComponentsInChildren<PlannedContentPreviewDriver>(true)).Count(),Is.EqualTo(1),group+" driver");foreach(var controller in roots.SelectMany(v=>v.GetComponentsInChildren<PlannedContentVfxController>(true)))Assert.That(controller.GetComponentInChildren<PlannedContentPreviewDriver>(true),Is.Null,group+" driver leaked into Runtime Entry");}
        }

        private static string Absolute(string assetPath){return Path.GetFullPath(Path.Combine(Directory.GetParent(Application.dataPath).FullName,assetPath.Replace('/',Path.DirectorySeparatorChar)));}
        private static string Describe(ValidationReport report){return string.Join(" | ",report.Entries.Select(v=>v.Code+" "+v.Path+" "+v.Message));}
    }
}

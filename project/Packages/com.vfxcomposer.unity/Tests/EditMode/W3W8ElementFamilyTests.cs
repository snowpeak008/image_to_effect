using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VFXComposer.Editor.Build;
using VFXComposer.Editor.Elements;
using VFXComposer.Editor.Patch;
using VFXComposer.Editor.Rules;
using VFXComposer.Editor.Style;
using VFXComposer.Editor.Validation;

namespace VFXComposer.Tests.EditMode
{
    public sealed class W3W8ElementFamilyTests
    {
        [Test]
        public void FortySevenRecipes_HaveExactRegisteredContentAndRejectFamilyKeyTypeAndRangeDrift()
        {
            ElementFamilyAuthoring.WriteAllRecipes();AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);var catalog=VfxCompiler.LoadFormalCatalog();Assert.That(ElementFamilyCatalog.All.Length,Is.EqualTo(47));Assert.That(ElementFamilyCatalog.All.Count(v=>ContentParameterRegistry.TryGet(v.Id,out _)),Is.EqualTo(47));
            foreach(var entry in ElementFamilyCatalog.All){var path=RecipePath(entry);var json=File.ReadAllText(Absolute(path));var report=RecipeValidator.Validate(json,catalog);Assert.That(report.HasErrors,Is.False,entry.Id+": "+Describe(report));var recipe=VFXComposer.Editor.Domain.VfxDomainParser.ParseRecipe(json).Value;Assert.That(recipe.Content.Family,Is.EqualTo(entry.Family));ContentDefinition definition;Assert.That(ContentParameterRegistry.TryGet(entry.Id,out definition),Is.True);CollectionAssert.AreEquivalent(definition.Parameters.Keys,recipe.Content.Parameters.Keys);}
            foreach(var path in Directory.GetFiles(Absolute(ElementFamilyAuthoring.RecipeRoot),"*.json",SearchOption.AllDirectories).Where(value=>!value.EndsWith(".default.json",StringComparison.OrdinalIgnoreCase))){var report=RecipeValidator.Validate(File.ReadAllText(path),catalog);Assert.That(report.HasErrors,Is.False,path+": "+Describe(report));}
            var source=JObject.Parse(File.ReadAllText(Absolute(RecipePath(ElementFamilyCatalog.All[0]))));source["content"]["family"]="frost";Assert.That(RecipeValidator.Validate(source.ToString(),catalog).Contains("E1820","/content/family"),Is.True);source=JObject.Parse(File.ReadAllText(Absolute(RecipePath(ElementFamilyCatalog.All[0]))));((JObject)source["content"]["parameters"]).Property("arc_width").Remove();Assert.That(RecipeValidator.Validate(source.ToString(),catalog).Contains("E1821","/content/parameters/arc_width"),Is.True);source=JObject.Parse(File.ReadAllText(Absolute(RecipePath(ElementFamilyCatalog.All[0]))));source["content"]["parameters"]["spark_count"]=99;Assert.That(RecipeValidator.Validate(source.ToString(),catalog).Contains("E1822","/content/parameters/spark_count"),Is.True);source["content"]["parameters"]["unknown"]=1;Assert.That(RecipeValidator.Validate(source.ToString(),catalog).Contains("E1821","/content/parameters/unknown"),Is.True);
        }

        [Test]
        public void FortySevenRuntimeEntries_AreStrictSharedAndIdempotentWithBehaviorAndContentProtocols()
        {
            ElementFamilyAuthoring.BuildEntries();foreach(var entry in ElementFamilyCatalog.All){var result=StyledContentCompiler.BuildAsset(RecipePath(entry));Assert.That(result.Succeeded,Is.True,entry.Id+": "+Describe(result.Report));Assert.That(result.Unchanged,Is.True,entry.Id+" second build must be unchanged");var prefabPath="Assets/VFX/Generated/"+entry.Id+"/VFX_"+entry.Id+".prefab";var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);Assert.That(prefab,Is.Not.Null,prefabPath);Assert.That(prefab.GetComponents<MonoBehaviour>().Count(v=>v is IVfxRuntimeEntry),Is.EqualTo(1));var controller=prefab.GetComponent<StyledVfxController>();Assert.That(controller,Is.Not.Null);Assert.That(controller.ContentFamily.ToString().ToLowerInvariant(),Is.EqualTo(entry.Family));var behavior=JObject.Parse(entry.BehaviorJson.Replace(":.",":0."));Assert.That(controller.MotionType,Is.EqualTo((string)behavior["motion"]?["type"]??"stationary"));Assert.That(controller.TimingType,Is.EqualTo((string)behavior["timing"]?["type"]??"instant"));Assert.That(prefab.GetComponentsInChildren<ParticleSystem>(true).Length,Is.EqualTo(1));var manifest=JObject.Parse(File.ReadAllText(VfxProjectRules.ManifestAbsolutePath(entry.Id)));Assert.That((string)manifest["enforcement"],Is.EqualTo("strict"));Assert.That((int)manifest["cost"]["localTextureBytes"],Is.EqualTo(0));Assert.That(((JArray)manifest["ownedOutputs"]).Count,Is.EqualTo(1));Assert.That(((JArray)manifest["dependencies"]).Any(v=>((string)v["path"]).StartsWith("Assets/VFX/Shared/Styles/",StringComparison.Ordinal)),Is.True);}
        }

        [Test]
        public void FortySevenSemanticPatches_UseStableContentPathsAndValidateAgainstLiveRegistry()
        {
            ElementFamilyAuthoring.WriteAllRecipes();AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);var service=new VfxPatchService();foreach(var entry in ElementFamilyCatalog.All){var recipe=File.ReadAllText(Absolute(RecipePath(entry)));var patch=File.ReadAllText(Absolute(ElementFamilyAuthoring.PatchRoot+"/"+entry.Id+".semantic.patch.json"));var operations=JArray.Parse(patch);Assert.That(operations.Count,Is.EqualTo(1));Assert.That((string)operations[0]["op"],Is.EqualTo("set_content_param"));Assert.That(((string)operations[0]["path"]).StartsWith("/content/parameters/",StringComparison.Ordinal),Is.True);var result=service.Validate(recipe,patch,1);Assert.That(result.IsValid,Is.True,entry.Id+": "+Describe(result.Report));Assert.That(result.AfterRevision,Is.EqualTo(2));Assert.That(result.BeforeCanonicalHash,Is.Not.EqualTo(result.AfterCanonicalHash));}
            var invalid=service.Validate(File.ReadAllText(Absolute(RecipePath(ElementFamilyCatalog.All[0]))),"[{\"op\":\"set_content_param\",\"path\":\"/content/parameters/not_registered\",\"value\":1}]",1);Assert.That(invalid.IsValid,Is.False);Assert.That(invalid.Report.Contains("E706","/content/parameters/not_registered"),Is.True);
        }

        [Test]
        public void SixPreviewScenes_HaveStableCellsOneSerializedCameraAndOnlyPreviewDriverOutsideEntries()
        {
            var groups=new[]{new object[]{"fire",8},new object[]{"frost",7},new object[]{"lightning",7},new object[]{"water_wind",8},new object[]{"earth_nature_toxic",8},new object[]{"magic",9}};foreach(var pair in groups){var group=(string)pair[0];var expected=(int)pair[1];var path=ElementFamilyAuthoring.PreviewPath(group);Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(path),Is.Not.Null,path);var scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Single);var roots=scene.GetRootGameObjects();Assert.That(roots.Count(v=>v.name.StartsWith("Cell_",StringComparison.Ordinal)),Is.EqualTo(expected),group);Assert.That(roots.SelectMany(v=>v.GetComponentsInChildren<StyledVfxController>(true)).Count(),Is.EqualTo(expected),group);Assert.That(roots.SelectMany(v=>v.GetComponentsInChildren<Camera>(true)).Count(),Is.EqualTo(1),group);Assert.That(roots.SelectMany(v=>v.GetComponentsInChildren<ElementFamilyPreviewDriver>(true)).Count(),Is.EqualTo(1),group);foreach(var controller in roots.SelectMany(v=>v.GetComponentsInChildren<StyledVfxController>(true)))Assert.That(controller.GetComponent<ElementFamilyPreviewDriver>(),Is.Null,"Preview driver must never be inside Runtime Entry.");}
        }

        private static string RecipePath(ElementContentEntry entry){var folder=entry.Family=="water"||entry.Family=="wind"?"WaterWind":entry.Family=="earth"||entry.Family=="nature"||entry.Family=="toxic"?"EarthNatureToxic":entry.Family=="holy"||entry.Family=="shadow"||entry.Family=="arcane"?"Magic":char.ToUpperInvariant(entry.Family[0])+entry.Family.Substring(1);return ElementFamilyAuthoring.RecipeRoot+"/"+folder+"/"+entry.Id+".default.json";}
        private static string Describe(VFXComposer.Editor.Domain.ValidationReport report){return string.Join(" | ",report.Entries.Select(v=>v.Code+" "+v.Path+" "+v.Message));}
        private static string Absolute(string assetPath){return Path.GetFullPath(Path.Combine(Directory.GetParent(Application.dataPath).FullName,assetPath.Replace('/',Path.DirectorySeparatorChar)));}
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VFXComposer.Editor.Build;
using VFXComposer.Editor.Composite;
using VFXComposer.Editor.Domain;
using VFXComposer.Editor.Patch;
using VFXComposer.Editor.Rules;
using VFXComposer.Editor.Validation;

namespace VFXComposer.Tests.EditMode
{
    public sealed class W13W18CompositeAndHeroKitTests
    {
        [OneTimeSetUp] public void BuildPlannedDelivery(){CompositeAndHeroKitAuthoring.BuildAll();}

        [Test] public void CompositeSchema_AcceptsPlansAndRejectsSixInvalidContractShapes()
        {
            var catalog=VfxCompiler.LoadFormalCatalog();foreach(var entry in CompositeAndHeroKitCatalog.AllComposites){var json=File.ReadAllText(Absolute(CompositeAndHeroKitAuthoring.CompositeRecipePath(entry)));Assert.That(RecipeValidator.Validate(json,catalog).HasErrors,Is.False,entry.Id);}
            var source=JObject.Parse(File.ReadAllText(Absolute(CompositeAndHeroKitAuthoring.CompositeRecipePath(CompositeAndHeroKitCatalog.Ultimates[0]))));
            var nonComposite=(JObject)source.DeepClone();nonComposite["archetype"]="impact";AssertCode(nonComposite,"E1850");
            var missing=(JObject)source.DeepClone();missing.Remove("timeline");AssertCode(missing,"E1851");
            var action=(JObject)source.DeepClone();action["timeline"][0]["action"]="explode";AssertCode(action,"E1852");
            var over=(JObject)source.DeepClone();over["timeline"][0]["overrides"]["raw_texture"]="forbidden";AssertCode(over,"E1853");
            var hint=(JObject)source.DeepClone();hint["camera_hints"][0]["type"]="own_camera";AssertCode(hint,"E1854");
            var gate=JObject.Parse(File.ReadAllText(Absolute(CompositeAndHeroKitAuthoring.CompositeRecipePath(CompositeAndHeroKitCatalog.Ultimates[4]))));gate["gates"][1]["wait_for"]=(string)gate["gates"][0]["wait_for"];AssertCode(gate,"E1855");
        }

        [Test] public void PlannedDelivery_HasNineteenStrictEntriesPatchesAndIdempotentBytes()
        {
            var ids=CompositeAndHeroKitCatalog.Exclusives.Select(v=>v.Id).Concat(CompositeAndHeroKitCatalog.AllComposites.Select(v=>v.Id)).OrderBy(v=>v,StringComparer.Ordinal).ToArray();Assert.That(ids.Length,Is.EqualTo(19));var before=Snapshot(ids);var guids=ids.ToDictionary(v=>v,v=>AssetDatabase.AssetPathToGUID(Prefab(v)),StringComparer.Ordinal);CompositeAndHeroKitAuthoring.BuildAll();var after=Snapshot(ids);CollectionAssert.AreEquivalent(before,after);foreach(var id in ids){Assert.That(AssetDatabase.AssetPathToGUID(Prefab(id)),Is.EqualTo(guids[id]),id);var patchText=File.ReadAllText(Absolute("Assets/VFX/Recipes/Patches/"+id+".semantic.patch.json"));var patch=JArray.Parse(patchText);Assert.That(patch.Count,Is.EqualTo(1),id);var recipe=CompositeAndHeroKitCatalog.Exclusives.FirstOrDefault(v=>v.Id==id);var recipePath=recipe!=null?CompositeAndHeroKitAuthoring.ExclusiveRecipePath(recipe):CompositeAndHeroKitAuthoring.CompositeRecipePath(CompositeAndHeroKitCatalog.AllComposites.Single(v=>v.Id==id));var validation=new VfxPatchService().Validate(File.ReadAllText(Absolute(recipePath)),patchText,1);Assert.That(validation.IsValid,Is.True,id+": "+string.Join(" | ",validation.Report.Entries.Select(v=>v.Code+" "+v.Path+" "+v.Message)));var manifest=Manifest(id);Assert.That((string)manifest["enforcement"],Is.EqualTo("strict"),id);Assert.That((long)manifest.SelectToken("cost.localTextureBytes"),Is.EqualTo(0),id);}
        }

        [Test] public void Composites_AreDependencyOnlyAndStayInsideRelaxedPeakBudget()
        {
            foreach(var entry in CompositeAndHeroKitCatalog.AllComposites){var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Prefab(entry.Id));Assert.That(prefab.transform.childCount,Is.EqualTo(0),entry.Id+" must not embed copied child hierarchies");Assert.That(prefab.GetComponents<MonoBehaviour>().Count(v=>v is IVfxRuntimeEntry),Is.EqualTo(1));var manifest=Manifest(entry.Id);var owned=(JArray)manifest["ownedOutputs"];Assert.That(owned.Count,Is.EqualTo(2),entry.Id);Assert.That(owned.Any(v=>(string)v["path"]=="Assets/VFX/Generated/"+entry.Id+"/Composition.json"),Is.True);var deps=(JArray)manifest["dependencies"];foreach(var refId in entry.Timeline.Select(v=>(string)v["ref_id"]).Distinct())Assert.That(deps.Any(v=>(string)v["path"]==RefPrefab(refId)),Is.True,entry.Id+" -> "+refId);var peak=(JObject)manifest["compositePeakBudget"];Assert.That((int)peak["particles"],Is.LessThanOrEqualTo(200),entry.Id);Assert.That((int)peak["particleSystems"],Is.LessThanOrEqualTo(10),entry.Id);Assert.That((int)peak["materials"],Is.LessThanOrEqualTo(10),entry.Id);Assert.That((int)peak["renderers"],Is.LessThanOrEqualTo(14),entry.Id);}
            var blade=Manifest("blade_tempest_ultimate_3d");var dependencies=(JArray)blade["dependencies"];Assert.That(dependencies.Count(v=>(string)v["path"]==RefPrefab("slash_3d_stylized")),Is.EqualTo(1),"Eight timeline instances must still be one dependency record.");
        }

        [Test] public void HeroKits_HaveOneThemePaletteAndOnlyRegisteredReferences()
        {
            foreach(var kit in CompositeAndHeroKitCatalog.HeroKits){var descriptor=JObject.Parse(File.ReadAllText(Absolute("Assets/VFX/Generated/"+kit.Id+"/Composition.json")));Assert.That((string)descriptor["theme"],Is.EqualTo(kit.ThemeId));Assert.That((string)descriptor["shapeLanguage"],Is.EqualTo(kit.ShapeLanguage));var expected=new[]{kit.Primary,kit.Secondary,kit.Accent};CollectionAssert.AreEqual(expected,new[]{(string)descriptor.SelectToken("palette.primary"),(string)descriptor.SelectToken("palette.secondary"),(string)descriptor.SelectToken("palette.accent")});foreach(var item in ((JArray)descriptor["timeline"]).Where(v=>(string)v["action"]=="play")){Assert.That((string)item.SelectToken("overrides.palette"),Is.EqualTo(kit.ThemeId));Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(RefPrefab((string)item["ref_id"])),Is.Not.Null);}}
            Assert.That(CompositeAndHeroKitCatalog.Exclusives.Length,Is.EqualTo(8));Assert.That(CompositeAndHeroKitCatalog.Exclusives.Count(v=>v.Id.Contains("idle")),Is.EqualTo(3));
        }

        [Test] public void PreviewScenes_HaveOneSerializedCameraAndSelectorOnly()
        {
            var sandbox=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);try{foreach(var group in new[]{"ultimate","hero_kit"}){var scene=EditorSceneManager.OpenScene(CompositeAndHeroKitAuthoring.PreviewPath(group),OpenSceneMode.Additive);try{var roots=scene.GetRootGameObjects();Assert.That(roots.SelectMany(v=>v.GetComponentsInChildren<Camera>(true)).Count(),Is.EqualTo(1));Assert.That(roots.SelectMany(v=>v.GetComponentsInChildren<CompositePreviewDriver>(true)).Count(),Is.EqualTo(1));var expected=group=="ultimate"?6:4;Assert.That(roots.SelectMany(v=>v.GetComponentsInChildren<CompositeVfxController>(true)).Count(),Is.EqualTo(expected));}finally{EditorSceneManager.CloseScene(scene,true);}}}finally{if(sandbox.IsValid())EditorSceneManager.CloseScene(sandbox,true);}
        }

        [Test] public void Manifests_RecordTimelineCameraHintsGatesAndDescriptorOwnership()
        {
            foreach(var entry in CompositeAndHeroKitCatalog.AllComposites){var manifest=Manifest(entry.Id);Assert.That(((JArray)manifest["timeline"]).Count,Is.EqualTo(entry.Timeline.Count));Assert.That(((JArray)manifest["cameraHints"]).Count,Is.EqualTo(entry.CameraHints.Count));Assert.That(((JArray)manifest["gates"]).Count,Is.EqualTo(entry.Gates.Count));Assert.That((string)manifest["compilerVersion"],Is.EqualTo(CompositeContentCompiler.CompilerVersion));}
            var demon=Manifest("demon_gate_boss_3d");CollectionAssert.AreEqual(new[]{"gate_formed","hand_release"},((JArray)demon["gates"]).Select(v=>(string)v["wait_for"]).ToArray());
        }

        private static void AssertCode(JObject recipe,string code){var report=RecipeValidator.Validate(recipe.ToString(),VfxCompiler.LoadFormalCatalog());Assert.That(report.Entries.Any(v=>v.Code==code),Is.True,string.Join(" | ",report.Entries.Select(v=>v.Code+" "+v.Path)));}
        private static Dictionary<string,string> Snapshot(IEnumerable<string> ids){var result=new Dictionary<string,string>(StringComparer.Ordinal);foreach(var id in ids)foreach(var file in Directory.GetFiles(Absolute("Assets/VFX/Generated/"+id),"*",SearchOption.AllDirectories).Where(v=>!v.EndsWith(".meta",StringComparison.OrdinalIgnoreCase)).OrderBy(v=>v,StringComparer.Ordinal))result[id+"/"+Path.GetFileName(file)]=Sha(file);return result;}
        private static JObject Manifest(string id){return JObject.Parse(File.ReadAllText(VfxProjectRules.ManifestAbsolutePath(id)));}private static string Prefab(string id){return "Assets/VFX/Generated/"+id+"/VFX_"+id+".prefab";}private static string RefPrefab(string id){return(string)Manifest(id).SelectToken("runtimeEntry.path");}private static string Absolute(string asset){return Path.GetFullPath(Path.Combine(Directory.GetParent(Application.dataPath).FullName,asset.Replace('/',Path.DirectorySeparatorChar)));}private static string Sha(string file){using(var s=File.OpenRead(file))using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(s)).Replace("-","");}
    }
}

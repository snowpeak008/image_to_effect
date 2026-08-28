using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VFXComposer.Editor.Archetypes;
using VFXComposer.Editor.Build;
using VFXComposer.Editor.Domain;
using VFXComposer.Editor.Patch;
using VFXComposer.Editor.Rules;
using VFXComposer.Editor.Style;
using VFXComposer.Editor.Validation;

namespace VFXComposer.Tests.EditMode
{
    public sealed class W15NewArchetypeTests
    {
        [Test]
        public void SixArchetypes_ParseWithExactTypedParameterContractsAndRejectInvalidValues()
        {
            W15NewArchetypeAuthoring.EnsureRecipes();var catalog=VfxCompiler.LoadFormalCatalog();var archetypes=W15NewArchetypeAuthoring.Definitions.Select(value=>value.Archetype).Distinct().OrderBy(value=>value,StringComparer.Ordinal).ToArray();CollectionAssert.AreEqual(new[]{"decal","destruction","lifecycle","loot","portal","weapon_trail"},archetypes);
            foreach(var path in W15NewArchetypeAuthoring.RecipePaths){var json=File.ReadAllText(Absolute(path));var parsed=VfxDomainParser.ParseRecipe(json);Assert.That(parsed.Report.HasErrors,Is.False,path);Assert.That(parsed.Value.ArchetypeParameters.Count,Is.EqualTo(3),path);Assert.That(ArchetypeParameterRegistry.For(parsed.Value.Archetype).Count,Is.EqualTo(3),path);Assert.That(RecipeValidator.Validate(json,catalog).HasErrors,Is.False,path);}
            var decal=JObject.Parse(File.ReadAllText(Absolute(W15NewArchetypeAuthoring.RecipeRoot+"/scorch_decal_3d.default.json")));decal["archetypeParameters"]["stack_limit"]=99;Assert.That(RecipeValidator.Validate(decal.ToString(),catalog).Contains("E1811","/archetypeParameters/stack_limit"),Is.True);decal["archetypeParameters"].Parent.Remove();Assert.That(RecipeValidator.Validate(decal.ToString(),catalog).Contains("E1812","/archetypeParameters/size"),Is.True);
            var weapon=JObject.Parse(File.ReadAllText(Absolute(W15NewArchetypeAuthoring.RecipeRoot+"/katana_trail_weapon_3d.default.json")));weapon["archetypeParameters"]["unexpected"]=1;Assert.That(RecipeValidator.Validate(weapon.ToString(),catalog).Contains("E1810","/archetypeParameters/unexpected"),Is.True);
        }

        [Test]
        public void TenEntries_BuildStrictIdempotentSharedOutputsWithCorrectRuntimeProtocols()
        {
            W15NewArchetypeAuthoring.BuildAll();var first=W15NewArchetypeAuthoring.Definitions.Select(PathInfo).ToArray();W15NewArchetypeAuthoring.BuildAll();CollectionAssert.AreEqual(first,W15NewArchetypeAuthoring.Definitions.Select(PathInfo).ToArray());
            foreach(var definition in W15NewArchetypeAuthoring.Definitions)
            {
                var prefabPath="Assets/VFX/Generated/"+definition.Id+"/VFX_"+definition.Id+".prefab";var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);Assert.That(prefab,Is.Not.Null,prefabPath);var controller=prefab.GetComponent<StyledVfxController>();Assert.That(controller,Is.Not.Null);Assert.That(prefab.GetComponents<MonoBehaviour>().Count(value=>value is IVfxRuntimeEntry),Is.EqualTo(1));Assert.That(controller.Profile,Is.EqualTo(Profile(definition.Archetype)));Assert.That(prefab.GetComponentsInChildren<Rigidbody>(true),Is.Empty,"Destruction is deterministic fake physics, never Rigidbody-driven.");Assert.That(prefab.GetComponentsInChildren<ParticleSystem>(true).Length,Is.EqualTo(1));
                if(definition.Archetype=="destruction")Assert.That(prefab.transform.Find("Layer_10"),Is.Not.Null);if(definition.Archetype=="weapon_trail"){Assert.That(prefab.GetComponentInChildren<TrailRenderer>(true),Is.Not.Null);Assert.That(prefab.GetComponentInChildren<LineRenderer>(true),Is.Not.Null);}if(definition.Archetype=="portal")Assert.That(controller.PairId,Is.EqualTo("twin_portal_default"));
                var manifest=JObject.Parse(File.ReadAllText(VfxProjectRules.ManifestAbsolutePath(definition.Id)));Assert.That((string)manifest["enforcement"],Is.EqualTo("strict"));Assert.That((string)manifest["archetype"],Is.EqualTo(definition.Archetype));Assert.That((int)manifest["cost"]["localTextureBytes"],Is.EqualTo(0));Assert.That(((JArray)manifest["ownedOutputs"]).Count,Is.EqualTo(1));Assert.That(((JArray)manifest["dependencies"]).Any(value=>((string)value["path"]).StartsWith("Assets/VFX/Shared/Styles/",StringComparison.Ordinal)),Is.True);
            }
        }

        [Test]
        public void TenSemanticPatches_ValidateAndStyledPatchTransactionRebuildsTheProtocolPrefab()
        {
            W15NewArchetypeAuthoring.BuildAll();var service=new VfxPatchService();foreach(var definition in W15NewArchetypeAuthoring.Definitions){var recipe=File.ReadAllText(Absolute(W15NewArchetypeAuthoring.RecipeRoot+"/"+definition.Id+".default.json"));var patch=File.ReadAllText(Absolute("Assets/VFX/Recipes/Patches/"+definition.Id+".semantic.patch.json"));Assert.That(JToken.Parse(patch),Is.TypeOf<JArray>());var result=service.Validate(recipe,patch,1);Assert.That(result.IsValid,Is.True,definition.Id+": "+Describe(result));}
            const string tempRecipe="Assets/VFX/Recipes/NewArchetypes/w15_patch_transaction_temp.default.json";const string tempOutput="Assets/VFX/Generated/w15_patch_transaction_temp";try{var source=JObject.Parse(File.ReadAllText(Absolute(W15NewArchetypeAuthoring.RecipeRoot+"/loot_beam_pickup_3d.default.json")));source["id"]="w15_patch_transaction_temp";source["name"]="W15 Patch Transaction Temp";File.WriteAllText(Absolute(tempRecipe),source.ToString());AssetDatabase.ImportAsset(tempRecipe,ImportAssetOptions.ForceUpdate);Assert.That(StyledContentCompiler.BuildAsset(tempRecipe).Succeeded,Is.True);var result=service.ApplyToAsset(tempRecipe,"[{\"op\":\"set_archetype_param\",\"path\":\"/archetypeParameters/rarity\",\"value\":5}]",1);Assert.That(result.IsValid,Is.True,Describe(result));var after=JObject.Parse(File.ReadAllText(Absolute(tempRecipe)));Assert.That((int)after["revision"],Is.EqualTo(2));Assert.That((int)after["archetypeParameters"]["rarity"],Is.EqualTo(5));Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(tempOutput+"/VFX_w15_patch_transaction_temp.prefab").GetComponent<StyledVfxController>().Rarity,Is.EqualTo(5));var history=JArray.Parse(File.ReadAllText(Absolute(tempRecipe+VfxPatchService.HistorySuffix)));Assert.That((int)history[0]["beforeRevision"],Is.EqualTo(1));Assert.That((int)history[0]["afterRevision"],Is.EqualTo(2));}
            finally{DeleteAsset(tempOutput);DeleteAsset(tempRecipe);DeleteAsset(tempRecipe+VfxPatchService.HistorySuffix);var manifest=VfxProjectRules.ManifestAbsolutePath("w15_patch_transaction_temp");if(File.Exists(manifest))File.Delete(manifest);AssetDatabase.SaveAssets();AssetDatabase.Refresh();}
        }

        [Test]
        public void PreviewScene_HasTenStableCellsOneCameraAndProtocolSpecificComparisons()
        {
            W15NewArchetypeAuthoring.BuildAll();var scene=EditorSceneManager.OpenScene(W15NewArchetypeAuthoring.PreviewScenePath,OpenSceneMode.Additive);try{var roots=scene.GetRootGameObjects();Assert.That(roots.Count(value=>value.name.StartsWith("Cell_",StringComparison.Ordinal)),Is.EqualTo(10));Assert.That(roots.SelectMany(value=>value.GetComponentsInChildren<StyledVfxController>(true)).Count(),Is.EqualTo(15),"10 content cells include 2 portal roles and 5 loot rarities.");Assert.That(roots.SelectMany(value=>value.GetComponentsInChildren<Camera>(true)).Count(),Is.EqualTo(1));Assert.That(roots.SelectMany(value=>value.GetComponentsInChildren<NewArchetypePreviewDriver>(true)).Count(),Is.EqualTo(1));}finally{EditorSceneManager.CloseScene(scene,true);}
        }

        private static string PathInfo(W15NewArchetypeAuthoring.Definition definition){var prefab="Assets/VFX/Generated/"+definition.Id+"/VFX_"+definition.Id+".prefab";return AssetDatabase.AssetPathToGUID(prefab)+"|"+(string)JObject.Parse(File.ReadAllText(VfxProjectRules.ManifestAbsolutePath(definition.Id)))["buildHash"];}
        private static StyledVfxProfile Profile(string value){switch(value){case "decal":return StyledVfxProfile.Decal;case "weapon_trail":return StyledVfxProfile.WeaponTrail;case "destruction":return StyledVfxProfile.Destruction;case "lifecycle":return StyledVfxProfile.DeathRebirth;case "portal":return StyledVfxProfile.Teleport;default:return StyledVfxProfile.Loot;}}
        private static string Describe(VfxPatchResult result){return string.Join(" | ",result.Report.Entries.Select(value=>value.Code+" "+value.Path+" "+value.Message));}
        private static void DeleteAsset(string path){if(AssetDatabase.LoadMainAssetAtPath(path)!=null||AssetDatabase.IsValidFolder(path))AssetDatabase.DeleteAsset(path);else if(File.Exists(Absolute(path)))File.Delete(Absolute(path));}
        private static string Absolute(string assetPath){return Path.GetFullPath(Path.Combine(Directory.GetParent(Application.dataPath).FullName,assetPath.Replace('/',Path.DirectorySeparatorChar)));}
    }
}

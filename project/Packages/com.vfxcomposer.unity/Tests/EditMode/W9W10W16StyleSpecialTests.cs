using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using VFXComposer.Editor.Build;
using VFXComposer.Editor.Domain;
using VFXComposer.Editor.Patch;
using VFXComposer.Editor.Rules;
using VFXComposer.Editor.Style;
using VFXComposer.Editor.Validation;

namespace VFXComposer.Tests.EditMode
{
    public sealed class W9W10W16StyleSpecialTests
    {
        [Test]
        public void ThirtyTwoRecipes_AndFourteenStyleTokens_AreExactAndLegal()
        {
            StyleSpecialAuthoring.WriteAllRecipes();AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);Assert.That(StyleSpecialCatalog.All.Length,Is.EqualTo(32));Assert.That(StyleSpecialCatalog.Group("style2d").Count(),Is.EqualTo(10));Assert.That(StyleSpecialCatalog.Group("style3d").Count(),Is.EqualTo(10));Assert.That(StyleSpecialCatalog.Group("pack2").Count(),Is.EqualTo(12));CollectionAssert.AreEquivalent(new[]{"stylized","cartoon","pixel","inkwash","semireal","holo","dark","neon","lowpoly","crystal","candy","cosmic","steampunk","ghost"},VfxStyleRegistry.All.Select(v=>v.Token));var catalog=VfxCompiler.LoadFormalCatalog();foreach(var entry in StyleSpecialCatalog.All){var report=RecipeValidator.Validate(File.ReadAllText(Absolute(StyleSpecialAuthoring.RecipePath(entry))),catalog);Assert.That(report.HasErrors,Is.False,entry.Id+": "+Describe(report));}
        }

        [Test]
        public void PipelineContracts_RequireDeclaredTypesAndCarryPlanSpecificParameters()
        {
            var byId=StyleSpecialCatalog.All.ToDictionary(v=>v.Id,StringComparer.Ordinal);var pixel=JObject.Parse(File.ReadAllText(Absolute(StyleSpecialAuthoring.RecipePath(byId["pixel_burst_impact_2d"]))));Assert.That((int)pixel["style"]["snap_fps"],Is.EqualTo(12));Assert.That((int)pixel["style"]["virtual_res"],Is.EqualTo(96));var anime=JObject.Parse(File.ReadAllText(Absolute(StyleSpecialAuthoring.RecipePath(byId["anime_smear_slash_2d"]))));Assert.That((string)anime["style"]["atlas_id"],Is.EqualTo("AnimeSmearAtlas"));var semireal=JObject.Parse(File.ReadAllText(Absolute(StyleSpecialAuthoring.RecipePath(byId["real_explosion_impact_3d"]))));Assert.That((double)semireal["style"]["noise_primary_speed"],Is.EqualTo(.3).Within(.001));Assert.That((double)semireal["style"]["noise_detail_speed"],Is.EqualTo(1.7).Within(.001));var invalid=(JObject)pixel.DeepClone();invalid["style"]["virtual_res"]="large";var report=RecipeValidator.Validate(invalid.ToString(),VfxCompiler.LoadFormalCatalog());Assert.That(report.Contains("E327","/style/virtual_res"),Is.True,Describe(report));
        }

        [Test]
        public void ThirtyTwoRuntimeEntries_AreStrictIdempotentAndDependOnSharedStyleAssets()
        {
            StyleSpecialAuthoring.BuildEntries();foreach(var entry in StyleSpecialCatalog.All){var result=StyledContentCompiler.BuildAsset(StyleSpecialAuthoring.RecipePath(entry));Assert.That(result.Succeeded,Is.True,entry.Id+": "+Describe(result.Report));Assert.That(result.Unchanged,Is.True,entry.Id);var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/VFX/Generated/"+entry.Id+"/VFX_"+entry.Id+".prefab");Assert.That(prefab,Is.Not.Null);var controller=prefab.GetComponent<StyledVfxController>();Assert.That(controller,Is.Not.Null);Assert.That(controller.StyleToken,Is.EqualTo(entry.Style));Assert.That(prefab.GetComponents<MonoBehaviour>().Count(v=>v is IVfxRuntimeEntry),Is.EqualTo(1));var manifest=JObject.Parse(File.ReadAllText(VfxProjectRules.ManifestAbsolutePath(entry.Id)));Assert.That((string)manifest["compilerVersion"],Is.EqualTo(StyledContentCompiler.CompilerVersion));Assert.That((string)manifest["enforcement"],Is.EqualTo("strict"));Assert.That((int)manifest["cost"]["localTextureBytes"],Is.EqualTo(0));Assert.That(((JArray)manifest["dependencies"]).Any(v=>((string)v["path"]).StartsWith("Assets/VFX/Shared/Styles/",StringComparison.Ordinal)),Is.True);}
        }

        [Test]
        public void ThirtyTwoPatches_ValidateAndVariantsNeverMutateBaseRecipeOrManifest()
        {
            var service=new VfxPatchService();foreach(var entry in StyleSpecialCatalog.All){var recipe=File.ReadAllText(Absolute(StyleSpecialAuthoring.RecipePath(entry)));var patch=File.ReadAllText(Absolute(StyleSpecialAuthoring.PatchRoot+"/"+entry.Id+".semantic.patch.json"));var result=service.Validate(recipe,patch,1);Assert.That(result.IsValid,Is.True,entry.Id+": "+Describe(result.Report));}
            foreach(var entry in StyleSpecialCatalog.All.Where(v=>!string.IsNullOrEmpty(v.BaseId))){var baseRecipe=FindBaseRecipe(entry.BaseId);if(baseRecipe==null)continue;var before=File.ReadAllBytes(baseRecipe);var manifest=VfxProjectRules.ManifestAbsolutePath(entry.BaseId);var beforeManifest=File.Exists(manifest)?File.ReadAllBytes(manifest):null;Assert.That(StyledContentCompiler.BuildAsset(StyleSpecialAuthoring.RecipePath(entry)).Succeeded,Is.True);CollectionAssert.AreEqual(before,File.ReadAllBytes(baseRecipe),entry.Id+" mutated base Recipe");if(beforeManifest!=null)CollectionAssert.AreEqual(beforeManifest,File.ReadAllBytes(manifest),entry.Id+" mutated base Manifest");}
        }

        [Test]
        public void ThreePreviewScenes_HaveExactCellsAndPlannedViewpoints()
        {
            foreach(var pair in new[]{new object[]{"style2d",10,1},new object[]{"style3d",10,2},new object[]{"pack2",12,1}}){var group=(string)pair[0];var scene=EditorSceneManager.OpenScene(StyleSpecialAuthoring.PreviewPath(group),OpenSceneMode.Single);var roots=scene.GetRootGameObjects();Assert.That(roots.Count(v=>v.name.StartsWith("Cell_",StringComparison.Ordinal)),Is.EqualTo((int)pair[1]));Assert.That(roots.SelectMany(v=>v.GetComponentsInChildren<StyledVfxController>(true)).Count(),Is.EqualTo((int)pair[1]));Assert.That(roots.SelectMany(v=>v.GetComponentsInChildren<Camera>(true)).Count(),Is.EqualTo((int)pair[2]));Assert.That(roots.SelectMany(v=>v.GetComponentsInChildren<ElementFamilyPreviewDriver>(true)).Count(),Is.EqualTo(1));}
        }

        [Test]
        public void SharedPackTwoAssets_AreReusableAndSmallSourceTextures()
        {
            VfxStyleSharedLibrary.EnsureAll();foreach(var path in VfxStyleSharedLibrary.FacetPaths.Concat(VfxStyleSharedLibrary.GearPaths))Assert.That(AssetDatabase.LoadAssetAtPath<Mesh>(path),Is.Not.Null,path);foreach(var name in new[]{"T_AnimeSmearAtlas_256.png","T_SymbolAtlas_128.png","T_NebulaA_64.png","T_NebulaB_64.png","T_StarAtlas_64.png"}){var path=VfxStyleSharedLibrary.TextureRoot+"/"+name;Assert.That(AssetDatabase.LoadAssetAtPath<Texture2D>(path),Is.Not.Null,path);Assert.That(new FileInfo(Absolute(path)).Length,Is.LessThan(256*1024),path);}
        }

        private static string FindBaseRecipe(string id){return Directory.GetFiles(Absolute("Assets/VFX/Recipes"),"*.json",SearchOption.AllDirectories).FirstOrDefault(path=>{try{return(string)JObject.Parse(File.ReadAllText(path))["id"]==id;}catch{return false;}});}
        private static string Absolute(string assetPath){return Path.GetFullPath(Path.Combine(Directory.GetParent(Application.dataPath).FullName,assetPath.Replace('/',Path.DirectorySeparatorChar)));}
        private static string Describe(ValidationReport report){return string.Join(" | ",report.Entries.Select(v=>v.Code+" "+v.Path+" "+v.Message));}
    }
}

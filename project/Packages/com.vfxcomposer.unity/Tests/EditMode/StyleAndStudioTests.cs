using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VFXComposer.Editor.Domain;
using VFXComposer.Editor.Patch;
using VFXComposer.Editor.Rules;
using VFXComposer.Editor.Style;
using VFXComposer.Editor.UI;

namespace VFXComposer.Tests.EditMode
{
    public sealed class StyleAndStudioTests
    {
        [Test]
        public void FourteenStyleProfiles_HaveRegisteredShadersMaterialsAndDimensionContracts()
        {
            var styles=VfxStyleRegistry.All.ToArray();CollectionAssert.AreEquivalent(new[]{"stylized","cartoon","pixel","inkwash","semireal","holo","dark","neon","lowpoly","crystal","candy","cosmic","steampunk","ghost"},styles.Select(value=>value.Token));Assert.That(styles.Select(value=>value.Mode).Distinct().Count(),Is.EqualTo(14));Assert.That(styles.Single(value=>value.Token=="pixel").Supports2D,Is.True);Assert.That(styles.Single(value=>value.Token=="pixel").Supports3D,Is.False);Assert.That(styles.Single(value=>value.Token=="holo").Supports3D,Is.True);Assert.That(styles.Single(value=>value.Token=="steampunk").Supports2D,Is.False);
            VfxStyleSharedLibrary.EnsureAll();foreach(var style in styles){Assert.That(Shader.Find(style.ShaderName),Is.Not.Null,style.ShaderName);var material=VfxStyleSharedLibrary.MaterialFor(style.Token);Assert.That(material,Is.Not.Null,style.Token);Assert.That(material.shader.name,Is.EqualTo(style.ShaderName));Assert.That(material.GetFloat("_StyleMode"),Is.EqualTo(style.Mode).Within(.001f));}
        }

        [Test]
        public void SharedLibrary_HasExpandedReusableMeshesAtlasesAndSmallSourceFiles()
        {
            VfxStyleSharedLibrary.EnsureAll();foreach(var path in new[]{VfxStyleSharedLibrary.QuadPath,VfxStyleSharedLibrary.RingPath,VfxStyleSharedLibrary.RibbonPath,VfxStyleSharedLibrary.BurstPath,VfxStyleSharedLibrary.ConePath,VfxStyleSharedLibrary.ShardPath}){var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);Assert.That(mesh,Is.Not.Null,path);Assert.That(mesh.vertexCount,Is.GreaterThanOrEqualTo(4));Assert.That(mesh.bounds.size.sqrMagnitude,Is.GreaterThan(.01f));}
            var textures=Directory.GetFiles(Absolute(VfxStyleSharedLibrary.TextureRoot),"*.png",SearchOption.TopDirectoryOnly);Assert.That(textures.Length,Is.EqualTo(22));Assert.That(textures.Sum(path=>new FileInfo(path).Length),Is.LessThan(100000),"The complete shared style texture source set must stay below 100 KB.");Assert.That(textures.All(path=>new FileInfo(path).Length<32000),Is.True);
        }

        [Test]
        public void SixStyleAndCapabilitySkinSamples_BuildStrictIdempotentRuntimeEntriesAndPreviewScene()
        {
            W1StyleAuthoring.BuildAll();var first=W1StyleAuthoring.SampleRecipePaths.Select(PathInfo).ToArray();W1StyleAuthoring.BuildAll();var second=W1StyleAuthoring.SampleRecipePaths.Select(PathInfo).ToArray();CollectionAssert.AreEqual(first,second,"Second W1 build must preserve Prefab GUID and ownership buildHash.");foreach(var recipePath in W1StyleAuthoring.SampleRecipePaths){var parsed=VfxDomainParser.ParseRecipe(File.ReadAllText(Absolute(recipePath))).Value;var prefabPath="Assets/VFX/Generated/"+parsed.Id+"/VFX_"+parsed.Id+".prefab";var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);Assert.That(prefab,Is.Not.Null,prefabPath);Assert.That(prefab.GetComponent<StyledVfxController>(),Is.Not.Null);Assert.That(prefab.GetComponents<MonoBehaviour>().Count(value=>value is IVfxRuntimeEntry),Is.EqualTo(1));Assert.That(VfxProjectRules.EnforcementFor(parsed.Id),Is.EqualTo(VfxRulesEnforcement.Strict));var manifest=JObject.Parse(File.ReadAllText(VfxProjectRules.ManifestAbsolutePath(parsed.Id)));Assert.That((string)manifest["enforcement"],Is.EqualTo("strict"));Assert.That((int)manifest["cost"]["gameObjects"],Is.LessThanOrEqualTo(10));Assert.That((int)manifest["cost"]["localTextureBytes"],Is.EqualTo(0));Assert.That(((JArray)manifest["ownedOutputs"]).Any(value=>((string)value["path"]).EndsWith(".mat",StringComparison.OrdinalIgnoreCase)),Is.False,"Style materials are shared dependencies, not duplicated local outputs.");}
            var scene=EditorSceneManager.OpenScene(W1StyleAuthoring.PreviewScenePath,OpenSceneMode.Additive);try{Assert.That(scene.GetRootGameObjects().Count(value=>value.name.StartsWith("Cell_",StringComparison.Ordinal)),Is.EqualTo(7));Assert.That(scene.GetRootGameObjects().Count(value=>value.CompareTag("MainCamera")),Is.EqualTo(1));}finally{EditorSceneManager.CloseScene(scene,true);}
        }

        [Test]
        public void ThreePlannedCapabilitySkinDemos_KeepExactBehaviorAndStyleOrthogonal()
        {
            W1StyleAuthoring.BuildAll();AssertDemo("cap_demo_fan_wave_cartoon_2d","wave","single","fan","instant","cartoon");AssertDemo("cap_demo_charge_occlude_holo_3d","stationary","occlude","single","charge_scale","holo");AssertDemo("cap_demo_telegraph_nova_holy_3d","expand_ring","single","ring","telegraph","stylized");
        }

        [Test]
        public void DarkStylePatch_IsBareValidatedAndProducesTheFrozenDarkRecipe()
        {
            var final=JObject.Parse(File.ReadAllText(Absolute("Assets/VFX/Recipes/StyleSamples/frost_impact_2d.dark.json")));var source=(JObject)final.DeepClone();source["revision"]=1;source["style"]["token"]="stylized";source["style"]["palette"]=new JObject{{"primary","#8FA3B8"},{"secondary","#DDE6F0"},{"accent","#FFFFFF"}};var patch=File.ReadAllText(Absolute("Assets/VFX/Recipes/Patches/frost_impact_2d.to-dark.patch.json"));Assert.That(JToken.Parse(patch),Is.TypeOf<JArray>());var result=new VfxPatchService().Validate(source.ToString(),patch,1);Assert.That(result.IsValid,Is.True,string.Join(" | ",result.Report.Entries.Select(value=>value.Code+" "+value.Path+" "+value.Message)));var actual=JObject.Parse(result.PatchedRecipeJson);Assert.That((string)actual["style"]["token"],Is.EqualTo("dark"));Assert.That((string)actual["style"]["palette"]["primary"],Is.EqualTo("#10051F"));Assert.That((int)actual["revision"],Is.EqualTo(2));
        }

        [Test]
        public void StudioLibraryFiltersByCapabilityAndDraftPatchReviewModelsAreDeterministic()
        {
            var items=VfxStudioLibrary.Scan();Assert.That(items.Count,Is.GreaterThanOrEqualTo(50));var homing=items.Single(value=>value.Id=="cap_homing_proj_3d");Assert.That(homing.Capabilities,Does.Contain("homing"));var filter=new VfxStudioLibraryFilter{Capability="homing"};Assert.That(items.Where(filter.Matches).Any(value=>value.Id==homing.Id),Is.True);filter.Search="no_such_effect";Assert.That(items.Where(filter.Matches),Is.Empty);
            var source=File.ReadAllText(Absolute("Assets/VFX/Recipes/Capability/cap_homing_proj_3d.default.json"));var draft=JObject.Parse(VfxStudioDraftBuilder.FromRecipe(source,"studio_homing_variant","Studio Homing","neon","3d","projectile"));Assert.That((string)draft["id"],Is.EqualTo("studio_homing_variant"));Assert.That((string)draft["style"]["token"],Is.EqualTo("neon"));Assert.That((string)draft["behavior"]["motion"]["type"],Is.EqualTo("homing"));
            var queue=new VfxStudioPatchQueue();queue.SetStyle("cartoon");queue.SetStyle("neon");queue.SetPalette("primary","#00D9FF");queue.SetBehavior("motion","turn_rate",new JValue(90));Assert.That(queue.Count,Is.EqualTo(3));var first=queue.ToJson();Assert.That(first,Is.EqualTo(queue.ToJson()));Assert.That(JArray.Parse(first).Count,Is.EqualTo(3));
            var review=new VfxStudioReviewState{Schema=true,RuntimeEntry=true,Manifest=true,StrictBudget=true,Idempotence=true,PlaybackReset=true,Evidence=true};StringAssert.Contains("PENDING USER SIGN-OFF",review.ToMarkdown("sample",""));review.Shape=review.Layers=review.Motion=review.Dissipation=review.Depth=true;StringAssert.Contains("PENDING USER SIGN-OFF",review.ToMarkdown("sample","user"));Assert.That(review.ToMarkdown("sample","user"),Does.Not.Contain("USER SIGNED"));
        }

        private static string PathInfo(string recipePath){var recipe=VfxDomainParser.ParseRecipe(File.ReadAllText(Absolute(recipePath))).Value;var prefab="Assets/VFX/Generated/"+recipe.Id+"/VFX_"+recipe.Id+".prefab";return AssetDatabase.AssetPathToGUID(prefab)+"|"+(string)JObject.Parse(File.ReadAllText(VfxProjectRules.ManifestAbsolutePath(recipe.Id)))["buildHash"];}
        private static void AssertDemo(string id,string motion,string hit,string emission,string timing,string style){var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/VFX/Generated/"+id+"/VFX_"+id+".prefab");Assert.That(prefab,Is.Not.Null,id);var entry=prefab.GetComponent<StyledVfxController>();Assert.That(entry,Is.Not.Null);Assert.That(entry.MotionType,Is.EqualTo(motion));Assert.That(entry.HitType,Is.EqualTo(hit));Assert.That(entry.EmissionType,Is.EqualTo(emission));Assert.That(entry.TimingType,Is.EqualTo(timing));Assert.That(entry.StyleToken,Is.EqualTo(style));}
        private static string Absolute(string assetPath){return Path.GetFullPath(Path.Combine(Directory.GetParent(Application.dataPath).FullName,assetPath.Replace('/',Path.DirectorySeparatorChar)));}
    }
}

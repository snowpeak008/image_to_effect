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
    public sealed class InteractionGalleryProductionTests
    {
        [Test]
        public void NineRecipesAndRuntimeEntries_CoverInteractionProfiles()
        {
            Assert.That(InteractionGalleryCompiler.Definitions.Length,Is.EqualTo(9));var profiles=new System.Collections.Generic.HashSet<InteractionGalleryProfile>();foreach(var definition in InteractionGalleryCompiler.Definitions){var recipe=InteractionGalleryCompiler.Parse(File.ReadAllText(Absolute(definition.RecipePath)),definition);var path="Assets/VFX/Generated/"+definition.Id+"/VFX_"+definition.Id+".prefab";var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);Assert.That(prefab,Is.Not.Null,definition.Id);var entry=prefab.GetComponent<InteractionGalleryVfxController>();Assert.That(entry,Is.Not.Null);Assert.That(entry.Profile,Is.EqualTo(definition.Profile));Assert.That(entry.Sustained,Is.EqualTo(recipe.Sustained));Assert.That(prefab.GetComponents<MonoBehaviour>().Count(value=>value is IVfxRuntimeEntry),Is.EqualTo(1));Assert.That(prefab.GetComponentsInChildren<Renderer>(true).All(value=>!value.enabled),Is.True);var manifest=JObject.Parse(File.ReadAllText(VfxProjectRules.ManifestAbsolutePath(definition.Id)));Assert.That((string)manifest["runtimeEntry"]["path"],Is.EqualTo(path));profiles.Add(entry.Profile);}Assert.That(profiles.Count,Is.EqualTo(9));
        }

        [Test]
        public void DominantStructures_AreProfileSpecificAndUseNoNewPng()
        {
            AssertChild("focus_charge_3d","ChargeNode_0");AssertChild("channel_tether_3d","ChannelLayer_0");AssertChild("warning_telegraph_3d","CountdownFill");AssertChild("chain_arc_3d","ChainSegment_0");AssertChild("seeker_orb_3d","HomingHead");AssertChild("weapon_enchant_3d","WeaponRig");AssertChild("phase_dash_3d","StartGhost");AssertChild("dissolve_transform_3d","DissolveFragments");AssertChild("ultimate_sequence_3d","ChargeStage");Assert.That(Directory.GetFiles(Absolute("Assets/VFX/Generated"),"*.png",SearchOption.AllDirectories).Any(path=>InteractionGalleryCompiler.Definitions.Any(definition=>path.Contains(definition.Id))),Is.False);
        }

        [Test]
        public void PreviewScene_HasNineEntriesOneCameraAndPreviewOnlyDriver()
        {
            try{var scene=UnityEditor.SceneManagement.EditorSceneManager.OpenScene(InteractionGalleryScene.ScenePath,UnityEditor.SceneManagement.OpenSceneMode.Single);var roots=scene.GetRootGameObjects();Assert.That(roots.SelectMany(root=>root.GetComponentsInChildren<Camera>(true)).Count(),Is.EqualTo(1));Assert.That(roots.SelectMany(root=>root.GetComponentsInChildren<InteractionGalleryVfxController>(true)).Count(),Is.EqualTo(9));Assert.That(roots.SelectMany(root=>root.GetComponentsInChildren<InteractionGalleryPlaybackDriver>(true)).Count(),Is.EqualTo(1));}
            finally{UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,UnityEditor.SceneManagement.NewSceneMode.Single);}
        }

        [Test]
        public void CSharpThresholdSmoothing_IsZeroBeforeAndOneAfterWindow()
        {
            var method=typeof(InteractionGalleryVfxController).GetMethod("Smooth01",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static);Assert.That(method,Is.Not.Null);Assert.That((float)method.Invoke(null,new object[]{.4f,.6f,.2f}),Is.EqualTo(0).Within(.0001f));Assert.That((float)method.Invoke(null,new object[]{.4f,.6f,.5f}),Is.EqualTo(.5f).Within(.0001f));Assert.That((float)method.Invoke(null,new object[]{.4f,.6f,.8f}),Is.EqualTo(1).Within(.0001f));
        }

        private static void AssertChild(string id,string name){var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/VFX/Generated/"+id+"/VFX_"+id+".prefab");Assert.That(prefab.transform.Find(name),Is.Not.Null,id+" missing "+name);}
        private static string Absolute(string path){return Path.GetFullPath(Path.Combine(Directory.GetParent(Application.dataPath).FullName,path.Replace('/',Path.DirectorySeparatorChar)));}
    }
}

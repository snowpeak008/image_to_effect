using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VFXComposer.Editor.Rules;
using VFXComposer.Editor.ValidationGallery;

namespace VFXComposer.Tests.EditMode
{
    public sealed class ValidationGalleryProductionTests
    {
        [Test]
        public void FiveRecipes_AreStrictAndIdentityLocked()
        {
            foreach(var definition in ValidationGalleryCompiler.Definitions)
            {
                var text=File.ReadAllText(Absolute(definition.RecipePath)); var recipe=ValidationGalleryCompiler.Parse(text,definition); Assert.That(recipe.Id,Is.EqualTo(definition.Id));
                var bad=JObject.Parse(text); bad["unityProperty"]=1; Assert.Throws<InvalidOperationException>(()=>ValidationGalleryCompiler.Parse(bad.ToString(),definition));
            }
        }

        [Test]
        public void FiveRuntimePrefabs_AreStrictCompactAndIdleHidden()
        {
            ValidationGalleryCompiler.BuildAll();
            foreach(var definition in ValidationGalleryCompiler.Definitions)
            {
                var folder="Assets/VFX/Generated/"+definition.Id; var path=folder+"/VFX_"+definition.Id+".prefab"; var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path); Assert.That(prefab,Is.Not.Null,definition.Id);
                Assert.That(prefab.GetComponentsInChildren<Transform>(true).Length,Is.EqualTo(7)); Assert.That(prefab.GetComponentsInChildren<Renderer>(true).Length,Is.EqualTo(6)); Assert.That(prefab.GetComponentsInChildren<ParticleSystem>(true).Length,Is.EqualTo(1)); Assert.That(prefab.GetComponentsInChildren<Renderer>(true).All(r=>!r.enabled),Is.True); Assert.That(prefab.GetComponents<MonoBehaviour>().Count(value=>value is IVfxRuntimeEntry),Is.EqualTo(1)); Assert.That(prefab.GetComponentInChildren<ValidationGalleryPlaybackDriver>(true),Is.Null);
                var files=Directory.GetFiles(Absolute(folder),"*",SearchOption.AllDirectories).Where(file=>!file.EndsWith(".meta",StringComparison.OrdinalIgnoreCase)).ToArray(); Assert.That(files.Length,Is.EqualTo(1));
                var manifest=JObject.Parse(File.ReadAllText(VfxProjectRules.ManifestAbsolutePath(definition.Id))); Assert.That((string)manifest["enforcement"],Is.EqualTo("strict")); Assert.That((string)manifest["archetype"],Is.EqualTo(definition.Archetype)); Assert.That(((JArray)manifest["ownedOutputs"]).Count,Is.EqualTo(1));
            }
        }

        [Test]
        public void GalleryScene_ContainsNineInactiveFormalEntriesAndSceneOnlyDriver()
        {
            var setup=EditorSceneManager.GetSceneManagerSetup();
            try
            {
                ValidationGalleryScene.BuildForBatch(); var scene=SceneManager.GetSceneByPath(ValidationGalleryScene.ScenePath); Assert.That(scene.IsValid(),Is.True);
                var roots=scene.GetRootGameObjects(); var entries=roots.SelectMany(root=>root.GetComponentsInChildren<MonoBehaviour>(true)).Where(value=>value is IVfxRuntimeEntry).ToArray(); Assert.That(entries.Length,Is.EqualTo(9)); Assert.That(entries.All(value=>!value.gameObject.activeSelf),Is.True); Assert.That(roots.SelectMany(root=>root.GetComponentsInChildren<TextMesh>(true)).Count(),Is.EqualTo(9)); Assert.That(roots.SelectMany(root=>root.GetComponentsInChildren<ValidationGalleryPlaybackDriver>(true)).Count(),Is.EqualTo(1)); Assert.That(entries.All(value=>value.GetComponentInChildren<ValidationGalleryPlaybackDriver>(true)==null),Is.True);
            }
            finally
            {
                if(setup.Any(value=>value.isLoaded)) EditorSceneManager.RestoreSceneManagerSetup(setup);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            }
        }

        private static string Absolute(string assetPath){return Path.Combine(Application.dataPath,assetPath.Substring("Assets/".Length));}
    }
}

using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VFXComposer.Editor.SlashV2;

namespace VFXComposer.Editor.Preview
{
    /// <summary>S12B preview source is the generated v2 prefab only; it never composes Gold Sample template inputs.</summary>
    public static class S12SlashGeneratedPreview
    {
        public const string ScenePath = "Assets/VFX/Preview/S12_SlashGeneratedPreview.unity";
        public const string RecipePath = "Assets/VFX/Recipes/Slash/slash-3d-stylized.default.v2.json";
        private const string SavedHostScene = "Assets/VFX/Preview/S12_SlashGoldSample.unity";
        [MenuItem("Tools/VFX Composer/Slash v2 Preview/Open Generated Scene")]
        public static void OpenOrCreate()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildSceneForBatch();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }
        /// <summary>Builds and opens the exact selected v2 Recipe text; it never falls back to canonical bytes.</summary>
        public static bool OpenOrCreate(TextAsset selectedRecipe)
        {
            if (selectedRecipe == null || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
            BuildSceneForBatch(selectedRecipe.text);
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            return true;
        }

        /// <summary>Creates the fixed generated-preview scene additively, so automated verification never unloads the last loaded scene.</summary>
        public static void BuildSceneForBatch()
        {
            var recipe = AssetDatabase.LoadAssetAtPath<TextAsset>(RecipePath); if (recipe == null) throw new InvalidOperationException("Missing canonical Slash v2 recipe."); BuildSceneForBatch(recipe.text);
        }
        /// <summary>Batch-safe scene authoring from supplied selected Recipe bytes.</summary>
        public static void BuildSceneForBatch(string recipeJson)
        {
            var built = new S12SlashCompiler().Build(recipeJson); if (!built.Succeeded) throw new InvalidOperationException("Slash preview build failed for selected Recipe bytes.");
            if (Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).Any(scene => string.IsNullOrEmpty(scene.path))) { if (!File.Exists(SavedHostScene)) throw new InvalidOperationException("S12 saved preview host is missing: " + SavedHostScene); EditorSceneManager.OpenScene(SavedHostScene, OpenSceneMode.Single); }
            var existing = File.Exists(ScenePath); var scene = existing ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive) : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive); try { foreach (var root in scene.GetRootGameObjects()) UnityEngine.Object.DestroyImmediate(root); var camera = new GameObject("S12_SlashGeneratedCamera"); SceneManager.MoveGameObjectToScene(camera, scene); camera.tag = "MainCamera"; var component = camera.AddComponent<Camera>(); component.clearFlags = CameraClearFlags.SolidColor; component.backgroundColor = new Color(.075f, .082f, .095f, 1f); component.allowHDR = false; component.allowMSAA = false; component.orthographic = false; component.fieldOfView = 60f; component.cullingMask = 1; component.useOcclusionCulling = false; camera.transform.position = new Vector3(0f, 1.25f, -3.7f); camera.transform.LookAt(new Vector3(0f, .48f, 0f));
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(built.PrefabPath); var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject; SceneManager.MoveGameObjectToScene(instance, scene); instance.name = "S12_GeneratedSlash"; var driverGo = new GameObject("S12_SlashPreviewDriver"); SceneManager.MoveGameObjectToScene(driverGo, scene); var driver = driverGo.AddComponent<SlashPreviewPlaybackDriver>(); driver.Controller = instance.GetComponent<SlashEffectController>(); EditorSceneManager.SaveScene(scene, ScenePath); EnsureBuildScene(); }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }
        private static void EnsureBuildScene() { if (EditorBuildSettings.scenes.All(scene => !string.Equals(scene.path, ScenePath, StringComparison.Ordinal))) EditorBuildSettings.scenes = EditorBuildSettings.scenes.Concat(new[] { new EditorBuildSettingsScene(ScenePath, true) }).ToArray(); }
    }
}

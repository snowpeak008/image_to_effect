using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VFXComposer.Editor.Impact2D
{
    public static class Impact2DPreviewScene
    {
        public const string ScenePath = "Assets/VFX/Preview/VFXPREVIEW_Impact2D.unity";

        [MenuItem("Tools/VFX Composer/2D Impact/Build Frost Impact + Preview")]
        public static void BuildDefault()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var recipe = File.ReadAllText(Absolute(Impact2DCompiler.DefaultRecipePath));
            var result = new Impact2DCompiler().Build(recipe);
            if (!result.Succeeded) throw new System.InvalidOperationException("Frost Impact build failed. See validation report.");
            BuildScene(result.PrefabPath, false);
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        public static void BuildForBatch()
        {
            var recipe = File.ReadAllText(Absolute(Impact2DCompiler.DefaultRecipePath));
            var result = new Impact2DCompiler().Build(recipe);
            if (!result.Succeeded) throw new System.InvalidOperationException("Frost Impact build failed.");
            BuildScene(result.PrefabPath, true);
        }

        private static void BuildScene(string prefabPath, bool replaceUntitledBatchScene)
        {
            // Batch mode may start with one unsaved untitled scene, which cannot host a
            // second additive scene. Replace it, save the preview, and leave that saved
            // scene loaded until the batch process exits. Interactive mode stays additive.
            var mode = replaceUntitledBatchScene ? NewSceneMode.Single : NewSceneMode.Additive;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, mode);
            try
            {
                var cameraObject = new GameObject("VFXPREVIEW_Impact2D_Camera"); SceneManager.MoveGameObjectToScene(cameraObject, scene); var camera = cameraObject.AddComponent<Camera>(); cameraObject.tag = "MainCamera"; camera.orthographic = true; camera.orthographicSize = .84f; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.055f, .065f, .085f, 1f); camera.allowHDR = false; camera.allowMSAA = false; camera.cullingMask = 1; cameraObject.transform.position = new Vector3(0f, 0f, -10f);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath); var instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject; if (instance == null) throw new System.InvalidOperationException("Could not instantiate Frost Impact preview."); instance.transform.position = Vector3.zero;
                var driverObject = new GameObject("VFXPREVIEW_Impact2D_Driver"); SceneManager.MoveGameObjectToScene(driverObject, scene); var driver = driverObject.AddComponent<ImpactPreviewPlaybackDriver>(); var serialized = new SerializedObject(driver); serialized.FindProperty("controller").objectReferenceValue = instance.GetComponent<TimedImpactVfxController>(); serialized.FindProperty("interval").floatValue = .85f; serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.SaveScene(scene, ScenePath); EnsureSceneIsLoadable(); AssetDatabase.SaveAssets();
            }
            finally
            {
                if (!replaceUntitledBatchScene && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void EnsureSceneIsLoadable()
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            var existing = scenes.FindIndex(value => value.path == ScenePath);
            if (existing >= 0) scenes[existing] = new EditorBuildSettingsScene(ScenePath, true);
            else scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static string Absolute(string assetPath) { return Path.Combine(Application.dataPath, assetPath.Substring("Assets/".Length)); }
    }
}

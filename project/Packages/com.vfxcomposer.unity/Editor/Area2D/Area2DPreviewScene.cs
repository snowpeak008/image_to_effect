using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VFXComposer.Editor.Area2D
{
    public static class Area2DPreviewScene
    {
        public const string ScenePath = "Assets/VFX/Preview/VFXPREVIEW_Area2D.unity";

        [MenuItem("Tools/VFX Composer/2D Area/Build Inferno Vortex + Preview")]
        public static void BuildDefault()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var result = Build();
            BuildScene(result.PrefabPath, false);
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        public static void BuildForBatch()
        {
            var result = Build();
            BuildScene(result.PrefabPath, true);
        }

        private static VFXComposer.Editor.Build.VfxBuildResult Build()
        {
            var recipe = File.ReadAllText(Absolute(Area2DCompiler.DefaultRecipePath));
            var result = new Area2DCompiler().Build(recipe);
            if (!result.Succeeded) throw new System.InvalidOperationException("Inferno Vortex Area build failed. " + string.Join(" | ", result.Plan.Report.Entries.Select(entry => entry.Code + " " + entry.Path + " " + entry.Message)));
            return result;
        }

        private static void BuildScene(string prefabPath, bool replaceUntitledBatchScene)
        {
            var mode = replaceUntitledBatchScene ? NewSceneMode.Single : NewSceneMode.Additive;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, mode);
            try
            {
                var cameraObject = new GameObject("VFXPREVIEW_Area2D_Camera"); SceneManager.MoveGameObjectToScene(cameraObject, scene); var camera = cameraObject.AddComponent<Camera>(); cameraObject.tag = "MainCamera"; camera.orthographic = true; camera.orthographicSize = 1.82f; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.035f, .038f, .048f, 1f); camera.allowHDR = false; camera.allowMSAA = false; camera.cullingMask = 1; cameraObject.transform.position = new Vector3(0f, 0f, -10f);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath); var instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject; if (instance == null) throw new System.InvalidOperationException("Could not instantiate Inferno Vortex Area preview."); instance.transform.position = Vector3.zero;
                var driverObject = new GameObject("VFXPREVIEW_Area2D_Driver"); SceneManager.MoveGameObjectToScene(driverObject, scene); var driver = driverObject.AddComponent<AreaPreviewPlaybackDriver>(); var serialized = new SerializedObject(driver); serialized.FindProperty("controller").objectReferenceValue = instance.GetComponent<InfernoAreaVfxController>(); serialized.FindProperty("activeSeconds").floatValue = 4f; serialized.FindProperty("restartDelay").floatValue = .8f; serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.SaveScene(scene, ScenePath); EnsureSceneIsLoadable(); AssetDatabase.SaveAssets();
            }
            finally
            {
                if (!replaceUntitledBatchScene && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void EnsureSceneIsLoadable()
        {
            var scenes = EditorBuildSettings.scenes.ToList(); var existing = scenes.FindIndex(value => value.path == ScenePath);
            if (existing >= 0) scenes[existing] = new EditorBuildSettingsScene(ScenePath, true); else scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static string Absolute(string assetPath) { return Path.Combine(Application.dataPath, assetPath.Substring("Assets/".Length)); }
    }
}

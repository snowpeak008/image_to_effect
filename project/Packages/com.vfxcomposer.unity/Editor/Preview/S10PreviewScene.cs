using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VFXComposer.Editor.Build;

namespace VFXComposer.Editor.Preview
{
    public enum S10PreviewView { Front, Side, ObliqueTop, Close, GameDistance }

    /// <summary>One code-owned perspective pose used by both the interactive S10 preview and its evidence capture.</summary>
    public struct S10PreviewCameraPose
    {
        public Vector3 Position;
        public Vector3 Target;
        public float FieldOfView;
    }

    public sealed class S10PreviewWindow : EditorWindow
    {
        [MenuItem("Tools/VFX Composer/3D Preview")]
        public static void Open() { GetWindow<S10PreviewWindow>("VFX 3D Preview"); }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox("S10 3D Gold Preview: all review controls use the retained perspective scene and the same camera-pose table as evidence capture.", MessageType.Info);
            if (GUILayout.Button("Open 3D Gold Preview")) S10PreviewScene.OpenOrCreate();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Front")) S10PreviewScene.ApplyView(S10PreviewView.Front);
            if (GUILayout.Button("Side")) S10PreviewScene.ApplyView(S10PreviewView.Side);
            if (GUILayout.Button("Oblique")) S10PreviewScene.ApplyView(S10PreviewView.ObliqueTop);
            if (GUILayout.Button("Close")) S10PreviewScene.ApplyView(S10PreviewView.Close);
            if (GUILayout.Button("Game Distance")) S10PreviewScene.ApplyView(S10PreviewView.GameDistance);
            EditorGUILayout.EndHorizontal();
        }
    }

    /// <summary>Interactive S10 perspective preview. It never redirects a 3D Recipe to the S7 orthographic scene.</summary>
    public static class S10PreviewScene
    {
        public const string ScenePath = "Assets/VFX/Preview/S10_3D_FireballPreview.unity";
        public const string CameraName = "S10_PerspectiveCamera";
        private const string RecipePath = "Assets/VFX/Recipes/fireball-3d.default.json";

        [MenuItem("Tools/VFX Composer/3D Preview/Open Gold Scene")]
        public static void OpenOrCreate()
        {
            if (string.Equals(SceneManager.GetActiveScene().path, ScenePath, StringComparison.Ordinal)) { ApplyView(S10PreviewView.Front); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            OpenScene();
            ApplyView(S10PreviewView.Front);
        }

        public static S10PreviewCameraPose GetPose(S10PreviewView view)
        {
            var target = new Vector3(0f, .25f, 0f);
            switch (view)
            {
                case S10PreviewView.Front: return new S10PreviewCameraPose { Position = new Vector3(0f, 3f, -15f), Target = target, FieldOfView = 48f };
                case S10PreviewView.Side: return new S10PreviewCameraPose { Position = new Vector3(10.5f, 3.5f, -6f), Target = target, FieldOfView = 48f };
                case S10PreviewView.ObliqueTop: return new S10PreviewCameraPose { Position = new Vector3(8f, 8f, -11f), Target = target, FieldOfView = 50f };
                case S10PreviewView.Close: return new S10PreviewCameraPose { Position = new Vector3(0f, 2f, -7f), Target = target, FieldOfView = 52f };
                case S10PreviewView.GameDistance: return new S10PreviewCameraPose { Position = new Vector3(0f, 5f, -20f), Target = target, FieldOfView = 36f };
                default: throw new ArgumentOutOfRangeException("view", view, "Unsupported S10 review view.");
            }
        }

        public static Camera ApplyView(S10PreviewView view)
        {
            var camera = FindOrOpenCamera();
            ApplyPose(camera, GetPose(view));
            Selection.activeGameObject = camera.gameObject;
            EditorGUIUtility.PingObject(camera.gameObject);
            return camera;
        }

        public static void ApplyPose(Camera camera, S10PreviewCameraPose pose)
        {
            if (camera == null) throw new ArgumentNullException("camera");
            camera.orthographic = false;
            camera.fieldOfView = pose.FieldOfView;
            camera.transform.position = pose.Position;
            camera.transform.LookAt(pose.Target);
        }

        private static Camera FindOrOpenCamera()
        {
            var scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded) OpenScene();
            var camera = UnityEngine.Object.FindObjectsOfType<Camera>().FirstOrDefault(value => value.name == CameraName);
            if (camera == null) throw new InvalidOperationException("S10 preview scene has no " + CameraName + ".");
            return camera;
        }

        private static void OpenScene()
        {
            if (File.Exists(ScenePath)) { EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single); return; }
            CreateFallbackScene();
        }

        private static void CreateFallbackScene()
        {
            var recipe = AssetDatabase.LoadAssetAtPath<TextAsset>(RecipePath);
            if (recipe == null) throw new InvalidOperationException("Missing S10 default Recipe: " + RecipePath);
            var result = new VfxCompiler().Build(recipe.text);
            if (!result.Succeeded) throw new InvalidOperationException("Could not build S10 preview Prefab.");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(result.PrefabPath);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameraGo = new GameObject(CameraName); cameraGo.tag = "MainCamera"; cameraGo.AddComponent<Camera>();
            var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject; instance.name = "S10_FallbackGeneratedFireball";
            EditorSceneManager.SaveScene(scene, ScenePath);
        }
    }
}

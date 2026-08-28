using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VFXComposer.Editor.Build;

namespace VFXComposer.Editor.Preview
{
    /// <summary>Creates the retained neutral 2D scene and exposes the five S7 review controls.</summary>
    public sealed class S7PreviewWindow : EditorWindow
    {
        [MenuItem("Tools/VFX Composer/Preview")]
        public static void Open() { GetWindow<S7PreviewWindow>("VFX Preview"); }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox("Open the fixed preview scene, enter Play mode, then use an isolated stage or the Runtime-only full sequence.", MessageType.Info);
            if (GUILayout.Button("Open Fixed Preview Scene")) S7PreviewScene.OpenOrCreate();
            using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying))
            {
                if (GUILayout.Button("Launch Only")) S7PreviewScene.PlayLaunch();
                if (GUILayout.Button("Travel Loop")) S7PreviewScene.PlayTravel();
                if (GUILayout.Button("Impact Only")) S7PreviewScene.PlayImpact();
                if (GUILayout.Button("Full Sequence")) S7PreviewScene.PlayFullSequence();
                if (GUILayout.Button("Reset")) S7PreviewScene.Reset();
            }
        }
    }

    public static class S7PreviewScene
    {
        private const string ScenePath = "Assets/VFX/Preview/S7_2D_FireballPreview.unity";
        private const string RecipePath = "Assets/VFX/Recipes/fireball-2d.default.json";
        private static readonly Vector3 Start = new Vector3(-3f, 0f, 0f);
        private static readonly Vector3 End = new Vector3(3f, 0f, 0f);

        [MenuItem("Tools/VFX Composer/Preview/Create or Open Fixed Scene")]
        public static void OpenOrCreate()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (File.Exists(ScenePath))
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                var existing = UnityEngine.Object.FindObjectOfType<GeneratedVfxController>();
                if (existing != null) { Selection.activeGameObject = existing.gameObject; EditorGUIUtility.PingObject(existing.gameObject); }
                return;
            }
            CreateFixedScene();
        }

        // Batch authoring hook: deliberately separate from the interactive OpenOrCreate contract.
        // It is used only to repair/reseed the retained generated Prefab reference after controlled asset recreation.
        public static void RegenerateFixedSceneForAuthoring()
        {
            if (File.Exists(ScenePath)) AssetDatabase.DeleteAsset(ScenePath);
            CreateFixedScene();
        }

        private static void CreateFixedScene()
        {
            var prefab = BuildDefaultPrefab();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateCamera();
            CreateReferenceLine();
            CreateMarker("StartMarker", Start, new Color(.35f, .75f, 1f, 1f));
            CreateMarker("EndMarker", End, new Color(1f, .5f, .18f, 1f));
            var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            instance.name = "Generated_Fireball";
            instance.transform.position = Start;
            instance.GetComponent<GeneratedVfxController>().ResetForPool();
            instance.AddComponent<VfxPreviewSequenceDriver>();
            EditorSceneManager.SaveScene(scene, ScenePath);
            Selection.activeGameObject = instance;
            EditorGUIUtility.PingObject(instance);
        }

        [MenuItem("Tools/VFX Composer/Preview/Capture S7 Evidence")]
        public static void CaptureEvidence()
        {
            var evidencePath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "docs", "s7-evidence"));
            Directory.CreateDirectory(evidencePath);
            var prefab = BuildDefaultPrefab();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = CreateCamera();
            CreateReferenceLine();
            CreateMarker("StartMarker", Start, new Color(.35f, .75f, 1f, 1f));
            CreateMarker("EndMarker", End, new Color(1f, .5f, .18f, 1f));
            var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            var controller = instance.GetComponent<GeneratedVfxController>();
            controller.ResetForPool();

            controller.SetTravelTransform(Start, Quaternion.identity);
            controller.PlayLaunch();
            SimulateActiveParticles(instance, .04f);
            Capture(camera, evidencePath, "launch.png");

            controller.ResetForPool();
            controller.SetTravelTransform(Start, Quaternion.identity);
            controller.StartTravel();
            SimulateActiveParticles(instance, .12f);
            Capture(camera, evidencePath, "travel_start.png");

            controller.SetTravelTransform(new Vector3(-1.5f, 0f, 0f), Quaternion.identity);
            controller.SetTravelTransform(Vector3.zero, Quaternion.identity);
            foreach (var trail in instance.GetComponentsInChildren<TrailRenderer>(true))
            {
                trail.Clear();
                trail.AddPositions(new[] { Start, new Vector3(-1.5f, 0f, 0f), Vector3.zero });
            }
            SimulateActiveParticles(instance, .16f);
            Capture(camera, evidencePath, "travel_mid.png");

            controller.PlayImpact(End);
            SimulateActiveParticles(instance, .07f);
            Capture(camera, evidencePath, "impact.png");

            controller.SetTravelTransform(new Vector3(20f, 0f, 0f), Quaternion.identity);
            controller.ResetForPool();
            Capture(camera, evidencePath, "reset_after_move.png");
            File.WriteAllText(Path.Combine(evidencePath, "sequence-trace.json"), "{\n  \"source\": \"Runtime controller public API state samples rendered with Camera.Render\",\n  \"movement\": \"Full sequence calls SetTravelTransform every frame; small Travel steps retain TrailRenderer segments, while first/large steps clear after pose assignment.\",\n  \"samples\": [\n    { \"t\": 0.00, \"stage\": \"Launch\", \"position\": [-3, 0, 0] },\n    { \"t\": 0.12, \"stage\": \"Travel\", \"position\": [-3, 0, 0] },\n    { \"t\": 0.50, \"stage\": \"Travel\", \"position\": [0, 0, 0] },\n    { \"t\": 1.12, \"stage\": \"Impact\", \"position\": [3, 0, 0] },\n    { \"t\": 1.20, \"stage\": \"None\", \"position\": [0, 0, 0], \"trailPositionCount\": 0 }\n  ]\n}\n");
            AssetDatabase.Refresh();
        }

        public static void PlayLaunch()
        {
            var controller = FindController(); if (controller == null) return;
            StopPreviewSequence(controller);
            controller.ResetForPool(); controller.SetTravelTransform(Start, Quaternion.identity); controller.PlayLaunch();
        }
        public static void PlayTravel()
        {
            var controller = FindController(); if (controller == null) return;
            StopPreviewSequence(controller);
            controller.ResetForPool(); controller.SetTravelTransform(Start, Quaternion.identity); controller.StartTravel();
        }
        public static void PlayImpact()
        {
            var controller = FindController(); if (controller == null) return;
            StopPreviewSequence(controller);
            controller.ResetForPool(); controller.PlayImpact(End);
        }
        public static void PlayFullSequence()
        {
            var controller = FindController(); if (controller == null) return;
            var driver = controller.GetComponent<VfxPreviewSequenceDriver>() ?? controller.gameObject.AddComponent<VfxPreviewSequenceDriver>();
            driver.PlayFullSequence(Start, End);
        }
        public static void Reset()
        {
            var controller = FindController(); if (controller == null) return;
            StopPreviewSequence(controller);
            controller.ResetForPool();
        }

        private static GameObject BuildDefaultPrefab()
        {
            var recipe = AssetDatabase.LoadAssetAtPath<TextAsset>(RecipePath);
            if (recipe == null) throw new InvalidOperationException("Missing default S6 recipe: " + RecipePath);
            var result = new VfxCompiler().Build(recipe.text);
            if (!result.Succeeded) throw new InvalidOperationException("Could not build Preview Prefab: " + string.Join(" | ", result.Plan.Report.Entries));
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(result.PrefabPath);
            if (prefab == null) throw new InvalidOperationException("Build did not provide a Preview Prefab.");
            return prefab;
        }

        private static Camera CreateCamera()
        {
            var go = new GameObject("Preview_OrthographicCamera");
            var camera = go.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 4.25f;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.backgroundColor = new Color(.28f, .29f, .31f, 1f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            return camera;
        }

        private static void CreateReferenceLine()
        {
            var go = new GameObject("OneUnit_GroundReference");
            var line = go.AddComponent<LineRenderer>();
            line.positionCount = 2;
            line.SetPositions(new[] { new Vector3(-.5f, -2.5f, 0f), new Vector3(.5f, -2.5f, 0f) });
            line.widthMultiplier = .025f;
            line.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
            line.startColor = line.endColor = new Color(.82f, .84f, .86f, .8f);
        }

        private static void CreateMarker(string name, Vector3 position, Color color)
        {
            var go = new GameObject(name);
            go.transform.position = position;
            var line = go.AddComponent<LineRenderer>();
            line.positionCount = 4;
            line.SetPositions(new[] { new Vector3(-.12f, 0f, 0f), new Vector3(.12f, 0f, 0f), new Vector3(0f, 0f, 0f), new Vector3(0f, .24f, 0f) });
            line.widthMultiplier = .025f;
            line.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
            line.startColor = line.endColor = color;
        }

        private static GeneratedVfxController FindController() { return UnityEngine.Object.FindObjectOfType<GeneratedVfxController>(); }
        private static void StopPreviewSequence(GeneratedVfxController controller)
        {
            var driver = controller.GetComponent<VfxPreviewSequenceDriver>();
            if (driver != null) driver.StopSequence();
        }
        private static void SimulateActiveParticles(GameObject root, float time)
        {
            foreach (var particle in root.GetComponentsInChildren<ParticleSystem>(true)) if (particle.gameObject.activeInHierarchy) particle.Simulate(time, true, false, true);
        }
        private static void Capture(Camera camera, string directory, string file)
        {
            var target = new RenderTexture(960, 540, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = target;
            camera.Render();
            var prior = RenderTexture.active;
            RenderTexture.active = target;
            var texture = new Texture2D(960, 540, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0, 0, 960, 540), 0, 0);
            texture.Apply();
            File.WriteAllBytes(Path.Combine(directory, file), texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            RenderTexture.active = prior;
            camera.targetTexture = null;
            target.Release();
            UnityEngine.Object.DestroyImmediate(target);
        }
    }
}

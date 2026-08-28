using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VFXComposer.Editor.ValidationGallery;

namespace VFXComposer.Editor.Capabilities
{
    public static class CapabilityProjectilePreviewScene
    {
        public const string ScenePath = "Assets/VFX/Preview/VFXPREVIEW_CapProjectile.unity";
        public const string CandidateStatusRootName = "W_C1_NEXT_CANDIDATE_VISUAL_PENDING";
        public const float CellWidth = 3.25f;
        public const float CellHeight = 1.82f;
        public const float EntryScale = .2f;
        public const float EntryOffsetX = -.8f;
        public const float EntryOffsetY = .14f;
        public const float LabelY = -.72f;
        private static readonly string[] Ids =
        {
            "cap_linear_proj_3d", "cap_accel_proj_3d", "cap_parabola_proj_3d", "cap_homing_proj_3d",
            "cap_wave_proj_2d", "cap_boomerang_proj_3d", "cap_bounce_proj_3d", "cap_orbit_proj_3d",
            "cap_pierce_proj_3d", "cap_split_proj_2d", "cap_chainhop_proj_2d", "cap_volley_proj_2d"
        };

        [MenuItem("Tools/VFX Composer/Capabilities/Build + Open W-C1 Next Candidate (Visual Pending)")]
        public static void BuildAndOpen()
        {
            BuildForBatch();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        public static void BuildForBatch()
        {
            CapabilityBlankCompiler.BuildProjectileBlanks();
            ValidationGalleryCompiler.EnsureFolder("Assets/VFX/Preview");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var entries = new List<MonoBehaviour>();
            const float x = CellWidth, y = CellHeight;
            for (var i = 0; i < Ids.Length; i++)
            {
                var column = i % 4;
                var row = i / 4;
                var position = new Vector3((column - 1.5f) * x, (1 - row) * y, 0f);
                var cell = new GameObject("Cell_" + (i + 1).ToString("00") + "_" + Ids[i]);
                SceneManager.MoveGameObjectToScene(cell, scene);
                cell.transform.position = position;
                var path = "Assets/VFX/Generated/" + Ids[i] + "/VFX_" + Ids[i] + ".prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) throw new InvalidOperationException("Missing W-C1 Runtime Entry: " + path);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                instance.transform.SetParent(cell.transform, false);
                instance.transform.localPosition = new Vector3(EntryOffsetX, EntryOffsetY, 0f);
                instance.transform.localScale = Vector3.one * EntryScale;
                var entry = instance.GetComponents<MonoBehaviour>().FirstOrDefault(value => value is IVfxRuntimeEntry);
                if (entry == null) throw new InvalidOperationException(path + " has no root IVfxRuntimeEntry.");
                entries.Add(entry);
                instance.SetActive(false);
                CreateLabel(cell.transform, (i + 1).ToString("00") + " " + Ids[i].Replace("cap_", string.Empty).Replace("_proj_3d", string.Empty).Replace("_proj_2d", string.Empty));
            }

            CreateCandidateStatus(scene);

            CreateInteractionWitness(scene, "StaticTarget", new Vector3(5.1f, .15f, .5f), new Vector3(.18f, .55f, .18f));
            CreateInteractionWitness(scene, "MovingTargetWitness", new Vector3(5.1f, 1.85f, .5f), new Vector3(.18f, .55f, .18f));
            CreateInteractionWitness(scene, "WallWitness", new Vector3(5.1f, -.85f, .5f), new Vector3(.12f, 1.3f, .18f));
            CreateInteractionWitness(scene, "FloorWitness", new Vector3(0f, -2.72f, .6f), new Vector3(6.3f, .04f, .18f));

            var cameraObject = new GameObject("VFXPREVIEW_CapProjectile_Camera");
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 3.65f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.022f, .025f, .034f, 1f);
            camera.allowHDR = false;
            camera.allowMSAA = false;
            camera.cullingMask = 1;
            cameraObject.transform.position = new Vector3(0, 0, -10);

            var driverObject = new GameObject("VFXPREVIEW_CapProjectile_Driver");
            SceneManager.MoveGameObjectToScene(driverObject, scene);
            var driver = driverObject.AddComponent<ValidationGalleryPlaybackDriver>();
            var serialized = new SerializedObject(driver);
            var property = serialized.FindProperty("runtimeEntries");
            property.arraySize = entries.Count;
            for (var i = 0; i < entries.Count; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = entries[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EnsureSceneIsLoadable();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void CreateInteractionWitness(Scene scene, string name, Vector3 position, Vector3 scale)
        {
            var value = GameObject.CreatePrimitive(PrimitiveType.Cube);
            value.name = name;
            SceneManager.MoveGameObjectToScene(value, scene);
            value.transform.position = position;
            value.transform.localScale = scale;
            UnityEngine.Object.DestroyImmediate(value.GetComponent<Collider>());
        }

        private static void CreateLabel(Transform parent, string text)
        {
            var value = new GameObject("Label");
            value.transform.SetParent(parent, false);
            value.transform.localPosition = new Vector3(0, LabelY, -.1f);
            var label = value.AddComponent<TextMesh>();
            label.text = text;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.fontSize = 42;
            label.characterSize = .025f;
            label.color = new Color(.64f, .69f, .76f, .92f);
            label.GetComponent<MeshRenderer>().sortingOrder = 100;
        }

        private static void CreateCandidateStatus(Scene scene)
        {
            var value = new GameObject(CandidateStatusRootName);
            SceneManager.MoveGameObjectToScene(value, scene);
            value.transform.position = new Vector3(0f, 3.14f, -.2f);
            var label = value.AddComponent<TextMesh>();
            label.text = "W-C1 NEXT CANDIDATE - VISUAL SIGN-OFF PENDING\n10 split | 11 chain-hop | 12 fan > stagger > ring";
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.fontSize = 48;
            label.characterSize = .025f;
            label.color = new Color(.78f, .82f, .9f, .96f);
            label.GetComponent<MeshRenderer>().sortingOrder = 110;
        }

        private static void EnsureSceneIsLoadable()
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            var index = scenes.FindIndex(value => value.path == ScenePath);
            if (index >= 0) scenes[index] = new EditorBuildSettingsScene(ScenePath, true);
            else scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}

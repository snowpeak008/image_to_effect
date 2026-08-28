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
    public static class CapabilityAdditionalPreviewScenes
    {
        public const string BeamScenePath = "Assets/VFX/Preview/VFXPREVIEW_CapBeam.unity";
        public const string TimingScenePath = "Assets/VFX/Preview/VFXPREVIEW_CapTiming.unity";
        public const string BeamCandidateStatusRootName = "W_C2_NEXT_CANDIDATE_VISUAL_PENDING";
        public const string TimingCandidateStatusRootName = "W_C3_NEXT_CANDIDATE_VISUAL_PENDING";
        public const float BeamCellWidth = 2.7f;
        public const float BeamCellHeight = 2.35f;
        public const float BeamEntryScale = .22f;
        public const float BeamEntryOffsetX = -.55f;
        public const float BeamEntryOffsetY = .08f;
        public const float BeamLabelY = -.78f;
        public const int TimingColumns = 4;
        public const int TimingRows = 3;
        public const float TimingCellWidth = 2.75f;
        public const float TimingCellHeight = 1.82f;
        public const float TimingEntryScale = .16f;
        public const float TimingEntryOffsetX = 0f;
        public const float TimingEntryOffsetY = .14f;
        public const float TimingLabelY = -.7f;
        public const float TimingContractMaxExtent = 4f;
        private static readonly string[] BeamIds = { "cap_hitscan_beam_3d", "cap_sustained_beam_3d", "cap_sweep_beam_3d", "cap_charge_beam_3d", "cap_reflect_beam_3d", "cap_occlude_beam_3d", "cap_converge_beam_3d", "cap_arclink_beam_2d" };
        private static readonly string[] TimingIds = { "cap_telegraph_impact_3d", "cap_delayfuse_impact_2d", "cap_tickpulse_area_2d", "cap_charge_release_2d", "cap_channel_3d", "cap_chainseq_impact_2d", "cap_expand_area_3d", "cap_implode_area_3d", "cap_movingzone_area_3d", "cap_growth_area_2d" };

        [MenuItem("Tools/VFX Composer/Capabilities/Build + Open W-C2 Preview")]
        public static void BuildAndOpenBeam() { BuildBeamForBatch(); EditorSceneManager.OpenScene(BeamScenePath, OpenSceneMode.Single); }
        [MenuItem("Tools/VFX Composer/Capabilities/Build + Open W-C3 Preview")]
        public static void BuildAndOpenTiming() { BuildTimingForBatch(); EditorSceneManager.OpenScene(TimingScenePath, OpenSceneMode.Single); }

        public static void BuildBeamForBatch() { CapabilityBlankCompiler.BuildBeamBlanks(); BuildBeam(); }
        public static void BuildTimingForBatch() { CapabilityBlankCompiler.BuildTimingAreaBlanks(); BuildTiming(); }

        private static void BuildBeam()
        {
            ValidationGalleryCompiler.EnsureFolder("Assets/VFX/Preview");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var entries = new List<CapabilityBlankVfxController>();
            var cells = new List<Transform>();
            for (var i = 0; i < BeamIds.Length; i++)
            {
                var column = i % 4;
                var row = i / 4;
                var cell = new GameObject("Cell_" + (i + 1).ToString("00") + "_" + BeamIds[i]);
                SceneManager.MoveGameObjectToScene(cell, scene);
                cell.transform.position = new Vector3((column - 1.5f) * BeamCellWidth, (.5f - row) * BeamCellHeight, 0f);
                cells.Add(cell.transform);
                var path = "Assets/VFX/Generated/" + BeamIds[i] + "/VFX_" + BeamIds[i] + ".prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) throw new InvalidOperationException("Missing W-C2 Runtime Entry: " + path);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                instance.transform.SetParent(cell.transform, false);
                instance.transform.localPosition = new Vector3(BeamEntryOffsetX, BeamEntryOffsetY, 0f);
                instance.transform.localScale = Vector3.one * BeamEntryScale;
                var entry = instance.GetComponent<CapabilityBlankVfxController>();
                if (entry == null || entry.BeamVisual == null) throw new InvalidOperationException(path + " has no beam visual Runtime Entry.");
                entries.Add(entry);
                instance.SetActive(false);
                CreateLabel(cell.transform, (i + 1).ToString("00") + " " + BeamIds[i].Replace("cap_", string.Empty).Replace("_beam_3d", string.Empty).Replace("_beam_2d", string.Empty));
            }

            var sustainedSource = CreateWitness(cells[1], "SustainedSourceAnchor", new Vector3(BeamEntryOffsetX, BeamEntryOffsetY, -.05f), new Vector3(.07f, .13f, .07f), false);
            var sustainedTarget = CreateWitness(cells[1], "SustainedMovingTargetAnchor", new Vector3(BeamEntryOffsetX + BeamEntryScale * 4f, BeamEntryOffsetY + BeamEntryScale * .4f, -.05f), new Vector3(.07f, .18f, .07f), false);
            var occludeSource = CreateWitness(cells[5], "OccludeSourceAnchor", new Vector3(BeamEntryOffsetX, BeamEntryOffsetY, -.05f), new Vector3(.07f, .13f, .07f), false);
            var occludeTarget = CreateWitness(cells[5], "OccludeTargetAnchor", new Vector3(BeamEntryOffsetX + BeamEntryScale * 4f, BeamEntryOffsetY + BeamEntryScale * .4f, -.05f), new Vector3(.07f, .18f, .07f), false);
            var blocker = CreateWitness(cells[5], "MovableOcclusionBlocker", new Vector3(BeamEntryOffsetX + BeamEntryScale * 2f, BeamEntryOffsetY + BeamEntryScale * .2f, -.03f), new Vector3(.12f, .54f, .12f), true);
            var probeObject = new GameObject("ExplicitOcclusionProbe");
            probeObject.transform.SetParent(cells[5], false);
            var probe = probeObject.AddComponent<BeamCapabilityObstacleProbe>();
            var probeSerialized = new SerializedObject(probe);
            var blockers = probeSerialized.FindProperty("blockers");
            blockers.arraySize = 1;
            blockers.GetArrayElementAtIndex(0).objectReferenceValue = blocker.GetComponent<Collider>();
            probeSerialized.ApplyModifiedPropertiesWithoutUndo();

            CreateCandidateStatus(scene);

            var cameraObject = new GameObject("VFXPREVIEW_CapBeam_Camera");
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 3.25f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.022f, .025f, .034f, 1f);
            camera.allowHDR = false;
            camera.allowMSAA = false;
            camera.cullingMask = 1;
            cameraObject.transform.position = new Vector3(0, 0, -10);

            var driverObject = new GameObject("VFXPREVIEW_CapBeam_Driver");
            SceneManager.MoveGameObjectToScene(driverObject, scene);
            var driver = driverObject.AddComponent<BeamCapabilityPreviewDriver>();
            var serialized = new SerializedObject(driver);
            var runtimeEntries = serialized.FindProperty("runtimeEntries");
            runtimeEntries.arraySize = entries.Count;
            for (var i = 0; i < entries.Count; i++) runtimeEntries.GetArrayElementAtIndex(i).objectReferenceValue = entries[i];
            serialized.FindProperty("sustainedEntry").objectReferenceValue = entries[1];
            serialized.FindProperty("chargeEntry").objectReferenceValue = entries[3];
            serialized.FindProperty("occludeEntry").objectReferenceValue = entries[5];
            serialized.FindProperty("sustainedSource").objectReferenceValue = sustainedSource;
            serialized.FindProperty("sustainedTarget").objectReferenceValue = sustainedTarget;
            serialized.FindProperty("occludeSource").objectReferenceValue = occludeSource;
            serialized.FindProperty("occludeTarget").objectReferenceValue = occludeTarget;
            serialized.FindProperty("movableBlocker").objectReferenceValue = blocker;
            serialized.FindProperty("obstacleProbe").objectReferenceValue = probe;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, BeamScenePath);
            EnsureSceneIsLoadable(BeamScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void BuildTiming()
        {
            ValidationGalleryCompiler.EnsureFolder("Assets/VFX/Preview");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var entries = new List<CapabilityBlankVfxController>();
            var boundaryMaterial = AssetDatabase.LoadAssetAtPath<Material>(CapabilityBlankCompiler.AdditiveMaterialPath);
            if (boundaryMaterial == null) throw new InvalidOperationException("Missing shared capability material: " + CapabilityBlankCompiler.AdditiveMaterialPath);
            for (var i = 0; i < TimingColumns * TimingRows; i++)
            {
                var column = i % TimingColumns;
                var row = i / TimingColumns;
                var id = i < TimingIds.Length ? TimingIds[i] : "RESERVED";
                var cell = new GameObject("Cell_" + (i + 1).ToString("00") + "_" + id);
                SceneManager.MoveGameObjectToScene(cell, scene);
                cell.transform.position = new Vector3((column - (TimingColumns - 1) * .5f) * TimingCellWidth, (1f - row) * TimingCellHeight, 0f);
                CreateTimingBoundary(cell.transform, boundaryMaterial);
                if (i < TimingIds.Length)
                {
                    var path = "Assets/VFX/Generated/" + id + "/VFX_" + id + ".prefab";
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab == null) throw new InvalidOperationException("Missing W-C3 Runtime Entry: " + path);
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                    instance.transform.SetParent(cell.transform, false);
                    instance.transform.localPosition = new Vector3(TimingEntryOffsetX, TimingEntryOffsetY, 0f);
                    instance.transform.localScale = Vector3.one * TimingEntryScale;
                    var entry = instance.GetComponent<CapabilityBlankVfxController>();
                    if (entry == null || entry.TimingAreaVisual == null) throw new InvalidOperationException(path + " has no timing/area visual Runtime Entry.");
                    entries.Add(entry);
                    instance.SetActive(false);
                }
                CreateTimingLabel(cell.transform, i < TimingIds.Length ? (i + 1).ToString("00") + " " + ShortTimingId(id) : "RESERVED");
            }

            CreateTimingCandidateStatus(scene);

            var cameraObject = new GameObject("VFXPREVIEW_CapTiming_Camera");
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 3.55f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.022f, .025f, .034f, 1f);
            camera.allowHDR = false;
            camera.allowMSAA = false;
            camera.cullingMask = 1;
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);

            var driverObject = new GameObject("VFXPREVIEW_CapTiming_Driver");
            SceneManager.MoveGameObjectToScene(driverObject, scene);
            var driver = driverObject.AddComponent<TimingAreaCapabilityPreviewDriver>();
            var serialized = new SerializedObject(driver);
            var runtimeEntries = serialized.FindProperty("runtimeEntries");
            runtimeEntries.arraySize = entries.Count;
            for (var i = 0; i < entries.Count; i++) runtimeEntries.GetArrayElementAtIndex(i).objectReferenceValue = entries[i];
            serialized.FindProperty("chargeEntry").objectReferenceValue = entries[3];
            serialized.FindProperty("channelEntry").objectReferenceValue = entries[4];
            serialized.FindProperty("movingZoneEntry").objectReferenceValue = entries[8];
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, TimingScenePath);
            EnsureSceneIsLoadable(TimingScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void CreateTimingBoundary(Transform parent, Material material)
        {
            var value = new GameObject("CellBoundary");
            value.transform.SetParent(parent, false);
            var line = value.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Stretch;
            line.widthMultiplier = .014f;
            line.positionCount = 4;
            line.SetPosition(0, new Vector3(-TimingCellWidth * .5f, -TimingCellHeight * .5f, .08f));
            line.SetPosition(1, new Vector3(-TimingCellWidth * .5f, TimingCellHeight * .5f, .08f));
            line.SetPosition(2, new Vector3(TimingCellWidth * .5f, TimingCellHeight * .5f, .08f));
            line.SetPosition(3, new Vector3(TimingCellWidth * .5f, -TimingCellHeight * .5f, .08f));
            line.sharedMaterial = material;
            line.startColor = new Color(.24f, .29f, .36f, .58f);
            line.endColor = line.startColor;
            line.sortingOrder = -20;
        }

        private static void CreateTimingLabel(Transform parent, string text)
        {
            var value = new GameObject("Label");
            value.transform.SetParent(parent, false);
            value.transform.localPosition = new Vector3(0f, TimingLabelY, -.1f);
            var label = value.AddComponent<TextMesh>();
            label.text = text;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.fontSize = 40;
            label.characterSize = .021f;
            label.color = new Color(.64f, .69f, .76f, .92f);
            label.GetComponent<MeshRenderer>().sortingOrder = 100;
        }

        private static string ShortTimingId(string id)
        {
            return id.Replace("cap_", string.Empty).Replace("_impact_3d", string.Empty).Replace("_impact_2d", string.Empty).Replace("_area_3d", string.Empty).Replace("_area_2d", string.Empty).Replace("_3d", string.Empty).Replace("_2d", string.Empty);
        }

        private static void CreateTimingCandidateStatus(Scene scene)
        {
            var value = new GameObject(TimingCandidateStatusRootName);
            SceneManager.MoveGameObjectToScene(value, scene);
            value.transform.position = new Vector3(0f, 3.19f, -.2f);
            var label = value.AddComponent<TextMesh>();
            label.text = "W-C3 NEXT CANDIDATE - VISUAL SIGN-OFF PENDING\nBOUNDED 4 x 3 WALL | RUNTIME SLOTS | COMPLETE / CANCEL EXITS";
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.fontSize = 46;
            label.characterSize = .023f;
            label.color = new Color(.78f, .82f, .9f, .96f);
            label.GetComponent<MeshRenderer>().sortingOrder = 110;
        }

        private static Transform CreateWitness(Transform parent, string name, Vector3 localPosition, Vector3 localScale, bool keepCollider)
        {
            var value = GameObject.CreatePrimitive(PrimitiveType.Cube);
            value.name = name;
            value.transform.SetParent(parent, false);
            value.transform.localPosition = localPosition;
            value.transform.localScale = localScale;
            if (!keepCollider) UnityEngine.Object.DestroyImmediate(value.GetComponent<Collider>());
            return value.transform;
        }

        private static void CreateLabel(Transform parent, string text)
        {
            var value = new GameObject("Label");
            value.transform.SetParent(parent, false);
            value.transform.localPosition = new Vector3(0f, BeamLabelY, -.1f);
            var label = value.AddComponent<TextMesh>();
            label.text = text;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.fontSize = 40;
            label.characterSize = .024f;
            label.color = new Color(.64f, .69f, .76f, .92f);
            label.GetComponent<MeshRenderer>().sortingOrder = 100;
        }

        private static void CreateCandidateStatus(Scene scene)
        {
            var value = new GameObject(BeamCandidateStatusRootName);
            SceneManager.MoveGameObjectToScene(value, scene);
            value.transform.position = new Vector3(0f, 3.02f, -.2f);
            var label = value.AddComponent<TextMesh>();
            label.text = "W-C2 NEXT CANDIDATE - VISUAL SIGN-OFF PENDING\nRUNTIME ENDPOINTS | OBSTACLE PROBE | BOUNDED MULTI-LINE CARRIERS";
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.fontSize = 46;
            label.characterSize = .024f;
            label.color = new Color(.78f, .82f, .9f, .96f);
            label.GetComponent<MeshRenderer>().sortingOrder = 110;
        }

        private static void EnsureSceneIsLoadable(string scenePath)
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            var existing = scenes.FindIndex(value => value.path == scenePath);
            if (existing >= 0) scenes[existing] = new EditorBuildSettingsScene(scenePath, true);
            else scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VFXComposer.Editor.ValidationGallery
{
    public static class CoverageGalleryBScene
    {
        public const string ScenePath="Assets/VFX/Preview/VFXPREVIEW_CoverageGalleryB_3x3.unity";
        public const string ScreenUiScenePath="Assets/VFX/Preview/VFXPREVIEW_DamageWarningUI_Fullscreen.unity";
        private sealed class Cell{public string Label,Id;public Vector3 Position;public float Scale;public Cell(string label,string id,Vector3 position,float scale){Label=label;Id=id;Position=position;Scale=scale;}}

        [MenuItem("Tools/VFX Composer/Coverage Gallery B/Build + Open 3x3")]
        public static void BuildAndOpen(){BuildForBatch();EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);}

        [MenuItem("Tools/VFX Composer/Coverage Gallery B/Open Screen UI Fullscreen")]
        public static void BuildAndOpenScreenUi(){BuildForBatch();EditorSceneManager.OpenScene(ScreenUiScenePath,OpenSceneMode.Single);}

        public static void BuildForBatch()
        {
            CoverageGalleryBCompiler.BuildAll();ValidationGalleryCompiler.EnsureFolder("Assets/VFX/Preview");var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var entries=new List<MonoBehaviour>();var screenEntries=new List<CoverageGalleryVfxController>();
            foreach(var cell in Cells())
            {
                var cellRoot=new GameObject("Cell_"+cell.Label.Replace(" ","_"));SceneManager.MoveGameObjectToScene(cellRoot,scene);cellRoot.transform.position=cell.Position;
                var prefabPath="Assets/VFX/Generated/"+cell.Id+"/VFX_"+cell.Id+".prefab";var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);if(prefab==null)throw new InvalidOperationException("Missing coverage prefab: "+prefabPath);var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);instance.name="Entry_"+cell.Label.Replace(" ","_");instance.transform.SetParent(cellRoot.transform,false);instance.transform.localPosition=new Vector3(0,.12f,0);instance.transform.localScale=Vector3.one*cell.Scale;var entry=instance.GetComponent<CoverageGalleryVfxController>();if(entry==null)throw new InvalidOperationException(prefabPath+" has no CoverageGalleryVfxController.");entries.Add(entry);if(entry.Profile==CoverageGalleryProfile.ScreenUi)screenEntries.Add(entry);instance.SetActive(false);CreateLabel(cellRoot.transform,cell.Label);
            }
            var cameraObject=new GameObject("VFXPREVIEW_CoverageGalleryB_Camera");SceneManager.MoveGameObjectToScene(cameraObject,scene);cameraObject.tag="MainCamera";var camera=cameraObject.AddComponent<Camera>();camera.orthographic=false;camera.fieldOfView=42f;camera.nearClipPlane=.1f;camera.farClipPlane=50;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.018f,.022f,.03f,1);camera.allowHDR=false;camera.allowMSAA=false;camera.cullingMask=1;cameraObject.transform.position=new Vector3(0,.15f,-13.2f);cameraObject.transform.rotation=Quaternion.Euler(1.1f,0,0);
            foreach(var entry in screenEntries){var canvas=entry.GetComponent<Canvas>();if(canvas!=null){canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;}var safe=entry.transform.Find("ScreenFeedbackSafeArea") as RectTransform;if(safe!=null){safe.anchorMin=new Vector2(2f/3f,0);safe.anchorMax=new Vector2(1,1f/3f);safe.offsetMin=new Vector2(28,78);safe.offsetMax=new Vector2(-28,-22);}}
            var driverObject=new GameObject("VFXPREVIEW_CoverageGalleryB_Driver");SceneManager.MoveGameObjectToScene(driverObject,scene);var driver=driverObject.AddComponent<CoverageGalleryBPlaybackDriver>();var serialized=new SerializedObject(driver);var property=serialized.FindProperty("runtimeEntries");property.arraySize=entries.Count;for(var i=0;i<entries.Count;i++)property.GetArrayElementAtIndex(i).objectReferenceValue=entries[i];serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(scene,ScenePath);BuildScreenUiPreview();EnsureSceneIsLoadable(ScenePath);EnsureSceneIsLoadable(ScreenUiScenePath);AssetDatabase.SaveAssets();AssetDatabase.Refresh();
        }

        private static void BuildScreenUiPreview()
        {
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/VFX/Generated/damage_warning_ui/VFX_damage_warning_ui.prefab");if(prefab==null)throw new InvalidOperationException("Missing Screen/UI Runtime Entry.");var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);instance.name="VFX_DamageWarningUI_Fullscreen";var entry=instance.GetComponent<CoverageGalleryVfxController>();instance.SetActive(false);
            var cameraObject=new GameObject("VFXPREVIEW_DamageWarningUI_Camera");SceneManager.MoveGameObjectToScene(cameraObject,scene);cameraObject.tag="MainCamera";var camera=cameraObject.AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=5;camera.nearClipPlane=.1f;camera.farClipPlane=20;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.018f,.022f,.03f,1);camera.allowHDR=false;camera.allowMSAA=false;cameraObject.transform.position=new Vector3(0,0,-10);
            var canvas=instance.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;
            var driverObject=new GameObject("VFXPREVIEW_DamageWarningUI_Driver");SceneManager.MoveGameObjectToScene(driverObject,scene);var driver=driverObject.AddComponent<CoverageGalleryBPlaybackDriver>();var serialized=new SerializedObject(driver);var property=serialized.FindProperty("runtimeEntries");property.arraySize=1;property.GetArrayElementAtIndex(0).objectReferenceValue=entry;serialized.ApplyModifiedPropertiesWithoutUndo();EditorSceneManager.SaveScene(scene,ScreenUiScenePath);
        }

        private static Cell[] Cells()
        {
            const float x=3.9f,y=2.48f;return new[]{new Cell("1 3D IMPACT","meteor_impact_3d",new Vector3(-x,y,0),1.12f),new Cell("2 3D AURA","astral_aura_3d",new Vector3(0,y,0),1.08f),new Cell("3 3D AREA","toxic_field_3d",new Vector3(x,y,0),1.08f),new Cell("4 3D BEAM","plasma_link_3d",new Vector3(-x,0,0),1.14f),new Cell("5 3D TRAIL","spectral_trail_3d",new Vector3(0,0,0),1.08f),new Cell("6 3D SHIELD","prismatic_shield_3d",new Vector3(x,0,0),1.05f),new Cell("7 3D SPAWN","rift_spawn_3d",new Vector3(-x,-y,0),1.08f),new Cell("8 ENVIRONMENT","snow_weather_volume",new Vector3(0,-y,0),1.08f),new Cell("9 SCREEN UI","damage_warning_ui",new Vector3(x,-y,0),1f)};
        }

        private static void CreateLabel(Transform parent,string text){var go=new GameObject("Label");go.transform.SetParent(parent,false);go.transform.localPosition=new Vector3(0,-.88f,-.12f);var label=go.AddComponent<TextMesh>();label.text=text;label.anchor=TextAnchor.MiddleCenter;label.alignment=TextAlignment.Center;label.fontSize=48;label.characterSize=.032f;label.color=new Color(.64f,.69f,.76f,.92f);label.GetComponent<MeshRenderer>().sortingOrder=100;}
        private static void EnsureSceneIsLoadable(string path){var scenes=EditorBuildSettings.scenes.ToList();var index=scenes.FindIndex(value=>value.path==path);if(index>=0)scenes[index]=new EditorBuildSettingsScene(path,true);else scenes.Add(new EditorBuildSettingsScene(path,true));EditorBuildSettings.scenes=scenes.ToArray();}
    }
}

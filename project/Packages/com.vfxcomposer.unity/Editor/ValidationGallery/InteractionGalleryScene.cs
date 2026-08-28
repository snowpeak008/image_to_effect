using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VFXComposer.Editor.ValidationGallery
{
    public static class InteractionGalleryScene
    {
        public const string ScenePath="Assets/VFX/Preview/VFXPREVIEW_InteractionGallery_3x3.unity";
        private sealed class Cell{public string Label,Id;public Vector3 Position;public float Scale;public Cell(string label,string id,Vector3 position,float scale){Label=label;Id=id;Position=position;Scale=scale;}}

        [MenuItem("Tools/VFX Composer/Interaction Gallery/Build + Open 3x3")]
        public static void BuildAndOpen(){BuildForBatch();EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);}
        public static void BuildForBatch()
        {
            InteractionGalleryCompiler.BuildAll();ValidationGalleryCompiler.EnsureFolder("Assets/VFX/Preview");var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var entries=new List<MonoBehaviour>();
            foreach(var cell in Cells())
            {
                var cellRoot=new GameObject("Cell_"+cell.Label.Replace(" ","_"));SceneManager.MoveGameObjectToScene(cellRoot,scene);cellRoot.transform.position=cell.Position;var path="Assets/VFX/Generated/"+cell.Id+"/VFX_"+cell.Id+".prefab";var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(prefab==null)throw new InvalidOperationException("Missing interaction prefab: "+path);var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);instance.name="Entry_"+cell.Label.Replace(" ","_");instance.transform.SetParent(cellRoot.transform,false);instance.transform.localPosition=new Vector3(0,.12f,0);instance.transform.localScale=Vector3.one*cell.Scale;var entry=instance.GetComponent<InteractionGalleryVfxController>();entries.Add(entry);instance.SetActive(false);CreateLabel(cellRoot.transform,cell.Label);
            }
            var cameraObject=new GameObject("VFXPREVIEW_InteractionGallery_Camera");SceneManager.MoveGameObjectToScene(cameraObject,scene);cameraObject.tag="MainCamera";var camera=cameraObject.AddComponent<Camera>();camera.orthographic=false;camera.fieldOfView=42;camera.nearClipPlane=.1f;camera.farClipPlane=50;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.018f,.022f,.03f,1);camera.allowHDR=false;camera.allowMSAA=false;camera.cullingMask=1;cameraObject.transform.position=new Vector3(0,.15f,-13.2f);cameraObject.transform.rotation=Quaternion.Euler(1.1f,0,0);
            var driverObject=new GameObject("VFXPREVIEW_InteractionGallery_Driver");SceneManager.MoveGameObjectToScene(driverObject,scene);var driver=driverObject.AddComponent<InteractionGalleryPlaybackDriver>();var serialized=new SerializedObject(driver);var property=serialized.FindProperty("runtimeEntries");property.arraySize=entries.Count;for(var i=0;i<entries.Count;i++)property.GetArrayElementAtIndex(i).objectReferenceValue=entries[i];serialized.ApplyModifiedPropertiesWithoutUndo();EditorSceneManager.SaveScene(scene,ScenePath);EnsureSceneIsLoadable();AssetDatabase.SaveAssets();AssetDatabase.Refresh();
        }

        private static Cell[] Cells(){const float x=3.9f,y=2.48f;return new[]{new Cell("1 CHARGE","focus_charge_3d",new Vector3(-x,y,0),1.02f),new Cell("2 CHANNEL","channel_tether_3d",new Vector3(0,y,0),1.05f),new Cell("3 TELEGRAPH","warning_telegraph_3d",new Vector3(x,y,0),1.05f),new Cell("4 CHAIN","chain_arc_3d",new Vector3(-x,0,0),1.05f),new Cell("5 HOMING","seeker_orb_3d",new Vector3(0,0,0),1.06f),new Cell("6 WEAPON ATTACH","weapon_enchant_3d",new Vector3(x,0,0),1.0f),new Cell("7 DASH","phase_dash_3d",new Vector3(-x,-y,0),1.03f),new Cell("8 TRANSFORM","dissolve_transform_3d",new Vector3(0,-y,0),1.02f),new Cell("9 MULTI-STAGE","ultimate_sequence_3d",new Vector3(x,-y,0),1.04f)};}
        private static void CreateLabel(Transform parent,string text){var go=new GameObject("Label");go.transform.SetParent(parent,false);go.transform.localPosition=new Vector3(0,-.88f,-.12f);var label=go.AddComponent<TextMesh>();label.text=text;label.anchor=TextAnchor.MiddleCenter;label.alignment=TextAlignment.Center;label.fontSize=48;label.characterSize=.032f;label.color=new Color(.64f,.69f,.76f,.92f);label.GetComponent<MeshRenderer>().sortingOrder=100;}
        private static void EnsureSceneIsLoadable(){var scenes=EditorBuildSettings.scenes.ToList();var index=scenes.FindIndex(value=>value.path==ScenePath);if(index>=0)scenes[index]=new EditorBuildSettingsScene(ScenePath,true);else scenes.Add(new EditorBuildSettingsScene(ScenePath,true));EditorBuildSettings.scenes=scenes.ToArray();}
    }
}

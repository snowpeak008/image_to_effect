using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VFXComposer.Editor.ValidationGallery
{
    public static class ValidationGalleryScene
    {
        public const string ScenePath = "Assets/VFX/Preview/VFXPREVIEW_ValidationGallery_3x3.unity";

        private sealed class Cell
        {
            public string Label, PrefabPath; public Vector3 Position; public float Scale;
            public Cell(string label,string prefabPath,Vector3 position,float scale){Label=label;PrefabPath=prefabPath;Position=position;Scale=scale;}
        }

        [MenuItem("Tools/VFX Composer/Validation Gallery/Build + Open 3x3 Gallery")]
        public static void BuildAndOpen() { BuildForBatch(); EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single); }

        public static void BuildForBatch()
        {
            ValidationGalleryCompiler.BuildAll();
            ValidationGalleryCompiler.EnsureFolder("Assets/VFX/Preview");
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var cells=Cells(); var entries=new List<MonoBehaviour>();
            foreach(var cell in cells)
            {
                var cellRoot=new GameObject("Cell_"+cell.Label.Replace(" ","_")); SceneManager.MoveGameObjectToScene(cellRoot,scene); cellRoot.transform.position=cell.Position;
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(cell.PrefabPath); if(prefab==null)throw new InvalidOperationException("Missing gallery Runtime Prefab: "+cell.PrefabPath);
                var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene); instance.name="Entry_"+cell.Label.Replace(" ","_"); instance.transform.SetParent(cellRoot.transform,false); instance.transform.localPosition=new Vector3(0,.12f,0); instance.transform.localScale=Vector3.one*cell.Scale;
                var entry=instance.GetComponents<MonoBehaviour>().FirstOrDefault(value=>value is IVfxRuntimeEntry); if(entry==null)throw new InvalidOperationException(cell.PrefabPath+" has no root IVfxRuntimeEntry."); entries.Add(entry); instance.SetActive(false);
                CreateLabel(cellRoot.transform,cell.Label);
            }

            var cameraObject=new GameObject("VFXPREVIEW_ValidationGallery_Camera"); SceneManager.MoveGameObjectToScene(cameraObject,scene); cameraObject.tag="MainCamera"; var camera=cameraObject.AddComponent<Camera>(); camera.orthographic=true; camera.orthographicSize=3.65f; camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.022f,.025f,.034f,1f); camera.allowHDR=false; camera.allowMSAA=false; camera.cullingMask=1; cameraObject.transform.position=new Vector3(0,0,-10);
            var driverObject=new GameObject("VFXPREVIEW_ValidationGallery_Driver"); SceneManager.MoveGameObjectToScene(driverObject,scene); var driver=driverObject.AddComponent<ValidationGalleryPlaybackDriver>(); var so=new SerializedObject(driver); var property=so.FindProperty("runtimeEntries"); property.arraySize=entries.Count; for(var i=0;i<entries.Count;i++)property.GetArrayElementAtIndex(i).objectReferenceValue=entries[i]; so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(scene,ScenePath); EnsureSceneIsLoadable(); AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        }

        private static Cell[] Cells()
        {
            const float x=3.7f,y=2.25f;
            return new[]{
                new Cell("1 PROJECTILE","Assets/VFX/Generated/fireball_2d/VFX_Fireball_2D.prefab",new Vector3(-x,y,0),.42f),
                new Cell("2 IMPACT","Assets/VFX/Generated/frost_impact_2d/VFX_frost_impact_2d.prefab",new Vector3(0,y,0),.31f),
                new Cell("3 SLASH","Assets/VFX/Generated/slash_3d_stylized/VFX_Slash_3D_Stylized.prefab",new Vector3(x,y,0),.32f),
                new Cell("4 AURA",Prefab("guardian_aura_2d"),new Vector3(-x,0,0),.78f),
                new Cell("5 AREA","Assets/VFX/Generated/inferno_vortex_area_2d/VFX_inferno_vortex_area_2d.prefab",new Vector3(0,0,0),.4f),
                new Cell("6 BEAM",Prefab("arc_lightning_beam_2d"),new Vector3(x,0,0),.78f),
                new Cell("7 TRAIL",Prefab("comet_motion_trail_2d"),new Vector3(-x,-y,0),.78f),
                new Cell("8 SHIELD",Prefab("hex_guard_shield_2d"),new Vector3(0,-y,0),.78f),
                new Cell("9 SPAWN",Prefab("summoning_portal_2d"),new Vector3(x,-y,0),.78f)
            };
        }

        private static string Prefab(string id){return "Assets/VFX/Generated/"+id+"/VFX_"+id+".prefab";}
        private static void CreateLabel(Transform parent,string text)
        {
            var go=new GameObject("Label"); go.transform.SetParent(parent,false); go.transform.localPosition=new Vector3(0,-.82f,-.1f); var label=go.AddComponent<TextMesh>(); label.text=text; label.anchor=TextAnchor.MiddleCenter; label.alignment=TextAlignment.Center; label.fontSize=48; label.characterSize=.035f; label.color=new Color(.64f,.69f,.76f,.92f); label.GetComponent<MeshRenderer>().sortingOrder=100;
        }

        private static void EnsureSceneIsLoadable()
        {
            var scenes=EditorBuildSettings.scenes.ToList(); var index=scenes.FindIndex(value=>value.path==ScenePath);
            if(index>=0)scenes[index]=new EditorBuildSettingsScene(ScenePath,true);else scenes.Add(new EditorBuildSettingsScene(ScenePath,true)); EditorBuildSettings.scenes=scenes.ToArray();
        }
    }
}

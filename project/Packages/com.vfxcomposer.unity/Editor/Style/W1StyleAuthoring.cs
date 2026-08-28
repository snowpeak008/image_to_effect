using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VFXComposer.Editor.ValidationGallery;

namespace VFXComposer.Editor.Style
{
    public static class W1StyleAuthoring
    {
        public const string PreviewScenePath = "Assets/VFX/Preview/VFXPREVIEW_W1_StyleSamples.unity";
        public static readonly string[] SampleRecipePaths =
        {
            "Assets/VFX/Recipes/StyleSamples/fireball_2d.cartoon.json",
            "Assets/VFX/Recipes/StyleSamples/fireball_2d.neon.json",
            "Assets/VFX/Recipes/StyleSamples/frost_impact_2d.dark.json",
            "Assets/VFX/Recipes/Capability/Demos/cap_demo_fan_wave_cartoon_2d.default.json",
            "Assets/VFX/Recipes/Capability/Demos/cap_demo_charge_occlude_holo_3d.default.json",
            "Assets/VFX/Recipes/Capability/Demos/cap_demo_telegraph_nova_holy_3d.default.json"
        };

        [MenuItem("Tools/VFX Composer/Style/Build W1 Samples and Preview")]
        public static void BuildAllMenu() { BuildAll(); Debug.Log("W1 style samples and Preview Scene are current. User visual sign-off remains deferred."); }

        public static void BuildAll()
        {
            foreach (var path in SampleRecipePaths)
            {
                var result=StyledContentCompiler.BuildAsset(path);if(!result.Succeeded)throw new InvalidOperationException(path+": "+string.Join(" | ",result.Report.Entries.Select(value=>value.Code+" "+value.Path+" "+value.Message).ToArray()));
            }
            BuildPreviewScene(); AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        }

        public static void BuildPreviewScene()
        {
            ValidationGalleryCompiler.EnsureFolder("Assets/VFX/Preview");var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var entries=new List<MonoBehaviour>();
            var cells=new[]
            {
                new Cell("Stylized baseline","Assets/VFX/Generated/fireball_2d/VFX_Fireball_2D.prefab",new Vector3(-2.4f,1.5f,0)),
                new Cell("Cartoon","Assets/VFX/Generated/fireball_2d_cartoon/VFX_fireball_2d_cartoon.prefab",new Vector3(0,1.5f,0)),
                new Cell("Neon","Assets/VFX/Generated/fireball_2d_neon/VFX_fireball_2d_neon.prefab",new Vector3(2.4f,1.5f,0)),
                new Cell("Dark frost","Assets/VFX/Generated/frost_impact_2d_dark/VFX_frost_impact_2d_dark.prefab",new Vector3(-2.4f,0,0)),
                new Cell("Fan + Wave / Cartoon","Assets/VFX/Generated/cap_demo_fan_wave_cartoon_2d/VFX_cap_demo_fan_wave_cartoon_2d.prefab",new Vector3(0,0,0)),
                new Cell("Charge + Occlude / Holo","Assets/VFX/Generated/cap_demo_charge_occlude_holo_3d/VFX_cap_demo_charge_occlude_holo_3d.prefab",new Vector3(2.4f,0,0)),
                new Cell("Telegraph + Nova / Holy","Assets/VFX/Generated/cap_demo_telegraph_nova_holy_3d/VFX_cap_demo_telegraph_nova_holy_3d.prefab",new Vector3(0,-1.5f,0))
            };
            for(var i=0;i<cells.Length;i++)
            {
                var source=AssetDatabase.LoadAssetAtPath<GameObject>(cells[i].PrefabPath);if(source==null)throw new InvalidOperationException("Missing W1 sample Prefab: "+cells[i].PrefabPath);var holder=new GameObject("Cell_"+(i+1).ToString("00")+"_"+cells[i].Label.Replace(' ','_'));holder.transform.position=cells[i].Position;var instance=(GameObject)PrefabUtility.InstantiatePrefab(source,holder.transform);instance.transform.localPosition=Vector3.zero;var entry=instance.GetComponents<MonoBehaviour>().FirstOrDefault(value=>value is IVfxRuntimeEntry);if(entry==null)throw new InvalidOperationException(source.name+" has no root Runtime Entry.");entries.Add(entry);AddLabel(holder.transform,cells[i].Label);
            }
            var cameraObject=new GameObject("W1StyleReviewCamera");cameraObject.tag="MainCamera";var camera=cameraObject.AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=3.05f;camera.transform.position=new Vector3(0,0,-10);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.025f,.03f,.045f);camera.allowHDR=false;camera.allowMSAA=false;
            var driverObject=new GameObject("W1StylePreviewDriver");var driver=driverObject.AddComponent<ValidationGalleryPlaybackDriver>();var serialized=new SerializedObject(driver);var property=serialized.FindProperty("runtimeEntries");property.arraySize=entries.Count;for(var i=0;i<entries.Count;i++)property.GetArrayElementAtIndex(i).objectReferenceValue=entries[i];serialized.FindProperty("cycleDuration").floatValue=3.5f;serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(scene,PreviewScenePath);EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        }

        private static void AddLabel(Transform parent,string text)
        {
            var go=new GameObject("Label");go.transform.SetParent(parent,false);go.transform.localPosition=new Vector3(0,-.83f,0);var label=go.AddComponent<TextMesh>();label.text=text;label.anchor=TextAnchor.MiddleCenter;label.alignment=TextAlignment.Center;label.fontSize=34;label.characterSize=.045f;label.color=new Color(.72f,.78f,.88f);
        }
        private sealed class Cell{public string Label,PrefabPath;public Vector3 Position;public Cell(string label,string path,Vector3 position){Label=label;PrefabPath=path;Position=position;}}
    }
}

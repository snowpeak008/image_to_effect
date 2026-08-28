using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VFXComposer.Editor.Elements
{
    /// <summary>W6-W8-only authoring. It cannot select, rebuild or overwrite the W3-W5 cohort.</summary>
    public static class ElementNextCandidateW6W8Authoring
    {
        public const string W6ScenePath="Assets/VFX/Preview/VFXPREVIEW_WaterWindFamily_NextCandidate.unity";
        public const string W7ScenePath="Assets/VFX/Preview/VFXPREVIEW_EarthNatureToxicFamily_NextCandidate.unity";
        public const string W8ScenePath="Assets/VFX/Preview/VFXPREVIEW_HolyShadowArcaneFamily_NextCandidate.unity";
        public const string W6StatusRoot="W6_NEXT_CANDIDATE_VISUAL_PENDING";
        public const string W7StatusRoot="W7_NEXT_CANDIDATE_VISUAL_PENDING";
        public const string W8StatusRoot="W8_NEXT_CANDIDATE_VISUAL_PENDING";

        private static readonly string[] W6Families={"water","wind"};
        private static readonly string[] W7Families={"earth","nature","toxic"};
        private static readonly string[] W8Families={"holy","shadow","arcane"};

        [MenuItem("Tools/VFX Composer/Elements/Next Candidate/Build W6-W8 Only (Batch Safe)")]
        public static void BuildW6W8ForBatch(){BuildGroup("w6");BuildGroup("w7");BuildGroup("w8");AssetDatabase.SaveAssets();AssetDatabase.Refresh();Debug.Log("W6-W8 next candidates built as VISUAL_PENDING. W3-W5, old rejected outputs and user verdicts were not written.");}
        [MenuItem("Tools/VFX Composer/Elements/Next Candidate/Build W6 Water-Wind Only (Batch Safe)")]
        public static void BuildW6ForBatch(){BuildGroup("w6");AssetDatabase.SaveAssets();AssetDatabase.Refresh();}
        [MenuItem("Tools/VFX Composer/Elements/Next Candidate/Build W7 Earth-Nature-Toxic Only (Batch Safe)")]
        public static void BuildW7ForBatch(){BuildGroup("w7");AssetDatabase.SaveAssets();AssetDatabase.Refresh();}
        [MenuItem("Tools/VFX Composer/Elements/Next Candidate/Build W8 Holy-Shadow-Arcane Only (Batch Safe)")]
        public static void BuildW8ForBatch(){BuildGroup("w8");AssetDatabase.SaveAssets();AssetDatabase.Refresh();}

        // No-argument elemental-family entry points are intentionally executeMethod-friendly.
        public static void BuildWaterForBatch(){BuildSingle("water");}
        public static void BuildWindForBatch(){BuildSingle("wind");}
        public static void BuildEarthForBatch(){BuildSingle("earth");}
        public static void BuildNatureForBatch(){BuildSingle("nature");}
        public static void BuildToxicForBatch(){BuildSingle("toxic");}
        public static void BuildHolyForBatch(){BuildSingle("holy");}
        public static void BuildShadowForBatch(){BuildSingle("shadow");}
        public static void BuildArcaneForBatch(){BuildSingle("arcane");}

        [MenuItem("Tools/VFX Composer/Elements/Next Candidate/Build and Open W6 Water-Wind")]
        public static void BuildAndOpenW6(){BuildW6ForBatch();EditorSceneManager.OpenScene(W6ScenePath,OpenSceneMode.Single);}
        [MenuItem("Tools/VFX Composer/Elements/Next Candidate/Build and Open W7 Earth-Nature-Toxic")]
        public static void BuildAndOpenW7(){BuildW7ForBatch();EditorSceneManager.OpenScene(W7ScenePath,OpenSceneMode.Single);}
        [MenuItem("Tools/VFX Composer/Elements/Next Candidate/Build and Open W8 Holy-Shadow-Arcane")]
        public static void BuildAndOpenW8(){BuildW8ForBatch();EditorSceneManager.OpenScene(W8ScenePath,OpenSceneMode.Single);}

        public static ElementNextCandidateBuildResult[] BuildGroup(string group)
        {
            var families=Families(group);var results=new List<ElementNextCandidateBuildResult>();
            foreach(var family in families)results.AddRange(BuildElementFamily(family));
            var values=results.ToArray();BuildPreview(group,values);return values;
        }

        /// <summary>Isolated elemental-family entry for CI and Patch transaction staging.</summary>
        public static ElementNextCandidateBuildResult[] BuildElementFamily(string family)
        {
            if(!W6Families.Concat(W7Families).Concat(W8Families).Contains(family,StringComparer.Ordinal))throw new ArgumentOutOfRangeException("family","W6-W8 authoring accepts water, wind, earth, nature, toxic, holy, shadow or arcane only.");
            var entries=ElementFamilyCatalog.Family(family).ToArray();var results=new ElementNextCandidateBuildResult[entries.Length];
            for(var index=0;index<entries.Length;index++){results[index]=ElementNextCandidateCompiler.BuildAsset(RecipePath(entries[index]));if(!results[index].Succeeded)throw new InvalidOperationException(entries[index].Id+": "+Describe(results[index]));}
            return results;
        }

        private static void BuildSingle(string family){BuildElementFamily(family);AssetDatabase.SaveAssets();AssetDatabase.Refresh();}

        public static string RecipePath(ElementContentEntry entry)
        {
            var folder=entry.Family=="water"||entry.Family=="wind"?"WaterWind":entry.Family=="earth"||entry.Family=="nature"||entry.Family=="toxic"?"EarthNatureToxic":"Magic";
            return ElementFamilyAuthoring.RecipeRoot+"/"+folder+"/"+entry.Id+".default.json";
        }

        public static string ScenePath(string group){return group=="w6"?W6ScenePath:group=="w7"?W7ScenePath:group=="w8"?W8ScenePath:throw new ArgumentOutOfRangeException("group");}
        public static string CandidateStatusRoot(string group){return group=="w6"?W6StatusRoot:group=="w7"?W7StatusRoot:group=="w8"?W8StatusRoot:throw new ArgumentOutOfRangeException("group");}
        public static float DisplayScale(ElementNextCandidatePlan plan){return Mathf.Min(.72f,ElementNextCandidateAuthoring.CellEffectHalfExtent/Mathf.Max(.01f,plan.MaxLocalExtent));}

        private static string[] Families(string group){return group=="w6"?W6Families:group=="w7"?W7Families:group=="w8"?W8Families:throw new ArgumentOutOfRangeException("group");}

        private static void BuildPreview(string group,ElementNextCandidateBuildResult[] results)
        {
            var scenePath=ScenePath(group);var statusRoot=CandidateStatusRoot(group);var previewHash=PreviewHash(group,results);var receiptPath=ElementNextCandidateCompiler.CandidateRootW6W8+"/Preview/"+group+"-preview.json";EnsureFolder(Path.GetDirectoryName(receiptPath).Replace('\\','/'));
            if(PreviewIsCurrent(scenePath,receiptPath,previewHash)){EnsureSceneLoadable(scenePath);return;}
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var entries=new List<ElementNextCandidateVisualExecutor>();CreateStatusMarker(statusRoot,group);
            for(var index=0;index<results.Length;index++)
            {
                var plan=results[index].Plan;var row=index/3;var column=index%3;var cellObject=new GameObject("Cell_"+(index+1).ToString("00",CultureInfo.InvariantCulture)+"_"+plan.EffectId);cellObject.transform.position=new Vector3((column-1)*ElementNextCandidateAuthoring.CellWidth,2.5f-row*ElementNextCandidateAuthoring.CellHeight,0f);
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(ElementNextCandidateCompiler.PrefabPath(plan.EffectId));if(prefab==null)throw new InvalidOperationException("Missing W6-W8 next-candidate Prefab: "+plan.EffectId);
                var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab,cellObject.transform);instance.name="Runtime_"+plan.EffectId+"_NEXT";instance.transform.localPosition=new Vector3(0f,.16f,0f);instance.transform.localScale=Vector3.one*DisplayScale(plan);
                var definition=ElementFamilyCatalog.All.First(value=>value.Id==plan.EffectId);if(definition.Dimension=="3d")instance.transform.localRotation=Quaternion.Euler(17f,-22f,0f);
                var entry=instance.GetComponent<ElementNextCandidateVisualExecutor>();if(entry==null)throw new InvalidOperationException(plan.EffectId+" has no dedicated next-candidate executor.");entries.Add(entry);ConfigureCell(cellObject.AddComponent<ElementNextCandidateCell>(),index+1,plan,entry);CreateLabel(cellObject.transform,(index+1).ToString("00",CultureInfo.InvariantCulture)+"  "+plan.EffectId+"\n"+plan.ShapeToken.Replace('_',' '));
            }
            CreateCamera();CreateDriver(entries);if(!EditorSceneManager.SaveScene(scene,scenePath))throw new InvalidOperationException("Could not save W6-W8 next-candidate Preview Scene: "+scenePath);
            WriteIfChanged(receiptPath,new JObject{{"previewVersion",1},{"group",group},{"families",new JArray(Families(group))},{"scenePath",scenePath},{"candidateStatusRoot",statusRoot},{"visualStatus",ElementNextCandidatePlanCompiler.VisualStatus},{"previewHash",previewHash},{"cellCount",results.Length},{"compilerVersion",ElementNextCandidatePlanCompiler.CompilerVersionW6W8},{"machineEvidenceIsVisualAcceptance",false}}.ToString(Formatting.Indented)+"\n");AssetDatabase.ImportAsset(receiptPath,ImportAssetOptions.ForceSynchronousImport);EnsureSceneLoadable(scenePath);
        }

        private static void ConfigureCell(ElementNextCandidateCell cell,int index,ElementNextCandidatePlan plan,ElementNextCandidateVisualExecutor entry)
        {
            var serialized=new SerializedObject(cell);serialized.FindProperty("cellIndex").intValue=index;serialized.FindProperty("effectId").stringValue=plan.EffectId;serialized.FindProperty("fullBounds").rectValue=ElementNextCandidateAuthoring.FullCellBounds;serialized.FindProperty("effectBounds").rectValue=ElementNextCandidateAuthoring.EffectBounds;serialized.FindProperty("labelBounds").rectValue=ElementNextCandidateAuthoring.LabelBounds;serialized.FindProperty("authoredDisplayScale").floatValue=DisplayScale(plan);serialized.FindProperty("compiledLocalExtent").floatValue=plan.MaxLocalExtent;serialized.FindProperty("entry").objectReferenceValue=entry;serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreateStatusMarker(string rootName,string group)
        {
            var root=new GameObject(rootName);root.transform.position=new Vector3(0f,4.46f,0f);var text=root.AddComponent<TextMesh>();text.text=rootName+"\nNEXT CANDIDATE / VISUAL SIGN-OFF PENDING / MACHINE BUILD ONLY";text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;text.fontSize=34;text.characterSize=.025f;text.color=group=="w6"?new Color(.32f,.82f,.82f):group=="w7"?new Color(.46f,.72f,.28f):new Color(.76f,.55f,1f);text.GetComponent<MeshRenderer>().sortingOrder=200;
        }

        private static void CreateLabel(Transform parent,string value){var labelObject=new GameObject("Label");labelObject.transform.SetParent(parent,false);labelObject.transform.localPosition=new Vector3(0f,ElementNextCandidateAuthoring.LabelY,-.08f);var text=labelObject.AddComponent<TextMesh>();text.text=value;text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;text.fontSize=32;text.characterSize=.0235f;text.color=new Color(.68f,.75f,.84f);text.GetComponent<MeshRenderer>().sortingOrder=190;}
        private static void CreateCamera(){var cameraObject=new GameObject("W6W8NextCandidateReviewCamera");cameraObject.tag="MainCamera";var camera=cameraObject.AddComponent<Camera>();camera.transform.position=new Vector3(0f,.18f,-14f);camera.transform.rotation=Quaternion.identity;camera.orthographic=true;camera.orthographicSize=4.95f;camera.nearClipPlane=.05f;camera.farClipPlane=40f;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.012f,.016f,.025f);camera.allowHDR=false;camera.allowMSAA=false;}
        private static void CreateDriver(List<ElementNextCandidateVisualExecutor> entries){var driverObject=new GameObject("ElementNextCandidateW6W8PreviewDriver");var driver=driverObject.AddComponent<ElementNextCandidatePreviewDriver>();var serialized=new SerializedObject(driver);var property=serialized.FindProperty("entries");property.arraySize=entries.Count;for(var index=0;index<entries.Count;index++)property.GetArrayElementAtIndex(index).objectReferenceValue=entries[index];serialized.FindProperty("replayInterval").floatValue=4.2f;serialized.FindProperty("sustainedStopTime").floatValue=3.25f;serialized.FindProperty("triggerEventDriven").boolValue=true;serialized.FindProperty("eventTriggerTime").floatValue=1.15f;serialized.ApplyModifiedPropertiesWithoutUndo();}
        private static bool PreviewIsCurrent(string scenePath,string receiptPath,string hash){if(AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath)==null)return false;var absolute=Absolute(receiptPath);if(!File.Exists(absolute))return false;try{var root=JObject.Parse(File.ReadAllText(absolute));return(string)root["previewHash"]==hash&&(string)root["visualStatus"]==ElementNextCandidatePlanCompiler.VisualStatus;}catch{return false;}}
        private static string PreviewHash(string group,IEnumerable<ElementNextCandidateBuildResult> results){var value=group+"|"+ElementNextCandidatePlanCompiler.CompilerVersionW6W8+"|"+ElementNextCandidateAuthoring.FullCellBounds+"|"+ElementNextCandidateAuthoring.EffectBounds+"|"+ElementNextCandidateAuthoring.LabelBounds+"|"+string.Join("|",results.OrderBy(item=>item.Plan.EffectId,StringComparer.Ordinal).Select(item=>item.Plan.EffectId+":"+item.BuildHash).ToArray());using(var sha=SHA256.Create())return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(value)).Select(item=>item.ToString("x2",CultureInfo.InvariantCulture)).ToArray());}
        private static void EnsureSceneLoadable(string scenePath){var scenes=EditorBuildSettings.scenes.ToList();if(scenes.Any(value=>value.path==scenePath))return;scenes.Add(new EditorBuildSettingsScene(scenePath,true));EditorBuildSettings.scenes=scenes.ToArray();}
        private static string Describe(ElementNextCandidateBuildResult result){return string.Join(" | ",result.Report.Entries.Select(value=>value.Code+" "+value.Path+" "+value.Message).ToArray());}
        private static void WriteIfChanged(string path,string value){var absolute=Absolute(path);Directory.CreateDirectory(Path.GetDirectoryName(absolute));if(File.Exists(absolute)&&File.ReadAllText(absolute)==value)return;File.WriteAllText(absolute,value,new UTF8Encoding(false));}
        private static void EnsureFolder(string path){if(AssetDatabase.IsValidFolder(path))return;var parent=Path.GetDirectoryName(path).Replace('\\','/');if(!AssetDatabase.IsValidFolder(parent))EnsureFolder(parent);AssetDatabase.CreateFolder(parent,Path.GetFileName(path));}
        private static string Absolute(string path){return Path.GetFullPath(Path.Combine(Directory.GetParent(Application.dataPath).FullName,path.Replace('/',Path.DirectorySeparatorChar)));}
    }
}

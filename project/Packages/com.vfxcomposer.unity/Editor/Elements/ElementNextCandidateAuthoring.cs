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
    /// <summary>W3-W5-only candidate authoring.  No legacy output or W6+ recipe is an owned target.</summary>
    public static class ElementNextCandidateAuthoring
    {
        public const string FireScenePath = "Assets/VFX/Preview/VFXPREVIEW_FireFamily_NextCandidate.unity";
        public const string FrostScenePath = "Assets/VFX/Preview/VFXPREVIEW_FrostFamily_NextCandidate.unity";
        public const string LightningScenePath = "Assets/VFX/Preview/VFXPREVIEW_LightningFamily_NextCandidate.unity";
        public const string FireStatusRoot = "W3_NEXT_CANDIDATE_VISUAL_PENDING";
        public const string FrostStatusRoot = "W4_NEXT_CANDIDATE_VISUAL_PENDING";
        public const string LightningStatusRoot = "W5_NEXT_CANDIDATE_VISUAL_PENDING";
        public const float CellWidth = 3.35f;
        public const float CellHeight = 2.42f;
        public const float CellEffectHalfExtent = .72f;
        public const float LabelY = -.94f;
        public static readonly Rect FullCellBounds = new Rect(-1.56f, -1.12f, 3.12f, 2.24f);
        public static readonly Rect EffectBounds = new Rect(-1.34f, -.58f, 2.68f, 1.63f);
        public static readonly Rect LabelBounds = new Rect(-1.43f, -1.08f, 2.86f, .33f);

        [MenuItem("Tools/VFX Composer/Elements/Next Candidate/Build W3-W5 Only (Batch Safe)")]
        public static void BuildW3W5ForBatch()
        {
            var fire = BuildEntries("fire"); var frost = BuildEntries("frost"); var lightning = BuildEntries("lightning");
            BuildPreview("fire",fire); BuildPreview("frost",frost); BuildPreview("lightning",lightning);
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            Debug.Log("W3-W5 next candidates built as VISUAL_PENDING. No old candidate, W6+, user verdict, L3 or L4 was written.");
        }

        [MenuItem("Tools/VFX Composer/Elements/Next Candidate/Build W3 Fire Only (Batch Safe)")]
        public static void BuildW3ForBatch() { BuildFamily("fire"); }
        [MenuItem("Tools/VFX Composer/Elements/Next Candidate/Build W4 Frost Only (Batch Safe)")]
        public static void BuildW4ForBatch() { BuildFamily("frost"); }
        [MenuItem("Tools/VFX Composer/Elements/Next Candidate/Build W5 Lightning Only (Batch Safe)")]
        public static void BuildW5ForBatch() { BuildFamily("lightning"); }

        [MenuItem("Tools/VFX Composer/Elements/Next Candidate/Build and Open W3 Fire")]
        public static void BuildAndOpenW3() { BuildFamily("fire"); EditorSceneManager.OpenScene(FireScenePath,OpenSceneMode.Single); }
        [MenuItem("Tools/VFX Composer/Elements/Next Candidate/Build and Open W4 Frost")]
        public static void BuildAndOpenW4() { BuildFamily("frost"); EditorSceneManager.OpenScene(FrostScenePath,OpenSceneMode.Single); }
        [MenuItem("Tools/VFX Composer/Elements/Next Candidate/Build and Open W5 Lightning")]
        public static void BuildAndOpenW5() { BuildFamily("lightning"); EditorSceneManager.OpenScene(LightningScenePath,OpenSceneMode.Single); }

        public static ElementNextCandidateBuildResult[] BuildFamily(string family)
        {
            RequireFamily(family); var results=BuildEntries(family); BuildPreview(family,results); AssetDatabase.SaveAssets(); AssetDatabase.Refresh(); return results;
        }

        public static string ScenePath(string family) { RequireFamily(family); return family=="fire"?FireScenePath:family=="frost"?FrostScenePath:LightningScenePath; }
        public static string CandidateStatusRoot(string family) { RequireFamily(family); return family=="fire"?FireStatusRoot:family=="frost"?FrostStatusRoot:LightningStatusRoot; }
        public static string RecipePath(ElementContentEntry entry) { return ElementFamilyAuthoring.RecipeRoot + "/" + (entry.Family=="fire"?"Fire":entry.Family=="frost"?"Frost":"Lightning") + "/" + entry.Id + ".default.json"; }

        public static ElementNextCandidateBuildResult[] BuildEntries(string family)
        {
            RequireFamily(family); var entries=ElementFamilyCatalog.Family(family).ToArray(); var results=new ElementNextCandidateBuildResult[entries.Length];
            for(var index=0;index<entries.Length;index++)
            {
                results[index]=ElementNextCandidateCompiler.BuildAsset(RecipePath(entries[index]));
                if(!results[index].Succeeded)throw new InvalidOperationException(entries[index].Id+": "+Describe(results[index]));
            }
            return results;
        }

        public static float DisplayScale(ElementNextCandidatePlan plan) { return Mathf.Min(.72f,CellEffectHalfExtent/Mathf.Max(.01f,plan.MaxLocalExtent)); }

        private static void BuildPreview(string family,ElementNextCandidateBuildResult[] results)
        {
            var scenePath=ScenePath(family);var statusRoot=CandidateStatusRoot(family);var previewHash=PreviewHash(family,results);var receiptPath=ElementNextCandidateCompiler.CandidateRoot+"/Preview/"+family+"-preview.json";
            EnsureFolder(Path.GetDirectoryName(receiptPath).Replace('\\','/'));
            if(PreviewIsCurrent(scenePath,receiptPath,previewHash)){EnsureSceneLoadable(scenePath);return;}
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var entries=new List<ElementNextCandidateVisualExecutor>();
            CreateStatusMarker(statusRoot,family);
            for(var index=0;index<results.Length;index++)
            {
                var plan=results[index].Plan;var row=index/3;var column=index%3;var cellObject=new GameObject("Cell_"+(index+1).ToString("00",CultureInfo.InvariantCulture)+"_"+plan.EffectId);cellObject.transform.position=new Vector3((column-1)*CellWidth,2.5f-row*CellHeight,0f);
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(ElementNextCandidateCompiler.PrefabPath(plan.EffectId));if(prefab==null)throw new InvalidOperationException("Missing next-candidate Prefab: "+plan.EffectId);
                var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab,cellObject.transform);instance.name="Runtime_"+plan.EffectId+"_NEXT";instance.transform.localPosition=new Vector3(0f,.16f,0f);instance.transform.localScale=Vector3.one*DisplayScale(plan);
                var definition=ElementFamilyCatalog.All.First(value=>value.Id==plan.EffectId);if(definition.Dimension=="3d")instance.transform.localRotation=Quaternion.Euler(17f,-22f,0f);
                var entry=instance.GetComponent<ElementNextCandidateVisualExecutor>();if(entry==null)throw new InvalidOperationException(plan.EffectId+" has no dedicated next-candidate executor.");entries.Add(entry);
                ConfigureCell(cellObject.AddComponent<ElementNextCandidateCell>(),index+1,plan,entry);CreateLabel(cellObject.transform,(index+1).ToString("00",CultureInfo.InvariantCulture)+"  "+plan.EffectId+"\n"+plan.ShapeToken.Replace('_',' '));
            }
            CreateCamera();CreateDriver(entries);
            if(!EditorSceneManager.SaveScene(scene,scenePath))throw new InvalidOperationException("Could not save next-candidate Preview Scene: "+scenePath);
            WriteIfChanged(receiptPath,new JObject{{"previewVersion",1},{"family",family},{"scenePath",scenePath},{"candidateStatusRoot",statusRoot},{"visualStatus",ElementNextCandidatePlanCompiler.VisualStatus},{"previewHash",previewHash},{"cellCount",results.Length},{"compilerVersion",ElementNextCandidatePlanCompiler.CompilerVersion},{"machineEvidenceIsVisualAcceptance",false}}.ToString(Formatting.Indented)+"\n");
            AssetDatabase.ImportAsset(receiptPath,ImportAssetOptions.ForceSynchronousImport);EnsureSceneLoadable(scenePath);
        }

        private static void ConfigureCell(ElementNextCandidateCell cell,int index,ElementNextCandidatePlan plan,ElementNextCandidateVisualExecutor entry)
        {
            var serialized=new SerializedObject(cell);serialized.FindProperty("cellIndex").intValue=index;serialized.FindProperty("effectId").stringValue=plan.EffectId;serialized.FindProperty("fullBounds").rectValue=FullCellBounds;serialized.FindProperty("effectBounds").rectValue=EffectBounds;serialized.FindProperty("labelBounds").rectValue=LabelBounds;serialized.FindProperty("authoredDisplayScale").floatValue=DisplayScale(plan);serialized.FindProperty("compiledLocalExtent").floatValue=plan.MaxLocalExtent;serialized.FindProperty("entry").objectReferenceValue=entry;serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreateStatusMarker(string rootName,string family)
        {
            var root=new GameObject(rootName);root.transform.position=new Vector3(0f,4.46f,0f);var text=root.AddComponent<TextMesh>();text.text=rootName+"\nNEXT CANDIDATE / VISUAL SIGN-OFF PENDING / MACHINE BUILD ONLY";text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;text.fontSize=34;text.characterSize=.025f;text.color=family=="fire"?new Color(1f,.48f,.18f):family=="frost"?new Color(.5f,.85f,1f):new Color(.55f,.68f,1f);text.GetComponent<MeshRenderer>().sortingOrder=200;
        }

        private static void CreateLabel(Transform parent,string value)
        {
            var labelObject=new GameObject("Label");labelObject.transform.SetParent(parent,false);labelObject.transform.localPosition=new Vector3(0f,LabelY,-.08f);var text=labelObject.AddComponent<TextMesh>();text.text=value;text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;text.fontSize=32;text.characterSize=.0235f;text.color=new Color(.68f,.75f,.84f);text.GetComponent<MeshRenderer>().sortingOrder=190;
        }

        private static void CreateCamera()
        {
            var cameraObject=new GameObject("W3W5NextCandidateReviewCamera");cameraObject.tag="MainCamera";var camera=cameraObject.AddComponent<Camera>();camera.transform.position=new Vector3(0f,.18f,-14f);camera.transform.rotation=Quaternion.identity;camera.orthographic=true;camera.orthographicSize=4.95f;camera.nearClipPlane=.05f;camera.farClipPlane=40f;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.012f,.016f,.025f);camera.allowHDR=false;camera.allowMSAA=false;
        }

        private static void CreateDriver(List<ElementNextCandidateVisualExecutor> entries)
        {
            var driverObject=new GameObject("ElementNextCandidatePreviewDriver");var driver=driverObject.AddComponent<ElementNextCandidatePreviewDriver>();var serialized=new SerializedObject(driver);var property=serialized.FindProperty("entries");property.arraySize=entries.Count;for(var index=0;index<entries.Count;index++)property.GetArrayElementAtIndex(index).objectReferenceValue=entries[index];serialized.FindProperty("replayInterval").floatValue=4.2f;serialized.FindProperty("sustainedStopTime").floatValue=3.25f;serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static bool PreviewIsCurrent(string scenePath,string receiptPath,string hash)
        {
            if(AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath)==null)return false;var absolute=Absolute(receiptPath);if(!File.Exists(absolute))return false;try{var root=JObject.Parse(File.ReadAllText(absolute));return(string)root["previewHash"]==hash&&(string)root["visualStatus"]==ElementNextCandidatePlanCompiler.VisualStatus;}catch{return false;}
        }

        private static string PreviewHash(string family,IEnumerable<ElementNextCandidateBuildResult> results)
        {
            var value=family+"|"+ElementNextCandidatePlanCompiler.CompilerVersion+"|"+FullCellBounds+"|"+EffectBounds+"|"+LabelBounds+"|"+string.Join("|",results.OrderBy(item=>item.Plan.EffectId,StringComparer.Ordinal).Select(item=>item.Plan.EffectId+":"+item.BuildHash).ToArray());using(var sha=SHA256.Create())return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(value)).Select(item=>item.ToString("x2",CultureInfo.InvariantCulture)).ToArray());
        }

        private static void EnsureSceneLoadable(string scenePath)
        {
            var scenes=EditorBuildSettings.scenes.ToList();if(scenes.Any(value=>value.path==scenePath))return;scenes.Add(new EditorBuildSettingsScene(scenePath,true));EditorBuildSettings.scenes=scenes.ToArray();
        }

        private static void RequireFamily(string family){if(family!="fire"&&family!="frost"&&family!="lightning")throw new ArgumentOutOfRangeException("family","W3-W5 next-candidate authoring accepts fire, frost or lightning only.");}
        private static string Describe(ElementNextCandidateBuildResult result){return string.Join(" | ",result.Report.Entries.Select(value=>value.Code+" "+value.Path+" "+value.Message).ToArray());}
        private static void WriteIfChanged(string path,string value){var absolute=Absolute(path);Directory.CreateDirectory(Path.GetDirectoryName(absolute));if(File.Exists(absolute)&&File.ReadAllText(absolute)==value)return;File.WriteAllText(absolute,value,new UTF8Encoding(false));}
        private static void EnsureFolder(string path){if(AssetDatabase.IsValidFolder(path))return;var parent=Path.GetDirectoryName(path).Replace('\\','/');if(!AssetDatabase.IsValidFolder(parent))EnsureFolder(parent);AssetDatabase.CreateFolder(parent,Path.GetFileName(path));}
        private static string Absolute(string path){return Path.GetFullPath(Path.Combine(Directory.GetParent(Application.dataPath).FullName,path.Replace('/',Path.DirectorySeparatorChar)));}
    }
}


using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using VFXComposer.Editor.Build;
using VFXComposer.Editor.Domain;
using VFXComposer.Editor.Rules;
using VFXComposer.Editor.W24;
using VFXComposer.Editor.W24.S1;
using VFXComposer.Editor.W24.S5;
using VFXComposer.Editor.W24.S6.External;

namespace VFXComposer.Editor.UI
{
    public sealed class VfxStudioLibraryItem
    {
        public string Id, Name, RecipePath, Archetype, Dimension, Element, Style, PrefabPath, ManifestPath;
        public string[] Capabilities = new string[0];
        public bool HasRuntimeEntry;
        public bool Strict;
        // S5 deliberately exposes a conservative default.  This library is not a signature store:
        // only a future signed-status provider may promote a row to L4/commercial eligibility.
        public string ProductionStatus;
        public bool CommercialEligible;
        public int GameObjects, Renderers, Materials;

        // W24 information is a read-only projection of authoritative persisted files.  It is
        // intentionally not an editor-owned status or verdict store.
        public string ContractPath, ContractFileHash, ContractHash, TracePath, TraceFileHash;
        public string UserVerdictRecordPath, UserVerdictRecordHash;
        public int ContractRevision;
        public string[] Carriers = new string[0];
        public string Lifecycle = "missing";
        public string Maturity = "UNASSESSED";
        public string BuildHash, CaptureProfileHash, EvidenceCorpusPath, EvidenceCorpusHash;
        public string SourceRecipePath, RuntimeEntryPath, OwnershipSummary;
        public bool HasContract, HasTrace, HasEvidence, HasMachineEvidence, HasVisualQaEvidence, HasUserVerdict;
        public bool HasStrictBudgetEvidence, HasIdempotenceEvidence;
        public string MachineGate = "NOT_RECORDED", VisualQa = "NOT_RECORDED", UserVerdict = "NOT_RECORDED";
        public string[] StatusReasons = new string[0];
        public VfxDesignContract Contract;
        public VfxImplementationTrace Trace;
    }

    public sealed class VfxStudioLibraryFilter
    {
        public string Search = string.Empty, Archetype = "all", Dimension = "all", Element = "all", Style = "all", Capability = "all", Carrier = "all", Lifecycle = "all", Maturity = "all", ProductionStatus = "all";
        public bool Matches(VfxStudioLibraryItem item)
        {
            if(item==null)return false;var search=(Search??string.Empty).Trim();if(search.Length>0&&!Contains(item.Id,search)&&!Contains(item.Name,search))return false;
            return Match(Archetype,item.Archetype)&&Match(Dimension,item.Dimension)&&Match(Element,item.Element)&&Match(Style,item.Style)&&Match(Carrier,item.Carriers)&&Match(Lifecycle,item.Lifecycle)&&Match(Maturity,item.Maturity)&&Match(ProductionStatus,item.ProductionStatus)&&(Capability=="all"||item.Capabilities.Contains(Capability,StringComparer.Ordinal));
        }
        private static bool Match(string filter,string value){return string.IsNullOrEmpty(filter)||filter=="all"||string.Equals(filter,value,StringComparison.Ordinal);}
        private static bool Match(string filter,IEnumerable<string> values){return string.IsNullOrEmpty(filter)||filter=="all"||(values??Enumerable.Empty<string>()).Contains(filter,StringComparer.Ordinal);}
        private static bool Contains(string value,string search){return(value??string.Empty).IndexOf(search,StringComparison.OrdinalIgnoreCase)>=0;}
    }

    public static class VfxStudioLibrary
    {
        public static List<VfxStudioLibraryItem> Scan()
        {
            var items=new List<VfxStudioLibraryItem>();foreach(var guid in AssetDatabase.FindAssets("t:TextAsset",new[]{"Assets/VFX/Recipes"}))
            {
            var path=AssetDatabase.GUIDToAssetPath(guid);if(!path.EndsWith(".json",StringComparison.OrdinalIgnoreCase)||path.EndsWith(".patch.json",StringComparison.OrdinalIgnoreCase)||path.EndsWith(".history.json",StringComparison.OrdinalIgnoreCase))continue;VfxStudioLibraryItem item;try{item=Parse(path,File.ReadAllText(Absolute(path)));}catch{continue;}if(item!=null)items.Add(item);
            }
            return items.GroupBy(value=>value.Id,StringComparer.Ordinal).Select(group=>group.OrderBy(value=>value.RecipePath,StringComparer.Ordinal).First()).OrderBy(value=>value.Id,StringComparer.Ordinal).ToList();
        }
        public static List<VfxStudioLibraryItem> IndexForTests(IEnumerable<VfxStudioLibraryItem> source)
        {
            return (source??Enumerable.Empty<VfxStudioLibraryItem>()).Where(item=>item!=null).Select(item=>
            {
                if(item.ProductionStatus!="LEGACY"&&item.ProductionStatus!="VISUAL_PENDING")item.ProductionStatus="VISUAL_PENDING";
                if(string.IsNullOrEmpty(item.Maturity)||item.Maturity.StartsWith("L",StringComparison.Ordinal))item.Maturity="UNASSESSED";
                if(item.Carriers==null)item.Carriers=new string[0];
                if(item.StatusReasons==null)item.StatusReasons=new string[0];
                item.HasMachineEvidence=false;item.HasVisualQaEvidence=false;item.HasStrictBudgetEvidence=false;item.HasIdempotenceEvidence=false;
                item.MachineGate="NOT_RECORDED";item.VisualQa="NOT_RECORDED";
                item.CommercialEligible=false;
                return item;
            }).GroupBy(item=>item.Id??string.Empty,StringComparer.Ordinal).Select(group=>group.OrderBy(item=>item.RecipePath??string.Empty,StringComparer.Ordinal).First()).OrderBy(item=>item.Id,StringComparer.Ordinal).ToList();
        }
        public static string[] Values(IEnumerable<VfxStudioLibraryItem> items,Func<VfxStudioLibraryItem,string> selector){return new[]{"all"}.Concat(items.Select(selector).Where(value=>!string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).OrderBy(value=>value,StringComparer.Ordinal)).ToArray();}
        private static VfxStudioLibraryItem Parse(string path,string json)
        {
            var root=W24StrictJsonText.ParseObject(json,"Studio Recipe JSON");var id=(string)root["id"];if(string.IsNullOrWhiteSpace(id))return null;var item=new VfxStudioLibraryItem{Id=id,Name=(string)root["name"]??id,RecipePath=path,Archetype=(string)root["archetype"]??"unknown",Dimension=(string)root["dimension"]??"unknown",Style=Style(root["style"]),Element=Element(id,root),SourceRecipePath=path};var behavior=root["behavior"] as JObject;if(behavior!=null)item.Capabilities=behavior.Properties().Select(property=>property.Value as JObject).Where(value=>value!=null).Select(value=>(string)value["type"]).Where(value=>!string.IsNullOrEmpty(value)).Distinct(StringComparer.Ordinal).OrderBy(value=>value,StringComparer.Ordinal).ToArray();item.ManifestPath=VfxProjectRules.ManifestAbsolutePath(id);item.Strict=VfxProjectRules.EnforcementFor(id)==VfxRulesEnforcement.Strict;item.ProductionStatus=item.Strict?"VISUAL_PENDING":"LEGACY";item.CommercialEligible=false;var reasons=new List<string>();if(File.Exists(item.ManifestPath)){try{var manifest=ParseExactManifest(File.ReadAllText(item.ManifestPath),id);item.GameObjects=(int?)manifest["cost"]?["gameObjects"]??0;item.Renderers=(int?)manifest["cost"]?["transparentRenderers"]??0;item.Materials=(int?)manifest["cost"]?["localMaterials"]??0;item.BuildHash=(string)manifest["buildHash"];item.SourceRecipePath=(string)manifest["sourceRecipePath"]??path;item.RuntimeEntryPath=(string)manifest.SelectToken("runtimeEntry.path");if(AssetDatabase.LoadAssetAtPath<GameObject>(item.RuntimeEntryPath)!=null){item.PrefabPath=item.RuntimeEntryPath;item.HasRuntimeEntry=true;}else reasons.Add("Exact manifest Runtime Entry is not a loadable prefab; no fallback prefab was selected.");item.OwnershipSummary="manifest ownedOutputs="+((JArray)manifest["ownedOutputs"])?.Count;ReadFormal(item,manifest,reasons);}catch(Exception e){reasons.Add("Manifest unreadable or non-canonical: "+e.Message);}}else reasons.Add("No authoritative BuildManifest.");if(!item.HasContract)reasons.Add("No persisted formal W24 contract binding; conservative VISUAL_PENDING.");if(!item.HasTrace)reasons.Add("No verified implementation trace.");if(!item.HasEvidence)reasons.Add("No verified evidence corpus binding.");item.StatusReasons=reasons.Distinct(StringComparer.Ordinal).ToArray();return item;
        }

        internal static JObject ParseExactManifest(string json,string expectedEffectId)
        {
            var manifest=W24StrictJsonText.ParseObject(json,"Studio BuildManifest JSON");
            var effectToken=manifest["effectId"];
            if(effectToken==null||effectToken.Type!=JTokenType.String||!string.Equals((string)effectToken,expectedEffectId,StringComparison.Ordinal))
                throw new JsonSerializationException("Studio BuildManifest effectId does not match the selected effect.");
            var runtimeToken=manifest.SelectToken("runtimeEntry.path");
            if(runtimeToken==null||runtimeToken.Type!=JTokenType.String||!W24S6LocalDocumentInspector.IsExactManifestRuntimeEntry((string)runtimeToken,expectedEffectId))
                throw new JsonSerializationException("Studio BuildManifest Runtime Entry is not the exact canonical effect-owned prefab path.");
            return manifest;
        }
        private static void ReadFormal(VfxStudioLibraryItem item,JObject manifest,List<string> reasons)
        {
            var formal=manifest["formalProduction"] as JObject;if(formal==null)return;
            item.ContractPath=(string)formal["contractPath"];item.ContractFileHash=(string)formal["contractFileHash"];item.ContractHash=(string)formal["contractHash"];item.ContractRevision=(int?)formal["contractRevision"]??0;item.TracePath=(string)formal["tracePath"];item.TraceFileHash=(string)formal["traceFileHash"];var claimedStatus=(string)formal["visualStatus"];item.EvidenceCorpusPath=(string)formal["evidenceCorpusPath"];item.EvidenceCorpusHash=(string)formal["evidenceCorpusHash"];item.UserVerdictRecordPath=(string)formal["userVerdictRecordPath"];item.UserVerdictRecordHash=(string)formal["userVerdictRecordHash"];item.HasEvidence=FileMatches(item.EvidenceCorpusPath,item.EvidenceCorpusHash,W24S5RecordScope.EvidenceCorpus);item.HasUserVerdict=FileMatches(item.UserVerdictRecordPath,item.UserVerdictRecordHash,W24S5RecordScope.Verdict);item.UserVerdict=item.HasUserVerdict?"RECORD_BYTES_VERIFIED (not an L4 decision)":"NOT_RECORDED";
            try { var contractFile=ReadPinnedFormal(item.ContractPath,item.ContractFileHash,"studioLibraryContract","W24S6-LIBRARY-CONTRACT");if(contractFile!=null){var report=VfxDesignContractJson.ValidateJson(contractFile.Text,out item.Contract);item.HasContract=!report.HasErrors&&item.Contract!=null&&item.Contract.EffectId==item.Id&&item.Contract.ContractHash==item.ContractHash;item.Lifecycle=item.HasContract&&item.Contract.Lifecycle!=null?item.Contract.Lifecycle.Kind:"invalid";item.Carriers=item.HasContract?(item.Contract.Layers??Array.Empty<VfxLayer>()).Where(x=>x!=null).Select(x=>x.Carrier).Where(x=>!string.IsNullOrEmpty(x)).Distinct(StringComparer.Ordinal).OrderBy(x=>x,StringComparer.Ordinal).ToArray():new string[0];if(!item.HasContract)reasons.Add("Contract schema, semantic validation, or identity binding failed.");}else reasons.Add("Contract path or file hash does not verify."); } catch(Exception e) { reasons.Add("Contract is not authoritative: "+e.Message); }
            try { var traceFile=item.HasContract?ReadPinnedFormal(item.TracePath,item.TraceFileHash,"studioLibraryTrace","W24S6-LIBRARY-TRACE"):null;if(traceFile!=null){var validation=VfxImplementationTraceJson.ValidateJson(traceFile.Text,item.Contract,out item.Trace);item.HasTrace=!validation.Report.HasErrors&&item.Trace!=null&&item.Trace.EffectId==item.Id&&item.Trace.ContractHash==item.ContractHash&&item.Trace.ContractRevision==item.ContractRevision;item.CaptureProfileHash=item.Trace==null?null:item.Trace.CaptureProfileHash;var traces=(item.Trace==null?Array.Empty<VfxRequirementTrace>():item.Trace.RequirementTraces??Array.Empty<VfxRequirementTrace>()).Where(x=>x!=null).ToArray();item.HasMachineEvidence=false;item.MachineGate="NOT_RECORDED (S5 byte-level verifier not run)";var visualQaClaim=item.HasTrace&&traces.Any(x=>(x.AuthorityEvidence??Array.Empty<VfxTraceEvidence>()).Any(y=>y!=null&&y.Kind=="visualQa")||(x.CrossEvidence??Array.Empty<VfxTraceEvidence>()).Any(y=>y!=null&&y.Kind=="visualQa"));item.HasVisualQaEvidence=false;item.VisualQa=visualQaClaim?"UNVERIFIED_TRACE_CLAIM_PRESENT (not evidence or QA verdict)":"NOT_RECORDED";VfxStudioAutomaticReviewChecks.RefreshVerificationFlags(item);if(!item.HasTrace)reasons.Add("Trace schema, semantic validation, or identity binding failed.");}else reasons.Add("Trace path or file hash does not verify."); } catch(Exception e) { reasons.Add("Trace is not authoritative: "+e.Message); }
            // A manifest's status is not a UI authority.  Until S5 can re-verify an exact L4
            // decision, Studio will not display L4 or commercial eligibility.
            item.Maturity="UNASSESSED";
            if(claimedStatus=="L3"||claimedStatus=="L4")reasons.Add(claimedStatus+" is never trusted from a display manifest; independent workflow/verdict verification is required.");
            else if(!string.IsNullOrEmpty(claimedStatus)&&claimedStatus!="VISUAL_PENDING")reasons.Add("Unknown display status '"+claimedStatus+"' was ignored.");
        }
        private static W24S5PersistedFile ReadPinnedFormal(string relative,string expected,string field,string code){var result=new W24S5ProductionGateResult();return W24S5ProductionGate.ReadPersisted(result,relative,expected,field,code,W24S5RecordScope.Formal);}
        private static bool FileMatches(string relative,string expected,W24S5RecordScope scope=W24S5RecordScope.Formal){if(string.IsNullOrWhiteSpace(relative)||!W24Hash.IsCanonical(expected))return false;try{string path;if(!W24S5ProductionGate.TryResolvePersistedPath(relative,scope,out path))return false;return File.Exists(path)&&W24S5Hash.Sha256Bytes(File.ReadAllBytes(path))==expected;}catch{return false;}}
        private static string ProjectPath(string relative){return Path.GetFullPath(Path.Combine(Directory.GetParent(Application.dataPath).FullName,relative.Replace('/',Path.DirectorySeparatorChar)));}
        private static string Style(JToken token){if(token==null)return"stylized";if(token.Type==JTokenType.String)return(string)token;return(string)token["token"]??"stylized";}
        private static string Element(string id,JObject root){var lower=id.ToLowerInvariant();foreach(var pair in new[]{new[]{"fire","fireball","inferno","flame"},new[]{"frost","ice","snow"},new[]{"lightning","arc","thunder"},new[]{"water","aqua"},new[]{"wind","gale"},new[]{"earth","rock","stone"},new[]{"nature","leaf","vine"},new[]{"toxic","poison"},new[]{"holy","light"},new[]{"shadow","dark"},new[]{"arcane","astral"}})if(pair.Any(lower.Contains))return pair[0];var style=root["style"] as JObject;var palette=style?["palette"] as JObject;return palette==null?"neutral":"custom";}
        private static string Absolute(string assetPath){return Path.GetFullPath(Path.Combine(Directory.GetParent(Application.dataPath).FullName,assetPath.Replace('/',Path.DirectorySeparatorChar)));}
    }

    /// <summary>Fail-closed projection for Studio's automatic review checkboxes.</summary>
    public static class VfxStudioAutomaticReviewChecks
    {
        public static VfxStudioReviewState Evaluate(VfxStudioLibraryItem item,bool schemaValid,bool runtimeEntryVerified,bool manifestVerified,bool playbackResetVerified)
        {
            return new VfxStudioReviewState
            {
                Schema=schemaValid,
                RuntimeEntry=runtimeEntryVerified,
                Manifest=false,
                StrictBudget=false,
                Idempotence=false,
                PlaybackReset=false,
                Evidence=item!=null&&item.HasEvidence
            };
        }

        internal static void RefreshVerificationFlags(VfxStudioLibraryItem item)
        {
            item.HasStrictBudgetEvidence=false;
            item.HasIdempotenceEvidence=false;
        }
    }

    /// <summary>Studio may open only the exact scene frozen in an authoritative contract.</summary>
    public static class VfxStudioAuthoritativePreview
    {
        public static bool TryOpen(VfxStudioLibraryItem item,out string status){return TryOpen(item,EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo,out status);}
        internal static bool TryOpenForIntegrationTests(VfxStudioLibraryItem item,Func<bool> confirmSceneReplacement,out string status){return TryOpen(item,confirmSceneReplacement,out status);}
        private static bool TryOpen(VfxStudioLibraryItem item,Func<bool> confirmSceneReplacement,out string status)
        {
            string scenePath;
            VfxCaptureContract capture;
            if(!ValidateCurrentPhysicalBindings(item,out scenePath,out capture,out status))return false;
            if(confirmSceneReplacement==null||!confirmSceneReplacement()){status="Preview cancelled: current modified scenes were not approved for replacement.";return false;}
            // The user-controlled save/confirmation interval can be arbitrarily long.  Re-run the
            // complete physical guard immediately before replacing the current Scene.
            if(!ValidateCurrentPhysicalBindings(item,out scenePath,out capture,out status))return false;
            EditorSceneManager.OpenScene(scenePath,OpenSceneMode.Single);
            status="Opened exact contract scene: "+scenePath+"\nsceneHash: "+capture.SceneHash+"\nruntime entry: "+item.RuntimeEntryPath;
            return true;
        }
        private static bool ValidateCurrentPhysicalBindings(VfxStudioLibraryItem item,out string scenePath,out VfxCaptureContract capture,out string status)
        {
            scenePath=null;capture=null;
            if(item==null||!item.HasContract||!item.HasTrace||item.Contract==null||item.Trace==null){status="Preview blocked: an authoritative contract and trace are required.";return false;}
            if(!CurrentFormalBytesArePinned(item,out status))return false;
            capture=item.Contract.CaptureProfile;
            if(capture==null||string.IsNullOrEmpty(capture.SceneSerializedReference)||!W24Hash.IsCanonical(capture.SceneHash)){status="Preview blocked: contract has no exact serialized scene identity.";return false;}
            if(!W24S6LocalDocumentInspector.IsExactManifestRuntimeEntry(item.RuntimeEntryPath,item.Id)){status="Preview blocked: manifest Runtime Entry is not the exact canonical effect-owned prefab path.";return false;}
            if(!string.Equals(item.Trace.RuntimeEntryAssetPath,item.RuntimeEntryPath,StringComparison.Ordinal)){status="Preview blocked: trace Runtime Entry does not equal the manifest Runtime Entry.";return false;}
            if(!item.HasRuntimeEntry||!string.Equals(item.PrefabPath,item.RuntimeEntryPath,StringComparison.Ordinal)||AssetDatabase.LoadAssetAtPath<GameObject>(item.RuntimeEntryPath)==null){status="Preview blocked: indexed Runtime Entry is not the exact loadable Prefab selected by the manifest.";return false;}
            scenePath=capture.SceneSerializedReference;
            if(!scenePath.StartsWith("Assets/",StringComparison.Ordinal)){status="Preview blocked: frozen scene is not a project Asset path.";return false;}
            var absolute=ProjectPath(scenePath);
            if(!File.Exists(absolute)||W24S5Hash.Sha256Bytes(File.ReadAllBytes(absolute))!=capture.SceneHash){status="Preview blocked: authoritative scene bytes do not match contract sceneHash.";return false;}
            if(AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath)==null){status="Preview blocked: frozen scene reference is not a serialized Unity SceneAsset.";return false;}
            var expectedManifest=W24S5ProductionGate.ManifestRoot+item.Id+".manifest.json";
            if(!string.Equals(capture.PrefabManifestSerializedReference,expectedManifest,StringComparison.Ordinal)||!W24Hash.IsCanonical(capture.PrefabManifestHash)||!File.Exists(item.ManifestPath)){status="Preview blocked: capture profile does not bind the current authoritative prefab manifest.";return false;}
            JObject currentManifest;
            try
            {
                var manifestBytes=File.ReadAllBytes(item.ManifestPath);
                if(W24S5Hash.Sha256Bytes(manifestBytes)!=capture.PrefabManifestHash){status="Preview blocked: current manifest bytes do not match the capture profile hash.";return false;}
                var manifestText=new UTF8Encoding(false,true).GetString(manifestBytes);
                if(manifestText.Length>0&&manifestText[0]=='\ufeff')manifestText=manifestText.Substring(1);
                currentManifest=VfxStudioLibrary.ParseExactManifest(manifestText,item.Id);
            }
            catch(Exception e) when(e is IOException||e is UnauthorizedAccessException||e is DecoderFallbackException||e is JsonException)
            {
                status="Preview blocked: current manifest is unreadable or non-canonical: "+e.Message;return false;
            }
            var currentRuntimeEntry=(string)currentManifest.SelectToken("runtimeEntry.path");
            if(!string.Equals(currentRuntimeEntry,item.RuntimeEntryPath,StringComparison.Ordinal)){status="Preview blocked: current strict manifest Runtime Entry differs from the indexed Runtime Entry.";return false;}
            var currentBuildHash=(string)currentManifest["buildHash"];
            if(!IsRawSha256(currentBuildHash)||!string.Equals(currentBuildHash,item.BuildHash,StringComparison.Ordinal)||!string.Equals("sha256:"+currentBuildHash,item.Trace.BuildHash,StringComparison.Ordinal)){status="Preview blocked: current strict manifest, index, and trace do not bind the same build hash.";return false;}
            status=null;
            return true;
        }
        private static bool CurrentFormalBytesArePinned(VfxStudioLibraryItem item,out string status)
        {
            var result=new W24S5ProductionGateResult();
            W24S5PersistedFile contract;
            W24S5PersistedFile trace;
            try
            {
                contract=W24S5ProductionGate.ReadPersisted(result,item.ContractPath,item.ContractFileHash,"studioPreviewContract","W24S6-PREVIEW-CONTRACT",W24S5RecordScope.Formal);
                trace=W24S5ProductionGate.ReadPersisted(result,item.TracePath,item.TraceFileHash,"studioPreviewTrace","W24S6-PREVIEW-TRACE",W24S5RecordScope.Formal);
            }
            catch(Exception e)
            {
                status="Preview blocked: current Contract/Trace bytes could not be safely re-read: "+e.Message;
                return false;
            }
            if(contract!=null&&trace!=null){status=null;return true;}
            status="Preview blocked: current Contract/Trace bytes no longer match their safe indexed path/hash bindings.";
            if(result.Issues.Count>0)status+=" "+string.Join(" | ",result.Issues.Select(issue=>issue.Code+": "+issue.Message));
            return false;
        }
        private static bool IsRawSha256(string value){return value!=null&&value.Length==64&&value.All(character=>(character>='0'&&character<='9')||(character>='a'&&character<='f'));}
        private static string ProjectPath(string relative){return Path.GetFullPath(Path.Combine(Directory.GetParent(Application.dataPath).FullName,relative.Replace('/',Path.DirectorySeparatorChar)));}
    }

    public static class VfxStudioProductionGatePresenter
    {
        public static string EvaluateCurrent(VfxStudioLibraryItem item)
        {
            if(item==null)return "W24 S5 gate blocked: no selected entry.";
            // Studio never lets a display projection request L3/L4 authority.  This is a
            // development diagnostic only; publication and status promotion stay in S5.
            var requested=W24S5VisualStatus.VISUAL_PENDING;
            var result=W24S5ProductionGate.Evaluate(new W24S5ProductionGateRequest{EffectId=item.Id,ContractPath=item.ContractPath,ContractFileHash=item.ContractFileHash,TracePath=item.TracePath,TraceFileHash=item.TraceFileHash,UserVerdictRecordPath=item.UserVerdictRecordPath,UserVerdictRecordHash=item.UserVerdictRecordHash,PlannedBuildHash=item.BuildHash,ExpectedRuntimeEntryPath=item.RuntimeEntryPath,ExpectedManifestPath=W24S5ProductionGate.ManifestRoot+item.Id+".manifest.json",Intent=W24S5BuildIntent.Development,VisualStatus=requested});
            var lines=result.Issues.Select(issue=>(issue.IsError?"BLOCKER ":"NOTICE ")+issue.Code+": "+issue.Message).ToList();
            if(lines.Count==0)lines.Add(result.CanBuild?"S5 development gate currently passes.":"S5 gate blocked.");
            return string.Join("\n",lines);
        }
    }

    public static class VfxStudioDraftBuilder
    {
        public static string FromRecipe(string sourceJson,string id,string name,string styleToken,string dimension=null,string archetype=null)
        {
            var root=W24StrictJsonText.ParseObject(sourceJson,"Studio source Recipe JSON");root["revision"]=1;root["id"]=id;root["name"]=name;if(!string.IsNullOrEmpty(dimension))root["dimension"]=dimension;if(!string.IsNullOrEmpty(archetype))root["archetype"]=archetype;var style=root["style"] as JObject??new JObject();style["token"]=styleToken;style["palette"]=style["palette"]??new JObject{{"primary","#8FA3B8"},{"secondary","#DDE6F0"},{"accent","#FFFFFF"}};root["style"]=style;return root.ToString(Formatting.Indented);
        }
        public static string Prompt(string description,string archetype,string dimension,string style,string capability){return "Create one raw VFX Composer Recipe JSON. Description: "+description+". archetype="+archetype+", dimension="+dimension+", style="+style+", capability="+capability+". Use only registered fields/tokens; no Markdown or prose.";}
    }

    public sealed class VfxStudioPatchQueue
    {
        private readonly List<JObject> operations=new List<JObject>();
        public int Count{get{return operations.Count;}}
        public IReadOnlyList<JObject> Operations{get{return operations;}}
        public void SetStyle(string token){ReplaceSingleton("set_style_token","/style/token",new JValue(token));}
        public void SetPalette(string role,string color){ReplaceSingleton("set_palette","/style/palette/"+role,new JValue(color));}
        public void SetBehavior(string domain,string parameter,JToken value){ReplaceSingleton("set_behavior_param","/behavior/"+domain+"/"+parameter,value);}
        public void SetArchetypeParameter(string parameter,JToken value){ReplaceSingleton("set_archetype_param","/archetypeParameters/"+parameter,value);}
        public void SetContentParameter(string parameter,JToken value){ReplaceSingleton("set_content_param","/content/parameters/"+parameter,value);}
        public void Replace(string path,JToken value){ReplaceSingleton("replace",path,value);}
        public void RemoveAt(int index){if(index>=0&&index<operations.Count)operations.RemoveAt(index);}
        public void Clear(){operations.Clear();}
        public string ToJson(){return new JArray(operations.Select(value=>value.DeepClone())).ToString(Formatting.Indented);}
        private void ReplaceSingleton(string op,string path,JToken value){var existing=operations.FindIndex(item=>(string)item["path"]==path);var operation=new JObject{{"op",op},{"path",path},{"value",value.DeepClone()}};if(existing>=0)operations[existing]=operation;else operations.Add(operation);}
    }

    public sealed class VfxStudioReviewState
    {
        public bool Schema,RuntimeEntry,Manifest,StrictBudget,Idempotence,PlaybackReset,Evidence;
        public bool Shape,Layers,Motion,Dissipation,Depth;
        public bool AutomaticComplete{get{return Schema&&RuntimeEntry&&Manifest&&StrictBudget&&Idempotence&&PlaybackReset&&Evidence;}}
        public bool ManualComplete{get{return Shape&&Layers&&Motion&&Dissipation&&Depth;}}
        public void Reset(){Schema=RuntimeEntry=Manifest=StrictBudget=Idempotence=PlaybackReset=Evidence=false;Shape=Layers=Motion=Dissipation=Depth=false;}
        public string ToMarkdown(string id,string reviewer)
        {
            var builder=new StringBuilder("# "+id+" Review\n\n");builder.AppendLine("Reviewer: "+reviewer);builder.AppendLine("Recorded: "+DateTime.Now.ToString("O"));builder.AppendLine();Append(builder,"Schema",Schema);Append(builder,"Runtime Entry",RuntimeEntry);Append(builder,"Manifest",Manifest);Append(builder,"Strict budget",StrictBudget);Append(builder,"Idempotence",Idempotence);Append(builder,"Playback reset",PlaybackReset);Append(builder,"Evidence",Evidence);Append(builder,"Shape",Shape);Append(builder,"Layers",Layers);Append(builder,"Motion",Motion);Append(builder,"Dissipation",Dissipation);Append(builder,"Depth",Depth);builder.AppendLine();builder.AppendLine("Final visual acceptance: PENDING USER SIGN-OFF. This is non-authoritative review evidence only: it cannot create machine pass, Visual QA pass, a user verdict, L3, L4, migration approval, or a commercial claim. Status remains VISUAL_PENDING.");return builder.ToString();
        }
        private static void Append(StringBuilder builder,string name,bool value){builder.AppendLine("- ["+(value?"x":" ")+"] "+name);}
    }
}

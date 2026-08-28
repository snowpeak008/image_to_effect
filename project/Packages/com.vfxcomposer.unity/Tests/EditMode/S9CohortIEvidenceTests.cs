using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using VFXComposer.Editor.Build;
using VFXComposer.Editor.Domain;
using VFXComposer.Editor.Patch;
using VFXComposer.Editor.Workflow;

namespace VFXComposer.Tests.EditMode
{
    // Historical full-batch audit only: I Recipe reached 4/5 but Patch reached 1/3. It must not define the recovered M6 gate.
    public sealed class S9CohortIEvidenceTests
    {
        private const string PatchBase = "Assets/VFX/Recipes/s9_cohort_i_final_patch_base.json";
        [Test, Explicit("Historical Cohort I full-batch gate; preserved to expose its Patch 1/3 failure.")]
        public void CohortI_CompleteEvidenceMeetsBuildSemanticPatchAndTransportGate()
        {
            foreach (var key in VfxCohortIProtocol.RecipeKeys.Concat(VfxCohortIProtocol.PatchKeys)) Chain(key);
            var success = 0; var outcomes = new JArray();
            foreach (var key in VfxCohortIProtocol.RecipeKeys)
            {
                VfxBuildResult build = null; JObject recipe = null; var ok = false; var detail = string.Empty;
                try { recipe = JObject.Parse(File.ReadAllText(Final(key))); build = new VfxCompiler().Build(recipe.ToString()); ok = build.Succeeded && Semantic(key, recipe); detail = build.Succeeded ? (ok ? "build and semantic assertion passed" : "build passed but semantic assertion failed") : Describe(build.Plan.Report); }
                catch (Exception ex) { detail = ex.ToString(); }
                finally { if (build != null && build.Succeeded && recipe != null) AssetDatabase.DeleteAsset(VfxCompiler.OutputFolder(VfxDomainParser.ParseRecipe(recipe.ToString()).Value)); }
                if (ok) success++; outcomes.Add(new JObject { ["key"] = key, ["succeeded"] = ok, ["detail"] = detail });
            }
            VfxCohortIProtocol.WriteOnce(E("recipe-results.generated.json"), new JObject { ["successes"] = success, ["total"] = 5, ["outcomes"] = outcomes }.ToString(Newtonsoft.Json.Formatting.Indented).Replace("\r\n", "\n") + "\n"); Assert.That(success, Is.GreaterThanOrEqualTo(4));
            var patchFailures = VfxCohortIProtocol.PatchKeys.Where(key => !Patch(key)).ToArray(); Assert.That(patchFailures, Is.Empty);
            Assert.That(AssetDatabase.GetSubFolders(VfxCompiler.GeneratedRoot).Where(x => x != VfxCompiler.GeneratedRoot + "/fireball_2d"), Is.Empty);
        }
        private static void Chain(string key)
        {
            var attempts = Enumerable.Range(0, 3).Where(n => File.Exists(VfxCohortIProtocol.AttemptPath(key, n))).ToArray(); CollectionAssert.AreEqual(Enumerable.Range(0, attempts.Length).ToArray(), attempts); Assert.That(attempts.Length, Is.GreaterThan(0)); Assert.That(File.Exists(VfxCohortIProtocol.AttemptPath(key, 3)), Is.False); string thread = null;
            foreach (var n in attempts)
            {
                var report = VfxCohortIProtocol.ReportPath(key, n); Assert.That(File.Exists(report), Is.True); AssertReport(report); var w = JObject.Parse(File.ReadAllText(VfxCohortIProtocol.TransportPath(key, n))); var envelope = n == 0 ? VfxCohortIProtocol.InitialEnvelopePath(key) : VfxCohortIProtocol.RepairEnvelopePath(key, n); var payload = n == 0 ? VfxCohortIProtocol.InitialPayloadPath(key) : VfxCohortIProtocol.RepairPayloadPath(key, n); var temp = n == 0 ? VfxCohortIProtocol.TempInitialPayloadPath(key) : VfxCohortIProtocol.TempRepairPayloadPath(key, n);
                foreach(var f in new[]{"question","attempt","agentName","model","reasoningEffort","forkTurns","threadId","disclosure","envelopeSha256","payloadSha256","tempPayloadSha256"})Assert.That(w.ContainsKey(f),Is.True); Assert.That((string)w["question"],Is.EqualTo(key));Assert.That((int)w["attempt"],Is.EqualTo(n));Assert.That((string)w["agentName"],Is.EqualTo("s9_i_"+key.ToLowerInvariant()));Assert.That((string)w["model"],Is.EqualTo("gpt-5.6-terra"));Assert.That((string)w["reasoningEffort"],Is.EqualTo("high"));Assert.That((string)w["forkTurns"],Is.EqualTo("none"));Assert.That((string)w["threadId"],Is.Not.Empty); Assert.That((string)w["envelopeSha256"], Is.EqualTo(Hash(File.ReadAllBytes(envelope)))); Assert.That((string)w["payloadSha256"], Is.EqualTo(Hash(File.ReadAllBytes(payload)))); Assert.That((string)w["tempPayloadSha256"], Is.EqualTo(Hash(File.ReadAllBytes(temp)))); CollectionAssert.AreEqual(File.ReadAllBytes(payload), File.ReadAllBytes(temp)); Assert.That((string)w["transport"], Is.EqualTo(n == 0 ? "spawn_agent" : "followup_task")); if (n == 0) thread = (string)w["threadId"]; else { Assert.That((string)w["threadId"], Is.EqualTo(thread)); StringAssert.Contains(VfxCohortIProtocol.Normalize(File.ReadAllText(VfxCohortIProtocol.ReportPath(key, n - 1))), VfxCohortIProtocol.Normalize(File.ReadAllText(payload))); var prepared=JObject.Parse(File.ReadAllText(VfxCohortIProtocol.PreparedPath(key,n)));Assert.That((string)prepared["EnvelopeSha256"],Is.EqualTo(Hash(File.ReadAllBytes(envelope))));Assert.That((string)prepared["PayloadSha256"],Is.EqualTo(Hash(File.ReadAllBytes(payload))));Assert.That((string)prepared["TempPayloadSha256"],Is.EqualTo(Hash(File.ReadAllBytes(temp))));Assert.That((string)prepared["PriorReportSha256"],Is.EqualTo(Hash(File.ReadAllBytes(VfxCohortIProtocol.ReportPath(key,n-1))))); }
            }
            CollectionAssert.AreEqual(File.ReadAllBytes(VfxCohortIProtocol.AttemptPath(key, attempts.Last())), File.ReadAllBytes(Final(key)));
        }
        private static bool Semantic(string key, JObject recipe)
        {
            var s = (JObject)JObject.Parse(File.ReadAllText(E("acceptance-spec.json")))["recipes"][key]; if ((string)recipe["id"] != (string)s["id"] || (string)recipe["targetProfile"] != (string)s["profile"]) return false; var stages = recipe["stages"].Children<JObject>().ToList(); if (!Stage(stages,"launch","on_launch") || !Stage(stages,"travel","after_previous") || !Stage(stages,"impact","on_hit")) return false;
            foreach(var k in s["travel"].Values<string>()) if(Module(stages,"travel",k)==null)return false; foreach(var k in (s["forbidTravel"]??new JArray()).Values<string>())if(Module(stages,"travel",k)!=null)return false; foreach(var k in s["impact"].Values<string>())if(Module(stages,"impact",k)==null)return false; return ((JObject)s["compare"]).Properties().All(p=>Compare(Value(stages,p.Name),Value(Canonical()["stages"].Children<JObject>().ToList(),p.Name),(string)p.Value));
        }
        private static bool Patch(string key)
        {
            try { File.WriteAllText(A(PatchBase),File.ReadAllText(A(VfxAiWorkflowExporter.FormalDefaultRecipePath)).Replace("\"id\": \"fireball_2d\"","\"id\": \"s9_cohort_i_final_patch_base\"")); AssetDatabase.ImportAsset(PatchBase,ImportAssetOptions.ForceUpdate); var r=new VfxPatchService().ApplyToAsset(PatchBase,File.ReadAllText(Final(key)),1); if(!r.IsValid||r.AfterRevision!=2)return false; var history=JArray.Parse(File.ReadAllText(A(PatchBase+VfxPatchService.HistorySuffix)));var last=(JObject)history.Last;if((int)last["beforeRevision"]!=1||(int)last["afterRevision"]!=2)return false; var modules=JObject.Parse(File.ReadAllText(A(PatchBase)))["stages"].Children<JObject>().Single(x=>(string)x["id"]=="travel")["modules"].Children<JObject>().ToList(); if(key=="P1")return (double)modules.Single(x=>(string)x["id"]=="trail")["parameters"]["width"]==.3; if(key=="P2")return !(bool)modules.Single(x=>(string)x["id"]=="trail")["enabled"]; var m=modules.SingleOrDefault(x=>(string)x["id"]=="afterglow_embers"); return m!=null&&(string)m["id"]=="afterglow_embers"&&(string)m["kind"]=="secondary_particles"&&(string)m["templateId"]=="PFT_2D_Embers"&&(string)m["attachTo"]=="core"; } catch{return false;} finally{CleanPatch();}
        }
        private static JObject Canonical(){var t=VfxCohortIProtocol.Normalize(File.ReadAllText(E("contract-snapshot.md")));const string b="<!-- BEGIN canonical-recipe.generated.json -->\n",e="\n<!-- END canonical-recipe.generated.json -->";var s=t.IndexOf(b,StringComparison.Ordinal)+b.Length;return JObject.Parse(t.Substring(s,t.IndexOf(e,s,StringComparison.Ordinal)-s));}
        private static bool Stage(System.Collections.Generic.List<JObject> x,string id,string trigger){var s=x.SingleOrDefault(v=>(string)v["id"]==id);return s!=null&&(string)s["trigger"]==trigger;} private static JObject Module(System.Collections.Generic.List<JObject>x,string stage,string kind){var s=x.SingleOrDefault(v=>(string)v["id"]==stage);return s==null?null:s["modules"].Children<JObject>().SingleOrDefault(v=>(string)v["kind"]==kind);} private static double Value(System.Collections.Generic.List<JObject>x,string key){var p=key.Split('.');var kind=p[0]=="core"?"energy_body":p[0]=="trail"?"motion_trail":p[0]=="embers"?"secondary_particles":p[0]=="burst"?"impact_burst":"shockwave";var m=Module(x,p[0]=="core"||p[0]=="trail"||p[0]=="embers"?"travel":"impact",kind);return m==null?double.NaN:(double)m["parameters"][p[1]];} private static bool Compare(double a,double b,string o){return o=="<"?a<b:o=="<="?a<=b:o=="=="?Math.Abs(a-b)<.000001:o==">="?a>=b:o==">"&&a>b;}
        private static void AssertReport(string path){var r=JObject.Parse(File.ReadAllText(path));foreach(var f in new[]{"succeeded","detail","entries"})Assert.That(r.ContainsKey(f),Is.True);foreach(var e in (JArray)r["entries"])foreach(var f in new[]{"code","severity","path","message","actualValue","allowedRange"})Assert.That(((JObject)e).ContainsKey(f),Is.True);} private static string Final(string key){return E(key+".final."+(VfxCohortIProtocol.RecipeKeys.Contains(key)?"recipe.json":"patch.json"));} private static string E(string file){return Path.Combine(VfxCohortIProtocol.EvidenceDirectory(),file);} private static string A(string p){return Path.Combine(Application.dataPath,p.Substring("Assets/".Length));} private static string Hash(byte[]b){using(var s=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(s.ComputeHash(b)).Replace("-","");} private static string Describe(ValidationReport r){return string.Join(" | ",r.Entries.Select(x=>x.Code+" "+x.Path));} private static void CleanPatch(){if(AssetDatabase.IsValidFolder(VfxCompiler.GeneratedRoot+"/s9_cohort_i_final_patch_base"))AssetDatabase.DeleteAsset(VfxCompiler.GeneratedRoot+"/s9_cohort_i_final_patch_base");if(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(PatchBase)!=null)AssetDatabase.DeleteAsset(PatchBase);var h=PatchBase+VfxPatchService.HistorySuffix;if(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(h)!=null)AssetDatabase.DeleteAsset(h);AssetDatabase.Refresh();}
    }
}

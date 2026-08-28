using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using VFXComposer.Editor.Elements;
using VFXComposer.Editor.Patch;

namespace VFXComposer.Tests.EditMode
{
    public sealed class W6W8ElementNextCandidateTests
    {
        private static readonly string[] Families={"water","wind","earth","nature","toxic","holy","shadow","arcane"};

        [Test]
        public void TwentyFiveAuthorityPlans_HaveDedicatedShapesVersionsBudgetsAndPhysicalParameterBindings()
        {
            var entries=Entries().ToArray();Assert.That(entries.Length,Is.EqualTo(25));var shapes=new HashSet<string>(StringComparer.Ordinal);var topologies=new HashSet<string>(StringComparer.Ordinal);
            foreach(var entry in entries)
            {
                var result=ElementNextCandidateCompiler.PlanAsset(RecipePath(entry));Assert.That(result.Succeeded,Is.True,entry.Id+": "+Describe(result));var plan=result.Plan;
                Assert.That(plan.CompilerVersion,Is.EqualTo(ElementNextCandidatePlanCompiler.CompilerVersionW6W8),entry.Id);Assert.That(plan.Bindings.Count,Is.EqualTo(plan.Parameters.Count),entry.Id);Assert.That(plan.Bindings.All(value=>!string.IsNullOrEmpty(value.Carrier)),Is.True,entry.Id);CollectionAssert.AreEquivalent(plan.Parameters.Keys,plan.Bindings.Select(value=>value.Parameter),entry.Id);
                Assert.That(shapes.Add(plan.ShapeToken),Is.True,"Every W6-W8 effect must own a dedicated carrier shape: "+entry.Id);Assert.That(topologies.Add(plan.TopologySignature),Is.True,entry.Id);Assert.That(plan.ParticleBudget,Is.LessThanOrEqualTo(ElementNextCandidateVisualExecutor.AbsoluteMaxParticleCapacity),entry.Id);Assert.That(plan.RendererBudget,Is.LessThanOrEqualTo(ElementNextCandidateVisualExecutor.AbsoluteMaxRendererCount),entry.Id);Assert.That(plan.ArcCarrierCount,Is.LessThanOrEqualTo(ElementNextCandidateVisualExecutor.MaxArcCarriers),entry.Id);Assert.That(plan.MaxLocalExtent,Is.GreaterThan(0f),entry.Id);
                Assert.That(ElementNextCandidateCompiler.PrefabPath(entry.Id),Does.StartWith(ElementNextCandidateCompiler.GeneratedRootW6W8+"/"),entry.Id);
            }
            var old=ElementNextCandidateCompiler.PlanAsset(ElementNextCandidateAuthoring.RecipePath(ElementFamilyCatalog.All.Single(value=>value.Id=="flame_slash_2d")));Assert.That(old.Succeeded,Is.True);Assert.That(old.Plan.CompilerVersion,Is.EqualTo(ElementNextCandidatePlanCompiler.CompilerVersionW3W5));Assert.That(ElementNextCandidateCompiler.PrefabPath(old.Plan.EffectId),Does.StartWith(ElementNextCandidateCompiler.GeneratedRoot+"/"));
        }

        [Test]
        public void PatchValues_ChangePhysicalCarrierAndTopologyInEveryNewElementLanguage()
        {
            AssertPatch("water_jet_beam_3d","pressure",9f,"PrimaryWaterStrands.thickness_and_speed");
            AssertPatch("tornado_area_3d","height",4.5f,"PrimaryFunnel.height");
            AssertPatch("earth_spike_spawn_3d","spike_count",8,"PrimaryWedgeFault.topology");
            AssertPatch("thorn_snare_area_2d","thorn_density",30,"PrimaryThornRing.topology");
            AssertPatch("spore_burst_impact_2d","linger_time",2.5f,"ResidualSporeCloud.convergence_timing");
            AssertPatch("divine_smite_impact_3d","pillar_height",10f,"PrimaryOrderedPillar.height");
            AssertPatch("void_orb_projectile_3d","suction_particle_rate",60,"DetailInwardSpiral.count");
            AssertPatch("arcane_rune_spawn_2d","glyph_count",12,"PrimaryRuneRings.glyph_topology");
        }

        [Test]
        public void ExtremeValidParameters_ExpandBoundsButPreviewEnvelopeRemainsInsideFixedCell()
        {
            AssertExtent("water_jet_beam_3d",new Dictionary<string,object>{{"length",12f}},12.5f);
            AssertExtent("tornado_area_3d",new Dictionary<string,object>{{"height",5f}},6.2f);
            AssertExtent("vine_whip_slash_2d",new Dictionary<string,object>{{"whip_length",10f},{"wave_amp",3f}},13.2f);
            AssertExtent("quake_stomp_impact_3d",new Dictionary<string,object>{{"radius",10f}},11.5f);
            AssertExtent("divine_smite_impact_3d",new Dictionary<string,object>{{"pillar_height",12f},{"pillar_radius",5f}},19.9f);
            AssertExtent("shadow_grasp_area_2d",new Dictionary<string,object>{{"pool_radius",8f},{"hand_height",5f}},9.6f);
            AssertExtent("arcane_rune_spawn_2d",new Dictionary<string,object>{{"ring_radius",8f}},10.2f);
        }

        [Test]
        public void W6W8OnlyBuild_IsIdempotentBudgetedAndDoesNotRewriteW3W5CapabilityW1W15OrLegacyOutputs()
        {
            var before=SnapshotProtected();ElementNextCandidateW6W8Authoring.BuildW6W8ForBatch();var identity=Entries().ToDictionary(value=>value.Id,value=>AssetDatabase.AssetPathToGUID(ElementNextCandidateCompiler.PrefabPath(value.Id))+"|"+ManifestHash(value.Id),StringComparer.Ordinal);var afterFirst=SnapshotProtected();CollectionAssert.AreEquivalent(before,afterFirst,"W6-W8 build touched another candidate/legacy cohort.");
            ElementNextCandidateW6W8Authoring.BuildW6W8ForBatch();var second=Entries().ToDictionary(value=>value.Id,value=>AssetDatabase.AssetPathToGUID(ElementNextCandidateCompiler.PrefabPath(value.Id))+"|"+ManifestHash(value.Id),StringComparer.Ordinal);CollectionAssert.AreEquivalent(identity,second,"Second W6-W8 build was not idempotent.");CollectionAssert.AreEquivalent(afterFirst,SnapshotProtected(),"Second build touched another cohort.");
            foreach(var entry in Entries())
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(ElementNextCandidateCompiler.PrefabPath(entry.Id));Assert.That(prefab,Is.Not.Null,entry.Id);var executor=prefab.GetComponent<ElementNextCandidateVisualExecutor>();Assert.That(executor,Is.Not.Null,entry.Id);Assert.That(prefab.GetComponents<MonoBehaviour>().Count(value=>value is IVfxRuntimeEntry),Is.EqualTo(1),entry.Id);Assert.That(executor.BudgetWithinLimits,Is.True,entry.Id);Assert.That(executor.OwnedRendererCount,Is.LessThanOrEqualTo(executor.RendererBudget),entry.Id);Assert.That(prefab.GetComponentsInChildren<ParticleSystem>(true).Length,Is.EqualTo(1),entry.Id);Assert.That(prefab.GetComponentsInChildren<Rigidbody>(true),Is.Empty,entry.Id);
                var renderers=prefab.GetComponentsInChildren<Renderer>(true);Assert.That(renderers.Any(value=>(BlendMode)Mathf.RoundToInt(value.sharedMaterial.GetFloat("_DstBlend"))==BlendMode.OneMinusSrcAlpha),Is.True,entry.Id+" needs an alpha body/atmosphere, not additive-only recolor.");
                var manifest=JObject.Parse(File.ReadAllText(Absolute(ElementNextCandidateCompiler.ManifestPath(entry.Id))));Assert.That((string)manifest["compilerVersion"],Is.EqualTo(ElementNextCandidatePlanCompiler.CompilerVersionW6W8),entry.Id);Assert.That((string)manifest["visualStatus"],Is.EqualTo("VISUAL_PENDING"));Assert.That((bool)manifest["machineEvidenceIsVisualAcceptance"],Is.False);Assert.That(manifest["userVisualVerdict"].Type,Is.EqualTo(JTokenType.Null));Assert.That((bool)manifest["oldRejectedCandidateModified"],Is.False);Assert.That(((JArray)manifest["parameterBindings"]).Count,Is.EqualTo(executor.ParameterBindingCount));
            }
        }

        [Test]
        public void ProceduralMeshes_AreProfileSpecificAndContentShapedRatherThanSharedMeshRecolors()
        {
            ElementNextCandidateW6W8Authoring.BuildW6W8ForBatch();var signatures=new HashSet<string>(StringComparer.Ordinal);var primaryPaths=new HashSet<string>(StringComparer.Ordinal);
            foreach(var entry in Entries())
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(ElementNextCandidateCompiler.PrefabPath(entry.Id));var meshes=prefab.GetComponentsInChildren<MeshFilter>(true).Select(value=>value.sharedMesh).Where(value=>value!=null).ToArray();Assert.That(meshes.Length,Is.GreaterThan(0),entry.Id);var paths=meshes.Select(AssetDatabase.GetAssetPath).ToArray();Assert.That(paths.All(value=>value.StartsWith(ElementNextCandidateCompiler.OutputFolder(entry.Id)+"/Meshes/",StringComparison.Ordinal)),Is.True,entry.Id);Assert.That(primaryPaths.Add(paths[0]),Is.True,"Primary carrier mesh asset was shared across effects: "+entry.Id);signatures.Add(string.Join("|",meshes.Select(value=>value.vertexCount+":"+value.triangles.Length+":"+value.bounds.size.ToString("F4")).ToArray()));
            }
            Assert.That(signatures.Count,Is.GreaterThanOrEqualTo(20),"W6-W8 procedural profiles need broad topology diversity in addition to unique owned mesh assets.");
            var earth=AssetDatabase.LoadAssetAtPath<GameObject>(ElementNextCandidateCompiler.PrefabPath("earth_spike_spawn_3d")).transform.Find("PrimaryCarrier").GetComponent<MeshFilter>().sharedMesh;Assert.That(earth.uv.Select(value=>value.x).Distinct().Count(),Is.GreaterThanOrEqualTo(6),"Earth wedges need per-piece reveal coordinates.");
            var runes=AssetDatabase.LoadAssetAtPath<GameObject>(ElementNextCandidateCompiler.PrefabPath("arcane_rune_spawn_2d")).transform.Find("PrimaryCarrier").GetComponent<MeshFilter>().sharedMesh;Assert.That(runes.uv.Select(value=>value.x).Distinct().Count(),Is.EqualTo(10),"Rune glyph UV ranks must encode deterministic activation order.");
        }

        [Test]
        public void ThreePreviewScenes_HavePendingRootsFixedDisjointCellsOneDriverAndNoMachineVisualVerdict()
        {
            ElementNextCandidateW6W8Authoring.BuildW6W8ForBatch();var sandbox=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            try
            {
                foreach(var item in new[]{new object[]{"w6",8},new object[]{"w7",8},new object[]{"w8",9}})
                {
                    var group=(string)item[0];var expected=(int)item[1];var scene=EditorSceneManager.OpenScene(ElementNextCandidateW6W8Authoring.ScenePath(group),OpenSceneMode.Additive);try{var roots=scene.GetRootGameObjects();Assert.That(roots.Any(value=>value.name==ElementNextCandidateW6W8Authoring.CandidateStatusRoot(group)),Is.True,group);var cells=roots.SelectMany(value=>value.GetComponentsInChildren<ElementNextCandidateCell>(true)).ToArray();Assert.That(cells.Length,Is.EqualTo(expected),group);Assert.That(cells.All(value=>value.EffectAndLabelAreDisjoint&&value.ScaledEnvelopeFitsEffectBounds),Is.True,group);AssertCellsDisjoint(cells);Assert.That(roots.SelectMany(value=>value.GetComponentsInChildren<ElementNextCandidateVisualExecutor>(true)).Count(),Is.EqualTo(expected),group);Assert.That(roots.SelectMany(value=>value.GetComponentsInChildren<Camera>(true)).Count(),Is.EqualTo(1),group);var driver=roots.SelectMany(value=>value.GetComponentsInChildren<ElementNextCandidatePreviewDriver>(true)).Single();Assert.That(driver.EntryCount,Is.EqualTo(expected),group);Assert.That(driver.TriggerEventDriven,Is.True,group+" must exercise pop/impact/implode events during review.");var receipt=JObject.Parse(File.ReadAllText(Absolute(ElementNextCandidateCompiler.CandidateRootW6W8+"/Preview/"+group+"-preview.json")));Assert.That((string)receipt["visualStatus"],Is.EqualTo("VISUAL_PENDING"));Assert.That((bool)receipt["machineEvidenceIsVisualAcceptance"],Is.False);}finally{EditorSceneManager.CloseScene(scene,true);}
                }
            }
            finally{if(sandbox.IsValid())EditorSceneManager.CloseScene(sandbox,true);}
        }

        private static void AssertPatch(string id,string parameter,object value,string carrier)
        {
            var entry=ElementFamilyCatalog.All.Single(item=>item.Id==id);var path=RecipePath(entry);var source=File.ReadAllText(Absolute(path));var before=ElementNextCandidatePlanCompiler.PlanJson(path,source);var patch=new JArray(new JObject{{"op","set_content_param"},{"path","/content/parameters/"+parameter},{"value",JToken.FromObject(value)}}).ToString();var validated=new VfxPatchService().Validate(source,patch,1);Assert.That(validated.IsValid,Is.True,id+": "+string.Join(" | ",validated.Report.Entries.Select(item=>item.Code+" "+item.Path+" "+item.Message).ToArray()));var after=ElementNextCandidatePlanCompiler.PlanJson(path,validated.PatchedRecipeJson);Assert.That(before.Succeeded&&after.Succeeded,Is.True,id);Assert.That(after.Plan.TopologySignature,Is.Not.EqualTo(before.Plan.TopologySignature),id);Assert.That(after.Plan.BuildHash,Is.Not.EqualTo(before.Plan.BuildHash),id);Assert.That(after.Plan.CarrierFor(parameter),Is.EqualTo(carrier),id);Assert.That(after.Plan.Number(parameter,-1f),Is.EqualTo(Convert.ToSingle(value)).Within(.0001f),id);
        }

        private static void AssertExtent(string id,IDictionary<string,object> values,float minimum)
        {
            var entry=ElementFamilyCatalog.All.Single(item=>item.Id==id);var path=RecipePath(entry);var recipe=JObject.Parse(File.ReadAllText(Absolute(path)));foreach(var pair in values)recipe["content"]["parameters"][pair.Key]=JToken.FromObject(pair.Value);var plan=ElementNextCandidatePlanCompiler.PlanJson(path,recipe.ToString());Assert.That(plan.Succeeded,Is.True,id+": "+Describe(plan));Assert.That(plan.Plan.MaxLocalExtent,Is.GreaterThanOrEqualTo(minimum),id);var scale=ElementNextCandidateW6W8Authoring.DisplayScale(plan.Plan);Assert.That(plan.Plan.MaxLocalExtent*scale,Is.LessThanOrEqualTo(ElementNextCandidateAuthoring.CellEffectHalfExtent+.0001f),id);
        }

        private static void AssertCellsDisjoint(ElementNextCandidateCell[] cells){for(var a=0;a<cells.Length;a++)for(var b=a+1;b<cells.Length;b++){var left=WorldRect(cells[a]);var right=WorldRect(cells[b]);Assert.That(left.Overlaps(right),Is.False,cells[a].EffectId+" overlaps "+cells[b].EffectId);}}
        private static Rect WorldRect(ElementNextCandidateCell cell){var rect=cell.FullBounds;var position=cell.transform.position;return new Rect(rect.x+position.x,rect.y+position.y,rect.width,rect.height);}
        private static IEnumerable<ElementContentEntry> Entries(){return ElementFamilyCatalog.All.Where(value=>Families.Contains(value.Family));}
        private static string RecipePath(ElementContentEntry entry){return ElementNextCandidateW6W8Authoring.RecipePath(entry);}
        private static string ManifestHash(string id){return(string)JObject.Parse(File.ReadAllText(Absolute(ElementNextCandidateCompiler.ManifestPath(id))))["buildHash"];}
        private static string Describe(ElementNextCandidatePlanResult result){return string.Join(" | ",result.Report.Entries.Select(value=>value.Code+" "+value.Path+" "+value.Message).ToArray());}

        private static Dictionary<string,string> SnapshotProtected()
        {
            var values=new Dictionary<string,string>(StringComparer.Ordinal);var paths=new[]{ElementNextCandidateCompiler.CandidateRoot,ElementNextCandidateAuthoring.FireScenePath,ElementNextCandidateAuthoring.FrostScenePath,ElementNextCandidateAuthoring.LightningScenePath,"Assets/VFX/Preview/VFXPREVIEW_CapProjectile.unity","Assets/VFX/Preview/VFXPREVIEW_CapBeam.unity","Assets/VFX/Preview/VFXPREVIEW_CapTiming.unity","Assets/VFX/Preview/VFXPREVIEW_W1_NEXT_CANDIDATE.unity","Assets/VFX/Generated/W15NextCandidate","Assets/VFX/Generated/cap_linear_proj_3d","Assets/VFX/Generated/style_orb_stylized_2d","Assets/VFX/Generated/flame_slash_2d"};
            foreach(var path in paths){var absolute=Absolute(path);if(File.Exists(absolute))values[path]=HashFile(absolute);else if(Directory.Exists(absolute))foreach(var file in Directory.GetFiles(absolute,"*",SearchOption.AllDirectories).OrderBy(value=>value,StringComparer.Ordinal))values[path+"/"+file.Substring(absolute.Length).TrimStart(Path.DirectorySeparatorChar).Replace('\\','/')]=HashFile(file);else values[path]="missing";}return values;
        }
        private static string HashFile(string path){using(var stream=File.OpenRead(path))using(var sha=SHA256.Create())return string.Concat(sha.ComputeHash(stream).Select(value=>value.ToString("x2")).ToArray());}
        private static string Absolute(string assetPath){return Path.GetFullPath(Path.Combine(Directory.GetParent(Application.dataPath).FullName,assetPath.Replace('/',Path.DirectorySeparatorChar)));}
    }
}

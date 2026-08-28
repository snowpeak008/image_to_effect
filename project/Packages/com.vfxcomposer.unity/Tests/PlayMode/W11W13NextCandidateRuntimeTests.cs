using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VFXComposer.W11W13NextCandidate;

namespace VFXComposer.Tests.PlayMode
{
    public sealed class W11W13NextCandidateRuntimeTests
    {
        [UnityTest]
        public IEnumerator Environment_IntensityWindLoopAndImmediateCleanupAreContinuousAndBounded()
        {
            var entry=Create("w11nc_rain_weather_volume");
            Assert.That(entry.ParticleCapacity,Is.LessThanOrEqualTo(160));
            entry.Play();entry.SetIntensity(.18f);entry.SetWind(new Vector3(7f,0f,1f));Assert.That(entry.SetLayerDensities(.25f,.5f,.75f),Is.True);Assert.That(entry.LayerDensities,Is.EqualTo(new Vector3(.25f,.5f,.75f)));
            yield return new WaitForSeconds(.16f);
            Assert.That(entry.IsAlive,Is.True);Assert.That(entry.CurrentIntensity,Is.InRange(.18f,.999f));Assert.That(entry.TargetIntensity,Is.EqualTo(.18f));Assert.That(entry.Wind.x,Is.EqualTo(7f));
            var snapshot=entry.ReadSnapshot();Assert.That(snapshot.Stage,Is.EqualTo(W11W13NextRuntimeStage.Primary));Assert.That(snapshot.ActiveRendererCount,Is.GreaterThanOrEqualTo(3));
            entry.Stop(VfxStopMode.Immediate);Assert.That(entry.IsAlive,Is.False);Assert.That(entry.GetComponentsInChildren<Renderer>(true).All(value=>!value.enabled),Is.True);Assert.That(entry.GetComponentsInChildren<ParticleSystem>(true).All(value=>value.particleCount==0),Is.True);
            Object.Destroy(entry.gameObject);
        }

        [UnityTest]
        public IEnumerator HitFlash_UsesExternalPropertyBlocksWithoutReplacingMaterialAndRestoresIncomingState()
        {
            var entry=Create("w12nc_hit_flash_status_2d");var target=GameObject.CreatePrimitive(PrimitiveType.Capsule);var renderer=target.GetComponent<Renderer>();var material=renderer.sharedMaterial;
            var incoming=new MaterialPropertyBlock();incoming.SetFloat("_PreexistingProbe",.37f);renderer.SetPropertyBlock(incoming);
            Assert.That(entry.BindExternalRenderers(new[]{renderer}),Is.True);entry.Play();yield return null;
            var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block);Assert.That(block.GetFloat("_FlashAmount"),Is.GreaterThan(0f));Assert.That(renderer.sharedMaterial,Is.SameAs(material));
            entry.ResetForPool();renderer.GetPropertyBlock(block);Assert.That(block.GetFloat("_FlashAmount"),Is.Zero);Assert.That(block.GetFloat("_PreexistingProbe"),Is.EqualTo(.37f).Within(.0001f));Assert.That(renderer.sharedMaterial,Is.SameAs(material));
            Object.Destroy(entry.gameObject);Object.Destroy(target);
        }

        [UnityTest]
        public IEnumerator ComboSurge_StackOneThroughFiveChangesRealRingCountWithinFixedCapacity()
        {
            var entry=Create("w12nc_combo_surge_aura_2d");var capacity=entry.ParticleCapacity;entry.SetStackLevel(1);entry.Play();yield return null;var levelOne=entry.ActiveRendererCount;
            Assert.That(entry.SetStackLevel(5),Is.True);yield return null;var levelFive=entry.ActiveRendererCount;
            Assert.That(levelFive,Is.GreaterThan(levelOne));Assert.That(entry.StackLevel,Is.EqualTo(5));Assert.That(entry.ParticleCapacity,Is.EqualTo(capacity));Assert.That(entry.transform.Cast<Transform>().Count(value=>value.name.StartsWith("StackRing_")),Is.EqualTo(5));
            entry.ResetForPool();Object.Destroy(entry.gameObject);
        }

        [UnityTest]
        public IEnumerator ElementalReaction_ConvergesTwoBodiesBeforeActivatingThirdColorRelease()
        {
            var entry=Create("w12nc_elemental_reaction_burst_2d");var a=entry.transform.Find("ApproachEnergyA");var b=entry.transform.Find("ApproachEnergyB");var result=entry.transform.Find("FusionResultBody");var initial=Vector3.Distance(a.localPosition,b.localPosition);
            entry.SetReactionColors(Color.red,Color.blue,Color.magenta);entry.Play();yield return new WaitForSeconds(.24f);
            Assert.That(Vector3.Distance(a.localPosition,b.localPosition),Is.LessThan(initial*.45f));Assert.That(result.gameObject.activeSelf,Is.True);Assert.That(result.localScale.x,Is.GreaterThan(.1f));
            entry.ResetForPool();Object.Destroy(entry.gameObject);
        }

        [UnityTest]
        public IEnumerator LifestealLink_TracksWorldEndpointsWithSagAndReverseMovingMotes()
        {
            var entry=Create("w12nc_lifesteal_link_beam_2d");var from=new Vector3(-2f,.4f,.1f);var to=new Vector3(2f,.1f,-.1f);Assert.That(entry.SetWorldEndpoints(from,to),Is.True);entry.Play();yield return new WaitForSeconds(.12f);
            var line=entry.transform.Find("SaggingDynamicLink").GetComponent<LineRenderer>();Assert.That(line.useWorldSpace,Is.True);Assert.That(Vector3.Distance(line.GetPosition(0),from),Is.LessThan(.001f));Assert.That(Vector3.Distance(line.GetPosition(line.positionCount-1),to),Is.LessThan(.001f));Assert.That(line.GetPosition(line.positionCount/2).y,Is.LessThan(Mathf.Lerp(from.y,to.y,.5f)-.25f));
            var first=entry.transform.Find("ReverseFlowMoteA").position;yield return new WaitForSeconds(.12f);Assert.That(entry.transform.Find("ReverseFlowMoteA").position,Is.Not.EqualTo(first));entry.ResetForPool();Assert.That(line.enabled,Is.False);Object.Destroy(entry.gameObject);
        }

        [UnityTest]
        public IEnumerator Ultimate_TimelinePoolsReferencedSourcesAndReusesThemAcrossReplay()
        {
            var entry=Create("w13nc_dragon_breath_ultimate_3d");var intro=entry.transform.Find("IntroStage");var originalScale=intro.localScale;var originalRotation=intro.localRotation;entry.Play();yield return new WaitForSeconds(.72f);
            Assert.That(entry.CreatedSourceInstanceCount,Is.EqualTo(4));Assert.That(entry.TriggeredCueCount,Is.GreaterThanOrEqualTo(2));Assert.That(entry.ActiveSourceInstanceCount,Is.GreaterThanOrEqualTo(1));Assert.That(entry.CameraHintSerial,Is.GreaterThanOrEqualTo(1));var created=entry.CreatedSourceInstanceCount;
            entry.ResetForPool();Assert.That(entry.ActiveSourceInstanceCount,Is.Zero);Assert.That(intro.localScale,Is.EqualTo(originalScale));Assert.That(Quaternion.Angle(intro.localRotation,originalRotation),Is.LessThan(.001f));entry.Play();yield return null;Assert.That(entry.CreatedSourceInstanceCount,Is.EqualTo(created));entry.Stop(VfxStopMode.Immediate);Object.Destroy(entry.gameObject);
        }

        [UnityTest]
        public IEnumerator DemonGate_PausesAtExactNamedGateAndBladeTempestUsesEightPooledSlashInstances()
        {
            var gate=Create("w13nc_demon_gate_boss_3d");gate.Play();yield return new WaitForSeconds(1.28f);Assert.That(gate.WaitingForGate,Is.True);Assert.That(gate.WaitingGateId,Is.EqualTo("gate_formed"));var paused=gate.Elapsed;yield return new WaitForSeconds(.12f);Assert.That(gate.Elapsed,Is.EqualTo(paused).Within(.002f));Assert.That(gate.ReleaseGate("wrong"),Is.False);Assert.That(gate.SendEvent("gate:gate_formed",new VfxRuntimeEvent()),Is.True);Assert.That(gate.WaitingForGate,Is.False);
            var blade=Create("w13nc_blade_tempest_ultimate_3d");blade.Play();yield return new WaitForSeconds(1.65f);Assert.That(blade.CreatedSourceInstanceCount,Is.EqualTo(10));Assert.That(blade.TriggeredCueCount,Is.GreaterThanOrEqualTo(4));Assert.That(blade.transform.Find("PrimaryStage").childCount,Is.EqualTo(8));blade.Stop(VfxStopMode.Immediate);Assert.That(blade.ActiveSourceInstanceCount,Is.Zero);
            Object.Destroy(gate.gameObject);Object.Destroy(blade.gameObject);
        }

        private static W11W13NextCandidateController Create(string id)
        {
            GameObject prefab=null;
#if UNITY_EDITOR
            prefab=UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/VFX/Generated/W11W13NextCandidate/"+id+"/VFX_"+id+".prefab");
#endif
            Assert.That(prefab,Is.Not.Null,id+" (run W11W13NextCandidateAuthoring.BuildAllForBatch first)");var instance=Object.Instantiate(prefab);return instance.GetComponent<W11W13NextCandidateController>();
        }
    }
}

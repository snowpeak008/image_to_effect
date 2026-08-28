using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace VFXComposer.Tests.PlayMode
{
    public sealed class W6W8ElementNextCandidateRuntimeTests
    {
        private static readonly string[] W6Ids={"water_jet_beam_3d","tidal_wave_area_3d","bubble_shield_2d","splash_impact_2d","whirlpool_spawn_3d","tornado_area_3d","wind_blade_slash_2d","gale_dash_trail_2d"};
        private static readonly string[] W7Ids={"earth_spike_spawn_3d","boulder_projectile_3d","quake_stomp_impact_3d","thorn_snare_area_2d","vine_whip_slash_2d","healing_bloom_aura_2d","spore_burst_impact_2d","acid_lob_projectile_2d"};
        private static readonly string[] W8Ids={"divine_smite_impact_3d","holy_halo_aura_2d","resurrection_spawn_3d","shadow_claw_slash_2d","void_orb_projectile_3d","shadow_grasp_area_2d","curse_mark_status_2d","arcane_missile_projectile_2d","arcane_rune_spawn_2d"};

        [UnityTest]
        public IEnumerator Water_ExecutesVolumeFoamSplashResidueAndAuthoredStopSag()
        {
            var jet=Create("water_jet_beam_3d");jet.Play();jet.EvaluateAtTime(.4f);Assert.That(jet.Phase,Is.EqualTo(ElementNextCandidatePhase.Sustain));Assert.That(jet.WaterFlow,Is.GreaterThan(.8f));Assert.That(jet.WaterFoam,Is.GreaterThan(0f));Assert.That(jet.ActiveLayerCount,Is.GreaterThanOrEqualTo(5));Assert.That(jet.VisibleParticleCount,Is.GreaterThan(0));jet.Stop(VfxStopMode.AllowTail);jet.EvaluateTailAtTime(.15f);Assert.That(jet.WaterSag,Is.EqualTo(.5f).Within(.02f));Assert.That(jet.WaterResidue,Is.GreaterThan(0f));jet.EvaluateTailAtTime(.31f);Assert.That(jet.IsAlive,Is.False);Assert.That(jet.AllVisualsHidden,Is.True);
            var wave=Create("tidal_wave_area_3d");wave.Play();wave.EvaluateAtTime(.45f);Assert.That(wave.Phase,Is.EqualTo(ElementNextCandidatePhase.Curl));Assert.That(wave.WaterFlow,Is.GreaterThan(0f));Assert.That(wave.WaterFoam,Is.GreaterThan(0f));Assert.That(wave.WaterSplash,Is.GreaterThan(0f));wave.EvaluateAtTime(.82f);Assert.That(wave.Phase,Is.EqualTo(ElementNextCandidatePhase.Residue));Assert.That(wave.WaterResidue,Is.GreaterThan(0f));
            var bubble=Create("bubble_shield_2d");bubble.Play();bubble.EvaluateAtTime(.2f);Assert.That(bubble.Phase,Is.EqualTo(ElementNextCandidatePhase.Sustain));bubble.TriggerLocalEvent(Vector3.right*.3f);Assert.That(bubble.Phase,Is.EqualTo(ElementNextCandidatePhase.Pop));Assert.That(bubble.WaterSplash,Is.EqualTo(1f).Within(.001f));
            foreach(var entry in new[]{jet,wave,bubble}){entry.Stop(VfxStopMode.Immediate);UnityEngine.Object.Destroy(entry.gameObject);}yield return null;
        }

        [UnityTest]
        public IEnumerator Wind_IsLowOpacityAndReadableThroughDebrisThinBladesAndFlowLines()
        {
            var tornado=Create("tornado_area_3d");tornado.Play();tornado.EvaluateAtTime(.4f);Assert.That(tornado.WindOpacity,Is.GreaterThan(0f).And.LessThanOrEqualTo(.35f));Assert.That(tornado.WindDebrisCount,Is.EqualTo(26));Assert.That(tornado.VisibleParticleCount,Is.EqualTo(26));Assert.That(tornado.VisibleArcCount,Is.EqualTo(2));
            var blade=Create("wind_blade_slash_2d");blade.Play();blade.EvaluateAtTime(.16f);Assert.That(blade.WindFlowLineCount,Is.EqualTo(3));Assert.That(blade.VisibleArcCount,Is.EqualTo(3));Assert.That(blade.WindOpacity,Is.LessThanOrEqualTo(.35f));Assert.That(blade.PrimaryCarrierMultiplicity,Is.EqualTo(3));
            var dash=Create("gale_dash_trail_2d");dash.Play();dash.EvaluateAtTime(.25f);Assert.That(dash.WindFlowLineCount,Is.EqualTo(14));Assert.That(dash.PrimaryCarrierMultiplicity,Is.EqualTo(2));Assert.That(dash.VisibleArcCount,Is.EqualTo(3));
            foreach(var entry in new[]{tornado,blade,dash}){entry.Stop(VfxStopMode.Immediate);UnityEngine.Object.Destroy(entry.gameObject);}yield return null;
        }

        [UnityTest]
        public IEnumerator EarthNatureAndToxic_UseWeightRevealWitherDoublePulseLingerAndCorrosionPool()
        {
            var spikes=Create("earth_spike_spawn_3d");spikes.Play();spikes.EvaluateAtTime(.08f);var early=spikes.EarthRise;var earlyCount=spikes.EarthRevealedSpikeCount;Assert.That(spikes.EarthOvershoot,Is.GreaterThan(0f));Assert.That(earlyCount,Is.InRange(1,5));spikes.EvaluateAtTime(.45f);Assert.That(spikes.EarthRise,Is.GreaterThan(early));Assert.That(spikes.EarthRevealedSpikeCount,Is.GreaterThan(earlyCount));Assert.That(spikes.EarthWeight,Is.GreaterThan(.8f));Assert.That(spikes.PrimaryCarrierMultiplicity,Is.EqualTo(6));
            var boulder=Create("boulder_projectile_3d");boulder.Play();boulder.EvaluateAtTime(.35f);boulder.TriggerLocalEvent(Vector3.right*.4f);Assert.That(boulder.Phase,Is.EqualTo(ElementNextCandidatePhase.Impact));Assert.That(boulder.EarthDebrisCount,Is.EqualTo(7));Assert.That(boulder.EarthDust,Is.EqualTo(1f).Within(.001f));
            var thorns=Create("thorn_snare_area_2d");thorns.Play();thorns.EvaluateAtTime(.35f);Assert.That(thorns.NatureGrowth,Is.EqualTo(1f).Within(.001f));Assert.That(thorns.PrimaryCarrierMultiplicity,Is.EqualTo(16));thorns.Stop(VfxStopMode.AllowTail);thorns.EvaluateTailAtTime(.4f);Assert.That(thorns.Phase,Is.EqualTo(ElementNextCandidatePhase.Wither));Assert.That(thorns.NatureWither,Is.EqualTo(.5f).Within(.02f));
            var vine=Create("vine_whip_slash_2d");vine.Play();vine.EvaluateAtTime(.18f);Assert.That(vine.NatureGrowth,Is.GreaterThan(0f));Assert.That(vine.VisibleArcCount,Is.EqualTo(1));Assert.That(vine.NatureBloomCount,Is.EqualTo(6));
            var bloom=Create("healing_bloom_aura_2d");bloom.Play();bloom.EvaluateAtTime(.1f);var first=bloom.NatureBloomCount;bloom.EvaluateAtTime(.8f);Assert.That(bloom.NatureBloomCount,Is.GreaterThan(first));
            var spore=Create("spore_burst_impact_2d");spore.Play();spore.EvaluateAtTime(.5f);var lingerA=spore.ToxicLinger;spore.EvaluateAtTime(.9f);Assert.That(spore.ToxicLinger,Is.LessThan(lingerA),"Lingering cloud must converge monotonically after the second pulse.");Assert.That(spore.VisibleParticleCount,Is.EqualTo(32));
            var acid=Create("acid_lob_projectile_2d");acid.Play();acid.EvaluateAtTime(.35f);acid.TriggerLocalEvent(Vector3.left*.25f);Assert.That(acid.Phase,Is.EqualTo(ElementNextCandidatePhase.Linger));Assert.That(acid.ToxicPool,Is.EqualTo(1f).Within(.001f));Assert.That(acid.ToxicBubbleCount,Is.GreaterThan(0));
            foreach(var entry in new[]{spikes,boulder,thorns,vine,bloom,spore,acid}){entry.Stop(VfxStopMode.Immediate);UnityEngine.Object.Destroy(entry.gameObject);}yield return null;
        }

        [UnityTest]
        public IEnumerator HolyShadowAndArcane_UseOrderedRevealNegativeSpaceSuctionImplodeAndDiscreteActivation()
        {
            var smite=Create("divine_smite_impact_3d");smite.Play();smite.EvaluateAtTime(.04f);var early=smite.HolyOrderedReveal;Assert.That(smite.HolyVerticalReveal,Is.GreaterThan(0f));smite.EvaluateAtTime(.18f);Assert.That(smite.HolyOrderedReveal,Is.GreaterThan(early));Assert.That(smite.HolyFeatherCount,Is.EqualTo(8));Assert.That(smite.VisibleArcCount,Is.EqualTo(2));
            var claws=Create("shadow_claw_slash_2d");claws.Play();claws.EvaluateAtTime(.12f);Assert.That(claws.ShadowNegativeSpace,Is.GreaterThan(0f));Assert.That(claws.ShadowMist,Is.GreaterThan(0f));Assert.That(claws.VisibleArcCount,Is.EqualTo(3));Assert.That(claws.PrimaryCarrierMultiplicity,Is.EqualTo(3));
            var orb=Create("void_orb_projectile_3d");orb.Play();orb.EvaluateAtTime(.2f);Assert.That(orb.ShadowSuction,Is.GreaterThan(0f));Assert.That(orb.VisibleParticleCount,Is.EqualTo(30));orb.TriggerLocalEvent(Vector3.right*.2f);Assert.That(orb.Phase,Is.EqualTo(ElementNextCandidatePhase.Implode));Assert.That(orb.ShadowImplode,Is.EqualTo(1f).Within(.001f));
            var grasp=Create("shadow_grasp_area_2d");grasp.Play();grasp.EvaluateAtTime(.2f);Assert.That(grasp.ShadowHandCount,Is.EqualTo(3));Assert.That(grasp.ShadowSuction,Is.GreaterThan(0f));Assert.That(grasp.VisibleArcCount,Is.EqualTo(3));
            var missile=Create("arcane_missile_projectile_2d");missile.Play();missile.EvaluateAtTime(.21f);Assert.That(missile.ArcaneMissileCount,Is.EqualTo(3));Assert.That(missile.ArcaneStaggerStep,Is.EqualTo(3));Assert.That(missile.VisibleArcCount,Is.EqualTo(3));
            var rune=Create("arcane_rune_spawn_2d");rune.Play();rune.EvaluateAtTime(.3f);Assert.That(rune.ArcaneGlyphCount,Is.EqualTo(10));Assert.That(rune.ArcaneStaggerStep,Is.GreaterThan(0));Assert.That(rune.GetArcaneActivationOrdinal(0),Is.EqualTo(0));SetTextParameter(rune,"activate_order","reverse");Assert.That(rune.GetArcaneActivationOrdinal(0),Is.EqualTo(9));Assert.That(rune.GetArcaneActivationOrdinal(9),Is.EqualTo(0));
            var seededA=Create("arcane_rune_spawn_2d");var seededB=Create("arcane_rune_spawn_2d");SetTextParameter(seededA,"activate_order","seeded_random");SetTextParameter(seededB,"activate_order","seeded_random");var orderA=Enumerable.Range(0,10).Select(seededA.GetArcaneActivationOrdinal).ToArray();var orderB=Enumerable.Range(0,10).Select(seededB.GetArcaneActivationOrdinal).ToArray();CollectionAssert.AreEqual(orderA,orderB);CollectionAssert.AreEquivalent(Enumerable.Range(0,10),orderA);
            foreach(var entry in new[]{smite,claws,orb,grasp,missile,rune,seededA,seededB}){entry.Stop(VfxStopMode.Immediate);UnityEngine.Object.Destroy(entry.gameObject);}yield return null;
        }

        [UnityTest]
        public IEnumerator ContentParametersDriveGeometryCountsAndTimingAcrossAllEightFamilies()
        {
            var shortJet=Create("water_jet_beam_3d");var longJet=Create("water_jet_beam_3d");SetNumberParameter(shortJet,"length",2f);SetNumberParameter(longJet,"length",10f);shortJet.Play();longJet.Play();shortJet.EvaluateAtTime(.3f);longJet.EvaluateAtTime(.3f);Assert.That(longJet.transform.Find("PrimaryCarrier").localPosition.x,Is.GreaterThan(shortJet.transform.Find("PrimaryCarrier").localPosition.x+3f));
            var lowWind=Create("tornado_area_3d");var highWind=Create("tornado_area_3d");SetNumberParameter(lowWind,"height",2f);SetNumberParameter(highWind,"height",5f);lowWind.Play();highWind.Play();lowWind.EvaluateAtTime(.3f);highWind.EvaluateAtTime(.3f);Assert.That(highWind.transform.Find("PrimaryCarrier").localScale.y,Is.GreaterThan(lowWind.transform.Find("PrimaryCarrier").localScale.y*2f));
            var fewSpikes=Create("earth_spike_spawn_3d");var manySpikes=Create("earth_spike_spawn_3d");SetNumberParameter(fewSpikes,"spike_count",5);SetNumberParameter(manySpikes,"spike_count",8);fewSpikes.Play();manySpikes.Play();fewSpikes.EvaluateAtTime(.3f);manySpikes.EvaluateAtTime(.3f);Assert.That(manySpikes.PrimaryCarrierMultiplicity,Is.EqualTo(8));Assert.That(fewSpikes.PrimaryCarrierMultiplicity,Is.EqualTo(5));
            var shortVine=Create("vine_whip_slash_2d");var longVine=Create("vine_whip_slash_2d");SetNumberParameter(shortVine,"whip_length",2f);SetNumberParameter(longVine,"whip_length",8f);shortVine.Play();longVine.Play();shortVine.EvaluateAtTime(.18f);longVine.EvaluateAtTime(.18f);Assert.That(longVine.GetArcPoint(0,longVine.GetArcPointCount(0)-1).x,Is.GreaterThan(shortVine.GetArcPoint(0,shortVine.GetArcPointCount(0)-1).x*3f));
            var sparseSpores=Create("spore_burst_impact_2d");var denseSpores=Create("spore_burst_impact_2d");SetNumberParameter(sparseSpores,"spore_count",8);SetNumberParameter(denseSpores,"spore_count",50);sparseSpores.Play();denseSpores.Play();sparseSpores.EvaluateAtTime(.25f);denseSpores.EvaluateAtTime(.25f);Assert.That(denseSpores.VisibleParticleCount,Is.GreaterThan(sparseSpores.VisibleParticleCount));
            var lowSmite=Create("divine_smite_impact_3d");var highSmite=Create("divine_smite_impact_3d");SetNumberParameter(lowSmite,"pillar_height",2f);SetNumberParameter(highSmite,"pillar_height",10f);lowSmite.Play();highSmite.Play();lowSmite.EvaluateAtTime(.15f);highSmite.EvaluateAtTime(.15f);Assert.That(highSmite.transform.Find("PrimaryCarrier").localPosition.y,Is.GreaterThan(lowSmite.transform.Find("PrimaryCarrier").localPosition.y*4f));
            var sparseVoid=Create("void_orb_projectile_3d");var denseVoid=Create("void_orb_projectile_3d");SetNumberParameter(sparseVoid,"suction_particle_rate",6);SetNumberParameter(denseVoid,"suction_particle_rate",44);sparseVoid.Play();denseVoid.Play();sparseVoid.EvaluateAtTime(.2f);denseVoid.EvaluateAtTime(.2f);Assert.That(denseVoid.VisibleParticleCount,Is.GreaterThan(sparseVoid.VisibleParticleCount));
            var oneMissile=Create("arcane_missile_projectile_2d");var fiveMissiles=Create("arcane_missile_projectile_2d");SetNumberParameter(oneMissile,"missile_count",1);SetNumberParameter(fiveMissiles,"missile_count",5);SetNumberParameter(fiveMissiles,"stagger_interval",.03f);oneMissile.Play();fiveMissiles.Play();oneMissile.EvaluateAtTime(.2f);fiveMissiles.EvaluateAtTime(.2f);Assert.That(oneMissile.VisibleArcCount,Is.EqualTo(1));Assert.That(fiveMissiles.VisibleArcCount,Is.EqualTo(5));
            foreach(var entry in new[]{shortJet,longJet,lowWind,highWind,fewSpikes,manySpikes,shortVine,longVine,sparseSpores,denseSpores,lowSmite,highSmite,sparseVoid,denseVoid,oneMissile,fiveMissiles}){entry.Stop(VfxStopMode.Immediate);UnityEngine.Object.Destroy(entry.gameObject);}yield return null;
        }

        [UnityTest]
        public IEnumerator TerminalEvents_DoNotResurrectProjectileOrShieldBodiesOnLaterDurationLoops()
        {
            foreach(var id in new[]{"bubble_shield_2d","boulder_projectile_3d","acid_lob_projectile_2d","void_orb_projectile_3d","arcane_missile_projectile_2d"})
            {
                var entry=Create(id);entry.Play();entry.EvaluateAtTime(.2f);entry.TriggerLocalEvent(Vector3.right*.2f);entry.EvaluateAtTime(12f);Assert.That(entry.AllVisualsHidden,Is.True,id+" resurrected after its terminal event window.");entry.Stop(VfxStopMode.Immediate);UnityEngine.Object.Destroy(entry.gameObject);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator AllTwentyFiveEntries_AreDeterministicBudgetedPoolCleanAndReplayWithoutMaterialAllocation()
        {
            var entries=W6Ids.Concat(W7Ids).Concat(W8Ids).Select(Create).ToArray();var materialBindings=entries.ToDictionary(value=>value,value=>value.GetComponentsInChildren<Renderer>(true).Select(renderer=>renderer.sharedMaterial).ToArray());
            foreach(var entry in entries)
            {
                Assert.That(entry.BudgetWithinLimits,Is.True,entry.EffectId);Assert.That(entry.ParameterBindingCount,Is.GreaterThan(0),entry.EffectId);entry.Play();entry.EvaluateAtTime(.11f);Assert.That(entry.VisibleParticleCount,Is.LessThanOrEqualTo(entry.ParticleBudget),entry.EffectId);entry.Stop(VfxStopMode.Immediate);Assert.That(entry.IsAlive,Is.False,entry.EffectId);Assert.That(entry.AllVisualsHidden,Is.True,entry.EffectId);entry.Play();if(entry.Lifecycle==StyledVfxLifecycle.OneShot)entry.EvaluateAtTime(entry.Duration);entry.Stop(VfxStopMode.AllowTail);if(entry.IsAlive)entry.EvaluateTailAtTime(2f);Assert.That(entry.IsAlive,Is.False,entry.EffectId);Assert.That(entry.AllVisualsHidden,Is.True,entry.EffectId);Assert.That(entry.PlayCount,Is.EqualTo(2),entry.EffectId);CollectionAssert.AreEqual(materialBindings[entry],entry.GetComponentsInChildren<Renderer>(true).Select(renderer=>renderer.sharedMaterial).ToArray(),entry.EffectId);
            }
            foreach(var entry in entries)UnityEngine.Object.Destroy(entry.gameObject);yield return null;
        }

        [UnityTest]
        public IEnumerator PreviewDriver_StopsNewSustainedEntriesResetsAndReplaysCleanly()
        {
            var oneShot=Create("divine_smite_impact_3d");var sustainedWater=Create("water_jet_beam_3d");var sustainedNature=Create("thorn_snare_area_2d");var eventBubble=Create("bubble_shield_2d");var entries=new[]{oneShot,sustainedWater,sustainedNature,eventBubble};var root=new GameObject("W6W8NextPreviewDriverFixture");var driver=root.AddComponent<ElementNextCandidatePreviewDriver>();SetPrivate(driver,"entries",entries);SetPrivate(driver,"replayInterval",.4f);SetPrivate(driver,"sustainedStopTime",.05f);SetPrivate(driver,"triggerEventDriven",true);SetPrivate(driver,"eventTriggerTime",.02f);driver.ReplayNow();yield return new WaitForSeconds(.09f);Assert.That(driver.EventsTriggered,Is.True);Assert.That(eventBubble.EventSequence,Is.EqualTo(1),"Preview must expose event-only pop/impact semantics before stopping.");Assert.That(driver.SustainedStopped,Is.True);Assert.That(oneShot.IsAlive,Is.True);Assert.That(sustainedWater.IsStopping||!sustainedWater.IsAlive,Is.True);Assert.That(sustainedNature.IsStopping||!sustainedNature.IsAlive,Is.True);driver.enabled=false;Assert.That(entries.All(value=>!value.IsAlive&&value.AllVisualsHidden),Is.True);driver.enabled=true;Assert.That(entries.All(value=>value.IsAlive),Is.True);driver.enabled=false;Assert.That(entries.All(value=>!value.IsAlive&&value.AllVisualsHidden),Is.True);UnityEngine.Object.Destroy(root);foreach(var entry in entries)UnityEngine.Object.Destroy(entry.gameObject);yield return null;
        }

        private static ElementNextCandidateVisualExecutor Create(string id)
        {
            GameObject prefab=null;
#if UNITY_EDITOR
            prefab=UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/VFX/NextCandidates/W6W8Elements/Generated/"+id+"/VFX_"+id+"_NEXT.prefab");
#endif
            Assert.That(prefab,Is.Not.Null,id+" must be built with ElementNextCandidateW6W8Authoring.BuildW6W8ForBatch before PlayMode.");var instance=UnityEngine.Object.Instantiate(prefab);var executor=instance.GetComponent<ElementNextCandidateVisualExecutor>();Assert.That(executor,Is.Not.Null,id);return executor;
        }

        private static void SetNumberParameter(ElementNextCandidateVisualExecutor target,string key,float value){var keys=(string[])GetPrivate(target,"contentKeys");var values=(float[])GetPrivate(target,"contentValues");var index=Array.IndexOf(keys,key);Assert.That(index,Is.GreaterThanOrEqualTo(0),target.EffectId+": "+key);values[index]=value;}
        private static void SetTextParameter(ElementNextCandidateVisualExecutor target,string key,string value){var keys=(string[])GetPrivate(target,"contentTextKeys");var values=(string[])GetPrivate(target,"contentTextValues");var index=Array.IndexOf(keys,key);Assert.That(index,Is.GreaterThanOrEqualTo(0),target.EffectId+": "+key);values[index]=value;}
        private static object GetPrivate(object target,string fieldName){return target.GetType().GetField(fieldName,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(target);}
        private static void SetPrivate(object target,string fieldName,object value){target.GetType().GetField(fieldName,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(target,value);}
    }
}

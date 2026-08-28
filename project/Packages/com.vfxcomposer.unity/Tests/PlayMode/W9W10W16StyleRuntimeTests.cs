using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace VFXComposer.Tests.PlayMode
{
    public sealed class W9W10W16StyleRuntimeTests
    {
        [UnityTest]
        public IEnumerator PixelAndCartoonPhases_AreTimeQuantizedInsteadOfSmoothlyInterpolated()
        {
            var pixel=Create("pixel_burst_impact_2d");pixel.Play();var a=pixel.LastAppliedStylePhase;yield return null;var b=pixel.LastAppliedStylePhase;Assert.That(b,Is.EqualTo(a).Within(.00001),"adjacent 60fps frames must share a 12fps pixel phase");yield return new WaitForSeconds(.09f);var scaled=pixel.LastAppliedStylePhase*.55f*12f;Assert.That(scaled,Is.EqualTo(Mathf.Round(scaled)).Within(.001));Object.Destroy(pixel.gameObject);
            var anime=Create("anime_smear_slash_2d");anime.Play();yield return new WaitForSeconds(.05f);var frame=anime.LastAppliedStylePhase*.42f*24f;Assert.That(frame,Is.EqualTo(Mathf.Round(frame)).Within(.001));Object.Destroy(anime.gameObject);
        }

        [UnityTest]
        public IEnumerator HoloGlitch_IsSeedDeterministicAndBounded()
        {
            var a=Create("holo_barrier_shield_3d");var b=Create("holo_barrier_shield_3d");a.Play();b.Play();yield return new WaitForSeconds(.24f);Assert.That(a.LastGlitchOffset,Is.EqualTo(b.LastGlitchOffset).Within(.000001));Assert.That(Mathf.Abs(a.LastGlitchOffset),Is.LessThanOrEqualTo(.0801f));a.ResetForPool();b.ResetForPool();Object.Destroy(a.gameObject);Object.Destroy(b.gameObject);
        }

        [UnityTest]
        public IEnumerator SustainedGhostAndSemirealEntries_SurviveCyclesAndResetCleanly()
        {
            var ghost=Create("phantom_wail_area_2d");var smoke=Create("smoke_plume_area_3d");ghost.Play();smoke.Play();yield return new WaitForSeconds(2.6f);Assert.That(ghost.IsAlive,Is.True);Assert.That(smoke.IsAlive,Is.True);Assert.That(ghost.StyleToken,Is.EqualTo("ghost"));Assert.That(smoke.GetStyleNumber("noise_detail_speed"),Is.EqualTo(1.7f).Within(.001));ghost.ResetForPool();smoke.ResetForPool();Assert.That(ghost.IsAlive,Is.False);Assert.That(smoke.IsAlive,Is.False);foreach(var particle in ghost.GetComponentsInChildren<ParticleSystem>(true))Assert.That(particle.particleCount,Is.EqualTo(0));Object.Destroy(ghost.gameObject);Object.Destroy(smoke.gameObject);
        }

        private static StyledVfxController Create(string id)
        {
            var root=new GameObject("RuntimeStyleFixture_"+id);var visual=new GameObject("Visual");visual.transform.SetParent(root.transform,false);visual.AddComponent<MeshFilter>();var renderer=visual.AddComponent<MeshRenderer>();var particle=visual.AddComponent<ParticleSystem>();particle.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);var main=particle.main;main.playOnAwake=false;main.maxParticles=8;main.startLifetime=.08f;
            var controller=root.AddComponent<StyledVfxController>();var token=id.StartsWith("pixel_")?"pixel":id.StartsWith("anime_")?"cartoon":id.StartsWith("holo_")?"holo":id.StartsWith("phantom_")?"ghost":"semireal";var sustained=token=="ghost"||token=="semireal";var duration=token=="pixel"?.55f:token=="cartoon"?.42f:token=="holo"?1.2f:token=="ghost"?1.2f:1.5f;Set(controller,"styleToken",token);Set(controller,"duration",duration);Set(controller,"lifecycle",sustained?StyledVfxLifecycle.Sustained:StyledVfxLifecycle.OneShot);Set(controller,"renderers",new Renderer[]{renderer});Set(controller,"animatedTransforms",new[]{visual.transform});Set(controller,"particles",new[]{particle});Set(controller,"lines",new LineRenderer[0]);Set(controller,"trails",new TrailRenderer[0]);if(token=="pixel"){Set(controller,"styleKeys",new[]{"snap_fps"});Set(controller,"styleValues",new[]{12f});}else if(token=="cartoon"){Set(controller,"styleKeys",new[]{"atlas_fps"});Set(controller,"styleValues",new[]{24f});}else if(token=="holo"){Set(controller,"styleKeys",new[]{"glitch_offset","glitch_rate"});Set(controller,"styleValues",new[]{.08f,5f});}else if(token=="ghost"){Set(controller,"styleKeys",new[]{"ghost_pulse_fps"});Set(controller,"styleValues",new[]{1.5f});}else{Set(controller,"styleKeys",new[]{"noise_detail_speed","noise_primary_speed"});Set(controller,"styleValues",new[]{1.7f,.3f});}controller.ResetForPool();return controller;
        }
        private static void Set(object target,string field,object value){target.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(target,value);}
    }
}

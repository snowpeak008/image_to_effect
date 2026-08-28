using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace VFXComposer.Tests.PlayMode
{
    public sealed class W3W8ElementRuntimeTests
    {
        [UnityTest]
        public IEnumerator ElementContentProtocolsRoundTripAndAllFamiliesResetCleanly()
        {
            var fixtures=new List<Fixture>();foreach(ElementContentFamily family in System.Enum.GetValues(typeof(ElementContentFamily))){if(family==ElementContentFamily.Neutral)continue;var fixture=new Fixture(StyledVfxProfile.Impact,StyledVfxLifecycle.OneShot,.045f,family);fixtures.Add(fixture);SetPrivate(fixture.Controller,"contentKeys",new[]{"density","enabled"});SetPrivate(fixture.Controller,"contentValues",new[]{12.5f,1f});SetPrivate(fixture.Controller,"contentTextKeys",new[]{"variant"});SetPrivate(fixture.Controller,"contentTextValues",new[]{family.ToString().ToLowerInvariant()});fixture.Controller.Play();Assert.That(fixture.Controller.ContentFamily,Is.EqualTo(family));Assert.That(fixture.Controller.GetContentNumber("density"),Is.EqualTo(12.5f));Assert.That(fixture.Controller.GetContentText("variant"),Is.EqualTo(family.ToString().ToLowerInvariant()));}
            yield return new WaitForSeconds(.09f);foreach(var fixture in fixtures){Assert.That(fixture.Controller.IsAlive,Is.False);Assert.That(fixture.Renderer.enabled,Is.False);fixture.Controller.ResetForPool();Assert.That(fixture.Particle.particleCount,Is.EqualTo(0));fixture.Destroy();}
        }

        [UnityTest]
        public IEnumerator SustainedElementFamiliesRemainStableForThreeCyclesThenStopAndReset()
        {
            var fixtures=new List<Fixture>();foreach(ElementContentFamily family in System.Enum.GetValues(typeof(ElementContentFamily))){if(family==ElementContentFamily.Neutral)continue;var fixture=new Fixture(StyledVfxProfile.Aura,StyledVfxLifecycle.Sustained,.04f,family);fixtures.Add(fixture);fixture.Controller.Play();}
            yield return new WaitForSeconds(.15f);foreach(var fixture in fixtures){Assert.That(fixture.Controller.IsAlive,Is.True,fixture.Controller.ContentFamily.ToString());Assert.That(fixture.Renderer.enabled,Is.True);Assert.That(fixture.Controller.NormalizedTime,Is.InRange(0f,1f));fixture.Controller.Stop(VfxStopMode.Immediate);Assert.That(fixture.Controller.IsAlive,Is.False);Assert.That(fixture.Renderer.enabled,Is.False);fixture.Controller.ResetForPool();fixture.Destroy();}
        }

        [UnityTest]
        public IEnumerator StyledBehaviorUsesTheSameDeterministicSamplerForHomingParabolaDashAndExpandRing()
        {
            var a=new Fixture(StyledVfxProfile.Projectile,StyledVfxLifecycle.OneShot,.4f,ElementContentFamily.Arcane);var b=new Fixture(StyledVfxProfile.Projectile,StyledVfxLifecycle.OneShot,.4f,ElementContentFamily.Arcane);ConfigureBehavior(a.Controller,"homing",new[]{"turn_rate","max_speed"},new[]{180f,5f});ConfigureBehavior(b.Controller,"homing",new[]{"turn_rate","max_speed"},new[]{180f,5f});SetPrivate(a.Controller,"seed",(uint)77);SetPrivate(b.Controller,"seed",(uint)77);a.Controller.Play();b.Controller.Play();Assert.That(a.Controller.BehaviorTrace.Frames.Count,Is.EqualTo(b.Controller.BehaviorTrace.Frames.Count));for(var i=0;i<a.Controller.BehaviorTrace.Frames.Count;i++)Assert.That(a.Controller.BehaviorTrace.Frames[i].Position,Is.EqualTo(b.Controller.BehaviorTrace.Frames[i].Position));
            var parabola=new Fixture(StyledVfxProfile.Projectile,StyledVfxLifecycle.OneShot,.5f,ElementContentFamily.Earth);ConfigureBehavior(parabola.Controller,"parabola",new[]{"apex_height","flight_time"},new[]{2f,.5f});parabola.Controller.Play();Assert.That(parabola.Controller.BehaviorTrace.Frames.Exists(v=>v.Position.y>1.5f),Is.True);
            var dash=new Fixture(StyledVfxProfile.Trail,StyledVfxLifecycle.OneShot,.35f,ElementContentFamily.Wind);ConfigureBehavior(dash.Controller,"dash",new[]{"distance","duration"},new[]{5f,.35f});dash.Controller.Play();Assert.That(dash.Controller.BehaviorTrace.Frames[dash.Controller.BehaviorTrace.Frames.Count-1].Position.x,Is.GreaterThan(4.9f));
            var ring=new Fixture(StyledVfxProfile.Impact,StyledVfxLifecycle.OneShot,.5f,ElementContentFamily.Fire);ConfigureBehavior(ring.Controller,"expand_ring",new[]{"max_radius","expand_speed","edge_thickness"},new[]{4f,8f,.3f});ring.Controller.Play();Assert.That(ring.Controller.BehaviorTrace.Frames[ring.Controller.BehaviorTrace.Frames.Count-1].Radius,Is.EqualTo(4f).Within(.01f));
            yield return null;a.Destroy();b.Destroy();parabola.Destroy();dash.Destroy();ring.Destroy();
        }

        private static void ConfigureBehavior(StyledVfxController controller,string motion,string[] keys,float[] values){SetPrivate(controller,"behaviorEnabled",true);SetPrivate(controller,"motionType",motion);SetPrivate(controller,"hitType","single");SetPrivate(controller,"emissionType","single");SetPrivate(controller,"timingType","instant");SetPrivate(controller,"motionKeys",keys);SetPrivate(controller,"motionValues",values);}

        private sealed class Fixture
        {
            public readonly GameObject Root;public readonly StyledVfxController Controller;public readonly MeshRenderer Renderer;public readonly ParticleSystem Particle;
            public Fixture(StyledVfxProfile profile,StyledVfxLifecycle lifecycle,float duration,ElementContentFamily family)
            {
                Root=new GameObject("ElementRuntimeFixture_"+family);var visual=new GameObject("Visual");visual.transform.SetParent(Root.transform,false);visual.AddComponent<MeshFilter>();Renderer=visual.AddComponent<MeshRenderer>();var particleGo=new GameObject("Particle");particleGo.transform.SetParent(Root.transform,false);Particle=particleGo.AddComponent<ParticleSystem>();Particle.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);var main=Particle.main;main.playOnAwake=false;main.loop=lifecycle==StyledVfxLifecycle.Sustained;main.duration=Mathf.Max(.05f,duration);main.startLifetime=.03f;var emission=Particle.emission;emission.rateOverTime=4;Controller=Root.AddComponent<StyledVfxController>();SetPrivate(Controller,"profile",profile);SetPrivate(Controller,"lifecycle",lifecycle);SetPrivate(Controller,"duration",duration);SetPrivate(Controller,"contentFamily",family);SetPrivate(Controller,"renderers",new Renderer[]{Renderer,particleGo.GetComponent<ParticleSystemRenderer>()});SetPrivate(Controller,"animatedTransforms",new[]{visual.transform});SetPrivate(Controller,"particles",new[]{Particle});Controller.ResetForPool();
            }
            public void Destroy(){Object.Destroy(Root);}
        }
        private static void SetPrivate(object target,string field,object value){target.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(target,value);}
    }
}

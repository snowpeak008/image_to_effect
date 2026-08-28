using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace VFXComposer.Tests.PlayMode
{
    public sealed class W11W17IndependentRuntimeTests
    {
        [UnityTest]
        public IEnumerator SustainedEnvironmentAndUi_StayAliveAcrossCyclesAndResetWithoutResidue()
        {
            var environment=new Fixture("rain_weather_volume",PlannedContentKind.Environment,PlannedContentLifecycle.Sustained,.04f,true,false);var ui=new Fixture("poison_veil_ui",PlannedContentKind.ScreenUi,PlannedContentLifecycle.Sustained,.04f,false,true);environment.Controller.SetIntensity(.3f);environment.Controller.SetWind(new Vector3(3,0,0));ui.Controller.SetStackLevel(3);environment.Controller.Play();ui.Controller.Play();yield return new WaitForSeconds(.15f);Assert.That(environment.Controller.IsAlive,Is.True);Assert.That(ui.Controller.IsAlive,Is.True);Assert.That(environment.Controller.Intensity,Is.EqualTo(.3f));Assert.That(ui.Controller.StackLevel,Is.EqualTo(3));environment.Controller.Stop(VfxStopMode.Immediate);ui.Controller.Stop(VfxStopMode.Immediate);Assert.That(environment.Particle.particleCount,Is.EqualTo(0));Assert.That(ui.Graphic.enabled,Is.False);environment.Destroy();ui.Destroy();
        }

        [UnityTest]
        public IEnumerator HitFlash_UsesExternalPropertyBlockWithoutChangingMaterialReference()
        {
            var fixture=new Fixture("hit_flash_status_2d",PlannedContentKind.HitFeedback,PlannedContentLifecycle.OneShot,.08f,true,false);var target=new GameObject("ExternalTarget");target.AddComponent<MeshFilter>();var renderer=target.AddComponent<MeshRenderer>();var materialBefore=renderer.sharedMaterial;fixture.Controller.BindExternalRenderers(new Renderer[]{renderer});fixture.Controller.Play();yield return null;var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block);Assert.That(block.GetFloat("_FlashAmount"),Is.GreaterThan(0));Assert.That(renderer.sharedMaterial,Is.SameAs(materialBefore));fixture.Controller.ResetForPool();renderer.GetPropertyBlock(block);Assert.That(block.GetFloat("_FlashAmount"),Is.EqualTo(0));Object.Destroy(target);fixture.Destroy();
        }

        [UnityTest]
        public IEnumerator GameUiProtocols_ClampAnchorSkipRarityFillAndReuseWithoutLeaks()
        {
            var fixture=new Fixture("gacha_single_reveal_ui",PlannedContentKind.GameUi,PlannedContentLifecycle.EventDriven,.2f,false,true);var anchor=new GameObject("Anchor",typeof(RectTransform)).GetComponent<RectTransform>();fixture.Controller.SetAnchorRect(anchor,true);fixture.Controller.SetRarity(9);fixture.Controller.SkipToReveal();Assert.That(fixture.Controller.IsAlive,Is.True);Assert.That(fixture.Controller.Rarity,Is.EqualTo(5));Assert.That(fixture.Controller.LastProtocolErrorCode,Is.Null);fixture.Controller.SetFillRatio(1.4f);Assert.That(fixture.Controller.LastProtocolErrorCode,Is.EqualTo("E1840"));yield return null;fixture.Controller.Stop(VfxStopMode.Immediate);fixture.Controller.Play();fixture.Controller.Stop(VfxStopMode.Immediate);Assert.That(fixture.Controller.PlayCount,Is.EqualTo(2));Assert.That(fixture.Graphic.enabled,Is.False);Object.Destroy(anchor.gameObject);fixture.Destroy();
        }

        [UnityTest]
        public IEnumerator IntensityAndStackRuntimeUpdates_AreClampedAndDoNotChangeParticleCapacity()
        {
            var fixture=new Fixture("combo_surge_aura_2d",PlannedContentKind.HitFeedback,PlannedContentLifecycle.Sustained,.05f,true,false);var capacity=fixture.Controller.ParticleCapacity;fixture.Controller.SetIntensity(-2);fixture.Controller.SetStackLevel(0);Assert.That(fixture.Controller.Intensity,Is.EqualTo(0));Assert.That(fixture.Controller.StackLevel,Is.EqualTo(1));fixture.Controller.SetIntensity(2);fixture.Controller.SetStackLevel(5);fixture.Controller.Play();yield return new WaitForSeconds(.07f);Assert.That(fixture.Controller.Intensity,Is.EqualTo(1));Assert.That(fixture.Controller.StackLevel,Is.EqualTo(5));Assert.That(fixture.Controller.ParticleCapacity,Is.EqualTo(capacity));fixture.Controller.ResetForPool();fixture.Destroy();
        }

        [UnityTest]
        public IEnumerator EndpointProtocols_UpdateBeamAndRewardGeometry_AndRejectUnsupportedEntries()
        {
            var beamRoot=new GameObject("BeamFixture");var line=beamRoot.AddComponent<LineRenderer>();line.positionCount=8;line.enabled=false;var beam=beamRoot.AddComponent<PlannedContentVfxController>();SetPrivate(beam,"contentId","lifesteal_link_beam_2d");SetPrivate(beam,"kind",PlannedContentKind.HitFeedback);SetPrivate(beam,"lifecycle",PlannedContentLifecycle.Sustained);SetPrivate(beam,"duration",1f);SetPrivate(beam,"renderers",new Renderer[]{line});SetPrivate(beam,"lines",new[]{line});SetPrivate(beam,"particles",new ParticleSystem[0]);SetPrivate(beam,"animatedTransforms",new Transform[0]);SetPrivate(beam,"graphics",new Graphic[0]);SetPrivate(beam,"parameterKeys",new[]{"sag"});SetPrivate(beam,"parameterValues",new[]{.5f});beam.ResetForPool();beam.SetWorldEndpoints(Vector3.zero,new Vector3(4,0,0));beam.Play();yield return null;Assert.That(line.GetPosition(0).x,Is.EqualTo(0).Within(.001));Assert.That(line.GetPosition(7).x,Is.EqualTo(4).Within(.001));Assert.That(line.GetPosition(3).y,Is.LessThan(-.4f));beam.ResetForPool();Assert.That(line.enabled,Is.False);Assert.That(beam.LastProtocolErrorCode,Is.Null);
            var unsupported=new Fixture("heal_glow_ui",PlannedContentKind.ScreenUi,PlannedContentLifecycle.OneShot,.1f,false,true);unsupported.Controller.SetWorldEndpoints(Vector3.zero,Vector3.one);Assert.That(unsupported.Controller.LastProtocolErrorCode,Is.EqualTo("E1840"));unsupported.Controller.ResetForPool();Assert.That(unsupported.Controller.LastProtocolErrorCode,Is.Null);Object.Destroy(beamRoot);unsupported.Destroy();
        }

        private sealed class Fixture
        {
            public readonly GameObject Root;public readonly PlannedContentVfxController Controller;public readonly ParticleSystem Particle;public readonly Graphic Graphic;
            public Fixture(string id,PlannedContentKind kind,PlannedContentLifecycle lifecycle,float duration,bool particle,bool graphic)
            {
                Particle=null;Graphic=null;Root=graphic?new GameObject("Fixture_"+id,typeof(RectTransform)):new GameObject("Fixture_"+id);Renderer[] renderers=new Renderer[0];ParticleSystem[] particles=new ParticleSystem[0];Graphic[] graphics=new Graphic[0];if(particle){var go=new GameObject("Particle");go.transform.SetParent(Root.transform,false);Particle=go.AddComponent<ParticleSystem>();Particle.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);var main=Particle.main;main.playOnAwake=false;main.loop=lifecycle==PlannedContentLifecycle.Sustained;main.duration=Mathf.Max(.05f,duration);main.startLifetime=.03f;main.maxParticles=12;renderers=new Renderer[]{go.GetComponent<ParticleSystemRenderer>()};particles=new[]{Particle};}if(graphic){var canvas=Root.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;var go=new GameObject("Graphic",typeof(RectTransform),typeof(CanvasRenderer),typeof(CoverageScreenFeedbackGraphic));go.transform.SetParent(Root.transform,false);Graphic=go.GetComponent<Graphic>();Graphic.color=Color.cyan;graphics=new[]{Graphic};SetAfterAddCanvas=canvas;}Controller=Root.AddComponent<PlannedContentVfxController>();SetPrivate(Controller,"contentId",id);SetPrivate(Controller,"kind",kind);SetPrivate(Controller,"lifecycle",lifecycle);SetPrivate(Controller,"duration",duration);SetPrivate(Controller,"renderers",renderers);SetPrivate(Controller,"particles",particles);SetPrivate(Controller,"animatedTransforms",new Transform[0]);SetPrivate(Controller,"graphics",graphics);if(SetAfterAddCanvas!=null)SetPrivate(Controller,"canvas",SetAfterAddCanvas);Controller.ResetForPool();
            }
            private Canvas SetAfterAddCanvas;public void Destroy(){Object.Destroy(Root);}
        }
        private static void SetPrivate(object target,string field,object value){target.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(target,value);}
    }
}

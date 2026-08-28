using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace VFXComposer.Tests.PlayMode
{
    public sealed class RuntimeSmokeTests
    {
        [UnityTest]
        public IEnumerator RuntimeAssembly_LoadsInPlayMode()
        {
            Assert.That(VFXComposer.VFXComposerRuntimeMarker.PackageVersion, Is.EqualTo("0.1.0"));
            yield return null;
        }

        [UnityTest]
        public IEnumerator GeneratedController_PlaysStagesStopsAndResetsWithoutEditorCode()
        {
            var fixture = new RuntimeFixture();
            yield return null;

            fixture.Controller.PlayLaunch();
            Assert.That(fixture.Controller.CurrentStage, Is.EqualTo(VFXComposer.VfxRuntimeStage.Launch));
            Assert.That(fixture.Launch.activeSelf, Is.True);
            Assert.That(fixture.Travel.activeSelf, Is.False);
            Assert.That(fixture.Impact.activeSelf, Is.False);
            Assert.That(fixture.LaunchParticles.isPlaying, Is.True);

            fixture.Controller.StartTravel();
            fixture.Controller.SetTravelTransform(new Vector3(2f, 1f, 0f), Quaternion.Euler(0f, 0f, 25f));
            fixture.Trail.AddPositions(new[] { new Vector3(-2f, 0f, 0f), new Vector3(2f, 1f, 0f) });
            Assert.That(fixture.Trail.positionCount, Is.GreaterThan(0));
            fixture.Controller.SetTravelTransform(new Vector3(2.1f, 1f, 0f), Quaternion.Euler(0f, 0f, 25f));
            Assert.That(fixture.Trail.positionCount, Is.GreaterThan(0), "Small continuous Travel steps must retain the TrailRenderer.");
            fixture.Controller.SetTravelTransform(new Vector3(40f, 0f, 0f), Quaternion.identity);
            Assert.That(fixture.Controller.transform.position, Is.EqualTo(new Vector3(40f, 0f, 0f)));
            Assert.That(fixture.Trail.positionCount, Is.EqualTo(0), "Large travel teleports must clear after assigning the new pose.");

            fixture.TravelParticles.Emit(5);
            fixture.Trail.AddPositions(new[] { new Vector3(39f, 0f, 0f), new Vector3(40f, 0f, 0f) });
            fixture.Controller.StopEffect(true);
            Assert.That(fixture.AllParticlesHaveCount(0), Is.True);
            Assert.That(fixture.AllTrailsHaveCount(0), Is.True);
            Assert.That(fixture.Controller.CurrentStage, Is.EqualTo(VFXComposer.VfxRuntimeStage.None));

            fixture.Controller.StartTravel();
            fixture.TravelParticles.Emit(3);
            fixture.Controller.StopEffect(false);
            Assert.That(fixture.Travel.activeSelf, Is.True, "Non-immediate stop leaves live particles to fade.");
            Assert.That(fixture.TravelParticles.isEmitting, Is.False);
            yield return new WaitForSeconds(.3f);
            Assert.That(fixture.TravelParticles.particleCount, Is.EqualTo(0));

            fixture.Controller.ResetForPool();
            Assert.That(fixture.Controller.transform.position, Is.EqualTo(Vector3.zero));
            Assert.That(fixture.AllTrailsHaveCount(0), Is.True);
            fixture.Controller.PlayImpact(new Vector3(3f, 0f, 0f));
            Assert.That(fixture.Controller.CurrentStage, Is.EqualTo(VFXComposer.VfxRuntimeStage.Impact));
            Assert.That(fixture.Impact.activeSelf, Is.True);
            fixture.Destroy();
        }

        [UnityTest]
        public IEnumerator RuntimeSequenceDriver_ProducesLaunchTravelImpactInOrder()
        {
            var fixture = new RuntimeFixture();
            var driver = fixture.Root.AddComponent<VFXComposer.VfxPreviewSequenceDriver>();
            SetPrivate(driver, "launchDuration", .01f);
            SetPrivate(driver, "travelDuration", .04f);
            var stages = new List<VFXComposer.VfxRuntimeStage>();
            fixture.Controller.StageChanged += stages.Add;
            yield return null;

            driver.PlayFullSequence(new Vector3(-1f, 0f, 0f), new Vector3(1f, 0f, 0f));
            yield return new WaitForSeconds(.18f);
            CollectionAssert.AreEqual(new[] { VFXComposer.VfxRuntimeStage.Launch, VFXComposer.VfxRuntimeStage.Travel, VFXComposer.VfxRuntimeStage.Impact }, stages);
            Assert.That(fixture.Controller.CurrentStage, Is.EqualTo(VFXComposer.VfxRuntimeStage.Impact));
            Assert.That(fixture.Controller.transform.position.x, Is.EqualTo(1f).Within(.001f));
            fixture.Destroy();
        }

        [UnityTest]
        public IEnumerator DisabledStage_DoesNotPlay_AndCancellingASequenceLeavesResetStable()
        {
            var fixture = new RuntimeFixture();
            SetPrivate(fixture.Controller, "launchEnabled", false);
            yield return null;
            fixture.Controller.PlayLaunch();
            Assert.That(fixture.Controller.CurrentStage, Is.EqualTo(VFXComposer.VfxRuntimeStage.None));
            Assert.That(fixture.Launch.activeSelf, Is.False);
            fixture.Controller.StartTravel();
            Assert.That(fixture.Controller.CurrentStage, Is.EqualTo(VFXComposer.VfxRuntimeStage.Travel));

            var driver = fixture.Root.AddComponent<VFXComposer.VfxPreviewSequenceDriver>();
            SetPrivate(driver, "launchDuration", .01f);
            SetPrivate(driver, "travelDuration", 1f);
            driver.PlayFullSequence(new Vector3(-1f, 0f, 0f), new Vector3(1f, 0f, 0f));
            yield return new WaitForSeconds(.04f);
            driver.StopSequence();
            fixture.Controller.ResetForPool();
            yield return new WaitForSeconds(.08f);
            Assert.That(fixture.Controller.CurrentStage, Is.EqualTo(VFXComposer.VfxRuntimeStage.None), "A cancelled preview sequence must not overwrite a manual Reset on a later frame.");
            fixture.Destroy();
        }

        private static void SetPrivate(object target, string fieldName, object value)
        {
            target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        }

        private sealed class RuntimeFixture
        {
            public readonly GameObject Root = new GameObject("RuntimeFixture");
            public readonly GameObject Launch;
            public readonly GameObject Travel;
            public readonly GameObject Impact;
            public readonly ParticleSystem LaunchParticles;
            public readonly ParticleSystem TravelParticles;
            public readonly ParticleSystem ImpactParticles;
            public readonly TrailRenderer Trail;
            public readonly VFXComposer.GeneratedVfxController Controller;

            public RuntimeFixture()
            {
                Launch = Stage("Launch", out LaunchParticles, false);
                Travel = Stage("Travel", out TravelParticles, true);
                Impact = Stage("Impact", out ImpactParticles, false);
                Trail = Travel.AddComponent<TrailRenderer>();
                Trail.time = .05f;
                Trail.minVertexDistance = .001f;
                Trail.emitting = true;
                Controller = Root.AddComponent<VFXComposer.GeneratedVfxController>();
                SetPrivate(Controller, "launchRoot", Launch);
                SetPrivate(Controller, "travelRoot", Travel);
                SetPrivate(Controller, "impactRoot", Impact);
            }

            private GameObject Stage(string name, out ParticleSystem particle, bool loop)
            {
                var stage = new GameObject(name);
                stage.transform.SetParent(Root.transform, false);
                particle = stage.AddComponent<ParticleSystem>();
                var main = particle.main;
                main.loop = loop;
                main.playOnAwake = false;
                main.startLifetime = .08f;
                main.maxParticles = 32;
                var emission = particle.emission;
                emission.rateOverTime = loop ? 20f : 0f;
                return stage;
            }

            public bool AllParticlesHaveCount(int expected)
            {
                foreach (var particle in Root.GetComponentsInChildren<ParticleSystem>(true)) if (particle.particleCount != expected) return false;
                return true;
            }

            public bool AllTrailsHaveCount(int expected)
            {
                foreach (var trail in Root.GetComponentsInChildren<TrailRenderer>(true)) if (trail.positionCount != expected) return false;
                return true;
            }

            public void Destroy() { Object.Destroy(Root); }
        }
    }
}

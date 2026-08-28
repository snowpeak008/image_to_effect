using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace VFXComposer.Tests.EditMode
{
    public sealed class SustainedEffectControllerTests
    {
        [Test]
        public void Lifecycle_StartSteadyStopCleanup_IsDeterministicAndBounded()
        {
            var fixture = CreateFixture();
            try
            {
                fixture.Controller.PlayWithSeed(77u);
                Assert.AreEqual(SustainedEffectState.Starting, fixture.Controller.State);
                Assert.AreEqual(77u, fixture.Controller.ActiveSeed);
                fixture.Controller.Advance(.21f);
                Assert.AreEqual(SustainedEffectState.Steady, fixture.Controller.State);
                Assert.IsTrue(fixture.Light.enabled);

                fixture.Controller.Stop(VfxStopMode.AllowTail);
                Assert.AreEqual(SustainedEffectState.Stopping, fixture.Controller.State);
                fixture.Controller.Advance(.61f);
                Assert.AreEqual(SustainedEffectState.Idle, fixture.Controller.State);
                Assert.IsFalse(fixture.Controller.IsAlive);
                Assert.IsTrue(fixture.Controller.ReadTelemetry().CleanupComplete);
            }
            finally
            {
                Object.DestroyImmediate(fixture.Root);
            }
        }

        [Test]
        public void Interrupt_UsesDistinctExitAndClearsBeforeDeadline()
        {
            var fixture = CreateFixture();
            try
            {
                fixture.Controller.Play();
                fixture.Controller.Advance(.21f);
                fixture.Controller.Interrupt();
                Assert.AreEqual(SustainedEffectState.Interrupted, fixture.Controller.State);
                fixture.Controller.Advance(.61f);
                Assert.AreEqual(SustainedEffectState.Idle, fixture.Controller.State);
                Assert.LessOrEqual(fixture.Controller.LifetimeElapsed, fixture.Controller.CleanupDeadline + .25f);
            }
            finally
            {
                Object.DestroyImmediate(fixture.Root);
            }
        }

        [Test]
        public void ImmediateStopAndPoolReset_ClearLightAndRestorePose()
        {
            var fixture = CreateFixture();
            try
            {
                var initial = fixture.Root.transform.position;
                fixture.Controller.Play();
                fixture.Root.transform.position = new Vector3(4f, 5f, 6f);
                fixture.Controller.Stop(VfxStopMode.Immediate);
                Assert.AreEqual(SustainedEffectState.Idle, fixture.Controller.State);
                Assert.AreEqual(initial, fixture.Root.transform.position);
                Assert.IsFalse(fixture.Light.enabled);
                Assert.AreEqual(0f, fixture.Light.intensity);
            }
            finally
            {
                Object.DestroyImmediate(fixture.Root);
            }
        }

        [Test]
        public void Events_MapToStartStopInterruptAndClear()
        {
            var fixture = CreateFixture();
            try
            {
                var payload = new VfxRuntimeEvent(new Vector3(1f, 2f, 3f), Quaternion.Euler(0f, 30f, 0f));
                Assert.IsTrue(fixture.Controller.SendEvent("start", payload));
                Assert.AreEqual(payload.Position, fixture.Root.transform.position);
                Assert.IsTrue(fixture.Controller.SendEvent("interrupt", payload));
                Assert.AreEqual(SustainedEffectState.Interrupted, fixture.Controller.State);
                Assert.IsTrue(fixture.Controller.SendEvent("clear", payload));
                Assert.AreEqual(SustainedEffectState.Idle, fixture.Controller.State);
                Assert.IsFalse(fixture.Controller.SendEvent("unknown", payload));
            }
            finally
            {
                Object.DestroyImmediate(fixture.Root);
            }
        }

        [Test]
        public void Starting_ExactSixtyFpsBoundary_DoesNotSlipOneFrame()
        {
            var fixture = CreateFixture();
            try
            {
                var serialized = new SerializedObject(fixture.Controller);
                serialized.FindProperty("startDuration").floatValue = .35f;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                fixture.Controller.PlayWithSeed(24001u);
                for (var frame = 1; frame <= 20; frame++)
                {
                    fixture.Controller.Advance(1f / 60f);
                    Assert.AreEqual(SustainedEffectState.Starting, fixture.Controller.State, "Starting must remain active before the declared .35-second boundary.");
                }

                fixture.Controller.Advance(1f / 60f);
                Assert.AreEqual(SustainedEffectState.Steady, fixture.Controller.State, "The exact 21-frame/.35-second boundary must not slip to frame 22 because of float accumulation.");
            }
            finally
            {
                Object.DestroyImmediate(fixture.Root);
            }
        }

        private static Fixture CreateFixture()
        {
            var root = new GameObject("SustainedFixture");
            root.transform.position = new Vector3(.25f, .5f, .75f);
            var start = Child(root, "Start");
            var steady = Child(root, "Steady");
            var stop = Child(root, "Stop");
            var interrupt = Child(root, "Interrupt");
            AddParticle(start, false, .1f);
            AddParticle(steady, true, .2f);
            AddParticle(stop, false, .1f);
            AddParticle(interrupt, false, .1f);
            var lightObject = Child(root, "Light");
            var light = lightObject.AddComponent<Light>();
            light.enabled = false;
            var controller = root.AddComponent<SustainedEffectController>();
            var serialized = new SerializedObject(controller);
            serialized.FindProperty("startRoot").objectReferenceValue = start;
            serialized.FindProperty("steadyRoot").objectReferenceValue = steady;
            serialized.FindProperty("stopRoot").objectReferenceValue = stop;
            serialized.FindProperty("interruptRoot").objectReferenceValue = interrupt;
            var lights = serialized.FindProperty("controlledLights");
            lights.arraySize = 1;
            lights.GetArrayElementAtIndex(0).objectReferenceValue = light;
            serialized.FindProperty("startDuration").floatValue = .2f;
            serialized.FindProperty("stopDuration").floatValue = .35f;
            serialized.FindProperty("interruptDuration").floatValue = .25f;
            serialized.FindProperty("cleanupDeadline").floatValue = .6f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            controller.ResetForPool();
            return new Fixture(root, controller, light);
        }

        private static GameObject Child(GameObject root, string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(root.transform, false);
            return child;
        }

        private static void AddParticle(GameObject root, bool loop, float lifetime)
        {
            var particle = root.AddComponent<ParticleSystem>();
            var main = particle.main;
            main.playOnAwake = false;
            main.loop = loop;
            main.duration = .1f;
            main.startLifetime = lifetime;
            main.maxParticles = 4;
            var emission = particle.emission;
            emission.rateOverTime = loop ? 1f : 0f;
            if (!loop) emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)1) });
        }

        private readonly struct Fixture
        {
            public readonly GameObject Root;
            public readonly SustainedEffectController Controller;
            public readonly Light Light;
            public Fixture(GameObject root, SustainedEffectController controller, Light light)
            {
                Root = root;
                Controller = controller;
                Light = light;
            }
        }
    }
}

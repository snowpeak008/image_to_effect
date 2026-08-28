using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace VFXComposer.Tests.PlayMode
{
    public sealed class SustainedEffectLifecyclePlayModeTests
    {
        [UnityTest]
        public IEnumerator UpdateDrivenLifecycle_EntersSteadyAndCleansAfterStop()
        {
            var root = new GameObject("SustainedPlayModeFixture");
            var start = Child(root, "Start");
            var steady = Child(root, "Steady");
            var stop = Child(root, "Stop");
            var interrupt = Child(root, "Interrupt");
            AddParticle(start, false, .03f);
            AddParticle(steady, true, .05f);
            AddParticle(stop, false, .03f);
            AddParticle(interrupt, false, .03f);
            var controller = root.AddComponent<SustainedEffectController>();

            Set(controller, "startRoot", start);
            Set(controller, "steadyRoot", steady);
            Set(controller, "stopRoot", stop);
            Set(controller, "interruptRoot", interrupt);
            Set(controller, "startDuration", .03f);
            Set(controller, "stopDuration", .04f);
            Set(controller, "cleanupDeadline", .2f);

            controller.PlayWithSeed(501u);
            yield return new WaitForSeconds(.06f);
            Assert.AreEqual(SustainedEffectState.Steady, controller.State);
            Assert.Greater(controller.ReadTelemetry().EmittingParticleSystemCount, 0);
            controller.Stop(VfxStopMode.AllowTail);
            yield return new WaitForSeconds(.25f);
            Assert.AreEqual(SustainedEffectState.Idle, controller.State);
            Assert.IsTrue(controller.ReadTelemetry().CleanupComplete);
            Object.Destroy(root);
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
            // Adding a ParticleSystem to an active GameObject starts it immediately in
            // PlayMode. Stop it before changing duration; Unity rejects duration edits
            // while the system is playing even when playOnAwake is disabled afterwards.
            particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particle.main;
            main.playOnAwake = false;
            main.loop = loop;
            main.duration = .05f;
            main.startLifetime = lifetime;
            main.maxParticles = 8;
            var emission = particle.emission;
            emission.rateOverTime = loop ? 8f : 0f;
            if (!loop) emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)2) });
        }

        private static void Set(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field, "Missing test configuration field: " + fieldName);
            field.SetValue(target, value);
        }
    }
}

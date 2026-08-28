using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace VFXComposer.Tests.PlayMode
{
    public sealed class StyledVfxRuntimeTests
    {
        [UnityTest]
        public IEnumerator OneShot_HidesAtIdlePlaysAndReturnsToCleanIdle()
        {
            var fixture = new Fixture(StyledVfxLifecycle.OneShot, .05f);
            yield return null;
            fixture.Controller.ResetForPool();
            Assert.That(fixture.Renderer.enabled, Is.False);
            Assert.That(fixture.Controller.IsAlive, Is.False);

            fixture.Controller.Play();
            Assert.That(fixture.Renderer.enabled, Is.True);
            Assert.That(fixture.Controller.IsAlive, Is.True);
            yield return new WaitForSeconds(.09f);

            Assert.That(fixture.Controller.IsAlive, Is.False);
            Assert.That(fixture.Renderer.enabled, Is.False);
            fixture.Destroy();
        }

        [UnityTest]
        public IEnumerator Sustained_LoopsUntilExplicitStopAndEventsRemainDeterministic()
        {
            var fixture = new Fixture(StyledVfxLifecycle.Sustained, .03f);
            yield return null;
            Assert.That(fixture.Controller.SendEvent("unknown", new VfxRuntimeEvent()), Is.False);
            Assert.That(fixture.Controller.SendEvent("trigger", new VfxRuntimeEvent(new Vector3(2f, 3f, 0f), Quaternion.identity)), Is.True);
            yield return new WaitForSeconds(.11f);

            Assert.That(fixture.Controller.IsAlive, Is.True);
            Assert.That(fixture.Renderer.enabled, Is.True);
            Assert.That(fixture.Controller.transform.position, Is.EqualTo(new Vector3(2f, 3f, 0f)));
            Assert.That(fixture.Controller.NormalizedTime, Is.InRange(0f, 1f));

            Assert.That(fixture.Controller.SendEvent("cancel", new VfxRuntimeEvent()), Is.True);
            Assert.That(fixture.Controller.IsAlive, Is.False);
            Assert.That(fixture.Renderer.enabled, Is.False);
            fixture.Controller.ResetForPool();
            Assert.That(fixture.Renderer.enabled, Is.False);
            fixture.Destroy();
        }

        private sealed class Fixture
        {
            public readonly GameObject Root = new GameObject("StyledRuntimeFixture");
            public readonly MeshRenderer Renderer;
            public readonly StyledVfxController Controller;

            public Fixture(StyledVfxLifecycle lifecycle, float duration)
            {
                var visual = new GameObject("Visual");
                visual.transform.SetParent(Root.transform, false);
                visual.AddComponent<MeshFilter>();
                Renderer = visual.AddComponent<MeshRenderer>();
                Controller = Root.AddComponent<StyledVfxController>();
                SetPrivate(Controller, "profile", StyledVfxProfile.Aura);
                SetPrivate(Controller, "lifecycle", lifecycle);
                SetPrivate(Controller, "duration", duration);
                SetPrivate(Controller, "renderers", new Renderer[] { Renderer });
                SetPrivate(Controller, "animatedTransforms", new[] { visual.transform });
                Controller.ResetForPool();
            }

            public void Destroy() { Object.Destroy(Root); }
        }

        private static void SetPrivate(object target, string fieldName, object value)
        {
            target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        }
    }
}

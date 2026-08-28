using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VFXComposer.W24;

namespace VFXComposer.Tests.PlayMode
{
    public sealed class W24S2RuntimeModulePlayModeTests
    {
        [UnityTest]
        public IEnumerator MovingEmitter_StopsEmittingWhenSourceStopsAndPoolResetClearsTrail()
        {
            var source = new GameObject("Source"); var root = new GameObject("Emitter");
            var trail = root.AddComponent<TrailRenderer>(); trail.time = 2f; trail.minVertexDistance = .001f;
            var module = root.AddComponent<W24MovingEmitterTrailProtocol>(); module.SetMotionSource(source.transform); module.SetTrails(new[] { trail }); module.Play(10u);
            source.transform.position = Vector3.right; yield return null;
            Assert.That(module.IsMoving, Is.True);
            source.transform.position = Vector3.right * 2f; yield return null;
            Assert.That(trail.positionCount, Is.GreaterThan(0));
            yield return null;
            Assert.That(module.IsMoving, Is.False, "stationary source may retain fading geometry but must not emit a new head");
            Assert.That(trail.emitting, Is.False);
            module.Stop(false);
            Assert.That(module.ReadSemanticTelemetry().CleanupComplete, Is.False, "A stopped emitter with live world-space trail points is still clearing, not clean.");
            Assert.That(module.ReadSemanticTelemetry().State, Is.EqualTo(W24SemanticState.Clearing));
            module.ResetForPool();
            Assert.That(trail.positionCount, Is.EqualTo(0));
            Assert.That(module.ReadSemanticTelemetry().CleanupComplete, Is.True);
            Object.Destroy(root); Object.Destroy(source);
        }

        [UnityTest]
        public IEnumerator FragmentSystem_IndependentlyMovesAndCleansUp()
        {
            var root = new GameObject("Fragments"); var one = new GameObject("One"); var two = new GameObject("Two"); one.transform.SetParent(root.transform); two.transform.SetParent(root.transform); two.transform.localPosition = Vector3.right;
            var module = root.AddComponent<W24FragmentMotionSystem>(); module.SetFragments(new[] { one.transform, two.transform }); module.Play(42u);
            Assert.That(module.ReadSemanticTelemetry().Seed, Is.EqualTo(42u));
            var beforeOne = one.transform.localPosition; var beforeTwo = two.transform.localPosition;
            yield return null;
            Assert.That(one.transform.localPosition, Is.Not.EqualTo(beforeOne)); Assert.That(two.transform.localPosition, Is.Not.EqualTo(beforeTwo));
            Assert.That(one.transform.localPosition - beforeOne, Is.Not.EqualTo(two.transform.localPosition - beforeTwo));
            module.Advance(2f);
            Assert.That(module.ReadSemanticTelemetry().LastEventId, Is.EqualTo("fragment_complete"));
            Assert.That(module.ReadSemanticTelemetry().CleanupComplete, Is.True);
            module.ResetForPool();
            Assert.That(one.activeSelf, Is.False); Assert.That(two.activeSelf, Is.False);
            Object.Destroy(root);
        }

        [UnityTest]
        public IEnumerator ModelBindingAdapter_ParentsVisualToRequestedSocket()
        {
            var model = new GameObject("Model"); var socket = new GameObject("hand_socket"); socket.transform.SetParent(model.transform);
            var visual = new GameObject("Visual"); var host = new GameObject("BindingHost"); var adapter = host.AddComponent<W24ModelBindingAdapter>();
            var type = typeof(W24ModelBindingAdapter);
            type.GetField("visualRoot", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(adapter, visual.transform);
            type.GetField("request", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(adapter, new W24ModelBindingRequest { Target = W24BindingTarget.Socket, TargetName = "hand_socket" });
            var originalPosition = new Vector3(.2f, .3f, .4f); visual.transform.localPosition = originalPosition;
            Assert.That(adapter.Bind(model.transform), Is.True); yield return null;
            Assert.That(visual.transform.parent, Is.EqualTo(socket.transform));
            Assert.That(adapter.ReadSemanticTelemetry().FaultCode, Is.EqualTo(W24BindingFault.None.ToString()));
            adapter.ResetForPool();
            Assert.That(visual.transform.parent, Is.Null, "Pool reset must not leave the visual attached to the previous owner's socket.");
            Assert.That(visual.transform.localPosition, Is.EqualTo(originalPosition));
            var missingModel = new GameObject("MissingSocketModel");
            Assert.That(adapter.Bind(missingModel.transform), Is.False);
            Assert.That(visual.transform.parent, Is.Null, "A failed rebind must restore the neutral parent instead of retaining the previous binding.");
            Object.Destroy(host); Object.Destroy(visual); Object.Destroy(model); Object.Destroy(missingModel);
        }

        [UnityTest]
        public IEnumerator LightingModule_ControlsActualLightWithinBudget()
        {
            var root = new GameObject("Lights"); var first = new GameObject("First").AddComponent<Light>(); var second = new GameObject("Second").AddComponent<Light>(); var replacement = new GameObject("Replacement").AddComponent<Light>(); first.transform.SetParent(root.transform); second.transform.SetParent(root.transform); replacement.transform.SetParent(root.transform);
            var module = root.AddComponent<W24RealLightingModule>();
            module.Configure3DLights(new Light[] { null, first, first, second }, 2);
            module.SetLights(true, 5f); yield return null;
            Assert.That(module.ReadSemanticTelemetry().ActiveItemCount, Is.EqualTo(2), "Duplicate serialized slots must count once by Unity object identity.");
            Assert.That(first.enabled, Is.True, "Null serialized slots must not consume the real-light budget.");
            Assert.That(second.enabled, Is.True, "A duplicate of the first light must not consume the second identity's budget slot.");

            module.Configure3DLights(new Light[] { replacement, null, replacement }, 2);
            var cleared = module.ReadSemanticTelemetry();
            Assert.That(first.enabled || second.enabled || replacement.enabled, Is.False, "Reconfiguration must immediately disable the old and replacement sets before reuse.");
            Assert.That(cleared.State, Is.EqualTo(W24SemanticState.Idle));
            Assert.That(cleared.ActiveItemCount, Is.Zero);
            Assert.That(cleared.CleanupComplete, Is.True);
            module.SetLights(true, 5f); yield return null;
            Assert.That(replacement.enabled, Is.True);
            Assert.That(module.ReadSemanticTelemetry().ActiveItemCount, Is.EqualTo(1));
            module.ResetForPool(); Assert.That(first.enabled || second.enabled || replacement.enabled, Is.False);
            Object.Destroy(root);
        }

        [UnityTest]
        public IEnumerator Timeline_EmitsEventsAndMaintainsDoubleExit()
        {
            var root = new GameObject("Timeline"); var timeline = root.AddComponent<W24SemanticTimeline>();
            timeline.Send(W24TimelineCommand.Continuous); yield return null;
            Assert.That(timeline.State, Is.EqualTo(W24SemanticState.Continuous));
            timeline.Send(W24TimelineCommand.Interrupt); yield return null;
            Assert.That(timeline.ReadSemanticTelemetry().State, Is.EqualTo(W24SemanticState.Interrupted));
            Object.Destroy(root);
        }
    }
}

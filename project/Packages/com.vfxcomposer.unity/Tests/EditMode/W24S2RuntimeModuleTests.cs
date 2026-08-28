using NUnit.Framework;
using UnityEngine;
using VFXComposer.W24;

namespace VFXComposer.Tests.EditMode
{
    public sealed class W24S2RuntimeModuleTests
    {
        [Test]
        public void MotionProtocol_StationarySamplesDoNotCreateAdditionalHeads()
        {
            var protocol = new W24MotionSampleProtocol(.1f);
            W24MotionSample sample;
            Assert.That(protocol.TrySample(Vector3.zero, 0f, out sample), Is.True);
            Assert.That(protocol.TrySample(new Vector3(.02f, 0f, 0f), .1f, out sample), Is.False);
            Assert.That(protocol.TrySample(new Vector3(.11f, 0f, 0f), .2f, out sample), Is.True);
            Assert.That(protocol.SampleCount, Is.EqualTo(2));
        }

        [Test]
        public void ModelBindingResolver_ReportsExplicitFaultsInsteadOfSilentFallback()
        {
            var root = new GameObject("Model");
            try
            {
                var missing = W24ModelBindingResolver.Resolve(root.transform, new W24ModelBindingRequest { Target = W24BindingTarget.Socket, TargetName = "hand_socket" });
                Assert.That(missing.Fault, Is.EqualTo(W24BindingFault.MissingTarget));
                var socket = new GameObject("hand_socket"); socket.transform.SetParent(root.transform);
                var found = W24ModelBindingResolver.Resolve(root.transform, new W24ModelBindingRequest { Target = W24BindingTarget.Socket, TargetName = "hand_socket" });
                Assert.That(found.IsBound, Is.True);
                Assert.That(found.Anchor, Is.EqualTo(socket.transform));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void FragmentKernel_SeedIsDeterministicAndFragmentsIntegrateIndependently()
        {
            var a = W24FragmentMotionKernel.Create(Vector3.zero, Quaternion.identity, 9u, 1f, 2f);
            var b = W24FragmentMotionKernel.Create(Vector3.zero, Quaternion.identity, 9u, 1f, 2f);
            var c = W24FragmentMotionKernel.Create(Vector3.zero, Quaternion.identity, 10u, 1f, 2f);
            Assert.That(a.Velocity, Is.EqualTo(b.Velocity));
            Assert.That(a.Velocity, Is.Not.EqualTo(c.Velocity));
            var advanced = W24FragmentMotionKernel.Advance(a, .25f, 1f);
            Assert.That(advanced.Position, Is.Not.EqualTo(a.Position));
            Assert.That(advanced.Rotation, Is.Not.EqualTo(a.Rotation));
            Assert.That(advanced.Velocity.magnitude, Is.LessThan(a.Velocity.magnitude));
        }

        [Test]
        public void LightBudget_ClampsRequestedCount()
        {
            Assert.That(W24LightBudget.SelectEnabledCount(5, 2), Is.EqualTo(2));
            Assert.That(W24LightBudget.SelectEnabledCount(1, 2), Is.EqualTo(1));
            Assert.That(W24LightBudget.SelectEnabledCount(2, -1), Is.EqualTo(0));
        }

        [Test]
        public void RealLighting_DeduplicatesByIdentityAndClearsOldAndReplacementSets()
        {
            var root = new GameObject("Lights");
            var first = new GameObject("First").AddComponent<Light>();
            var second = new GameObject("Second").AddComponent<Light>();
            var replacement = new GameObject("Replacement").AddComponent<Light>();
            first.transform.SetParent(root.transform);
            second.transform.SetParent(root.transform);
            replacement.transform.SetParent(root.transform);
            try
            {
                var module = root.AddComponent<W24RealLightingModule>();
                module.Configure3DLights(new[] { null, first, first, second }, 2);
                module.SetLights(true, 5f);
                Assert.That(first.enabled, Is.True);
                Assert.That(second.enabled, Is.True, "A duplicate of the first Light must not consume the second identity's budget slot.");
                Assert.That(module.ReadSemanticTelemetry().ActiveItemCount, Is.EqualTo(2), "ActiveItemCount is a count of unique emitting identities.");

                module.Configure3DLights(new[] { replacement, null, replacement }, 2);
                var cleared = module.ReadSemanticTelemetry();
                Assert.That(first.enabled || second.enabled || replacement.enabled, Is.False, "Replacing the configured set must synchronously disable both old and replacement identities.");
                Assert.That(cleared.State, Is.EqualTo(W24SemanticState.Idle));
                Assert.That(cleared.ActiveItemCount, Is.Zero);
                Assert.That(cleared.CleanupComplete, Is.True);

                module.SetLights(true, 5f);
                Assert.That(replacement.enabled, Is.True);
                Assert.That(module.ReadSemanticTelemetry().ActiveItemCount, Is.EqualTo(1), "Duplicate replacement slots still describe one Light identity.");
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void SemanticTimeline_HasSeparateCompletionAndInterruptionExits()
        {
            var complete = new W24SemanticTimelineModel(.1f);
            complete.Send(W24TimelineCommand.Impulse); complete.Advance(.1f);
            Assert.That(complete.State, Is.EqualTo(W24SemanticState.Completed));
            Assert.That(complete.Events[complete.Events.Count - 1].EventId, Is.EqualTo("completed"));
            var interrupted = new W24SemanticTimelineModel(.1f);
            interrupted.Send(W24TimelineCommand.Continuous); interrupted.Send(W24TimelineCommand.Interrupt);
            Assert.That(interrupted.State, Is.EqualTo(W24SemanticState.Interrupted));
            Assert.That(interrupted.Events[interrupted.Events.Count - 1].EventId, Is.EqualTo("interrupted"));
        }

        [Test]
        public void RuntimeModules_ExposeSemanticTelemetry()
        {
            var root = new GameObject("W24S2");
            try
            {
                var timeline = root.AddComponent<W24SemanticTimeline>(); timeline.Send(W24TimelineCommand.Continuous);
                var telemetry = timeline.ReadSemanticTelemetry();
                Assert.That(telemetry.Module, Is.EqualTo("semantic_timeline"));
                Assert.That(telemetry.State, Is.EqualTo(W24SemanticState.Continuous));
                Assert.That(telemetry.EventSerial, Is.GreaterThan(0));
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}

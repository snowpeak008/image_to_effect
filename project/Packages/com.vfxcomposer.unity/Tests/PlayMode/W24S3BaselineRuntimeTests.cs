using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VFXComposer;
using VFXComposer.W24;

namespace VFXComposer.Tests.PlayMode
{
    public sealed class W24S3BaselineRuntimeTests
    {
        [UnityTest]
        public IEnumerator ProjectileEntry_ControlsMotionTrailThroughOnlyItsPublicEntryEvents()
        {
            var root = new GameObject("ProjectileRuntimeEntry"); var travel = Child(root, "Travel"); var trail = travel.AddComponent<TrailRenderer>(); trail.time = 1f; trail.minVertexDistance = .001f;
            var motion = root.AddComponent<W24MovingEmitterTrailProtocol>(); motion.SetTrails(new[] { trail });
            var entry = root.AddComponent<W24S3RuntimeEntry>(); Configure(entry, "activeRoot", travel); Configure(entry, "movingTrail", motion); Configure(entry, "timeline", root.AddComponent<W24SemanticTimeline>());
            Assert.Throws<System.ArgumentOutOfRangeException>(() => entry.SetCaptureSeed(0u));
            entry.SetCaptureSeed(777u);
            entry.Initialize(new VfxRuntimeContext(Vector3.zero, Quaternion.identity)); entry.Play();
            Assert.That(entry.ReadEmitterHistory().Seed, Is.EqualTo(777u));
            Assert.That(motion.UsesWorldSpaceHistory, Is.True, "Unity 2022.3 TrailRenderer history is intrinsically world-space and the protocol must explicitly require that invariant.");
            Assert.That(entry.SendEvent("travel", new VfxRuntimeEvent(Vector3.right, Quaternion.identity)), Is.True); yield return null;
            Assert.That(entry.SendEvent("travel", new VfxRuntimeEvent(Vector3.right * 2f, Quaternion.identity)), Is.True); yield return null;
            Assert.That(trail.positionCount, Is.GreaterThan(0));
            var history = entry.ReadEmitterHistory();
            Assert.That(history.Count, Is.GreaterThanOrEqualTo(2));
            Assert.That(history.Samples.All(value => value.IsNewHead), Is.True);
            Assert.That(history.Samples[history.Count - 1].Position.x, Is.GreaterThan(history.Samples[0].Position.x));
            entry.ResetForPool(); Assert.That(trail.positionCount, Is.EqualTo(0)); Assert.That(entry.IsAlive, Is.False);
            var cleared = entry.ReadEmitterHistory(); Assert.That(cleared.IsCleared, Is.True); Assert.That(cleared.LastClearReason, Is.EqualTo("pool_reset"));
            Object.Destroy(root);
        }

        [Test]
        public void MissingBindingProbeReport_CoversAllFrozenFaultsWithoutFallback()
        {
            var model = new GameObject("ProbeModel");
            try
            {
                var report = W24BindingDiagnosticProbes.Run(model.transform);
                Assert.That(report.Passed, Is.True);
                Assert.That(report.Results.Select(value => value.ProbeId), Is.EquivalentTo(new[] { "missing_socket", "missing_renderer", "missing_mesh", "missing_bone" }));
                Assert.That(report.Results.Select(value => value.ActualFault), Is.EquivalentTo(new[] { W24BindingFault.MissingTarget, W24BindingFault.MissingRenderer, W24BindingFault.MissingMesh, W24BindingFault.MissingBone }));
                Assert.That(report.Results.All(value => !value.HadAnchor && !value.HadRenderer), Is.True);
                StringAssert.Contains("\"schema\":\"w24-binding-probes/v1\"", report.ToJson());
                StringAssert.Contains("\"passed\":true", report.ToJson());
            }
            finally { Object.DestroyImmediate(model); }
        }

        [UnityTest]
        public IEnumerator BindingEntry_UsesConfiguredModelRoot_AndReportsMissingRootWithoutFallback()
        {
            var model = new GameObject("Model"); var socket = new GameObject("weapon_socket"); socket.transform.SetParent(model.transform);
            var root = new GameObject("BindingRuntimeEntry"); var visual = Child(root, "SocketVisualRoot"); var adapter = root.AddComponent<W24ModelBindingAdapter>();
            Configure(adapter, "visualRoot", visual.transform); Configure(adapter, "request", new W24ModelBindingRequest { Target = W24BindingTarget.Socket, TargetName = "weapon_socket" });
            var entry = root.AddComponent<W24S3RuntimeEntry>(); Configure(entry, "modelBinding", adapter); Configure(entry, "bindingVisualRoot", visual.transform); Configure(entry, "requiresModelBinding", true); Configure(entry, "timeline", root.AddComponent<W24SemanticTimeline>());
            entry.ConfigureModelRoot(model.transform); entry.Initialize(new VfxRuntimeContext(Vector3.zero, Quaternion.identity)); entry.Play(); yield return null;
            Assert.That(visual.transform.parent, Is.EqualTo(socket.transform)); Assert.That(entry.LastBindingFault, Is.EqualTo(W24BindingFault.None));
            entry.ResetForPool(); Assert.That(visual.transform.parent, Is.EqualTo(root.transform));
            entry.ConfigureModelRoot(null); entry.Initialize(new VfxRuntimeContext(Vector3.zero, Quaternion.identity)); entry.Play(); yield return null;
            Assert.That(entry.LastBindingFault, Is.EqualTo(W24BindingFault.MissingRoot)); Assert.That(entry.IsAlive, Is.False);
            Object.Destroy(root); Object.Destroy(model);
        }

        [UnityTest]
        public IEnumerator BindingEntry_FragmentEventRunsIndependentKinematicsThroughTheEntry()
        {
            var model = new GameObject("MovingModel"); var socket = Child(model, "weapon_socket");
            var root = new GameObject("FragmentRuntimeEntry"); var visual = Child(root, "SocketVisualRoot"); visual.transform.SetParent(socket.transform, false); var one = Child(visual, "IndependentFragment_0"); var two = Child(visual, "IndependentFragment_1"); two.transform.localPosition = Vector3.right;
            var system = visual.AddComponent<W24FragmentMotionSystem>(); system.SetFragments(new[] { one.transform, two.transform });
            var entry = root.AddComponent<W24S3RuntimeEntry>(); Configure(entry, "bindingVisualRoot", visual.transform); Configure(entry, "fragments", system); Configure(entry, "timeline", root.AddComponent<W24SemanticTimeline>());
            entry.Initialize(new VfxRuntimeContext(Vector3.zero, Quaternion.identity)); entry.Play(); var beforeOne = one.transform.localPosition; var beforeTwo = two.transform.localPosition;
            Assert.That(entry.SendEvent("fragment", new VfxRuntimeEvent(Vector3.zero, Quaternion.identity)), Is.True); yield return null;
            Assert.That(one.transform.localPosition - beforeOne, Is.Not.EqualTo(two.transform.localPosition - beforeTwo));
            Assert.That(visual.transform.parent, Is.EqualTo(root.transform), "fragment exit must detach from the moving model while preserving its world pose");
            var detachedWorld = visual.transform.position; socket.transform.position += Vector3.right * 5f; yield return null;
            Assert.That(Vector3.Distance(visual.transform.position, detachedWorld), Is.LessThan(.5f), "detached fragments must not inherit later socket movement");
            entry.ResetForPool(); Assert.That(one.activeSelf && two.activeSelf, Is.False); Object.Destroy(root); Object.Destroy(model);
        }

        [UnityTest]
        public IEnumerator ImmediateStop_RecordsInterruptBeforePoolReset_AndLeavesNoLiveState()
        {
            var root = new GameObject("InterruptRuntimeEntry"); var timeline = root.AddComponent<W24SemanticTimeline>(); var entry = root.AddComponent<W24S3RuntimeEntry>(); Configure(entry, "timeline", timeline);
            var interrupted = false; timeline.EventRaised += item => interrupted |= item.EventId == "interrupted";
            entry.Initialize(new VfxRuntimeContext(Vector3.zero, Quaternion.identity)); entry.Play(); yield return null;
            entry.Stop(VfxStopMode.Immediate);
            Assert.That(interrupted, Is.True, "cancel/reset must preserve a distinct interrupt event before returning the object to its pool-safe state");
            Assert.That(entry.IsAlive, Is.False); Assert.That(entry.ReadSemanticTelemetry().CleanupComplete, Is.True);
            Object.Destroy(root);
        }

        [UnityTest]
        public IEnumerator LightingEntry_ControlsActualLightsAndCleanupThroughItsPublicLifecycle()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) Assert.Ignore("URP Lit is required for the S3 source-body isolation test.");
            var material = new Material(shader); material.SetColor("_EmissionColor", new Color(2.1f, .42f, .08f, 1f)); material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive; material.EnableKeyword("_EMISSION");
            var root = new GameObject("RealLightRuntimeEntry"); var muzzle = Child(root, "MuzzleFlashLight").AddComponent<Light>(); var flame = Child(root, "SustainedFireLight").AddComponent<Light>();
            var active = Child(root, "SustainedFlame"); var coreObject = GameObject.CreatePrimitive(PrimitiveType.Sphere); coreObject.name = "PhysicalLightCoreMesh"; coreObject.transform.SetParent(active.transform, false);
            Object.DestroyImmediate(coreObject.GetComponent<Collider>()); var core = coreObject.GetComponent<MeshRenderer>(); core.sharedMaterial = material;
            muzzle.shadows = flame.shadows = LightShadows.None; var lighting = root.AddComponent<W24RealLightingModule>(); Configure(lighting, "lights3D", new[] { muzzle, flame }); Configure(lighting, "maximum3DLights", 2);
            var entry = root.AddComponent<W24S3RuntimeEntry>(); Configure(entry, "activeRoot", active); Configure(entry, "lighting", lighting); Configure(entry, "timeline", root.AddComponent<W24SemanticTimeline>());
            entry.Initialize(new VfxRuntimeContext(Vector3.zero, Quaternion.identity)); entry.Play(); yield return null;
            Assert.That(muzzle.enabled && flame.enabled, Is.True); Assert.That(entry.ReadSemanticTelemetry().State, Is.EqualTo(W24SemanticState.Continuous));
            var properties = new MaterialPropertyBlock(); core.GetPropertyBlock(properties);
            Assert.That(properties.isEmpty, Is.True, "The source body must not depend on an unserialized runtime MPB for its emission.");
            Assert.That(material.IsKeywordEnabled("_EMISSION"), Is.True);
            Assert.That(material.GetColor("_EmissionColor").maxColorComponent, Is.GreaterThan(1f), "The effect-owned core material itself must carry truthful non-black HDR emission.");
            entry.Stop(VfxStopMode.Immediate); Assert.That(muzzle.enabled || flame.enabled, Is.False); Assert.That(entry.ReadSemanticTelemetry().CleanupComplete, Is.True);
            Assert.That(core.gameObject.activeInHierarchy, Is.False, "Immediate pool cleanup must remove the declared source body together with the real lights.");
            Object.Destroy(root); Object.Destroy(material);
        }

        private static GameObject Child(GameObject parent, string name) { var result = new GameObject(name); result.transform.SetParent(parent.transform, false); return result; }
        private static void Configure(object target, string name, object value) { target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value); }
    }
}

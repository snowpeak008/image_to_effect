using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace VFXComposer.Tests.PlayMode
{
    public sealed class BeamCapabilityRuntimeTests
    {
        private static readonly string[] Ids =
        {
            "cap_hitscan_beam_3d", "cap_sustained_beam_3d", "cap_sweep_beam_3d", "cap_charge_beam_3d",
            "cap_reflect_beam_3d", "cap_occlude_beam_3d", "cap_converge_beam_3d", "cap_arclink_beam_2d"
        };

        [UnityTest]
        public IEnumerator Hitscan_WholeLineFadesToZeroWithEndpointMarker()
        {
            var instance = Instantiate("cap_hitscan_beam_3d", out var controller, out var visual);
            controller.Play();
            controller.EvaluateVisualAtTime(0f);
            var initialWidth = visual.EffectiveWidth;
            Assert.That(visual.VisibleLineCount, Is.EqualTo(1));
            Assert.That(visual.EndpointMarkerVisible, Is.True);
            Assert.That(visual.HitscanFade, Is.EqualTo(1f).Within(.0001f));
            controller.EvaluateVisualAtTime(.075f);
            Assert.That(visual.LineAlpha, Is.InRange(.45f, .55f));
            Assert.That(visual.EffectiveWidth, Is.LessThan(initialWidth));
            var lineBlock = new MaterialPropertyBlock();
            instance.GetComponent<LineRenderer>().GetPropertyBlock(lineBlock);
            Assert.That(lineBlock.GetFloat("_GlobalAlpha"), Is.InRange(.45f, .55f), "fade must reach the real renderer property block");
            controller.EvaluateVisualAtTime(.15f);
            Assert.That(visual.HitscanFade, Is.EqualTo(0f).Within(.0001f));
            Assert.That(visual.LineAlpha, Is.EqualTo(0f).Within(.0001f));
            Assert.That(visual.EffectiveWidth, Is.EqualTo(0f).Within(.0001f));
            Assert.That(visual.EndpointMarkerVisible, Is.False);
            Object.Destroy(instance);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Sustained_ExplicitStartStopFollowsBothAnchorsAndRetilesByLength()
        {
            var instance = Instantiate("cap_sustained_beam_3d", out var controller, out var visual);
            var source = new GameObject("RuntimeSourceAnchor");
            var target = new GameObject("RuntimeTargetAnchor");
            source.transform.position = instance.transform.TransformPoint(new Vector3(.2f, -.1f, 0f));
            target.transform.position = instance.transform.TransformPoint(new Vector3(3f, .2f, 0f));
            Assert.That(controller.BindBeamEndpoints(source.transform, target.transform), Is.True);
            controller.Play();
            controller.EvaluateVisualAtTime(.2f);
            var firstLength = visual.BeamLength;
            var firstTiles = visual.TextureTileCount;
            Assert.That(Vector3.Distance(visual.EffectiveSource, new Vector3(.2f, -.1f, 0f)), Is.LessThan(.0001f));
            target.transform.position = instance.transform.TransformPoint(new Vector3(4.4f, .7f, 0f));
            source.transform.position = instance.transform.TransformPoint(new Vector3(.4f, .1f, 0f));
            controller.EvaluateVisualAtTime(.25f);
            Assert.That(Vector3.Distance(visual.EffectiveSource, new Vector3(.4f, .1f, 0f)), Is.LessThan(.0001f));
            Assert.That(Vector3.Distance(visual.EffectiveTarget, new Vector3(4.4f, .7f, 0f)), Is.LessThan(.0001f));
            Assert.That(visual.BeamLength, Is.GreaterThan(firstLength));
            Assert.That(visual.TextureTileCount, Is.GreaterThan(firstTiles));
            Assert.That(controller.StopBeam(), Is.True);
            Assert.That(visual.AllVisualsHidden, Is.True);
            Assert.That(controller.StartBeam(Vector3.zero, new Vector3(2f, 0f, 0f)), Is.True);
            Assert.That(controller.IsAlive, Is.True);
            controller.StopBeam();
            Object.Destroy(source);
            Object.Destroy(target);
            Object.Destroy(instance);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Sweep_TraversesNonZeroBoundedArcWithoutSustainedOverwrite()
        {
            var instance = Instantiate("cap_sweep_beam_3d", out var controller, out var visual);
            var forbiddenFixedTarget = new GameObject("SweepMustIgnoreSustainedTargetBinding");
            forbiddenFixedTarget.transform.position = instance.transform.TransformPoint(new Vector3(-3f, -2f, 0f));
            visual.BindEndpoints(null, forbiddenFixedTarget.transform);
            controller.Play();
            controller.EvaluateVisualAtTime(0f);
            var start = visual.EffectiveTarget;
            controller.EvaluateVisualAtTime(.5f);
            var middle = visual.EffectiveTarget;
            controller.EvaluateVisualAtTime(1f);
            Assert.That(Vector3.Distance(start, middle), Is.GreaterThan(.1f));
            Assert.That(Mathf.Abs(visual.SweepAngle), Is.GreaterThan(1f));
            Assert.That(visual.SweepAngularVelocity, Is.LessThanOrEqualTo(visual.SweepSpeedLimit + .05f));
            Assert.That(Vector3.Distance(visual.EffectiveTarget, new Vector3(-3f, -2f, 0f)), Is.GreaterThan(.1f));
            Assert.That(visual.SweepUsesTraceTarget, Is.True);
            Assert.That(visual.SweepInertia, Is.GreaterThan(0f));
            Object.Destroy(forbiddenFixedTarget);
            Object.Destroy(instance);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ChargeScale_HasThreeWidthBrightnessTiersAndDistinctCompleteCancelExits()
        {
            var instance = Instantiate("cap_charge_beam_3d", out var controller, out var visual);
            controller.Play();
            controller.EvaluateVisualAtTime(.1f);
            var width1 = visual.EffectiveWidth;
            var light1 = visual.Brightness;
            Assert.That(visual.ChargeTier, Is.EqualTo(1));
            controller.EvaluateVisualAtTime(.7f);
            var width2 = visual.EffectiveWidth;
            var light2 = visual.Brightness;
            Assert.That(visual.ChargeTier, Is.EqualTo(2));
            controller.EvaluateVisualAtTime(1.3f);
            var width3 = visual.EffectiveWidth;
            var light3 = visual.Brightness;
            Assert.That(visual.ChargeTier, Is.EqualTo(3));
            Assert.That(width2 / width1, Is.GreaterThanOrEqualTo(1.6f));
            Assert.That(width3 / width2, Is.GreaterThanOrEqualTo(1.6f));
            Assert.That(light2, Is.GreaterThan(light1));
            Assert.That(light3, Is.GreaterThan(light2));
            var chargeBlock = new MaterialPropertyBlock();
            instance.GetComponent<LineRenderer>().GetPropertyBlock(chargeBlock);
            Assert.That(chargeBlock.GetFloat("_Intensity"), Is.EqualTo(light3).Within(.001f), "brightness tier must reach the real line renderer");

            controller.Complete();
            Assert.That(visual.ExitVisual, Is.EqualTo("endpoint_burst"));
            Assert.That(visual.EndpointMarkerVisible, Is.True);
            var burstScale = visual.EndpointMarkerScale.magnitude;
            visual.EvaluateExitAtTime(.21f);
            Assert.That(visual.EndpointMarkerVisible, Is.False);

            controller.Play();
            controller.EvaluateVisualAtTime(.7f);
            controller.Cancel();
            Assert.That(visual.ExitVisual, Is.EqualTo("cancel_collapse"));
            Assert.That(visual.EndpointMarkerVisible, Is.True);
            Assert.That(visual.EndpointMarkerScale.magnitude, Is.LessThan(burstScale));
            visual.EvaluateExitAtTime(.21f);
            Assert.That(visual.AllVisualsHidden, Is.True);
            Object.Destroy(instance);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Reflect_BuildsTraceDrivenPolylineMarkersAndSegmentDamping()
        {
            var instance = Instantiate("cap_reflect_beam_3d", out var controller, out var visual);
            controller.Play();
            controller.EvaluateVisualAtTime(1.8f);
            Assert.That(visual.ReflectSegmentCount, Is.EqualTo(3));
            Assert.That(visual.PrimaryPointCount, Is.EqualTo(4));
            Assert.That(visual.BounceMarkerCount, Is.EqualTo(2));
            Assert.That(visual.MarkerParticleCount, Is.EqualTo(2));
            for (var i = 1; i < visual.ReflectSegmentCount; i++)
            {
                Assert.That(visual.GetReflectSegmentWidth(i), Is.LessThan(visual.GetReflectSegmentWidth(i - 1)));
                Assert.That(visual.GetReflectSegmentBrightness(i), Is.LessThan(visual.GetReflectSegmentBrightness(i - 1)));
            }
            var segmentLines = instance.GetComponentsInChildren<LineRenderer>(true);
            for (var i = 1; i < segmentLines.Length; i++)
            {
                var beforeBlock = new MaterialPropertyBlock();
                var afterBlock = new MaterialPropertyBlock();
                segmentLines[i - 1].GetPropertyBlock(beforeBlock);
                segmentLines[i].GetPropertyBlock(afterBlock);
                Assert.That(afterBlock.GetFloat("_Intensity"), Is.LessThan(beforeBlock.GetFloat("_Intensity")), "actual segment brightness must damp");
            }
            var bounceEvents = controller.Trace.Events.Where(value => value.Type == "on_bounce" && value.Detail == "reflect").ToArray();
            for (var segment = 0; segment < visual.ReflectSegmentCount; segment++)
            {
                var direction = visual.GetPrimaryPoint(segment + 1) - visual.GetPrimaryPoint(segment);
                var contractDirection = segment == 0 ? bounceEvents[0].Before : bounceEvents[segment - 1].After;
                Assert.That(Vector3.Angle(direction, contractDirection), Is.LessThan(.1f), "segment " + segment + " must follow reflection trace direction");
            }
            Object.Destroy(instance);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Occlude_FailsClosedThenTruncatesAtFirstBlockerAndExtendsSameFrame()
        {
            var instance = Instantiate("cap_occlude_beam_3d", out var controller, out var visual);
            controller.StartBeam(Vector3.zero, new Vector3(4f, 0f, 0f));
            controller.EvaluateVisualAtTime(.1f);
            Assert.That(visual.OcclusionFailClosed, Is.True);
            Assert.That(visual.VisibleLineCount, Is.EqualTo(0));

            var obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obstacle.name = "RuntimeMovableBlocker";
            obstacle.transform.position = instance.transform.TransformPoint(new Vector3(2f, 0f, 0f));
            obstacle.transform.localScale = new Vector3(.4f, 1f, 1f);
            var probeObject = new GameObject("RuntimeExplicitProbe");
            var probe = probeObject.AddComponent<BeamCapabilityObstacleProbe>();
            probe.SetBlockers(obstacle.GetComponent<Collider>());
            visual.SetObstacleProbe(probe);
            controller.EvaluateVisualAtTime(.12f);
            Assert.That(visual.OcclusionFailClosed, Is.False);
            Assert.That(visual.ObstacleBlocked, Is.True);
            Assert.That(visual.EffectiveTarget.x, Is.InRange(1.7f, 1.9f));
            Assert.That(visual.EndpointMarkerVisible, Is.True, "burn point is visible at the first blocker");

            obstacle.transform.position = instance.transform.TransformPoint(new Vector3(2f, 3f, 0f));
            controller.EvaluateVisualAtTime(.14f);
            Assert.That(visual.ObstacleBlocked, Is.False);
            Assert.That(visual.EffectiveTarget.x, Is.EqualTo(4f).Within(.001f));
            Assert.That(visual.LastObstacleResponseFrames, Is.LessThanOrEqualTo(2));

            visual.ClearObstacleProbe();
            controller.EvaluateVisualAtTime(.16f);
            Assert.That(visual.OcclusionFailClosed, Is.True);
            Assert.That(visual.AllVisualsHidden, Is.True);
            Object.Destroy(obstacle);
            Object.Destroy(probeObject);
            Object.Destroy(instance);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Converge_UsesFourRealLinesWithRingSourcesAndGrowingSharedFocus()
        {
            var instance = Instantiate("cap_converge_beam_3d", out var controller, out var visual);
            controller.Play();
            controller.EvaluateVisualAtTime(0f);
            var initialFocus = visual.FocusScale;
            controller.EvaluateVisualAtTime(1.5f);
            Assert.That(visual.ConvergeLineCount, Is.EqualTo(4));
            Assert.That(visual.VisibleLineCount, Is.EqualTo(4));
            for (var i = 0; i < 4; i++)
            {
                Assert.That(visual.GetConvergeSource(i).magnitude, Is.EqualTo(1f).Within(.001f));
                for (var j = i + 1; j < 4; j++) Assert.That(Vector3.Distance(visual.GetConvergeSource(i), visual.GetConvergeSource(j)), Is.GreaterThan(.5f));
            }
            Assert.That(visual.FocusScale, Is.GreaterThan(initialFocus));
            Assert.That(visual.EndpointMarkerVisible, Is.True);
            Object.Destroy(instance);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ArcLink_RevealsDeterministicSequentialHopsWithSagAndJitter()
        {
            var instance = Instantiate("cap_arclink_beam_2d", out var controller, out var visual);
            controller.Play();
            var hits = controller.Trace.Events.Where(value => value.Type == "on_hit" && value.Detail == "arc_link").ToArray();
            Assert.That(hits.Length, Is.EqualTo(4));
            for (var i = 1; i < hits.Length; i++) Assert.That(hits[i].Time, Is.GreaterThan(hits[i - 1].Time));
            controller.EvaluateVisualAtTime(.49f);
            Assert.That(visual.ArcVisibleHopCount, Is.EqualTo(0));
            controller.EvaluateVisualAtTime(.6f);
            Assert.That(visual.ArcVisibleHopCount, Is.EqualTo(1));
            controller.EvaluateVisualAtTime(.85f);
            Assert.That(visual.ArcVisibleHopCount, Is.EqualTo(2));
            controller.EvaluateVisualAtTime(1.3f);
            Assert.That(visual.ArcVisibleHopCount, Is.EqualTo(4));
            Assert.That(visual.ArcPointCount, Is.EqualTo(1 + 4 * BeamCapabilityVisualExecutor.ArcSamplesPerHop));
            var from = visual.GetPrimaryPoint(0);
            var to = visual.GetPrimaryPoint(BeamCapabilityVisualExecutor.ArcSamplesPerHop);
            var midpoint = visual.GetPrimaryPoint(2);
            Assert.That(DistanceToLine(midpoint, from, to), Is.GreaterThan(.01f), "arc carrier must visibly apply sag/jitter");
            var snapshot = Snapshot(visual);
            controller.EvaluateVisualAtTime(1.3f);
            Assert.That(Snapshot(visual), Is.EqualTo(snapshot));
            Object.Destroy(instance);
            yield return null;
        }

        [UnityTest]
        public IEnumerator EightBeamBlanks_ReplayDeterministicallyAndStopResetWithoutResidue()
        {
            foreach (var id in Ids)
            {
                var instance = Instantiate(id, out var controller, out var visual);
                controller.Play();
                controller.EvaluateVisualAtTime(Mathf.Min(controller.Duration * .7f, 1.3f));
                var canonical = controller.Trace.ToCanonicalJson();
                controller.Stop(VfxStopMode.Immediate);
                Assert.That(visual.AllVisualsHidden, Is.True, id);
                Assert.That(instance.GetComponentsInChildren<ParticleSystem>(true).All(value => value.particleCount == 0), Is.True, id);
                controller.Play();
                Assert.That(controller.Trace.ToCanonicalJson(), Is.EqualTo(canonical), id);
                controller.ResetForPool();
                Assert.That(visual.AllVisualsHidden, Is.True, id);
                Assert.That(instance.GetComponentsInChildren<Renderer>(true).All(value => !value.enabled), Is.True, id);
                Object.Destroy(instance);
            }
            yield return null;
        }

        private static GameObject Instantiate(string id, out CapabilityBlankVfxController controller, out BeamCapabilityVisualExecutor visual)
        {
            GameObject prefab = null;
#if UNITY_EDITOR
            prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/VFX/Generated/" + id + "/VFX_" + id + ".prefab");
#endif
            Assert.That(prefab, Is.Not.Null, id);
            var instance = Object.Instantiate(prefab);
            controller = instance.GetComponent<CapabilityBlankVfxController>();
            visual = instance.GetComponent<BeamCapabilityVisualExecutor>();
            Assert.That(controller, Is.Not.Null, id);
            Assert.That(visual, Is.Not.Null, id);
            return instance;
        }

        private static float DistanceToLine(Vector3 point, Vector3 start, Vector3 end)
        {
            var delta = end - start;
            if (delta.sqrMagnitude <= .000001f) return Vector3.Distance(point, start);
            var t = Mathf.Clamp01(Vector3.Dot(point - start, delta) / delta.sqrMagnitude);
            return Vector3.Distance(point, start + delta * t);
        }

        private static string Snapshot(BeamCapabilityVisualExecutor visual)
        {
            return string.Join("|", Enumerable.Range(0, visual.PrimaryPointCount).Select(index => visual.GetPrimaryPoint(index).ToString("F5")).ToArray());
        }
    }
}

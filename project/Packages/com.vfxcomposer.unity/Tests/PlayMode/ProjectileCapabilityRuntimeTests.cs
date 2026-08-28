using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VFXComposer.Capabilities;

namespace VFXComposer.Tests.PlayMode
{
    public sealed class ProjectileCapabilityRuntimeTests
    {
        [UnityTest]
        public IEnumerator TwelveCapabilityBlanks_PlayTheSameSampledContractAndResetCleanly()
        {
            var ids = new[]
            {
                "cap_linear_proj_3d", "cap_accel_proj_3d", "cap_parabola_proj_3d", "cap_homing_proj_3d",
                "cap_wave_proj_2d", "cap_boomerang_proj_3d", "cap_bounce_proj_3d", "cap_orbit_proj_3d",
                "cap_pierce_proj_3d", "cap_split_proj_2d", "cap_chainhop_proj_2d", "cap_volley_proj_2d"
            };
            foreach (var id in ids)
            {
                GameObject prefab = null;
#if UNITY_EDITOR
                prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/VFX/Generated/" + id + "/VFX_" + id + ".prefab");
#endif
                Assert.That(prefab, Is.Not.Null, id);
                var instance = Object.Instantiate(prefab);
                try
                {
                    var controller = instance.GetComponent<CapabilityBlankVfxController>();
                    Assert.That(controller, Is.Not.Null, id);
                    controller.Initialize(new VfxRuntimeContext(Vector3.zero, Quaternion.identity));
                    controller.Play();
                    Assert.That(controller.IsAlive, Is.True, id);
                    Assert.That(controller.Trace, Is.Not.Null, id);
                    var before = controller.Trace.ToCanonicalJson();
                    yield return null;
                    Assert.That(instance.GetComponentsInChildren<Renderer>(true).Any(renderer => renderer.enabled), Is.True, id);
                    controller.Stop(VfxStopMode.Immediate);
                    controller.Play();
                    Assert.That(controller.Trace.ToCanonicalJson(), Is.EqualTo(before), id + " replay determinism");
                    controller.ResetForPool();
                    Assert.That(controller.IsAlive, Is.False, id);
                    Assert.That(instance.GetComponentsInChildren<Renderer>(true), Has.All.Matches<Renderer>(renderer => !renderer.enabled), id);
                }
                finally { Object.Destroy(instance); }
            }
        }

        [UnityTest]
        public IEnumerator SplitChainAndVolley_ExecuteVisibleMachineReadableCarriersAndCleanUp()
        {
            var splitInstance = Object.Instantiate(Load("cap_split_proj_2d"));
            try
            {
                var controller = splitInstance.GetComponent<CapabilityBlankVfxController>();
                controller.Play();
                var splitEvents = controller.Trace.Events.Where(value => value.Type == "on_split").ToArray();
                controller.EvaluateVisualAtTime(splitEvents[0].Time + .08f);
                Assert.That(controller.VisibleCarrierCount, Is.EqualTo(5));
                Assert.That(splitInstance.GetComponentInChildren<ParticleSystem>(true).particleCount, Is.EqualTo(5));
                for (var i = 0; i < splitEvents.Length; i++)
                {
                    Assert.That(Vector3.Distance(controller.GetCarrierDirection(i), splitEvents[i].After), Is.LessThan(.0001f));
                    Assert.That(controller.GetCarrierScale(i), Is.EqualTo(.6f).Within(.0001f));
                }
                Assert.That(Vector3.Angle(controller.GetCarrierDirection(0), controller.GetCarrierDirection(4)), Is.EqualTo(80f).Within(.01f));
                controller.EvaluateVisualAtTime(controller.Duration - .05f);
                Assert.That(controller.VisibleCarrierCount, Is.EqualTo(5), "split children persist after the split event");
                for (var i = 0; i < splitEvents.Length; i++) Assert.That(Vector3.Distance(controller.GetCarrierDirection(i), splitEvents[i].After), Is.LessThan(.0001f));
                var canonical = CarrierSnapshot(controller);
                controller.Stop(VfxStopMode.Immediate);
                AssertCarrierCleanup(splitInstance, controller);
                controller.Play();
                controller.EvaluateVisualAtTime(controller.Duration - .05f);
                Assert.That(CarrierSnapshot(controller), Is.EqualTo(canonical), "same-seed split visual replay");
                controller.ResetForPool();
                AssertCarrierCleanup(splitInstance, controller);
            }
            finally { Object.Destroy(splitInstance); }

            var chainInstance = Object.Instantiate(Load("cap_chainhop_proj_2d"));
            try
            {
                var controller = chainInstance.GetComponent<CapabilityBlankVfxController>();
                controller.Play();
                var hops = controller.Trace.Events.Where(value => value.Type == "on_hit" && value.Detail == "chain_hop").ToArray();
                Assert.That(hops.Length, Is.EqualTo(4));
                var core = chainInstance.transform.Find("CapabilityCore");
                var marker = chainInstance.transform.Find("CapabilityEventMarker");
                var baseScale = core.localScale.x;
                var baseMarkerScale = marker.localScale.x;
                for (var i = 0; i < hops.Length; i++)
                {
                    controller.EvaluateVisualAtTime(hops[i].Time + .001f);
                    Assert.That(controller.ProcessedChainHopCount, Is.EqualTo(i + 1));
                    Assert.That(Vector3.Distance(controller.CoreVisualPosition, hops[i].Position), Is.LessThan(.08f));
                    Assert.That(core.localScale.x, Is.EqualTo(baseScale * hops[i].After.magnitude).Within(.001f));
                    Assert.That(Vector3.Distance(marker.localPosition, hops[i].Position), Is.LessThan(.0001f));
                    Assert.That(marker.localScale.x, Is.EqualTo(baseMarkerScale * hops[i].After.magnitude).Within(.001f));
                    Assert.That(marker.GetComponent<Renderer>().enabled, Is.True, "each hop has independent feedback");
                    if (i > 0) Assert.That(hops[i].Time, Is.GreaterThan(hops[i - 1].Time));
                }
                controller.Stop(VfxStopMode.Immediate);
                Assert.That(chainInstance.GetComponentsInChildren<Renderer>(true), Has.All.Matches<Renderer>(renderer => !renderer.enabled));
            }
            finally { Object.Destroy(chainInstance); }

            var volleyInstance = Object.Instantiate(Load("cap_volley_proj_2d"));
            try
            {
                var controller = volleyInstance.GetComponent<CapabilityBlankVfxController>();
                controller.Play();
                controller.EvaluateVisualAtTime(.1f);
                Assert.That(controller.ActiveShowcaseMode, Is.EqualTo("fan"));
                Assert.That(controller.VisibleCarrierCount, Is.EqualTo(5));
                Assert.That(Vector3.Angle(controller.GetCarrierDirection(0), controller.GetCarrierDirection(4)), Is.EqualTo(50f).Within(.01f));

                var burst = controller.Trace.Events.Where(value => value.Type == "on_emit" && value.Detail == "burst_stagger").ToArray();
                for (var i = 0; i < burst.Length; i++)
                {
                    controller.EvaluateVisualAtTime(burst[i].Time + .001f);
                    Assert.That(controller.ActiveShowcaseMode, Is.EqualTo("burst_stagger"));
                    Assert.That(controller.VisibleCarrierCount, Is.EqualTo(i + 1), "burst carrier count grows on each stagger time");
                    if (i > 0) Assert.That(burst[i].Time, Is.EqualTo(burst[i - 1].Time + .09f).Within(.0001f));
                }

                controller.EvaluateVisualAtTime(1.42f);
                Assert.That(controller.ActiveShowcaseMode, Is.EqualTo("ring"));
                Assert.That(controller.VisibleCarrierCount, Is.EqualTo(8));
                for (var i = 0; i < 8; i++) Assert.That(Vector3.Angle(controller.GetCarrierDirection(i), controller.GetCarrierDirection((i + 1) % 8)), Is.EqualTo(45f).Within(.01f));
                Assert.That(volleyInstance.GetComponentInChildren<ParticleSystem>(true).particleCount, Is.EqualTo(8));
                var canonical = CarrierSnapshot(controller);
                controller.Stop(VfxStopMode.Immediate);
                AssertCarrierCleanup(volleyInstance, controller);
                controller.Play();
                controller.EvaluateVisualAtTime(1.42f);
                Assert.That(CarrierSnapshot(controller), Is.EqualTo(canonical), "same-seed volley visual replay");
                controller.ResetForPool();
                AssertCarrierCleanup(volleyInstance, controller);
            }
            finally { Object.Destroy(volleyInstance); }
            yield return null;
        }

        private static GameObject Load(string id)
        {
            GameObject value = null;
#if UNITY_EDITOR
            value = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/VFX/Generated/" + id + "/VFX_" + id + ".prefab");
#endif
            Assert.That(value, Is.Not.Null, id);
            return value;
        }

        private static string CarrierSnapshot(CapabilityBlankVfxController controller)
        {
            var values = new string[controller.VisibleCarrierCount];
            for (var i = 0; i < values.Length; i++)
            {
                var p = controller.GetCarrierPosition(i); var d = controller.GetCarrierDirection(i);
                values[i] = p.x.ToString("R") + "," + p.y.ToString("R") + "," + d.x.ToString("R") + "," + d.y.ToString("R") + "," + controller.GetCarrierScale(i).ToString("R");
            }
            return controller.ActiveShowcaseMode + "|" + string.Join(";", values);
        }

        private static void AssertCarrierCleanup(GameObject instance, CapabilityBlankVfxController controller)
        {
            Assert.That(controller.VisibleCarrierCount, Is.EqualTo(0));
            Assert.That(instance.GetComponentInChildren<ParticleSystem>(true).particleCount, Is.EqualTo(0));
            Assert.That(instance.GetComponentsInChildren<Renderer>(true), Has.All.Matches<Renderer>(renderer => !renderer.enabled));
        }
    }
}

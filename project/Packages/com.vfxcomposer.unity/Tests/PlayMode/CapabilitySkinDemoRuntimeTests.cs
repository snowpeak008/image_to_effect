using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace VFXComposer.Tests.PlayMode
{
    public sealed class CapabilitySkinDemoRuntimeTests
    {
        [UnityTest]
        public IEnumerator ThreeSkinnedDemos_ExecuteTheirPlannedCombinedBehaviorTraces()
        {
            var fan=Create("cap_demo_fan_wave_cartoon_2d");fan.Play();Assert.That(fan.BehaviorTrace.Events.Count(v=>v.Type=="on_emit"&&v.Detail=="fan"),Is.EqualTo(5));Assert.That(fan.BehaviorTrace.Frames.Select(v=>v.Position.y).Distinct().Count(),Is.GreaterThan(3));
            var beam=Create("cap_demo_charge_occlude_holo_3d");beam.Play();Assert.That(beam.BehaviorTrace.Events.Any(v=>v.Detail=="occluded"),Is.True);Assert.That(beam.BehaviorTrace.Frames.Max(v=>v.Width),Is.GreaterThan(beam.BehaviorTrace.Frames.Min(v=>v.Width)));
            var nova=Create("cap_demo_telegraph_nova_holy_3d");nova.Play();Assert.That(nova.BehaviorTrace.Events.Count(v=>v.Type=="on_emit"&&v.Detail=="ring"),Is.EqualTo(12));Assert.That(nova.BehaviorTrace.Events.Any(v=>v.Detail=="telegraph_complete"),Is.True);Assert.That(nova.BehaviorTrace.Frames.Max(v=>v.Radius),Is.GreaterThan(nova.BehaviorTrace.Frames.Min(v=>v.Radius)));
            yield return null;Assert.That(fan.IsAlive&&beam.IsAlive&&nova.IsAlive,Is.True);Object.Destroy(fan.gameObject);Object.Destroy(beam.gameObject);Object.Destroy(nova.gameObject);
        }

        private static StyledVfxController Create(string id)
        {
            GameObject prefab=null;
#if UNITY_EDITOR
            prefab=UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/VFX/Generated/"+id+"/VFX_"+id+".prefab");
#endif
            Assert.That(prefab,Is.Not.Null,id);return Object.Instantiate(prefab).GetComponent<StyledVfxController>();
        }
    }
}

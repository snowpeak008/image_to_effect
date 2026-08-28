using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace VFXComposer.Tests.PlayMode
{
    public sealed class Area2DRuntimeTests
    {
        [UnityTest]
        public IEnumerator InfernoArea_StartRefreshTickStopAndPoolRemainStable()
        {
            GameObject prefab = null;
#if UNITY_EDITOR
            prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/VFX/Generated/inferno_vortex_area_2d/VFX_inferno_vortex_area_2d.prefab");
#endif
            Assert.That(prefab, Is.Not.Null); var instance = Object.Instantiate(prefab);
            try
            {
                var controller = instance.GetComponent<InfernoAreaVfxController>(); Assert.That(controller, Is.Not.Null); Assert.That(instance.GetComponentsInChildren<Renderer>(true).All(renderer => !renderer.enabled), Is.True); controller.Initialize(new VfxRuntimeContext(Vector3.zero, Quaternion.identity)); controller.Play(); yield return new WaitForSeconds(.72f); Assert.That(controller.IsAlive, Is.True); Assert.That(instance.GetComponentsInChildren<Renderer>(true).Count(renderer => renderer.enabled), Is.GreaterThanOrEqualTo(6)); Assert.That(instance.GetComponentsInChildren<ParticleSystem>(true).Sum(system => system.particleCount), Is.GreaterThan(0));
                var elapsed = controller.RuntimeElapsed; var rotation = instance.transform.Find("InnerSixArmVortex").localEulerAngles.z; Assert.That(controller.SendEvent("refresh", new VfxRuntimeEvent(Vector3.zero, Quaternion.identity)), Is.True); yield return new WaitForSeconds(.12f); Assert.That(controller.RuntimeElapsed, Is.GreaterThan(elapsed), "Refresh must preserve the active timeline instead of restarting it."); Assert.That(Mathf.Abs(Mathf.DeltaAngle(rotation, instance.transform.Find("InnerSixArmVortex").localEulerAngles.z)), Is.GreaterThan(.1f));
                controller.SendEvent("tick", new VfxRuntimeEvent()); Assert.That(controller.PulseCount, Is.GreaterThanOrEqualTo(1)); controller.Stop(VfxStopMode.AllowTail); yield return new WaitForSeconds(.42f); Assert.That(controller.IsAlive, Is.False); Assert.That(instance.GetComponentsInChildren<ParticleSystem>(true).All(system => system.particleCount == 0), Is.True); Assert.That(instance.GetComponentsInChildren<Renderer>(true).All(renderer => !renderer.enabled), Is.True);
                controller.Play(); yield return null; controller.Stop(VfxStopMode.Immediate); Assert.That(controller.IsAlive, Is.False); controller.ResetForPool(); Assert.That(instance.GetComponentsInChildren<ParticleSystem>(true).All(system => system.particleCount == 0), Is.True);
            }
            finally { Object.Destroy(instance); }
        }
    }
}

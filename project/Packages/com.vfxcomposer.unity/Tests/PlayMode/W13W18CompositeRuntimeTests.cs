using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace VFXComposer.Tests.PlayMode
{
    public sealed class W13W18CompositeRuntimeTests
    {
        [UnityTest] public IEnumerator Ultimate_TimelineCreatesReusableChildrenAndConsumesCameraHints()
        {
            var controller=Create("dragon_breath_ultimate_3d");controller.Play();yield return new WaitForSeconds(1.05f);Assert.That(controller.TriggeredEventCount,Is.GreaterThanOrEqualTo(2));Assert.That(controller.CreatedInstanceCount,Is.EqualTo(4));Assert.That(controller.ActiveChildCount,Is.GreaterThan(0));Assert.That(controller.CameraHintSerial,Is.GreaterThanOrEqualTo(2));var count=controller.CreatedInstanceCount;controller.ResetForPool();Assert.That(controller.ActiveChildCount,Is.EqualTo(0));controller.Play();yield return null;Assert.That(controller.CreatedInstanceCount,Is.EqualTo(count),"Replay must reuse its runtime pool.");Object.Destroy(controller.gameObject);
        }

        [UnityTest] public IEnumerator BossGate_PausesUntilExactExternalEvent()
        {
            var controller=Create("demon_gate_boss_3d");controller.Play();yield return new WaitForSeconds(1.35f);Assert.That(controller.WaitingForGate,Is.True);Assert.That(controller.WaitingGateId,Is.EqualTo("gate_formed"));var paused=controller.Elapsed;yield return new WaitForSeconds(.15f);Assert.That(controller.Elapsed,Is.EqualTo(paused).Within(.001));Assert.That(controller.ReleaseGate("wrong"),Is.False);Assert.That(controller.SendEvent("gate:gate_formed",new VfxRuntimeEvent()),Is.True);Assert.That(controller.WaitingForGate,Is.False);Object.Destroy(controller.gameObject);
        }

        [UnityTest] public IEnumerator BladeTempest_UsesEightRuntimeInstancesOfOneReferencedRecipe()
        {
            var controller=Create("blade_tempest_ultimate_3d");controller.Play();yield return new WaitForSeconds(2.85f);Assert.That(controller.TriggeredEventCount,Is.GreaterThanOrEqualTo(10));Assert.That(controller.CreatedInstanceCount,Is.EqualTo(11));Assert.That(controller.ActiveChildCount,Is.GreaterThan(0));controller.Stop(VfxStopMode.Immediate);Assert.That(controller.ActiveChildCount,Is.EqualTo(0));Object.Destroy(controller.gameObject);
        }

        [UnityTest] public IEnumerator HeroKit_ShowcaseHasDescriptorAndResetsWithoutActiveResidue()
        {
            var controller=Create("ice_moon_mage_kit_showcase_3d");Assert.That(controller.Descriptor,Is.Not.Null);Assert.That(controller.Descriptor.text,Does.Contain("ice_moon"));controller.Play();yield return new WaitForSeconds(1.5f);Assert.That(controller.TriggeredEventCount,Is.GreaterThanOrEqualTo(3));controller.ResetForPool();Assert.That(controller.IsAlive,Is.False);Assert.That(controller.ActiveChildCount,Is.EqualTo(0));Object.Destroy(controller.gameObject);
        }

        [UnityTest] public IEnumerator ReleaseInstances_RemovesRuntimePoolWithoutAccumulation()
        {
            var controller=Create("judgement_ray_ultimate_3d");controller.Play();yield return new WaitForSeconds(.1f);Assert.That(controller.CreatedInstanceCount,Is.GreaterThan(0));controller.ReleaseInstances();yield return null;Assert.That(controller.CreatedInstanceCount,Is.EqualTo(0));Assert.That(controller.transform.childCount,Is.EqualTo(0));Object.Destroy(controller.gameObject);
        }

        private static CompositeVfxController Create(string id){GameObject prefab=null;
#if UNITY_EDITOR
            prefab=UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/VFX/Generated/"+id+"/VFX_"+id+".prefab");
#endif
            Assert.That(prefab,Is.Not.Null,id);return Object.Instantiate(prefab).GetComponent<CompositeVfxController>();}
    }
}

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace VFXComposer.Tests.PlayMode
{
    public sealed class ValidationGalleryRuntimeTests
    {
        [UnityTest]
        public IEnumerator FiveNewArchetypes_PlayPulseStopAndPoolWithoutIdleLeak()
        {
            var ids=new[]{"guardian_aura_2d","arc_lightning_beam_2d","comet_motion_trail_2d","hex_guard_shield_2d","summoning_portal_2d"}; var instances=new List<GameObject>();
            try
            {
                for(var index=0;index<ids.Length;index++)
                {
                    var id=ids[index];
                    GameObject prefab=null;
#if UNITY_EDITOR
                    prefab=UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/VFX/Generated/"+id+"/VFX_"+id+".prefab");
#endif
                    Assert.That(prefab,Is.Not.Null,id); var instance=Object.Instantiate(prefab); instances.Add(instance); var controller=instance.GetComponent<ValidationArchetypeVfxController>(); Assert.That(controller,Is.Not.Null); Assert.That(controller.Sustained,Is.EqualTo(index<4),id+" lifecycle"); Assert.That(instance.GetComponentsInChildren<Renderer>(true).All(r=>!r.enabled),Is.True); controller.Play();
                }
                yield return new WaitForSeconds(.3f);
                foreach(var instance in instances)
                {
                    var controller=instance.GetComponent<ValidationArchetypeVfxController>(); Assert.That(controller.IsAlive,Is.True); Assert.That(instance.GetComponentsInChildren<Renderer>(true).Count(r=>r.enabled),Is.EqualTo(6)); Assert.That(instance.GetComponentInChildren<ParticleSystem>(true).isPlaying,Is.True); Assert.That(controller.SendEvent("hit",new VfxRuntimeEvent(instance.transform.position,instance.transform.rotation)),Is.True); controller.Stop(VfxStopMode.Immediate); Assert.That(controller.IsAlive,Is.False); Assert.That(instance.GetComponentsInChildren<Renderer>(true).All(r=>!r.enabled),Is.True); Assert.That(instance.GetComponentInChildren<ParticleSystem>(true).particleCount,Is.EqualTo(0));
                }
            }
            finally { foreach(var instance in instances)if(instance!=null)Object.Destroy(instance); }
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace VFXComposer.Tests.PlayMode
{
    public sealed class CoverageGalleryBRuntimeTests
    {
        [UnityTest]
        public IEnumerator NineEntries_PlayEventStopAndPoolWithoutLeak()
        {
            var ids=new[]{"meteor_impact_3d","astral_aura_3d","toxic_field_3d","plasma_link_3d","spectral_trail_3d","prismatic_shield_3d","rift_spawn_3d","snow_weather_volume","damage_warning_ui"};var instances=new List<GameObject>();
            try
            {
                foreach(var id in ids){GameObject prefab=null;
#if UNITY_EDITOR
                    prefab=UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/VFX/Generated/"+id+"/VFX_"+id+".prefab");
#endif
                    Assert.That(prefab,Is.Not.Null,id);var instance=Object.Instantiate(prefab);instances.Add(instance);var entry=instance.GetComponent<CoverageGalleryVfxController>();Assert.That(entry,Is.Not.Null);entry.Play();}
                yield return new WaitForSeconds(.35f);Assert.That(instances.All(instance=>instance.GetComponent<CoverageGalleryVfxController>().IsAlive),Is.True);foreach(var instance in instances){var entry=instance.GetComponent<CoverageGalleryVfxController>();Assert.That(entry.SendEvent("hit",new VfxRuntimeEvent(instance.transform.position,instance.transform.rotation)),Is.True);entry.Stop(VfxStopMode.Immediate);Assert.That(entry.IsAlive,Is.False);Assert.That(instance.GetComponentsInChildren<Renderer>(true).All(value=>!value.enabled),Is.True);Assert.That(instance.GetComponentsInChildren<ParticleSystem>(true).All(value=>value.particleCount==0),Is.True);}
            }
            finally{foreach(var instance in instances)if(instance!=null)Object.Destroy(instance);}
        }
    }
}

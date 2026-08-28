using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace VFXComposer.Tests.PlayMode
{
    public sealed class InteractionGalleryRuntimeTests
    {
        [UnityTest]
        public IEnumerator NineEntries_PlayEventsStopAndPoolWithoutLeak()
        {
            var ids=new[]{"focus_charge_3d","channel_tether_3d","warning_telegraph_3d","chain_arc_3d","seeker_orb_3d","weapon_enchant_3d","phase_dash_3d","dissolve_transform_3d","ultimate_sequence_3d"};var instances=new List<GameObject>();try{foreach(var id in ids){GameObject prefab=null;
#if UNITY_EDITOR
                prefab=UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/VFX/Generated/"+id+"/VFX_"+id+".prefab");
#endif
                Assert.That(prefab,Is.Not.Null,id);var instance=Object.Instantiate(prefab);instances.Add(instance);instance.GetComponent<InteractionGalleryVfxController>().Play();}yield return new WaitForSeconds(.3f);Assert.That(instances.All(instance=>instance.GetComponent<InteractionGalleryVfxController>().IsAlive),Is.True);foreach(var instance in instances){var entry=instance.GetComponent<InteractionGalleryVfxController>();Assert.That(entry.SendEvent("retarget",new VfxRuntimeEvent(instance.transform.position,instance.transform.rotation)),Is.True);entry.Stop(VfxStopMode.Immediate);Assert.That(entry.IsAlive,Is.False);Assert.That(instance.GetComponentsInChildren<Renderer>(true).All(value=>!value.enabled),Is.True);}}
            finally{foreach(var instance in instances)if(instance!=null)Object.Destroy(instance);}
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace VFXComposer
{
    /// <summary>Preview-only state/event scheduler for the interaction gallery.</summary>
    [DisallowMultipleComponent]
    public sealed class InteractionGalleryPlaybackDriver : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour[] runtimeEntries=new MonoBehaviour[0];
        [SerializeField,Min(4f)] private float cycleDuration=5.2f;
        private readonly List<InteractionGalleryVfxController> entries=new List<InteractionGalleryVfxController>();
        private float elapsed;private bool started,released,retargeted,hit;

        private void Start(){entries.Clear();foreach(var value in runtimeEntries){var entry=value as InteractionGalleryVfxController;if(entry!=null){entries.Add(entry);entry.gameObject.SetActive(true);entry.ResetForPool();}}BeginCycle();}
        private void Update(){elapsed+=Time.deltaTime;if(!started&&elapsed>=.08f){started=true;foreach(var entry in entries)entry.Play();}if(!released&&elapsed>=1.2f){released=true;Dispatch("release");}if(!retargeted&&elapsed>=2f){retargeted=true;Dispatch("retarget");}if(!hit&&elapsed>=2.8f){hit=true;Dispatch("hit");}if(elapsed>=cycleDuration)BeginCycle();}
        private void Dispatch(string id){foreach(var entry in entries)if(entry.IsAlive)entry.SendEvent(id,new VfxRuntimeEvent(entry.transform.position,entry.transform.rotation));}
        private void BeginCycle(){foreach(var entry in entries)entry.ResetForPool();elapsed=0;started=released=retargeted=hit=false;}
        private void OnDisable(){foreach(var entry in entries)if(entry!=null)entry.ResetForPool();}
    }
}

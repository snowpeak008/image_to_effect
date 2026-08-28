using System.Collections.Generic;
using UnityEngine;

namespace VFXComposer
{
    /// <summary>Preview-only scheduler: seven sustained entries stay alive while two one-shots replay.</summary>
    [DisallowMultipleComponent]
    public sealed class CoverageGalleryBPlaybackDriver : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour[] runtimeEntries = new MonoBehaviour[0];
        [SerializeField, Min(2f)] private float cycleDuration = 3.6f;
        private readonly List<CoverageGalleryVfxController> entries = new List<CoverageGalleryVfxController>();
        private float elapsed;
        private bool started, pulseSent, impactReplaySent;

        private void Start()
        {
            entries.Clear();foreach(var behaviour in runtimeEntries){var entry=behaviour as CoverageGalleryVfxController;if(entry!=null){entries.Add(entry);entry.gameObject.SetActive(true);entry.ResetForPool();}}BeginCycle(true);
        }

        private void Update()
        {
            elapsed+=Time.deltaTime;
            if(!started&&elapsed>=.08f){started=true;foreach(var entry in entries)if(!entry.IsAlive)entry.Play();}
            if(!pulseSent&&elapsed>=1.05f){pulseSent=true;foreach(var entry in entries)if(entry.Sustained)entry.SendEvent("hit",new VfxRuntimeEvent(entry.transform.position,entry.transform.rotation));}
            if(!impactReplaySent&&elapsed>=1.85f){impactReplaySent=true;foreach(var entry in entries)if(entry.Profile==CoverageGalleryProfile.Impact3D){entry.ResetForPool();entry.Play();}}
            if(elapsed>=cycleDuration)BeginCycle(false);
        }

        private void BeginCycle(bool first)
        {
            foreach(var entry in entries)if(first||!entry.Sustained)entry.ResetForPool();elapsed=0f;started=false;pulseSent=false;impactReplaySent=false;
        }

        private void OnDisable(){foreach(var entry in entries)if(entry!=null)entry.ResetForPool();}
    }
}

using UnityEngine;

namespace VFXComposer
{
    /// <summary>Preview-only replay harness; forbidden in formal Runtime Entries.</summary>
    public sealed class PlannedContentPreviewDriver : MonoBehaviour
    {
        [SerializeField] private PlannedContentVfxController[] entries=new PlannedContentVfxController[0];
        [SerializeField] private float replayInterval=2.2f;
        private float elapsed;
        private void Start(){Replay();}
        private void Update(){elapsed+=Time.deltaTime;if(elapsed>=replayInterval){elapsed=0;Replay();}}
        private void Replay(){foreach(var entry in entries)if(entry!=null){entry.ResetForPool();entry.Play();}}
    }
}

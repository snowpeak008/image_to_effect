using UnityEngine;

namespace VFXComposer
{
    /// <summary>Preview-scene-only replay helper. Production output auditing forbids it inside Runtime Prefabs.</summary>
    public sealed class ImpactPreviewPlaybackDriver : MonoBehaviour
    {
        [SerializeField] private TimedImpactVfxController controller;
        [SerializeField, Min(.1f)] private float interval = .85f;
        private float nextPlay;

        private void OnEnable() { nextPlay = 0f; }
        private void Update()
        {
            if (controller == null || Time.time < nextPlay) return;
            controller.Play();
            nextPlay = Time.time + interval;
        }
    }
}

using UnityEngine;

namespace VFXComposer
{
    /// <summary>Preview-scene-only lifecycle demonstration; production auditing forbids this component in Runtime Prefabs.</summary>
    public sealed class AreaPreviewPlaybackDriver : MonoBehaviour
    {
        [SerializeField] private InfernoAreaVfxController controller;
        [SerializeField, Min(1f)] private float activeSeconds = 4f;
        [SerializeField, Min(.2f)] private float restartDelay = .8f;
        private float nextAction;
        private bool stopping;

        private void OnEnable()
        {
            stopping = false;
            nextAction = Time.time;
        }

        private void Update()
        {
            if (controller == null || Time.time < nextAction) return;
            if (!stopping)
            {
                controller.Play();
                stopping = true;
                nextAction = Time.time + activeSeconds;
            }
            else
            {
                controller.Stop(VfxStopMode.AllowTail);
                stopping = false;
                nextAction = Time.time + restartDelay;
            }
        }
    }
}

using UnityEngine;

namespace VFXComposer
{
    /// <summary>Preview-only behaviour which exercises the real lifecycle without sampling or phase toggles.</summary>
    [DisallowMultipleComponent]
    public sealed class SustainedEffectPreviewDriver : MonoBehaviour
    {
        [SerializeField] private SustainedEffectController controller;
        [SerializeField, Min(.5f)] private float steadySeconds = 3.25f;
        [SerializeField, Min(.1f)] private float idleSeconds = .75f;
        [SerializeField] private bool loop = true;

        private float idleElapsed;
        private bool stopSent;

        private void OnEnable()
        {
            idleElapsed = 0f;
            stopSent = false;
            if (controller != null) controller.Play();
        }

        private void Update()
        {
            if (controller == null) return;
            if (!stopSent && controller.State == SustainedEffectState.Steady && controller.StateElapsed >= steadySeconds)
            {
                controller.Stop(VfxStopMode.AllowTail);
                stopSent = true;
            }
            if (!loop || !stopSent) return;
            if (controller.IsAlive)
            {
                idleElapsed = 0f;
                return;
            }
            idleElapsed += Mathf.Max(0f, Time.deltaTime);
            if (idleElapsed < idleSeconds) return;
            idleElapsed = 0f;
            stopSent = false;
            controller.Play();
        }
    }
}

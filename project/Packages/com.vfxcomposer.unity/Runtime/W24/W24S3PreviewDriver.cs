using UnityEngine;
using VFXComposer;

namespace VFXComposer.W24
{
    /// <summary>Scene-only natural-playback driver for the three S3 formal Preview scenes. Never add to a Runtime Prefab.</summary>
    [DisallowMultipleComponent]
    public sealed class W24S3PreviewDriver : MonoBehaviour
    {
        [SerializeField] private W24S3RuntimeEntry entry;
        [SerializeField] private Transform modelRoot;
        [SerializeField] private int mode; // 0 projectile, 1 model binding, 2 real light
        [SerializeField, Min(.1f)] private float loopSeconds = 2.4f;
        private float elapsed;
        private bool impactSent;
        private bool fragmentSent;
        private bool stopSent;
        private uint captureSeed;
        private bool captureSeedConfigured;
        private bool formalCaptureRun;

        private void OnEnable() { Begin(); }
        private void Update()
        {
            if (entry == null) return;
            elapsed += Mathf.Max(0f, Time.deltaTime);
            if (mode == 0) UpdateProjectile();
            else if (mode == 1) UpdateBinding();
            else UpdateLighting();
            if (!formalCaptureRun && elapsed >= loopSeconds) Begin();
        }

        /// <summary>
        /// Formal capture reset. It invokes the same Begin/Update event timeline as the normal
        /// serialized Preview Driver; only the frozen seed differs. The call is rejected while
        /// the Runtime Entry is live so a capture cannot rewrite a running lifecycle.
        /// </summary>
        public void RestartForFormalCapture(uint seed)
        {
            if (entry == null) throw new System.InvalidOperationException("W24 S3 Preview Driver requires its serialized Runtime Entry.");
            if (entry.IsAlive) entry.ResetForPool();
            captureSeed = seed; captureSeedConfigured = true; formalCaptureRun = true;
            Begin();
        }

        private void Begin()
        {
            elapsed = 0f; impactSent = false; fragmentSent = false; stopSent = false;
            if (entry == null) return;
            if (mode == 1) entry.ConfigureModelRoot(modelRoot);
            var start = mode == 0 ? new Vector3(-2f, .3f, 0f) : Vector3.zero;
            if (captureSeedConfigured) entry.SetCaptureSeed(captureSeed);
            entry.Initialize(new VfxRuntimeContext(start, Quaternion.identity));
            entry.Play();
        }
        private void UpdateProjectile()
        {
            var progress = Mathf.Clamp01(elapsed / 1.2f);
            var position = Vector3.Lerp(new Vector3(-2f, .3f, 0f), new Vector3(2f, .3f, 0f), progress);
            entry.SendEvent("travel", new VfxRuntimeEvent(position, Quaternion.identity));
            if (!impactSent && progress >= 1f) { impactSent = true; entry.SendEvent("impact", new VfxRuntimeEvent(position, Quaternion.identity)); }
            if (impactSent && !stopSent && elapsed >= 1.45f) { stopSent = true; entry.Stop(VfxStopMode.AllowTail); }
        }
        private void UpdateBinding()
        {
            if (modelRoot != null) modelRoot.localRotation = Quaternion.Euler(0f, Mathf.Sin(elapsed * 2f) * 35f, 0f);
            if (!fragmentSent && elapsed >= .8f) { fragmentSent = true; entry.SendEvent("fragment", new VfxRuntimeEvent(entry.transform.position, entry.transform.rotation)); }
            if (!stopSent && elapsed >= 1.65f) { stopSent = true; entry.Stop(VfxStopMode.AllowTail); }
        }
        private void UpdateLighting()
        {
            if (!stopSent && elapsed >= 1.7f) { stopSent = true; entry.Stop(VfxStopMode.AllowTail); }
        }
    }
}

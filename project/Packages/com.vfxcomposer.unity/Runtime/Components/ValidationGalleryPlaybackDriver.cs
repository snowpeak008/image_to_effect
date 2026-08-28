using System.Collections.Generic;
using UnityEngine;

namespace VFXComposer
{
    /// <summary>Preview-scene-only scheduler for the 3x3 validation wall.</summary>
    [DisallowMultipleComponent]
    public sealed class ValidationGalleryPlaybackDriver : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour[] runtimeEntries = new MonoBehaviour[0];
        [SerializeField, Min(2f)] private float cycleDuration = 4.2f;
        private readonly List<IVfxRuntimeEntry> entries = new List<IVfxRuntimeEntry>();
        private GeneratedVfxController projectile;
        private float elapsed;
        private bool launchSent, travelSent, impactSent, replayOneShotsA, replayOneShotsB, pulsesSent, stopSent;
        private bool cycleStarted;
        private Vector3 projectileOrigin;

        private void Start()
        {
            foreach (var behaviour in runtimeEntries) if (behaviour != null) behaviour.gameObject.SetActive(true);
            Resolve(); BeginCycle();
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            if (!launchSent && elapsed >= .08f) { launchSent = true; foreach (var entry in entries) if (!entry.IsAlive) entry.Play(); }
            if (projectile != null && launchSent && !travelSent && elapsed >= .15f) { travelSent = true; projectile.SendEvent("travel", new VfxRuntimeEvent(projectileOrigin + Vector3.left * .52f, Quaternion.identity)); }
            if (projectile != null && travelSent && !impactSent && elapsed < .78f)
            {
                var t = Mathf.InverseLerp(.15f, .78f, elapsed); projectile.SetTravelTransform(Vector3.Lerp(projectileOrigin + Vector3.left * .52f, projectileOrigin + Vector3.right * .52f, t), Quaternion.identity);
            }
            if (projectile != null && !impactSent && elapsed >= .78f) { impactSent = true; projectile.SendEvent("impact", new VfxRuntimeEvent(projectileOrigin + Vector3.right * .52f, Quaternion.identity)); }
            if (!pulsesSent && elapsed >= 1.08f) { pulsesSent = true; Broadcast("tick"); Broadcast("hit"); }
            if (!replayOneShotsA && elapsed >= 1.35f) { replayOneShotsA = true; ReplayOneShots(); }
            if (!replayOneShotsB && elapsed >= 2.12f) { replayOneShotsB = true; ReplayOneShots(); }
            if (!stopSent && elapsed >= 3.12f) { stopSent = true; foreach (var entry in entries) if (!IsSustainedPreviewEntry(entry)) entry.Stop(VfxStopMode.AllowTail); }
            if (elapsed >= cycleDuration) BeginCycle();
        }

        private void Resolve()
        {
            entries.Clear(); projectile = null;
            foreach (var behaviour in runtimeEntries)
            {
                var entry = behaviour as IVfxRuntimeEntry; if (entry == null) continue;
                entries.Add(entry); if (projectile == null) projectile = behaviour as GeneratedVfxController;
            }
            if (projectile != null) projectileOrigin = projectile.transform.position;
        }

        private void BeginCycle()
        {
            foreach (var entry in entries) if (!cycleStarted || !IsSustainedPreviewEntry(entry)) entry.ResetForPool();
            if (projectile != null) projectile.transform.position = projectileOrigin;
            elapsed = 0f; launchSent = travelSent = impactSent = replayOneShotsA = replayOneShotsB = pulsesSent = stopSent = false;
            cycleStarted = true;
        }

        private static bool IsSustainedPreviewEntry(IVfxRuntimeEntry entry)
        {
            var galleryEntry = entry as ValidationArchetypeVfxController;
            if (galleryEntry != null) return galleryEntry.Sustained;
            return entry is InfernoAreaVfxController;
        }

        private void Broadcast(string eventId) { foreach (var entry in entries) entry.SendEvent(eventId, new VfxRuntimeEvent(((MonoBehaviour)entry).transform.position, ((MonoBehaviour)entry).transform.rotation)); }
        private void ReplayOneShots() { PlayNamed<TimedImpactVfxController>(); PlayNamed<SlashEffectController>(); }
        private void PlayNamed<T>() where T : MonoBehaviour, IVfxRuntimeEntry { foreach (var entry in entries) { var typed = entry as T; if (typed != null) typed.Play(); } }
    }
}

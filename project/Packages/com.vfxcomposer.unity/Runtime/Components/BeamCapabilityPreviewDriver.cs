using UnityEngine;

namespace VFXComposer
{
    /// <summary>Preview-only external inputs for the W-C2 wall; carrier semantics stay in Runtime Entries.</summary>
    [DisallowMultipleComponent]
    public sealed class BeamCapabilityPreviewDriver : MonoBehaviour
    {
        [SerializeField] private CapabilityBlankVfxController[] runtimeEntries = new CapabilityBlankVfxController[0];
        [SerializeField] private CapabilityBlankVfxController sustainedEntry;
        [SerializeField] private CapabilityBlankVfxController chargeEntry;
        [SerializeField] private CapabilityBlankVfxController occludeEntry;
        [SerializeField] private Transform sustainedSource;
        [SerializeField] private Transform sustainedTarget;
        [SerializeField] private Transform occludeSource;
        [SerializeField] private Transform occludeTarget;
        [SerializeField] private Transform movableBlocker;
        [SerializeField] private BeamCapabilityObstacleProbe obstacleProbe;
        [SerializeField, Min(4.6f)] private float cycleDuration = 5.2f;

        private float elapsed;
        private bool launchSent;
        private bool cancelSent;
        private bool chargeReplaySent;
        private bool stopSent;
        private Vector3 sustainedSourceBase;
        private Vector3 sustainedTargetBase;
        private Vector3 blockerBase;

        public float CycleElapsed { get { return elapsed; } }
        public bool ChargeCancelSent { get { return cancelSent; } }
        public bool BlockerMoved { get { return movableBlocker != null && (movableBlocker.localPosition - blockerBase).sqrMagnitude > .0001f; } }

        private void Start()
        {
            if (sustainedSource != null) sustainedSourceBase = sustainedSource.localPosition;
            if (sustainedTarget != null) sustainedTargetBase = sustainedTarget.localPosition;
            if (movableBlocker != null) blockerBase = movableBlocker.localPosition;
            for (var i = 0; i < runtimeEntries.Length; i++)
                if (runtimeEntries[i] != null) runtimeEntries[i].gameObject.SetActive(true);
            BindRuntimeInputs();
            BeginCycle();
        }

        private void Update()
        {
            elapsed += Mathf.Max(0f, Time.deltaTime);
            DriveAnchors();
            if (!launchSent && elapsed >= .08f)
            {
                launchSent = true;
                for (var i = 0; i < runtimeEntries.Length; i++)
                    if (runtimeEntries[i] != null) runtimeEntries[i].Play();
            }
            if (!cancelSent && elapsed >= 1.36f && chargeEntry != null)
            {
                cancelSent = true;
                chargeEntry.Cancel();
            }
            if (!chargeReplaySent && elapsed >= 1.72f && chargeEntry != null)
            {
                chargeReplaySent = true;
                chargeEntry.Play();
            }
            if (!stopSent && elapsed >= 4.45f)
            {
                stopSent = true;
                for (var i = 0; i < runtimeEntries.Length; i++)
                    if (runtimeEntries[i] != null) runtimeEntries[i].Stop(VfxStopMode.Immediate);
            }
            if (elapsed >= cycleDuration) BeginCycle();
        }

        private void BeginCycle()
        {
            for (var i = 0; i < runtimeEntries.Length; i++)
                if (runtimeEntries[i] != null) runtimeEntries[i].ResetForPool();
            elapsed = 0f;
            launchSent = false;
            cancelSent = false;
            chargeReplaySent = false;
            stopSent = false;
            if (movableBlocker != null) movableBlocker.localPosition = blockerBase;
            BindRuntimeInputs();
        }

        private void BindRuntimeInputs()
        {
            if (sustainedEntry != null && sustainedEntry.BeamVisual != null)
                sustainedEntry.BeamVisual.BindEndpoints(sustainedSource, sustainedTarget);
            if (occludeEntry != null && occludeEntry.BeamVisual != null)
            {
                occludeEntry.BeamVisual.BindEndpoints(occludeSource, occludeTarget);
                occludeEntry.BeamVisual.SetObstacleProbe(obstacleProbe);
            }
        }

        private void DriveAnchors()
        {
            if (sustainedSource != null)
                sustainedSource.localPosition = sustainedSourceBase + new Vector3(0f, Mathf.Sin(elapsed * 2.2f) * .12f, 0f);
            if (sustainedTarget != null)
                sustainedTarget.localPosition = sustainedTargetBase + new Vector3(Mathf.Sin(elapsed * 1.35f) * .18f, Mathf.Cos(elapsed * 1.8f) * .28f, 0f);
            if (movableBlocker != null)
                movableBlocker.localPosition = elapsed >= 1.28f && elapsed < 3.55f ? blockerBase + Vector3.up * .72f : blockerBase;
        }
    }
}

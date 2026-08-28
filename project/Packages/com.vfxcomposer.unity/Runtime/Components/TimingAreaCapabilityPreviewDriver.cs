using UnityEngine;

namespace VFXComposer
{
    /// <summary>
    /// Preview-only external inputs for the bounded W-C3 wall. All timing/area rendering
    /// remains inside each Runtime Entry; this driver only supplies a local path and lifecycle inputs.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TimingAreaCapabilityPreviewDriver : MonoBehaviour
    {
        private const float LaunchGeneralAt = .08f;
        private const float ChargeCompleteAt = 1.34f;
        private const float ChargeReplayAt = 1.72f;
        private const float ChargeCancelAt = 2.4f;
        private const float ChannelLaunchAt = 2.78f;
        private const float ChannelCompleteAt = 4.74f;
        private const float ChannelReplayAt = 5.12f;
        private const float ChannelCancelAt = 5.96f;
        private const float MovingLaunchAt = 6.34f;
        private const float MovingCompleteAt = 8.2f;
        private const float MovingReplayAt = 8.58f;
        private const float MovingCancelAt = 9.42f;

        [SerializeField] private CapabilityBlankVfxController[] runtimeEntries = new CapabilityBlankVfxController[0];
        [SerializeField] private CapabilityBlankVfxController chargeEntry;
        [SerializeField] private CapabilityBlankVfxController channelEntry;
        [SerializeField] private CapabilityBlankVfxController movingZoneEntry;
        [SerializeField, Min(9.8f)] private float cycleDuration = 10f;

        private readonly Vector3[] movingZoneLocalPath =
        {
            new Vector3(-3.4f, -.35f, 0f),
            new Vector3(-1.15f, .58f, 0f),
            new Vector3(1.05f, -.48f, 0f),
            new Vector3(3.45f, .32f, 0f)
        };

        private float elapsed;
        private bool generalLaunched;
        private bool chargeCompleted;
        private bool chargeReplayed;
        private bool chargeCancelled;
        private bool channelLaunched;
        private bool channelCompleted;
        private bool channelReplayed;
        private bool channelCancelled;
        private bool movingLaunched;
        private bool movingCompleted;
        private bool movingReplayed;
        private bool movingCancelled;

        public float CycleElapsed { get { return elapsed; } }
        public bool ExternalPathBound { get { return movingZoneEntry != null && movingZoneEntry.TimingAreaVisual != null && movingZoneEntry.TimingAreaVisual.UsesExternalPath; } }
        public int CompleteDemoCount { get; private set; }
        public int CancelDemoCount { get; private set; }

        private void Start()
        {
            for (var i = 0; i < runtimeEntries.Length; i++)
                if (runtimeEntries[i] != null) runtimeEntries[i].gameObject.SetActive(true);
            BeginCycle();
        }

        private void Update()
        {
            elapsed += Mathf.Max(0f, Time.deltaTime);
            if (!generalLaunched && elapsed >= LaunchGeneralAt)
            {
                generalLaunched = true;
                for (var i = 0; i < runtimeEntries.Length; i++)
                {
                    var entry = runtimeEntries[i];
                    if (entry != null && entry != chargeEntry && entry != channelEntry && entry != movingZoneEntry) entry.Play();
                }
                if (chargeEntry != null) chargeEntry.Play();
            }
            if (!chargeCompleted && elapsed >= ChargeCompleteAt) { chargeCompleted = true; Complete(chargeEntry); }
            if (!chargeReplayed && elapsed >= ChargeReplayAt) { chargeReplayed = true; Replay(chargeEntry); }
            if (!chargeCancelled && elapsed >= ChargeCancelAt) { chargeCancelled = true; Cancel(chargeEntry); }
            if (!channelLaunched && elapsed >= ChannelLaunchAt) { channelLaunched = true; Replay(channelEntry); }
            if (!channelCompleted && elapsed >= ChannelCompleteAt) { channelCompleted = true; Complete(channelEntry); }
            if (!channelReplayed && elapsed >= ChannelReplayAt) { channelReplayed = true; Replay(channelEntry); }
            if (!channelCancelled && elapsed >= ChannelCancelAt) { channelCancelled = true; Cancel(channelEntry); }
            if (!movingLaunched && elapsed >= MovingLaunchAt) { movingLaunched = true; ReplayMovingZone(); }
            if (!movingCompleted && elapsed >= MovingCompleteAt) { movingCompleted = true; Complete(movingZoneEntry); }
            if (!movingReplayed && elapsed >= MovingReplayAt) { movingReplayed = true; ReplayMovingZone(); }
            if (!movingCancelled && elapsed >= MovingCancelAt) { movingCancelled = true; Cancel(movingZoneEntry); }
            if (elapsed >= cycleDuration) BeginCycle();
        }

        private void BeginCycle()
        {
            for (var i = 0; i < runtimeEntries.Length; i++)
                if (runtimeEntries[i] != null) runtimeEntries[i].ResetForPool();
            BindExternalPath();
            elapsed = 0f;
            generalLaunched = false;
            chargeCompleted = false;
            chargeReplayed = false;
            chargeCancelled = false;
            channelLaunched = false;
            channelCompleted = false;
            channelReplayed = false;
            channelCancelled = false;
            movingLaunched = false;
            movingCompleted = false;
            movingReplayed = false;
            movingCancelled = false;
            CompleteDemoCount = 0;
            CancelDemoCount = 0;
        }

        private void BindExternalPath()
        {
            if (movingZoneEntry != null && movingZoneEntry.TimingAreaVisual != null)
                movingZoneEntry.TimingAreaVisual.SetExternalPath(movingZoneLocalPath);
        }

        private void ReplayMovingZone()
        {
            if (movingZoneEntry == null) return;
            movingZoneEntry.ResetForPool();
            BindExternalPath();
            movingZoneEntry.Play();
        }

        private static void Replay(CapabilityBlankVfxController entry)
        {
            if (entry == null) return;
            entry.ResetForPool();
            entry.Play();
        }

        private void Complete(CapabilityBlankVfxController entry)
        {
            if (entry == null) return;
            entry.Complete();
            CompleteDemoCount++;
        }

        private void Cancel(CapabilityBlankVfxController entry)
        {
            if (entry == null) return;
            entry.Cancel();
            CancelDemoCount++;
        }
    }
}

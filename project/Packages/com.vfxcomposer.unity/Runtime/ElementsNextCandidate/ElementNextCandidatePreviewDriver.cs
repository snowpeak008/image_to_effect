using UnityEngine;

namespace VFXComposer
{
    /// <summary>Preview-only deterministic replay.  It never participates in a Runtime Entry.</summary>
    [DisallowMultipleComponent]
    public sealed class ElementNextCandidatePreviewDriver : MonoBehaviour
    {
        [SerializeField] private ElementNextCandidateVisualExecutor[] entries = new ElementNextCandidateVisualExecutor[0];
        [SerializeField, Min(.5f)] private float replayInterval = 4.2f;
        [SerializeField, Min(.1f)] private float sustainedStopTime = 3.25f;
        [SerializeField] private bool triggerEventDriven;
        [SerializeField, Min(.05f)] private float eventTriggerTime = 1.15f;

        private float elapsed;
        private bool sustainedStopped;
        private bool eventsTriggered;

        public int EntryCount { get { return entries == null ? 0 : entries.Length; } }
        public float ReplayInterval { get { return replayInterval; } }
        public bool SustainedStopped { get { return sustainedStopped; } }
        public bool TriggerEventDriven { get { return triggerEventDriven; } }
        public bool EventsTriggered { get { return eventsTriggered; } }

        private void OnEnable() { ReplayNow(); }

        private void Update()
        {
            elapsed += Mathf.Max(0f, Time.deltaTime);
            if (triggerEventDriven && !eventsTriggered && elapsed >= eventTriggerTime)
            {
                eventsTriggered = true;
                ForEach(delegate(ElementNextCandidateVisualExecutor entry)
                {
                    if (entry.Lifecycle == StyledVfxLifecycle.EventDriven) entry.TriggerLocalEvent(new Vector3(.32f, .08f, 0f));
                });
            }
            if (!sustainedStopped && elapsed >= sustainedStopTime)
            {
                sustainedStopped = true;
                ForEach(delegate(ElementNextCandidateVisualExecutor entry)
                {
                    if (entry.Lifecycle != StyledVfxLifecycle.OneShot) entry.Stop(VfxStopMode.AllowTail);
                });
            }
            if (elapsed >= replayInterval) ReplayNow();
        }

        private void OnDisable()
        {
            ForEach(delegate(ElementNextCandidateVisualExecutor entry) { entry.ResetForPool(); });
            elapsed = 0f;
            sustainedStopped = false;
            eventsTriggered = false;
        }

        public void ReplayNow()
        {
            elapsed = 0f;
            sustainedStopped = false;
            eventsTriggered = false;
            ForEach(delegate(ElementNextCandidateVisualExecutor entry)
            {
                entry.ResetForPool();
                entry.Play();
            });
        }

        private void ForEach(System.Action<ElementNextCandidateVisualExecutor> action)
        {
            if (entries == null) return;
            for (var index = 0; index < entries.Length; index++)
                if (entries[index] != null) action(entries[index]);
        }
    }
}

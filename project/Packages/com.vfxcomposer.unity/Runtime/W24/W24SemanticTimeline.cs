using System;
using System.Collections.Generic;
using UnityEngine;

namespace VFXComposer.W24
{
    public enum W24TimelineCommand { Continuous, Impulse, Replace, Clear, Interrupt }
    [Serializable]
    public struct W24TimelineEvent { public int Serial; public string EventId; public W24SemanticState State; public float Time; }

    /// <summary>Pure semantic state model. Completion and interruption are intentionally separate terminal exits.</summary>
    public sealed class W24SemanticTimelineModel
    {
        private readonly float impulseDuration;
        private readonly List<W24TimelineEvent> events = new List<W24TimelineEvent>();
        private int serial;
        public W24SemanticState State { get; private set; }
        public float Elapsed { get; private set; }
        public IReadOnlyList<W24TimelineEvent> Events { get { return events; } }
        public W24SemanticTimelineModel(float impulseDuration) { this.impulseDuration = Mathf.Max(.01f, impulseDuration); State = W24SemanticState.Idle; }
        public void Send(W24TimelineCommand command)
        {
            Elapsed = 0f;
            switch (command)
            {
                case W24TimelineCommand.Continuous: Set(W24SemanticState.Continuous, "continuous_start"); break;
                case W24TimelineCommand.Impulse: Set(W24SemanticState.Impulse, "impulse_start"); break;
                case W24TimelineCommand.Replace: Set(W24SemanticState.Replacing, "replace"); Set(W24SemanticState.Continuous, "replacement_started"); break;
                case W24TimelineCommand.Clear: Set(W24SemanticState.Clearing, "clear"); Set(W24SemanticState.Completed, "completed"); break;
                case W24TimelineCommand.Interrupt: Set(W24SemanticState.Interrupted, "interrupted"); break;
            }
        }
        public void Advance(float deltaTime)
        {
            if (State != W24SemanticState.Impulse) return;
            Elapsed += Mathf.Max(0f, deltaTime);
            if (Elapsed >= impulseDuration) Set(W24SemanticState.Completed, "completed");
        }
        public void Reset() { Elapsed = 0f; Set(W24SemanticState.Idle, "reset"); }
        private void Set(W24SemanticState state, string eventId) { State = state; events.Add(new W24TimelineEvent { Serial = ++serial, EventId = eventId, State = state, Time = Elapsed }); }
    }

    [DisallowMultipleComponent]
    public sealed class W24SemanticTimeline : MonoBehaviour, IW24SemanticTelemetrySource
    {
        [SerializeField, Min(.01f)] private float impulseDuration = .3f;
        [SerializeField] private uint canonicalSeed = 701u;
        private W24SemanticTimelineModel model;
        public event Action<W24TimelineEvent> EventRaised;
        public W24SemanticState State { get { EnsureModel(); return model.State; } }
        private void Awake() { EnsureModel(); }
        private void Update() { Advance(Time.deltaTime); }
        public void Send(W24TimelineCommand command) { EnsureModel(); var count = model.Events.Count; model.Send(command); PublishFrom(count); }
        public void Advance(float deltaTime) { EnsureModel(); var count = model.Events.Count; model.Advance(deltaTime); PublishFrom(count); }
        public void ResetForPool() { EnsureModel(); var count = model.Events.Count; model.Reset(); PublishFrom(count); }
        public W24SemanticTelemetry ReadSemanticTelemetry()
        {
            EnsureModel(); var latest = model.Events.Count == 0 ? "" : model.Events[model.Events.Count - 1].EventId;
            return new W24SemanticTelemetry { Module = "semantic_timeline", State = model.State, Seed = canonicalSeed, EventSerial = model.Events.Count, ActiveItemCount = model.State == W24SemanticState.Continuous || model.State == W24SemanticState.Impulse ? 1 : 0, Elapsed = model.Elapsed, CleanupComplete = model.State == W24SemanticState.Completed || model.State == W24SemanticState.Idle || model.State == W24SemanticState.Interrupted, LastEventId = latest };
        }
        private void EnsureModel() { if (model == null) model = new W24SemanticTimelineModel(impulseDuration); }
        private void PublishFrom(int count) { for (var index = count; index < model.Events.Count; index++) { var raised = EventRaised; if (raised != null) raised(model.Events[index]); } }
    }
}

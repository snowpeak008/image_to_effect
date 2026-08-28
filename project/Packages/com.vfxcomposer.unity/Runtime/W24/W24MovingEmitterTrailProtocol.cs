using System;
using System.Collections.Generic;
using UnityEngine;

namespace VFXComposer.W24
{
    [Serializable]
    public struct W24MotionSample
    {
        public Vector3 Position;
        public float Time;
        public bool IsNewHead;
    }

    /// <summary>
    /// Immutable capture-facing readback of the real emitter samples accepted by the motion
    /// protocol.  The array is a copy: diagnostics cannot mutate live protocol state and never
    /// need to inspect TrailRenderer vertices.
    /// </summary>
    [Serializable]
    public struct W24EmitterHistoryReadback
    {
        public uint Seed;
        public int Generation;
        public bool IsRecording;
        public bool IsFrozenTail;
        public string LastClearReason;
        public W24MotionSample[] Samples;
        public int Count { get { return Samples == null ? 0 : Samples.Length; } }
        public bool IsCleared { get { return Count == 0; } }
    }

    /// <summary>Pure motion sampler. Stationary samples intentionally do not create a new trail head.</summary>
    public sealed class W24MotionSampleProtocol
    {
        private readonly float minimumDistance;
        private bool hasSample;
        private Vector3 lastPosition;
        private int sampleCount;

        public W24MotionSampleProtocol(float minimumDistance)
        {
            this.minimumDistance = Mathf.Max(.00001f, minimumDistance);
        }

        public int SampleCount { get { return sampleCount; } }
        public bool TrySample(Vector3 position, float time, out W24MotionSample sample)
        {
            var isNew = !hasSample || (position - lastPosition).sqrMagnitude >= minimumDistance * minimumDistance;
            sample = new W24MotionSample { Position = position, Time = time, IsNewHead = isNew };
            if (!isNew) return false;
            hasSample = true;
            lastPosition = position;
            sampleCount++;
            return true;
        }
        public void Reset() { hasSample = false; sampleCount = 0; lastPosition = Vector3.zero; }
    }

    /// <summary>
    /// Moves an emission anchor from a real transform and only enables TrailRenderer emission while the anchor has moved.
    /// Reset clears every TrailRenderer so pooled instances cannot inherit a previous owner's path.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class W24MovingEmitterTrailProtocol : MonoBehaviour, IW24SemanticTelemetrySource
    {
        [SerializeField] private Transform motionSource;
        [SerializeField] private TrailRenderer[] trails = new TrailRenderer[0];
        [SerializeField, Min(.00001f)] private float minimumHeadDistance = .01f;
        [SerializeField] private uint canonicalSeed = 101u;
        [SerializeField] private bool requireWorldSpaceHistory = true;

        private W24MotionSampleProtocol sampler;
        private readonly List<W24MotionSample> emitterHistory = new List<W24MotionSample>();
        private float elapsed;
        private int eventSerial;
        private int historyGeneration;
        private bool active;
        private bool historyFrozenTail;
        private uint activeSeed;
        private string lastHistoryClearReason = "reset";
        private string lastEventId = "reset";

        public int SampleCount { get { EnsureSampler(); return sampler.SampleCount; } }
        public int EmitterHistoryCount { get { return emitterHistory.Count; } }
        public bool UsesWorldSpaceHistory { get { return requireWorldSpaceHistory; } }
        public bool IsMoving { get; private set; }
        public int LiveTrailPointCount
        {
            get
            {
                var count = 0;
                if (trails != null) foreach (var trail in trails) if (trail != null) count += trail.positionCount;
                return count;
            }
        }
        public void SetMotionSource(Transform source) { motionSource = source; ResetForPool(); }
        public void SetTrails(TrailRenderer[] values) { trails = values ?? new TrailRenderer[0]; }

        private void Awake() { EnsureSampler(); ResetForPool(); }
        private void Update() { Tick(Time.deltaTime); }
        public void Tick(float deltaTime)
        {
            if (!active) return;
            elapsed += Mathf.Max(0f, deltaTime);
            var source = motionSource == null ? transform : motionSource;
            if (source != transform) transform.SetPositionAndRotation(source.position, source.rotation);
            W24MotionSample accepted;
            IsMoving = sampler.TrySample(transform.position, elapsed, out accepted);
            foreach (var trail in trails)
                if (trail != null) trail.emitting = IsMoving;
            if (IsMoving)
            {
                emitterHistory.Add(accepted);
                eventSerial++; lastEventId = "motion_sample";
            }
        }
        public void Play(uint seed = 0u)
        {
            if (!requireWorldSpaceHistory) throw new InvalidOperationException("W24 moving-emitter trails require world-space history.");
            canonicalSeed = seed == 0u ? canonicalSeed : seed;
            activeSeed = canonicalSeed;
            ClearEmitterHistory("play_seed_restart");
            active = true; elapsed = 0f; eventSerial++; lastEventId = "start";
            EnsureSampler(); sampler.Reset();
            foreach (var trail in trails) if (trail != null) { trail.Clear(); trail.enabled = true; trail.emitting = false; }
        }
        public void Stop(bool clearImmediately)
        {
            active = false; IsMoving = false; historyFrozenTail = !clearImmediately && emitterHistory.Count > 0; eventSerial++; lastEventId = clearImmediately ? "clear" : "stop";
            foreach (var trail in trails)
            {
                if (trail == null) continue;
                trail.emitting = false;
                if (clearImmediately) { trail.Clear(); trail.enabled = false; }
            }
            if (clearImmediately) ClearEmitterHistory("immediate_stop");
        }
        public void ResetForPool() { Stop(true); EnsureSampler(); sampler.Reset(); elapsed = 0f; ClearEmitterHistory("pool_reset"); }
        public W24EmitterHistoryReadback ReadEmitterHistory()
        {
            return new W24EmitterHistoryReadback
            {
                Seed = activeSeed == 0u ? canonicalSeed : activeSeed,
                Generation = historyGeneration,
                IsRecording = active,
                IsFrozenTail = historyFrozenTail,
                LastClearReason = lastHistoryClearReason,
                Samples = emitterHistory.ToArray()
            };
        }
        public W24SemanticTelemetry ReadSemanticTelemetry()
        {
            var livePoints = LiveTrailPointCount;
            var state = active ? W24SemanticState.Continuous : livePoints > 0 ? W24SemanticState.Clearing : W24SemanticState.Idle;
            return new W24SemanticTelemetry { Module = "moving_emitter_trail", State = state, Seed = activeSeed == 0u ? canonicalSeed : activeSeed, EventSerial = eventSerial, ActiveItemCount = livePoints, Elapsed = elapsed, CleanupComplete = !active && livePoints == 0 && emitterHistory.Count == 0, LastEventId = lastEventId };
        }
        private void ClearEmitterHistory(string reason)
        {
            emitterHistory.Clear(); historyGeneration++; historyFrozenTail = false; lastHistoryClearReason = reason;
        }
        private void EnsureSampler() { if (sampler == null) sampler = new W24MotionSampleProtocol(minimumHeadDistance); }
        private void OnDisable() { if (sampler != null) ResetForPool(); }
    }
}

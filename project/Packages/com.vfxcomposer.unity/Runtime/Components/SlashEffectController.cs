using System;
using UnityEngine;

namespace VFXComposer
{
    /// <summary>Serialized by the v2 Slash compiler; schedules only visual sibling phases and exposes no gameplay data.</summary>
    [DisallowMultipleComponent]
    public sealed class SlashEffectController : MonoBehaviour, IVfxRuntimeEntry
    {
        [Serializable]
        public sealed class PhaseBinding
        {
            [SerializeField] private string phaseId;
            [SerializeField] private GameObject root;
            [SerializeField, Min(0f)] private float startTime;
            [SerializeField, Min(0f)] private float duration;
            public string PhaseId { get { return phaseId; } }
            public GameObject Root { get { return root; } }
            public float StartTime { get { return startTime; } }
            public float Duration { get { return duration; } }
        }

        [SerializeField, Min(.01f)] private float timelineDuration = .45f;
        [SerializeField] private PhaseBinding[] phases = new PhaseBinding[0];
        private Vector3 initialPosition;
        private Quaternion initialRotation;
        private bool initialPoseCaptured;
        private float elapsed;
        private bool playing;
        private bool draining;

        public bool IsPlaying { get { return playing; } }
        public bool IsAlive { get { return playing || draining || !AllParticlesClear(); } }
        public float Elapsed { get { return elapsed; } }
        public float TimelineDuration { get { return timelineDuration; } }
        public PhaseBinding[] Phases { get { return phases; } }

        private void Awake() { CaptureInitialPose(); ResetForPool(); }
        private void Update()
        {
            if (draining)
            {
                if (AllParticlesClear()) { ClearAll(true); draining = false; }
                return;
            }
            if (!playing) return;
            elapsed += Time.deltaTime;
            ApplySchedule(elapsed);
            if (elapsed >= timelineDuration)
            {
                ClearAll(true);
                playing = false;
            }
        }

        /// <summary>Starts the self-contained visual Slash at a world pose. It intentionally accepts no target, hit, weapon, or damage data.</summary>
        public void PlaySlash(Vector3 position, Quaternion orientation)
        {
            CaptureInitialPose();
            ClearAll(true);
            transform.SetPositionAndRotation(position, orientation);
            elapsed = 0f;
            draining = false;
            playing = true;
            ApplySchedule(0f);
        }

        public void Initialize(VfxRuntimeContext context) { ResetForPool(); transform.SetPositionAndRotation(context.Position, context.Rotation); }
        public void Play() { PlaySlash(transform.position, transform.rotation); }
        public bool SendEvent(string eventId, VfxRuntimeEvent payload)
        {
            if (eventId != "play" && eventId != "slash") return false;
            PlaySlash(payload.Position, payload.Rotation);
            return true;
        }
        public void Stop(VfxStopMode mode) { StopEffect(mode == VfxStopMode.Immediate); }

        /// <summary>Stops this visual effect. Immediate synchronously clears all visuals; non-immediate stops emission and completes on the next clear.</summary>
        public void StopEffect(bool immediate)
        {
            if (immediate) { ClearAll(true); playing = false; draining = false; return; }
            foreach (var phase in phases) StopPhase(phase, false);
            playing = false;
            draining = true;
        }

        /// <summary>Restores the prefab pose and clears particles, render state, and per-renderer property blocks for pooling.</summary>
        public void ResetForPool()
        {
            CaptureInitialPose();
            ClearAll(true);
            transform.SetPositionAndRotation(initialPosition, initialRotation);
            elapsed = 0f;
            playing = false;
            draining = false;
        }

        public bool IsPhaseVisible(string id)
        {
            foreach (var phase in phases) if (phase != null && phase.PhaseId == id) return phase.Root != null && phase.Root.activeInHierarchy;
            return false;
        }

        // Compiler-owned deterministic sampler for Editor capture/tests. It is internal, remains player-safe,
        // and does not widen the player-safe public API.
        internal void SampleForPreview(float time)
        {
            ClearAll(true);
            elapsed = Mathf.Max(0f, time);
            if (elapsed >= timelineDuration) { playing = false; draining = false; return; }
            playing = true;
            draining = false;
            ApplySchedule(elapsed);
            foreach (var phase in phases) if (phase != null && phase.Root != null && phase.Root.activeSelf) foreach (var particle in phase.Root.GetComponentsInChildren<ParticleSystem>(true)) particle.Simulate(Mathf.Max(.01f, elapsed - phase.StartTime), true, true, true);
        }

        // Explicit evidence-only sequential step. Unlike SampleForPreview it never seeks: every call advances
        // exactly one delta through the same schedule and natural ParticleSystem simulation used by playback.
        internal void StepForContinuousCapture(float deltaTime)
        {
            if (!playing || deltaTime <= 0f) return;
            elapsed += deltaTime;
            ApplySchedule(elapsed);
            foreach (var phase in phases)
            {
                if (phase == null || phase.Root == null || !phase.Root.activeSelf) continue;
                foreach (var particle in phase.Root.GetComponentsInChildren<ParticleSystem>(true)) particle.Simulate(deltaTime, true, false, false);
            }
            if (elapsed >= timelineDuration) { ClearAll(true); playing = false; }
        }

        private void ApplySchedule(float time)
        {
            foreach (var phase in phases)
            {
                if (phase == null || phase.Root == null) continue;
                var active = time >= phase.StartTime && time < phase.StartTime + phase.Duration && time <= timelineDuration;
                if (active && !phase.Root.activeSelf) StartPhase(phase);
                if (active) ApplyPhaseProgress(phase, (time - phase.StartTime) / Mathf.Max(.0001f, phase.Duration));
                else if (!active && phase.Root.activeSelf) StopPhase(phase, true);
            }
        }

        private static void StartPhase(PhaseBinding phase)
        {
            phase.Root.SetActive(true);
            foreach (var particle in phase.Root.GetComponentsInChildren<ParticleSystem>(true))
            {
                particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                particle.Clear(true);
                particle.Play(true);
            }
        }

        private static void ApplyPhaseProgress(PhaseBinding phase, float progress)
        {
            foreach (var reveal in phase.Root.GetComponentsInChildren<SlashArcSweepReveal>(true)) reveal.SetReveal(progress);
            foreach (var layer in phase.Root.GetComponentsInChildren<SlashPaintedLayerFade>(true)) layer.SetPhaseProgress(progress);
        }

        private static void StopPhase(PhaseBinding phase, bool immediate)
        {
            if (phase == null || phase.Root == null) return;
            foreach (var particle in phase.Root.GetComponentsInChildren<ParticleSystem>(true))
            {
                particle.Stop(true, immediate ? ParticleSystemStopBehavior.StopEmittingAndClear : ParticleSystemStopBehavior.StopEmitting);
                if (immediate) particle.Clear(true);
            }
            if (immediate)
            {
                foreach (var renderer in phase.Root.GetComponentsInChildren<Renderer>(true)) renderer.SetPropertyBlock(null);
                phase.Root.SetActive(false);
            }
        }

        private void ClearAll(bool immediate)
        {
            foreach (var phase in phases) StopPhase(phase, immediate);
        }

        private bool AllParticlesClear()
        {
            foreach (var phase in phases)
            {
                if (phase == null || phase.Root == null) continue;
                foreach (var particle in phase.Root.GetComponentsInChildren<ParticleSystem>(true)) if (particle.IsAlive(true)) return false;
            }
            return true;
        }

        private void CaptureInitialPose()
        {
            if (initialPoseCaptured) return;
            initialPosition = transform.position;
            initialRotation = transform.rotation;
            initialPoseCaptured = true;
        }
    }
}

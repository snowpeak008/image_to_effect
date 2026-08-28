using System;
using UnityEngine;

namespace VFXComposer
{
    public enum SustainedEffectState
    {
        Idle,
        Starting,
        Steady,
        Stopping,
        Interrupted
    }

    [Serializable]
    public struct SustainedEffectTelemetry
    {
        public SustainedEffectState State;
        public float StateElapsed;
        public float LifetimeElapsed;
        public uint Seed;
        public int LiveParticleCount;
        public int EmittingParticleSystemCount;
        public int EnabledRendererCount;
        public int EnabledLightCount;
        public int TransitionSerial;
        public bool CleanupComplete;
    }

    /// <summary>
    /// Player-safe lifecycle for effects which remain alive until explicitly stopped.  Start,
    /// steady, stop and interrupt are semantic phases; they are not aliases for one looping image.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SustainedEffectController : MonoBehaviour, IVfxRuntimeEntry
    {
        [Header("Semantic phase roots")]
        [SerializeField] private GameObject startRoot;
        [SerializeField] private GameObject steadyRoot;
        [SerializeField] private GameObject stopRoot;
        [SerializeField] private GameObject interruptRoot;

        [Header("Real lighting")]
        [SerializeField] private Light[] controlledLights = new Light[0];
        [SerializeField, Min(0f)] private float steadyLightIntensity = 1.25f;

        [Header("Lifecycle deadlines")]
        [SerializeField, Min(.01f)] private float startDuration = .35f;
        [SerializeField, Min(.01f)] private float stopDuration = .8f;
        [SerializeField, Min(.01f)] private float interruptDuration = .35f;
        [SerializeField, Min(.01f)] private float cleanupDeadline = 1.25f;
        [SerializeField] private uint canonicalSeed = 24001u;

        private Vector3 initialPosition;
        private Quaternion initialRotation;
        private bool capturedInitialTransform;
        private float stateElapsed;
        private float lifetimeElapsed;
        private float cleanupElapsed;
        private int transitionSerial;
        private uint activeSeed;

        public SustainedEffectState State { get; private set; }
        public float StateElapsed { get { return stateElapsed; } }
        public float LifetimeElapsed { get { return lifetimeElapsed; } }
        public float CleanupDeadline { get { return cleanupDeadline; } }
        public uint ActiveSeed { get { return activeSeed; } }
        public int TransitionSerial { get { return transitionSerial; } }
        public bool IsAlive { get { return State != SustainedEffectState.Idle || HasLiveVisuals(); } }

        public event Action<SustainedEffectState> StateChanged;

        private void Awake()
        {
            CaptureInitialTransform();
            ResetForPool();
        }

        private void Update()
        {
            Advance(Mathf.Max(0f, Time.deltaTime));
        }

        public void Initialize(VfxRuntimeContext context)
        {
            ResetForPool();
            transform.SetPositionAndRotation(context.Position, context.Rotation);
        }

        public void Play()
        {
            PlayWithSeed(canonicalSeed);
        }

        public void PlayWithSeed(uint seed)
        {
            ClearVisuals();
            activeSeed = seed == 0u ? 1u : seed;
            ApplyDeterministicSeeds(activeSeed);
            lifetimeElapsed = 0f;
            cleanupElapsed = 0f;
            ActivateRoot(startRoot, true);
            ActivateRoot(steadyRoot, false);
            ActivateRoot(stopRoot, false);
            ActivateRoot(interruptRoot, false);
            SetLights(true, 0f);
            TransitionTo(SustainedEffectState.Starting);
        }

        public bool SendEvent(string eventId, VfxRuntimeEvent payload)
        {
            switch (eventId)
            {
                case "start":
                case "play":
                    transform.SetPositionAndRotation(payload.Position, payload.Rotation);
                    Play();
                    return true;
                case "stop":
                    Stop(VfxStopMode.AllowTail);
                    return true;
                case "interrupt":
                case "cancel":
                    Interrupt();
                    return true;
                case "clear":
                case "reset":
                    ResetForPool();
                    return true;
                default:
                    return false;
            }
        }

        public void Stop(VfxStopMode mode)
        {
            if (mode == VfxStopMode.Immediate)
            {
                ResetForPool();
                return;
            }

            if (State == SustainedEffectState.Idle || State == SustainedEffectState.Stopping)
                return;

            StopEmission(startRoot, false);
            StopEmission(steadyRoot, false);
            ActivateRoot(stopRoot, true);
            ActivateRoot(interruptRoot, false);
            cleanupElapsed = 0f;
            TransitionTo(SustainedEffectState.Stopping);
        }

        public void Interrupt()
        {
            if (State == SustainedEffectState.Idle || State == SustainedEffectState.Interrupted)
                return;

            StopEmission(startRoot, true);
            StopEmission(steadyRoot, true);
            ActivateRoot(stopRoot, false);
            ActivateRoot(interruptRoot, true);
            cleanupElapsed = 0f;
            TransitionTo(SustainedEffectState.Interrupted);
        }

        public void ResetForPool()
        {
            CaptureInitialTransform();
            ClearVisuals();
            transform.SetPositionAndRotation(initialPosition, initialRotation);
            stateElapsed = 0f;
            lifetimeElapsed = 0f;
            cleanupElapsed = 0f;
            activeSeed = canonicalSeed == 0u ? 1u : canonicalSeed;
            if (State != SustainedEffectState.Idle)
                TransitionTo(SustainedEffectState.Idle);
            else
                State = SustainedEffectState.Idle;
        }

        public SustainedEffectTelemetry ReadTelemetry()
        {
            var particleCount = 0;
            var emittingCount = 0;
            var rendererCount = 0;
            foreach (var particle in GetComponentsInChildren<ParticleSystem>(true))
            {
                particleCount += particle.particleCount;
                if (particle.isEmitting) emittingCount++;
            }
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
                if (renderer.enabled && renderer.gameObject.activeInHierarchy) rendererCount++;
            var lightCount = 0;
            if (controlledLights != null)
                foreach (var light in controlledLights)
                    if (light != null && light.enabled && light.gameObject.activeInHierarchy && light.intensity > .0001f) lightCount++;

            return new SustainedEffectTelemetry
            {
                State = State,
                StateElapsed = stateElapsed,
                LifetimeElapsed = lifetimeElapsed,
                Seed = activeSeed,
                LiveParticleCount = particleCount,
                EmittingParticleSystemCount = emittingCount,
                EnabledRendererCount = rendererCount,
                EnabledLightCount = lightCount,
                TransitionSerial = transitionSerial,
                CleanupComplete = State == SustainedEffectState.Idle && !HasLiveVisuals()
            };
        }

        internal void Advance(float deltaTime)
        {
            if (deltaTime <= 0f || State == SustainedEffectState.Idle) return;
            stateElapsed += deltaTime;
            lifetimeElapsed += deltaTime;

            if (State == SustainedEffectState.Starting)
            {
                SetLights(true, Mathf.SmoothStep(0f, steadyLightIntensity, Mathf.Clamp01(stateElapsed / startDuration)));
                // Begin the steady carriers during the latter half of ignition so the
                // hand-off is continuous, but do not falsely run the full steady effect
                // from frame zero while telemetry still reports Starting.
                if (stateElapsed >= startDuration * .55f && steadyRoot != null && !steadyRoot.activeSelf)
                    ActivateRoot(steadyRoot, true);
                // A duration expressed as an exact frame boundary (for example .35 s at
                // 60 fps) can accumulate a few ULPs below the serialized value. Treat
                // that mathematically equal boundary as reached; otherwise a 21-frame
                // contract silently becomes 22 frames in the real PlayerLoop.
                if (stateElapsed >= startDuration || Mathf.Approximately(stateElapsed, startDuration))
                {
                    if (steadyRoot != null && !steadyRoot.activeSelf)
                        ActivateRoot(steadyRoot, true);
                    StopEmission(startRoot, false);
                    TransitionTo(SustainedEffectState.Steady);
                    SetLights(true, steadyLightIntensity);
                }
                return;
            }

            if (State == SustainedEffectState.Steady)
            {
                SetLights(true, steadyLightIntensity);
                return;
            }

            cleanupElapsed += deltaTime;
            var duration = State == SustainedEffectState.Interrupted ? interruptDuration : stopDuration;
            var fade = 1f - Mathf.Clamp01(stateElapsed / Mathf.Max(.01f, duration));
            SetLights(fade > 0f, steadyLightIntensity * fade);
            if (stateElapsed >= duration)
            {
                StopEmission(stopRoot, false);
                StopEmission(interruptRoot, false);
            }

            if ((stateElapsed >= duration && !HasLiveParticles()) || cleanupElapsed >= cleanupDeadline)
                CompleteCleanup();
        }

        private void CompleteCleanup()
        {
            ClearVisuals();
            TransitionTo(SustainedEffectState.Idle);
        }

        private void TransitionTo(SustainedEffectState next)
        {
            if (State == next) return;
            State = next;
            stateElapsed = 0f;
            transitionSerial++;
            var changed = StateChanged;
            if (changed != null) changed(next);
        }

        private void ApplyDeterministicSeeds(uint seed)
        {
            var systems = GetComponentsInChildren<ParticleSystem>(true);
            for (var index = 0; index < systems.Length; index++)
            {
                systems[index].useAutoRandomSeed = false;
                systems[index].randomSeed = unchecked(seed + (uint)(index * 104729) + 1u);
            }
        }

        private static void ActivateRoot(GameObject root, bool play)
        {
            if (root == null) return;
            root.SetActive(play);
            foreach (var particle in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                particle.Clear(true);
                if (play) particle.Play(true);
            }
        }

        private static void StopEmission(GameObject root, bool clear)
        {
            if (root == null) return;
            foreach (var particle in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                particle.Stop(true, clear ? ParticleSystemStopBehavior.StopEmittingAndClear : ParticleSystemStopBehavior.StopEmitting);
                if (clear) particle.Clear(true);
            }
            if (clear) root.SetActive(false);
        }

        private void ClearVisuals()
        {
            ClearRoot(startRoot);
            ClearRoot(steadyRoot);
            ClearRoot(stopRoot);
            ClearRoot(interruptRoot);
            SetLights(false, 0f);
        }

        private static void ClearRoot(GameObject root)
        {
            if (root == null) return;
            foreach (var particle in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                particle.Clear(true);
            }
            foreach (var trail in root.GetComponentsInChildren<TrailRenderer>(true)) trail.Clear();
            root.SetActive(false);
        }

        private void SetLights(bool enabled, float intensity)
        {
            if (controlledLights == null) return;
            foreach (var light in controlledLights)
            {
                if (light == null) continue;
                light.enabled = enabled;
                light.intensity = Mathf.Max(0f, intensity);
            }
        }

        private bool HasLiveVisuals()
        {
            if (HasLiveParticles()) return true;
            if (controlledLights != null)
                foreach (var light in controlledLights)
                    if (light != null && light.enabled && light.intensity > .0001f) return true;
            return false;
        }

        private bool HasLiveParticles()
        {
            foreach (var particle in GetComponentsInChildren<ParticleSystem>(true))
                if (particle.IsAlive(true)) return true;
            return false;
        }

        private void CaptureInitialTransform()
        {
            if (capturedInitialTransform) return;
            initialPosition = transform.position;
            initialRotation = transform.rotation;
            capturedInitialTransform = true;
        }
    }
}

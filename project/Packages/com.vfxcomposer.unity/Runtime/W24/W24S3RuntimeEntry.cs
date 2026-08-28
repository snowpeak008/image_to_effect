using UnityEngine;
using VFXComposer;

namespace VFXComposer.W24
{
    /// <summary>
    /// Player-safe S3 composition root. It is deliberately a thin lifecycle bridge: the S2
    /// modules retain their own carrier semantics, while game code has one IVfxRuntimeEntry.
    /// Model roots cannot be expressed by VfxRuntimeContext, so callers must ConfigureModelRoot
    /// before Play (or send bind_configured); a missing root is surfaced as a binding fault.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class W24S3RuntimeEntry : MonoBehaviour, IVfxRuntimeEntry, IW24SemanticTelemetrySource
    {
        [SerializeField] private GameObject launchRoot;
        [SerializeField] private GameObject activeRoot;
        [SerializeField] private GameObject impactRoot;
        [SerializeField] private W24MovingEmitterTrailProtocol movingTrail;
        [SerializeField] private W24ModelBindingAdapter modelBinding;
        [SerializeField] private Transform bindingVisualRoot;
        [SerializeField] private W24FragmentMotionSystem fragments;
        [SerializeField] private W24SemanticTimeline timeline;
        [SerializeField] private W24RealLightingModule lighting;
        [SerializeField] private bool requiresModelBinding;
        [SerializeField, Min(.01f)] private float allowTailSeconds = .45f;
        [SerializeField, Min(0f)] private float requestedLightIntensity = 1.2f;
        [SerializeField] private uint canonicalSeed = 24101u;

        private Transform configuredModelRoot;
        private Vector3 visualHomePosition;
        private Quaternion visualHomeRotation;
        private bool capturedVisualHome;
        private bool playing;
        private bool allowingTail;
        private float tailElapsed;
        private W24BindingFault lastBindingFault;
        private string lastEntryEventId = "reset";

        public bool IsAlive { get { return playing || allowingTail; } }
        public W24BindingFault LastBindingFault { get { return lastBindingFault; } }
        public Transform ConfiguredModelRoot { get { return configuredModelRoot; } }
        public W24EmitterHistoryReadback ReadEmitterHistory()
        {
            if (movingTrail == null) return new W24EmitterHistoryReadback { Seed = canonicalSeed, LastClearReason = "no_moving_trail", Samples = new W24MotionSample[0] };
            return movingTrail.ReadEmitterHistory();
        }
        public W24BindingProbeReport ReadMissingBindingProbeReport() { return W24BindingDiagnosticProbes.Run(configuredModelRoot); }

        /// <summary>
        /// Capture-only deterministic seed bridge.  It is intentionally rejected while live so
        /// normal gameplay cannot silently change an effect's stochastic identity mid-lifecycle.
        /// Formal graphics capture invokes it before Initialize/Play for the frozen canonical and
        /// two robustness seeds declared by the candidate Capture Profile.
        /// </summary>
        public void SetCaptureSeed(uint seed)
        {
            if (IsAlive) throw new System.InvalidOperationException("W24 S3 capture seed can only change while the Runtime Entry is inactive.");
            if (seed == 0u) throw new System.ArgumentOutOfRangeException(nameof(seed), "W24 S3 formal capture seed must be non-zero.");
            canonicalSeed = seed;
        }

        private void Awake()
        {
            CaptureVisualHome();
            ResetForPoolInternal(false);
        }

        private void Update()
        {
            if (!allowingTail) return;
            tailElapsed += Mathf.Max(0f, Time.deltaTime);
            if (tailElapsed >= EffectiveTailDeadline()) CompleteTail();
        }

        /// <summary>Explicit gameplay configuration for model-bound S3 entries. Null is a valid fault-producing input.</summary>
        public void ConfigureModelRoot(Transform modelRoot) { configuredModelRoot = modelRoot; }

        public void Initialize(VfxRuntimeContext context)
        {
            ResetForPoolInternal(false);
            transform.SetPositionAndRotation(context.Position, context.Rotation);
            if (requiresModelBinding) TryBindConfiguredModel();
        }

        public void Play()
        {
            ResetVisualRoots();
            allowingTail = false; tailElapsed = 0f; lastBindingFault = W24BindingFault.None;
            if (requiresModelBinding && !TryBindConfiguredModel())
            {
                playing = false;
                if (timeline != null) timeline.Send(W24TimelineCommand.Interrupt);
                return;
            }

            playing = true;
            lastEntryEventId = "playing";
            ActivateAndPlay(launchRoot); ActivateAndPlay(activeRoot);
            if (movingTrail != null)
            {
                movingTrail.SetMotionSource(transform);
                movingTrail.Play(canonicalSeed);
                if (movingTrail.ReadEmitterHistory().Seed != canonicalSeed)
                {
                    movingTrail.ResetForPool(); playing = false;
                    throw new System.InvalidOperationException("W24 S3 moving-emitter history did not bind the Runtime Entry capture seed.");
                }
            }
            if (lighting != null) lighting.SetLights(true, requestedLightIntensity);
            if (timeline != null) timeline.Send(W24TimelineCommand.Continuous);
        }

        public bool SendEvent(string eventId, VfxRuntimeEvent payload)
        {
            switch (eventId)
            {
                case "play":
                case "launch":
                    transform.SetPositionAndRotation(payload.Position, payload.Rotation); Play(); return !requiresModelBinding || lastBindingFault == W24BindingFault.None;
                case "travel":
                    if (!playing) return false;
                    transform.SetPositionAndRotation(payload.Position, payload.Rotation); ActivateAndPlay(activeRoot); return true;
                case "impact":
                    if (!playing) return false;
                    transform.SetPositionAndRotation(payload.Position, payload.Rotation); ActivateAndPlay(impactRoot);
                    if (movingTrail != null) movingTrail.Stop(false);
                    if (timeline != null) timeline.Send(W24TimelineCommand.Impulse);
                    BeginTail("impact");
                    return true;
                case "fragment":
                    if (!playing || fragments == null) return false;
                    if (bindingVisualRoot != null) bindingVisualRoot.SetParent(transform, true);
                    fragments.Play(canonicalSeed); if (timeline != null) timeline.Send(W24TimelineCommand.Impulse);
                    BeginTail("fragment");
                    return true;
                case "bind_configured":
                    return TryBindConfiguredModel();
                case "stop":
                    Stop(VfxStopMode.AllowTail); return true;
                case "cancel":
                    Stop(VfxStopMode.Immediate); return true;
                case "reset":
                    ResetForPool(); return true;
                default:
                    return false;
            }
        }

        public void Stop(VfxStopMode mode)
        {
            if (mode == VfxStopMode.Immediate) { ResetForPoolInternal(true); return; }
            if (!playing && !allowingTail) return;
            BeginTail("stop");
            StopParticles(launchRoot); StopParticles(activeRoot); StopParticles(impactRoot);
            if (movingTrail != null) movingTrail.Stop(false);
            if (lighting != null) lighting.SetLights(false, 0f);
        }

        public void ResetForPool() { ResetForPoolInternal(true); }

        private void ResetForPoolInternal(bool recordInterrupt)
        {
            CaptureVisualHome();
            playing = false; allowingTail = false; tailElapsed = 0f; lastBindingFault = W24BindingFault.None;
            if (recordInterrupt && timeline != null) timeline.Send(W24TimelineCommand.Interrupt);
            if (movingTrail != null) movingTrail.ResetForPool();
            if (fragments != null) fragments.ResetForPool();
            if (lighting != null) lighting.ResetForPool();
            if (timeline != null) timeline.ResetForPool();
            if (modelBinding != null) modelBinding.ResetForPool();
            RestoreBindingVisualHome(); ResetVisualRoots();
            lastEntryEventId = recordInterrupt ? "interrupted_and_reset" : "reset";
        }

        public W24SemanticTelemetry ReadSemanticTelemetry()
        {
            return new W24SemanticTelemetry
            {
                Module = "w24_s3_runtime_entry",
                State = lastBindingFault != W24BindingFault.None ? W24SemanticState.Faulted : playing ? W24SemanticState.Continuous : allowingTail ? W24SemanticState.Clearing : W24SemanticState.Idle,
                Seed = canonicalSeed,
                ActiveItemCount = IsAlive ? 1 : 0,
                CleanupComplete = !IsAlive,
                LastEventId = lastBindingFault == W24BindingFault.None ? lastEntryEventId : "binding_fault",
                FaultCode = lastBindingFault.ToString()
            };
        }

        private void BeginTail(string eventId)
        {
            playing = false;
            allowingTail = true;
            tailElapsed = 0f;
            lastEntryEventId = eventId + "_tail";
        }

        private float EffectiveTailDeadline()
        {
            // These bounds mirror the frozen S3 contracts: fragments have an .8 s exit,
            // projectile residue has a .5 s exit, and real-light cleanup has a .2 s exit.
            if (fragments != null) return Mathf.Max(allowTailSeconds, .8f);
            if (movingTrail != null) return Mathf.Max(allowTailSeconds, .5f);
            if (lighting != null) return Mathf.Min(allowTailSeconds, .2f);
            return allowTailSeconds;
        }

        private void CompleteTail()
        {
            playing = false; allowingTail = false; tailElapsed = 0f;
            if (movingTrail != null) movingTrail.ResetForPool();
            if (fragments != null) fragments.ResetForPool();
            if (lighting != null) lighting.ResetForPool();
            if (modelBinding != null) modelBinding.ResetForPool();
            RestoreBindingVisualHome(); ResetVisualRoots();
            if (timeline != null) timeline.Send(W24TimelineCommand.Clear);
            lastEntryEventId = "completed";
        }

        private bool TryBindConfiguredModel()
        {
            if (modelBinding == null) { lastBindingFault = requiresModelBinding ? W24BindingFault.MissingTarget : W24BindingFault.None; return !requiresModelBinding; }
            var bound = modelBinding.Bind(configuredModelRoot);
            lastBindingFault = modelBinding.Result.Fault;
            return bound;
        }

        private void CaptureVisualHome()
        {
            if (capturedVisualHome || bindingVisualRoot == null) return;
            visualHomePosition = bindingVisualRoot.localPosition; visualHomeRotation = bindingVisualRoot.localRotation; capturedVisualHome = true;
        }
        private void RestoreBindingVisualHome()
        {
            if (bindingVisualRoot == null) return;
            bindingVisualRoot.SetParent(transform, false);
            bindingVisualRoot.localPosition = visualHomePosition; bindingVisualRoot.localRotation = visualHomeRotation;
        }
        private void ResetVisualRoots()
        {
            ClearRoot(launchRoot); ClearRoot(activeRoot); ClearRoot(impactRoot);
        }
        private static void ActivateAndPlay(GameObject root)
        {
            if (root == null) return;
            root.SetActive(true);
            foreach (var particle in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                particle.Play(true);
            }
        }
        private static void StopParticles(GameObject root)
        {
            if (root == null) return;
            foreach (var particle in root.GetComponentsInChildren<ParticleSystem>(true)) particle.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
        private static void ClearRoot(GameObject root)
        {
            if (root == null) return;
            foreach (var particle in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); particle.Clear(true);
            }
            foreach (var trail in root.GetComponentsInChildren<TrailRenderer>(true)) trail.Clear();
            root.SetActive(false);
        }
    }
}

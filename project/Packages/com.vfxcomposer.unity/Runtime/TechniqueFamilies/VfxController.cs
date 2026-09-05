using System;
using UnityEngine;
using UnityEngine.Events;

namespace VFXComposer.TechniqueFamilies
{
    /// <summary>Zero-GC struct payload for both inbound and outbound events (COMPILER_BOUNDARY_V2.md section 5.4).</summary>
    [Serializable]
    public struct VfxEventPayload
    {
        public Vector3 Position;
        public Vector3 Normal;
        public int Index;
        public float Value;
        public Quaternion Rotation;
    }

    /// <summary>
    /// Runtime controller v2 (COMPILER_BOUNDARY_V2.md section 5). Independent
    /// implementation — deliberately does NOT reuse GeneratedVfxController:
    /// phases are a data-driven table (0..5 entries), not the launch/travel/
    /// impact enum + container trio. Layers can span phases via per-phase
    /// activeLayers indices; sustain loops with a safety-valve timeout; events
    /// dispatch through a sorted-string binary search (no dictionaries, no
    /// reflection); ResetForPool performs the 9 mandated steps.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VfxController : MonoBehaviour
    {
        [Serializable]
        public sealed class PhaseEntry
        {
            public string id = "launch";
            [Tooltip("< 0 = externally driven / waits for an inbound event")]
            public float duration = 1f;
            public bool loop;
            public bool autoAdvance = true;
            public int[] activeLayers = new int[0];
            public float[] layerOffsets = new float[0];
            public float[] layerDurations = new float[0];
        }

        private static readonly int ProgressId = Shader.PropertyToID("_Progress");
        private static readonly int LocalTimeId = Shader.PropertyToID("_LocalTime");
        private static readonly int PhaseId = Shader.PropertyToID("_Phase");

        [Header("Phase table (compile-time)")]
        [SerializeField] private PhaseEntry[] phases = new PhaseEntry[0];
        [SerializeField, Min(1f)] private float maxSustainSeconds = 300f;

        [Header("Layer registry (compile-time)")]
        [SerializeField] private GameObject[] layerNodes = new GameObject[0];
        [SerializeField] private Renderer[] layerRenderers = new Renderer[0];
        [SerializeField] private ParticleSystem[] particleSystems = new ParticleSystem[0];
        [SerializeField] private TrailRenderer[] trailRenderers = new TrailRenderer[0];
        [SerializeField] private LineRenderer[] lineRenderers = new LineRenderer[0];
        [SerializeField] private Rigidbody[] debrisBodies = new Rigidbody[0];
        [SerializeField] private Vector3[] debrisInitialPositions = new Vector3[0];
        [SerializeField] private Quaternion[] debrisInitialRotations = new Quaternion[0];
        [SerializeField] private VfxLightBeat[] lightBeats = new VfxLightBeat[0];

        [Header("Inbound event table (compile-time, sorted)")]
        [SerializeField] private string[] eventIds = new string[0];
        [SerializeField] private int[] eventHandlers = new int[0]; // index into HandleEvent switch

        [Header("Outbound events")]
        [SerializeField] private UnityEvent<VfxEventPayload> onLaunchUnity;
        [SerializeField] private UnityEvent<VfxEventPayload> onImpactUnity;
        [SerializeField] private UnityEvent<VfxEventPayload> onEndUnity;
        [SerializeField] private UnityEvent<VfxEventPayload> onCompleteUnity;
        [SerializeField] private UnityEvent<VfxEventPayload> onSustainTimeoutUnity;

        public event Action<VfxEventPayload> OnLaunch;
        public event Action<VfxEventPayload> OnImpact;
        public event Action<VfxEventPayload> OnEnd;
        public event Action<VfxEventPayload> OnComplete;
        public event Action<VfxEventPayload> OnSustainTimeout;

        private MaterialPropertyBlock block;
        private float phaseTime;
        private float sustainElapsed;
        private Vector3 initialLocalPosition;
        private Quaternion initialLocalRotation;
        private Vector3 initialLocalScale;
        private bool initialCaptured;
        private bool completed;

        /// <summary>-1 = not playing. int, not an enum: the phase set is data.</summary>
        public int CurrentPhaseIndex { get; private set; } = -1;

        public float PhaseTime { get { return phaseTime; } }

        public string CurrentPhaseId
        {
            get { return CurrentPhaseIndex >= 0 && CurrentPhaseIndex < phases.Length ? phases[CurrentPhaseIndex].id : null; }
        }

        public PhaseEntry[] Phases { get { return phases; } }

        public bool IsAlive
        {
            get
            {
                if (CurrentPhaseIndex >= 0) return true;
                for (int i = 0; i < particleSystems.Length; i++)
                    if (particleSystems[i] != null && particleSystems[i].particleCount > 0) return true;
                for (int i = 0; i < trailRenderers.Length; i++)
                    if (trailRenderers[i] != null && trailRenderers[i].positionCount > 0) return true;
                for (int i = 0; i < debrisBodies.Length; i++)
                    if (debrisBodies[i] != null && !debrisBodies[i].isKinematic && !debrisBodies[i].IsSleeping()) return true;
                return false;
            }
        }

        /// <summary>Compile-time wiring (compiler / editor tooling).</summary>
        public void ConfigurePhases(PhaseEntry[] table)
        {
            phases = table ?? new PhaseEntry[0];
        }

        public void ConfigureLayers(GameObject[] nodes, Renderer[] renderers)
        {
            layerNodes = nodes ?? new GameObject[0];
            layerRenderers = renderers ?? new Renderer[0];
        }

        public void ConfigureEvents(string[] sortedIds, int[] handlerIndices)
        {
            eventIds = sortedIds ?? new string[0];
            eventHandlers = handlerIndices ?? new int[0];
        }

        public void ConfigureDebris(Rigidbody[] bodies)
        {
            debrisBodies = bodies ?? new Rigidbody[0];
            debrisInitialPositions = new Vector3[debrisBodies.Length];
            debrisInitialRotations = new Quaternion[debrisBodies.Length];
            for (int i = 0; i < debrisBodies.Length; i++)
            {
                if (debrisBodies[i] == null) continue;
                debrisInitialPositions[i] = debrisBodies[i].transform.localPosition;
                debrisInitialRotations[i] = debrisBodies[i].transform.localRotation;
            }
        }

        public void ConfigureLightBeats(VfxLightBeat[] beats)
        {
            lightBeats = beats ?? new VfxLightBeat[0];
        }

        private void Awake()
        {
            CaptureInitial();
            block = new MaterialPropertyBlock();
        }

        private void Update()
        {
            if (CurrentPhaseIndex < 0 || CurrentPhaseIndex >= phases.Length) return;
            PhaseEntry phase = phases[CurrentPhaseIndex];
            phaseTime += Time.deltaTime;

            if (phase.loop)
            {
                sustainElapsed += Time.deltaTime;
                if (sustainElapsed >= maxSustainSeconds)
                {
                    // Never end silently: raise onSustainTimeout, then advance to end.
                    Raise(OnSustainTimeout, onSustainTimeoutUnity, default);
                    AdvanceToPhase("end");
                    return;
                }
                float cycle = phase.duration > 0f ? phase.duration : 1f;
                PushMaterialTime((phaseTime / cycle) - Mathf.Floor(phaseTime / cycle));
                return;
            }

            float progress = phase.duration > 0f ? Mathf.Clamp01(phaseTime / phase.duration) : 0f;
            PushMaterialTime(progress);

            if (phase.autoAdvance && phase.duration >= 0f && phaseTime >= phase.duration)
                AdvanceToIndex(CurrentPhaseIndex + 1);

            if (CurrentPhaseIndex < 0 && !completed && !IsAlive)
                RaiseComplete();
        }

        // ------------------------------------------------------------ inbound

        /// <summary>
        /// Binary search over the compile-time sorted event id table; unknown
        /// ids return false (never throw). O(log n), zero allocation.
        /// </summary>
        public bool SendEvent(string eventId, in VfxEventPayload payload)
        {
            if (string.IsNullOrEmpty(eventId) || eventIds.Length == 0) return false;
            int lo = 0, hi = eventIds.Length - 1;
            while (lo <= hi)
            {
                int mid = (lo + hi) >> 1;
                int cmp = string.CompareOrdinal(eventIds[mid], eventId);
                if (cmp == 0) return HandleEvent(eventHandlers[mid], payload);
                if (cmp < 0) lo = mid + 1;
                else hi = mid - 1;
            }
            return false;
        }

        /// <summary>Handler indices are compile-time constants; extend per archetype.</summary>
        private bool HandleEvent(int handlerIndex, in VfxEventPayload payload)
        {
            switch (handlerIndex)
            {
                case 0: // launch
                    // Capture the pool-home TRS before the first externally driven
                    // move (Awake may not have run yet in EditMode contexts).
                    CaptureInitial();
                    transform.SetPositionAndRotation(payload.Position, payload.Rotation.Equals(default) ? transform.rotation : payload.Rotation);
                    Play();
                    Raise(OnLaunch, onLaunchUnity, payload);
                    return true;
                case 1: // travel / sustain advance
                    return AdvanceToPhase("travel") || AdvanceToPhase("sustain");
                case 2: // impact
                    transform.position = payload.Position;
                    ClearTrails();
                    bool ok = AdvanceToPhase("impact");
                    if (ok) Raise(OnImpact, onImpactUnity, payload);
                    return ok;
                case 3: // end
                    bool ended = AdvanceToPhase("end");
                    if (!ended) StopAll();
                    Raise(OnEnd, onEndUnity, payload);
                    return true;
                case 4: // break (debris release)
                    ReleaseDebris(payload.Position, Mathf.Max(payload.Value, 1f));
                    return true;
                case 5: // setProgress (externally driven phase)
                    PushMaterialTime(Mathf.Clamp01(payload.Value));
                    return true;
                case 6: // setTravelPose
                    CaptureInitial();
                    transform.SetPositionAndRotation(payload.Position, payload.Rotation.Equals(default) ? transform.rotation : payload.Rotation);
                    return true;
                default:
                    return false;
            }
        }

        // ------------------------------------------------------------ control

        public void Play()
        {
            completed = false;
            if (phases.Length == 0) return;
            AdvanceToIndex(0);
        }

        public void StopAll()
        {
            for (int i = 0; i < particleSystems.Length; i++)
                if (particleSystems[i] != null)
                    particleSystems[i].Stop(true, ParticleSystemStopBehavior.StopEmitting);
            CurrentPhaseIndex = -1;
            phaseTime = 0f;
            SetAllLayersActive(false);
        }

        /// <summary>The 9 mandated pool-reset steps (COMPILER_BOUNDARY_V2.md section 5.5).</summary>
        public void ResetForPool()
        {
            CaptureInitial();
            // 1. particles: stop + clear
            for (int i = 0; i < particleSystems.Length; i++)
            {
                if (particleSystems[i] == null) continue;
                particleSystems[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                particleSystems[i].Clear(true);
            }
            // 2. trails / lines
            ClearTrails();
            for (int i = 0; i < lineRenderers.Length; i++)
                if (lineRenderers[i] != null) lineRenderers[i].positionCount = 0;
            // 3. VisualEffect.Reinit is handled by the graph binder layer when present
            //    (this package has no hard VFX-graph dependency; the compiler adds a
            //    VisualEffect reset relay when the recipe uses gpu_particles).
            // 4. debris back to kinematic + initial local TRS (baked arrays, no GetChild walk)
            for (int i = 0; i < debrisBodies.Length; i++)
            {
                Rigidbody body = debrisBodies[i];
                if (body == null) continue;
                body.isKinematic = true;
                body.useGravity = false;
                if (i < debrisInitialPositions.Length)
                {
                    body.transform.localPosition = debrisInitialPositions[i];
                    body.transform.localRotation = debrisInitialRotations[i];
                }
            }
            // 5. cloth motion (via components, when present)
            var cloths = GetComponentsInChildren<Cloth>(true);
            for (int i = 0; i < cloths.Length; i++) cloths[i].ClearTransformMotion();
            // 6. material time state to zero
            PushMaterialReset();
            // 7. light beats to base
            for (int i = 0; i < lightBeats.Length; i++)
                if (lightBeats[i] != null) lightBeats[i].ResetForPool();
            // 8. phase machine reset
            CurrentPhaseIndex = -1;
            phaseTime = 0f;
            sustainElapsed = 0f;
            completed = false;
            // 9. root local TRS (local, not world: the pool may reparent us)
            transform.localPosition = initialLocalPosition;
            transform.localRotation = initialLocalRotation;
            transform.localScale = initialLocalScale;
            SetAllLayersActive(false);
        }

        // ------------------------------------------------------------ internals

        private void CaptureInitial()
        {
            if (initialCaptured) return;
            initialLocalPosition = transform.localPosition;
            initialLocalRotation = transform.localRotation;
            initialLocalScale = transform.localScale;
            initialCaptured = true;
        }

        private bool AdvanceToPhase(string id)
        {
            for (int i = 0; i < phases.Length; i++)
            {
                if (!string.Equals(phases[i].id, id, StringComparison.Ordinal)) continue;
                AdvanceToIndex(i);
                return true;
            }
            return false;
        }

        private void AdvanceToIndex(int index)
        {
            if (index >= phases.Length)
            {
                CurrentPhaseIndex = -1;
                phaseTime = 0f;
                SetAllLayersActive(false);
                if (!completed && !IsAlive) RaiseComplete();
                return;
            }
            CurrentPhaseIndex = index;
            phaseTime = 0f;
            sustainElapsed = 0f;
            ApplyPhaseActivation(phases[index]);
            PushMaterialPhase(index);
        }

        private void ApplyPhaseActivation(PhaseEntry phase)
        {
            // Layers can span phases: activation is per-phase index sets, not containers.
            for (int i = 0; i < layerNodes.Length; i++)
            {
                if (layerNodes[i] == null) continue;
                bool active = false;
                for (int k = 0; k < phase.activeLayers.Length; k++)
                    if (phase.activeLayers[k] == i) { active = true; break; }
                if (layerNodes[i].activeSelf != active) layerNodes[i].SetActive(active);
            }
            for (int i = 0; i < particleSystems.Length; i++)
            {
                ParticleSystem ps = particleSystems[i];
                if (ps == null) continue;
                if (ps.gameObject.activeInHierarchy && !ps.isPlaying) ps.Play(true);
            }
        }

        private void SetAllLayersActive(bool active)
        {
            for (int i = 0; i < layerNodes.Length; i++)
                if (layerNodes[i] != null && layerNodes[i].activeSelf != active)
                    layerNodes[i].SetActive(active);
        }

        private void ClearTrails()
        {
            for (int i = 0; i < trailRenderers.Length; i++)
                if (trailRenderers[i] != null) trailRenderers[i].Clear();
        }

        private void ReleaseDebris(Vector3 impactPoint, float force)
        {
            for (int i = 0; i < debrisBodies.Length; i++)
            {
                Rigidbody body = debrisBodies[i];
                if (body == null) continue;
                body.isKinematic = false;
                body.useGravity = true;
                body.AddExplosionForce(force, impactPoint, 5f, 0.4f, ForceMode.Impulse);
            }
        }

        private void PushMaterialTime(float progress)
        {
            if (block == null) block = new MaterialPropertyBlock();
            for (int i = 0; i < layerRenderers.Length; i++)
            {
                Renderer r = layerRenderers[i];
                if (r == null) continue;
                r.GetPropertyBlock(block);
                block.SetFloat(ProgressId, progress);
                block.SetFloat(LocalTimeId, phaseTime);
                r.SetPropertyBlock(block);
            }
            for (int i = 0; i < lightBeats.Length; i++)
                if (lightBeats[i] != null) lightBeats[i].SetLocalTime(phaseTime);
        }

        private void PushMaterialPhase(int phaseIndex)
        {
            if (block == null) block = new MaterialPropertyBlock();
            for (int i = 0; i < layerRenderers.Length; i++)
            {
                Renderer r = layerRenderers[i];
                if (r == null) continue;
                r.GetPropertyBlock(block);
                block.SetFloat(PhaseId, phaseIndex);
                r.SetPropertyBlock(block);
            }
        }

        private void PushMaterialReset()
        {
            if (block == null) block = new MaterialPropertyBlock();
            for (int i = 0; i < layerRenderers.Length; i++)
            {
                Renderer r = layerRenderers[i];
                if (r == null) continue;
                r.GetPropertyBlock(block);
                block.SetFloat(ProgressId, 0f);
                block.SetFloat(LocalTimeId, 0f);
                block.SetFloat(PhaseId, 0f);
                r.SetPropertyBlock(block);
            }
        }

        private void RaiseComplete()
        {
            completed = true;
            Raise(OnComplete, onCompleteUnity, default);
        }

        private static void Raise(Action<VfxEventPayload> csharpEvent, UnityEvent<VfxEventPayload> unityEvent, VfxEventPayload payload)
        {
            csharpEvent?.Invoke(payload);
            unityEvent?.Invoke(payload);
        }
    }
}

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
        private static readonly int[] HitDataIds =
        {
            Shader.PropertyToID("_HitData0"),
            Shader.PropertyToID("_HitData1"),
            Shader.PropertyToID("_HitData2"),
            Shader.PropertyToID("_HitData3")
        };
        private const float HitSlotIdle = -1000f;

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
        [SerializeField] private Rigidbody2D[] debrisBodies2D = new Rigidbody2D[0];
        [SerializeField] private Vector3[] debrisInitialPositions = new Vector3[0];
        [SerializeField] private Quaternion[] debrisInitialRotations = new Quaternion[0];
        [SerializeField] private Vector3[] debris2DInitialPositions = new Vector3[0];
        [SerializeField] private Quaternion[] debris2DInitialRotations = new Quaternion[0];
        [SerializeField] private VfxLightBeat[] lightBeats = new VfxLightBeat[0];

        [Header("Inbound event table (compile-time, sorted)")]
        [SerializeField] private string[] eventIds = new string[0];
        [SerializeField] private int[] eventHandlers = new int[0]; // index into HandleEvent switch

        [Header("Node sequence (compile-time; chain-style archetypes)")]
        [SerializeField] private Vector3[] nodePositions = new Vector3[0];
        [SerializeField, Min(0.01f)] private float hopInterval = 0.12f;
        [SerializeField] private Transform[] hopFollowers = new Transform[0];
        [SerializeField] private ParticleSystem[] hopBurstSystems = new ParticleSystem[0];
        // Segment objects revealed one per hop ("appears hop by hop"). They are
        // sequencer-owned: phase activation never touches them, reset/stop do.
        [SerializeField] private GameObject[] hopRevealNodes = new GameObject[0];
        [SerializeField] private string hopPhaseId = "travel";

        [Header("Outbound events")]
        [SerializeField] private UnityEvent<VfxEventPayload> onLaunchUnity;
        [SerializeField] private UnityEvent<VfxEventPayload> onImpactUnity;
        [SerializeField] private UnityEvent<VfxEventPayload> onEndUnity;
        [SerializeField] private UnityEvent<VfxEventPayload> onCompleteUnity;
        [SerializeField] private UnityEvent<VfxEventPayload> onSustainTimeoutUnity;
        [SerializeField] private UnityEvent<VfxEventPayload> onHopUnity;
        [SerializeField] private UnityEvent<VfxEventPayload> onHitRippleUnity;
        [SerializeField] private UnityEvent<VfxEventPayload> onBreakUnity;

        public event Action<VfxEventPayload> OnLaunch;
        public event Action<VfxEventPayload> OnImpact;
        public event Action<VfxEventPayload> OnEnd;
        public event Action<VfxEventPayload> OnComplete;
        public event Action<VfxEventPayload> OnSustainTimeout;
        public event Action<VfxEventPayload> OnHop;
        public event Action<VfxEventPayload> OnHitRipple;
        public event Action<VfxEventPayload> OnBreak;

        private MaterialPropertyBlock block;
        private float phaseTime;
        private float sustainElapsed;
        private Vector3 initialLocalPosition;
        private Quaternion initialLocalRotation;
        private Vector3 initialLocalScale;
        private bool initialCaptured;
        private bool completed;
        private int nextHopIndex;
        private float integrity = 1f;
        private int nextHitSlot;
        private readonly Vector4[] hitSlots =
        {
            new Vector4(0f, 0f, 0f, HitSlotIdle),
            new Vector4(0f, 0f, 0f, HitSlotIdle),
            new Vector4(0f, 0f, 0f, HitSlotIdle),
            new Vector4(0f, 0f, 0f, HitSlotIdle)
        };

        private static readonly int IntegrityId = Shader.PropertyToID("_Integrity");

        /// <summary>Externally driven structural progress (shield cracks etc.), 1 = intact.</summary>
        public float Integrity { get { return integrity; } }

        public int NextHopIndex { get { return nextHopIndex; } }
        public Vector3[] NodePositions { get { return nodePositions; } }

        /// <summary>-1 = not playing. int, not an enum: the phase set is data.</summary>
        public int CurrentPhaseIndex { get; private set; } = -1;

        public float PhaseTime { get { return phaseTime; } }

        public string CurrentPhaseId
        {
            get { return CurrentPhaseIndex >= 0 && CurrentPhaseIndex < phases.Length ? phases[CurrentPhaseIndex].id : null; }
        }

        public PhaseEntry[] Phases { get { return phases; } }

        /// <summary>Compile-time layer registry (read by the cost model's per-phase overdraw estimate).</summary>
        public GameObject[] LayerNodes { get { return layerNodes; } }

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
                for (int i = 0; i < debrisBodies2D.Length; i++)
                    if (debrisBodies2D[i] != null && debrisBodies2D[i].bodyType == RigidbodyType2D.Dynamic && !debrisBodies2D[i].IsSleeping()) return true;
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

        public void ConfigureDebris2D(Rigidbody2D[] bodies)
        {
            debrisBodies2D = bodies ?? new Rigidbody2D[0];
            debris2DInitialPositions = new Vector3[debrisBodies2D.Length];
            debris2DInitialRotations = new Quaternion[debrisBodies2D.Length];
            for (int i = 0; i < debrisBodies2D.Length; i++)
            {
                if (debrisBodies2D[i] == null) continue;
                debris2DInitialPositions[i] = debrisBodies2D[i].transform.localPosition;
                debris2DInitialRotations[i] = debrisBodies2D[i].transform.localRotation;
            }
        }

        public void ConfigureParticles(ParticleSystem[] systems)
        {
            particleSystems = systems ?? new ParticleSystem[0];
        }

        public void ConfigureTrails(TrailRenderer[] trails, LineRenderer[] lines)
        {
            trailRenderers = trails ?? new TrailRenderer[0];
            lineRenderers = lines ?? new LineRenderer[0];
        }

        public void ConfigureLightBeats(VfxLightBeat[] beats)
        {
            lightBeats = beats ?? new VfxLightBeat[0];
        }

        /// <summary>
        /// Compile-time node-sequence wiring (chain-style archetypes): node
        /// world-local positions, the hop cadence, the transforms that jump to
        /// the latest node (light + flash quad) and the per-hop burst systems.
        /// </summary>
        public void ConfigureNodes(Vector3[] positions, float interval, Transform[] followers,
            ParticleSystem[] burstSystems, GameObject[] revealNodes = null, string phaseId = "travel")
        {
            nodePositions = positions ?? new Vector3[0];
            hopInterval = Mathf.Max(interval, 0.01f);
            hopFollowers = followers ?? new Transform[0];
            hopBurstSystems = burstSystems ?? new ParticleSystem[0];
            hopRevealNodes = revealNodes ?? new GameObject[0];
            hopPhaseId = string.IsNullOrEmpty(phaseId) ? "travel" : phaseId;
        }

        /// <summary>Runtime node override (the setNodes inbound interface).</summary>
        public void SetNodes(Vector3[] positions)
        {
            nodePositions = positions ?? new Vector3[0];
            nextHopIndex = 0;
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
            AdvanceHops(phase);

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
                    AdvanceToPhase("break");
                    Raise(OnBreak, onBreakUnity, payload);
                    return true;
                case 5: // setProgress (externally driven phase)
                    PushMaterialTime(Mathf.Clamp01(payload.Value));
                    return true;
                case 6: // setTravelPose
                    CaptureInitial();
                    transform.SetPositionAndRotation(payload.Position, payload.Rotation.Equals(default) ? transform.rotation : payload.Rotation);
                    return true;
                case 7: // hitAt (shield ripple; local-space point, strength in Value)
                    RegisterHit(payload.Position, Mathf.Max(payload.Value, 0.01f));
                    Raise(OnHitRipple, onHitRippleUnity, payload);
                    return true;
                case 8: // setIntegrity (externally driven crack progress)
                    integrity = Mathf.Clamp01(payload.Value);
                    PushMaterialFloat(IntegrityId, integrity);
                    return true;
                case 9: // addNode (append one hop target)
                {
                    var extended = new Vector3[nodePositions.Length + 1];
                    Array.Copy(nodePositions, extended, nodePositions.Length);
                    extended[nodePositions.Length] = payload.Position;
                    nodePositions = extended;
                    return true;
                }
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
            for (int i = 0; i < debrisBodies2D.Length; i++)
            {
                Rigidbody2D body = debrisBodies2D[i];
                if (body == null) continue;
                body.bodyType = RigidbodyType2D.Kinematic;
                body.velocity = Vector2.zero;
                body.angularVelocity = 0f;
                if (i < debris2DInitialPositions.Length)
                {
                    body.transform.localPosition = debris2DInitialPositions[i];
                    body.transform.localRotation = debris2DInitialRotations[i];
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
            // 8. phase machine reset (incl. archetype-specific state: hops, hits, integrity)
            CurrentPhaseIndex = -1;
            phaseTime = 0f;
            sustainElapsed = 0f;
            completed = false;
            nextHopIndex = 0;
            integrity = 1f;
            nextHitSlot = 0;
            for (int i = 0; i < hitSlots.Length; i++) hitSlots[i] = new Vector4(0f, 0f, 0f, HitSlotIdle);
            for (int i = 0; i < hopRevealNodes.Length; i++)
                if (hopRevealNodes[i] != null && hopRevealNodes[i].activeSelf) hopRevealNodes[i].SetActive(false);
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
            if (!active)
                for (int i = 0; i < hopRevealNodes.Length; i++)
                    if (hopRevealNodes[i] != null && hopRevealNodes[i].activeSelf)
                        hopRevealNodes[i].SetActive(false);
        }

        private void ClearTrails()
        {
            for (int i = 0; i < trailRenderers.Length; i++)
                if (trailRenderers[i] != null) trailRenderers[i].Clear();
        }

        /// <summary>
        /// Hop sequencer (chain-style archetypes): during the configured phase,
        /// each hopInterval advances to the next node — moves the followers
        /// (latest-node light + node flash), fires the per-hop bursts and
        /// raises onHop(index, position).
        /// </summary>
        private void AdvanceHops(PhaseEntry phase)
        {
            if (nodePositions.Length == 0 || nextHopIndex >= nodePositions.Length) return;
            if (!string.Equals(phase.id, hopPhaseId, StringComparison.Ordinal)) return;
            while (nextHopIndex < nodePositions.Length && phaseTime >= nextHopIndex * hopInterval)
            {
                Vector3 local = nodePositions[nextHopIndex];
                if (nextHopIndex < hopRevealNodes.Length && hopRevealNodes[nextHopIndex] != null)
                    hopRevealNodes[nextHopIndex].SetActive(true);
                for (int i = 0; i < hopFollowers.Length; i++)
                    if (hopFollowers[i] != null) hopFollowers[i].localPosition = local;
                for (int i = 0; i < hopBurstSystems.Length; i++)
                {
                    ParticleSystem ps = hopBurstSystems[i];
                    if (ps == null) continue;
                    ps.transform.localPosition = local;
                    ps.Emit(Mathf.Max(1, ps.main.maxParticles / Mathf.Max(nodePositions.Length, 1)));
                }
                var payload = new VfxEventPayload { Position = transform.TransformPoint(local), Index = nextHopIndex };
                Raise(OnHop, onHopUnity, payload);
                nextHopIndex++;
            }
        }

        /// <summary>Registers a hit ripple in the 4-slot ring (xyz = local point, w = start time).</summary>
        private void RegisterHit(Vector3 localPoint, float strength)
        {
            hitSlots[nextHitSlot] = new Vector4(localPoint.x, localPoint.y, localPoint.z, phaseTime);
            nextHitSlot = (nextHitSlot + 1) % hitSlots.Length;
            if (block == null) block = new MaterialPropertyBlock();
            for (int i = 0; i < layerRenderers.Length; i++)
            {
                Renderer r = layerRenderers[i];
                if (r == null) continue;
                r.GetPropertyBlock(block);
                for (int s = 0; s < hitSlots.Length; s++)
                    block.SetVector(HitDataIds[s], hitSlots[s]);
                r.SetPropertyBlock(block);
            }
        }

        private void PushMaterialFloat(int nameId, float value)
        {
            if (block == null) block = new MaterialPropertyBlock();
            for (int i = 0; i < layerRenderers.Length; i++)
            {
                Renderer r = layerRenderers[i];
                if (r == null) continue;
                r.GetPropertyBlock(block);
                block.SetFloat(nameId, value);
                r.SetPropertyBlock(block);
            }
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
            for (int i = 0; i < debrisBodies2D.Length; i++)
            {
                Rigidbody2D body = debrisBodies2D[i];
                if (body == null) continue;
                body.bodyType = RigidbodyType2D.Dynamic;
                Vector2 dir = (Vector2)(body.transform.position - impactPoint);
                if (dir.sqrMagnitude < 1e-6f) dir = Vector2.up;
                body.AddForce(dir.normalized * force, ForceMode2D.Impulse);
                body.AddTorque((i % 2 == 0 ? 1f : -1f) * force * 0.2f, ForceMode2D.Impulse);
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

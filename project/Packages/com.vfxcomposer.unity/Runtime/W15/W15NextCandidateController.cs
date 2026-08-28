using System;
using System.Collections.Generic;
using UnityEngine;

namespace VFXComposer.W15NextCandidate
{
    public enum W15NextArchetype { Decal, WeaponTrail, Destruction, LifeCycle, Portal, Loot }
    public enum W15NextVariant
    {
        ScorchDecal, FrostDecal, KatanaTrail, EnergyWhipTrail, CrateBreak, CrystalShatter,
        DeathDissolve, HeroEntrance, TwinPortal, LootBeam
    }
    public enum W15PortalPhase { Idle, EntryIntake, HiddenTransit, ExitDelay, ExitEjection, Settle }
    public enum W15LootGeometry { Circle, Diamond, Hexagon, Crown, Star }

    /// <summary>
    /// Dedicated runtime carrier for the W15 next candidate.  Each archetype drives a different
    /// piece of observable geometry; the enum never substitutes for the carrier itself.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class W15NextCandidateController : MonoBehaviour, IVfxRuntimeEntry
    {
        [Header("Identity")]
        [SerializeField] private W15NextArchetype archetype;
        [SerializeField] private W15NextVariant variant;
        [SerializeField, Min(.1f)] private float duration = 1.5f;
        [SerializeField] private uint seed = 1;
        [SerializeField] private Color primary = Color.white;
        [SerializeField] private Color secondary = Color.cyan;
        [SerializeField] private Color accent = Color.white;

        [Header("Owned runtime objects")]
        [SerializeField] private Renderer[] ownedRenderers = new Renderer[0];
        [SerializeField] private ParticleSystem[] particles = new ParticleSystem[0];

        [Header("Decal carrier")]
        [SerializeField] private Transform[] decalLayers = new Transform[0];
        [SerializeField, Min(.0005f)] private float surfaceBias = .006f;
        [SerializeField, Range(1, 16)] private int stackLimit = 4;

        [Header("Weapon trail carrier")]
        [SerializeField] private MeshFilter weaponRibbonFilter;
        [SerializeField] private MeshRenderer weaponRibbonRenderer;
        [SerializeField] private LineRenderer weaponEndpointLine;
        [SerializeField, Range(8, 16)] private int historyPoints = 12;
        [SerializeField, Min(0f)] private float speedThreshold = 1.5f;
        [SerializeField, Min(.01f)] private float fadeTime = .15f;

        [Header("Destruction carrier")]
        [SerializeField] private Transform destructionIntact;
        [SerializeField] private Renderer destructionIntactRenderer;
        [SerializeField] private MeshFilter destructionFragmentsFilter;
        [SerializeField] private MeshRenderer destructionFragmentsRenderer;
        [SerializeField] private ParticleSystem destructionDust;
        [SerializeField, Range(8, 12)] private int pieceCount = 10;
        [SerializeField, Min(.1f)] private float explodeForce = 2.6f;
        [SerializeField, Min(.1f)] private float debrisLifetime = 1.5f;

        [Header("LifeCycle carrier")]
        [SerializeField] private Renderer lifecycleEdgeRenderer;
        [SerializeField] private ParticleSystem lifecycleParticles;
        [SerializeField] private string lifecycleDirection = "up";
        [SerializeField] private bool inverseEntrance;

        [Header("Portal carrier")]
        [SerializeField] private Transform portalRing;
        [SerializeField] private Renderer portalRingRenderer;
        [SerializeField] private Transform portalInterior;
        [SerializeField] private Renderer portalInteriorRenderer;
        [SerializeField] private Transform portalEntryFunnel;
        [SerializeField] private Renderer portalEntryFunnelRenderer;
        [SerializeField] private Transform portalExitBurst;
        [SerializeField] private Renderer portalExitBurstRenderer;
        [SerializeField] private LineRenderer portalFlowLine;
        [SerializeField] private string pairId = "w15_next_pair";
        [SerializeField] private PortalEndpointRole portalRole;
        [SerializeField, Min(.2f)] private float portalRadius = 1f;
        [SerializeField, Min(.1f)] private float swirlSpeed = 2.8f;

        [Header("Loot carrier")]
        [SerializeField] private Transform lootBase;
        [SerializeField] private Renderer lootBaseRenderer;
        [SerializeField] private Transform lootBeam;
        [SerializeField] private Renderer lootBeamRenderer;
        [SerializeField] private MeshFilter lootCrownFilter;
        [SerializeField] private MeshRenderer lootCrownRenderer;
        [SerializeField] private ParticleSystem lootSparkles;
        [SerializeField] private LineRenderer lootPickupArc;
        [SerializeField, Range(1, 5)] private int rarity = 3;
        [SerializeField, Min(.1f)] private float pickupSpeed = 4.8f;
        [SerializeField, Min(.5f)] private float beamHeight = 2.4f;

        private const float TailDuration = .22f;
        private const float DestructionHold = .16f;
        private const float PortalIntakeEnd = .20f;
        private const float PortalExitStart = .35f;
        private const float PortalEjectionEnd = .55f;

        private readonly List<BladeSample> weaponSamples = new List<BladeSample>(16);
        private static readonly Dictionary<string, List<W15NextCandidateController>> DecalStacks = new Dictionary<string, List<W15NextCandidateController>>(StringComparer.Ordinal);
        private Vector3[] basePositions = new Vector3[0];
        private Quaternion[] baseRotations = new Quaternion[0];
        private Vector3[] baseScales = new Vector3[0];
        private Transform[] cachedTransforms = new Transform[0];
        private Renderer[] externalRenderers = new Renderer[0];
        private MaterialPropertyBlock[] externalOriginalBlocks = new MaterialPropertyBlock[0];
        private readonly Vector3[] destructionPositions = new Vector3[12];
        private MaterialPropertyBlock block;
        private Mesh weaponMesh;
        private Mesh destructionMesh;
        private Mesh lootCrownMesh;
        private float elapsed;
        private float stopElapsed;
        private bool playing;
        private bool stopping;
        private bool dustEmitted;
        private bool pickingUp;
        private bool portalTraversing;
        private string decalSurfaceKey;
        private Vector3 surfaceNormal = Vector3.forward;
        private Vector3 surfaceTangent = Vector3.up;
        private Vector3 lastWeaponRoot;
        private Vector3 lastWeaponTip;
        private bool hasWeaponSample;
        private float weaponOpacity;
        private float lastWeaponSpeed;
        private Vector3 destructionImpulse;
        private Vector3 pickupTarget;
        private Vector3 pickupStart;
        private Vector3 pickupStartRoot;
        private float pickupProgress;
        private float pickupArcHeight;
        private Vector3 lootBaseLocalPosition;
        private int playCount;
        private W15PortalPhase portalPhase;
        private float lifecycleProgress;

        public bool IsAlive { get { return playing || stopping; } }
        public W15NextArchetype Archetype { get { return archetype; } }
        public W15NextVariant Variant { get { return variant; } }
        public float NormalizedTime { get { return Mathf.Clamp01(elapsed / Mathf.Max(.1f, duration)); } }
        public int PlayCount { get { return playCount; } }
        public int OwnedRendererCount { get { return ownedRenderers == null ? 0 : ownedRenderers.Length; } }
        public int ParticleCapacity
        {
            get
            {
                var total = 0;
                if (particles != null) for (var index = 0; index < particles.Length; index++) if (particles[index] != null) total += particles[index].main.maxParticles;
                return total;
            }
        }

        public Vector3 SurfaceNormal { get { return surfaceNormal; } }
        public Vector3 SurfaceTangent { get { return surfaceTangent; } }
        public float SurfaceBias { get { return surfaceBias; } }
        public int WeaponSampleCount { get { return weaponSamples.Count; } }
        public float WeaponOpacity { get { return weaponOpacity; } }
        public float LastWeaponSpeed { get { return lastWeaponSpeed; } }
        public int ActiveDestructionPieceCount { get { return playing && elapsed >= DestructionHold && archetype == W15NextArchetype.Destruction ? pieceCount : 0; } }
        public int PieceCount { get { return pieceCount; } }
        public int BoundRendererCount { get { return externalRenderers == null ? 0 : externalRenderers.Length; } }
        public float LifecycleProgress { get { return lifecycleProgress; } }
        public string PairId { get { return pairId; } }
        public PortalEndpointRole PortalRole { get { return portalRole; } }
        public W15PortalPhase PortalPhase { get { return portalPhase; } }
        public float PortalFlowDirection { get { return portalRole == PortalEndpointRole.Entry ? -1f : 1f; } }
        public int Rarity { get { return rarity; } }
        public W15LootGeometry LootGeometry { get { return (W15LootGeometry)(rarity - 1); } }
        public int LootLayerCount { get { return rarity; } }
        public float LootCadenceHz { get { return 1.1f + rarity * .55f; } }
        public float LootPeakScale { get { return 1f + rarity * .12f; } }
        public float LootBeamHeight { get { return beamHeight * (.58f + rarity * .105f); } }
        public float LootBeamWidth { get { return .08f + rarity * .035f; } }
        public float LootSparkleRate { get { return 2f + rarity * rarity * 1.1f; } }
        public float PickupProgress { get { return pickupProgress; } }
        public float PickupArcHeight { get { return pickupArcHeight; } }
        public Vector3 PickupTravelPosition { get { return lootBase == null ? transform.position : lootBase.position; } }

        private MaterialPropertyBlock Block { get { if (block == null) block = new MaterialPropertyBlock(); return block; } }

        private void Awake()
        {
            CaptureBaseState();
            EnsureRuntimeMeshes();
            ResetForPool();
        }

        private void OnDestroy()
        {
            RestoreExternalRenderers();
            RemoveFromDecalStack();
            DestroyRuntimeMesh(weaponMesh);
            DestroyRuntimeMesh(destructionMesh);
            DestroyRuntimeMesh(lootCrownMesh);
        }

        private static void DestroyRuntimeMesh(Mesh mesh)
        {
            if (mesh == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(mesh); else UnityEngine.Object.DestroyImmediate(mesh);
        }

        private void Update()
        {
            var delta = Mathf.Max(0f, Time.deltaTime);
            if (stopping)
            {
                stopElapsed += delta;
                ApplyGlobalAlpha(1f - Mathf.Clamp01(stopElapsed / TailDuration));
                if (stopElapsed >= TailDuration) CompleteReset();
                return;
            }
            if (!playing) return;
            elapsed += delta;
            switch (archetype)
            {
                case W15NextArchetype.WeaponTrail: UpdateWeapon(delta); break;
                case W15NextArchetype.Destruction: UpdateDestruction(); break;
                case W15NextArchetype.LifeCycle: UpdateLifeCycle(); break;
                case W15NextArchetype.Portal: UpdatePortal(); break;
                case W15NextArchetype.Loot: UpdateLoot(delta); break;
                default: UpdateDecal(); break;
            }
            if ((archetype == W15NextArchetype.Decal || archetype == W15NextArchetype.Destruction || archetype == W15NextArchetype.LifeCycle) && elapsed >= duration) Stop(VfxStopMode.AllowTail);
        }

        public void Initialize(VfxRuntimeContext context)
        {
            ResetForPool();
            transform.SetPositionAndRotation(context.Position, context.Rotation);
        }

        public void Play()
        {
            EnsureRuntimeMeshes();
            if (cachedTransforms.Length == 0) CaptureBaseState();
            playing = true;
            stopping = false;
            elapsed = 0f;
            stopElapsed = 0f;
            dustEmitted = false;
            playCount++;
            ApplyGlobalAlpha(1f);
            switch (archetype)
            {
                case W15NextArchetype.Decal: SetDecalVisible(true); PlayParticles(); break;
                case W15NextArchetype.WeaponTrail: SetWeaponVisible(true, false); break;
                case W15NextArchetype.Destruction: BeginDestructionVisuals(); break;
                case W15NextArchetype.LifeCycle: BeginLifeCycleVisuals(); break;
                case W15NextArchetype.Portal: BeginPortalVisuals(); break;
                case W15NextArchetype.Loot: BeginLootVisuals(); break;
            }
        }

        public bool SendEvent(string eventId, VfxRuntimeEvent payload)
        {
            if (eventId == "play" || eventId == "start") { transform.SetPositionAndRotation(payload.Position, payload.Rotation); Play(); return true; }
            if (eventId == "break") { TriggerDestruction(payload.Position); return archetype == W15NextArchetype.Destruction; }
            if (eventId == "traverse") { TriggerTraverse(); return archetype == W15NextArchetype.Portal; }
            if (eventId == "pickup") { SetPickupTarget(payload.Position); BeginPickup(); return archetype == W15NextArchetype.Loot; }
            if (eventId == "stop") { Stop(VfxStopMode.AllowTail); return true; }
            if (eventId == "reset") { ResetForPool(); return true; }
            return false;
        }

        public void Stop(VfxStopMode mode)
        {
            if (mode == VfxStopMode.Immediate) { CompleteReset(); return; }
            if (!playing && !stopping) return;
            playing = false;
            stopping = true;
            stopElapsed = 0f;
            StopParticles(false);
        }

        public void ResetForPool()
        {
            CompleteReset();
        }

        private void CompleteReset()
        {
            RestoreExternalRenderers();
            RemoveFromDecalStack();
            playing = false;
            stopping = false;
            pickingUp = false;
            portalTraversing = false;
            elapsed = 0f;
            stopElapsed = 0f;
            dustEmitted = false;
            pickupProgress = 0f;
            pickupArcHeight = 0f;
            pickupStart = Vector3.zero;
            pickupStartRoot = Vector3.zero;
            pickupTarget = Vector3.zero;
            lifecycleProgress = 0f;
            portalPhase = W15PortalPhase.Idle;
            weaponOpacity = 0f;
            lastWeaponSpeed = 0f;
            hasWeaponSample = false;
            weaponSamples.Clear();
            destructionImpulse = Vector3.zero;
            Array.Clear(destructionPositions, 0, destructionPositions.Length);
            decalSurfaceKey = null;
            StopParticles(true);
            RestoreBaseState();
            SetAllOwnedVisible(false);
            ClearLine(weaponEndpointLine);
            ClearLine(portalFlowLine);
            ClearLine(lootPickupArc);
            ClearMesh(weaponMesh);
            ClearMesh(destructionMesh);
            ClearMesh(lootCrownMesh);
            if (lootBase != null) lootBase.localPosition = lootBaseLocalPosition;
        }

        public void AttachToSurface(string surfaceKey, Vector3 point, Vector3 normal, Vector3 tangent)
        {
            if (archetype != W15NextArchetype.Decal) return;
            if (normal.sqrMagnitude < .0001f) normal = Vector3.forward;
            normal.Normalize();
            tangent -= normal * Vector3.Dot(tangent, normal);
            if (tangent.sqrMagnitude < .0001f) tangent = Mathf.Abs(Vector3.Dot(normal, Vector3.up)) > .98f ? Vector3.forward : Vector3.up;
            tangent.Normalize();
            surfaceNormal = normal;
            surfaceTangent = tangent;
            transform.SetPositionAndRotation(point + normal * surfaceBias, Quaternion.LookRotation(normal, tangent));
            RemoveFromDecalStack();
            decalSurfaceKey = string.IsNullOrEmpty(surfaceKey) ? "default" : surfaceKey;
            List<W15NextCandidateController> stack;
            if (!DecalStacks.TryGetValue(decalSurfaceKey, out stack)) { stack = new List<W15NextCandidateController>(); DecalStacks.Add(decalSurfaceKey, stack); }
            stack.RemoveAll(value => value == null || !value.IsAlive);
            while (stack.Count >= Mathf.Max(1, stackLimit))
            {
                var oldest = stack[0];
                stack.RemoveAt(0);
                if (oldest != null) oldest.Stop(VfxStopMode.Immediate);
            }
            stack.Add(this);
            Play();
        }

        private void RemoveFromDecalStack()
        {
            if (string.IsNullOrEmpty(decalSurfaceKey)) return;
            List<W15NextCandidateController> stack;
            if (DecalStacks.TryGetValue(decalSurfaceKey, out stack))
            {
                stack.Remove(this);
                stack.RemoveAll(value => value == null);
                if (stack.Count == 0) DecalStacks.Remove(decalSurfaceKey);
            }
            decalSurfaceKey = null;
        }

        public void DriveWeaponEndpoints(Vector3 bladeRoot, Vector3 bladeTip, float deltaTime)
        {
            if (archetype != W15NextArchetype.WeaponTrail) return;
            deltaTime = Mathf.Max(.0001f, deltaTime);
            if (!playing) Play();
            var rootDelta = hasWeaponSample ? Vector3.Distance(lastWeaponRoot, bladeRoot) : 0f;
            var tipDelta = hasWeaponSample ? Vector3.Distance(lastWeaponTip, bladeTip) : 0f;
            lastWeaponSpeed = Mathf.Max(rootDelta, tipDelta) / deltaTime;
            if (hasWeaponSample && Mathf.Max(rootDelta, tipDelta) > 1.5f) weaponSamples.Clear();
            lastWeaponRoot = bladeRoot;
            lastWeaponTip = bladeTip;
            hasWeaponSample = true;
            var target = lastWeaponSpeed >= speedThreshold ? 1f : 0f;
            weaponOpacity = Mathf.MoveTowards(weaponOpacity, target, deltaTime / Mathf.Max(.01f, fadeTime));
            if (target > 0f)
            {
                weaponSamples.Add(new BladeSample(transform.InverseTransformPoint(bladeRoot), transform.InverseTransformPoint(bladeTip), 0f));
                while (weaponSamples.Count > Mathf.Clamp(historyPoints, 8, 16)) weaponSamples.RemoveAt(0);
            }
            else
            {
                for (var index = weaponSamples.Count - 1; index >= 0; index--)
                {
                    var sample = weaponSamples[index]; sample.Age += deltaTime; weaponSamples[index] = sample;
                    if (sample.Age >= fadeTime) weaponSamples.RemoveAt(index);
                }
            }
            if (weaponEndpointLine != null)
            {
                weaponEndpointLine.useWorldSpace = false;
                weaponEndpointLine.positionCount = 2;
                weaponEndpointLine.SetPosition(0, transform.InverseTransformPoint(bladeRoot));
                weaponEndpointLine.SetPosition(1, transform.InverseTransformPoint(bladeTip));
                weaponEndpointLine.enabled = true;
            }
            RebuildWeaponRibbon();
            SetWeaponVisible(true, weaponOpacity > .01f && weaponSamples.Count >= 2);
            ApplyRendererProperties(weaponRibbonRenderer, weaponOpacity, elapsed, 1f);
        }

        public void TriggerDestruction(Vector3 impulse)
        {
            if (archetype != W15NextArchetype.Destruction) return;
            destructionImpulse = impulse;
            Play();
        }

        public Vector3 GetDeterministicPiecePosition(int index, float pieceAge)
        {
            if (index < 0 || index >= pieceCount) throw new ArgumentOutOfRangeException("index");
            var velocity = InitialPieceVelocity(index);
            var height = .08f + (index % 3) * .025f;
            var bounce = EvaluateBouncedHeight(height, velocity.y, Mathf.Max(0f, pieceAge), out _);
            var damping = 1f / (1f + Mathf.Max(0f, pieceAge) * .18f);
            return new Vector3(velocity.x * pieceAge * damping, bounce, velocity.z * pieceAge * damping);
        }

        public int GetDeterministicPieceBounceCount(int index, float pieceAge)
        {
            if (index < 0 || index >= pieceCount) throw new ArgumentOutOfRangeException("index");
            var ignored = EvaluateBouncedHeight(.08f + (index % 3) * .025f, InitialPieceVelocity(index).y, Mathf.Max(0f, pieceAge), out var bounceCount);
            return bounceCount;
        }

        public Vector3 GetCurrentPiecePosition(int index)
        {
            if (index < 0 || index >= pieceCount) throw new ArgumentOutOfRangeException("index");
            return destructionPositions[index];
        }

        public void BindCharacterRenderers(Renderer[] targets)
        {
            RestoreExternalRenderers();
            externalRenderers = targets == null ? new Renderer[0] : (Renderer[])targets.Clone();
            externalOriginalBlocks = new MaterialPropertyBlock[externalRenderers.Length];
            for (var index = 0; index < externalRenderers.Length; index++) if (externalRenderers[index] != null)
            {
                var original = new MaterialPropertyBlock();
                externalRenderers[index].GetPropertyBlock(original);
                externalOriginalBlocks[index] = original;
            }
            ApplyExternalDissolve(0f);
        }

        public void ConfigurePortal(string id, PortalEndpointRole role)
        {
            if (archetype != W15NextArchetype.Portal) return;
            pairId = string.IsNullOrEmpty(id) ? "w15_next_pair" : id;
            portalRole = role;
            portalPhase = W15PortalPhase.Idle;
        }

        public void TriggerTraverse()
        {
            if (archetype != W15NextArchetype.Portal) return;
            portalTraversing = true;
            Play();
        }

        public void ConfigureRarity(int value)
        {
            if (archetype != W15NextArchetype.Loot) return;
            rarity = Mathf.Clamp(value, 1, 5);
            var colors = new[]
            {
                new Color(.92f, .95f, 1f), new Color(.24f, 1f, .42f), new Color(.18f, .54f, 1f),
                new Color(.72f, .24f, 1f), new Color(1f, .48f, .05f)
            };
            primary = colors[rarity - 1];
            secondary = Color.Lerp(primary, Color.white, .42f);
            accent = rarity == 5 ? new Color(1f, .9f, .35f) : Color.white;
            RebuildLootGeometry();
            ConfigureLootParticles();
            ApplyLootDimensions(0f);
        }

        public void SetPickupTarget(Vector3 target) { pickupTarget = target; }

        public void BeginPickup()
        {
            if (archetype != W15NextArchetype.Loot) return;
            if (!playing) Play();
            pickingUp = true;
            pickupProgress = 0f;
            pickupStartRoot = transform.position;
            pickupStart = lootBase == null ? transform.position : lootBase.position;
            pickupArcHeight = .28f + rarity * .09f;
            if (lootPickupArc != null)
            {
                lootPickupArc.useWorldSpace = true;
                lootPickupArc.positionCount = 17;
                for (var index = 0; index < 17; index++) lootPickupArc.SetPosition(index, EvaluatePickupArc(index / 16f));
                lootPickupArc.enabled = true;
            }
        }

        private void UpdateDecal()
        {
            var phase = NormalizedTime;
            var reveal = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, .24f, phase));
            var fade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.72f, 1f, phase));
            for (var index = 0; index < decalLayers.Length; index++)
            {
                var layer = decalLayers[index]; if (layer == null) continue;
                var baseScale = index < baseScales.Length ? BaseScale(layer) : Vector3.one;
                var growth = variant == W15NextVariant.FrostDecal ? Mathf.Clamp01(reveal * 1.18f - index * .08f) : reveal;
                layer.localScale = baseScale * Mathf.Max(.02f, growth);
                ApplyRendererProperties(layer.GetComponent<Renderer>(), fade, phase + index * .11f, 1f + index * .18f);
            }
        }

        private void UpdateWeapon(float delta)
        {
            if (!hasWeaponSample) weaponOpacity = Mathf.MoveTowards(weaponOpacity, 0f, delta / Mathf.Max(.01f, fadeTime));
            ApplyRendererProperties(weaponRibbonRenderer, weaponOpacity, elapsed, 1.3f);
        }

        private void UpdateDestruction()
        {
            if (elapsed < DestructionHold)
            {
                if (destructionIntact != null) destructionIntact.localScale = BaseScale(destructionIntact) * (1f + .08f * Mathf.Sin(elapsed / DestructionHold * Mathf.PI));
                ApplyRendererProperties(destructionIntactRenderer, 1f, elapsed / DestructionHold, 1.2f);
                return;
            }
            if (destructionIntactRenderer != null) destructionIntactRenderer.enabled = false;
            if (destructionFragmentsRenderer != null) destructionFragmentsRenderer.enabled = true;
            var age = elapsed - DestructionHold;
            if (!dustEmitted)
            {
                dustEmitted = true;
                if (destructionDust != null) { SetParticleRendererEnabled(destructionDust, true); destructionDust.Play(true); destructionDust.Emit(variant == W15NextVariant.CrystalShatter ? 18 : 14); }
            }
            RebuildDestructionMesh(age);
            var fade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(debrisLifetime * .72f, debrisLifetime, age));
            ApplyRendererProperties(destructionFragmentsRenderer, fade, age / Mathf.Max(.1f, debrisLifetime), 1.25f);
        }

        private void UpdateLifeCycle()
        {
            var normalized = NormalizedTime;
            lifecycleProgress = inverseEntrance ? 1f - normalized : normalized;
            ApplyExternalDissolve(lifecycleProgress);
            var edge = Mathf.Sin(Mathf.PI * normalized);
            if (lifecycleEdgeRenderer != null)
            {
                lifecycleEdgeRenderer.enabled = true;
                lifecycleEdgeRenderer.transform.localScale = BaseScale(lifecycleEdgeRenderer.transform) * (.7f + edge * .55f);
                ApplyRendererProperties(lifecycleEdgeRenderer, edge, normalized, 1.4f);
            }
        }

        private void UpdatePortal()
        {
            if (!portalTraversing) { portalPhase = W15PortalPhase.Idle; return; }
            if (portalRole == PortalEndpointRole.Entry)
            {
                if (elapsed < PortalIntakeEnd)
                {
                    portalPhase = W15PortalPhase.EntryIntake;
                    var t = Mathf.SmoothStep(0f, 1f, elapsed / PortalIntakeEnd);
                    SetPortalRoleVisibility(true, false);
                    if (portalRing != null) portalRing.localScale = BaseScale(portalRing) * Mathf.Lerp(1.12f, .68f, t);
                    if (portalEntryFunnel != null) portalEntryFunnel.localScale = Vector3.Scale(BaseScale(portalEntryFunnel), new Vector3(Mathf.Lerp(1.2f, .22f, t), Mathf.Lerp(1.15f, .35f, t), 1f));
                    ApplyRendererProperties(portalRingRenderer, 1f, t, 1.25f);
                    ApplyRendererProperties(portalInteriorRenderer, 1f, t, 1.15f);
                    ApplyRendererProperties(portalEntryFunnelRenderer, 1f - t * .18f, t, 1.5f);
                }
                else
                {
                    portalPhase = W15PortalPhase.HiddenTransit;
                    var t = Mathf.Clamp01((elapsed - PortalIntakeEnd) / .28f);
                    SetPortalRoleVisibility(t < 1f, false);
                    ApplyRendererProperties(portalRingRenderer, 1f - t, t, 1f);
                    ApplyRendererProperties(portalInteriorRenderer, 1f - t, t, 1f);
                    ApplyRendererProperties(portalEntryFunnelRenderer, 1f - t, t, 1f);
                }
            }
            else
            {
                if (elapsed < PortalExitStart)
                {
                    portalPhase = W15PortalPhase.ExitDelay;
                    SetPortalRoleVisibility(false, false);
                }
                else if (elapsed < PortalEjectionEnd)
                {
                    portalPhase = W15PortalPhase.ExitEjection;
                    var t = Mathf.SmoothStep(0f, 1f, (elapsed - PortalExitStart) / (PortalEjectionEnd - PortalExitStart));
                    SetPortalRoleVisibility(false, true);
                    if (portalRing != null) portalRing.localScale = BaseScale(portalRing) * Mathf.Lerp(.55f, 1.18f, t);
                    if (portalExitBurst != null) portalExitBurst.localScale = BaseScale(portalExitBurst) * Mathf.Lerp(.25f, 1.45f, t);
                    ApplyRendererProperties(portalExitBurstRenderer, 1f - t * .4f, t, 1.8f);
                }
                else
                {
                    portalPhase = W15PortalPhase.Settle;
                    var t = Mathf.Clamp01((elapsed - PortalEjectionEnd) / .3f);
                    SetPortalRoleVisibility(false, true);
                    ApplyRendererProperties(portalExitBurstRenderer, 1f - t, t, 1.2f);
                }
            }
            UpdatePortalFlowGeometry();
            if (portalRing != null) portalRing.localRotation = BaseRotation(portalRing) * Quaternion.Euler(0f, 0f, PortalFlowDirection * elapsed * swirlSpeed * 90f);
        }

        private void UpdateLoot(float delta)
        {
            if (pickingUp)
            {
                var distance = Mathf.Max(.05f, Vector3.Distance(pickupStart, pickupTarget));
                pickupProgress = Mathf.Clamp01(pickupProgress + delta * pickupSpeed / distance);
                if (lootBase != null) lootBase.position = EvaluatePickupArc(pickupProgress);
                var collapse = 1f - Mathf.SmoothStep(0f, 1f, pickupProgress);
                if (lootBeam != null) lootBeam.localScale = new Vector3(LootBeamWidth * collapse, LootBeamHeight * collapse, LootBeamWidth * collapse);
                if (lootCrownRenderer != null) ApplyRendererProperties(lootCrownRenderer, collapse, pickupProgress, 1f + rarity * .12f);
                if (lootBase != null) lootBase.localScale = BaseScale(lootBase) * Mathf.Lerp(1f, .15f, pickupProgress);
                if (pickupProgress >= 1f)
                {
                    if (lootBase != null) lootBase.position = pickupTarget;
                    transform.position = pickupStartRoot;
                    Stop(VfxStopMode.Immediate);
                }
                return;
            }
            ApplyLootDimensions(elapsed);
        }

        private void BeginDestructionVisuals()
        {
            if (destructionIntactRenderer != null) destructionIntactRenderer.enabled = true;
            if (destructionFragmentsRenderer != null) destructionFragmentsRenderer.enabled = false;
            if (destructionDust != null) destructionDust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private void BeginLifeCycleVisuals()
        {
            lifecycleProgress = inverseEntrance ? 1f : 0f;
            ApplyExternalDissolve(lifecycleProgress);
            if (lifecycleEdgeRenderer != null) lifecycleEdgeRenderer.enabled = true;
            if (lifecycleParticles != null) { SetParticleRendererEnabled(lifecycleParticles, true); lifecycleParticles.Play(true); }
        }

        private void BeginPortalVisuals()
        {
            portalPhase = portalRole == PortalEndpointRole.Entry ? W15PortalPhase.EntryIntake : W15PortalPhase.ExitDelay;
            SetPortalRoleVisibility(portalRole == PortalEndpointRole.Entry, false);
            if (portalRingRenderer != null) portalRingRenderer.enabled = portalRole == PortalEndpointRole.Entry;
            if (portalFlowLine != null) portalFlowLine.enabled = portalRole == PortalEndpointRole.Entry;
        }

        private void BeginLootVisuals()
        {
            pickingUp = false;
            pickupProgress = 0f;
            ConfigureRarity(rarity);
            if (lootBaseRenderer != null) lootBaseRenderer.enabled = true;
            if (lootBeamRenderer != null) lootBeamRenderer.enabled = true;
            if (lootCrownRenderer != null) lootCrownRenderer.enabled = true;
            if (lootSparkles != null) { SetParticleRendererEnabled(lootSparkles, true); lootSparkles.Play(true); }
        }

        private void SetDecalVisible(bool value)
        {
            for (var index = 0; index < decalLayers.Length; index++) if (decalLayers[index] != null)
            {
                var renderer = decalLayers[index].GetComponent<Renderer>();
                if (renderer != null) renderer.enabled = value;
            }
        }

        private void SetWeaponVisible(bool endpoint, bool ribbon)
        {
            if (weaponEndpointLine != null) weaponEndpointLine.enabled = endpoint;
            if (weaponRibbonRenderer != null) weaponRibbonRenderer.enabled = ribbon;
        }

        private void SetPortalRoleVisibility(bool entry, bool exitBurst)
        {
            if (portalRingRenderer != null) portalRingRenderer.enabled = entry || exitBurst;
            if (portalInteriorRenderer != null) portalInteriorRenderer.enabled = entry || exitBurst;
            if (portalEntryFunnelRenderer != null) portalEntryFunnelRenderer.enabled = entry;
            if (portalExitBurstRenderer != null) portalExitBurstRenderer.enabled = exitBurst;
            if (portalFlowLine != null) portalFlowLine.enabled = entry || exitBurst;
        }

        private void UpdatePortalFlowGeometry()
        {
            if (portalFlowLine == null || !portalFlowLine.enabled) return;
            portalFlowLine.useWorldSpace = false;
            portalFlowLine.positionCount = 20;
            for (var index = 0; index < 20; index++)
            {
                var t = index / 19f;
                var flowT = portalRole == PortalEndpointRole.Entry ? 1f - t : t;
                var radius = portalRadius * Mathf.Lerp(.08f, .82f, flowT);
                var angle = flowT * Mathf.PI * 5f + elapsed * swirlSpeed * PortalFlowDirection;
                portalFlowLine.SetPosition(index, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, (t - .5f) * .12f));
            }
        }

        private void RebuildWeaponRibbon()
        {
            if (weaponMesh == null) return;
            if (weaponSamples.Count < 2) { weaponMesh.Clear(); return; }
            var ribbonPointCount = (weaponSamples.Count - 1) * 2 + 1;
            var vertices = new Vector3[ribbonPointCount * 2];
            var uv = new Vector2[vertices.Length];
            var colors = new Color[vertices.Length];
            var triangles = new int[(ribbonPointCount - 1) * 6];
            for (var index = 0; index < ribbonPointCount; index++)
            {
                var segment = Mathf.Min(weaponSamples.Count - 2, index / 2);
                var localT = index == ribbonPointCount - 1 ? 1f : (index & 1) * .5f;
                var a = weaponSamples[Mathf.Max(0, segment - 1)];
                var b = weaponSamples[segment];
                var c = weaponSamples[segment + 1];
                var d = weaponSamples[Mathf.Min(weaponSamples.Count - 1, segment + 2)];
                var t = index / (float)(ribbonPointCount - 1);
                vertices[index * 2] = CatmullRom(a.Root, b.Root, c.Root, d.Root, localT);
                vertices[index * 2 + 1] = CatmullRom(a.Tip, b.Tip, c.Tip, d.Tip, localT);
                uv[index * 2] = new Vector2(t, 0f);
                uv[index * 2 + 1] = new Vector2(t, 1f);
                colors[index * 2] = colors[index * 2 + 1] = new Color(1f, 1f, 1f, Mathf.Lerp(.08f, 1f, t));
                if (index < ribbonPointCount - 1)
                {
                    var q = index * 6; var v = index * 2;
                    triangles[q] = v; triangles[q + 1] = v + 1; triangles[q + 2] = v + 3;
                    triangles[q + 3] = v; triangles[q + 4] = v + 3; triangles[q + 5] = v + 2;
                }
            }
            weaponMesh.Clear();
            weaponMesh.vertices = vertices;
            weaponMesh.uv = uv;
            weaponMesh.colors = colors;
            weaponMesh.triangles = triangles;
            weaponMesh.RecalculateBounds();
        }

        private static Vector3 CatmullRom(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t)
        {
            var t2 = t * t; var t3 = t2 * t;
            return .5f * ((2f * b) + (-a + c) * t + (2f * a - 5f * b + 4f * c - d) * t2 + (-a + 3f * b - 3f * c + d) * t3);
        }

        private void RebuildDestructionMesh(float age)
        {
            if (destructionMesh == null) return;
            var vertices = new Vector3[pieceCount * 4];
            var uv = new Vector2[vertices.Length];
            var colors = new Color[vertices.Length];
            var triangles = new int[pieceCount * 6];
            for (var index = 0; index < pieceCount; index++)
            {
                var center = GetDeterministicPiecePosition(index, age);
                destructionPositions[index] = center;
                var angle = (index * 47f + age * (110f + index * 9f)) * Mathf.Deg2Rad;
                var right = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), Mathf.Sin(angle * .7f) * .35f) * (.07f + (index % 3) * .018f);
                var up = new Vector3(-Mathf.Sin(angle), Mathf.Cos(angle), Mathf.Cos(angle * .6f) * .28f) * (.11f + (index % 2) * .025f);
                var v = index * 4;
                vertices[v] = center - right - up; vertices[v + 1] = center - right + up;
                vertices[v + 2] = center + right + up; vertices[v + 3] = center + right - up;
                uv[v] = Vector2.zero; uv[v + 1] = Vector2.up; uv[v + 2] = Vector2.one; uv[v + 3] = Vector2.right;
                var tint = variant == W15NextVariant.CrystalShatter ? Color.Lerp(Color.cyan, Color.white, (index % 4) / 3f) : Color.Lerp(new Color(.35f, .12f, .03f), new Color(1f, .62f, .18f), (index % 4) / 3f);
                colors[v] = colors[v + 1] = colors[v + 2] = colors[v + 3] = tint;
                var q = index * 6;
                triangles[q] = v; triangles[q + 1] = v + 1; triangles[q + 2] = v + 2;
                triangles[q + 3] = v; triangles[q + 4] = v + 2; triangles[q + 5] = v + 3;
            }
            destructionMesh.Clear(); destructionMesh.vertices = vertices; destructionMesh.uv = uv; destructionMesh.colors = colors; destructionMesh.triangles = triangles; destructionMesh.RecalculateBounds();
        }

        private Vector3 InitialPieceVelocity(int index)
        {
            var hash = Hash(seed, (uint)index);
            var angle = (hash & 65535u) / 65535f * Mathf.PI * 2f;
            var lift = .72f + ((hash >> 16) & 255u) / 255f * .55f;
            var radial = .72f + ((hash >> 24) & 255u) / 255f * .36f;
            var variantScale = variant == W15NextVariant.CrystalShatter ? .82f : 1f;
            return new Vector3(Mathf.Cos(angle) * radial, lift, Mathf.Sin(angle) * radial * .52f) * explodeForce * variantScale + destructionImpulse * .22f;
        }

        private static float EvaluateBouncedHeight(float height, float velocity, float age, out int bounceCount)
        {
            const float gravity = 7.6f;
            var firstHit = (velocity + Mathf.Sqrt(velocity * velocity + 2f * gravity * height)) / gravity;
            if (age <= firstHit) { bounceCount = 0; return Mathf.Max(0f, height + velocity * age - .5f * gravity * age * age); }
            var incoming = Mathf.Max(.1f, gravity * firstHit - velocity);
            var firstBounceVelocity = incoming * .42f;
            var secondHit = firstHit + 2f * firstBounceVelocity / gravity;
            if (age <= secondHit)
            {
                bounceCount = 1; var t = age - firstHit;
                return Mathf.Max(0f, firstBounceVelocity * t - .5f * gravity * t * t);
            }
            var secondBounceVelocity = firstBounceVelocity * .34f;
            var settleHit = secondHit + 2f * secondBounceVelocity / gravity;
            if (age <= settleHit)
            {
                bounceCount = 2; var t = age - secondHit;
                return Mathf.Max(0f, secondBounceVelocity * t - .5f * gravity * t * t);
            }
            bounceCount = 2; return 0f;
        }

        private void RebuildLootGeometry()
        {
            if (lootCrownMesh == null) return;
            var segments = rarity == 1 ? 12 : rarity == 2 ? 4 : rarity == 3 ? 6 : rarity == 4 ? 16 : 10;
            var vertices = new List<Vector3>();
            var uv = new List<Vector2>();
            var colors = new List<Color>();
            var triangles = new List<int>();
            for (var layer = 0; layer < rarity; layer++)
            {
                var inner = .18f + layer * .035f;
                var outer = inner + .035f + rarity * .006f;
                var y = .22f + layer * .11f;
                var start = vertices.Count;
                for (var index = 0; index <= segments; index++)
                {
                    var angle = index / (float)segments * Mathf.PI * 2f;
                    var shape = 1f;
                    if (rarity == 4) shape = (index & 1) == 0 ? 1f : .72f;
                    else if (rarity == 5) shape = (index & 1) == 0 ? 1f : .46f;
                    var direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                    vertices.Add(direction * inner * shape + Vector3.up * y);
                    vertices.Add(direction * outer * shape + Vector3.up * y);
                    uv.Add(new Vector2(index / (float)segments, 0f)); uv.Add(new Vector2(index / (float)segments, 1f));
                    colors.Add(Color.white); colors.Add(Color.white);
                    if (index < segments)
                    {
                        var v = start + index * 2;
                        triangles.Add(v); triangles.Add(v + 1); triangles.Add(v + 3);
                        triangles.Add(v); triangles.Add(v + 3); triangles.Add(v + 2);
                    }
                }
            }
            lootCrownMesh.Clear(); lootCrownMesh.SetVertices(vertices); lootCrownMesh.SetUVs(0, uv); lootCrownMesh.SetColors(colors); lootCrownMesh.SetTriangles(triangles, 0); lootCrownMesh.RecalculateBounds();
        }

        private void ConfigureLootParticles()
        {
            if (lootSparkles == null) return;
            var emission = lootSparkles.emission; emission.rateOverTime = LootSparkleRate;
            var main = lootSparkles.main; main.maxParticles = Mathf.Min(24, 5 + rarity * 3);
        }

        private void ApplyLootDimensions(float time)
        {
            var pulse = 1f + (LootPeakScale - 1f) * (.5f + .5f * Mathf.Sin(time * Mathf.PI * 2f * LootCadenceHz));
            if (lootBase != null) lootBase.localScale = BaseScale(lootBase) * pulse;
            if (lootBeam != null) lootBeam.localScale = new Vector3(LootBeamWidth * pulse, LootBeamHeight, LootBeamWidth * pulse);
            if (lootCrownFilter != null) lootCrownFilter.transform.localScale = BaseScale(lootCrownFilter.transform) * pulse;
            ApplyRendererProperties(lootBaseRenderer, 1f, time, 1f + rarity * .18f);
            ApplyRendererProperties(lootBeamRenderer, .72f + rarity * .045f, time, 1f + rarity * .22f);
            ApplyRendererProperties(lootCrownRenderer, 1f, time * LootCadenceHz, 1f + rarity * .28f);
        }

        private Vector3 EvaluatePickupArc(float t)
        {
            t = Mathf.Clamp01(t);
            var midpoint = (pickupStart + pickupTarget) * .5f + Vector3.up * pickupArcHeight;
            var a = Vector3.Lerp(pickupStart, midpoint, t);
            var b = Vector3.Lerp(midpoint, pickupTarget, t);
            return Vector3.Lerp(a, b, t);
        }

        private void ApplyExternalDissolve(float dissolve)
        {
            if (externalRenderers == null || externalRenderers.Length == 0) return;
            var minY = float.PositiveInfinity; var maxY = float.NegativeInfinity;
            for (var index = 0; index < externalRenderers.Length; index++) if (externalRenderers[index] != null)
            {
                minY = Mathf.Min(minY, externalRenderers[index].bounds.min.y);
                maxY = Mathf.Max(maxY, externalRenderers[index].bounds.max.y);
            }
            if (float.IsNaN(minY) || float.IsInfinity(minY) || float.IsNaN(maxY) || float.IsInfinity(maxY)) { minY = transform.position.y - 1f; maxY = transform.position.y + 1f; }
            for (var index = 0; index < externalRenderers.Length; index++)
            {
                var target = externalRenderers[index]; if (target == null) continue;
                target.GetPropertyBlock(Block);
                Block.SetFloat("_Dissolve", Mathf.Clamp01(dissolve));
                Block.SetFloat("_DissolveMinY", minY);
                Block.SetFloat("_DissolveMaxY", Mathf.Max(minY + .01f, maxY));
                Block.SetFloat("_DissolveDirection", lifecycleDirection == "radial" ? 2f : lifecycleDirection == "down" ? -1f : 1f);
                Block.SetColor("_PrimaryColor", primary);
                Block.SetColor("_SecondaryColor", secondary);
                Block.SetColor("_DissolveEdgeColor", accent);
                Block.SetFloat("_GlobalAlpha", 1f);
                target.SetPropertyBlock(Block);
            }
        }

        private void RestoreExternalRenderers()
        {
            if (externalRenderers == null) { externalRenderers = new Renderer[0]; externalOriginalBlocks = new MaterialPropertyBlock[0]; return; }
            for (var index = 0; index < externalRenderers.Length; index++)
            {
                var target = externalRenderers[index]; if (target == null) continue;
                target.SetPropertyBlock(index < externalOriginalBlocks.Length ? externalOriginalBlocks[index] : null);
            }
            externalRenderers = new Renderer[0];
            externalOriginalBlocks = new MaterialPropertyBlock[0];
        }

        private void EnsureRuntimeMeshes()
        {
            if (weaponRibbonFilter != null && weaponMesh == null) { weaponMesh = NewRuntimeMesh(name + "_SweptBladeMesh"); weaponRibbonFilter.sharedMesh = weaponMesh; }
            if (destructionFragmentsFilter != null && destructionMesh == null) { destructionMesh = NewRuntimeMesh(name + "_IndependentFragmentsMesh"); destructionFragmentsFilter.sharedMesh = destructionMesh; }
            if (lootCrownFilter != null && lootCrownMesh == null) { lootCrownMesh = NewRuntimeMesh(name + "_RarityGeometryMesh"); lootCrownFilter.sharedMesh = lootCrownMesh; RebuildLootGeometry(); }
        }

        private static Mesh NewRuntimeMesh(string meshName)
        {
            var mesh = new Mesh(); mesh.name = meshName; mesh.MarkDynamic(); return mesh;
        }

        private static void ClearMesh(Mesh mesh) { if (mesh != null) mesh.Clear(); }
        private static void ClearLine(LineRenderer line) { if (line != null) { line.positionCount = 0; line.enabled = false; } }

        private void CaptureBaseState()
        {
            cachedTransforms = GetComponentsInChildren<Transform>(true);
            basePositions = new Vector3[cachedTransforms.Length]; baseRotations = new Quaternion[cachedTransforms.Length]; baseScales = new Vector3[cachedTransforms.Length];
            for (var index = 0; index < cachedTransforms.Length; index++)
            {
                basePositions[index] = cachedTransforms[index].localPosition;
                baseRotations[index] = cachedTransforms[index].localRotation;
                baseScales[index] = cachedTransforms[index].localScale;
            }
            if (lootBase != null) lootBaseLocalPosition = lootBase.localPosition;
        }

        private void RestoreBaseState()
        {
            for (var index = 0; index < cachedTransforms.Length; index++) if (cachedTransforms[index] != null)
            {
                cachedTransforms[index].localPosition = basePositions[index];
                cachedTransforms[index].localRotation = baseRotations[index];
                cachedTransforms[index].localScale = baseScales[index];
            }
        }

        private Vector3 BaseScale(Transform value)
        {
            for (var index = 0; index < cachedTransforms.Length; index++) if (cachedTransforms[index] == value) return baseScales[index];
            return value == null ? Vector3.one : value.localScale;
        }

        private Quaternion BaseRotation(Transform value)
        {
            for (var index = 0; index < cachedTransforms.Length; index++) if (cachedTransforms[index] == value) return baseRotations[index];
            return value == null ? Quaternion.identity : value.localRotation;
        }

        private void SetAllOwnedVisible(bool value)
        {
            if (ownedRenderers == null) return;
            for (var index = 0; index < ownedRenderers.Length; index++) if (ownedRenderers[index] != null) ownedRenderers[index].enabled = value;
        }

        private void StopParticles(bool clear)
        {
            if (particles == null) return;
            for (var index = 0; index < particles.Length; index++) if (particles[index] != null)
                particles[index].Stop(true, clear ? ParticleSystemStopBehavior.StopEmittingAndClear : ParticleSystemStopBehavior.StopEmitting);
        }

        private void PlayParticles()
        {
            if (particles == null) return;
            for (var index = 0; index < particles.Length; index++) if (particles[index] != null)
            {
                SetParticleRendererEnabled(particles[index], true);
                particles[index].Play(true);
            }
        }

        private static void SetParticleRendererEnabled(ParticleSystem particle, bool value)
        {
            if (particle == null) return;
            var renderer = particle.GetComponent<ParticleSystemRenderer>();
            if (renderer != null) renderer.enabled = value;
        }

        private void ApplyGlobalAlpha(float alpha)
        {
            if (ownedRenderers == null) return;
            for (var index = 0; index < ownedRenderers.Length; index++) ApplyRendererProperties(ownedRenderers[index], alpha, elapsed, 1f);
        }

        private void ApplyRendererProperties(Renderer renderer, float alpha, float phase, float intensity)
        {
            if (renderer == null) return;
            renderer.GetPropertyBlock(Block);
            Block.SetColor("_PrimaryColor", primary); Block.SetColor("_SecondaryColor", secondary); Block.SetColor("_AccentColor", accent);
            Block.SetFloat("_GlobalAlpha", Mathf.Clamp01(alpha)); Block.SetFloat("_Phase", phase); Block.SetFloat("_Intensity", intensity); Block.SetFloat("_Rarity", rarity);
            renderer.SetPropertyBlock(Block);
        }

        private static uint Hash(uint a, uint b)
        {
            unchecked { var value = a ^ (b + 0x9e3779b9u + (a << 6) + (a >> 2)); value ^= value >> 16; value *= 0x7feb352du; value ^= value >> 15; value *= 0x846ca68bu; return value ^ (value >> 16); }
        }

        private struct BladeSample
        {
            public Vector3 Root;
            public Vector3 Tip;
            public float Age;
            public BladeSample(Vector3 root, Vector3 tip, float age) { Root = root; Tip = tip; Age = age; }
        }
    }
}

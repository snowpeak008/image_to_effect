using System;
using UnityEngine;

namespace VFXComposer
{
    public enum ElementNextCandidateFamily
    {
        Fire, Frost, Lightning,
        Water, Wind, Earth, Nature, Toxic, Holy, Shadow, Arcane
    }

    public enum ElementNextCandidateProfile
    {
        FlameSlash, FireNova, Flamethrower, BurningStatus, EmberRain, PhoenixDart, ChainBlast, FireShield,
        IceSpike, Blizzard, FrostBreath, IceShard, FreezeStatus, CrystalShield, FlashFreeze,
        ThunderStrike, BallLightning, StaticField, StormCharge, ElectroSlash, EmpNova, VoltShield,
        WaterJet, TidalWave, BubbleShield, SplashImpact, Whirlpool, Tornado, WindBlade, GaleDash,
        EarthSpike, Boulder, QuakeStomp, ThornSnare, VineWhip, HealingBloom, SporeBurst, AcidLob,
        DivineSmite, HolyHalo, Resurrection, ShadowClaw, VoidOrb, ShadowGrasp, CurseMark,
        ArcaneMissile, ArcaneRune
    }

    public enum ElementNextCandidatePhase
    {
        Hidden, Anticipation, Growth, Sustain, Eruption, Discharge, Residue, Fracture, Melt, Afterglow,
        Flow, Curl, Pop, HeavyRise, Impact, Reveal, Pulse, Wither, Bloom, Linger, Suction, Implode,
        Activation, Retract
    }

    [Serializable]
    public struct ElementNextParameterBinding
    {
        [SerializeField] private string parameter;
        [SerializeField] private string carrier;
        [SerializeField] private float authoredValue;

        public string Parameter { get { return parameter; } }
        public string Carrier { get { return carrier; } }
        public float AuthoredValue { get { return authoredValue; } }
    }

    /// <summary>
    /// Physical visual execution for the W3-W8 next candidates.  Every element family has a
    /// deliberately separate evaluator: palette never selects the motion or geometry language.
    /// Content values remain serialized by their Recipe names and are consumed on every frame.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed partial class ElementNextCandidateVisualExecutor : MonoBehaviour, IVfxRuntimeEntry
    {
        public const int AbsoluteMaxParticleCapacity = 120;
        public const int AbsoluteMaxRendererCount = 7;
        public const int AbsoluteMaxMaterialCount = 5;
        public const int AbsoluteMaxParticleSystemCount = 5;
        public const int MaxArcCarriers = 5;
        public const int MaxArcPoints = 12;

        [Header("Candidate identity")]
        [SerializeField] private string effectId = string.Empty;
        [SerializeField] private string compilerVersion = string.Empty;
        [SerializeField] private string visualStatus = "VISUAL_PENDING";
        [SerializeField] private string carrierShapeToken = string.Empty;
        [SerializeField] private string topologySignature = string.Empty;
        [SerializeField] private ElementNextCandidateFamily family;
        [SerializeField] private ElementNextCandidateProfile profile;
        [SerializeField] private StyledVfxLifecycle lifecycle = StyledVfxLifecycle.OneShot;
        [SerializeField, Min(.05f)] private float duration = 1f;
        [SerializeField] private uint seed = 1;
        [SerializeField] private Color primary = Color.white;
        [SerializeField] private Color secondary = Color.white;
        [SerializeField] private Color accent = Color.white;

        [Header("Recipe content -> carrier protocol")]
        [SerializeField] private string[] contentKeys = new string[0];
        [SerializeField] private float[] contentValues = new float[0];
        [SerializeField] private string[] contentTextKeys = new string[0];
        [SerializeField] private string[] contentTextValues = new string[0];
        [SerializeField] private ElementNextParameterBinding[] parameterBindings = new ElementNextParameterBinding[0];

        [Header("Five visual responsibilities")]
        [SerializeField] private Transform primaryCarrier;
        [SerializeField] private Renderer primaryRenderer;
        [SerializeField] private Transform highlightCarrier;
        [SerializeField] private Renderer highlightRenderer;
        [SerializeField] private Transform outerCarrier;
        [SerializeField] private Renderer outerRenderer;
        [SerializeField] private Transform residualCarrier;
        [SerializeField] private Renderer residualRenderer;
        [SerializeField] private Transform eventCarrier;
        [SerializeField] private Renderer eventRenderer;
        [SerializeField] private LineRenderer[] arcCarriers = new LineRenderer[0];
        [SerializeField] private ParticleSystem detailParticles;
        [SerializeField] private ParticleSystemRenderer detailParticleRenderer;
        [SerializeField] private Renderer[] ownedRenderers = new Renderer[0];

        [Header("Compiled budget and bounds")]
        [SerializeField, Range(0, AbsoluteMaxParticleCapacity)] private int particleBudget = 40;
        [SerializeField, Range(1, AbsoluteMaxRendererCount)] private int rendererBudget = AbsoluteMaxRendererCount;
        [SerializeField, Range(1, AbsoluteMaxMaterialCount)] private int materialBudget = 4;
        [SerializeField, Range(1, AbsoluteMaxParticleSystemCount)] private int particleSystemBudget = 3;
        [SerializeField, Min(.01f)] private float maxLocalExtent = 1f;

        private readonly ParticleSystem.Particle[] particleBuffer = new ParticleSystem.Particle[AbsoluteMaxParticleCapacity];
        private readonly Vector3[,] sampledArcPoints = new Vector3[MaxArcCarriers, MaxArcPoints];
        private readonly int[] sampledArcPointCounts = new int[MaxArcCarriers];
        private Transform[] roleTransforms;
        private Renderer[] roleRenderers;
        private Vector3[] roleBasePositions;
        private Quaternion[] roleBaseRotations;
        private Vector3[] roleBaseScales;
        private MaterialPropertyBlock propertyBlock;
        private float elapsed;
        private float stopElapsed;
        private float triggeredAt = float.PositiveInfinity;
        private Vector3 triggeredLocalPosition;
        private bool playing;
        private bool stopping;
        private bool defaultsCaptured;
        private int particleCount;
        private int discreteStep;
        private int playCount;
        private float framePeakAlpha;
        private float tailStartStrength;

        public string EffectId { get { return effectId; } }
        public string CompilerVersion { get { return compilerVersion; } }
        public string VisualStatus { get { return visualStatus; } }
        public string CarrierShapeToken { get { return carrierShapeToken; } }
        public string TopologySignature { get { return topologySignature; } }
        public ElementNextCandidateFamily Family { get { return family; } }
        public ElementNextCandidateProfile Profile { get { return profile; } }
        public StyledVfxLifecycle Lifecycle { get { return lifecycle; } }
        public float Duration { get { return duration; } }
        public uint Seed { get { return seed; } }
        public bool IsAlive { get { return playing || stopping; } }
        public bool IsStopping { get { return stopping; } }
        public float NormalizedTime { get { return Mathf.Clamp01(elapsed / Mathf.Max(.05f, duration)); } }
        public int PlayCount { get { return playCount; } }
        public ElementNextCandidatePhase Phase { get; private set; }
        public int ParameterBindingCount { get { return parameterBindings == null ? 0 : parameterBindings.Length; } }
        public int OwnedRendererCount { get { return ownedRenderers == null ? 0 : ownedRenderers.Length; } }
        public int ParticleBudget { get { return particleBudget; } }
        public int RendererBudget { get { return rendererBudget; } }
        public int MaterialBudget { get { return materialBudget; } }
        public int ParticleSystemBudget { get { return particleSystemBudget; } }
        public float MaxLocalExtent { get { return maxLocalExtent; } }
        public int VisibleParticleCount { get { return particleCount; } }
        public int VisibleArcCount { get; private set; }
        public int ActiveLayerCount { get; private set; }
        public int PrimaryCarrierMultiplicity { get; private set; }
        public int EventSequence { get; private set; }

        // Fire readback: combustion -> eruption -> embers/heat -> residue.
        public float FireCombustion { get; private set; }
        public float FireEruption { get; private set; }
        public float FireHeatHaze { get; private set; }
        public float FireResidue { get; private set; }
        public int FireEmberCount { get; private set; }
        public Color FireFuelColor { get; private set; }

        // Frost readback: crystalline growth -> mist -> fracture/melt.  Sharpness is geometric.
        public float IceCrystalGrowth { get; private set; }
        public float IceSharpness { get; private set; }
        public float IceMistOpacity { get; private set; }
        public int IceFractureCount { get; private set; }
        public float IceMelt { get; private set; }
        public Color IceHitFlashColor { get; private set; }

        // Lightning readback: discrete charge/flash/discharge/afterglow.
        public int LightningDiscreteStep { get { return discreteStep; } }
        public int LightningForkCount { get; private set; }
        public bool LightningFlashOn { get; private set; }
        public float LightningCharge { get; private set; }
        public float LightningDischarge { get; private set; }
        public float LightningAfterglow { get; private set; }
        public int LightningControlledFlashCount { get; private set; }

        // W6 readback: water volume/foam/residue versus low-opacity wind/debris/flow lines.
        public float WaterFlow { get; private set; }
        public float WaterFoam { get; private set; }
        public float WaterSplash { get; private set; }
        public float WaterResidue { get; private set; }
        public float WaterSag { get; private set; }
        public float WindOpacity { get; private set; }
        public int WindDebrisCount { get; private set; }
        public int WindFlowLineCount { get; private set; }

        // W7 readback: earth weight/rise/dust, botanical reveal/wither and toxic linger/pool.
        public float EarthWeight { get; private set; }
        public float EarthRise { get; private set; }
        public float EarthOvershoot { get; private set; }
        public float EarthDust { get; private set; }
        public int EarthDebrisCount { get; private set; }
        public int EarthRevealedSpikeCount { get; private set; }
        public float NatureGrowth { get; private set; }
        public float NaturePulse { get; private set; }
        public float NatureWither { get; private set; }
        public int NatureBloomCount { get; private set; }
        public float ToxicSwelling { get; private set; }
        public float ToxicLinger { get; private set; }
        public float ToxicPool { get; private set; }
        public int ToxicBubbleCount { get; private set; }

        // W8 readback: ordered holy reveal, negative-space shadow and deterministic arcane order.
        public float HolyOrderedReveal { get; private set; }
        public float HolyVerticalReveal { get; private set; }
        public float HolyAfterglow { get; private set; }
        public int HolyFeatherCount { get; private set; }
        public float ShadowNegativeSpace { get; private set; }
        public float ShadowMist { get; private set; }
        public float ShadowSuction { get; private set; }
        public float ShadowImplode { get; private set; }
        public int ShadowHandCount { get; private set; }
        public float ArcaneActivation { get; private set; }
        public int ArcaneGlyphCount { get; private set; }
        public int ArcaneMissileCount { get; private set; }
        public int ArcaneStaggerStep { get; private set; }

        public bool BudgetWithinLimits
        {
            get
            {
                return OwnedRendererCount <= rendererBudget && rendererBudget <= AbsoluteMaxRendererCount
                    && particleBudget <= AbsoluteMaxParticleCapacity
                    && CountDistinctMaterials() <= materialBudget
                    && (detailParticles == null ? 0 : 1) <= particleSystemBudget;
            }
        }

        public bool AllVisualsHidden
        {
            get
            {
                if (ownedRenderers != null)
                    for (var index = 0; index < ownedRenderers.Length; index++)
                        if (ownedRenderers[index] != null && ownedRenderers[index].enabled) return false;
                return detailParticles == null || detailParticles.particleCount == 0;
            }
        }

        private MaterialPropertyBlock Block
        {
            get { if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock(); return propertyBlock; }
        }

        private void Awake()
        {
            CaptureDefaults();
            CompleteReset();
        }

        private void Update()
        {
            var delta = Mathf.Max(0f, Time.deltaTime);
            if (stopping)
            {
                EvaluateTailAtTime(stopElapsed + delta);
                return;
            }
            if (!playing) return;
            elapsed += delta;
            EvaluateAtTime(elapsed);
            if (lifecycle == StyledVfxLifecycle.OneShot && elapsed >= duration) Stop(VfxStopMode.AllowTail);
        }

        public void Initialize(VfxRuntimeContext context)
        {
            ResetForPool();
            transform.SetPositionAndRotation(context.Position, context.Rotation);
        }

        public void Play()
        {
            CaptureDefaults();
            RestoreRoles();
            playing = true;
            stopping = false;
            elapsed = 0f;
            stopElapsed = 0f;
            triggeredAt = float.PositiveInfinity;
            triggeredLocalPosition = Vector3.zero;
            playCount++;
            FireFuelColor = GetContentColor("fuel_color", primary);
            IceHitFlashColor = GetContentColor("hit_flash_color", accent);
            if (detailParticles != null)
            {
                detailParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                detailParticles.Play(true);
            }
            EvaluateAtTime(0f);
        }

        public bool SendEvent(string eventId, VfxRuntimeEvent payload)
        {
            if (eventId == "play" || eventId == "start" || eventId == "launch")
            {
                transform.SetPositionAndRotation(payload.Position, payload.Rotation);
                Play();
                return true;
            }
            if (eventId == "hit" || eventId == "impact" || eventId == "tick")
            {
                if (!IsAlive) Play();
                TriggerLocalEvent(transform.InverseTransformPoint(payload.Position));
                return true;
            }
            if (eventId == "stop" || eventId == "cancel") { Stop(VfxStopMode.AllowTail); return true; }
            if (eventId == "reset") { ResetForPool(); return true; }
            return false;
        }

        public void TriggerLocalEvent(Vector3 localPosition)
        {
            triggeredAt = elapsed;
            triggeredLocalPosition = localPosition;
            EventSequence++;
            EvaluateAtTime(elapsed);
        }

        public void Stop(VfxStopMode mode)
        {
            if (mode == VfxStopMode.Immediate) { CompleteReset(); return; }
            if (!playing && !stopping) return;
            tailStartStrength = Mathf.Clamp01(framePeakAlpha);
            if (tailStartStrength <= .001f) { CompleteReset(); return; }
            playing = false;
            stopping = true;
            stopElapsed = 0f;
            EvaluateTailAtTime(0f);
        }

        public void ResetForPool() { CompleteReset(); }

        /// <summary>Deterministic authoring/test sample; no Unity random state is read or written.</summary>
        public void EvaluateAtTime(float absoluteTime)
        {
            if (!playing) return;
            elapsed = Mathf.Max(0f, absoluteTime);
            var localTime = lifecycle == StyledVfxLifecycle.OneShot
                ? Mathf.Min(elapsed, duration)
                : Mathf.Repeat(elapsed, Mathf.Max(.05f, duration));
            var normalized = Mathf.Clamp01(localTime / Mathf.Max(.05f, duration));
            ClearFrame();
            if (family == ElementNextCandidateFamily.Fire) EvaluateFire(localTime, normalized);
            else if (family == ElementNextCandidateFamily.Frost) EvaluateFrost(localTime, normalized);
            else if (family == ElementNextCandidateFamily.Lightning) EvaluateLightning(localTime, normalized);
            else EvaluateW6W8(localTime, normalized);
            CommitParticles();
        }

        public void EvaluateTailAtTime(float age)
        {
            if (!stopping) return;
            stopElapsed = Mathf.Max(0f, age);
            var tailDuration = family == ElementNextCandidateFamily.Fire ? .4f : family == ElementNextCandidateFamily.Frost ? .36f : family == ElementNextCandidateFamily.Lightning ? .28f : W6W8TailDuration();
            var t = Mathf.Clamp01(stopElapsed / tailDuration);
            if (t >= 1f) { CompleteReset(); return; }
            ClearFrame();
            FireCombustion = FireEruption = FireHeatHaze = FireResidue = 0f; FireEmberCount = 0; FireFuelColor = Color.clear;
            IceCrystalGrowth = IceSharpness = IceMistOpacity = IceMelt = 0f; IceFractureCount = 0; IceHitFlashColor = Color.clear;
            LightningForkCount = 0; LightningFlashOn = false; LightningCharge = LightningDischarge = LightningAfterglow = 0f; LightningControlledFlashCount = 0;
            var alpha = (1f - t) * tailStartStrength;
            if (family == ElementNextCandidateFamily.Fire)
            {
                Phase = ElementNextCandidatePhase.Residue;
                FireResidue = alpha;
                FireHeatHaze = alpha * .55f;
                ShowRole(2, Vector3.zero, Quaternion.identity, Vector3.one * Mathf.Lerp(1f, 1.28f, t), alpha * .42f, .55f, 1f);
                ShowRole(3, Vector3.zero, Quaternion.identity, new Vector3(1f + t * .2f, .18f + t * .08f, 1f), alpha * .72f, .72f, 3f);
                AddRadialParticles(Mathf.Min(particleBudget, 8), Mathf.Lerp(.25f, .9f, t), .05f + t * .6f, .06f, alpha, 701);
                FireEmberCount = particleCount;
            }
            else if (family == ElementNextCandidateFamily.Frost)
            {
                var shatter = GetContentText("exit_mode", "shatter") == "shatter" || profile != ElementNextCandidateProfile.IceSpike;
                Phase = shatter ? ElementNextCandidatePhase.Fracture : ElementNextCandidatePhase.Melt;
                IceMelt = shatter ? t * .35f : t;
                IceFractureCount = shatter ? Mathf.Min(particleBudget, Mathf.RoundToInt(GetContentNumber("shatter_piece_count", 8f))) : 0;
                ShowRole(2, Vector3.down * t * .08f, Quaternion.identity, Vector3.one * Mathf.Lerp(1f, 1.12f, t), alpha * .5f, .5f, 4f);
                if (shatter) AddRadialParticles(IceFractureCount, Mathf.Lerp(.12f, .82f, t), -.12f - t * .3f, .08f, alpha, 809);
            }
            else if (family == ElementNextCandidateFamily.Lightning)
            {
                Phase = ElementNextCandidatePhase.Afterglow;
                LightningAfterglow = alpha;
                LightningFlashOn = false;
                ShowRole(0, Vector3.zero, Quaternion.identity, Vector3.one * Mathf.Lerp(1f, 1.35f, t), alpha * .25f, .45f, 6f);
                AddRadialParticles(Mathf.Min(particleBudget, 6), Mathf.Lerp(.18f, .75f, t), 0f, .055f, alpha, 911);
            }
            else EvaluateW6W8Tail(t, alpha);
            CommitParticles();
        }

        public float GetContentNumber(string key, float fallback)
        {
            var count = Mathf.Min(contentKeys == null ? 0 : contentKeys.Length, contentValues == null ? 0 : contentValues.Length);
            for (var index = 0; index < count; index++) if (contentKeys[index] == key) return contentValues[index];
            return fallback;
        }

        public string GetContentText(string key, string fallback)
        {
            var count = Mathf.Min(contentTextKeys == null ? 0 : contentTextKeys.Length, contentTextValues == null ? 0 : contentTextValues.Length);
            for (var index = 0; index < count; index++) if (contentTextKeys[index] == key) return contentTextValues[index];
            return fallback;
        }

        public string GetParameterCarrier(string parameter)
        {
            if (parameterBindings != null)
                for (var index = 0; index < parameterBindings.Length; index++)
                    if (parameterBindings[index].Parameter == parameter) return parameterBindings[index].Carrier;
            return string.Empty;
        }

        public float GetBoundAuthoredValue(string parameter, float fallback)
        {
            if (parameterBindings != null)
                for (var index = 0; index < parameterBindings.Length; index++)
                    if (parameterBindings[index].Parameter == parameter) return parameterBindings[index].AuthoredValue;
            return fallback;
        }

        public int GetArcPointCount(int arcIndex)
        {
            if (arcIndex < 0 || arcIndex >= MaxArcCarriers) throw new ArgumentOutOfRangeException("arcIndex");
            return sampledArcPointCounts[arcIndex];
        }

        public Vector3 GetArcPoint(int arcIndex, int pointIndex)
        {
            if (arcIndex < 0 || arcIndex >= MaxArcCarriers) throw new ArgumentOutOfRangeException("arcIndex");
            if (pointIndex < 0 || pointIndex >= sampledArcPointCounts[arcIndex]) throw new ArgumentOutOfRangeException("pointIndex");
            return sampledArcPoints[arcIndex, pointIndex];
        }

        public Vector3 GetVisibleParticlePosition(int index)
        {
            if (index < 0 || index >= particleCount) throw new ArgumentOutOfRangeException("index");
            return particleBuffer[index].position;
        }

        private void EvaluateFire(float time, float n)
        {
            FireCombustion = 0f; FireEruption = 0f; FireHeatHaze = 0f; FireResidue = 0f; FireEmberCount = 0;
            if (profile != ElementNextCandidateProfile.Flamethrower) FireFuelColor = primary;
            var pulse = .92f + .08f * Mathf.Sin(time * 15f + (seed & 7));
            if (profile == ElementNextCandidateProfile.FlameSlash)
            {
                var width = GetContentNumber("arc_width", .72f);
                var sweep = GetContentNumber("sweep_angle", 110f) / 110f;
                var ignition = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, .12f, n));
                var fade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.56f, 1f, n));
                FireCombustion = ignition * fade; FireEruption = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.08f, .38f, n)); FireHeatHaze = fade * .55f; FireResidue = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.48f, .72f, n)) * fade;
                Phase = n < .12f ? ElementNextCandidatePhase.Anticipation : n < .5f ? ElementNextCandidatePhase.Eruption : ElementNextCandidatePhase.Residue;
                ShowRole(0, Vector3.zero, Quaternion.Euler(0f, 0f, Mathf.Lerp(-28f, 24f, FireEruption)), new Vector3(sweep, width, 1f), FireCombustion, 1.05f, 0f);
                ShowRole(1, new Vector3(.08f, .03f, -.01f), Quaternion.identity, new Vector3(sweep * .84f, width * .42f, 1f), FireCombustion, 1.8f, 2f);
                ShowRole(2, Vector3.zero, Quaternion.identity, new Vector3(sweep * 1.08f, width * 1.3f, 1f), FireHeatHaze, .48f, 1f);
                ShowRole(3, new Vector3(-.12f, -.03f, .01f), Quaternion.identity, new Vector3(sweep, width * .82f, 1f), FireResidue, .7f, 3f);
                AddArc(0, new Vector3(-.8f, -.28f), new Vector3(.85f, .22f), 8, .08f * width, FireCombustion, .045f, Mathf.FloorToInt(time * 24f));
                AddRadialParticles(Mathf.RoundToInt(GetContentNumber("spark_count", 8f)), .42f + n * .58f, n * .5f, .055f, fade, 101);
            }
            else if (profile == ElementNextCandidateProfile.FireNova)
            {
                var radius = GetContentNumber("radius", 4f);
                var speed = GetContentNumber("ring_speed", 8f);
                var ring = Mathf.Min(radius, time * speed);
                var normalizedRing = ring / Mathf.Max(.01f, radius);
                var scorch = Mathf.Clamp01(GetContentNumber("scorch_lifetime", 1.2f) / 3f);
                FireCombustion = 1f - Mathf.SmoothStep(.62f, 1f, n); FireEruption = normalizedRing; FireHeatHaze = Mathf.Sin(normalizedRing * Mathf.PI); FireResidue = scorch * Mathf.SmoothStep(.22f, .5f, n) * (1f - Mathf.SmoothStep(.78f, 1f, n));
                PrimaryCarrierMultiplicity = Mathf.RoundToInt(GetContentNumber("tongue_count", 12f));
                Phase = n < .08f ? ElementNextCandidatePhase.Anticipation : n < .52f ? ElementNextCandidatePhase.Eruption : ElementNextCandidatePhase.Residue;
                ShowRole(0, Vector3.zero, Quaternion.Euler(64f, 0f, time * 28f), Vector3.one * Mathf.Max(.02f, ring), FireCombustion, 1.2f, 0f);
                ShowRole(1, Vector3.up * Mathf.Sin(n * Mathf.PI) * .22f, Quaternion.identity, new Vector3(.3f, .35f + FireEruption * 1.2f, .3f), FireCombustion, 2f, 2f);
                ShowRole(2, Vector3.zero, Quaternion.Euler(64f, 0f, -time * 17f), Vector3.one * Mathf.Max(.02f, ring * 1.08f), FireHeatHaze, .55f, 1f);
                ShowRole(3, Vector3.zero, Quaternion.Euler(64f, 0f, 0f), Vector3.one * radius, FireResidue, .45f, 3f);
                AddRadialParticles(Mathf.Min(particleBudget, PrimaryCarrierMultiplicity * 2), Mathf.Max(.08f, ring), .1f + FireEruption * .55f, .07f, 1f - n, 117);
            }
            else if (profile == ElementNextCandidateProfile.Flamethrower)
            {
                var length = GetContentNumber("length", 5f); var angle = GetContentNumber("cone_angle", 24f); var intensity = GetContentNumber("intensity", 1.3f);
                FireCombustion = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, .16f, n)); FireEruption = FireCombustion; FireHeatHaze = Mathf.Clamp01(intensity / 4f) * (.72f + .28f * pulse);
                Phase = n < .16f ? ElementNextCandidatePhase.Growth : ElementNextCandidatePhase.Sustain;
                ShowRoleWithColors(0, Vector3.right * length * .5f, Quaternion.Euler(0f, 0f, -90f), new Vector3(angle / 24f, length * .5f, 1f), FireCombustion, Mathf.Min(2.4f, intensity), 0f, FireFuelColor, secondary, accent);
                ShowRole(1, Vector3.right * length * .22f, Quaternion.Euler(0f, 0f, -90f), new Vector3(.22f, length * .22f, 1f), FireCombustion, 2.1f, 2f);
                ShowRole(2, Vector3.right * length * .52f, Quaternion.Euler(0f, 0f, -90f), new Vector3(angle / 18f, length * .56f, 1f), FireHeatHaze, .48f, 1f);
                AddArc(0, Vector3.zero, Vector3.right * length, 9, .08f + angle * .003f, FireCombustion * .7f, .08f, Mathf.FloorToInt(time * 18f));
                AddConeParticles(Mathf.Min(particleBudget, 20 + Mathf.RoundToInt(intensity * 8f)), length, angle, .06f, FireCombustion, 131);
            }
            else if (profile == ElementNextCandidateProfile.BurningStatus)
            {
                PrimaryCarrierMultiplicity = Mathf.RoundToInt(GetContentNumber("flame_count", 3f));
                var tick = GetContentNumber("tick_pulse", 1f) > .5f ? .82f + .18f * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(time * Mathf.PI * 4f)), 6f) : 1f;
                FireCombustion = tick; FireHeatHaze = .44f;
                Phase = ElementNextCandidatePhase.Sustain;
                ShowRole(0, Vector3.zero, Quaternion.identity, new Vector3(.7f + PrimaryCarrierMultiplicity * .08f, .9f * pulse, 1f), tick, 1.15f, 0f);
                ShowRole(1, Vector3.up * .05f, Quaternion.identity, new Vector3(.42f, .62f * pulse, 1f), tick, 1.75f, 2f);
                ShowRole(2, Vector3.up * .25f, Quaternion.identity, new Vector3(.82f, 1.1f, 1f), .38f, .5f, 1f);
                AddRadialParticles(Mathf.Min(particleBudget, PrimaryCarrierMultiplicity * 3), .42f, .2f + n * .65f, .052f, tick, 149);
            }
            else if (profile == ElementNextCandidateProfile.EmberRain)
            {
                var radius = GetContentNumber("radius", 5f); var density = Mathf.RoundToInt(GetContentNumber("rain_density", 48f)); var interval = GetContentNumber("tick_interval", .5f);
                var tickPhase = Mathf.Repeat(time, Mathf.Max(.05f, interval)) / Mathf.Max(.05f, interval);
                PrimaryCarrierMultiplicity = Mathf.RoundToInt(GetContentNumber("burn_patch_count", 5f)); FireCombustion = .82f; FireEruption = 1f - tickPhase; FireHeatHaze = .48f;
                Phase = ElementNextCandidatePhase.Sustain;
                ShowRole(0, Vector3.zero, Quaternion.Euler(68f, 0f, time * 8f), Vector3.one * radius, .72f, .8f, 0f);
                ShowRole(2, Vector3.up * .08f, Quaternion.Euler(68f, 0f, -time * 5f), Vector3.one * radius * 1.04f, .36f, .45f, 1f);
                ShowRole(3, Vector3.zero, Quaternion.Euler(68f, 0f, 0f), Vector3.one * radius, .55f, .62f, 3f);
                AddRainParticles(Mathf.Min(particleBudget, density), radius, time, .055f, .95f, 163);
                if (tickPhase < .18f) ShowRole(4, Vector3.zero, Quaternion.Euler(68f, 0f, 0f), Vector3.one * radius * (.4f + tickPhase * 3f), 1f - tickPhase / .18f, 1.7f, 2f);
            }
            else if (profile == ElementNextCandidateProfile.PhoenixDart)
            {
                var span = GetContentNumber("wing_span", 1.4f); var trail = GetContentNumber("trail_length", 1.1f); var eventAge = elapsed - triggeredAt;
                var impact = eventAge >= 0f && eventAge <= .28f;
                var travelFade = 1f - Mathf.SmoothStep(.82f, 1f, n);
                FireCombustion = impact ? 1f - eventAge / .28f : travelFade; FireHeatHaze = .38f * FireCombustion; FireEruption = impact ? 1f : 0f;
                Phase = impact ? ElementNextCandidatePhase.Eruption : ElementNextCandidatePhase.Sustain;
                var travel = impact ? triggeredLocalPosition : new Vector3(Mathf.Lerp(-.9f, .9f, n), Mathf.Sin(time * 8f) * .08f, 0f);
                ShowRole(0, travel, Quaternion.Euler(0f, 0f, Mathf.Sin(time * 12f) * 8f), new Vector3(span, .68f + Mathf.Sin(time * 12f) * .12f, 1f), FireCombustion, 1.2f, 0f);
                ShowRole(1, travel, Quaternion.identity, new Vector3(span * .42f, .26f, 1f), FireCombustion, 1.9f, 2f);
                ShowRole(2, travel + Vector3.left * trail * .45f, Quaternion.Euler(0f, 0f, 90f), new Vector3(.4f, trail * .5f, 1f), .52f * FireCombustion, .52f, 1f);
                AddArc(0, travel - Vector3.left * .05f, travel - Vector3.right * trail, 7, .09f, .7f * FireCombustion, .06f, Mathf.FloorToInt(time * 20f));
                var feathers = impact ? Mathf.RoundToInt(GetContentNumber("impact_feather_count", 10f)) : 5;
                AddRadialParticles(Mathf.Min(particleBudget, feathers), impact ? eventAge * 3.2f : .32f, impact ? eventAge * .6f : 0f, .06f, FireCombustion, 181);
            }
            else if (profile == ElementNextCandidateProfile.ChainBlast)
            {
                var count = Mathf.RoundToInt(GetContentNumber("blast_count", 3f)); var interval = GetContentNumber("interval", .12f); var scale = GetContentNumber("per_blast_scale", 1f);
                var safeInterval = Mathf.Max(.08f, interval); var sequence = Mathf.Clamp(Mathf.FloorToInt(time / safeInterval), 0, count - 1); var local = SequencePoint(sequence, count, GetContentText("spread_pattern", "triangle")); var age = Mathf.Max(0f, time - sequence * safeInterval);
                PrimaryCarrierMultiplicity = count; EventSequence = Mathf.Max(EventSequence, sequence + 1); FireCombustion = 1f - Mathf.Clamp01(age / interval); FireEruption = FireCombustion; FireResidue = 1f - Mathf.Clamp01(age / (safeInterval * 2f));
                Phase = ElementNextCandidatePhase.Eruption;
                ShowRole(0, local, Quaternion.identity, Vector3.one * scale * Mathf.Lerp(.3f, 1.25f, 1f - FireCombustion), FireCombustion, 1.25f, 0f);
                ShowRole(1, local, Quaternion.identity, Vector3.one * scale * Mathf.Lerp(.18f, .75f, 1f - FireCombustion), FireCombustion, 2f, 2f);
                ShowRole(3, local, Quaternion.identity, Vector3.one * scale, FireResidue * .65f, .55f, 3f);
                AddRadialParticles(Mathf.Min(particleBudget, 8), .18f + (1f - FireCombustion) * .55f, .05f, .055f, FireCombustion, 199 + sequence * 17);
            }
            else
            {
                var radius = GetContentNumber("shell_radius", 1.1f); var orbit = GetContentNumber("orbit_speed", 3f); var eventAge = elapsed - triggeredAt; var hit = eventAge >= 0f && eventAge <= .22f;
                FireCombustion = .82f + .18f * pulse; FireHeatHaze = .35f; FireEruption = hit ? 1f - eventAge / .22f : 0f;
                Phase = hit ? ElementNextCandidatePhase.Eruption : ElementNextCandidatePhase.Sustain;
                ShowRole(0, Vector3.zero, Quaternion.Euler(12f, time * orbit * 28f, time * orbit * 12f), Vector3.one * radius, .78f, .92f, 0f);
                ShowRole(1, Vector3.zero, Quaternion.Euler(22f, -time * orbit * 41f, 0f), Vector3.one * radius * .92f, .72f, 1.6f, 2f);
                ShowRole(2, Vector3.zero, Quaternion.Euler(-18f, time * orbit * 24f, 40f), Vector3.one * radius * 1.08f, .34f, .48f, 1f);
                if (hit) ShowRole(4, triggeredLocalPosition, Quaternion.identity, Vector3.one * GetContentNumber("hit_burst_scale", 1.2f) * (1f + eventAge * 2f), FireEruption, 2f, 2f);
                AddRadialParticles(Mathf.Min(particleBudget, hit ? 14 : 6), radius, .1f, .052f, hit ? FireEruption : .65f, 211);
            }
            FireEmberCount = particleCount;
        }

        private void EvaluateFrost(float time, float n)
        {
            IceCrystalGrowth = 0f; IceSharpness = 0f; IceMistOpacity = 0f; IceFractureCount = 0; IceMelt = 0f;
            if (profile != ElementNextCandidateProfile.CrystalShield) IceHitFlashColor = accent;
            if (profile == ElementNextCandidateProfile.IceSpike)
            {
                var count = Mathf.RoundToInt(GetContentNumber("spike_count", 5f)); var height = GetContentNumber("height", 1.5f); var grow = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.12f, .36f, n)); var exit = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.72f, 1f, n)); var shatter = GetContentText("exit_mode", "shatter") == "shatter";
                PrimaryCarrierMultiplicity = count; IceCrystalGrowth = grow * (shatter ? 1f : 1f - exit); IceSharpness = Mathf.Clamp01(.5f + height / 5f); IceMistOpacity = Mathf.Sin(Mathf.Clamp01(n / .38f) * Mathf.PI) * .72f; IceMelt = shatter ? 0f : exit;
                Phase = n < .15f ? ElementNextCandidatePhase.Anticipation : n < .72f ? ElementNextCandidatePhase.Growth : shatter ? ElementNextCandidatePhase.Fracture : ElementNextCandidatePhase.Melt;
                ShowRole(0, shatter ? Vector3.zero : Vector3.down * height * exit, Quaternion.identity, new Vector3(1f, Mathf.Max(.01f, IceCrystalGrowth) * height, 1f), 1f - exit, .95f, 3f);
                ShowRole(1, Vector3.up * height * .42f, Quaternion.identity, new Vector3(.68f, Mathf.Max(.01f, IceCrystalGrowth) * height * .75f, .68f), 1f - exit, 1.5f, 2f);
                ShowRole(2, Vector3.zero, Quaternion.Euler(68f, 0f, time * 9f), Vector3.one * (1f + grow * .5f), IceMistOpacity, .45f, 4f);
                if (shatter && exit > 0f) { IceFractureCount = Mathf.Min(particleBudget, count * 2); AddRadialParticles(IceFractureCount, exit * .85f, -.2f * exit, .075f, 1f - exit, 307); }
            }
            else if (profile == ElementNextCandidateProfile.Blizzard)
            {
                var radius = GetContentNumber("radius", 6f); var density = Mathf.RoundToInt(GetContentNumber("density", 86f)); var fog = GetContentNumber("fog_height", .8f); var direction = GetContentText("wind_dir", "north_east");
                IceCrystalGrowth = 1f; IceSharpness = .68f; IceMistOpacity = Mathf.Clamp01(.28f + fog / 4f); Phase = ElementNextCandidatePhase.Sustain;
                ShowRole(0, Vector3.zero, Quaternion.Euler(68f, 0f, direction == "west" ? 24f : -24f), Vector3.one * radius, .42f, .72f, 3f);
                ShowRole(2, Vector3.up * fog * .18f, Quaternion.Euler(68f, 0f, time * 4f), new Vector3(radius, Mathf.Max(.2f, fog), radius), IceMistOpacity, .42f, 4f);
                AddBlizzardParticles(Mathf.Min(particleBudget, density), radius, fog, time, direction, .052f, 1f, 331);
            }
            else if (profile == ElementNextCandidateProfile.FrostBreath)
            {
                var length = GetContentNumber("length", 4f); var angle = GetContentNumber("cone_angle", 52f); var density = Mathf.RoundToInt(GetContentNumber("crystal_density", 34f));
                IceCrystalGrowth = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, .18f, n)); IceSharpness = .72f; IceMistOpacity = .68f; Phase = n < .18f ? ElementNextCandidatePhase.Growth : ElementNextCandidatePhase.Sustain;
                ShowRole(0, Vector3.right * length * .5f, Quaternion.Euler(0f, 0f, -90f), new Vector3(angle / 52f, length * .5f, 1f), IceCrystalGrowth, .82f, 3f);
                ShowRole(1, Vector3.right * length * .18f, Quaternion.Euler(0f, 0f, -90f), new Vector3(.22f, length * .2f, 1f), IceCrystalGrowth, 1.45f, 2f);
                ShowRole(2, Vector3.right * length * .5f, Quaternion.Euler(0f, 0f, -90f), new Vector3(angle / 42f, length * .56f, 1f), IceMistOpacity, .4f, 4f);
                AddConeParticles(Math.Min(particleBudget, density), length, angle, .052f, IceCrystalGrowth, 347);
            }
            else if (profile == ElementNextCandidateProfile.IceShard)
            {
                var spin = GetContentNumber("spin_speed", 360f); var trail = GetContentNumber("trail_length", .8f); var variant = Mathf.RoundToInt(GetContentNumber("shard_variant", 2f)); var eventAge = elapsed - triggeredAt; var impact = eventAge >= 0f && eventAge <= .28f;
                var visibility = impact ? 1f - eventAge / .28f : 1f - Mathf.SmoothStep(.84f, 1f, n);
                IceCrystalGrowth = visibility; IceSharpness = .55f + variant * .1f; IceMistOpacity = .38f * visibility; IceFractureCount = impact ? Mathf.Min(particleBudget, 6 + variant * 2) : 0; Phase = impact ? ElementNextCandidatePhase.Fracture : ElementNextCandidatePhase.Sustain;
                var position = impact ? triggeredLocalPosition : new Vector3(Mathf.Lerp(-.9f, .9f, n), 0f, 0f);
                ShowRole(0, position, Quaternion.Euler(0f, 0f, time * spin), Vector3.one * (.65f + variant * .09f), visibility, .95f, 3f);
                ShowRole(1, position, Quaternion.Euler(0f, 0f, time * spin), Vector3.one * (.34f + variant * .05f), visibility, 1.5f, 2f);
                ShowRole(2, position + Vector3.left * trail * .5f, Quaternion.Euler(0f, 0f, 90f), new Vector3(.34f, trail * .5f, 1f), IceMistOpacity, .45f, 4f);
                if (impact) AddRadialParticles(IceFractureCount, eventAge * 3f, -.2f * eventAge, .07f, 1f - eventAge / .28f, 359);
            }
            else if (profile == ElementNextCandidateProfile.FreezeStatus)
            {
                var opacity = GetContentNumber("shell_opacity", .62f); var pieces = Mathf.RoundToInt(GetContentNumber("shatter_piece_count", 8f)); var growth = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, .25f, n));
                IceCrystalGrowth = growth; IceSharpness = .82f; IceMistOpacity = .48f; IceFractureCount = 0; Phase = n < .25f ? ElementNextCandidatePhase.Growth : ElementNextCandidatePhase.Sustain;
                ShowRole(0, Vector3.zero, Quaternion.identity, new Vector3(.82f, Mathf.Max(.02f, growth) * 1.2f, 1f), opacity * growth, .9f, 3f);
                ShowRole(1, Vector3.zero, Quaternion.identity, new Vector3(.72f, Mathf.Max(.02f, growth), 1f), growth, 1.35f, 2f);
                ShowRole(2, Vector3.down * .42f, Quaternion.Euler(68f, 0f, time * 8f), Vector3.one * (1f + growth * .35f), IceMistOpacity, .42f, 4f);
                AddRadialParticles(Mathf.Min(particleBudget, pieces / 2), .55f, -.1f + n * .2f, .05f, .6f, 373);
            }
            else if (profile == ElementNextCandidateProfile.CrystalShield)
            {
                var petals = Mathf.RoundToInt(GetContentNumber("petal_count", 6f)); var radius = GetContentNumber("orbit_radius", 1.1f); var eventAge = elapsed - triggeredAt; var hit = eventAge >= 0f && eventAge <= .22f;
                PrimaryCarrierMultiplicity = petals; IceCrystalGrowth = 1f; IceSharpness = .9f; IceMistOpacity = .3f; Phase = hit ? ElementNextCandidatePhase.Discharge : ElementNextCandidatePhase.Sustain;
                ShowRole(0, Vector3.zero, Quaternion.Euler(18f, time * 18f, 0f), Vector3.one * radius, .84f, .92f, 3f);
                ShowRole(1, Vector3.zero, Quaternion.Euler(-12f, -time * 29f, 20f), Vector3.one * radius * .86f, .9f, 1.45f, 2f);
                ShowRole(2, Vector3.zero, Quaternion.Euler(20f, time * 11f, -18f), Vector3.one * radius * 1.08f, IceMistOpacity, .42f, 4f);
                if (hit) ShowRoleWithColors(4, triggeredLocalPosition, Quaternion.identity, Vector3.one * Mathf.Lerp(.35f, 1.2f, eventAge / .22f), 1f - eventAge / .22f, 1.8f, 2f, IceHitFlashColor, Color.white, IceHitFlashColor);
                AddRadialParticles(Mathf.Min(particleBudget, petals), radius, .05f, .05f, hit ? 1f : .55f, 389);
            }
            else
            {
                var hold = GetContentNumber("freeze_duration", .45f); var rise = GetContentNumber("rise_speed", 3f); var shatterScale = GetContentNumber("shatter_scale", 1.2f); var growEnd = Mathf.Clamp(.08f + 1f / Mathf.Max(.1f, rise), .15f, .42f); var fractureStart = Mathf.Clamp01(growEnd + hold / Mathf.Max(.1f, duration)); var grow = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, growEnd, n)); var fracture = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(fractureStart, 1f, n));
                IceCrystalGrowth = grow * (1f - fracture); IceSharpness = .94f; IceMistOpacity = .45f * (1f - fracture); IceFractureCount = fracture > 0f ? Mathf.Min(particleBudget, Mathf.RoundToInt(10f * shatterScale)) : 0; Phase = n < growEnd ? ElementNextCandidatePhase.Growth : n < fractureStart ? ElementNextCandidatePhase.Sustain : ElementNextCandidatePhase.Fracture;
                ShowRole(0, Vector3.up * (grow - 1f) * .55f, Quaternion.identity, new Vector3(.85f, Mathf.Max(.02f, grow) * 1.4f, .85f), 1f - fracture, .95f, 3f);
                ShowRole(1, Vector3.up * .18f, Quaternion.identity, new Vector3(.68f, Mathf.Max(.02f, grow) * 1.18f, .68f), 1f - fracture, 1.55f, 2f);
                ShowRole(2, Vector3.zero, Quaternion.Euler(68f, 0f, time * 5f), Vector3.one * (1f + grow * .4f), IceMistOpacity, .4f, 4f);
                if (fracture > 0f) AddRadialParticles(IceFractureCount, fracture * shatterScale, -.25f * fracture, .08f, 1f - fracture, 401);
            }
        }

        private void EvaluateLightning(float time, float n)
        {
            LightningForkCount = 0; LightningFlashOn = false; LightningCharge = 0f; LightningDischarge = 0f; LightningAfterglow = 0f; LightningControlledFlashCount = 0;
            discreteStep = Mathf.FloorToInt(time * 30f);
            if (profile == ElementNextCandidateProfile.ThunderStrike)
            {
                var height = GetContentNumber("strike_height", 7f); var forks = Mathf.RoundToInt(GetContentNumber("fork_count", 2f)); var ground = Mathf.RoundToInt(GetContentNumber("ground_arc_count", 5f)); var flashes = Mathf.RoundToInt(GetContentNumber("flash_times", 2f));
                LightningCharge = Mathf.Clamp01(time / .05f); LightningControlledFlashCount = flashes; var flashWindow = time <= .18f; var pulseIndex = Mathf.FloorToInt(time / Mathf.Max(.02f, .18f / (flashes * 2f))); LightningFlashOn = flashWindow && (pulseIndex % 2 == 0 || time <= .055f); LightningDischarge = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.05f, .22f, time)); LightningAfterglow = time < .2f ? 0f : 1f - Mathf.Clamp01((time - .2f) / .4f); LightningForkCount = forks;
                Phase = time < .05f ? ElementNextCandidatePhase.Anticipation : time < .2f ? ElementNextCandidatePhase.Discharge : ElementNextCandidatePhase.Afterglow;
                if (LightningFlashOn)
                {
                    AddArc(0, Vector3.up * height, Vector3.zero, 9, .12f, 1f, .105f, discreteStep);
                    for (var index = 0; index < forks && index + 1 < MaxArcCarriers; index++)
                    {
                        var start = Vector3.up * Mathf.Lerp(height * .75f, height * .35f, index / Mathf.Max(1f, forks - 1f));
                        var end = start + new Vector3(Signed(index + 71, discreteStep) * (1.1f + index * .25f), -.65f - index * .18f, Signed(index + 97, discreteStep) * .35f);
                        AddArc(index + 1, start, end, 7, .11f, .82f, .055f, discreteStep + index * 31);
                    }
                }
                ShowRole(0, Vector3.zero, Quaternion.identity, Vector3.one * Mathf.Lerp(.2f, 1.5f, LightningDischarge), Mathf.Max(LightningAfterglow * .35f, LightningFlashOn ? 1f : 0f), LightningFlashOn ? 1.8f : .45f, 5f);
                AddRadialParticles(Mathf.Min(particleBudget, ground), Mathf.Lerp(.15f, 1.25f, LightningDischarge), .02f, .055f, Mathf.Max(LightningAfterglow, LightningFlashOn ? 1f : 0f), 503);
            }
            else if (profile == ElementNextCandidateProfile.BallLightning)
            {
                var radius = GetContentNumber("orb_radius", .55f); var tendrils = Mathf.RoundToInt(GetContentNumber("tendril_count", 4f)); var wobble = GetContentNumber("drift_wobble", .45f); var range = GetContentNumber("discharge_range", 3f); var eventAge = elapsed - triggeredAt; var impact = eventAge >= 0f && eventAge <= .28f;
                var visibility = impact ? 1f - eventAge / .28f : 1f - Mathf.SmoothStep(.84f, 1f, n);
                discreteStep = Mathf.FloorToInt(time * 24f); LightningCharge = visibility; LightningFlashOn = visibility > .001f && (discreteStep % 3) != 1; LightningForkCount = tendrils; LightningDischarge = (impact ? 1f - eventAge / .28f : .42f) * visibility; LightningAfterglow = (impact ? Mathf.Clamp01(eventAge / .28f) : .22f) * visibility;
                Phase = impact ? ElementNextCandidatePhase.Discharge : ElementNextCandidatePhase.Sustain;
                var center = impact ? triggeredLocalPosition : new Vector3(Mathf.Lerp(-.8f, .8f, n), Mathf.Sin(time * 4.1f) * wobble, Mathf.Cos(time * 3.4f) * wobble * .25f);
                ShowRole(0, center, Quaternion.identity, Vector3.one * radius * (1f + .08f * Signed(8, discreteStep)), .86f * visibility, 1.25f, 5f);
                for (var index = 0; index < tendrils && index < MaxArcCarriers; index++)
                {
                    var angle = Mathf.PI * 2f * (index + .17f * discreteStep) / Mathf.Max(1, tendrils);
                    var end = center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), Signed(index + 12, discreteStep) * .25f) * (impact ? range * .45f : radius * 1.8f);
                    AddArc(index, center, end, 6, .08f, (LightningFlashOn ? .85f : .24f) * visibility, .038f, discreteStep + index * 19);
                }
                AddRadialParticles(Mathf.Min(particleBudget, tendrils * 2), radius * 1.2f, 0f, .045f, .8f * visibility, 521);
            }
            else if (profile == ElementNextCandidateProfile.StaticField)
            {
                var radius = GetContentNumber("radius", 3f); var frequency = GetContentNumber("arc_frequency", .5f); var tick = GetContentNumber("tick_interval", .45f); var opacity = GetContentNumber("net_opacity", .45f);
                discreteStep = Mathf.FloorToInt(time / Mathf.Max(.05f, frequency)); var tickPhase = Mathf.Repeat(time, Mathf.Max(.05f, tick)) / Mathf.Max(.05f, tick); LightningCharge = opacity; LightningFlashOn = Mathf.Repeat(time, Mathf.Max(.05f, frequency)) < .13f; LightningDischarge = 1f - tickPhase; LightningAfterglow = opacity * .45f; LightningForkCount = LightningFlashOn ? 1 : 0;
                Phase = ElementNextCandidatePhase.Sustain;
                ShowRole(0, Vector3.zero, Quaternion.Euler(68f, 0f, time * 13f), Vector3.one * radius, opacity + (1f - tickPhase) * .3f, .82f, 5f);
                if (LightningFlashOn)
                {
                    var a = DeterministicPointOnCircle(radius * .82f, 0, discreteStep); var b = DeterministicPointOnCircle(radius * .82f, 1, discreteStep);
                    AddArc(0, a, b, 8, .12f, 1f, .055f, discreteStep);
                }
                AddRadialParticles(Mathf.Min(particleBudget, 14), radius * .82f, .08f, .045f, opacity, 547);
            }
            else if (profile == ElementNextCandidateProfile.StormCharge)
            {
                var cloudHeight = GetContentNumber("cloud_height", 1.8f); var swap = GetContentNumber("arc_swap_interval", .3f); var level = Mathf.RoundToInt(GetContentNumber("charge_level", 2f));
                discreteStep = Mathf.FloorToInt(time / Mathf.Max(.05f, swap)); LightningCharge = level / 3f; LightningFlashOn = (discreteStep % 3) != 2; LightningDischarge = .2f + level * .18f; LightningAfterglow = .2f; LightningForkCount = Mathf.Min(3, level);
                Phase = ElementNextCandidatePhase.Sustain;
                ShowRole(0, Vector3.up * cloudHeight, Quaternion.Euler(68f, 0f, time * 17f), new Vector3(.65f + level * .18f, .28f, .65f + level * .18f), .55f + level * .12f, .88f + level * .18f, 5f);
                for (var index = 0; index < LightningForkCount; index++)
                {
                    var start = Vector3.up * cloudHeight + DeterministicPointOnCircle(.45f + level * .08f, index, discreteStep);
                    var end = DeterministicPointOnCircle(.5f + level * .12f, index + 3, discreteStep) + Vector3.up * .1f;
                    AddArc(index, start, end, 8, .09f, LightningFlashOn ? .82f : .2f, .045f, discreteStep + index * 23);
                }
                AddRadialParticles(Mathf.Min(particleBudget, level * 5), .48f + level * .11f, cloudHeight * .4f, .045f, .75f, 563);
            }
            else if (profile == ElementNextCandidateProfile.ElectroSlash)
            {
                var jag = GetContentNumber("jag_amplitude", .35f); var afterimages = Mathf.RoundToInt(GetContentNumber("afterimage_count", 2f)); var sparks = Mathf.RoundToInt(GetContentNumber("spark_count", 8f));
                discreteStep = Mathf.FloorToInt(time * 30f); LightningControlledFlashCount = afterimages; var reveal = Mathf.Clamp01(n / .24f); var fade = 1f - Mathf.SmoothStep(.58f, 1f, n); var flashIndex = Mathf.FloorToInt(Mathf.InverseLerp(.48f, .94f, n) * afterimages * 2f); LightningFlashOn = n < .52f || (n < .94f && flashIndex % 2 == 0); LightningCharge = reveal; LightningDischarge = reveal * fade; LightningAfterglow = Mathf.Clamp01((n - .45f) / .55f) * fade; LightningForkCount = 1 + afterimages;
                Phase = n < .24f ? ElementNextCandidatePhase.Growth : n < .55f ? ElementNextCandidatePhase.Discharge : ElementNextCandidatePhase.Afterglow;
                ShowRole(0, Vector3.zero, Quaternion.identity, new Vector3(1.15f, .72f, 1f), LightningFlashOn ? fade : .08f, 1.3f, 5f);
                AddArc(0, new Vector3(-1f, -.35f), new Vector3(1f, .35f), 10, jag, LightningFlashOn ? 1f : .08f, .08f, discreteStep);
                for (var index = 0; index < afterimages && index + 1 < MaxArcCarriers; index++) AddArc(index + 1, new Vector3(-1f, -.35f - (index + 1) * .08f), new Vector3(1f, .35f - (index + 1) * .08f), 10, jag * .72f, LightningAfterglow * (1f - index * .2f), .045f, discreteStep - index - 1);
                AddRadialParticles(Mathf.Min(particleBudget, sparks), .35f + n * .55f, .05f, .05f, fade, 587);
            }
            else if (profile == ElementNextCandidateProfile.EmpNova)
            {
                var radius = GetContentNumber("ring_radius", 3f); var glitch = GetContentNumber("glitch_strength", .45f); var rings = Mathf.RoundToInt(GetContentNumber("ring_count", 2f));
                var visibility = 1f - Mathf.SmoothStep(.78f, 1f, n);
                discreteStep = Mathf.FloorToInt(time * (8f + glitch * 22f)); LightningCharge = (1f - n) * visibility; LightningDischarge = Mathf.Sin(n * Mathf.PI) * visibility; LightningAfterglow = (1f - n) * glitch; LightningFlashOn = visibility > .001f && discreteStep % 3 != 1; LightningControlledFlashCount = rings; PrimaryCarrierMultiplicity = rings;
                Phase = n < .2f ? ElementNextCandidatePhase.Anticipation : n < .72f ? ElementNextCandidatePhase.Discharge : ElementNextCandidatePhase.Afterglow;
                ShowRole(0, new Vector3(Signed(4, discreteStep) * glitch * .08f, 0f, 0f), Quaternion.identity, Vector3.one * Mathf.Max(.02f, radius * n), (LightningFlashOn ? 1f - n * .35f : .18f) * visibility, 1.1f + glitch, 5f);
                for (var index = 0; index < rings && index < MaxArcCarriers; index++) AddArc(index, DeterministicPointOnCircle(radius * n, index, discreteStep), DeterministicPointOnCircle(radius * n, index + 1, discreteStep), 7, .04f + glitch * .12f, (LightningFlashOn ? .7f : .12f) * visibility, .035f, discreteStep + index * 13);
                AddRadialParticles(Mathf.Min(particleBudget, rings * 5), radius * n, 0f, .045f, 1f - n, 601);
            }
            else
            {
                var density = GetContentNumber("net_density", 4f); var walk = Mathf.RoundToInt(GetContentNumber("walk_arc_count", 3f)); var counter = GetContentNumber("counter_arc", 1f) > .5f; var eventAge = elapsed - triggeredAt; var hit = eventAge >= 0f && eventAge <= .15f; var visibleWalk = Mathf.Min(MaxArcCarriers, walk);
                var counterVisible = hit && counter; var walkCarriers = counterVisible && visibleWalk == MaxArcCarriers ? visibleWalk - 1 : visibleWalk;
                discreteStep = Mathf.FloorToInt(time * (8f + density + walk * .35f)); LightningCharge = .72f; LightningFlashOn = discreteStep % 4 != 2; LightningDischarge = hit ? 1f - eventAge / .15f : .25f; LightningAfterglow = .28f; LightningForkCount = Mathf.Min(MaxArcCarriers, walkCarriers + (counterVisible ? 1 : 0)); PrimaryCarrierMultiplicity = walk;
                Phase = hit ? ElementNextCandidatePhase.Discharge : ElementNextCandidatePhase.Sustain;
                ShowRole(0, Vector3.zero, Quaternion.Euler(12f, time * 16f, 0f), Vector3.one * (1f + density * .035f), .58f + .04f * density, .9f, 5f);
                for (var index = 0; index < walkCarriers; index++)
                {
                    var logicalArc = walk <= visibleWalk ? index : (index + discreteStep * visibleWalk) % walk;
                    var a = DeterministicPointOnSphere(.82f, logicalArc, discreteStep); var b = DeterministicPointOnSphere(.82f, logicalArc + 7, discreteStep);
                    AddArc(index, a, b, 7, .08f, LightningFlashOn ? .74f : .18f, .04f, discreteStep + logicalArc * 29 + walk * 7);
                }
                if (counterVisible) AddArc(walkCarriers, triggeredLocalPosition, triggeredLocalPosition.normalized * 1.7f, 8, .13f, LightningDischarge, .065f, discreteStep + 401);
                AddRadialParticles(Mathf.Min(particleBudget, 6 + Mathf.RoundToInt(density)), .82f, 0f, .042f, .65f, 619);
            }
        }

        private void ClearFrame()
        {
            ActiveLayerCount = 0; PrimaryCarrierMultiplicity = 0; particleCount = 0; VisibleArcCount = 0; framePeakAlpha = 0f;
            if (roleRenderers != null) for (var index = 0; index < roleRenderers.Length; index++) if (roleRenderers[index] != null) roleRenderers[index].enabled = false;
            for (var index = 0; index < MaxArcCarriers; index++)
            {
                sampledArcPointCounts[index] = 0;
                if (arcCarriers != null && index < arcCarriers.Length && arcCarriers[index] != null) { arcCarriers[index].positionCount = 0; arcCarriers[index].enabled = false; }
            }
            if (detailParticleRenderer != null) detailParticleRenderer.enabled = false;
        }

        private void ShowRole(int role, Vector3 offset, Quaternion rotation, Vector3 scale, float alpha, float intensity, float carrierMode)
        {
            ShowRoleWithColors(role, offset, rotation, scale, alpha, intensity, carrierMode, primary, secondary, accent);
        }

        private void ShowRoleWithColors(int role, Vector3 offset, Quaternion rotation, Vector3 scale, float alpha, float intensity, float carrierMode, Color rolePrimary, Color roleSecondary, Color roleAccent)
        {
            if (alpha <= .001f || roleTransforms == null || role < 0 || role >= roleTransforms.Length) return;
            var item = roleTransforms[role]; var renderer = roleRenderers[role];
            if (item == null || renderer == null) return;
            item.localPosition = roleBasePositions[role] + offset;
            item.localRotation = roleBaseRotations[role] * rotation;
            item.localScale = Vector3.Scale(roleBaseScales[role], new Vector3(Mathf.Max(.001f, scale.x), Mathf.Max(.001f, scale.y), Mathf.Max(.001f, scale.z)));
            renderer.enabled = true;
            ApplyProperties(renderer, alpha, intensity, carrierMode, rolePrimary, roleSecondary, roleAccent);
            framePeakAlpha = Mathf.Max(framePeakAlpha, Mathf.Clamp01(alpha));
            ActiveLayerCount++;
        }

        private void AddArc(int index, Vector3 start, Vector3 end, int pointCount, float jaggedness, float alpha, float width, int sampleStep)
        {
            if (alpha <= .001f || arcCarriers == null || index < 0 || index >= arcCarriers.Length || index >= MaxArcCarriers) return;
            var line = arcCarriers[index]; if (line == null) return;
            pointCount = Mathf.Clamp(pointCount, 2, MaxArcPoints);
            var direction = end - start; var side = Vector3.Cross(direction.sqrMagnitude < .0001f ? Vector3.right : direction.normalized, Vector3.forward);
            if (side.sqrMagnitude < .001f) side = Vector3.right; side.Normalize();
            line.useWorldSpace = false; line.positionCount = pointCount; line.widthMultiplier = Mathf.Max(.004f, width); line.enabled = true;
            for (var point = 0; point < pointCount; point++)
            {
                var t = point / (float)(pointCount - 1); var envelope = Mathf.Sin(t * Mathf.PI); var hashOffset = Signed(index * 131 + point * 17 + 5, sampleStep); var depth = Signed(index * 47 + point * 29 + 11, sampleStep) * jaggedness * .28f * envelope;
                var value = Vector3.Lerp(start, end, t) + side * hashOffset * jaggedness * envelope + Vector3.forward * depth;
                line.SetPosition(point, value); sampledArcPoints[index, point] = value;
            }
            sampledArcPointCounts[index] = pointCount; ApplyProperties(line, alpha, 1.5f, 5f); VisibleArcCount++; ActiveLayerCount++;
            framePeakAlpha = Mathf.Max(framePeakAlpha, Mathf.Clamp01(alpha));
        }

        private void AddParticle(Vector3 position, float size, float alpha, int ordinal)
        {
            if (alpha <= .001f) return;
            if (particleCount >= Mathf.Min(particleBudget, AbsoluteMaxParticleCapacity)) return;
            var color = Color.Lerp(primary, accent, Hash01(seed + (uint)(ordinal * 31)));
            color.a = Mathf.Clamp01(alpha);
            particleBuffer[particleCount++] = new ParticleSystem.Particle
            {
                position = position,
                startSize = Mathf.Max(.005f, size),
                startColor = color,
                startLifetime = 1f,
                remainingLifetime = Mathf.Max(.01f, alpha)
            };
            framePeakAlpha = Mathf.Max(framePeakAlpha, Mathf.Clamp01(alpha));
        }

        private void AddRadialParticles(int count, float radius, float height, float size, float alpha, int salt)
        {
            for (var index = 0; index < count; index++)
            {
                var angle = Mathf.PI * 2f * (index + Hash01(seed + (uint)(salt + index * 7))) / Mathf.Max(1, count);
                var radial = radius * (.68f + .32f * Hash01(seed + (uint)(salt + index * 11)));
                AddParticle(new Vector3(Mathf.Cos(angle) * radial, Mathf.Sin(angle) * radial + height, Signed(salt + index, discreteStep) * .08f), size * (.75f + Hash01(seed + (uint)(salt + index * 13)) * .5f), alpha, salt + index);
            }
        }

        private void AddConeParticles(int count, float length, float angleDegrees, float size, float alpha, int salt)
        {
            var half = Mathf.Tan(angleDegrees * Mathf.Deg2Rad * .5f);
            for (var index = 0; index < count; index++)
            {
                var t = (index + .5f) / Mathf.Max(1, count); var x = length * t; var y = Signed(salt + index * 3, discreteStep) * half * x * .45f;
                AddParticle(new Vector3(x, y, Signed(salt + index * 5, discreteStep) * half * x * .18f), size, alpha, salt + index);
            }
        }

        private void AddRainParticles(int count, float radius, float time, float size, float alpha, int salt)
        {
            for (var index = 0; index < count; index++)
            {
                var x = Signed(salt + index * 5, 0) * radius; var z = Signed(salt + index * 7, 1) * radius; var phase = Mathf.Repeat(time * (1.1f + Hash01(seed + (uint)(salt + index)) * 1.2f) + Hash01(seed + (uint)(salt + index * 13)), 1f); var y = Mathf.Lerp(2.2f, .05f, phase);
                AddParticle(new Vector3(x, y, z), size, alpha, salt + index);
            }
        }

        private void AddBlizzardParticles(int count, float radius, float fogHeight, float time, string direction, float size, float alpha, int salt)
        {
            var sign = direction == "west" ? -1f : 1f;
            for (var index = 0; index < count; index++)
            {
                var phase = Mathf.Repeat(time * (1.2f + Hash01(seed + (uint)(salt + index)) * 1.4f) + Hash01(seed + (uint)(salt + index * 17)), 1f); var x = Mathf.Lerp(-radius, radius, phase) * sign; var y = Mathf.Lerp(radius * .45f + fogHeight, -.08f, phase) + Signed(salt + index * 3, 0) * .32f; var z = Signed(salt + index * 11, 1) * radius * .45f;
                AddParticle(new Vector3(x, y, z), size * (.75f + Hash01(seed + (uint)(index + salt)) * .6f), alpha, salt + index);
            }
        }

        private void CommitParticles()
        {
            if (detailParticles == null) return;
            detailParticles.SetParticles(particleBuffer, particleCount);
            if (detailParticleRenderer != null)
            {
                detailParticleRenderer.enabled = particleCount > 0;
                if (particleCount > 0) { ApplyProperties(detailParticleRenderer, 1f, 1.15f, ParticleCarrierMode()); ActiveLayerCount++; }
            }
        }

        private void ApplyProperties(Renderer renderer, float alpha, float intensity, float carrierMode)
        {
            ApplyProperties(renderer, alpha, intensity, carrierMode, primary, secondary, accent);
        }

        private void ApplyProperties(Renderer renderer, float alpha, float intensity, float carrierMode, Color rolePrimary, Color roleSecondary, Color roleAccent)
        {
            renderer.GetPropertyBlock(Block);
            Block.SetColor("_PrimaryColor", rolePrimary); Block.SetColor("_SecondaryColor", roleSecondary); Block.SetColor("_AccentColor", roleAccent);
            Block.SetFloat("_GlobalAlpha", Mathf.Clamp01(alpha)); Block.SetFloat("_Intensity", Mathf.Max(0f, intensity)); Block.SetFloat("_Phase", duration <= 0f ? 0f : Mathf.Repeat(elapsed / duration, 1f));
            Block.SetFloat("_FamilyMode", (float)family); Block.SetFloat("_CarrierMode", carrierMode); Block.SetFloat("_SemanticProgress", SemanticProgress()); Block.SetFloat("_Seed", seed & 65535u);
            renderer.SetPropertyBlock(Block);
        }

        private void CaptureDefaults()
        {
            if (defaultsCaptured) return;
            roleTransforms = new[] { primaryCarrier, highlightCarrier, outerCarrier, residualCarrier, eventCarrier };
            roleRenderers = new[] { primaryRenderer, highlightRenderer, outerRenderer, residualRenderer, eventRenderer };
            roleBasePositions = new Vector3[roleTransforms.Length]; roleBaseRotations = new Quaternion[roleTransforms.Length]; roleBaseScales = new Vector3[roleTransforms.Length];
            for (var index = 0; index < roleTransforms.Length; index++) if (roleTransforms[index] != null)
            {
                roleBasePositions[index] = roleTransforms[index].localPosition; roleBaseRotations[index] = roleTransforms[index].localRotation; roleBaseScales[index] = roleTransforms[index].localScale;
            }
            defaultsCaptured = true;
        }

        private void RestoreRoles()
        {
            CaptureDefaults();
            for (var index = 0; index < roleTransforms.Length; index++) if (roleTransforms[index] != null)
            {
                roleTransforms[index].localPosition = roleBasePositions[index]; roleTransforms[index].localRotation = roleBaseRotations[index]; roleTransforms[index].localScale = roleBaseScales[index];
            }
        }

        private void CompleteReset()
        {
            CaptureDefaults(); playing = false; stopping = false; elapsed = 0f; stopElapsed = 0f; triggeredAt = float.PositiveInfinity; triggeredLocalPosition = Vector3.zero; Phase = ElementNextCandidatePhase.Hidden; EventSequence = 0; framePeakAlpha = 0f; tailStartStrength = 0f;
            FireCombustion = FireEruption = FireHeatHaze = FireResidue = 0f; FireEmberCount = 0;
            FireFuelColor = Color.clear;
            IceCrystalGrowth = IceSharpness = IceMistOpacity = IceMelt = 0f; IceFractureCount = 0; IceHitFlashColor = Color.clear;
            LightningForkCount = 0; LightningFlashOn = false; LightningCharge = LightningDischarge = LightningAfterglow = 0f; LightningControlledFlashCount = 0; discreteStep = 0;
            ResetW6W8Readback();
            ClearFrame(); RestoreRoles();
            if (detailParticles != null) detailParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private int CountDistinctMaterials()
        {
            var materials = new Material[AbsoluteMaxRendererCount]; var count = 0;
            if (ownedRenderers == null) return 0;
            for (var index = 0; index < ownedRenderers.Length; index++)
            {
                var material = ownedRenderers[index] == null ? null : ownedRenderers[index].sharedMaterial; if (material == null) continue;
                var found = false; for (var m = 0; m < count; m++) if (materials[m] == material) { found = true; break; }
                if (!found && count < materials.Length) materials[count++] = material;
            }
            return count;
        }

        private Vector3 SequencePoint(int index, int count, string pattern)
        {
            if (pattern == "triangle")
            {
                if (index <= 0) return new Vector3(-.65f, -.2f);
                if (index == 1) return new Vector3(.65f, -.2f);
                if (index == 2) return new Vector3(0f, .55f);
                return new Vector3(0f, -.55f);
            }
            return new Vector3(Mathf.Lerp(-.75f, .75f, count <= 1 ? .5f : index / (float)(count - 1)), 0f, 0f);
        }

        private Vector3 DeterministicPointOnCircle(float radius, int ordinal, int step)
        {
            var angle = Hash01(seed + (uint)(ordinal * 131 + step * 17)) * Mathf.PI * 2f;
            return new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;
        }

        private Vector3 DeterministicPointOnSphere(float radius, int ordinal, int step)
        {
            var u = Hash01(seed + (uint)(ordinal * 97 + step * 29)); var v = Hash01(seed + (uint)(ordinal * 149 + step * 13)); var theta = u * Mathf.PI * 2f; var y = v * 2f - 1f; var radial = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
            return new Vector3(Mathf.Cos(theta) * radial, y, Mathf.Sin(theta) * radial) * radius;
        }

        private float Signed(int salt, int step) { return Hash01(seed + (uint)(salt * 374761393) + (uint)(step * 668265263)) * 2f - 1f; }

        private Color GetContentColor(string key, Color fallback)
        {
            Color value;
            return ColorUtility.TryParseHtmlString(GetContentText(key, string.Empty), out value) ? value : fallback;
        }

        private static float Hash01(uint value)
        {
            unchecked { value ^= value >> 16; value *= 0x7feb352du; value ^= value >> 15; value *= 0x846ca68bu; value ^= value >> 16; return (value & 0x00ffffffu) / 16777215f; }
        }
    }
}

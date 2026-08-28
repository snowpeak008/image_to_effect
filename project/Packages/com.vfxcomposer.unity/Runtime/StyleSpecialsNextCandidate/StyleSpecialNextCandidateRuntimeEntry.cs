using System;
using System.Linq;
using UnityEngine;

namespace VFXComposer
{
    public enum StyleSpecialCandidateGroup
    {
        W9Style2D,
        W10Style3D,
        W16StylePack2
    }

    public enum StyleSpecialMotionProfile
    {
        PixelSequence,
        CelSequence,
        InkBleed,
        Explosion,
        SustainedPlume,
        MuzzleFlash,
        HoloBarrier,
        HoloScan,
        GlitchBlink,
        RitualSummon,
        SoulDrain,
        DemonEruption,
        FacetBurst,
        LanceFlight,
        CandyBounce,
        CosmicOrbit,
        SteamBurst,
        GhostPulse
    }

    public enum StyleSpecialLifecyclePhase
    {
        Idle,
        Anticipation,
        MaterialHit,
        Sustain,
        Dissolve
    }

    public struct StyleSpecialBudgetSnapshot
    {
        public int GameObjects;
        public int Renderers;
        public int ParticleSystems;
        public int Materials;
    }

    /// <summary>
    /// Runtime-only visual executor for the W9/W10/W16 next-candidate. It owns a fixed set of
    /// real Mesh/Line renderers, writes the selected shared style Material through MPBs and keeps
    /// every carrier inside one declared review envelope. No label or Preview object participates
    /// in the visual semantics.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StyleSpecialNextCandidateRuntimeEntry : MonoBehaviour, IVfxRuntimeEntry
    {
        public const int MaxGameObjectsBudget = 9;
        public const int MaxRenderersBudget = 7;
        public const int MaxParticleSystemsBudget = 0;
        public const int MaxMaterialsBudget = 1;
        public static readonly Bounds UniformLocalEnvelope = new Bounds(new Vector3(0f, .02f, 0f), new Vector3(1.92f, 1.24f, .72f));

        [SerializeField] private string effectId = string.Empty;
        [SerializeField] private StyleSpecialCandidateGroup group;
        [SerializeField] private StyleSpecialMotionProfile motionProfile;
        [SerializeField] private string styleToken = "stylized";
        [SerializeField] private string semanticCode = string.Empty;
        [SerializeField] private string pairFamily = string.Empty;
        [SerializeField] private string pairRole = string.Empty;
        [SerializeField] private string sourceBaseId = string.Empty;
        [SerializeField] private string visualSignature = string.Empty;
        [SerializeField, Min(.12f)] private float duration = 1.4f;
        [SerializeField, Range(0f, .8f)] private float releaseNormalized = .18f;
        [SerializeField, Range(.2f, .95f)] private float sustainEndNormalized = .72f;
        [SerializeField] private bool sustained;
        [SerializeField] private uint seed = 1;
        [SerializeField] private Color primary = Color.white;
        [SerializeField] private Color secondary = Color.cyan;
        [SerializeField] private Color accent = Color.white;
        [SerializeField, Min(.1f)] private float baseIntensity = 1f;
        [SerializeField] private Bounds declaredLocalBounds = new Bounds(new Vector3(0f, .02f, 0f), new Vector3(1.92f, 1.24f, .72f));
        [SerializeField] private Renderer[] visualRenderers = new Renderer[0];
        [SerializeField] private Transform[] animatedCarriers = new Transform[0];
        [SerializeField] private LineRenderer semanticLine;

        private Vector3[] basePositions = new Vector3[0];
        private Quaternion[] baseRotations = new Quaternion[0];
        private Vector3[] baseScales = new Vector3[0];
        private MaterialPropertyBlock propertyBlock;
        private float elapsed;
        private float previousCycleTime;
        private bool playing;
        private bool hitObservedThisCycle;
        private StyleSpecialLifecyclePhase currentPhase = StyleSpecialLifecyclePhase.Idle;
        private int phaseTransitionCount;
        private int materialHitCount;
        private int replayCount;
        private int sustainedCycleCount;
        private int peakVisibleRendererCount;
        private float lastMaterialPhase;
        private float lastMaterialIntensity;
        private float lastMaterialAlpha;
        private VfxStopMode lastStopMode;

        public bool IsAlive { get { return playing; } }
        public string EffectId { get { return effectId; } }
        public StyleSpecialCandidateGroup Group { get { return group; } }
        public StyleSpecialMotionProfile MotionProfile { get { return motionProfile; } }
        public string StyleToken { get { return styleToken; } }
        public string SemanticCode { get { return semanticCode; } }
        public string PairFamily { get { return pairFamily; } }
        public string PairRole { get { return pairRole; } }
        public string SourceBaseId { get { return sourceBaseId; } }
        public string VisualSignature { get { return visualSignature; } }
        public string CombinationSignature { get { return styleToken + "|" + pairFamily + "|" + pairRole + "|" + sourceBaseId + "|" + semanticCode; } }
        public float Duration { get { return duration; } }
        public float ReleaseNormalized { get { return releaseNormalized; } }
        public bool Sustained { get { return sustained; } }
        public Bounds DeclaredLocalBounds { get { return declaredLocalBounds; } }
        public StyleSpecialLifecyclePhase CurrentPhase { get { return currentPhase; } }
        public int PhaseTransitionCount { get { return phaseTransitionCount; } }
        public int MaterialHitCount { get { return materialHitCount; } }
        public int ReplayCount { get { return replayCount; } }
        public int SustainedCycleCount { get { return sustainedCycleCount; } }
        public int PeakVisibleRendererCount { get { return peakVisibleRendererCount; } }
        public int VisibleRendererCount { get { return visualRenderers == null ? 0 : visualRenderers.Count(value => value != null && value.enabled); } }
        public int SemanticLinePointCount { get { return semanticLine == null || !semanticLine.enabled ? 0 : semanticLine.positionCount; } }
        public float LastMaterialPhase { get { return lastMaterialPhase; } }
        public float LastMaterialIntensity { get { return lastMaterialIntensity; } }
        public float LastMaterialAlpha { get { return lastMaterialAlpha; } }
        public VfxStopMode LastStopMode { get { return lastStopMode; } }

        private void Awake()
        {
            CacheBaseTransforms();
            ResetForPool();
        }

        private void Update()
        {
            if (!playing) return;
            elapsed += Mathf.Max(0f, Time.deltaTime);
            if (sustained)
            {
                var cycleTime = Mathf.Repeat(elapsed, duration);
                if (cycleTime + .0001f < previousCycleTime)
                {
                    sustainedCycleCount++;
                    hitObservedThisCycle = false;
                }
                previousCycleTime = cycleTime;
                ApplyAtTime(cycleTime);
            }
            else
            {
                ApplyAtTime(Mathf.Min(elapsed, duration));
                if (elapsed >= duration) Stop(VfxStopMode.Immediate);
            }
        }

        public void Initialize(VfxRuntimeContext context)
        {
            transform.SetPositionAndRotation(context.Position, context.Rotation);
            ResetForPool();
        }

        public void Play()
        {
            if (basePositions.Length != (animatedCarriers == null ? 0 : animatedCarriers.Length)) CacheBaseTransforms();
            RestoreBaseTransforms();
            DisableAllRenderers();
            elapsed = 0f;
            previousCycleTime = 0f;
            hitObservedThisCycle = false;
            currentPhase = StyleSpecialLifecyclePhase.Idle;
            phaseTransitionCount = 0;
            materialHitCount = 0;
            sustainedCycleCount = 0;
            peakVisibleRendererCount = 0;
            playing = true;
            replayCount++;
            ApplyAtTime(0f);
        }

        public bool SendEvent(string eventId, VfxRuntimeEvent payload)
        {
            if (eventId == "play" || eventId == "start" || eventId == "trigger" || eventId == "hit")
            {
                transform.SetPositionAndRotation(payload.Position, payload.Rotation);
                Play();
                return true;
            }
            if (eventId == "stop" || eventId == "cancel")
            {
                Stop(VfxStopMode.Immediate);
                return true;
            }
            if (eventId == "reset")
            {
                ResetForPool();
                return true;
            }
            return false;
        }

        public void Stop(VfxStopMode mode)
        {
            playing = false;
            lastStopMode = mode;
            currentPhase = StyleSpecialLifecyclePhase.Idle;
            DisableAllRenderers();
        }

        public void ResetForPool()
        {
            playing = false;
            elapsed = 0f;
            previousCycleTime = 0f;
            hitObservedThisCycle = false;
            currentPhase = StyleSpecialLifecyclePhase.Idle;
            phaseTransitionCount = 0;
            materialHitCount = 0;
            sustainedCycleCount = 0;
            peakVisibleRendererCount = 0;
            lastMaterialPhase = 0f;
            lastMaterialIntensity = 0f;
            lastMaterialAlpha = 0f;
            DisableAllRenderers();
            RestoreBaseTransforms();
            ClearPropertyBlocks();
        }

        public StyleSpecialBudgetSnapshot ReadBudget()
        {
            var renderers = GetComponentsInChildren<Renderer>(true);
            return new StyleSpecialBudgetSnapshot
            {
                GameObjects = GetComponentsInChildren<Transform>(true).Length,
                Renderers = renderers.Length,
                ParticleSystems = GetComponentsInChildren<ParticleSystem>(true).Length,
                Materials = renderers.SelectMany(value => value.sharedMaterials).Where(value => value != null).Distinct().Count()
            };
        }

        public bool TryGetCurrentLocalBounds(out Bounds bounds)
        {
            bounds = new Bounds();
            var initialized = false;
            if (visualRenderers == null) return false;
            for (var index = 0; index < visualRenderers.Length; index++)
            {
                var renderer = visualRenderers[index];
                if (renderer == null || !renderer.enabled) continue;
                var world = renderer.bounds;
                for (var corner = 0; corner < 8; corner++)
                {
                    var worldPoint = new Vector3((corner & 1) == 0 ? world.min.x : world.max.x, (corner & 2) == 0 ? world.min.y : world.max.y, (corner & 4) == 0 ? world.min.z : world.max.z);
                    var localPoint = transform.InverseTransformPoint(worldPoint);
                    if (!initialized)
                    {
                        bounds = new Bounds(localPoint, Vector3.zero);
                        initialized = true;
                    }
                    else bounds.Encapsulate(localPoint);
                }
            }
            return initialized;
        }

        public bool IsInsideDeclaredEnvelope(float epsilon)
        {
            Bounds current;
            if (!TryGetCurrentLocalBounds(out current)) return !playing;
            var amount = Mathf.Max(0f, epsilon);
            var min = declaredLocalBounds.min - Vector3.one * amount;
            var max = declaredLocalBounds.max + Vector3.one * amount;
            return current.min.x >= min.x && current.min.y >= min.y && current.min.z >= min.z && current.max.x <= max.x && current.max.y <= max.y && current.max.z <= max.z;
        }

        private void ApplyAtTime(float time)
        {
            var normalized = Mathf.Clamp01(time / Mathf.Max(.12f, duration));
            var nextPhase = EvaluateLifecyclePhase(normalized);
            if (nextPhase != currentPhase)
            {
                currentPhase = nextPhase;
                phaseTransitionCount++;
            }
            if (!hitObservedThisCycle && normalized >= releaseNormalized)
            {
                hitObservedThisCycle = true;
                materialHitCount++;
            }

            RestoreBaseTransforms();
            DisableAllRenderers();
            var materialPhase = normalized;
            var alpha = EntranceExitEnvelope(normalized);
            var intensity = baseIntensity;

            if (motionProfile == StyleSpecialMotionProfile.PixelSequence) ApplyPixelSequence(time, normalized, ref materialPhase, ref intensity);
            else if (motionProfile == StyleSpecialMotionProfile.CelSequence) ApplyCelSequence(time, normalized, ref materialPhase, ref intensity);
            else if (motionProfile == StyleSpecialMotionProfile.InkBleed) ApplyInkBleed(time, normalized, ref materialPhase, ref intensity);
            else if (motionProfile == StyleSpecialMotionProfile.Explosion) ApplyExplosion(time, normalized, ref intensity);
            else if (motionProfile == StyleSpecialMotionProfile.SustainedPlume) ApplyPlume(time, normalized, ref intensity);
            else if (motionProfile == StyleSpecialMotionProfile.MuzzleFlash) ApplyMuzzleFlash(time, normalized, ref intensity);
            else if (motionProfile == StyleSpecialMotionProfile.HoloBarrier) ApplyHoloBarrier(time, normalized, ref intensity);
            else if (motionProfile == StyleSpecialMotionProfile.HoloScan) ApplyHoloScan(time, normalized, ref intensity);
            else if (motionProfile == StyleSpecialMotionProfile.GlitchBlink) ApplyGlitchBlink(time, normalized, ref materialPhase, ref intensity);
            else if (motionProfile == StyleSpecialMotionProfile.RitualSummon) ApplyRitual(time, normalized, ref intensity);
            else if (motionProfile == StyleSpecialMotionProfile.SoulDrain) ApplySoulDrain(time, normalized, ref intensity);
            else if (motionProfile == StyleSpecialMotionProfile.DemonEruption) ApplyDemonEruption(time, normalized, ref intensity);
            else if (motionProfile == StyleSpecialMotionProfile.FacetBurst) ApplyFacetBurst(time, normalized, ref intensity);
            else if (motionProfile == StyleSpecialMotionProfile.LanceFlight) ApplyLanceFlight(time, normalized, ref intensity);
            else if (motionProfile == StyleSpecialMotionProfile.CandyBounce) ApplyCandyBounce(time, normalized, ref intensity);
            else if (motionProfile == StyleSpecialMotionProfile.CosmicOrbit) ApplyCosmicOrbit(time, normalized, ref intensity);
            else if (motionProfile == StyleSpecialMotionProfile.SteamBurst) ApplySteamBurst(time, normalized, ref materialPhase, ref intensity);
            else ApplyGhostPulse(time, normalized, ref intensity, ref alpha);

            peakVisibleRendererCount = Mathf.Max(peakVisibleRendererCount, VisibleRendererCount);
            ApplyMaterialState(alpha, materialPhase, intensity);
        }

        private StyleSpecialLifecyclePhase EvaluateLifecyclePhase(float normalized)
        {
            if (normalized < releaseNormalized) return StyleSpecialLifecyclePhase.Anticipation;
            var hitEnd = Mathf.Min(sustainEndNormalized, releaseNormalized + .2f);
            if (normalized < hitEnd) return StyleSpecialLifecyclePhase.MaterialHit;
            if (normalized < sustainEndNormalized) return StyleSpecialLifecyclePhase.Sustain;
            return StyleSpecialLifecyclePhase.Dissolve;
        }

        private void ApplyPixelSequence(float time, float normalized, ref float materialPhase, ref float intensity)
        {
            var frame = Mathf.Clamp(Mathf.FloorToInt(time * 12f), 0, 11);
            materialPhase = frame / 11f;
            var visible = 1 + frame % Mathf.Max(1, Mathf.Min(4, CarrierCount));
            EnableFirst(visible);
            for (var index = 0; index < CarrierCount; index++)
            {
                var carrier = animatedCarriers[index];
                if (carrier == null) continue;
                var snapped = Mathf.Round((index - 2f) * .17f * 64f) / 64f;
                carrier.localPosition = basePositions[index] + new Vector3(snapped, ((frame + index) % 3 - 1) * .025f, 0f);
                var scale = .72f + ((frame + index) % 4) * .09f;
                carrier.localScale = baseScales[index] * scale;
            }
            intensity *= (frame & 1) == 0 ? .82f : 1.12f;
        }

        private void ApplyCelSequence(float time, float normalized, ref float materialPhase, ref float intensity)
        {
            var frame = Mathf.Clamp(Mathf.FloorToInt(time * 18f), 0, 15);
            materialPhase = frame / 15f;
            EnableFirst(Mathf.Min(CarrierCount, 2 + frame % 4));
            for (var index = 0; index < CarrierCount; index++)
            {
                var squash = 1f + Mathf.Sin((frame + index) * .9f) * .13f;
                animatedCarriers[index].localScale = Vector3.Scale(baseScales[index], new Vector3(1f / squash, squash, 1f));
                animatedCarriers[index].localRotation = baseRotations[index] * Quaternion.Euler(0f, 0f, (frame - 4) * (index + 1) * 3f);
            }
            intensity *= .94f + .13f * (frame % 3);
        }

        private void ApplyInkBleed(float time, float normalized, ref float materialPhase, ref float intensity)
        {
            materialPhase = Mathf.SmoothStep(0f, 1f, normalized * normalized);
            EnableFirst(CarrierCount);
            for (var index = 0; index < CarrierCount; index++)
            {
                var bleed = 1f + materialPhase * (.08f + index * .035f);
                animatedCarriers[index].localScale = Vector3.Scale(baseScales[index], new Vector3(bleed, Mathf.Lerp(.72f, 1.08f, materialPhase), 1f));
                animatedCarriers[index].localPosition = basePositions[index] + new Vector3(-materialPhase * index * .018f, Mathf.Sin(time * 2.2f + index) * .025f, 0f);
            }
            intensity *= Mathf.Lerp(1.08f, .62f, normalized);
        }

        private void ApplyExplosion(float time, float normalized, ref float intensity)
        {
            EnableFirst(normalized < .62f ? CarrierCount : Mathf.Min(3, CarrierCount));
            for (var index = 0; index < CarrierCount; index++)
            {
                var direction = RadialDirection(index);
                var travel = Mathf.SmoothStep(0f, 1f, normalized) * (.12f + index * .035f);
                animatedCarriers[index].localPosition = basePositions[index] + direction * travel + Vector3.up * Mathf.Max(0f, normalized - .28f) * index * .025f;
                animatedCarriers[index].localScale = baseScales[index] * Mathf.Lerp(.35f, index < 3 ? 1.28f : .84f, Mathf.Sin(normalized * Mathf.PI));
            }
            intensity *= normalized < .16f ? 1.45f : Mathf.Lerp(1.1f, .58f, normalized);
        }

        private void ApplyPlume(float time, float normalized, ref float intensity)
        {
            EnableFirst(CarrierCount);
            for (var index = 0; index < CarrierCount; index++)
            {
                var lift = Mathf.Repeat(normalized + index / (float)Mathf.Max(1, CarrierCount), 1f);
                animatedCarriers[index].localPosition = basePositions[index] + new Vector3(Mathf.Sin(time * .9f + index) * .09f, lift * .36f - .16f, 0f);
                animatedCarriers[index].localScale = baseScales[index] * Mathf.Lerp(.58f, 1.12f, lift);
            }
            intensity *= .76f + .16f * Mathf.Sin(time * 2.3f);
        }

        private void ApplyMuzzleFlash(float time, float normalized, ref float intensity)
        {
            var open = normalized < .5f;
            EnableFirst(open ? CarrierCount : 0);
            for (var index = 0; index < CarrierCount; index++) animatedCarriers[index].localScale = baseScales[index] * Mathf.Lerp(1.35f, .15f, Mathf.Clamp01(normalized * 2f));
            intensity *= open ? 1.6f : 0f;
        }

        private void ApplyHoloBarrier(float time, float normalized, ref float intensity)
        {
            EnableFirst(CarrierCount);
            for (var index = 0; index < CarrierCount; index++)
            {
                var hit = Mathf.Exp(-Mathf.Pow((normalized - releaseNormalized) * 18f, 2f));
                animatedCarriers[index].localScale = baseScales[index] * (1f + hit * (.08f + index * .025f));
                animatedCarriers[index].localPosition = basePositions[index] + Vector3.right * DeterministicSignedStep(time, index) * .016f;
            }
            intensity *= .82f + Mathf.Abs(Mathf.Sin(time * 9f)) * .35f;
        }

        private void ApplyHoloScan(float time, float normalized, ref float intensity)
        {
            EnableFirst(CarrierCount);
            for (var index = 0; index < CarrierCount; index++)
            {
                var scan = Mathf.Repeat(normalized * 1.4f + index * .19f, 1f);
                animatedCarriers[index].localScale = baseScales[index] * Mathf.Lerp(.25f, 1.35f, scan);
                animatedCarriers[index].localRotation = baseRotations[index] * Quaternion.Euler(0f, 0f, time * (30f + index * 12f));
            }
            intensity *= .76f + .3f * Mathf.Abs(Mathf.Sin(time * 7f));
        }

        private void ApplyGlitchBlink(float time, float normalized, ref float materialPhase, ref float intensity)
        {
            materialPhase = Mathf.Floor(normalized * 14f) / 14f;
            var hidden = normalized > .32f && normalized < .58f;
            EnableFirst(hidden ? Mathf.Min(2, CarrierCount) : CarrierCount);
            for (var index = 0; index < CarrierCount; index++)
            {
                var side = normalized < .5f ? -1f : 1f;
                animatedCarriers[index].localPosition = basePositions[index] + Vector3.right * side * Mathf.Abs(normalized - .5f) * .38f + Vector3.up * DeterministicSignedStep(time, index) * .055f;
                animatedCarriers[index].localScale = Vector3.Scale(baseScales[index], new Vector3(hidden ? .18f : 1f, 1f, 1f));
            }
            intensity *= hidden ? .66f : 1.15f;
        }

        private void ApplyRitual(float time, float normalized, ref float intensity)
        {
            var sequential = Mathf.Clamp(1 + Mathf.FloorToInt(normalized * CarrierCount * 1.4f), 1, CarrierCount);
            EnableFirst(sequential);
            for (var index = 0; index < CarrierCount; index++)
            {
                animatedCarriers[index].localRotation = baseRotations[index] * Quaternion.Euler(0f, 0f, time * ((index & 1) == 0 ? 34f : -25f));
                animatedCarriers[index].localScale = baseScales[index] * Mathf.SmoothStep(.2f, 1f, Mathf.Clamp01(normalized * 3f - index * .12f));
            }
            intensity *= .72f + .42f * Mathf.Sin(time * 3.1f) * Mathf.Sin(time * 3.1f);
        }

        private void ApplySoulDrain(float time, float normalized, ref float intensity)
        {
            EnableFirst(CarrierCount);
            if (semanticLine != null)
            {
                semanticLine.enabled = true;
                semanticLine.positionCount = 9;
                for (var point = 0; point < semanticLine.positionCount; point++)
                {
                    var ratio = point / (float)(semanticLine.positionCount - 1);
                    semanticLine.SetPosition(point, new Vector3(Mathf.Lerp(-.78f, .78f, ratio), Mathf.Sin(ratio * Mathf.PI * 3f + time * 7f) * .08f, 0f));
                }
                semanticLine.startWidth = .055f;
                semanticLine.endWidth = .025f;
            }
            // Carrier 0 owns the full nine-point beam and must remain centered; only the
            // following wisp carriers travel from target back toward the caster.
            for (var index = 1; index < CarrierCount; index++)
            {
                var flow = Mathf.Repeat(1f - normalized - index * .18f, 1f);
                animatedCarriers[index].localPosition = basePositions[index] + new Vector3(Mathf.Lerp(.62f, -.62f, flow), Mathf.Sin(flow * Mathf.PI) * .14f, 0f);
            }
            intensity *= .84f + .24f * Mathf.Sin(time * 5f) * Mathf.Sin(time * 5f);
        }

        private void ApplyDemonEruption(float time, float normalized, ref float intensity)
        {
            EnableFirst(CarrierCount);
            for (var index = 0; index < CarrierCount; index++)
            {
                var rise = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.08f + index * .035f, .58f, normalized));
                animatedCarriers[index].localPosition = basePositions[index] + Vector3.up * Mathf.Lerp(-.28f, .12f + index * .025f, rise);
                animatedCarriers[index].localScale = Vector3.Scale(baseScales[index], new Vector3(1f + rise * .12f, Mathf.Max(.08f, rise), 1f));
            }
            intensity *= normalized < .22f ? .62f : 1.2f - normalized * .36f;
        }

        private void ApplyFacetBurst(float time, float normalized, ref float intensity)
        {
            EnableFirst(CarrierCount);
            for (var index = 0; index < CarrierCount; index++)
            {
                var direction = RadialDirection(index);
                var distance = Mathf.Sin(normalized * Mathf.PI) * (.18f + index * .035f);
                animatedCarriers[index].localPosition = basePositions[index] + direction * distance;
                animatedCarriers[index].localRotation = baseRotations[index] * Quaternion.Euler(time * (36f + index * 8f), time * (29f + index * 7f), time * 20f);
            }
            intensity *= 1.02f;
        }

        private void ApplyLanceFlight(float time, float normalized, ref float intensity)
        {
            EnableFirst(CarrierCount);
            var x = Mathf.Lerp(-.58f, .58f, normalized);
            for (var index = 0; index < CarrierCount; index++)
            {
                animatedCarriers[index].localPosition = basePositions[index] + new Vector3(x - index * .08f, Mathf.Sin(time * 6f + index) * .025f, 0f);
                animatedCarriers[index].localRotation = baseRotations[index] * Quaternion.Euler(0f, 0f, -90f);
            }
            intensity *= .92f + .18f * Mathf.Sin(time * 4f) * Mathf.Sin(time * 4f);
        }

        private void ApplyCandyBounce(float time, float normalized, ref float intensity)
        {
            EnableFirst(CarrierCount);
            for (var index = 0; index < CarrierCount; index++)
            {
                var bounce = Mathf.Abs(Mathf.Sin(time * 7f + index * .8f));
                animatedCarriers[index].localPosition = basePositions[index] + Vector3.up * bounce * (.08f + index * .018f);
                animatedCarriers[index].localScale = Vector3.Scale(baseScales[index], new Vector3(1f + bounce * .16f, 1f - bounce * .12f, 1f));
            }
            intensity *= 1.05f;
        }

        private void ApplyCosmicOrbit(float time, float normalized, ref float intensity)
        {
            EnableFirst(CarrierCount);
            for (var index = 0; index < CarrierCount; index++)
            {
                var angle = time * (.72f + index * .16f) + index * 2.399963f;
                var radius = .12f + index * .045f;
                animatedCarriers[index].localPosition = basePositions[index] + new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius * .62f, 0f);
                animatedCarriers[index].localRotation = baseRotations[index] * Quaternion.Euler(0f, 0f, -angle * Mathf.Rad2Deg);
            }
            intensity *= .78f + .28f * Mathf.Sin(time * 1.8f) * Mathf.Sin(time * 1.8f);
        }

        private void ApplySteamBurst(float time, float normalized, ref float materialPhase, ref float intensity)
        {
            var step = Mathf.Floor(time * 8f) / 8f;
            materialPhase = Mathf.Clamp01(step / duration);
            EnableFirst(CarrierCount);
            for (var index = 0; index < CarrierCount; index++)
            {
                animatedCarriers[index].localRotation = baseRotations[index] * Quaternion.Euler(0f, 0f, step * (index + 1) * 95f * ((index & 1) == 0 ? 1f : -1f));
                animatedCarriers[index].localPosition = basePositions[index] + Vector3.up * Mathf.Sin(normalized * Mathf.PI) * index * .022f;
            }
            intensity *= .82f + (Mathf.FloorToInt(time * 8f) % 2) * .24f;
        }

        private void ApplyGhostPulse(float time, float normalized, ref float intensity, ref float alpha)
        {
            EnableFirst(CarrierCount);
            var pulse = .48f + .52f * Mathf.Sin(time * 1.5f * Mathf.PI) * Mathf.Sin(time * 1.5f * Mathf.PI);
            alpha *= Mathf.Lerp(.42f, 1f, pulse);
            for (var index = 0; index < CarrierCount; index++)
            {
                animatedCarriers[index].localPosition = basePositions[index] + new Vector3(Mathf.Sin(time * .8f + index) * .08f, Mathf.Sin(time * 1.1f + index * .7f) * .07f, 0f);
                animatedCarriers[index].localScale = baseScales[index] * (1f + pulse * .07f);
            }
            intensity *= .7f + pulse * .34f;
        }

        private void ApplyMaterialState(float alpha, float phase, float intensity)
        {
            lastMaterialAlpha = Mathf.Clamp01(alpha);
            lastMaterialPhase = Mathf.Clamp01(phase);
            lastMaterialIntensity = Mathf.Max(0f, intensity);
            if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock();
            for (var index = 0; index < visualRenderers.Length; index++)
            {
                var renderer = visualRenderers[index];
                if (renderer == null || !renderer.enabled) continue;
                propertyBlock.Clear();
                propertyBlock.SetColor("_PrimaryColor", primary);
                propertyBlock.SetColor("_SecondaryColor", secondary);
                propertyBlock.SetColor("_AccentColor", accent);
                propertyBlock.SetFloat("_GlobalAlpha", lastMaterialAlpha);
                propertyBlock.SetFloat("_Phase", lastMaterialPhase);
                propertyBlock.SetFloat("_Intensity", lastMaterialIntensity * Mathf.Max(.68f, 1f - index * .055f));
                propertyBlock.SetFloat("_GlitchOffset", motionProfile == StyleSpecialMotionProfile.HoloBarrier || motionProfile == StyleSpecialMotionProfile.GlitchBlink ? DeterministicSignedStep(elapsed, index) * .07f : 0f);
                renderer.SetPropertyBlock(propertyBlock);
            }
        }

        private int CarrierCount { get { return animatedCarriers == null ? 0 : animatedCarriers.Length; } }

        private void EnableFirst(int count)
        {
            count = Mathf.Clamp(count, 0, visualRenderers == null ? 0 : visualRenderers.Length);
            for (var index = 0; index < (visualRenderers == null ? 0 : visualRenderers.Length); index++) if (visualRenderers[index] != null) visualRenderers[index].enabled = index < count;
            if (semanticLine != null && Array.IndexOf(visualRenderers, semanticLine) >= count) semanticLine.enabled = false;
        }

        private Vector3 RadialDirection(int index)
        {
            var angle = seed * .0001f + index * 2.399963f;
            return new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
        }

        private float DeterministicSignedStep(float time, int salt)
        {
            unchecked
            {
                var step = Mathf.FloorToInt(time * 9f);
                var value = seed ^ (uint)(step * 374761393) ^ (uint)(salt * 668265263);
                value = (value ^ (value >> 13)) * 1274126177u;
                return (value & 1023u) / 1023f * 2f - 1f;
            }
        }

        private void CacheBaseTransforms()
        {
            if (animatedCarriers == null) animatedCarriers = new Transform[0];
            basePositions = new Vector3[animatedCarriers.Length];
            baseRotations = new Quaternion[animatedCarriers.Length];
            baseScales = new Vector3[animatedCarriers.Length];
            for (var index = 0; index < animatedCarriers.Length; index++)
            {
                var carrier = animatedCarriers[index];
                if (carrier == null) continue;
                basePositions[index] = carrier.localPosition;
                baseRotations[index] = carrier.localRotation;
                baseScales[index] = carrier.localScale;
            }
        }

        private void RestoreBaseTransforms()
        {
            if (animatedCarriers == null) return;
            for (var index = 0; index < animatedCarriers.Length && index < basePositions.Length; index++)
            {
                var carrier = animatedCarriers[index];
                if (carrier == null) continue;
                carrier.localPosition = basePositions[index];
                carrier.localRotation = baseRotations[index];
                carrier.localScale = baseScales[index];
            }
        }

        private void DisableAllRenderers()
        {
            if (visualRenderers == null) return;
            for (var index = 0; index < visualRenderers.Length; index++) if (visualRenderers[index] != null) visualRenderers[index].enabled = false;
        }

        private void ClearPropertyBlocks()
        {
            if (visualRenderers == null) return;
            for (var index = 0; index < visualRenderers.Length; index++) if (visualRenderers[index] != null) visualRenderers[index].SetPropertyBlock(null);
        }

        private static float EntranceExitEnvelope(float normalized)
        {
            var enter = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, .055f, normalized));
            var exit = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.83f, 1f, normalized));
            return Mathf.Clamp01(enter * exit);
        }
    }
}

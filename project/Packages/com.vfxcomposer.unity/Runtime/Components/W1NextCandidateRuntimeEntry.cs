using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VFXComposer.Capabilities;

namespace VFXComposer
{
    public enum W1NextCandidateKind
    {
        StyleToken,
        FanWave,
        ChargeOcclude,
        TelegraphNova
    }

    public enum W1StyleTimingProfile
    {
        PaintedSweep,
        CelBounce,
        PixelStep,
        InkBleed,
        SoftTurbulence,
        HoloScan,
        RitualPulse,
        NeonBeat,
        FanWave,
        ChargeOcclude,
        TelegraphNova
    }

    public struct W1RuntimeBudgetSnapshot
    {
        public int GameObjects;
        public int Renderers;
        public int ParticleSystems;
        public int Materials;
    }

    /// <summary>
    /// W1-only Runtime Entry for the style comparison wall. Every visible topology has a fixed
    /// carrier capacity and is mapped into the same local envelope before the Preview viewport
    /// applies its hard cell clip.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class W1NextCandidateRuntimeEntry : MonoBehaviour, IVfxRuntimeEntry
    {
        public const int MaxGameObjectsBudget = 10;
        public const int MaxRenderersBudget = 6;
        public const int MaxParticleSystemsBudget = 0;
        public const int MaxMaterialsBudget = 1;
        public static readonly Bounds UniformLocalEnvelope = new Bounds(new Vector3(0f, .02f, 0f), new Vector3(1.56f, 1.08f, .5f));

        [SerializeField] private W1NextCandidateKind kind;
        [SerializeField] private W1StyleTimingProfile timingProfile;
        [SerializeField] private string styleToken = "stylized";
        [SerializeField] private string visualSignature = string.Empty;
        [SerializeField, Min(.1f)] private float duration = 2f;
        [SerializeField] private uint seed = 1;
        [SerializeField] private Color primary = Color.white;
        [SerializeField] private Color secondary = Color.cyan;
        [SerializeField] private Color accent = Color.white;
        [SerializeField, Min(.1f)] private float baseIntensity = 1f;
        [SerializeField] private Bounds declaredLocalBounds = new Bounds(new Vector3(0f, .02f, 0f), new Vector3(1.56f, 1.08f, .5f));
        [SerializeField] private Renderer[] visualRenderers = new Renderer[0];
        [SerializeField] private Transform[] animatedTransforms = new Transform[0];
        [SerializeField] private Transform[] styleCarriers = new Transform[0];
        [SerializeField] private Transform[] fanCarriers = new Transform[0];
        [SerializeField] private LineRenderer beamLine;
        [SerializeField] private Transform chargeGlyph;
        [SerializeField] private MeshRenderer telegraphRenderer;
        [SerializeField] private MeshRenderer novaRenderer;
        [SerializeField] private MeshRenderer novaMoteRenderer;

        private Vector3[] basePositions = new Vector3[0];
        private Quaternion[] baseRotations = new Quaternion[0];
        private Vector3[] baseScales = new Vector3[0];
        private readonly Vector3[] fanDirections = new Vector3[5];
        private MaterialPropertyBlock propertyBlock;
        private CapabilitySampleTrace behaviorTrace;
        private float elapsed;
        private float lastStylePhase;
        private float lastAppliedIntensity;
        private float lastBeamWidth;
        private Vector3 lastBeamEndpoint;
        private Vector3 previousBeamEndpoint;
        private bool hasPreviousBeamEndpoint;
        private bool playing;
        private int replayCount;
        private int occlusionTransitions;
        private int novaVisibleMoteCount;
        private VfxStopMode lastStopMode;

        public bool IsAlive { get { return playing; } }
        public W1NextCandidateKind Kind { get { return kind; } }
        public W1StyleTimingProfile TimingProfile { get { return timingProfile; } }
        public string StyleToken { get { return styleToken; } }
        public string VisualSignature { get { return visualSignature; } }
        public float Duration { get { return duration; } }
        public Bounds DeclaredLocalBounds { get { return declaredLocalBounds; } }
        public CapabilitySampleTrace BehaviorTrace { get { return behaviorTrace; } }
        public float LastStylePhase { get { return lastStylePhase; } }
        public float LastAppliedIntensity { get { return lastAppliedIntensity; } }
        public float LastBeamWidth { get { return lastBeamWidth; } }
        public Vector3 LastBeamEndpoint { get { return lastBeamEndpoint; } }
        public int ReplayCount { get { return replayCount; } }
        public int OcclusionTransitions { get { return occlusionTransitions; } }
        public int NovaVisibleMoteCount { get { return novaVisibleMoteCount; } }
        public int FanCarrierCapacity { get { return fanCarriers == null ? 0 : fanCarriers.Length; } }
        public int VisibleRendererCount { get { return visualRenderers == null ? 0 : visualRenderers.Count(value => value != null && value.enabled); } }
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
            ApplyAtTime(elapsed);
            if (elapsed >= duration) Stop(VfxStopMode.Immediate);
        }

        public void Initialize(VfxRuntimeContext context)
        {
            transform.SetPositionAndRotation(context.Position, context.Rotation);
            ResetForPool();
        }

        public void Play()
        {
            if (basePositions.Length != animatedTransforms.Length) CacheBaseTransforms();
            RestoreBaseTransforms();
            DisableAllRenderers();
            behaviorTrace = BuildTrace();
            ConfigureTraceCarriers();
            elapsed = 0f;
            occlusionTransitions = 0;
            novaVisibleMoteCount = 0;
            hasPreviousBeamEndpoint = false;
            playing = true;
            replayCount++;
            ApplyAtTime(0f);
        }

        public bool SendEvent(string eventId, VfxRuntimeEvent payload)
        {
            if (eventId == "play" || eventId == "start" || eventId == "trigger")
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
            DisableAllRenderers();
            novaVisibleMoteCount = 0;
        }

        public void ResetForPool()
        {
            playing = false;
            elapsed = 0f;
            behaviorTrace = null;
            lastStylePhase = 0f;
            lastAppliedIntensity = 0f;
            lastBeamWidth = 0f;
            lastBeamEndpoint = Vector3.zero;
            previousBeamEndpoint = Vector3.zero;
            hasPreviousBeamEndpoint = false;
            occlusionTransitions = 0;
            novaVisibleMoteCount = 0;
            DisableAllRenderers();
            RestoreBaseTransforms();
            ClearPropertyBlocks();
        }

        public W1RuntimeBudgetSnapshot ReadBudget()
        {
            var renderers = GetComponentsInChildren<Renderer>(true);
            return new W1RuntimeBudgetSnapshot
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
                Bounds rendererBounds;
                if (!TryGetRendererLocalBounds(renderer, out rendererBounds)) continue;
                if (!initialized) { bounds = rendererBounds; initialized = true; }
                else { bounds.Encapsulate(rendererBounds.min); bounds.Encapsulate(rendererBounds.max); }
            }
            return initialized;
        }

        public bool IsInsideDeclaredEnvelope(float epsilon)
        {
            Bounds current;
            if (!TryGetCurrentLocalBounds(out current)) return !playing;
            var allowedMin = declaredLocalBounds.min - Vector3.one * Mathf.Max(0f, epsilon);
            var allowedMax = declaredLocalBounds.max + Vector3.one * Mathf.Max(0f, epsilon);
            return current.min.x >= allowedMin.x && current.min.y >= allowedMin.y && current.min.z >= allowedMin.z &&
                   current.max.x <= allowedMax.x && current.max.y <= allowedMax.y && current.max.z <= allowedMax.z;
        }

        private void ApplyAtTime(float time)
        {
            var normalized = Mathf.Clamp01(time / Mathf.Max(.1f, duration));
            if (kind == W1NextCandidateKind.StyleToken) ApplyStyleToken(time, normalized);
            else if (kind == W1NextCandidateKind.FanWave) ApplyFanWave(time);
            else if (kind == W1NextCandidateKind.ChargeOcclude) ApplyChargeOcclude(time);
            else ApplyTelegraphNova(time);
        }

        private void ApplyStyleToken(float time, float normalized)
        {
            SetEnabled(visualRenderers, true);
            var phase = EvaluateStylePhase(time, normalized);
            var envelope = EntranceExitEnvelope(normalized);
            var pulse = EvaluateStylePulse(time, phase);
            lastStylePhase = phase;
            lastAppliedIntensity = Mathf.Max(styleToken == "dark" ? .96f : .62f, baseIntensity * (.9f + .1f * pulse)) * envelope;
            for (var index = 0; index < styleCarriers.Length; index++)
            {
                var item = styleCarriers[index];
                if (item == null) continue;
                var baseIndex = Array.IndexOf(animatedTransforms, item);
                if (baseIndex < 0) continue;
                var direction = (index & 1) == 0 ? 1f : -1f;
                var rotation = StyleRotation(time, phase, index) * direction;
                item.localRotation = baseRotations[baseIndex] * Quaternion.Euler(0f, 0f, rotation);
                var layerPulse = 1f + (.035f + index * .012f) * pulse;
                item.localScale = baseScales[baseIndex] * layerPulse;
                item.localPosition = basePositions[baseIndex] + StyleOffset(time, phase, index);
                ConstrainRendererInsideEnvelope(item, item.GetComponent<Renderer>());
            }
            ApplyMaterialState(envelope, phase, lastAppliedIntensity, styleToken == "holo" ? HoloGlitch(time) : 0f);
        }

        private void ApplyFanWave(float time)
        {
            if (behaviorTrace == null || behaviorTrace.Frames.Count == 0) return;
            var frame = FrameAt(time);
            var longitudinal = frame.Position.x * .12f;
            var lateral = frame.Position.y * .21f;
            for (var index = 0; index < fanCarriers.Length; index++)
            {
                var carrier = fanCarriers[index];
                if (carrier == null) continue;
                var direction = fanDirections[index].sqrMagnitude < .001f ? Vector3.right : fanDirections[index].normalized;
                var normal = new Vector3(-direction.y, direction.x, 0f);
                carrier.localPosition = ClampToEnvelope(direction * longitudinal + normal * lateral, .11f);
                carrier.localRotation = Quaternion.FromToRotation(Vector3.up, direction);
                var scale = .09f + .018f * Mathf.Sin(time * 9f + index * .7f);
                carrier.localScale = Vector3.one * scale;
                var renderer = carrier.GetComponent<Renderer>();
                if (renderer != null) renderer.enabled = true;
            }
            lastStylePhase = Mathf.Clamp01(time / duration);
            lastAppliedIntensity = baseIntensity;
            ApplyMaterialState(EntranceExitEnvelope(lastStylePhase), lastStylePhase, baseIntensity, 0f);
        }

        private void ApplyChargeOcclude(float time)
        {
            if (behaviorTrace == null || beamLine == null) return;
            var frame = FrameAt(time);
            var source = MapCapabilityPoint(frame.Source);
            var endpoint = MapCapabilityPoint(frame.Target);
            source = ClampToEnvelope(source, .08f);
            endpoint = ClampToEnvelope(endpoint, .08f);
            beamLine.enabled = true;
            beamLine.positionCount = 9;
            for (var point = 0; point < beamLine.positionCount; point++)
            {
                var ratio = point / (float)(beamLine.positionCount - 1);
                var position = Vector3.Lerp(source, endpoint, ratio);
                // Preserve the exact trace-backed source/target endpoints while allowing the
                // intermediate samples to carry the visible charged-wave displacement.
                position.y += Mathf.Sin(ratio * Mathf.PI * 8f + time * 15f) * .014f * Mathf.Sin(ratio * Mathf.PI);
                beamLine.SetPosition(point, position);
            }
            lastBeamWidth = Mathf.Clamp(.035f + frame.Width * .018f, .035f, .105f);
            beamLine.startWidth = lastBeamWidth;
            beamLine.endWidth = lastBeamWidth * .72f;
            lastBeamEndpoint = endpoint;
            if (hasPreviousBeamEndpoint && Vector3.Distance(endpoint, previousBeamEndpoint) > .08f) occlusionTransitions++;
            previousBeamEndpoint = endpoint;
            hasPreviousBeamEndpoint = true;
            if (chargeGlyph != null)
            {
                var index = Array.IndexOf(animatedTransforms, chargeGlyph);
                if (index >= 0)
                {
                    chargeGlyph.localPosition = source;
                    chargeGlyph.localScale = baseScales[index] * Mathf.Clamp(.72f + frame.Width * .13f, .72f, 1.28f);
                    chargeGlyph.localRotation = baseRotations[index] * Quaternion.Euler(0f, 0f, time * 90f);
                }
                var glyphRenderer = chargeGlyph.GetComponent<Renderer>();
                if (glyphRenderer != null) glyphRenderer.enabled = true;
            }
            lastStylePhase = Mathf.Clamp01(time / duration);
            lastAppliedIntensity = baseIntensity * Mathf.Clamp(.82f + frame.Width * .08f, .82f, 1.2f);
            ApplyMaterialState(EntranceExitEnvelope(lastStylePhase), lastStylePhase, lastAppliedIntensity, HoloGlitch(time));
        }

        private void ApplyTelegraphNova(float time)
        {
            if (behaviorTrace == null) return;
            var frame = FrameAt(time);
            const float warningDuration = .65f;
            var warning = Mathf.Clamp01(time / warningDuration);
            var released = time >= warningDuration;
            if (telegraphRenderer != null)
            {
                telegraphRenderer.enabled = !released;
                var transformValue = telegraphRenderer.transform;
                var index = Array.IndexOf(animatedTransforms, transformValue);
                if (index >= 0)
                {
                    var collapse = Mathf.Lerp(1f, .35f, warning);
                    transformValue.localScale = baseScales[index] * collapse;
                    transformValue.localRotation = baseRotations[index] * Quaternion.Euler(0f, 0f, time * 55f);
                }
            }
            var novaScale = Mathf.Clamp(frame.Radius * .17f, .05f, .68f);
            var novaProgress = Mathf.InverseLerp(.05f, .68f, novaScale);
            if (novaRenderer != null)
            {
                novaRenderer.enabled = released;
                // Rotating square-normalized carriers must fit by their diagonal, not their
                // authoring-time axis-aligned size. The ring remains dominant at .72.
                SetNormalizedCarrierSize(novaRenderer.transform, Vector2.one * Mathf.Lerp(.1f, .72f, novaProgress));
                novaRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, -time * 38f);
                ConstrainRendererInsideEnvelope(novaRenderer.transform, novaRenderer);
            }
            if (novaMoteRenderer != null)
            {
                novaMoteRenderer.enabled = released;
                // A square-normalized burst rotates continuously; .66 retains a safety margin
                // below the asymmetric 1.08 vertical envelope for every rotation angle.
                SetNormalizedCarrierSize(novaMoteRenderer.transform, Vector2.one * Mathf.Lerp(.1f, .66f, novaProgress));
                novaMoteRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, time * 31f);
                ConstrainRendererInsideEnvelope(novaMoteRenderer.transform, novaMoteRenderer);
            }
            novaVisibleMoteCount = released && novaMoteRenderer != null && novaMoteRenderer.enabled ? 12 : 0;
            lastStylePhase = released ? Mathf.Clamp01((time - warningDuration) / Mathf.Max(.1f, duration - warningDuration)) : warning;
            lastAppliedIntensity = baseIntensity * (released ? 1.08f : .76f + .18f * Mathf.Sin(time * 20f));
            ApplyMaterialState(EntranceExitEnvelope(Mathf.Clamp01(time / duration)), lastStylePhase, lastAppliedIntensity, 0f);
        }

        private CapabilitySampleTrace BuildTrace()
        {
            if (kind == W1NextCandidateKind.StyleToken) return null;
            var request = new CapabilitySampleRequest
            {
                Origin = Vector3.zero,
                Direction = Vector3.right,
                Target = new Vector3(5f, 0f, 0f),
                Duration = duration,
                DeltaTime = 1f / 60f,
                Seed = seed,
                MotionType = "stationary",
                HitType = "single",
                EmissionType = "single",
                TimingType = "instant"
            };
            if (kind == W1NextCandidateKind.FanWave)
            {
                request.MotionType = "wave";
                request.EmissionType = "fan";
                request.Motion["speed"] = 3.2d;
                request.Motion["amplitude"] = .42d;
                request.Motion["frequency"] = 1.8d;
                request.Emission["count"] = 5d;
                request.Emission["spread_angle"] = 42d;
            }
            else if (kind == W1NextCandidateKind.ChargeOcclude)
            {
                request.Origin = new Vector3(-3f, 0f, 0f);
                request.Target = new Vector3(3f, 0f, 0f);
                request.HitType = "occlude";
                request.TimingType = "charge_scale";
                request.ObstacleDistance = 2.15f;
                request.ObstacleChangeTime = .9f;
                request.ObstacleSecondDistance = 4.55f;
                request.Hit["probe_interval"] = .05d;
                request.Timing["level_1"] = .3d;
                request.Timing["level_2"] = .72d;
                request.Timing["per_level_width"] = 1.8d;
            }
            else
            {
                request.MotionType = "expand_ring";
                request.EmissionType = "ring";
                request.TimingType = "telegraph";
                request.Motion["max_radius"] = 4d;
                request.Motion["expand_speed"] = 2.8d;
                request.Motion["edge_thickness"] = .2d;
                request.Emission["count"] = 12d;
                request.Emission["ring_radius"] = .45d;
                request.Timing["warn_duration"] = .65d;
            }
            return CapabilitySampler.SampleTrajectory(request);
        }

        private void ConfigureTraceCarriers()
        {
            if (kind != W1NextCandidateKind.FanWave || behaviorTrace == null) return;
            var events = behaviorTrace.Events.Where(value => value.Type == "on_emit" && value.Detail == "fan").OrderBy(value => value.Sequence).ToArray();
            for (var index = 0; index < fanDirections.Length; index++) fanDirections[index] = index < events.Length ? events[index].After : Vector3.right;
        }

        private CapabilitySampleFrame FrameAt(float time)
        {
            var index = Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp(time, 0f, duration) * 60f), 0, behaviorTrace.Frames.Count - 1);
            return behaviorTrace.Frames[index];
        }

        private float EvaluateStylePhase(float time, float normalized)
        {
            if (timingProfile == W1StyleTimingProfile.CelBounce) return Mathf.Floor(normalized * 12f) / 12f;
            if (timingProfile == W1StyleTimingProfile.PixelStep) return Mathf.Floor(normalized * 8f) / 8f;
            if (timingProfile == W1StyleTimingProfile.InkBleed) return Mathf.SmoothStep(0f, 1f, normalized * normalized);
            if (timingProfile == W1StyleTimingProfile.SoftTurbulence) return Mathf.Clamp01(normalized + Mathf.Sin(time * 5.3f) * .025f);
            if (timingProfile == W1StyleTimingProfile.HoloScan) return Mathf.Repeat(normalized * 1.35f, 1f);
            if (timingProfile == W1StyleTimingProfile.RitualPulse) return Mathf.Clamp01(normalized * .85f + .15f * Mathf.Sin(time * 2.7f));
            if (timingProfile == W1StyleTimingProfile.NeonBeat) return Mathf.Floor(normalized * 16f) / 16f;
            return normalized;
        }

        private float EvaluateStylePulse(float time, float phase)
        {
            if (timingProfile == W1StyleTimingProfile.CelBounce) return Mathf.Abs(Mathf.Sin(phase * Mathf.PI * 3f));
            if (timingProfile == W1StyleTimingProfile.PixelStep) return ((Mathf.FloorToInt(phase * 8f) & 1) == 0) ? .18f : .92f;
            if (timingProfile == W1StyleTimingProfile.InkBleed) return Mathf.SmoothStep(0f, 1f, phase);
            if (timingProfile == W1StyleTimingProfile.SoftTurbulence) return .5f + .5f * Mathf.PerlinNoise(seed * .001f, time * .55f);
            if (timingProfile == W1StyleTimingProfile.HoloScan) return .35f + .65f * Mathf.Abs(Mathf.Sin(time * 11f));
            if (timingProfile == W1StyleTimingProfile.RitualPulse) return .55f + .45f * Mathf.Sin(time * 2.7f);
            if (timingProfile == W1StyleTimingProfile.NeonBeat) return Mathf.Pow(Mathf.Abs(Mathf.Sin(time * 6.4f)), .35f);
            return .5f + .5f * Mathf.Sin(time * 3.7f);
        }

        private float StyleRotation(float time, float phase, int index)
        {
            if (timingProfile == W1StyleTimingProfile.PixelStep) return Mathf.Floor(time * 8f) * (index + 1) * 4f;
            if (timingProfile == W1StyleTimingProfile.InkBleed) return Mathf.Sin(phase * Mathf.PI) * (index + 1) * 7f;
            if (timingProfile == W1StyleTimingProfile.HoloScan) return time * (42f + index * 19f);
            if (timingProfile == W1StyleTimingProfile.RitualPulse) return time * (12f + index * 8f);
            if (timingProfile == W1StyleTimingProfile.NeonBeat) return time * (68f + index * 11f);
            return time * (24f + index * 13f);
        }

        private Vector3 StyleOffset(float time, float phase, int index)
        {
            if (timingProfile == W1StyleTimingProfile.InkBleed && index == 2) return new Vector3(-.035f * phase, .025f * Mathf.Sin(time * 2f), 0f);
            if (timingProfile == W1StyleTimingProfile.HoloScan && index == 1) return Vector3.right * HoloGlitch(time) * .18f;
            if (timingProfile == W1StyleTimingProfile.PixelStep) return new Vector3(0f, (Mathf.FloorToInt(time * 8f) % 3 - 1) * .008f, 0f);
            return Vector3.zero;
        }

        private float HoloGlitch(float time)
        {
            var step = Mathf.FloorToInt(time * 7f);
            unchecked
            {
                var value = seed ^ (uint)(step * 374761393);
                value = (value ^ (value >> 13)) * 1274126177u;
                return ((value & 1023u) / 1023f * 2f - 1f) * .075f;
            }
        }

        private void ApplyMaterialState(float alpha, float phase, float intensity, float glitch)
        {
            if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock();
            for (var index = 0; index < visualRenderers.Length; index++)
            {
                var renderer = visualRenderers[index];
                if (renderer == null || !renderer.enabled) continue;
                propertyBlock.Clear();
                propertyBlock.SetColor("_PrimaryColor", primary);
                propertyBlock.SetColor("_SecondaryColor", secondary);
                propertyBlock.SetColor("_AccentColor", accent);
                propertyBlock.SetFloat("_GlobalAlpha", Mathf.Clamp01(alpha));
                propertyBlock.SetFloat("_Phase", Mathf.Clamp01(phase));
                propertyBlock.SetFloat("_Intensity", Mathf.Max(0f, intensity) * (1f - index * .045f));
                propertyBlock.SetFloat("_GlitchOffset", glitch);
                renderer.SetPropertyBlock(propertyBlock);
            }
        }

        private void CacheBaseTransforms()
        {
            if (animatedTransforms == null) animatedTransforms = new Transform[0];
            basePositions = new Vector3[animatedTransforms.Length];
            baseRotations = new Quaternion[animatedTransforms.Length];
            baseScales = new Vector3[animatedTransforms.Length];
            for (var index = 0; index < animatedTransforms.Length; index++)
            {
                var item = animatedTransforms[index];
                if (item == null) continue;
                basePositions[index] = item.localPosition;
                baseRotations[index] = item.localRotation;
                baseScales[index] = item.localScale;
            }
        }

        private void RestoreBaseTransforms()
        {
            if (animatedTransforms == null) return;
            for (var index = 0; index < animatedTransforms.Length && index < basePositions.Length; index++)
            {
                var item = animatedTransforms[index];
                if (item == null) continue;
                item.localPosition = basePositions[index];
                item.localRotation = baseRotations[index];
                item.localScale = baseScales[index];
            }
        }

        private void DisableAllRenderers()
        {
            SetEnabled(visualRenderers, false);
        }

        private void ClearPropertyBlocks()
        {
            if (visualRenderers == null) return;
            for (var index = 0; index < visualRenderers.Length; index++) if (visualRenderers[index] != null) visualRenderers[index].SetPropertyBlock(null);
        }

        private static void SetEnabled(Renderer[] values, bool enabled)
        {
            if (values == null) return;
            for (var index = 0; index < values.Length; index++) if (values[index] != null) values[index].enabled = enabled;
        }

        private Vector3 ClampToEnvelope(Vector3 value, float padding)
        {
            var min = declaredLocalBounds.min + Vector3.one * padding;
            var max = declaredLocalBounds.max - Vector3.one * padding;
            return new Vector3(Mathf.Clamp(value.x, min.x, max.x), Mathf.Clamp(value.y, min.y, max.y), Mathf.Clamp(value.z, min.z, max.z));
        }

        private bool TryGetRendererLocalBounds(Renderer renderer, out Bounds localBounds)
        {
            localBounds = new Bounds();
            var line = renderer as LineRenderer;
            if (line != null)
            {
                if (line.positionCount <= 0) return false;
                for (var point = 0; point < line.positionCount; point++)
                {
                    var position = line.GetPosition(point);
                    var worldPoint = line.useWorldSpace ? position : line.transform.TransformPoint(position);
                    var localPoint = transform.InverseTransformPoint(worldPoint);
                    if (point == 0) localBounds = new Bounds(localPoint, Vector3.zero);
                    else localBounds.Encapsulate(localPoint);
                }
                var radius = Mathf.Max(line.startWidth, line.endWidth) * .5f;
                localBounds.Expand(Vector3.one * radius * 2f);
                return true;
            }

            var world = renderer.bounds;
            var min = world.min;
            var max = world.max;
            for (var corner = 0; corner < 8; corner++)
            {
                var worldPoint = new Vector3((corner & 1) == 0 ? min.x : max.x, (corner & 2) == 0 ? min.y : max.y, (corner & 4) == 0 ? min.z : max.z);
                var localPoint = transform.InverseTransformPoint(worldPoint);
                if (corner == 0) localBounds = new Bounds(localPoint, Vector3.zero);
                else localBounds.Encapsulate(localPoint);
            }
            return true;
        }

        private void ConstrainRendererInsideEnvelope(Transform carrier, Renderer renderer)
        {
            if (carrier == null || renderer == null || !renderer.enabled || carrier.parent != transform) return;
            Bounds current;
            if (!TryGetRendererLocalBounds(renderer, out current)) return;
            var allowedMin = declaredLocalBounds.min;
            var allowedMax = declaredLocalBounds.max;
            var shift = Vector3.zero;
            if (current.min.x < allowedMin.x) shift.x += allowedMin.x - current.min.x;
            if (current.max.x + shift.x > allowedMax.x) shift.x += allowedMax.x - (current.max.x + shift.x);
            if (current.min.y < allowedMin.y) shift.y += allowedMin.y - current.min.y;
            if (current.max.y + shift.y > allowedMax.y) shift.y += allowedMax.y - (current.max.y + shift.y);
            if (current.min.z < allowedMin.z) shift.z += allowedMin.z - current.min.z;
            if (current.max.z + shift.z > allowedMax.z) shift.z += allowedMax.z - (current.max.z + shift.z);
            carrier.localPosition += shift;
        }

        private Vector3 MapCapabilityPoint(Vector3 value)
        {
            return new Vector3(value.x * .18f, value.y * .18f, value.z * .1f);
        }

        private void SetNormalizedCarrierSize(Transform carrier, Vector2 targetSize)
        {
            var index = Array.IndexOf(animatedTransforms, carrier);
            if (index < 0 || index >= baseScales.Length) return;
            // Nova carriers are authored at a normalized .1 x .1 reference size. Scaling from
            // that cached reference preserves mesh normalization for Ring and Burst meshes.
            carrier.localScale = Vector3.Scale(baseScales[index], new Vector3(targetSize.x * 10f, targetSize.y * 10f, 1f));
        }

        private static float EntranceExitEnvelope(float normalized)
        {
            var enter = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, .08f, normalized));
            var exit = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.86f, 1f, normalized));
            return Mathf.Clamp01(enter * exit);
        }
    }
}

using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace VFXComposer.TechniqueFamilies
{
    /// <summary>Which target the beat drives. Compile-time decision, no reflection at runtime.</summary>
    public enum VfxLightDriverKind { None = 0, Light3D = 1, Light2D = 2, MaterialOnly = 3 }

    public enum VfxFlickerMode { Steady = 0, Breathe = 1, Flicker = 2, Pulse = 3, Strobe = 4 }

    public enum VfxDecayShape { Exp = 0, Linear = 1, Step = 2, Smooth = 3, Spike = 4 }

    /// <summary>
    /// Local-light beat driver (TECH_FAMILY_SPEC_MESH_LIGHT.md section 6).
    /// Sole phase owner of its beat channel: computes the waveform once per
    /// frame in LateUpdate, drives Light / Light2D / material-only emission
    /// (the ML "light baked into material" tier), and broadcasts _BeatValue{ch}
    /// to the subscribed renderers via MaterialPropertyBlock so the glow layer
    /// breathes in sync (flickerCoupling). Never touches global shader state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VfxLightBeat : MonoBehaviour
    {
        private static readonly int[] BeatValueIds =
        {
            Shader.PropertyToID("_BeatValue0"),
            Shader.PropertyToID("_BeatValue1"),
            Shader.PropertyToID("_BeatValue2"),
            Shader.PropertyToID("_BeatValue3")
        };
        private static readonly int BeatValueId = Shader.PropertyToID("_BeatValue");
        private static readonly int BakedLightBeatId = Shader.PropertyToID("_BakedLightBeat");

        [Header("Target (compile-time)")]
        [SerializeField] private Component lightTarget;
        [SerializeField] private VfxLightDriverKind kind = VfxLightDriverKind.Light3D;

        [Header("Light")]
        [SerializeField] private Color color = Color.white;
        [SerializeField, Range(0f, 20f)] private float intensity = 1f;
        [SerializeField, Range(0.1f, 50f)] private float range = 5f;
        [SerializeField, Range(0f, 180f)] private float innerAngle = 30f;
        [SerializeField, Range(0f, 180f)] private float outerAngle = 60f;
        [SerializeField] private bool castShadows;

        [Header("Beat")]
        [SerializeField] private VfxFlickerMode flickerMode = VfxFlickerMode.Steady;
        [SerializeField, Range(0.1f, 30f)] private float flickerRate = 4f;
        [SerializeField, Range(0f, 1f)] private float flickerDepth = 0.25f;
        [SerializeField] private VfxDecayShape decayShape = VfxDecayShape.Exp;
        [SerializeField, Range(0f, 1f)] private float phaseOffset;
        [SerializeField, Range(0, 3)] private int beatChannel;

        [Header("Style quantization")]
        [SerializeField, Range(0, 8)] private int intensitySteps;
        [SerializeField] private bool flickerQuantize;
        [SerializeField, Range(0f, 30f)] private float beatFrameRate;
        [SerializeField, Range(0.5f, 2f)] private float saturationMul = 1f;

        [Header("Baked tier (kind == MaterialOnly)")]
        [SerializeField, Range(0f, 4f)] private float bakedEmissionMul = 1f;

        [Header("Sync targets")]
        [SerializeField] private Renderer[] beatTargets = new Renderer[0];
        [SerializeField] private float unitScale = 1f;

        private MaterialPropertyBlock block;
        private float localTime;
        private float decayProgress; // 0 while active, ramps 0->1 during the phase tail
        private float lastBeat = 1f;

        public VfxLightDriverKind Kind { get { return kind; } }
        public Component LightTarget { get { return lightTarget; } }
        public int BeatChannel { get { return beatChannel; } }
        public Renderer[] BeatTargets { get { return beatTargets; } }
        public float LastBeat { get { return lastBeat; } }
        public float Intensity { get { return intensity; } set { intensity = Mathf.Max(value, 0f); } }
        public Color Color { get { return color; } set { color = value; } }
        public float Range { get { return range; } set { range = Mathf.Max(value, 0.01f); } }
        public VfxFlickerMode FlickerMode { get { return flickerMode; } set { flickerMode = value; } }
        public float FlickerRate { get { return flickerRate; } set { flickerRate = Mathf.Clamp(value, 0.1f, 30f); } }
        public float FlickerDepth { get { return flickerDepth; } set { flickerDepth = Mathf.Clamp01(value); } }

        /// <summary>Compile-time wiring entry point (editor/compiler use).</summary>
        public void Configure(VfxLightDriverKind driverKind, Component target, Renderer[] targets, int channel)
        {
            kind = driverKind;
            lightTarget = target;
            beatTargets = targets ?? new Renderer[0];
            beatChannel = Mathf.Clamp(channel, 0, 3);
        }

        public void ConfigureStyle(int steps, bool quantize, float frameRate, float satMul)
        {
            intensitySteps = Mathf.Clamp(steps, 0, 8);
            flickerQuantize = quantize;
            beatFrameRate = Mathf.Clamp(frameRate, 0f, 30f);
            saturationMul = Mathf.Clamp(satMul, 0.5f, 2f);
        }

        /// <summary>Compile-time beat wiring (element preset injection).</summary>
        public void ConfigureBeat(VfxFlickerMode mode, float rate, float depth, VfxDecayShape shape, float phase)
        {
            flickerMode = mode;
            flickerRate = Mathf.Clamp(rate, 0.1f, 30f);
            flickerDepth = Mathf.Clamp01(depth);
            decayShape = shape;
            phaseOffset = Mathf.Repeat(phase, 1f);
        }

        /// <summary>Compile-time light shape wiring.</summary>
        public void ConfigureLight(Color lightColor, float lightIntensity, float lightRange, bool shadows)
        {
            color = lightColor;
            intensity = Mathf.Max(lightIntensity, 0f);
            range = Mathf.Max(lightRange, 0.01f);
            castShadows = shadows;
        }

        /// <summary>Pool reset: phase to zero, light back to base intensity.</summary>
        public void ResetForPool()
        {
            localTime = 0f;
            decayProgress = 0f;
            lastBeat = 1f;
            ApplyBeat(1f);
        }

        /// <summary>Controller drives the layer-local time (already speed-scaled).</summary>
        public void SetLocalTime(float time)
        {
            localTime = time;
        }

        /// <summary>0 = fully lit, 1 = fully decayed. Controller ramps this in the phase tail.</summary>
        public void SetDecayProgress(float progress)
        {
            decayProgress = Mathf.Clamp01(progress);
        }

        private void OnEnable()
        {
            if (block == null) block = new MaterialPropertyBlock();
        }

        private void LateUpdate()
        {
            localTime += Time.deltaTime;
            float beat = EvaluateBeat(localTime);
            ApplyBeat(beat);
        }

        /// <summary>Pure waveform evaluation; deterministic in (time, settings).</summary>
        public float EvaluateBeat(float time)
        {
            float qTime = beatFrameRate > 0f ? Mathf.Floor(time * beatFrameRate) / beatFrameRate : time;
            float t = qTime * flickerRate + phaseOffset;
            float b = Waveform(t);
            if (flickerQuantize && intensitySteps > 1)
                b = Mathf.Floor(b * intensitySteps) / (intensitySteps - 1f);
            float beat = 1f - flickerDepth * (1f - Mathf.Clamp01(b));
            beat *= DecayEnvelope(decayProgress);
            return Mathf.Clamp01(beat);
        }

        private float Waveform(float t)
        {
            switch (flickerMode)
            {
                case VfxFlickerMode.Breathe:
                    return 0.5f + 0.5f * Mathf.Sin(t * Mathf.PI * 2f);
                case VfxFlickerMode.Flicker:
                    // two-layer 1D value noise at 1 : 2.7 frequency ratio
                    return Mathf.Clamp01(ValueNoise1(t) * 0.65f + ValueNoise1(t * 2.7f + 13.7f) * 0.35f);
                case VfxFlickerMode.Pulse:
                {
                    float p = t - Mathf.Floor(t);
                    return 1f - Mathf.Pow(p, 0.25f);
                }
                case VfxFlickerMode.Strobe:
                {
                    float p = t - Mathf.Floor(t);
                    return p < 0.35f ? 1f : 0f;
                }
                default:
                    return 1f;
            }
        }

        private float DecayEnvelope(float progress)
        {
            if (progress <= 0f) return 1f;
            switch (decayShape)
            {
                case VfxDecayShape.Linear: return 1f - progress;
                case VfxDecayShape.Step: return progress < 0.5f ? 1f : progress < 0.85f ? 0.4f : 0f;
                case VfxDecayShape.Smooth: return 1f - progress * progress * (3f - 2f * progress);
                case VfxDecayShape.Spike: return Mathf.Pow(1f - progress, 4f);
                default: return Mathf.Exp(-4f * progress) * (1f - progress);
            }
        }

        private void ApplyBeat(float beat)
        {
            lastBeat = beat;
            Color displayColor = ApplySaturation(color, saturationMul);
            switch (kind)
            {
                case VfxLightDriverKind.Light3D:
                {
                    var l = lightTarget as Light;
                    if (l != null)
                    {
                        l.intensity = intensity * beat * unitScale;
                        l.color = displayColor;
                        l.range = range;
                        if (l.type == LightType.Spot) l.spotAngle = outerAngle;
                        l.shadows = castShadows ? LightShadows.Hard : LightShadows.None;
                    }
                    break;
                }
                case VfxLightDriverKind.Light2D:
                {
                    var l2 = lightTarget as Light2D;
                    if (l2 != null)
                    {
                        l2.intensity = intensity * beat * unitScale;
                        l2.color = displayColor;
                        if (l2.lightType == Light2D.LightType.Point)
                        {
                            l2.pointLightOuterRadius = range;
                            l2.pointLightInnerAngle = innerAngle;
                            l2.pointLightOuterAngle = outerAngle;
                        }
                        l2.shadowsEnabled = castShadows;
                    }
                    break;
                }
            }

            // Broadcast to subscribed renderers (glow stacks + baked-lit layers).
            // MaterialPropertyBlock per renderer; never Shader.SetGlobal*.
            if (beatTargets == null || beatTargets.Length == 0) return;
            if (block == null) block = new MaterialPropertyBlock();
            for (int i = 0; i < beatTargets.Length; i++)
            {
                Renderer r = beatTargets[i];
                if (r == null) continue;
                r.GetPropertyBlock(block);
                block.SetFloat(BeatValueIds[beatChannel], beat);
                block.SetFloat(BeatValueId, beat);
                if (kind == VfxLightDriverKind.MaterialOnly)
                    block.SetFloat(BakedLightBeatId, beat * bakedEmissionMul);
                r.SetPropertyBlock(block);
            }
        }

        private static Color ApplySaturation(Color c, float mul)
        {
            float lum = c.r * 0.2126f + c.g * 0.7152f + c.b * 0.0722f;
            return new Color(
                Mathf.LerpUnclamped(lum, c.r, mul),
                Mathf.LerpUnclamped(lum, c.g, mul),
                Mathf.LerpUnclamped(lum, c.b, mul),
                c.a);
        }

        private static float Hash1(float p)
        {
            p = (p * 0.1031f) % 1f;
            if (p < 0f) p += 1f;
            p *= p + 33.33f;
            p *= p + p;
            p %= 1f;
            return p < 0f ? p + 1f : p;
        }

        private static float ValueNoise1(float x)
        {
            float i = Mathf.Floor(x);
            float f = x - i;
            float u = f * f * (3f - 2f * f);
            return Mathf.Lerp(Hash1(i), Hash1(i + 1f), u);
        }
    }
}

using System;
using UnityEngine;
using VFXComposer.Capabilities;

namespace VFXComposer
{
    /// <summary>
    /// Player-safe neutral capability blank. The renderer follows the same pure sampled
    /// trajectory used by machine acceptance; element and style skins are deliberately absent.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CapabilityBlankVfxController : MonoBehaviour, IVfxRuntimeEntry
    {
        [SerializeField] private string motionType = "linear";
        [SerializeField] private string hitType = "single";
        [SerializeField] private string emissionType = "single";
        [SerializeField] private string timingType = "instant";
        [SerializeField] private string[] motionKeys = new string[0];
        [SerializeField] private float[] motionValues = new float[0];
        [SerializeField] private string[] hitKeys = new string[0];
        [SerializeField] private float[] hitValues = new float[0];
        [SerializeField] private string[] emissionKeys = new string[0];
        [SerializeField] private float[] emissionValues = new float[0];
        [SerializeField] private string[] timingKeys = new string[0];
        [SerializeField] private float[] timingValues = new float[0];
        [SerializeField, Min(.1f)] private float duration = 2f;
        [SerializeField] private uint seed = 1;
        [SerializeField] private Transform core;
        [SerializeField] private Renderer coreRenderer;
        [SerializeField] private TrailRenderer directionTrail;
        [SerializeField] private LineRenderer beamLine;
        [SerializeField, Min(.001f)] private float baseBeamWidth = .08f;
        [SerializeField] private Transform areaBoundary;
        [SerializeField] private Renderer areaRenderer;
        [SerializeField] private Transform eventMarker;
        [SerializeField] private Renderer eventRenderer;
        [SerializeField] private ParticleSystem carrierParticles;
        [SerializeField] private ParticleSystemRenderer carrierRenderer;
        [SerializeField] private BeamCapabilityVisualExecutor beamVisual;
        [SerializeField] private TimingAreaCapabilityVisualExecutor timingAreaVisual;

        private const int MaxCarrierCount = 24;
        private CapabilitySampleTrace trace;
        private float elapsed;
        private float markerAge;
        private int eventCursor;
        private bool playing;
        private string lastExit = string.Empty;
        private readonly ParticleSystem.Particle[] carrierParticleBuffer = new ParticleSystem.Particle[MaxCarrierCount];
        private readonly Vector3[] carrierPositions = new Vector3[MaxCarrierCount];
        private readonly Vector3[] carrierDirections = new Vector3[MaxCarrierCount];
        private readonly float[] carrierScales = new float[MaxCarrierCount];
        private int visibleCarrierCount;
        private int processedChainHopCount;
        private int showcasePhaseIndex = -1;
        private string activeShowcaseMode = string.Empty;
        private Vector3 coreBaseScale = Vector3.one;
        private Vector3 markerBaseScale = Vector3.one;
        private bool defaultsCaptured;

        public bool IsAlive { get { return playing; } }
        public string MotionType { get { return motionType; } }
        public string HitType { get { return hitType; } }
        public string EmissionType { get { return emissionType; } }
        public string TimingType { get { return timingType; } }
        public CapabilitySampleTrace Trace { get { return trace; } }
        public string LastExit { get { return lastExit; } }
        public float Duration { get { return duration; } }
        public int VisibleCarrierCount { get { return visibleCarrierCount; } }
        public int ProcessedChainHopCount { get { return processedChainHopCount; } }
        public int ShowcasePhaseIndex { get { return showcasePhaseIndex; } }
        public string ActiveShowcaseMode { get { return activeShowcaseMode; } }
        public Vector3 CoreVisualPosition { get { return core == null ? Vector3.zero : core.localPosition; } }
        public BeamCapabilityVisualExecutor BeamVisual { get { return beamVisual; } }
        public TimingAreaCapabilityVisualExecutor TimingAreaVisual { get { return timingAreaVisual; } }

        private void Awake() { CaptureDefaults(); ResetForPool(); }

        private void Update()
        {
            if (!playing || trace == null) return;
            elapsed += Mathf.Max(0f, Time.deltaTime);
            ApplyAtTime(elapsed);
            markerAge += Mathf.Max(0f, Time.deltaTime);
            if (beamVisual == null && timingAreaVisual == null && eventRenderer != null && markerAge >= .2f) eventRenderer.enabled = false;
            if (elapsed >= duration) Complete();
        }

        public void Initialize(VfxRuntimeContext context)
        {
            transform.SetPositionAndRotation(context.Position, context.Rotation);
            ResetForPool();
        }

        public void Play()
        {
            CaptureDefaults();
            trace = CapabilitySampler.SampleTrajectory(BuildRequest());
            playing = true;
            elapsed = 0f;
            markerAge = 99f;
            eventCursor = 0;
            lastExit = string.Empty;
            visibleCarrierCount = 0;
            processedChainHopCount = 0;
            showcasePhaseIndex = -1;
            activeShowcaseMode = string.Empty;
            if (core != null) core.localScale = coreBaseScale;
            if (eventMarker != null) eventMarker.localScale = markerBaseScale;
            if (beamVisual != null) beamVisual.Begin(trace, duration);
            if (timingAreaVisual != null) timingAreaVisual.Begin(trace, duration);
            if (coreRenderer != null) coreRenderer.enabled = beamVisual == null && timingAreaVisual == null;
            if (directionTrail != null)
            {
                directionTrail.Clear();
                directionTrail.enabled = true;
                directionTrail.emitting = true;
            }
            if (beamLine != null) beamLine.enabled = beamVisual == null && timingAreaVisual == null;
            if (areaRenderer != null) areaRenderer.enabled = timingAreaVisual == null;
            if (eventRenderer != null) eventRenderer.enabled = false;
            if (carrierParticles != null)
            {
                carrierParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                carrierParticles.Play(true);
            }
            if (carrierRenderer != null) carrierRenderer.enabled = false;
            ApplyAtTime(0f);
        }

        /// <summary>Deterministic machine/preview readback without changing the sampled contract.</summary>
        public void EvaluateVisualAtTime(float time)
        {
            if (trace == null) return;
            elapsed = Mathf.Clamp(time, 0f, duration);
            eventCursor = 0;
            markerAge = 99f;
            processedChainHopCount = 0;
            if (core != null) core.localScale = coreBaseScale;
            if (eventMarker != null) eventMarker.localScale = markerBaseScale;
            if (eventRenderer != null) eventRenderer.enabled = false;
            ApplyAtTime(elapsed);
        }

        public Vector3 GetCarrierPosition(int index)
        {
            if (index < 0 || index >= visibleCarrierCount) throw new ArgumentOutOfRangeException("index");
            return carrierPositions[index];
        }

        public Vector3 GetCarrierDirection(int index)
        {
            if (index < 0 || index >= visibleCarrierCount) throw new ArgumentOutOfRangeException("index");
            return carrierDirections[index];
        }

        public float GetCarrierScale(int index)
        {
            if (index < 0 || index >= visibleCarrierCount) throw new ArgumentOutOfRangeException("index");
            return carrierScales[index];
        }

        public bool SendEvent(string eventId, VfxRuntimeEvent payload)
        {
            if (eventId == "play" || eventId == "start" || eventId == "launch")
            {
                transform.SetPositionAndRotation(payload.Position, payload.Rotation);
                Play();
                return true;
            }
            if (eventId == "stop" || eventId == "cancel")
            {
                Cancel();
                return true;
            }
            if (eventId == "complete" || eventId == "release") { Complete(); return true; }
            return false;
        }

        public bool StartBeam(Vector3 source, Vector3 target)
        {
            if (beamVisual == null) return false;
            beamVisual.SetEndpoints(source, target);
            Play();
            return true;
        }

        public bool BindBeamEndpoints(Transform source, Transform target)
        {
            if (beamVisual == null) return false;
            beamVisual.BindEndpoints(source, target);
            return true;
        }

        public bool SetBeamObstacleProbe(BeamCapabilityObstacleProbe probe)
        {
            if (beamVisual == null) return false;
            beamVisual.SetObstacleProbe(probe);
            return true;
        }

        public bool StopBeam()
        {
            if (beamVisual == null) return false;
            Stop(VfxStopMode.Immediate);
            return true;
        }

        public void Complete() { if (!playing) return; lastExit = "complete"; Stop(VfxStopMode.AllowTail); }
        public void Cancel() { if (!playing) return; lastExit = "cancel"; Stop(VfxStopMode.AllowTail); }

        public void Stop(VfxStopMode mode)
        {
            playing = false;
            if (beamVisual != null) beamVisual.Stop(lastExit, mode);
            if (timingAreaVisual != null) timingAreaVisual.Stop(lastExit, mode);
            if (directionTrail != null)
            {
                directionTrail.emitting = false;
                if (mode == VfxStopMode.Immediate) directionTrail.Clear();
                directionTrail.enabled = false;
            }
            if (beamLine != null && beamVisual == null && timingAreaVisual == null) beamLine.enabled = false;
            if (areaRenderer != null && timingAreaVisual == null) areaRenderer.enabled = false;
            if (coreRenderer != null && beamVisual == null && timingAreaVisual == null) coreRenderer.enabled = false;
            if (eventRenderer != null && beamVisual == null && timingAreaVisual == null) eventRenderer.enabled = false;
            ClearCarrierVisuals();
        }

        public void ResetForPool()
        {
            playing = false;
            trace = null;
            elapsed = 0f;
            eventCursor = 0;
            markerAge = 99f;
            lastExit = string.Empty;
            visibleCarrierCount = 0;
            processedChainHopCount = 0;
            showcasePhaseIndex = -1;
            activeShowcaseMode = string.Empty;
            CaptureDefaults();
            if (beamVisual != null) beamVisual.ResetVisuals();
            if (timingAreaVisual != null) timingAreaVisual.ResetVisuals();
            if (core != null) { core.localPosition = Vector3.zero; core.localScale = coreBaseScale; }
            if (coreRenderer != null) coreRenderer.enabled = false;
            if (directionTrail != null) { directionTrail.emitting = false; directionTrail.Clear(); directionTrail.enabled = false; }
            if (beamLine != null) beamLine.enabled = false;
            if (areaBoundary != null) areaBoundary.localScale = Vector3.zero;
            if (areaRenderer != null) areaRenderer.enabled = false;
            if (eventMarker != null) eventMarker.localScale = markerBaseScale;
            if (eventRenderer != null) eventRenderer.enabled = false;
            ClearCarrierVisuals();
        }

        private CapabilitySampleRequest BuildRequest()
        {
            var request = new CapabilitySampleRequest
            {
                MotionType = motionType,
                HitType = hitType,
                EmissionType = emissionType,
                TimingType = timingType,
                Origin = Vector3.zero,
                Direction = Vector3.right,
                Target = new Vector3(4f, .4f, 0f),
                Duration = duration,
                DeltaTime = 1f / 60f,
                Seed = seed,
                CollisionMin = new Vector3(-3f, -.8f, -1f),
                CollisionMax = new Vector3(3f, .8f, 1f)
            };
            Copy(motionKeys, motionValues, request.Motion);
            Copy(hitKeys, hitValues, request.Hit);
            Copy(emissionKeys, emissionValues, request.Emission);
            Copy(timingKeys, timingValues, request.Timing);
            if (timingAreaVisual != null) timingAreaVisual.ApplyExternalInputs(request);
            return request;
        }

        private void ApplyAtTime(float time)
        {
            var index = Mathf.Clamp(Mathf.RoundToInt(time * 60f), 0, trace.Frames.Count - 1);
            var frame = trace.Frames[index];
            if (core != null && beamVisual == null && timingAreaVisual == null)
            {
                core.localScale = coreBaseScale;
                core.localPosition = beamLine == null ? frame.Position : frame.Source;
                if (frame.Velocity.sqrMagnitude > .000001f) core.localRotation = Quaternion.FromToRotation(Vector3.right, frame.Velocity.normalized);
            }
            if (beamVisual != null)
            {
                beamVisual.Evaluate(trace, frame, time, duration);
            }
            else if (timingAreaVisual != null)
            {
                timingAreaVisual.Evaluate(trace, frame, time, duration);
            }
            else if (beamLine != null)
            {
                beamLine.positionCount = 2;
                beamLine.SetPosition(0, frame.Source);
                beamLine.SetPosition(1, frame.Target);
                beamLine.widthMultiplier = baseBeamWidth * Mathf.Max(.1f, frame.Width);
            }
            if (areaBoundary != null && timingAreaVisual == null)
            {
                areaBoundary.localPosition = frame.Position;
                var scale = frame.Radius > .001f ? frame.Radius : Mathf.Lerp(.35f, 1f, frame.Progress);
                areaBoundary.localScale = Vector3.one * scale;
            }
            while (eventCursor < trace.Events.Count && trace.Events[eventCursor].Time <= time + .0001f)
            {
                var item = trace.Events[eventCursor++];
                if (beamVisual == null && timingAreaVisual == null && (item.Type == "on_hit" || item.Type == "on_split" || item.Type == "on_bounce" || item.Type == "on_release") && eventMarker != null)
                {
                    eventMarker.localPosition = item.Position;
                    eventMarker.localScale = markerBaseScale;
                    if (item.Type == "on_hit" && item.Detail == "chain_hop")
                    {
                        processedChainHopCount = item.Sequence;
                        var dampingScale = Mathf.Max(.25f, item.After.magnitude);
                        eventMarker.localScale = markerBaseScale * dampingScale;
                        if (core != null) core.localScale = coreBaseScale * dampingScale;
                    }
                    markerAge = 0f;
                    if (eventRenderer != null) eventRenderer.enabled = true;
                }
            }
            ApplyChainDampingAtTime(time);
            UpdateCarrierVisuals(time);
            var carrierDriven = carrierParticles != null && (visibleCarrierCount > 0 || emissionType == "volley_showcase" || emissionType == "fan" || emissionType == "burst_stagger" || emissionType == "ring");
            if (coreRenderer != null && beamVisual == null && timingAreaVisual == null) coreRenderer.enabled = playing && !carrierDriven;
            if (directionTrail != null)
            {
                directionTrail.enabled = playing && !carrierDriven;
                directionTrail.emitting = playing && !carrierDriven;
            }
        }

        private void ApplyChainDampingAtTime(float time)
        {
            if (hitType != "chain_hop" || trace == null) return;
            CapabilitySampleEvent latest = null;
            for (var i = 0; i < trace.Events.Count; i++)
            {
                var item = trace.Events[i];
                if (item.Type == "on_hit" && item.Detail == "chain_hop" && item.Time <= time + .0001f) latest = item;
            }
            if (latest == null) return;
            processedChainHopCount = latest.Sequence;
            if (core != null) core.localScale = coreBaseScale * Mathf.Max(.25f, latest.After.magnitude);
        }

        private void UpdateCarrierVisuals(float time)
        {
            visibleCarrierCount = 0;
            showcasePhaseIndex = -1;
            activeShowcaseMode = string.Empty;
            if (trace == null) { ApplyCarrierParticles(); return; }

            if (hitType == "split")
            {
                for (var i = 0; i < trace.Events.Count && visibleCarrierCount < MaxCarrierCount; i++)
                {
                    var item = trace.Events[i];
                    if (item.Type != "on_split" || item.Time > time + .0001f) continue;
                    AddCarrier(item, time, .6f);
                }
            }
            else if (emissionType == "volley_showcase")
            {
                var phaseDuration = Mathf.Max(1f / 60f, GetNumber(emissionKeys, emissionValues, "phase_duration", duration / 3f));
                showcasePhaseIndex = Mathf.Clamp(Mathf.FloorToInt(time / phaseDuration), 0, 2);
                activeShowcaseMode = showcasePhaseIndex == 0 ? "fan" : showcasePhaseIndex == 1 ? "burst_stagger" : "ring";
                AddEmissionCarriers(time, activeShowcaseMode);
            }
            else if (emissionType == "fan" || emissionType == "burst_stagger" || emissionType == "ring")
            {
                activeShowcaseMode = emissionType;
                AddEmissionCarriers(time, emissionType);
            }
            ApplyCarrierParticles();
        }

        private void AddEmissionCarriers(float time, string mode)
        {
            for (var i = 0; i < trace.Events.Count && visibleCarrierCount < MaxCarrierCount; i++)
            {
                var item = trace.Events[i];
                if (item.Type != "on_emit" || item.Detail != mode || item.Time > time + .0001f) continue;
                AddCarrier(item, time, 1f);
            }
        }

        private void AddCarrier(CapabilitySampleEvent item, float time, float scale)
        {
            if (visibleCarrierCount >= MaxCarrierCount) return;
            var direction = item.After.sqrMagnitude < .000001f ? Vector3.right : item.After.normalized;
            var speed = Mathf.Max(.1f, GetNumber(motionKeys, motionValues, "speed", 4f));
            var position = item.Position + direction * speed * Mathf.Max(0f, time - item.Time);
            var index = visibleCarrierCount++;
            carrierPositions[index] = position;
            carrierDirections[index] = item.After;
            carrierScales[index] = scale;
            var size = Mathf.Max(.001f, Mathf.Max(coreBaseScale.x, Mathf.Max(coreBaseScale.y, coreBaseScale.z)) * scale);
            var particle = new ParticleSystem.Particle
            {
                position = position,
                velocity = direction * .001f,
                startLifetime = 60f,
                remainingLifetime = 60f,
                startColor = Color.white,
                startSize3D = new Vector3(size * 1.45f, size * .72f, size * .72f),
                rotation3D = new Vector3(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg)
            };
            carrierParticleBuffer[index] = particle;
        }

        private void ApplyCarrierParticles()
        {
            if (carrierParticles != null) carrierParticles.SetParticles(carrierParticleBuffer, visibleCarrierCount);
            if (carrierRenderer != null) carrierRenderer.enabled = playing && visibleCarrierCount > 0;
        }

        private void ClearCarrierVisuals()
        {
            visibleCarrierCount = 0;
            showcasePhaseIndex = -1;
            activeShowcaseMode = string.Empty;
            if (carrierParticles != null) carrierParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (carrierRenderer != null) carrierRenderer.enabled = false;
        }

        private void CaptureDefaults()
        {
            if (defaultsCaptured) return;
            if (core != null) coreBaseScale = core.localScale;
            if (eventMarker != null) markerBaseScale = eventMarker.localScale;
            if (carrierRenderer == null && carrierParticles != null) carrierRenderer = carrierParticles.GetComponent<ParticleSystemRenderer>();
            defaultsCaptured = true;
        }

        private static float GetNumber(string[] keys, float[] values, string key, float fallback)
        {
            var count = Mathf.Min(keys == null ? 0 : keys.Length, values == null ? 0 : values.Length);
            for (var i = 0; i < count; i++) if (keys[i] == key) return values[i];
            return fallback;
        }

        private static void Copy(string[] keys, float[] values, System.Collections.Generic.Dictionary<string, double> destination)
        {
            var count = Mathf.Min(keys == null ? 0 : keys.Length, values == null ? 0 : values.Length);
            for (var i = 0; i < count; i++) if (!string.IsNullOrEmpty(keys[i])) destination[keys[i]] = values[i];
        }
    }
}

using System;
using UnityEngine;
using VFXComposer.Capabilities;

namespace VFXComposer
{
    /// <summary>
    /// Bounded visual execution layer for the eight W-C2 neutral beam capabilities. It consumes
    /// the deterministic trace but owns real line/marker topology and public runtime readbacks.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BeamCapabilityVisualExecutor : MonoBehaviour
    {
        public const int MaxReflectSegments = 16;
        public const int MaxArcHops = 8;
        public const int ArcSamplesPerHop = 4;
        public const int MaxMarkerParticles = 16;

        [SerializeField] private string visualMode = "sustained";
        [SerializeField] private LineRenderer primaryLine;
        [SerializeField] private LineRenderer[] auxiliaryLines = new LineRenderer[0];
        [SerializeField] private Transform sourceMarker;
        [SerializeField] private Renderer sourceRenderer;
        [SerializeField] private Transform endpointMarker;
        [SerializeField] private Renderer endpointRenderer;
        [SerializeField] private ParticleSystem markerParticles;
        [SerializeField] private ParticleSystemRenderer markerParticleRenderer;
        [SerializeField, Min(.001f)] private float baseWidth = .08f;
        [SerializeField, Min(.01f)] private float tilingPerMeter = 1f;
        [SerializeField, Range(.1f, .2f)] private float hitscanLinger = .15f;
        [SerializeField, Min(0f)] private float sweepSpeedMax = 90f;
        [SerializeField, Min(0f)] private float sweepInertia = .12f;
        [SerializeField, Range(1, MaxReflectSegments)] private int reflectSegmentLimit = 3;
        [SerializeField, Range(0f, 1f)] private float reflectDamping = .2f;
        [SerializeField, Range(2, 5)] private int convergeSourceCount = 4;
        [SerializeField, Min(0f)] private float focusGrowth = 1.5f;
        [SerializeField, Range(1, MaxArcHops)] private int arcHopCount = 4;
        [SerializeField, Min(0f)] private float arcSag = .3f;
        [SerializeField, Min(0f)] private float arcJitter = .12f;
        [SerializeField] private uint seed = 1;

        private readonly Vector3[] pointBuffer = new Vector3[1 + MaxArcHops * ArcSamplesPerHop];
        private readonly Vector3[] convergeSources = new Vector3[5];
        private readonly float[] segmentWidths = new float[MaxReflectSegments];
        private readonly float[] segmentBrightness = new float[MaxReflectSegments];
        private readonly ParticleSystem.Particle[] markerBuffer = new ParticleSystem.Particle[MaxMarkerParticles];
        private CapabilitySampleTrace trace;
        private Transform boundSource;
        private Transform boundTarget;
        private bool fixedEndpoints;
        private Vector3 fixedSource;
        private Vector3 fixedTarget;
        private BeamCapabilityObstacleProbe obstacleProbe;
        private Vector3 sourceBaseScale = Vector3.one;
        private Vector3 endpointBaseScale = Vector3.one;
        private bool defaultsCaptured;
        private bool executing;
        private float duration = 1f;
        private float lastEvaluationTime = -1f;
        private float lastSweepAngle;
        private int evaluationFrame;
        private bool priorObstacleBlocked;
        private Collider priorBlocker;
        private string exitVisual = string.Empty;
        private float exitAge;

        public string VisualMode { get { return visualMode; } }
        public bool IsExecuting { get { return executing; } }
        public Vector3 EffectiveSource { get; private set; }
        public Vector3 EffectiveTarget { get; private set; }
        public float BeamLength { get; private set; }
        public float TextureTileCount { get; private set; }
        public float LineAlpha { get; private set; }
        public float EffectiveWidth { get; private set; }
        public float Brightness { get; private set; }
        public float HitscanFade { get; private set; }
        public int VisibleLineCount { get; private set; }
        public int PrimaryPointCount { get; private set; }
        public bool EndpointMarkerVisible { get { return endpointRenderer != null && endpointRenderer.enabled; } }
        public Vector3 EndpointMarkerScale { get { return endpointMarker == null ? Vector3.zero : endpointMarker.localScale; } }
        public string ExitVisual { get { return exitVisual; } }
        public float ExitNormalizedAge { get { return Mathf.Clamp01(exitAge / .2f); } }
        public int ChargeTier { get; private set; }
        public float SweepAngle { get; private set; }
        public float SweepAngularVelocity { get; private set; }
        public float SweepSpeedLimit { get { return sweepSpeedMax; } }
        public float SweepInertia { get { return sweepInertia; } }
        public bool SweepUsesTraceTarget { get { return visualMode == "sweep"; } }
        public int ReflectSegmentCount { get; private set; }
        public int BounceMarkerCount { get; private set; }
        public bool ObstacleProbeReady { get { return obstacleProbe != null && obstacleProbe.IsConfigured; } }
        public bool ObstacleBlocked { get; private set; }
        public bool OcclusionFailClosed { get; private set; }
        public int LastObstacleResponseFrames { get; private set; }
        public int ConvergeLineCount { get; private set; }
        public float FocusScale { get; private set; }
        public int ArcVisibleHopCount { get; private set; }
        public int ArcPointCount { get; private set; }
        public int MarkerParticleCount { get { return markerParticles == null ? 0 : markerParticles.particleCount; } }
        public bool UsesTransformEndpoints { get { return boundSource != null || boundTarget != null; } }
        public bool UsesFixedEndpoints { get { return fixedEndpoints; } }
        public bool AllVisualsHidden
        {
            get
            {
                if (primaryLine != null && primaryLine.enabled) return false;
                if (auxiliaryLines != null)
                    for (var i = 0; i < auxiliaryLines.Length; i++)
                        if (auxiliaryLines[i] != null && auxiliaryLines[i].enabled) return false;
                if (sourceRenderer != null && sourceRenderer.enabled) return false;
                if (endpointRenderer != null && endpointRenderer.enabled) return false;
                return markerParticles == null || markerParticles.particleCount == 0;
            }
        }

        private void Awake()
        {
            CaptureDefaults();
            ResetVisuals();
        }

        private void Update()
        {
            if (executing || string.IsNullOrEmpty(exitVisual)) return;
            EvaluateExitAtTime(exitAge + Mathf.Max(0f, Time.deltaTime));
        }

        public void BindEndpoints(Transform source, Transform target)
        {
            boundSource = source;
            boundTarget = target;
            fixedEndpoints = false;
        }

        public void SetEndpoints(Vector3 source, Vector3 target)
        {
            fixedSource = source;
            fixedTarget = target;
            fixedEndpoints = true;
            boundSource = null;
            boundTarget = null;
        }

        public void ClearEndpointProtocol()
        {
            fixedEndpoints = false;
            boundSource = null;
            boundTarget = null;
        }

        public void SetObstacleProbe(BeamCapabilityObstacleProbe value) { obstacleProbe = value; }
        public void ClearObstacleProbe() { obstacleProbe = null; }

        public void Begin(CapabilitySampleTrace sampledTrace, float sampledDuration)
        {
            CaptureDefaults();
            trace = sampledTrace;
            duration = Mathf.Max(1f / 60f, sampledDuration);
            executing = trace != null;
            lastEvaluationTime = -1f;
            lastSweepAngle = 0f;
            evaluationFrame = 0;
            exitVisual = string.Empty;
            exitAge = 0f;
            ResetReadbacks();
            HideAllVisuals();
            if (markerParticles != null)
            {
                markerParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                markerParticles.Play(true);
            }
        }

        public void Evaluate(CapabilitySampleTrace sampledTrace, CapabilitySampleFrame frame, float time, float sampledDuration)
        {
            if (sampledTrace == null || frame == null) return;
            if (trace != sampledTrace) Begin(sampledTrace, sampledDuration);
            duration = Mathf.Max(1f / 60f, sampledDuration);
            executing = true;
            exitVisual = string.Empty;
            exitAge = 0f;
            evaluationFrame++;
            HideLinesAndMarkersForFrame();

            if (visualMode == "hitscan") ApplyHitscan(frame, time);
            else if (visualMode == "sweep") ApplySweep(frame, time);
            else if (visualMode == "charge_scale") ApplyCharge(frame, time);
            else if (visualMode == "reflect") ApplyReflect(frame, time);
            else if (visualMode == "occlude") ApplyOcclude(frame, time);
            else if (visualMode == "converge") ApplyConverge(frame, time);
            else if (visualMode == "arc_link") ApplyArcLink(frame, time);
            else ApplySustained(frame);
            lastEvaluationTime = time;
        }

        public void Stop(string exit, VfxStopMode mode)
        {
            executing = false;
            HideAllVisuals();
            if (visualMode == "charge_scale" && mode == VfxStopMode.AllowTail && (exit == "complete" || exit == "cancel"))
            {
                exitVisual = exit == "complete" ? "endpoint_burst" : "cancel_collapse";
                EvaluateExitAtTime(0f);
            }
            else
            {
                exitVisual = string.Empty;
                exitAge = 0f;
            }
        }

        public void EvaluateExitAtTime(float age)
        {
            exitAge = Mathf.Max(0f, age);
            if (string.IsNullOrEmpty(exitVisual) || endpointRenderer == null || endpointMarker == null) return;
            var normalized = Mathf.Clamp01(exitAge / .2f);
            if (normalized >= 1f)
            {
                endpointRenderer.enabled = false;
                exitVisual = string.Empty;
                return;
            }
            var burst = exitVisual == "endpoint_burst";
            var scale = burst ? Mathf.Lerp(2.4f, .25f, normalized) : Mathf.Lerp(.82f, .12f, normalized);
            var brightness = burst ? Mathf.Lerp(2.2f, .2f, normalized) : Mathf.Lerp(.55f, .08f, normalized);
            endpointMarker.localPosition = EffectiveTarget;
            endpointMarker.localScale = endpointBaseScale * scale;
            endpointRenderer.enabled = true;
            ApplyRendererColor(endpointRenderer, brightness, 1f - normalized);
        }

        public void ResetVisuals()
        {
            CaptureDefaults();
            executing = false;
            trace = null;
            exitVisual = string.Empty;
            exitAge = 0f;
            lastEvaluationTime = -1f;
            evaluationFrame = 0;
            priorObstacleBlocked = false;
            priorBlocker = null;
            ResetReadbacks();
            HideAllVisuals();
            if (sourceMarker != null) sourceMarker.localScale = sourceBaseScale;
            if (endpointMarker != null) endpointMarker.localScale = endpointBaseScale;
        }

        public Vector3 GetPrimaryPoint(int index)
        {
            if (index < 0 || index >= PrimaryPointCount) throw new ArgumentOutOfRangeException("index");
            if (visualMode == "reflect" || visualMode == "arc_link") return pointBuffer[index];
            if (primaryLine == null) throw new InvalidOperationException("Primary line is unavailable.");
            return primaryLine.GetPosition(index);
        }

        public Vector3 GetConvergeSource(int index)
        {
            if (index < 0 || index >= ConvergeLineCount) throw new ArgumentOutOfRangeException("index");
            return convergeSources[index];
        }

        public float GetReflectSegmentWidth(int index)
        {
            if (index < 0 || index >= ReflectSegmentCount) throw new ArgumentOutOfRangeException("index");
            return segmentWidths[index];
        }

        public float GetReflectSegmentBrightness(int index)
        {
            if (index < 0 || index >= ReflectSegmentCount) throw new ArgumentOutOfRangeException("index");
            return segmentBrightness[index];
        }

        private void ApplyHitscan(CapabilitySampleFrame frame, float time)
        {
            ResolveEndpoints(frame, true, out var source, out var target);
            HitscanFade = 1f - Mathf.Clamp01(time / Mathf.Max(.1f, hitscanLinger));
            ApplyStraightLine(source, target, baseWidth * HitscanFade, HitscanFade, 1f, HitscanFade > .0001f);
            ApplySourceMarker(source, HitscanFade > .0001f);
            ApplyEndpointMarker(target, endpointBaseScale, 1f, HitscanFade, HitscanFade > .0001f);
        }

        private void ApplySustained(CapabilitySampleFrame frame)
        {
            ResolveEndpoints(frame, true, out var source, out var target);
            ApplyStraightLine(source, target, baseWidth, 1f, 1f, true);
            ApplySourceMarker(source, true);
            ApplyEndpointMarker(target, endpointBaseScale, 1f, 1f, true);
        }

        private void ApplySweep(CapabilitySampleFrame frame, float time)
        {
            ResolveEndpoints(frame, false, out var source, out var ignored);
            var traceSource = frame.Source;
            var target = source + (frame.Target - traceSource);
            var initial = trace.Frames.Count == 0 ? target - source : trace.Frames[0].Target - trace.Frames[0].Source;
            SweepAngle = Vector3.SignedAngle(initial, target - source, Vector3.forward);
            var delta = lastEvaluationTime < 0f ? 0f : Mathf.Max(.000001f, time - lastEvaluationTime);
            SweepAngularVelocity = delta <= .000001f ? 0f : Mathf.Abs(Mathf.DeltaAngle(lastSweepAngle, SweepAngle)) / delta;
            lastSweepAngle = SweepAngle;
            ApplyStraightLine(source, target, baseWidth, 1f, 1f, true);
            ApplySourceMarker(source, true);
            ApplyEndpointMarker(target, endpointBaseScale, 1.15f, 1f, true);
        }

        private void ApplyCharge(CapabilitySampleFrame frame, float time)
        {
            ResolveEndpoints(frame, true, out var source, out var target);
            ChargeTier = Mathf.Clamp(frame.Stage + 1, 1, 3);
            Brightness = 1f + (ChargeTier - 1) * .48f;
            EffectiveWidth = baseWidth * Mathf.Max(1f, frame.Width);
            ApplyStraightLine(source, target, EffectiveWidth, 1f, Brightness, true);
            ApplySourceMarker(source, true);
            var scale = endpointBaseScale * (0.72f + ChargeTier * .24f);
            ApplyEndpointMarker(target, scale, Brightness, 1f, true);
        }

        private void ApplyReflect(CapabilitySampleFrame frame, float time)
        {
            var source = ResolveSource(frame.Source);
            pointBuffer[0] = source;
            var sourceOffset = source - frame.Source;
            var available = 0;
            var lastReflectedDirection = Vector3.zero;
            // N rendered segments have exactly N-1 bounce vertices. Do not consume the next
            // bounce merely to clamp it away: its outgoing direction belongs to segment N+1
            // and would reverse/corrupt the visible tail of the bounded N-segment polyline.
            var bounceLimit = Mathf.Max(0, reflectSegmentLimit - 1);
            for (var i = 0; i < trace.Events.Count && available < bounceLimit; i++)
            {
                var item = trace.Events[i];
                if (item.Type != "on_bounce" || item.Detail != "reflect" || item.Time > time + .0001f) continue;
                available++;
                pointBuffer[available] = item.Position + sourceOffset;
                lastReflectedDirection = item.After;
            }
            var segmentCount = Mathf.Clamp(available + 1, 1, reflectSegmentLimit);
            var tailIndex = segmentCount;
            var tail = frame.Position + sourceOffset;
            if (available > 0 && lastReflectedDirection.sqrMagnitude > .000001f)
            {
                // The sampled frame can wrap to a post-bounce position on the preceding leg.
                // Preserve its remaining travel distance, but take the final segment direction
                // from the last authoritative bounce event so the rendered polyline cannot fold
                // back across the reflector.
                var remainingDistance = Mathf.Max(.05f, Vector3.Distance(pointBuffer[tailIndex - 1], tail));
                tail = pointBuffer[tailIndex - 1] + lastReflectedDirection.normalized * remainingDistance;
            }
            else if ((tail - pointBuffer[tailIndex - 1]).sqrMagnitude <= .000001f)
            {
                var direction = frame.Velocity.sqrMagnitude <= .000001f ? Vector3.right : frame.Velocity.normalized;
                tail = pointBuffer[tailIndex - 1] + direction * .05f;
            }
            pointBuffer[tailIndex] = tail;
            ReflectSegmentCount = segmentCount;
            PrimaryPointCount = segmentCount + 1;
            EffectiveSource = source;
            EffectiveTarget = tail;
            ConfigureReflectLine(pointBuffer, PrimaryPointCount, segmentCount);
            ApplySourceMarker(source, true);
            ApplyEndpointMarker(tail, endpointBaseScale, segmentBrightness[segmentCount - 1], 1f, true);
            BounceMarkerCount = Mathf.Max(0, segmentCount - 1);
            ApplyBounceMarkers(pointBuffer, BounceMarkerCount);
        }

        private void ApplyOcclude(CapabilitySampleFrame frame, float time)
        {
            ResolveEndpoints(frame, true, out var source, out var desiredTarget);
            if (!ObstacleProbeReady)
            {
                OcclusionFailClosed = true;
                ObstacleBlocked = false;
                EffectiveSource = source;
                EffectiveTarget = source;
                BeamLength = 0f;
                TextureTileCount = 0f;
                VisibleLineCount = 0;
                PrimaryPointCount = 0;
                return;
            }

            OcclusionFailClosed = false;
            Vector3 worldHit;
            Collider blocker;
            var blocked = obstacleProbe.TryGetFirstBlocker(transform.TransformPoint(source), transform.TransformPoint(desiredTarget), out worldHit, out blocker);
            var target = blocked ? transform.InverseTransformPoint(worldHit) : desiredTarget;
            if (blocked != priorObstacleBlocked || blocker != priorBlocker) LastObstacleResponseFrames = 0;
            else LastObstacleResponseFrames = Mathf.Min(2, LastObstacleResponseFrames + 1);
            priorObstacleBlocked = blocked;
            priorBlocker = blocker;
            ObstacleBlocked = blocked;
            ApplyStraightLine(source, target, baseWidth, 1f, 1f, true);
            ApplySourceMarker(source, true);
            ApplyEndpointMarker(target, endpointBaseScale * 1.15f, 1.6f, 1f, blocked);
        }

        private void ApplyConverge(CapabilitySampleFrame frame, float time)
        {
            ResolveEndpoints(frame, true, out var ignoredSource, out var focus);
            var count = 0;
            for (var i = 0; i < trace.Events.Count && count < convergeSourceCount && count < 5; i++)
            {
                var item = trace.Events[i];
                if (item.Type != "on_emit" || item.Detail != "converge") continue;
                convergeSources[count] = item.Position;
                var line = count == 0 ? primaryLine : count - 1 < auxiliaryLines.Length ? auxiliaryLines[count - 1] : null;
                if (line == null) break;
                ConfigureStraightLine(line, item.Position, focus, baseWidth * .72f, 1f, 1f, true);
                count++;
            }
            ConvergeLineCount = count;
            VisibleLineCount = count;
            PrimaryPointCount = count > 0 ? 2 : 0;
            EffectiveSource = count > 0 ? convergeSources[0] : ignoredSource;
            EffectiveTarget = focus;
            BeamLength = count > 0 ? Vector3.Distance(convergeSources[0], focus) : 0f;
            TextureTileCount = BeamLength * tilingPerMeter;
            FocusScale = 1f + focusGrowth * Mathf.Clamp01(time / duration);
            ApplySourceMarker(ignoredSource, false);
            ApplyEndpointMarker(focus, endpointBaseScale * FocusScale, 1f + .4f * FocusScale, 1f, count > 0);
        }

        private void ApplyArcLink(CapabilitySampleFrame frame, float time)
        {
            var source = ResolveSource(frame.Source);
            pointBuffer[0] = source;
            var pointCount = 1;
            var hopCount = 0;
            var previous = source;
            for (var i = 0; i < trace.Events.Count && hopCount < arcHopCount && hopCount < MaxArcHops; i++)
            {
                var item = trace.Events[i];
                if (item.Type != "on_hit" || item.Detail != "arc_link" || item.Time > time + .0001f) continue;
                var target = item.Position + (source - frame.Source);
                for (var sample = 1; sample <= ArcSamplesPerHop; sample++)
                {
                    var u = sample / (float)ArcSamplesPerHop;
                    var envelope = Mathf.Sin(Mathf.PI * u);
                    var jitterPhase = (seed % 997u) * .013f + (hopCount + 1) * 2.17f + sample * 1.31f;
                    var jitter = Mathf.Sin(jitterPhase) * arcJitter * envelope;
                    var perpendicular = Vector3.Cross(Vector3.forward, target - previous).normalized;
                    if (perpendicular.sqrMagnitude <= .000001f) perpendicular = Vector3.up;
                    pointBuffer[pointCount++] = Vector3.Lerp(previous, target, u) + Vector3.down * (arcSag * 4f * u * (1f - u)) + perpendicular * jitter;
                }
                previous = target;
                hopCount++;
            }
            ArcVisibleHopCount = hopCount;
            ArcPointCount = pointCount;
            PrimaryPointCount = pointCount;
            EffectiveSource = source;
            EffectiveTarget = previous;
            if (hopCount > 0)
            {
                ConfigurePolyline(primaryLine, pointBuffer, pointCount, baseWidth, 1f, .86f);
                VisibleLineCount = 1;
                BeamLength = PolylineLength(pointBuffer, pointCount);
                TextureTileCount = BeamLength * tilingPerMeter;
            }
            ApplySourceMarker(source, true);
            ApplyEndpointMarker(previous, endpointBaseScale, 1.25f, 1f, hopCount > 0);
        }

        private void ResolveEndpoints(CapabilitySampleFrame frame, bool allowTargetProtocol, out Vector3 source, out Vector3 target)
        {
            source = ResolveSource(frame.Source);
            if (allowTargetProtocol && boundTarget != null) target = transform.InverseTransformPoint(boundTarget.position);
            else if (allowTargetProtocol && fixedEndpoints) target = fixedTarget;
            else target = frame.Target + (source - frame.Source);
        }

        private Vector3 ResolveSource(Vector3 fallback)
        {
            if (boundSource != null) return transform.InverseTransformPoint(boundSource.position);
            return fixedEndpoints ? fixedSource : fallback;
        }

        private void ApplyStraightLine(Vector3 source, Vector3 target, float width, float alpha, float brightness, bool visible)
        {
            ConfigureStraightLine(primaryLine, source, target, width, alpha, brightness, visible);
            EffectiveSource = source;
            EffectiveTarget = target;
            BeamLength = Vector3.Distance(source, target);
            TextureTileCount = BeamLength * tilingPerMeter;
            LineAlpha = visible ? Mathf.Clamp01(alpha) : 0f;
            EffectiveWidth = visible ? Mathf.Max(0f, width) : 0f;
            Brightness = visible ? Mathf.Max(0f, brightness) : 0f;
            VisibleLineCount = visible ? 1 : 0;
            PrimaryPointCount = visible ? 2 : 0;
        }

        private void ConfigureStraightLine(LineRenderer line, Vector3 source, Vector3 target, float width, float alpha, float brightness, bool visible)
        {
            if (line == null) return;
            line.positionCount = 2;
            line.SetPosition(0, source);
            line.SetPosition(1, target);
            line.widthMultiplier = Mathf.Max(0f, width);
            line.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);
            line.textureScale = new Vector2(Mathf.Max(.01f, Vector3.Distance(source, target) * tilingPerMeter), 1f);
            ApplyLineColor(line, brightness, alpha, brightness, alpha);
            ApplyLineProperties(line, brightness, alpha);
            line.enabled = visible && width > .000001f && alpha > .000001f;
        }

        private void ConfigurePolyline(LineRenderer line, Vector3[] points, int count, float width, float startBrightness, float endBrightness)
        {
            if (line == null) return;
            SetLinePoints(line, points, count);
            line.widthMultiplier = width;
            line.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, Mathf.Max(.1f, endBrightness));
            line.textureScale = new Vector2(Mathf.Max(.01f, PolylineLength(points, count) * tilingPerMeter), 1f);
            ApplyLineColor(line, startBrightness, 1f, endBrightness, Mathf.Max(.12f, endBrightness));
            ApplyLineProperties(line, Mathf.Max(startBrightness, endBrightness), 1f);
            line.enabled = count > 1;
        }

        private void ConfigureReflectLine(Vector3[] points, int pointCount, int segmentCount)
        {
            var damping = Mathf.Clamp01(1f - reflectDamping);
            for (var i = 0; i < segmentCount; i++)
            {
                var value = Mathf.Pow(damping, i);
                segmentWidths[i] = baseWidth * value;
                segmentBrightness[i] = value;
                var line = i == 0 ? primaryLine : i - 1 < auxiliaryLines.Length ? auxiliaryLines[i - 1] : null;
                if (line != null) ConfigureStraightLine(line, points[i], points[i + 1], segmentWidths[i], value, value, true);
            }
            BeamLength = PolylineLength(points, pointCount);
            TextureTileCount = BeamLength * tilingPerMeter;
            VisibleLineCount = segmentCount;
            LineAlpha = 1f;
            EffectiveWidth = baseWidth;
            Brightness = 1f;
        }

        private void ApplyReflectGradient(LineRenderer line, int segments, float damping)
        {
            var keyCount = Mathf.Clamp(segments + 1, 2, 8);
            var colors = new GradientColorKey[keyCount];
            var alphas = new GradientAlphaKey[keyCount];
            for (var i = 0; i < keyCount; i++)
            {
                var t = i / (float)(keyCount - 1);
                var segment = Mathf.RoundToInt(t * Mathf.Max(0, segments - 1));
                var value = Mathf.Pow(damping, segment);
                colors[i] = new GradientColorKey(NeutralColor(value), t);
                alphas[i] = new GradientAlphaKey(Mathf.Max(.12f, value), t);
            }
            var gradient = new Gradient();
            gradient.SetKeys(colors, alphas);
            line.colorGradient = gradient;
        }

        private void ApplyBounceMarkers(Vector3[] points, int count)
        {
            if (markerParticles == null || markerParticleRenderer == null)
            {
                BounceMarkerCount = 0;
                return;
            }
            var actual = Mathf.Min(count, MaxMarkerParticles);
            for (var i = 0; i < actual; i++)
            {
                var energy = segmentBrightness[Mathf.Min(i, Mathf.Max(0, ReflectSegmentCount - 1))];
                markerBuffer[i] = new ParticleSystem.Particle
                {
                    position = points[i + 1],
                    velocity = Vector3.zero,
                    startLifetime = 60f,
                    remainingLifetime = 60f,
                    startSize = Mathf.Max(.08f, baseWidth * 3.5f * energy),
                    startColor = NeutralColor(energy)
                };
            }
            markerParticles.SetParticles(markerBuffer, actual);
            markerParticleRenderer.enabled = executing && actual > 0;
            BounceMarkerCount = actual;
        }

        private void ApplySourceMarker(Vector3 position, bool visible)
        {
            if (sourceMarker != null)
            {
                sourceMarker.localPosition = position;
                sourceMarker.localScale = sourceBaseScale;
            }
            if (sourceRenderer != null)
            {
                sourceRenderer.enabled = visible;
                if (visible) ApplyRendererColor(sourceRenderer, 1f, 1f);
            }
        }

        private void ApplyEndpointMarker(Vector3 position, Vector3 scale, float brightness, float alpha, bool visible)
        {
            if (endpointMarker != null)
            {
                endpointMarker.localPosition = position;
                endpointMarker.localScale = scale;
            }
            if (endpointRenderer != null)
            {
                endpointRenderer.enabled = visible;
                if (visible) ApplyRendererColor(endpointRenderer, brightness, alpha);
            }
        }

        private static void ApplyRendererColor(Renderer renderer, float brightness, float alpha)
        {
            if (renderer == null) return;
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            block.SetColor("_PrimaryColor", NeutralColor(brightness));
            block.SetColor("_SecondaryColor", NeutralColor(brightness * 1.18f));
            block.SetFloat("_GlobalAlpha", Mathf.Clamp01(alpha));
            renderer.SetPropertyBlock(block);
        }

        private static void ApplyLineColor(LineRenderer line, float startBrightness, float startAlpha, float endBrightness, float endAlpha)
        {
            if (line == null) return;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(NeutralColor(startBrightness), 0f), new GradientColorKey(NeutralColor(endBrightness), 1f) },
                new[] { new GradientAlphaKey(Mathf.Clamp01(startAlpha), 0f), new GradientAlphaKey(Mathf.Clamp01(endAlpha), 1f) });
            line.colorGradient = gradient;
        }

        private static void ApplyLineProperties(LineRenderer line, float brightness, float alpha)
        {
            if (line == null) return;
            var block = new MaterialPropertyBlock();
            line.GetPropertyBlock(block);
            block.SetColor("_PrimaryColor", NeutralColor(brightness));
            block.SetColor("_SecondaryColor", NeutralColor(brightness * 1.18f));
            block.SetFloat("_Intensity", Mathf.Max(0f, brightness));
            block.SetFloat("_GlobalAlpha", Mathf.Clamp01(alpha));
            line.SetPropertyBlock(block);
        }

        private static Color NeutralColor(float brightness)
        {
            var value = Mathf.Max(0f, brightness);
            return new Color(.73f * value, .82f * value, .94f * value, 1f);
        }

        private void HideLinesAndMarkersForFrame()
        {
            HideAllLines();
            if (sourceRenderer != null) sourceRenderer.enabled = false;
            if (endpointRenderer != null) endpointRenderer.enabled = false;
            if (markerParticles != null) markerParticles.SetParticles(markerBuffer, 0);
            if (markerParticleRenderer != null) markerParticleRenderer.enabled = false;
            VisibleLineCount = 0;
            PrimaryPointCount = 0;
            BounceMarkerCount = 0;
            ReflectSegmentCount = 0;
            ConvergeLineCount = 0;
            ArcVisibleHopCount = 0;
            ArcPointCount = 0;
        }

        private void HideAllVisuals()
        {
            HideAllLines();
            if (sourceRenderer != null) sourceRenderer.enabled = false;
            if (endpointRenderer != null) endpointRenderer.enabled = false;
            if (markerParticles != null) markerParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (markerParticleRenderer != null) markerParticleRenderer.enabled = false;
            VisibleLineCount = 0;
            PrimaryPointCount = 0;
            BounceMarkerCount = 0;
        }

        private void HideAllLines()
        {
            if (primaryLine != null) primaryLine.enabled = false;
            if (auxiliaryLines == null) return;
            for (var i = 0; i < auxiliaryLines.Length; i++)
                if (auxiliaryLines[i] != null) auxiliaryLines[i].enabled = false;
        }

        private void ResetReadbacks()
        {
            EffectiveSource = Vector3.zero;
            EffectiveTarget = Vector3.zero;
            BeamLength = 0f;
            TextureTileCount = 0f;
            LineAlpha = 0f;
            EffectiveWidth = 0f;
            Brightness = 0f;
            HitscanFade = 0f;
            VisibleLineCount = 0;
            PrimaryPointCount = 0;
            ChargeTier = 0;
            SweepAngle = 0f;
            SweepAngularVelocity = 0f;
            ReflectSegmentCount = 0;
            BounceMarkerCount = 0;
            ObstacleBlocked = false;
            OcclusionFailClosed = false;
            LastObstacleResponseFrames = 0;
            ConvergeLineCount = 0;
            FocusScale = 0f;
            ArcVisibleHopCount = 0;
            ArcPointCount = 0;
        }

        private void CaptureDefaults()
        {
            if (defaultsCaptured) return;
            if (sourceMarker != null) sourceBaseScale = sourceMarker.localScale;
            if (endpointMarker != null) endpointBaseScale = endpointMarker.localScale;
            if (markerParticleRenderer == null && markerParticles != null) markerParticleRenderer = markerParticles.GetComponent<ParticleSystemRenderer>();
            defaultsCaptured = true;
        }

        private static float PolylineLength(Vector3[] points, int count)
        {
            var length = 0f;
            for (var i = 1; i < count; i++) length += Vector3.Distance(points[i - 1], points[i]);
            return length;
        }

        private static void SetLinePoints(LineRenderer line, Vector3[] points, int count)
        {
            line.positionCount = count;
            for (var i = 0; i < count; i++) line.SetPosition(i, points[i]);
        }
    }
}

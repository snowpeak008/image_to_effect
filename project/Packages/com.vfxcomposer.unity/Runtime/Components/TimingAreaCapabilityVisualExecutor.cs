using System;
using UnityEngine;
using VFXComposer.Capabilities;

namespace VFXComposer
{
    /// <summary>
    /// Bounded visual execution layer for the ten W-C3 timing/area capabilities. The pure
    /// sampler remains authoritative for time and topology; this component turns that trace
    /// into real, pooled renderer and particle carriers with explicit runtime readback.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TimingAreaCapabilityVisualExecutor : MonoBehaviour
    {
        public const int MaxParticleCapacity = 32;
        public const int MaxSequencePoints = 8;
        public const int MaxExternalPathPoints = 8;

        [SerializeField] private string visualMode = "telegraph";
        [SerializeField] private string telegraphShape = "circle";
        [SerializeField] private string fillStyle = "center_fill";
        [SerializeField] private string configuredSlotId = string.Empty;
        [SerializeField] private bool slotBindingResolved;
        [SerializeField] private Transform core;
        [SerializeField] private Renderer coreRenderer;
        [SerializeField] private Transform boundary;
        [SerializeField] private Renderer boundaryRenderer;
        [SerializeField] private Transform eventMarker;
        [SerializeField] private Renderer eventRenderer;
        [SerializeField] private LineRenderer detailLine;
        [SerializeField] private ParticleSystem slotParticles;
        [SerializeField] private ParticleSystemRenderer slotParticleRenderer;
        [SerializeField, Min(.01f)] private float maxRadius = 4f;
        [SerializeField, Min(.001f)] private float edgeThickness = .2f;
        [SerializeField, Min(.01f)] private float startRadius = 4f;
        [SerializeField, Range(2, 3)] private int growthStageCount = 3;
        [SerializeField, Min(.01f)] private float growthBaseRadius = 1f;

        private readonly ParticleSystem.Particle[] particleBuffer = new ParticleSystem.Particle[MaxParticleCapacity];
        private readonly Vector3[] sequencePositions = new Vector3[MaxSequencePoints];
        private readonly Vector3[] externalPath = new Vector3[MaxExternalPathPoints];
        private readonly Vector3[] linePoints = new Vector3[33];
        private CapabilitySampleTrace trace;
        private float duration = 1f;
        private bool executing;
        private int particleCount;
        private int externalPathCount;
        private Vector3 coreBaseScale = Vector3.one;
        private Vector3 eventBaseScale = Vector3.one;
        private bool defaultsCaptured;
        private string exitVisual = string.Empty;
        private float exitAge;
        private int releaseTier;
        private Vector3 exitCenter;

        public string VisualMode { get { return visualMode; } }
        public int ParticleCapacity { get { return MaxParticleCapacity; } }
        public string TelegraphShape { get { return telegraphShape; } }
        public string FillStyle { get { return fillStyle; } }
        public string ConfiguredSlotId { get { return configuredSlotId; } }
        public bool SlotBindingResolved { get { return slotBindingResolved; } }
        public bool IsExecuting { get { return executing; } }
        public int VisibleSlotCarrierCount { get { return particleCount; } }
        public int VisualSlotExecutionCount { get; private set; }
        public int TickExecutionCount { get; private set; }
        public int SequenceExecutionCount { get; private set; }
        public int UniqueSequencePositionCount { get; private set; }
        public float Progress { get; private set; }
        public float TelegraphFill { get; private set; }
        public bool ImpactSlotVisible { get; private set; }
        public float FuseBlinkFrequency { get; private set; }
        public bool FuseBlinkOn { get; private set; }
        public int ChargeTier { get; private set; }
        public int VisualDensity { get; private set; }
        public bool FullChargePromptVisible { get; private set; }
        public string ExitVisual { get { return exitVisual; } }
        public float ExitNormalizedAge { get { return Mathf.Clamp01(exitAge / .32f); } }
        public int ReleaseTier { get { return releaseTier; } }
        public float BoundaryRadius { get; private set; }
        public bool EdgeHitLayerVisible { get; private set; }
        public bool BreathHoldVisible { get; private set; }
        public bool ImplodeBurstVisible { get; private set; }
        public bool UsesExternalPath { get { return externalPathCount >= 2; } }
        public Vector3 ZoneCenter { get; private set; }
        public int ResidueCount { get; private set; }
        public int GrowthStage { get; private set; }
        public bool UpgradePulseVisible { get; private set; }
        public int LastSlotSequence { get; private set; }
        public bool AllVisualsHidden
        {
            get
            {
                if (coreRenderer != null && coreRenderer.enabled) return false;
                if (boundaryRenderer != null && boundaryRenderer.enabled) return false;
                if (eventRenderer != null && eventRenderer.enabled) return false;
                if (detailLine != null && detailLine.enabled) return false;
                return slotParticles == null || slotParticles.particleCount == 0;
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

        public void Begin(CapabilitySampleTrace sampledTrace, float sampledDuration)
        {
            CaptureDefaults();
            trace = sampledTrace;
            duration = Mathf.Max(1f / 60f, sampledDuration);
            executing = trace != null;
            exitVisual = string.Empty;
            exitAge = 0f;
            releaseTier = 0;
            ResetReadbacks();
            HideAllVisuals();
            if (slotParticles != null)
            {
                slotParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                slotParticles.Play(true);
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
            ResetFrameReadbacks();
            HideFrameVisuals();

            if (visualMode == "telegraph") ApplyTelegraph(frame, time);
            else if (visualMode == "delay_fuse") ApplyDelayFuse(frame, time);
            else if (visualMode == "tick_pulse") ApplyTickPulse(frame, time);
            else if (visualMode == "charge_release") ApplyCharge(frame, time);
            else if (visualMode == "channel_interrupt") ApplyChannel(frame, time);
            else if (visualMode == "chain_sequence") ApplyChainSequence(frame, time);
            else if (visualMode == "expand_ring") ApplyExpand(frame, time);
            else if (visualMode == "implode") ApplyImplode(frame, time);
            else if (visualMode == "moving_zone") ApplyMovingZone(frame, time);
            else if (visualMode == "growth_stage") ApplyGrowth(frame, time);
            CommitParticles();
        }

        public void Stop(string exit, VfxStopMode mode)
        {
            executing = false;
            exitCenter = ZoneCenter;
            if (visualMode != "moving_zone") exitCenter = core == null ? Vector3.zero : core.localPosition;
            HideAllVisuals();
            if (mode != VfxStopMode.AllowTail || (exit != "complete" && exit != "cancel"))
            {
                exitVisual = string.Empty;
                exitAge = 0f;
                return;
            }

            if (visualMode == "charge_release")
            {
                releaseTier = Mathf.Clamp(ChargeTier, 1, 3);
                exitVisual = exit == "complete" ? "charge_release_tier_" + releaseTier : "charge_cancel";
            }
            else if (visualMode == "channel_interrupt") exitVisual = exit == "complete" ? "channel_converge" : "channel_scatter";
            else if (visualMode == "moving_zone") exitVisual = exit == "complete" ? "zone_complete" : "zone_cancel";
            else
            {
                exitVisual = string.Empty;
                exitAge = 0f;
                return;
            }
            EvaluateExitAtTime(0f);
        }

        public void EvaluateExitAtTime(float age)
        {
            if (string.IsNullOrEmpty(exitVisual)) return;
            exitAge = Mathf.Max(0f, age);
            var normalized = Mathf.Clamp01(exitAge / .32f);
            if (normalized >= 1f)
            {
                exitVisual = string.Empty;
                HideAllVisuals();
                return;
            }

            HideFrameVisuals();
            particleCount = 0;
            var alpha = 1f - normalized;
            if (exitVisual.StartsWith("charge_release_tier_", StringComparison.Ordinal))
            {
                var count = 4 + releaseTier * 4;
                AddRadialBurst(exitCenter, Mathf.Lerp(.12f, .9f + releaseTier * .12f, normalized), count, .13f + releaseTier * .02f, 1.2f + releaseTier * .35f, alpha);
                ShowEvent(exitCenter, eventBaseScale * Mathf.Lerp(.7f + releaseTier * .25f, 2.2f, normalized), 1.4f + releaseTier * .4f, alpha);
            }
            else if (exitVisual == "charge_cancel")
            {
                AddRadialBurst(exitCenter, Mathf.Lerp(.55f, .04f, normalized), 6, .11f, .55f, alpha);
                ShowEvent(exitCenter, eventBaseScale * Mathf.Lerp(.9f, .12f, normalized), .55f, alpha);
            }
            else if (exitVisual == "channel_converge")
            {
                SetBoundary(exitCenter, Mathf.Lerp(.9f, .04f, normalized), 1.5f, alpha);
                AddRadialBurst(exitCenter, Mathf.Lerp(.75f, .02f, normalized), 8, .1f, 1.25f, alpha);
                ShowEvent(exitCenter, eventBaseScale * Mathf.Lerp(.55f, 1.35f, normalized), 1.5f, alpha);
            }
            else if (exitVisual == "channel_scatter")
            {
                AddRadialBurst(exitCenter, Mathf.Lerp(.1f, 1.1f, normalized), 12, .1f, .85f, alpha);
                ShowEvent(exitCenter, eventBaseScale * Mathf.Lerp(.75f, .08f, normalized), .65f, alpha);
            }
            else if (exitVisual == "zone_complete")
            {
                SetBoundary(exitCenter, Mathf.Lerp(1f, 1.5f, normalized), 1.25f, alpha);
                AddRadialBurst(exitCenter, Mathf.Lerp(.65f, 1.25f, normalized), 10, .1f, 1.1f, alpha);
            }
            else if (exitVisual == "zone_cancel")
            {
                SetBoundary(exitCenter, Mathf.Lerp(1f, .05f, normalized), .55f, alpha);
                AddRadialBurst(exitCenter, Mathf.Lerp(.75f, .08f, normalized), 6, .1f, .55f, alpha);
            }
            CommitParticles();
        }

        public void ResetVisuals()
        {
            CaptureDefaults();
            executing = false;
            trace = null;
            exitVisual = string.Empty;
            exitAge = 0f;
            releaseTier = 0;
            externalPathCount = 0;
            ResetReadbacks();
            HideAllVisuals();
            if (core != null) { core.localPosition = Vector3.zero; core.localScale = coreBaseScale; }
            if (boundary != null) { boundary.localPosition = Vector3.zero; boundary.localScale = Vector3.zero; }
            if (eventMarker != null) { eventMarker.localPosition = Vector3.zero; eventMarker.localScale = eventBaseScale; }
        }

        /// <summary>Supplies a bounded local-space path from gameplay/preview code; no scene lookup is performed.</summary>
        public void SetExternalPath(Vector3[] localPoints)
        {
            externalPathCount = Mathf.Min(localPoints == null ? 0 : localPoints.Length, MaxExternalPathPoints);
            for (var i = 0; i < externalPathCount; i++) externalPath[i] = localPoints[i];
        }

        public void ClearExternalPath() { externalPathCount = 0; }

        public void ApplyExternalInputs(CapabilitySampleRequest request)
        {
            if (request == null) throw new ArgumentNullException("request");
            if (externalPathCount < 2) return;
            request.ExternalPath = new Vector3[externalPathCount];
            for (var i = 0; i < externalPathCount; i++) request.ExternalPath[i] = externalPath[i];
        }

        public Vector3 GetSequencePosition(int index)
        {
            if (index < 0 || index >= UniqueSequencePositionCount) throw new ArgumentOutOfRangeException("index");
            return sequencePositions[index];
        }

        public Vector3 GetVisibleSlotPosition(int index)
        {
            if (index < 0 || index >= particleCount) throw new ArgumentOutOfRangeException("index");
            return particleBuffer[index].position;
        }

        private void ApplyTelegraph(CapabilitySampleFrame frame, float time)
        {
            Progress = Mathf.Clamp01(frame.Progress);
            TelegraphFill = Progress;
            var edgeCollapse = fillStyle == "edge_collapse";
            SetBoundary(frame.Position, edgeCollapse ? Mathf.Lerp(1f, .18f, Progress) : 1f, 1f + Progress * .6f, 1f);
            ShowCore(frame.Position, coreBaseScale * (edgeCollapse ? .2f : Mathf.Lerp(.18f, .72f, Progress)), .7f + Progress, edgeCollapse ? .65f : .4f + Progress * .6f);
            ConfigureProgressShape(frame.Position, Progress, telegraphShape);
            CapabilitySampleEvent release;
            if (TryLatestEvent("on_release", "telegraph_complete", time, out release))
            {
                VisualSlotExecutionCount = 1;
                LastSlotSequence = 1;
                var age = Mathf.Max(0f, time - release.Time);
                if (age <= .24f)
                {
                    ImpactSlotVisible = true;
                    AddRadialBurst(release.Position, Mathf.Lerp(.08f, .72f, age / .24f), 8, .13f, 1.7f, 1f - age / .24f);
                    ShowEvent(release.Position, eventBaseScale * Mathf.Lerp(.75f, 1.8f, age / .24f), 1.8f, 1f - age / .24f);
                }
            }
        }

        private void ApplyDelayFuse(CapabilitySampleFrame frame, float time)
        {
            Progress = Mathf.Clamp01(frame.Progress);
            var sampledFuseTime = GetEventTime("on_release", "fuse_complete", duration);
            var normalizedTime = Mathf.Min(time, sampledFuseTime);
            FuseBlinkFrequency = Mathf.Lerp(2f, 12f, Progress * Progress);
            var integratedCycles = 2f * normalizedTime + (10f / 3f) * normalizedTime * normalizedTime * normalizedTime / Mathf.Max(.0001f, sampledFuseTime * sampledFuseTime);
            FuseBlinkOn = Mathf.Repeat(integratedCycles, 1f) < .56f;
            SetBoundary(frame.Position, Mathf.Lerp(.72f, 1f, Progress), .75f + Progress, .65f + Progress * .35f);
            ShowCore(frame.Position, coreBaseScale * Mathf.Lerp(.34f, .72f, Progress), 1f + Progress * 1.1f, FuseBlinkOn ? 1f : .12f);
            CapabilitySampleEvent release;
            if (TryLatestEvent("on_release", "fuse_complete", time, out release))
            {
                VisualSlotExecutionCount = 1;
                LastSlotSequence = 1;
                var age = Mathf.Max(0f, time - release.Time);
                if (age <= .25f)
                {
                    ImpactSlotVisible = true;
                    AddRadialBurst(release.Position, Mathf.Lerp(.08f, .9f, age / .25f), 10, .14f, 1.9f, 1f - age / .25f);
                    ShowEvent(release.Position, eventBaseScale * Mathf.Lerp(.8f, 2f, age / .25f), 2f, 1f - age / .25f);
                }
            }
        }

        private void ApplyTickPulse(CapabilitySampleFrame frame, float time)
        {
            Progress = Mathf.Clamp01(frame.Progress);
            SetBoundary(frame.Position, 1f + Mathf.Sin(Progress * Mathf.PI) * .18f, 1.1f, 1f);
            ShowCore(frame.Position, coreBaseScale * .22f, .65f, .65f);
            for (var i = 0; i < trace.Events.Count; i++)
            {
                var item = trace.Events[i];
                if (item.Type != "on_tick" || item.Time > time + .0001f) continue;
                TickExecutionCount++;
                VisualSlotExecutionCount++;
                LastSlotSequence = item.Sequence;
                var age = time - item.Time;
                if (age <= .22f)
                {
                    var normalized = Mathf.Clamp01(age / .22f);
                    AddRadialBurst(item.Position, Mathf.Lerp(.08f, .85f, normalized), 6, .105f, 1.45f, 1f - normalized);
                }
            }
        }

        private void ApplyCharge(CapabilitySampleFrame frame, float time)
        {
            Progress = Mathf.Clamp01(frame.Progress);
            ChargeTier = Mathf.Clamp(frame.Stage + 1, 1, 3);
            VisualDensity = ChargeTier * 4;
            var scale = Mathf.Max(1f, frame.Width);
            ShowCore(frame.Position, coreBaseScale * (.34f * scale), 1f + ChargeTier * .45f, 1f);
            SetBoundary(frame.Position, .55f + ChargeTier * .22f + Mathf.Sin(time * (4f + ChargeTier)) * .04f, .8f + ChargeTier * .45f, 1f);
            FullChargePromptVisible = ChargeTier == 3;
            ConfigureProgressShape(frame.Position, ChargeTier == 3 ? Mathf.Repeat(time * 1.7f, 1f) : Progress, "circle");
            for (var i = 0; i < VisualDensity; i++)
            {
                var angle = Mathf.PI * 2f * i / VisualDensity + time * (ChargeTier % 2 == 0 ? -1.5f : 1.5f);
                var radius = .35f + ChargeTier * .12f;
                AddParticle(frame.Position + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius, .07f + ChargeTier * .018f, 1f + ChargeTier * .35f, 1f);
            }
        }

        private void ApplyChannel(CapabilitySampleFrame frame, float time)
        {
            Progress = Mathf.Clamp01(frame.Progress);
            CapabilitySampleEvent complete;
            var completed = TryLatestEvent("on_complete", "channel_complete", time, out complete);
            var settle = completed ? Mathf.Clamp01((time - complete.Time) / Mathf.Max(.1f, duration - complete.Time)) : 0f;
            SetBoundary(frame.Position, completed ? Mathf.Lerp(.82f, .08f, settle) : .82f + Mathf.Sin(time * 5f) * .05f, 1f + Progress * .55f, 1f);
            ShowCore(frame.Position, coreBaseScale * (completed ? Mathf.Lerp(.62f, .95f, settle) : Mathf.Lerp(.28f, .62f, Progress)), .8f + Progress, 1f);
            ConfigureProgressShape(frame.Position, Progress, "circle");
        }

        private void ApplyChainSequence(CapabilitySampleFrame frame, float time)
        {
            Progress = Mathf.Clamp01(frame.Progress);
            SetBoundary(frame.Position, .48f, .6f, .55f);
            ShowCore(frame.Position, coreBaseScale * .2f, .65f, .7f);
            for (var i = 0; i < trace.Events.Count && UniqueSequencePositionCount < MaxSequencePoints; i++)
            {
                var item = trace.Events[i];
                if (item.Type != "on_hit" || item.Detail != "chain_sequence" || item.Time > time + .0001f) continue;
                sequencePositions[UniqueSequencePositionCount++] = item.Position;
                SequenceExecutionCount++;
                VisualSlotExecutionCount++;
                LastSlotSequence = item.Sequence;
                var age = time - item.Time;
                if (age <= .34f)
                {
                    var normalized = Mathf.Clamp01(age / .34f);
                    AddRadialBurst(item.Position, Mathf.Lerp(.06f, .4f, normalized), 4, .09f, 1.25f, 1f - normalized);
                }
            }
            if (UniqueSequencePositionCount > 1) ConfigureSequenceLine(UniqueSequencePositionCount);
        }

        private void ApplyExpand(CapabilitySampleFrame frame, float time)
        {
            Progress = Mathf.Clamp01(frame.Progress);
            BoundaryRadius = Mathf.Clamp(frame.Radius, 0f, maxRadius);
            SetBoundary(frame.Position, BoundaryRadius, 1.15f, 1f);
            ShowCore(frame.Position, coreBaseScale * .16f, .65f, .7f);
            EdgeHitLayerVisible = BoundaryRadius > .02f;
            if (EdgeHitLayerVisible)
            {
                var count = 12;
                for (var i = 0; i < count; i++)
                {
                    var angle = Mathf.PI * 2f * i / count;
                    AddParticle(frame.Position + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * BoundaryRadius, Mathf.Max(.07f, edgeThickness), 1.3f, .92f);
                }
            }
            CapabilitySampleEvent edge;
            if (TryLatestEvent("on_hit", "expanding_edge", time, out edge)) { VisualSlotExecutionCount = 1; LastSlotSequence = edge.Sequence; }
        }

        private void ApplyImplode(CapabilitySampleFrame frame, float time)
        {
            Progress = Mathf.Clamp01(frame.Progress);
            BoundaryRadius = Mathf.Clamp(frame.Radius, 0f, startRadius);
            BreathHoldVisible = frame.Stage == 1;
            if (BoundaryRadius > .001f) SetBoundary(frame.Position, BoundaryRadius, 1.2f + Progress * .5f, 1f);
            ShowCore(frame.Position, coreBaseScale * (BreathHoldVisible ? .12f : .2f), BreathHoldVisible ? 1.7f : .7f, BreathHoldVisible ? 1f : .65f);
            CapabilitySampleEvent burst;
            if (TryLatestEvent("on_release", "implode_burst", time, out burst))
            {
                VisualSlotExecutionCount = 1;
                LastSlotSequence = 1;
                var age = Mathf.Max(0f, time - burst.Time);
                if (age <= .25f)
                {
                    ImplodeBurstVisible = true;
                    AddRadialBurst(burst.Position, Mathf.Lerp(.02f, .78f, age / .25f), 12, .12f, 1.9f, 1f - age / .25f);
                    ShowEvent(burst.Position, eventBaseScale * Mathf.Lerp(.45f, 1.8f, age / .25f), 2f, 1f - age / .25f);
                }
            }
        }

        private void ApplyMovingZone(CapabilitySampleFrame frame, float time)
        {
            Progress = Mathf.Clamp01(frame.Progress);
            ZoneCenter = frame.Position;
            SetBoundary(ZoneCenter, 1f + Mathf.Sin(time * 3.2f) * .035f, 1f, 1f);
            ShowCore(ZoneCenter, coreBaseScale * .18f, .65f, .65f);
            ConfigureExternalPathLine();
            var samples = UsesExternalPath ? Mathf.Clamp(Mathf.FloorToInt(Progress * 9f), 1, 8) : 1;
            for (var i = 0; i < samples; i++)
            {
                var sampleTime = Mathf.Max(0f, time - (i + 1) * duration * .075f);
                var position = TracePositionAt(sampleTime);
                AddParticle(position, .1f + .012f * (samples - i), .7f, .8f - i * .07f);
                ResidueCount++;
            }
            VisualSlotExecutionCount = ResidueCount;
            LastSlotSequence = ResidueCount;
        }

        private void ApplyGrowth(CapabilitySampleFrame frame, float time)
        {
            Progress = Mathf.Clamp01(frame.Progress);
            GrowthStage = Mathf.Clamp(frame.Stage + 1, 1, growthStageCount);
            BoundaryRadius = Mathf.Max(growthBaseRadius, frame.Radius);
            SetBoundary(frame.Position, BoundaryRadius, .85f + GrowthStage * .35f, 1f);
            ShowCore(frame.Position, coreBaseScale * (.15f + GrowthStage * .05f), .7f + GrowthStage * .25f, .8f);
            VisualDensity = GrowthStage == 1 ? 6 : GrowthStage == 2 ? 10 : 14;
            for (var i = 0; i < VisualDensity; i++)
            {
                var radial = Mathf.Sqrt((i + .5f) / VisualDensity) * BoundaryRadius * .72f;
                var angle = i * 2.39996323f + GrowthStage * .31f;
                AddParticle(frame.Position + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radial, .065f + GrowthStage * .012f, .75f + GrowthStage * .25f, .9f);
            }
            CapabilitySampleEvent latest = null;
            for (var i = 0; i < trace.Events.Count; i++)
            {
                var item = trace.Events[i];
                if (item.Type == "on_stage" && item.Detail == "growth_stage" && item.Time <= time + .0001f) latest = item;
            }
            if (latest != null && time - latest.Time <= .22f)
            {
                UpgradePulseVisible = true;
                var normalized = Mathf.Clamp01((time - latest.Time) / .22f);
                ShowEvent(frame.Position, eventBaseScale * Mathf.Lerp(.55f, 1.45f, normalized), 1.5f, 1f - normalized);
            }
        }

        private bool TryLatestEvent(string type, string detail, float time, out CapabilitySampleEvent result)
        {
            result = null;
            if (trace == null) return false;
            for (var i = 0; i < trace.Events.Count; i++)
            {
                var item = trace.Events[i];
                if (item.Type == type && item.Detail == detail && item.Time <= time + .0001f) result = item;
            }
            return result != null;
        }

        private float GetEventTime(string type, string detail, float fallback)
        {
            if (trace == null) return fallback;
            for (var i = 0; i < trace.Events.Count; i++)
            {
                var item = trace.Events[i];
                if (item.Type == type && item.Detail == detail) return item.Time;
            }
            return fallback;
        }

        private void ConfigureProgressShape(Vector3 center, float progress, string shape)
        {
            if (detailLine == null) return;
            var count = Mathf.Clamp(Mathf.CeilToInt(Mathf.Clamp01(progress) * 32f) + 1, 2, 33);
            if (shape == "rectangle")
            {
                for (var i = 0; i < count; i++) linePoints[i] = center + RectanglePoint(i / 32f);
            }
            else
            {
                var fan = shape == "fan";
                var start = fan ? -45f : -90f;
                var sweep = fan ? 90f : 360f;
                for (var i = 0; i < count; i++)
                {
                    var angle = (start + sweep * i / 32f) * Mathf.Deg2Rad;
                    linePoints[i] = center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
                }
            }
            SetLinePoints(count, .045f, 1.4f, 1f);
        }

        private void ConfigureSequenceLine(int count)
        {
            if (detailLine == null) return;
            var actual = Mathf.Min(count, linePoints.Length);
            for (var i = 0; i < actual; i++) linePoints[i] = sequencePositions[i];
            SetLinePoints(actual, .035f, .8f, .72f);
        }

        private void ConfigureExternalPathLine()
        {
            if (detailLine == null || !UsesExternalPath) return;
            var count = Mathf.Min(externalPathCount, linePoints.Length);
            for (var i = 0; i < count; i++) linePoints[i] = externalPath[i];
            SetLinePoints(count, .025f, .45f, .45f);
        }

        private void SetLinePoints(int count, float width, float brightness, float alpha)
        {
            detailLine.positionCount = count;
            for (var i = 0; i < count; i++) detailLine.SetPosition(i, linePoints[i]);
            detailLine.widthMultiplier = width;
            detailLine.enabled = count >= 2;
            ApplyRenderer(detailLine, brightness, alpha);
        }

        private static Vector3 RectanglePoint(float perimeter)
        {
            var value = Mathf.Repeat(perimeter, 1f) * 4f;
            if (value < 1f) return new Vector3(Mathf.Lerp(-1f, 1f, value), 1f, 0f);
            if (value < 2f) return new Vector3(1f, Mathf.Lerp(1f, -1f, value - 1f), 0f);
            if (value < 3f) return new Vector3(Mathf.Lerp(1f, -1f, value - 2f), -1f, 0f);
            return new Vector3(-1f, Mathf.Lerp(-1f, 1f, value - 3f), 0f);
        }

        private Vector3 TracePositionAt(float time)
        {
            if (trace == null || trace.Frames.Count == 0) return Vector3.zero;
            var index = Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(time / duration) * (trace.Frames.Count - 1)), 0, trace.Frames.Count - 1);
            return trace.Frames[index].Position;
        }

        private void SetBoundary(Vector3 position, float radius, float brightness, float alpha)
        {
            BoundaryRadius = Mathf.Max(0f, radius);
            if (boundary != null)
            {
                boundary.localPosition = position;
                boundary.localScale = Vector3.one * BoundaryRadius;
            }
            if (boundaryRenderer != null)
            {
                boundaryRenderer.enabled = BoundaryRadius > .001f && alpha > .001f;
                if (boundaryRenderer.enabled) ApplyRenderer(boundaryRenderer, brightness, alpha);
            }
        }

        private void ShowCore(Vector3 position, Vector3 scale, float brightness, float alpha)
        {
            if (core != null) { core.localPosition = position; core.localScale = scale; }
            if (coreRenderer != null)
            {
                coreRenderer.enabled = alpha > .001f;
                if (coreRenderer.enabled) ApplyRenderer(coreRenderer, brightness, alpha);
            }
        }

        private void ShowEvent(Vector3 position, Vector3 scale, float brightness, float alpha)
        {
            if (eventMarker != null) { eventMarker.localPosition = position; eventMarker.localScale = scale; }
            if (eventRenderer != null)
            {
                eventRenderer.enabled = alpha > .001f;
                if (eventRenderer.enabled) ApplyRenderer(eventRenderer, brightness, alpha);
            }
        }

        private void AddRadialBurst(Vector3 center, float radius, int count, float size, float brightness, float alpha)
        {
            var available = MaxParticleCapacity - particleCount;
            if (available <= 0) return;
            var actual = Mathf.Clamp(count, 1, available);
            for (var i = 0; i < actual; i++)
            {
                var angle = Mathf.PI * 2f * i / actual;
                AddParticle(center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius, size, brightness, alpha);
            }
        }

        private void AddParticle(Vector3 position, float size, float brightness, float alpha)
        {
            if (particleCount >= MaxParticleCapacity) return;
            particleBuffer[particleCount++] = new ParticleSystem.Particle
            {
                position = position,
                velocity = Vector3.zero,
                startLifetime = 60f,
                remainingLifetime = 60f,
                startSize = Mathf.Max(.001f, size),
                startColor = NeutralColor(brightness, alpha)
            };
        }

        private void CommitParticles()
        {
            if (slotParticles != null) slotParticles.SetParticles(particleBuffer, particleCount);
            if (slotParticleRenderer != null) slotParticleRenderer.enabled = particleCount > 0 && (executing || !string.IsNullOrEmpty(exitVisual));
        }

        private void HideFrameVisuals()
        {
            if (coreRenderer != null) coreRenderer.enabled = false;
            if (boundaryRenderer != null) boundaryRenderer.enabled = false;
            if (eventRenderer != null) eventRenderer.enabled = false;
            if (detailLine != null) detailLine.enabled = false;
            particleCount = 0;
            if (slotParticles != null) slotParticles.SetParticles(particleBuffer, 0);
            if (slotParticleRenderer != null) slotParticleRenderer.enabled = false;
        }

        private void HideAllVisuals()
        {
            HideFrameVisuals();
            if (slotParticles != null) slotParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private void ResetFrameReadbacks()
        {
            particleCount = 0;
            VisualSlotExecutionCount = 0;
            TickExecutionCount = 0;
            SequenceExecutionCount = 0;
            UniqueSequencePositionCount = 0;
            Progress = 0f;
            TelegraphFill = 0f;
            ImpactSlotVisible = false;
            FuseBlinkFrequency = 0f;
            FuseBlinkOn = false;
            ChargeTier = 0;
            VisualDensity = 0;
            FullChargePromptVisible = false;
            BoundaryRadius = 0f;
            EdgeHitLayerVisible = false;
            BreathHoldVisible = false;
            ImplodeBurstVisible = false;
            ZoneCenter = Vector3.zero;
            ResidueCount = 0;
            GrowthStage = 0;
            UpgradePulseVisible = false;
            LastSlotSequence = 0;
        }

        private void ResetReadbacks() { ResetFrameReadbacks(); }

        private void CaptureDefaults()
        {
            if (defaultsCaptured) return;
            if (core != null) coreBaseScale = core.localScale;
            if (eventMarker != null) eventBaseScale = eventMarker.localScale;
            if (slotParticleRenderer == null && slotParticles != null) slotParticleRenderer = slotParticles.GetComponent<ParticleSystemRenderer>();
            defaultsCaptured = true;
        }

        private static void ApplyRenderer(Renderer renderer, float brightness, float alpha)
        {
            if (renderer == null) return;
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            block.SetColor("_PrimaryColor", NeutralColor(brightness, 1f));
            block.SetColor("_SecondaryColor", NeutralColor(brightness * 1.18f, 1f));
            block.SetFloat("_Intensity", Mathf.Max(0f, brightness));
            block.SetFloat("_GlobalAlpha", Mathf.Clamp01(alpha));
            renderer.SetPropertyBlock(block);
        }

        private static Color NeutralColor(float brightness, float alpha)
        {
            var value = Mathf.Max(0f, brightness);
            return new Color(.73f * value, .82f * value, .94f * value, Mathf.Clamp01(alpha));
        }
    }
}

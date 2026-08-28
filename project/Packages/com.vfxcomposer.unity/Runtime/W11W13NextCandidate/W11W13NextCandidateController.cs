using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VFXComposer.W11W13NextCandidate
{
    public enum W11W13NextFamily { Environment, HitFeedback, Ultimate }

    public enum W11W13NextVariant
    {
        Rain, Sandstorm, MistFog, FallingLeaves, Fireflies, AmbientDust, Waterfall,
        HitFlash, CriticalStrike, ParrySpark, KnockupLauncher, ComboSurge, ElementalReaction, LifestealLink,
        DragonBreath, MeteorShower, FrozenDomain, JudgementRay, DemonGate, BladeTempest
    }

    public enum W11W13NextRuntimeStage { Idle, Intro, Primary, Release, Tail, WaitingGate }

    [Serializable]
    public struct W11W13TimelineCue
    {
        public float Time;
        public int SourceIndex;
        public bool Play;
        public Vector3 LocalPosition;
        public Vector3 LocalEuler;
        public float Scale;
        public string EventId;
    }

    [Serializable]
    public struct W11W13CameraHint
    {
        public float Time;
        public string Type;
        public float Strength;
    }

    [Serializable]
    public struct W11W13StageGate
    {
        public float Time;
        public string EventId;
    }

    public struct W11W13RuntimeSnapshot
    {
        public string CandidateId;
        public W11W13NextFamily Family;
        public W11W13NextVariant Variant;
        public W11W13NextRuntimeStage Stage;
        public float Elapsed;
        public float CurrentIntensity;
        public float TargetIntensity;
        public int StackLevel;
        public int ActiveRendererCount;
        public int ParticleCapacity;
        public int TriggeredCueCount;
        public string WaitingGateId;
    }

    /// <summary>
    /// Player-safe entry for the isolated W11/W12/W13 next-candidate line.  The component owns
    /// real render carriers and runtime protocols only.  It never emits a visual verdict or an L level.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class W11W13NextCandidateController : MonoBehaviour, IVfxRuntimeEntry
    {
        [SerializeField] private string candidateId = string.Empty;
        [SerializeField] private W11W13NextFamily family = W11W13NextFamily.Environment;
        [SerializeField] private W11W13NextVariant variant = W11W13NextVariant.Rain;
        [SerializeField, Min(.05f)] private float duration = 1f;
        [SerializeField] private uint seed = 1;
        [SerializeField] private Color primary = Color.white;
        [SerializeField] private Color secondary = Color.cyan;
        [SerializeField] private Color accent = Color.white;
        [SerializeField] private Renderer[] ownedRenderers = new Renderer[0];
        [SerializeField] private ParticleSystem[] particles = new ParticleSystem[0];
        [SerializeField] private LineRenderer[] lines = new LineRenderer[0];
        [SerializeField] private Transform[] animatedTransforms = new Transform[0];
        [SerializeField] private Transform primaryBody = null;
        [SerializeField] private Transform secondaryBody = null;
        [SerializeField] private Transform resultBody = null;
        [SerializeField] private Transform[] flowMotes = new Transform[0];
        [SerializeField] private GameObject[] stageRoots = new GameObject[0];

        [Header("Composite-only source references")]
        [SerializeField] private GameObject[] sourcePrefabs = new GameObject[0];
        [SerializeField] private W11W13TimelineCue[] timeline = new W11W13TimelineCue[0];
        [SerializeField] private W11W13CameraHint[] cameraHints = new W11W13CameraHint[0];
        [SerializeField] private W11W13StageGate[] gates = new W11W13StageGate[0];

        private Vector3[] basePositions = new Vector3[0];
        private Quaternion[] baseRotations = new Quaternion[0];
        private Vector3[] baseScales = new Vector3[0];
        private float[] baseEmissionRates = new float[0];
        private Renderer[] externalRenderers = new Renderer[0];
        private MaterialPropertyBlock[] externalBlocks = new MaterialPropertyBlock[0];
        private MaterialPropertyBlock propertyBlock;
        private Transform cameraFollowTarget;
        private bool cameraFollow;
        private Vector3 wind;
        private Vector3 sourcePoint;
        private Vector3 targetPoint = Vector3.right * 2f;
        private Color reactionA = new Color(1f, .25f, .03f);
        private Color reactionB = new Color(.1f, .55f, 1f);
        private Color reactionResult = new Color(.75f, .95f, 1f);
        private Vector3 layerDensities = Vector3.one;
        private float targetIntensity = 1f;
        private float currentIntensity = 1f;
        private int stackLevel = 1;
        private float elapsed;
        private bool playing;
        private bool allowingTail;
        private float tailRemaining;
        private int playCount;

        private GameObject[] sourceInstances = new GameObject[0];
        private IVfxRuntimeEntry[] sourceEntries = new IVfxRuntimeEntry[0];
        private bool[] cueConsumed = new bool[0];
        private bool[] hintConsumed = new bool[0];
        private bool[] gateReleased = new bool[0];
        private bool waitingForGate;
        private int waitingGateIndex = -1;
        private int triggeredCueCount;
        private int cameraHintSerial;
        private W11W13NextRuntimeStage stage;
        private bool originsCaptured;

        public event Action<W11W13CameraHint> CameraHintRaised;

        public bool IsAlive { get { return playing || allowingTail; } }
        public string CandidateId { get { return candidateId; } }
        public W11W13NextFamily Family { get { return family; } }
        public W11W13NextVariant Variant { get { return variant; } }
        public W11W13NextRuntimeStage Stage { get { return stage; } }
        public float Duration { get { return duration; } }
        public float Elapsed { get { return elapsed; } }
        public float CurrentIntensity { get { return currentIntensity; } }
        public float TargetIntensity { get { return targetIntensity; } }
        public Vector3 Wind { get { return wind; } }
        public Vector3 LayerDensities { get { return layerDensities; } }
        public int StackLevel { get { return stackLevel; } }
        public int PlayCount { get { return playCount; } }
        public int TriggeredCueCount { get { return triggeredCueCount; } }
        public int CameraHintSerial { get { return cameraHintSerial; } }
        public bool WaitingForGate { get { return waitingForGate; } }
        public string WaitingGateId { get { return waitingForGate && waitingGateIndex >= 0 && waitingGateIndex < gates.Length ? gates[waitingGateIndex].EventId : null; } }
        public int CreatedSourceInstanceCount { get { return sourceInstances == null ? 0 : sourceInstances.Count(value => value != null); } }
        public int ActiveSourceInstanceCount { get { return sourceInstances == null ? 0 : sourceInstances.Count(value => value != null && value.activeSelf); } }
        public int ParticleCapacity { get { return particles == null ? 0 : particles.Where(value => value != null).Sum(value => value.main.maxParticles); } }
        public int ActiveRendererCount { get { return ownedRenderers == null ? 0 : ownedRenderers.Count(value => value != null && value.enabled && value.gameObject.activeInHierarchy); } }

        private void Awake()
        {
            CaptureOrigins();
            ResetForPool();
        }

        private void Update()
        {
            var delta = Mathf.Max(0f, Time.deltaTime);
            if (allowingTail)
            {
                tailRemaining -= delta;
                ApplyOwnedProperties(Mathf.Clamp01(tailRemaining / .28f));
                if (tailRemaining <= 0f) ResetForPool();
                return;
            }
            if (!playing) return;
            if (family == W11W13NextFamily.Environment) UpdateEnvironment(delta);
            else if (family == W11W13NextFamily.HitFeedback) UpdateHitFeedback(delta);
            else UpdateUltimate(delta);
        }

        public void Initialize(VfxRuntimeContext context)
        {
            transform.SetPositionAndRotation(context.Position, context.Rotation);
            ResetForPool();
        }

        public void Play()
        {
            CaptureOrigins();
            var reboundTargets = variant == W11W13NextVariant.HitFlash ? externalRenderers.ToArray() : null;
            ResetForPool();
            if (reboundTargets != null && reboundTargets.Length > 0) BindExternalRenderers(reboundTargets);
            playing = true;
            playCount++;
            elapsed = 0f;
            stage = W11W13NextRuntimeStage.Intro;
            SetAllRenderers(true);
            StartParticles();
            if (family == W11W13NextFamily.Ultimate)
            {
                EnsureSourcePool();
                cueConsumed = new bool[timeline == null ? 0 : timeline.Length];
                hintConsumed = new bool[cameraHints == null ? 0 : cameraHints.Length];
                gateReleased = new bool[gates == null ? 0 : gates.Length];
                SetStageRoots(false);
                ApplyUltimateVisual();
                ProcessTimelineAndHints();
            }
            else if (family == W11W13NextFamily.HitFeedback)
            {
                ApplyHitFrame(0f);
            }
            else
            {
                currentIntensity = Mathf.Clamp01(currentIntensity);
                ApplyEnvironmentFrame(0f);
            }
        }

        public void Stop(VfxStopMode mode)
        {
            if (family == W11W13NextFamily.Ultimate) StopSourceEntries(mode);
            playing = false;
            waitingForGate = false;
            waitingGateIndex = -1;
            if (mode == VfxStopMode.AllowTail && ActiveRendererCount > 0)
            {
                allowingTail = true;
                tailRemaining = .28f;
                stage = W11W13NextRuntimeStage.Tail;
                StopParticles(false);
                return;
            }
            ResetForPool();
        }

        public void ResetForPool()
        {
            playing = false;
            allowingTail = false;
            tailRemaining = 0f;
            elapsed = 0f;
            stage = W11W13NextRuntimeStage.Idle;
            waitingForGate = false;
            waitingGateIndex = -1;
            triggeredCueCount = 0;
            cameraHintSerial = 0;
            RestoreOrigins();
            StopParticles(true);
            SetAllRenderers(false);
            SetStageRoots(false);
            RestoreExternalRenderers();
            StopSourceEntries(VfxStopMode.Immediate);
        }

        public bool SendEvent(string eventId, VfxRuntimeEvent payload)
        {
            if (eventId == "play" || eventId == "trigger" || eventId == "start")
            {
                transform.SetPositionAndRotation(payload.Position, payload.Rotation);
                Play();
                return true;
            }
            if (eventId == "stop" || eventId == "cancel") { Stop(VfxStopMode.Immediate); return true; }
            if (!string.IsNullOrEmpty(eventId) && eventId.StartsWith("gate:", StringComparison.Ordinal)) return ReleaseGate(eventId.Substring(5));
            if (eventId == "set_endpoints") { return SetWorldEndpoints(payload.Position, payload.Position + payload.Rotation * Vector3.right); }
            return false;
        }

        public bool SetIntensity(float value)
        {
            if (!Finite(value)) return false;
            targetIntensity = Mathf.Clamp01(value);
            return true;
        }

        public bool SetWind(Vector3 value)
        {
            if (!Finite(value)) return false;
            wind = Vector3.ClampMagnitude(value, 20f);
            return true;
        }

        public bool SetLayerDensities(float near, float mid, float far)
        {
            if (family != W11W13NextFamily.Environment || !Finite(near) || !Finite(mid) || !Finite(far)) return false;
            layerDensities = new Vector3(Mathf.Clamp01(near), Mathf.Clamp01(mid), Mathf.Clamp01(far));
            if (playing) ApplyEnvironmentFrame(Mathf.Repeat(elapsed / Mathf.Max(.1f, duration), 1f));
            return true;
        }

        public void SetCameraFollow(Transform target, bool follow)
        {
            cameraFollowTarget = target;
            cameraFollow = follow && target != null;
        }

        public bool SetStackLevel(int value)
        {
            if (variant != W11W13NextVariant.ComboSurge) return false;
            stackLevel = Mathf.Clamp(value, 1, 5);
            if (playing) ApplyHitFrame(Mathf.Repeat(elapsed / Mathf.Max(.05f, duration), 1f));
            return true;
        }

        public bool SetReactionColors(Color a, Color b, Color result)
        {
            if (variant != W11W13NextVariant.ElementalReaction) return false;
            reactionA = a; reactionB = b; reactionResult = result;
            if (playing) ApplyHitFrame(Mathf.Clamp01(elapsed / Mathf.Max(.05f, duration)));
            return true;
        }

        public bool SetWorldEndpoints(Vector3 source, Vector3 target)
        {
            if (variant != W11W13NextVariant.LifestealLink || !Finite(source) || !Finite(target) || Vector3.Distance(source, target) < .01f) return false;
            sourcePoint = source;
            targetPoint = target;
            if (playing) ApplyLifestealGeometry();
            return true;
        }

        public bool BindExternalRenderers(Renderer[] targets)
        {
            if (variant != W11W13NextVariant.HitFlash) return false;
            RestoreExternalRenderers();
            externalRenderers = (targets ?? new Renderer[0]).Where(value => value != null).Distinct().ToArray();
            externalBlocks = new MaterialPropertyBlock[externalRenderers.Length];
            for (var index = 0; index < externalRenderers.Length; index++)
            {
                externalBlocks[index] = new MaterialPropertyBlock();
                externalRenderers[index].GetPropertyBlock(externalBlocks[index]);
            }
            return externalRenderers.Length > 0;
        }

        public bool ReleaseGate(string eventId)
        {
            if (string.IsNullOrEmpty(eventId) || gates == null) return false;
            for (var index = 0; index < gates.Length; index++)
            {
                if (!string.Equals(gates[index].EventId, eventId, StringComparison.Ordinal)) continue;
                if (gateReleased == null || gateReleased.Length != gates.Length) gateReleased = new bool[gates.Length];
                gateReleased[index] = true;
                if (waitingGateIndex == index)
                {
                    waitingForGate = false;
                    waitingGateIndex = -1;
                    stage = W11W13NextRuntimeStage.Primary;
                }
                return true;
            }
            return false;
        }

        public W11W13RuntimeSnapshot ReadSnapshot()
        {
            return new W11W13RuntimeSnapshot
            {
                CandidateId = candidateId,
                Family = family,
                Variant = variant,
                Stage = stage,
                Elapsed = elapsed,
                CurrentIntensity = currentIntensity,
                TargetIntensity = targetIntensity,
                StackLevel = stackLevel,
                ActiveRendererCount = ActiveRendererCount,
                ParticleCapacity = ParticleCapacity,
                TriggeredCueCount = triggeredCueCount,
                WaitingGateId = WaitingGateId
            };
        }

        private void UpdateEnvironment(float delta)
        {
            elapsed += delta;
            currentIntensity = Mathf.MoveTowards(currentIntensity, targetIntensity, delta / 1.25f);
            if (cameraFollow && cameraFollowTarget != null) transform.position = cameraFollowTarget.position;
            ApplyEnvironmentFrame(Mathf.Repeat(elapsed / Mathf.Max(.1f, duration), 1f));
        }

        private void ApplyEnvironmentFrame(float phase)
        {
            stage = W11W13NextRuntimeStage.Primary;
            var intensity = Mathf.Clamp01(currentIntensity);
            ApplyOwnedProperties(intensity);
            if (particles != null)
            {
                for (var index = 0; index < particles.Length; index++)
                {
                    var particle = particles[index]; if (particle == null) continue;
                    var emission = particle.emission;
                    var rate = index < baseEmissionRates.Length ? baseEmissionRates[index] : 12f;
                    emission.rateOverTime = rate * intensity * EnvironmentLayerFactor(particle.transform, index, particles.Length);
                    var velocity = particle.velocityOverLifetime;
                    velocity.enabled = true;
                    velocity.space = ParticleSystemSimulationSpace.World;
                    velocity.x = new ParticleSystem.MinMaxCurve(wind.x * (.15f + index * .05f));
                    velocity.y = new ParticleSystem.MinMaxCurve(wind.y * .08f);
                    velocity.z = new ParticleSystem.MinMaxCurve(wind.z * (.15f + index * .05f));
                }
            }
            if (ownedRenderers != null)
            {
                if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock();
                for (var index = 0; index < ownedRenderers.Length; index++)
                {
                    var renderer = ownedRenderers[index]; if (renderer == null) continue;
                    renderer.GetPropertyBlock(propertyBlock);
                    propertyBlock.SetFloat("_GlobalAlpha", intensity * EnvironmentLayerFactor(renderer.transform, index, ownedRenderers.Length));
                    renderer.SetPropertyBlock(propertyBlock);
                }
            }
            var localWind = transform.InverseTransformVector(wind);
            for (var index = 0; index < animatedTransforms.Length && index < basePositions.Length; index++)
            {
                var item = animatedTransforms[index]; if (item == null) continue;
                var drift = localWind * (.012f + index * .003f) * Mathf.Sin(phase * Mathf.PI * 2f + index * .73f);
                var vertical = variant == W11W13NextVariant.Waterfall ? Vector3.down * Mathf.Repeat(phase + index * .17f, 1f) * .22f : Vector3.up * Mathf.Sin((phase * (1.1f + index * .13f) + index * .21f) * Mathf.PI * 2f) * .035f;
                item.localPosition = basePositions[index] + drift + vertical;
                item.localRotation = baseRotations[index] * Quaternion.Euler(
                    variant == W11W13NextVariant.FallingLeaves ? Mathf.Sin(phase * 13f + index) * 42f : 0f,
                    phase * (index % 2 == 0 ? 18f : -13f),
                    Mathf.Sin(phase * 7f + index) * (variant == W11W13NextVariant.MistFog ? 4f : 12f));
                var breath = variant == W11W13NextVariant.Fireflies ? .12f : .035f;
                item.localScale = baseScales[index] * (1f + Mathf.Sin(phase * Mathf.PI * 2f * (1f + index * .11f)) * breath);
            }
        }

        private void UpdateHitFeedback(float delta)
        {
            elapsed += delta;
            var sustained = variant == W11W13NextVariant.ComboSurge || variant == W11W13NextVariant.LifestealLink;
            var phase = sustained ? Mathf.Repeat(elapsed / Mathf.Max(.05f, duration), 1f) : Mathf.Clamp01(elapsed / Mathf.Max(.05f, duration));
            ApplyHitFrame(phase);
            if (!sustained && elapsed >= duration) Stop(VfxStopMode.Immediate);
        }

        private void ApplyHitFrame(float phase)
        {
            stage = phase < .18f ? W11W13NextRuntimeStage.Intro : phase < .72f ? W11W13NextRuntimeStage.Primary : W11W13NextRuntimeStage.Release;
            var oneShotEnvelope = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(phase));
            var envelope = variant == W11W13NextVariant.ComboSurge || variant == W11W13NextVariant.LifestealLink ? .78f + .22f * Mathf.Sin(phase * Mathf.PI * 2f) : oneShotEnvelope;
            ApplyOwnedProperties(envelope);
            switch (variant)
            {
                case W11W13NextVariant.HitFlash: ApplyExternalFlash(phase < .28f ? 1f - phase / .28f : 0f); break;
                case W11W13NextVariant.CriticalStrike:
                    if (primaryBody != null) primaryBody.localRotation = BaseRotation(primaryBody) * Quaternion.Euler(0f, 0f, phase * -32f);
                    if (secondaryBody != null) secondaryBody.localScale = BaseScale(secondaryBody) * Mathf.Lerp(.35f, 1.55f, Mathf.Sin(phase * Mathf.PI));
                    break;
                case W11W13NextVariant.ParrySpark:
                    if (primaryBody != null) primaryBody.localScale = BaseScale(primaryBody) * Mathf.Lerp(.4f, 1.35f, Mathf.Sin(phase * Mathf.PI));
                    break;
                case W11W13NextVariant.KnockupLauncher:
                    if (primaryBody != null) primaryBody.localScale = Vector3.Scale(BaseScale(primaryBody), new Vector3(1f + phase * .4f, .2f + phase * 2.8f, 1f + phase * .4f));
                    if (secondaryBody != null) secondaryBody.localScale = BaseScale(secondaryBody) * Mathf.Lerp(.4f, 1.8f, phase);
                    break;
                case W11W13NextVariant.ComboSurge:
                    for (var index = 0; index < ownedRenderers.Length; index++)
                    {
                        if (ownedRenderers[index] == null) continue;
                        ownedRenderers[index].enabled = index >= 5 || index < stackLevel;
                        if (index < 5) SetRendererColor(ownedRenderers[index].transform, index < 2 ? Color.Lerp(primary, Color.white, .65f) : index == 2 ? secondary : Color.Lerp(secondary, accent, .62f));
                    }
                    for (var index = 0; index < animatedTransforms.Length; index++) if (animatedTransforms[index] != null) animatedTransforms[index].localRotation = BaseRotation(animatedTransforms[index]) * Quaternion.Euler(0f, 0f, phase * 360f * (index % 2 == 0 ? 1f : -1f) * (1f + stackLevel * .12f));
                    break;
                case W11W13NextVariant.ElementalReaction: ApplyReactionGeometry(phase); break;
                case W11W13NextVariant.LifestealLink: ApplyLifestealGeometry(); break;
            }
        }

        private void ApplyReactionGeometry(float phase)
        {
            var approach = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(phase / .42f));
            if (primaryBody != null) primaryBody.localPosition = Vector3.Lerp(new Vector3(-1.25f, 0f, 0f), Vector3.zero, approach);
            if (secondaryBody != null) secondaryBody.localPosition = Vector3.Lerp(new Vector3(1.25f, 0f, 0f), Vector3.zero, approach);
            var released = phase >= .36f;
            if (resultBody != null)
            {
                resultBody.gameObject.SetActive(released);
                resultBody.localScale = BaseScale(resultBody) * Mathf.Lerp(.15f, 1.7f, Mathf.Clamp01((phase - .36f) / .28f));
            }
            SetRendererColor(primaryBody, reactionA);
            SetRendererColor(secondaryBody, reactionB);
            SetRendererColor(resultBody, reactionResult);
        }

        private void ApplyLifestealGeometry()
        {
            var line = lines == null ? null : lines.FirstOrDefault(value => value != null);
            if (line != null)
            {
                line.useWorldSpace = true;
                var count = Mathf.Max(12, line.positionCount);
                line.positionCount = count;
                for (var index = 0; index < count; index++)
                {
                    var u = index / (float)(count - 1);
                    var point = Vector3.Lerp(sourcePoint, targetPoint, u);
                    point += Vector3.down * (4f * u * (1f - u) * .42f);
                    point += transform.forward * Mathf.Sin((u * 3f + elapsed * 1.8f) * Mathf.PI * 2f) * Mathf.Sin(u * Mathf.PI) * .025f;
                    line.SetPosition(index, point);
                }
            }
            if (flowMotes == null) return;
            for (var index = 0; index < flowMotes.Length; index++)
            {
                var mote = flowMotes[index]; if (mote == null) continue;
                var u = 1f - Mathf.Repeat(elapsed * (.58f + index * .06f) + index / (float)Mathf.Max(1, flowMotes.Length), 1f);
                var point = Vector3.Lerp(sourcePoint, targetPoint, u) + Vector3.down * (4f * u * (1f - u) * .42f);
                mote.position = point;
            }
        }

        private void UpdateUltimate(float delta)
        {
            if (waitingForGate)
            {
                stage = W11W13NextRuntimeStage.WaitingGate;
                ApplyUltimateVisual();
                return;
            }
            var next = elapsed + delta;
            if (gates != null)
            {
                for (var index = 0; index < gates.Length; index++)
                {
                    if (gateReleased != null && index < gateReleased.Length && gateReleased[index]) continue;
                    if (gates[index].Time > elapsed + .0001f && gates[index].Time <= next + .0001f)
                    {
                        next = gates[index].Time;
                        waitingForGate = true;
                        waitingGateIndex = index;
                        break;
                    }
                }
            }
            elapsed = next;
            ProcessTimelineAndHints();
            ApplyUltimateVisual();
            if (elapsed >= duration && !waitingForGate) Stop(VfxStopMode.AllowTail);
        }

        private void ProcessTimelineAndHints()
        {
            if (timeline != null)
            {
                if (cueConsumed == null || cueConsumed.Length != timeline.Length) cueConsumed = new bool[timeline.Length];
                for (var index = 0; index < timeline.Length; index++)
                {
                    if (cueConsumed[index] || timeline[index].Time > elapsed + .0001f) continue;
                    cueConsumed[index] = true;
                    TriggerCue(timeline[index]);
                }
            }
            if (cameraHints != null)
            {
                if (hintConsumed == null || hintConsumed.Length != cameraHints.Length) hintConsumed = new bool[cameraHints.Length];
                for (var index = 0; index < cameraHints.Length; index++)
                {
                    if (hintConsumed[index] || cameraHints[index].Time > elapsed + .0001f) continue;
                    hintConsumed[index] = true;
                    cameraHintSerial++;
                    var handler = CameraHintRaised; if (handler != null) handler(cameraHints[index]);
                }
            }
        }

        private void TriggerCue(W11W13TimelineCue cue)
        {
            if (cue.SourceIndex < 0 || cue.SourceIndex >= sourceInstances.Length) return;
            var instance = sourceInstances[cue.SourceIndex]; if (instance == null) return;
            triggeredCueCount++;
            var entry = cue.SourceIndex < sourceEntries.Length ? sourceEntries[cue.SourceIndex] : null;
            if (!cue.Play)
            {
                if (entry != null) entry.Stop(VfxStopMode.Immediate);
                instance.SetActive(false);
                return;
            }
            instance.transform.localPosition = cue.LocalPosition;
            instance.transform.localRotation = Quaternion.Euler(cue.LocalEuler);
            instance.transform.localScale = Vector3.one * Mathf.Max(.01f, cue.Scale <= 0f ? 1f : cue.Scale);
            instance.SetActive(true);
            if (entry != null)
            {
                entry.Initialize(new VfxRuntimeContext(instance.transform.position, instance.transform.rotation));
                entry.Play();
                if (!string.IsNullOrEmpty(cue.EventId)) entry.SendEvent(cue.EventId, new VfxRuntimeEvent(instance.transform.position, instance.transform.rotation));
            }
        }

        private void ApplyUltimateVisual()
        {
            var phase = Mathf.Clamp01(elapsed / Mathf.Max(.05f, duration));
            if (waitingForGate) stage = W11W13NextRuntimeStage.WaitingGate;
            else stage = phase < .22f ? W11W13NextRuntimeStage.Intro : phase < .68f ? W11W13NextRuntimeStage.Primary : phase < .88f ? W11W13NextRuntimeStage.Release : W11W13NextRuntimeStage.Tail;
            for (var index = 0; index < stageRoots.Length; index++)
            {
                var root = stageRoots[index]; if (root == null) continue;
                var active = index == 0 ? phase < .34f : index == 1 ? phase >= .14f && phase < .75f : index == 2 ? phase >= .55f && phase < .93f : phase >= .74f;
                root.SetActive(active);
                if (!active) continue;
                var baseScale = index < baseScales.Length ? baseScales[index] : root.transform.localScale;
                var pulse = 1f + Mathf.Sin((phase * (2.4f + index) + index * .17f) * Mathf.PI * 2f) * (.06f + index * .015f);
                root.transform.localScale = baseScale * pulse;
                root.transform.localRotation = (index < baseRotations.Length ? baseRotations[index] : Quaternion.identity) * Quaternion.Euler(0f, phase * (index % 2 == 0 ? 90f : -120f), phase * (index + 1) * 18f);
            }
            if (variant == W11W13NextVariant.MeteorShower && stageRoots.Length > 1 && stageRoots[1] != null)
            {
                var children = stageRoots[1].GetComponentsInChildren<Transform>(true).Where(value => value != stageRoots[1].transform).ToArray();
                for (var index = 0; index < children.Length; index++) children[index].localPosition = new Vector3((index - 2.5f) * .38f, 2.4f - Mathf.Repeat(phase * 5f + index * .17f, 1f) * 4.4f, (index % 2) * .32f);
            }
            if (variant == W11W13NextVariant.BladeTempest && stageRoots.Length > 1 && stageRoots[1] != null) stageRoots[1].transform.localRotation = Quaternion.Euler(18f, phase * 420f, phase * 70f);
            ApplyOwnedProperties(.86f + .14f * Mathf.Sin(phase * Mathf.PI));
        }

        private void EnsureSourcePool()
        {
            if (sourcePrefabs == null) sourcePrefabs = new GameObject[0];
            if (sourceInstances != null && sourceInstances.Length == sourcePrefabs.Length && sourceInstances.All(value => value != null)) return;
            ReleaseSourceInstances();
            sourceInstances = new GameObject[sourcePrefabs.Length];
            sourceEntries = new IVfxRuntimeEntry[sourcePrefabs.Length];
            for (var index = 0; index < sourcePrefabs.Length; index++)
            {
                var prefab = sourcePrefabs[index]; if (prefab == null) continue;
                var instance = Instantiate(prefab, transform, false);
                instance.name = "RuntimeSource_" + index.ToString("00") + "_" + prefab.name;
                instance.SetActive(false);
                sourceInstances[index] = instance;
                sourceEntries[index] = FindEntry(instance);
            }
        }

        private void StopSourceEntries(VfxStopMode mode)
        {
            if (sourceInstances == null) return;
            for (var index = 0; index < sourceInstances.Length; index++)
            {
                if (index < sourceEntries.Length && sourceEntries[index] != null) sourceEntries[index].Stop(mode);
                if (sourceInstances[index] != null) sourceInstances[index].SetActive(false);
            }
        }

        public void ReleaseSourceInstances()
        {
            if (sourceInstances != null) foreach (var instance in sourceInstances) if (instance != null) Destroy(instance);
            sourceInstances = new GameObject[0];
            sourceEntries = new IVfxRuntimeEntry[0];
        }

        private void OnDestroy() { ReleaseSourceInstances(); RestoreExternalRenderers(); }

        private void CaptureOrigins()
        {
            if (animatedTransforms == null) animatedTransforms = new Transform[0];
            var expected = family == W11W13NextFamily.Ultimate && stageRoots != null ? stageRoots.Length : animatedTransforms.Length;
            if (originsCaptured && baseScales != null && baseScales.Length == expected) return;
            basePositions = animatedTransforms.Select(value => value == null ? Vector3.zero : value.localPosition).ToArray();
            baseRotations = animatedTransforms.Select(value => value == null ? Quaternion.identity : value.localRotation).ToArray();
            baseScales = animatedTransforms.Select(value => value == null ? Vector3.one : value.localScale).ToArray();
            if (family == W11W13NextFamily.Ultimate && stageRoots != null && stageRoots.Length > 0)
            {
                baseRotations = stageRoots.Select(value => value == null ? Quaternion.identity : value.transform.localRotation).ToArray();
                baseScales = stageRoots.Select(value => value == null ? Vector3.one : value.transform.localScale).ToArray();
            }
            baseEmissionRates = particles == null ? new float[0] : particles.Select(value => value == null ? 0f : value.emission.rateOverTime.constant).ToArray();
            originsCaptured = true;
        }

        private void RestoreOrigins()
        {
            if (animatedTransforms != null)
            {
                for (var index = 0; index < animatedTransforms.Length; index++)
                {
                    var item = animatedTransforms[index]; if (item == null) continue;
                    if (index < basePositions.Length) item.localPosition = basePositions[index];
                    if (index < baseRotations.Length) item.localRotation = baseRotations[index];
                    if (index < baseScales.Length) item.localScale = baseScales[index];
                }
            }
            if (family == W11W13NextFamily.Ultimate && stageRoots != null)
            {
                for (var index = 0; index < stageRoots.Length; index++)
                {
                    var root = stageRoots[index]; if (root == null) continue;
                    if (index < baseRotations.Length) root.transform.localRotation = baseRotations[index];
                    if (index < baseScales.Length) root.transform.localScale = baseScales[index];
                }
            }
        }

        private float EnvironmentLayerFactor(Transform target, int fallbackIndex, int count)
        {
            var name = target == null ? string.Empty : target.name;
            if (HasToken(name, "near") || HasToken(name, "ground") || HasToken(name, "splash") || HasToken(name, "skim") || HasToken(name, "impact")) return layerDensities.x;
            if (HasToken(name, "mid") || HasToken(name, "suspended") || HasToken(name, "band") || HasToken(name, "strand")) return layerDensities.y;
            if (HasToken(name, "far") || HasToken(name, "mist") || HasToken(name, "depth") || HasToken(name, "foam")) return layerDensities.z;
            var normalized = count <= 1 ? .5f : fallbackIndex / (float)(count - 1);
            return normalized < .34f ? layerDensities.x : normalized < .67f ? layerDensities.y : layerDensities.z;
        }

        private static bool HasToken(string value, string token) { return value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0; }

        private void StartParticles()
        {
            if (particles == null) return;
            for (var index = 0; index < particles.Length; index++)
            {
                var particle = particles[index]; if (particle == null) continue;
                particle.useAutoRandomSeed = false;
                particle.randomSeed = seed + (uint)index * 97u;
                particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                particle.Play(true);
            }
        }

        private void StopParticles(bool clear)
        {
            if (particles == null) return;
            foreach (var particle in particles) if (particle != null) particle.Stop(true, clear ? ParticleSystemStopBehavior.StopEmittingAndClear : ParticleSystemStopBehavior.StopEmitting);
        }

        private void SetAllRenderers(bool visible)
        {
            if (ownedRenderers == null) return;
            foreach (var renderer in ownedRenderers) if (renderer != null) renderer.enabled = visible;
        }

        private void SetStageRoots(bool active)
        {
            if (stageRoots == null) return;
            foreach (var root in stageRoots) if (root != null) root.SetActive(active);
        }

        private void ApplyOwnedProperties(float alpha)
        {
            if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock();
            if (ownedRenderers == null) return;
            foreach (var renderer in ownedRenderers)
            {
                if (renderer == null) continue;
                renderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor("_PrimaryColor", primary);
                propertyBlock.SetColor("_SecondaryColor", secondary);
                propertyBlock.SetColor("_AccentColor", accent);
                propertyBlock.SetFloat("_GlobalAlpha", Mathf.Clamp01(alpha));
                propertyBlock.SetFloat("_Intensity", Mathf.Lerp(.65f, 1.45f, Mathf.Clamp01(alpha)));
                propertyBlock.SetFloat("_Phase", elapsed / Mathf.Max(.05f, duration));
                renderer.SetPropertyBlock(propertyBlock);
            }
        }

        private void ApplyExternalFlash(float amount)
        {
            if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock();
            for (var index = 0; index < externalRenderers.Length; index++)
            {
                var renderer = externalRenderers[index]; if (renderer == null) continue;
                renderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetFloat("_FlashAmount", Mathf.Clamp01(amount));
                propertyBlock.SetColor("_HitTint", secondary);
                propertyBlock.SetFloat("_HitEdgeWidth", .16f);
                renderer.SetPropertyBlock(propertyBlock);
            }
        }

        private void RestoreExternalRenderers()
        {
            if (externalRenderers != null)
            {
                for (var index = 0; index < externalRenderers.Length; index++)
                {
                    if (externalRenderers[index] == null) continue;
                    if (externalBlocks != null && index < externalBlocks.Length && externalBlocks[index] != null) externalRenderers[index].SetPropertyBlock(externalBlocks[index]);
                    else externalRenderers[index].SetPropertyBlock(null);
                }
            }
            externalRenderers = new Renderer[0];
            externalBlocks = new MaterialPropertyBlock[0];
        }

        private void SetRendererColor(Transform target, Color color)
        {
            if (target == null) return;
            var renderer = target.GetComponent<Renderer>(); if (renderer == null) return;
            if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor("_PrimaryColor", color);
            propertyBlock.SetColor("_SecondaryColor", Color.Lerp(color, Color.white, .45f));
            renderer.SetPropertyBlock(propertyBlock);
        }

        private Vector3 BaseScale(Transform target)
        {
            var index = Array.IndexOf(animatedTransforms, target);
            return index >= 0 && index < baseScales.Length ? baseScales[index] : target == null ? Vector3.one : target.localScale;
        }

        private Quaternion BaseRotation(Transform target)
        {
            var index = Array.IndexOf(animatedTransforms, target);
            return index >= 0 && index < baseRotations.Length ? baseRotations[index] : target == null ? Quaternion.identity : target.localRotation;
        }

        private static IVfxRuntimeEntry FindEntry(GameObject root)
        {
            if (root == null) return null;
            foreach (var behaviour in root.GetComponents<MonoBehaviour>()) if (behaviour is IVfxRuntimeEntry) return (IVfxRuntimeEntry)behaviour;
            return null;
        }

        private static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
        private static bool Finite(Vector3 value) { return Finite(value.x) && Finite(value.y) && Finite(value.z); }
    }
}

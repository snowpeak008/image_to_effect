using UnityEngine;

namespace VFXComposer
{
    /// <summary>Player-safe sustained Area VFX controller. Preview replay remains scene-owned.</summary>
    [DisallowMultipleComponent]
    public sealed class InfernoAreaVfxController : MonoBehaviour, IVfxRuntimeEntry
    {
        private enum AreaState { Idle, Starting, Active, Stopping }

        [SerializeField] private Renderer[] animatedRenderers = new Renderer[0];
        [SerializeField] private Vector4[] maskRects = new Vector4[0];
        [SerializeField] private float[] flowSpeeds = new float[0];
        [SerializeField] private float[] intensities = new float[0];
        [SerializeField] private float[] geometryModes = new float[0];
        [SerializeField] private ParticleSystem[] systems = new ParticleSystem[0];
        [SerializeField] private Transform[] rotatingLayers = new Transform[0];
        [SerializeField] private float[] rotationSpeeds = new float[0];
        [SerializeField] private Renderer pulseRenderer;
        [SerializeField] private Transform pulseTransform;
        [SerializeField, Min(.01f)] private float establishDuration = .6f;
        [SerializeField, Min(.1f)] private float loopDuration = 1.6f;
        [SerializeField, Min(.1f)] private float tickInterval = .8f;
        [SerializeField, Min(.01f)] private float stopDuration = .35f;

        private MaterialPropertyBlock block;
        private Vector3 initialPosition;
        private Quaternion initialRotation;
        private Quaternion[] initialLayerRotations;
        private Vector3 initialPulseScale;
        private bool captured;
        private AreaState state;
        private float stateElapsed;
        private float runtimeElapsed;
        private float activeElapsed;
        private float nextPulse;
        private float pulseAge = float.PositiveInfinity;
        private int pulseCount;

        public bool IsAlive
        {
            get
            {
                if (state != AreaState.Idle) return true;
                foreach (var system in systems) if (system != null && system.IsAlive(true)) return true;
                return false;
            }
        }

        public float RuntimeElapsed { get { return runtimeElapsed; } }
        public float ActiveElapsed { get { return activeElapsed; } }
        public int PulseCount { get { return pulseCount; } }
        public float LoopDuration { get { return loopDuration; } }

        private void Awake() { Capture(); ResetForPool(); }

        private void Update()
        {
            if (state == AreaState.Idle) return;
            var delta = Mathf.Max(0f, Time.deltaTime);
            stateElapsed += delta;
            runtimeElapsed += delta;
            pulseAge += delta;
            RotateLayers(delta);

            if (state == AreaState.Starting && stateElapsed >= establishDuration)
            {
                state = AreaState.Active;
                stateElapsed = 0f;
                activeElapsed = 0f;
                nextPulse = tickInterval;
            }
            else if (state == AreaState.Active)
            {
                activeElapsed += delta;
                while (activeElapsed >= nextPulse)
                {
                    TriggerPulse();
                    nextPulse += tickInterval;
                }
            }
            else if (state == AreaState.Stopping && stateElapsed >= stopDuration)
            {
                ResetForPool();
                return;
            }

            UpdateVisuals();
        }

        public void Initialize(VfxRuntimeContext context)
        {
            ResetForPool();
            transform.SetPositionAndRotation(context.Position, context.Rotation);
        }

        public void Play()
        {
            if (state != AreaState.Idle)
            {
                Refresh();
                return;
            }

            Capture();
            ClearParticles();
            state = AreaState.Starting;
            stateElapsed = 0f;
            runtimeElapsed = 0f;
            activeElapsed = 0f;
            nextPulse = tickInterval;
            pulseAge = float.PositiveInfinity;
            pulseCount = 0;
            foreach (var renderer in animatedRenderers) if (renderer != null) renderer.enabled = true;
            foreach (var system in systems) if (system != null) system.Play(true);
            UpdateVisuals();
        }

        public bool SendEvent(string eventId, VfxRuntimeEvent payload)
        {
            if (eventId == "play" || eventId == "start")
            {
                transform.SetPositionAndRotation(payload.Position, payload.Rotation);
                Play();
                return true;
            }
            if (eventId == "refresh") { Refresh(); return true; }
            if (eventId == "tick") { if (state != AreaState.Idle) TriggerPulse(); return state != AreaState.Idle; }
            if (eventId == "stop") { Stop(VfxStopMode.AllowTail); return true; }
            return false;
        }

        public void Stop(VfxStopMode mode)
        {
            if (mode == VfxStopMode.Immediate)
            {
                ResetForPool();
                return;
            }
            if (state == AreaState.Idle || state == AreaState.Stopping) return;
            state = AreaState.Stopping;
            stateElapsed = 0f;
            foreach (var system in systems) if (system != null) system.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        public void ResetForPool()
        {
            Capture();
            ClearParticles();
            state = AreaState.Idle;
            stateElapsed = 0f;
            runtimeElapsed = 0f;
            activeElapsed = 0f;
            nextPulse = tickInterval;
            pulseAge = float.PositiveInfinity;
            pulseCount = 0;
            transform.SetPositionAndRotation(initialPosition, initialRotation);
            for (var index = 0; index < rotatingLayers.Length && index < initialLayerRotations.Length; index++)
                if (rotatingLayers[index] != null) rotatingLayers[index].localRotation = initialLayerRotations[index];
            if (pulseTransform != null) pulseTransform.localScale = initialPulseScale;
            ApplyAlpha(0f);
            foreach (var renderer in animatedRenderers) if (renderer != null) renderer.enabled = false;
        }

        private void Refresh()
        {
            if (state == AreaState.Idle) { Play(); return; }
            if (state == AreaState.Stopping) { state = AreaState.Active; stateElapsed = 0f; foreach (var system in systems) if (system != null) system.Play(true); }
            // Deliberately preserve runtimeElapsed, activeElapsed, particle ages and orbital phase.
        }

        private void TriggerPulse()
        {
            pulseAge = 0f;
            pulseCount++;
            if (pulseRenderer != null) pulseRenderer.enabled = true;
        }

        private void UpdateVisuals()
        {
            var alpha = 1f;
            if (state == AreaState.Starting) alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(stateElapsed / establishDuration));
            else if (state == AreaState.Stopping) alpha = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(stateElapsed / stopDuration));
            ApplyAlpha(alpha);

            if (pulseRenderer == null || pulseTransform == null) return;
            var pulseT = pulseAge / .36f;
            if (pulseT >= 1f)
            {
                pulseRenderer.enabled = false;
                return;
            }
            pulseRenderer.enabled = true;
            pulseTransform.localScale = initialPulseScale * Mathf.Lerp(.48f, 1.14f, Mathf.SmoothStep(0f, 1f, pulseT));
            pulseRenderer.GetPropertyBlock(Block);
            Block.SetFloat("_GlobalAlpha", alpha * Mathf.Sin(Mathf.PI * pulseT) * .28f);
            Block.SetFloat("_RuntimeTime", runtimeElapsed);
            Block.SetFloat("_GeometryMode", 7f);
            pulseRenderer.SetPropertyBlock(Block);
        }

        private void ApplyAlpha(float alpha)
        {
            for (var index = 0; index < animatedRenderers.Length; index++)
            {
                var renderer = animatedRenderers[index];
                if (renderer == null || renderer == pulseRenderer) continue;
                renderer.GetPropertyBlock(Block);
                Block.SetFloat("_GlobalAlpha", alpha);
                Block.SetFloat("_RuntimeTime", runtimeElapsed);
                Block.SetFloat("_RuntimePhase", index * .173f);
                if (index < maskRects.Length) Block.SetVector("_UVRect", maskRects[index]);
                if (index < flowSpeeds.Length) Block.SetFloat("_FlowSpeed", flowSpeeds[index]);
                if (index < intensities.Length) Block.SetFloat("_Intensity", intensities[index]);
                if (index < geometryModes.Length) Block.SetFloat("_GeometryMode", geometryModes[index]);
                renderer.SetPropertyBlock(Block);
            }
        }

        private void RotateLayers(float delta)
        {
            for (var index = 0; index < rotatingLayers.Length; index++)
            {
                var layer = rotatingLayers[index];
                if (layer == null) continue;
                var speed = index < rotationSpeeds.Length ? rotationSpeeds[index] : 0f;
                layer.Rotate(0f, 0f, speed * delta, Space.Self);
            }
        }

        private void ClearParticles()
        {
            foreach (var system in systems)
            {
                if (system == null) continue;
                system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                system.Clear(true);
            }
        }

        private void Capture()
        {
            if (captured) return;
            initialPosition = transform.position;
            initialRotation = transform.rotation;
            initialLayerRotations = new Quaternion[rotatingLayers.Length];
            for (var index = 0; index < rotatingLayers.Length; index++) initialLayerRotations[index] = rotatingLayers[index] == null ? Quaternion.identity : rotatingLayers[index].localRotation;
            initialPulseScale = pulseTransform == null ? Vector3.one : pulseTransform.localScale;
            captured = true;
        }

        private MaterialPropertyBlock Block
        {
            get
            {
                if (block == null) block = new MaterialPropertyBlock();
                return block;
            }
        }
    }
}

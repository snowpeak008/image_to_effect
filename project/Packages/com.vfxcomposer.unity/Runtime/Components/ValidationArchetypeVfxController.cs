using UnityEngine;

namespace VFXComposer
{
    public enum ValidationArchetypeProfile { Aura, Beam, Trail, Shield, Spawn }

    /// <summary>Player-safe controller shared by the five gallery validation archetypes.</summary>
    [DisallowMultipleComponent]
    public sealed class ValidationArchetypeVfxController : MonoBehaviour, IVfxRuntimeEntry
    {
        [SerializeField] private ValidationArchetypeProfile profile;
        [SerializeField] private Renderer[] renderers = new Renderer[0];
        [SerializeField] private ParticleSystem[] particles = new ParticleSystem[0];
        [SerializeField] private float[] shapeModes = new float[0];
        [SerializeField] private float[] intensities = new float[0];
        [SerializeField] private Color primaryColor = Color.white;
        [SerializeField] private Color secondaryColor = Color.white;
        [SerializeField] private bool sustained = true;
        [SerializeField, Min(.1f)] private float duration = 2.2f;
        [SerializeField, Min(.01f)] private float stopDuration = .3f;

        private MaterialPropertyBlock block;
        private Vector3 initialPosition;
        private Quaternion initialRotation;
        private bool captured;
        private bool playing;
        private bool stopping;
        private float elapsed;
        private float stopElapsed;
        private float pulseAge = 99f;

        public ValidationArchetypeProfile Profile { get { return profile; } }
        public bool Sustained { get { return sustained; } }
        public bool IsAlive { get { return playing || stopping; } }
        public float Elapsed { get { return elapsed; } }

        private MaterialPropertyBlock Block { get { if (block == null) block = new MaterialPropertyBlock(); return block; } }
        private void Awake() { Capture(); ResetForPool(); }

        private void Update()
        {
            if (!playing && !stopping) return;
            var delta = Mathf.Max(0f, Time.deltaTime);
            pulseAge += delta;
            if (playing)
            {
                elapsed += delta;
                if (!sustained && elapsed >= duration) BeginStop();
            }
            if (stopping)
            {
                stopElapsed += delta;
                if (stopElapsed >= stopDuration) { ResetForPool(); return; }
            }
            ApplyVisuals();
        }

        public void Initialize(VfxRuntimeContext context) { ResetForPool(); transform.SetPositionAndRotation(context.Position, context.Rotation); }

        public void Play()
        {
            Capture();
            playing = true; stopping = false; elapsed = 0f; stopElapsed = 0f; pulseAge = 99f;
            foreach (var renderer in renderers) if (renderer != null) renderer.enabled = true;
            foreach (var particle in particles) if (particle != null) { particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); particle.Play(true); }
            ApplyVisuals();
        }

        public bool SendEvent(string eventId, VfxRuntimeEvent payload)
        {
            if (eventId == "play" || eventId == "start") { transform.SetPositionAndRotation(payload.Position, payload.Rotation); Play(); return true; }
            if (eventId == "tick" || eventId == "hit") { if (!playing) return false; pulseAge = 0f; return true; }
            if (eventId == "break") { if (!playing) return false; pulseAge = 0f; BeginStop(); return true; }
            if (eventId == "stop") { Stop(VfxStopMode.AllowTail); return true; }
            return false;
        }

        public void Stop(VfxStopMode mode)
        {
            if (mode == VfxStopMode.Immediate) { ResetForPool(); return; }
            BeginStop();
        }

        public void ResetForPool()
        {
            Capture();
            playing = false; stopping = false; elapsed = 0f; stopElapsed = 0f; pulseAge = 99f;
            transform.SetPositionAndRotation(initialPosition, initialRotation);
            foreach (var particle in particles) if (particle != null) particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ApplyVisuals(0f);
            foreach (var renderer in renderers) if (renderer != null) renderer.enabled = false;
        }

        private void BeginStop()
        {
            if (!playing && !stopping) return;
            playing = false; stopping = true; stopElapsed = 0f;
            foreach (var particle in particles) if (particle != null) particle.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        private void ApplyVisuals(float forcedAlpha = -1f)
        {
            var alpha = forcedAlpha >= 0f ? forcedAlpha : Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / .24f));
            if (stopping) alpha *= 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(stopElapsed / stopDuration));
            var progress = sustained ? Mathf.Repeat(elapsed, Mathf.Max(.1f, duration)) / Mathf.Max(.1f, duration) : Mathf.Clamp01(elapsed / Mathf.Max(.1f, duration));
            var pulse = pulseAge >= .55f ? 0f : Mathf.Sin(Mathf.PI * Mathf.Clamp01(pulseAge / .55f));
            for (var index = 0; index < renderers.Length; index++)
            {
                var renderer = renderers[index]; if (renderer == null) continue;
                renderer.GetPropertyBlock(Block);
                Block.SetFloat("_RuntimeTime", elapsed);
                Block.SetFloat("_Progress", progress);
                Block.SetFloat("_Pulse", pulse);
                Block.SetFloat("_GlobalAlpha", alpha);
                Block.SetFloat("_ShapeMode", index < shapeModes.Length ? shapeModes[index] : 0f);
                Block.SetFloat("_Intensity", index < intensities.Length ? intensities[index] : 1f);
                Block.SetColor("_PrimaryColor", primaryColor);
                Block.SetColor("_SecondaryColor", secondaryColor);
                renderer.SetPropertyBlock(Block);
            }
        }

        private void Capture()
        {
            if (captured) return;
            initialPosition = transform.position; initialRotation = transform.rotation; captured = true;
        }
    }
}

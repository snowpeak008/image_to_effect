using UnityEngine;

namespace VFXComposer
{
    /// <summary>Player-safe one-shot point impact controller. Visual children remain compiler-owned.</summary>
    [DisallowMultipleComponent]
    public sealed class TimedImpactVfxController : MonoBehaviour, IVfxRuntimeEntry
    {
        [SerializeField] private ParticleSystem[] systems = new ParticleSystem[0];
        [SerializeField, Min(.01f)] private float duration = .48f;
        private Vector3 initialPosition;
        private Quaternion initialRotation;
        private bool captured;
        private bool playing;
        private float elapsed;

        public bool IsAlive
        {
            get
            {
                if (playing) return true;
                foreach (var system in systems) if (system != null && system.IsAlive(true)) return true;
                return false;
            }
        }

        private void Awake() { Capture(); ResetForPool(); }
        private void Update()
        {
            if (!playing) return;
            elapsed += Time.deltaTime;
            if (elapsed >= duration && !AnyParticleAlive()) playing = false;
        }

        public void Initialize(VfxRuntimeContext context)
        {
            ResetForPool();
            transform.SetPositionAndRotation(context.Position, context.Rotation);
        }

        public void Play()
        {
            Clear(true);
            elapsed = 0f;
            playing = true;
            foreach (var system in systems) if (system != null) system.Play(true);
        }

        public bool SendEvent(string eventId, VfxRuntimeEvent payload)
        {
            if (eventId != "impact" && eventId != "play") return false;
            transform.SetPositionAndRotation(payload.Position, payload.Rotation);
            Play();
            return true;
        }

        public void Stop(VfxStopMode mode)
        {
            var immediate = mode == VfxStopMode.Immediate;
            foreach (var system in systems) if (system != null) system.Stop(true, immediate ? ParticleSystemStopBehavior.StopEmittingAndClear : ParticleSystemStopBehavior.StopEmitting);
            if (immediate) { Clear(true); playing = false; }
        }

        public void ResetForPool()
        {
            Capture();
            Clear(true);
            transform.SetPositionAndRotation(initialPosition, initialRotation);
            elapsed = 0f;
            playing = false;
        }

        private bool AnyParticleAlive()
        {
            foreach (var system in systems) if (system != null && system.IsAlive(true)) return true;
            return false;
        }

        private void Clear(bool deactivateParticles)
        {
            foreach (var system in systems)
            {
                if (system == null) continue;
                system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                system.Clear(true);
                if (deactivateParticles) system.GetComponent<Renderer>().SetPropertyBlock(null);
            }
        }

        private void Capture()
        {
            if (captured) return;
            initialPosition = transform.position;
            initialRotation = transform.rotation;
            captured = true;
        }
    }
}

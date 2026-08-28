using System;
using System.Collections.Generic;
using UnityEngine;

namespace VFXComposer.W24
{
    [Serializable]
    public struct W24FragmentState
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Velocity;
        public Vector3 AngularVelocity;
        public float Age;
        public float Lifetime;
        public uint Seed;
        public bool Alive;
    }

    /// <summary>Pure per-fragment kinematic integration. Every fragment owns position, rotation, velocity, lifetime and seed.</summary>
    public static class W24FragmentMotionKernel
    {
        public static W24FragmentState Create(Vector3 position, Quaternion rotation, uint seed, float lifetime, float speed)
        {
            var random = new W24DeterministicRandom(seed);
            var direction = new Vector3(random.Range(-1f, 1f), random.Range(.15f, 1f), random.Range(-1f, 1f)).normalized;
            return new W24FragmentState { Position = position, Rotation = rotation, Velocity = direction * speed, AngularVelocity = new Vector3(random.Range(-260f, 260f), random.Range(-260f, 260f), random.Range(-260f, 260f)), Lifetime = Mathf.Max(.01f, lifetime), Seed = seed == 0u ? 1u : seed, Alive = true };
        }
        public static W24FragmentState Advance(W24FragmentState state, float deltaTime, float damping)
        {
            if (!state.Alive || deltaTime <= 0f) return state;
            var dt = Mathf.Max(0f, deltaTime);
            state.Age += dt;
            state.Position += state.Velocity * dt;
            state.Rotation = Quaternion.Euler(state.AngularVelocity * dt) * state.Rotation;
            var drag = Mathf.Exp(-Mathf.Max(0f, damping) * dt);
            state.Velocity *= drag;
            state.AngularVelocity *= drag;
            if (state.Age >= state.Lifetime) state.Alive = false;
            return state;
        }
    }

    [DisallowMultipleComponent]
    public sealed class W24FragmentMotionSystem : MonoBehaviour, IW24SemanticTelemetrySource
    {
        [SerializeField] private Transform[] fragments = new Transform[0];
        [SerializeField, Min(.01f)] private float lifetime = .8f;
        [SerializeField, Min(0f)] private float initialSpeed = 2.5f;
        [SerializeField, Min(0f)] private float damping = 1.2f;
        [SerializeField] private uint canonicalSeed = 401u;
        private readonly List<W24FragmentState> states = new List<W24FragmentState>();
        private readonly List<int> fragmentSlots = new List<int>();
        private Vector3[] originalPositions = new Vector3[0];
        private Quaternion[] originalRotations = new Quaternion[0];
        private float elapsed;
        private int eventSerial;
        private bool active;
        private uint activeSeed;
        private string lastEventId = "reset";

        public IReadOnlyList<W24FragmentState> States { get { return states; } }
        private void Awake() { CaptureInitial(); ResetForPool(); }
        private void Update() { Advance(Time.deltaTime); }
        public void SetFragments(Transform[] values)
        {
            fragments = values ?? new Transform[0];
            originalPositions = new Vector3[0]; originalRotations = new Quaternion[0];
            CaptureInitial(); ResetForPool();
        }
        public void Play(uint seed = 0u)
        {
            CaptureInitial(); states.Clear(); fragmentSlots.Clear(); elapsed = 0f; active = true; eventSerial++;
            activeSeed = seed == 0u ? canonicalSeed : seed;
            lastEventId = "fragment_start";
            for (var index = 0; index < fragments.Length; index++)
            {
                var fragment = fragments[index]; if (fragment == null) continue;
                fragment.gameObject.SetActive(true);
                states.Add(W24FragmentMotionKernel.Create(fragment.localPosition, fragment.localRotation, unchecked(activeSeed + (uint)(index * 7919 + 1)), lifetime, initialSpeed));
                fragmentSlots.Add(index);
            }
            if (states.Count == 0) { active = false; lastEventId = "fragment_complete"; eventSerial++; }
        }
        public void Advance(float deltaTime)
        {
            if (!active) return;
            elapsed += Mathf.Max(0f, deltaTime); var alive = 0;
            for (var index = 0; index < states.Count; index++)
            {
                var state = W24FragmentMotionKernel.Advance(states[index], deltaTime, damping); states[index] = state;
                var slot = index < fragmentSlots.Count ? fragmentSlots[index] : -1;
                if (slot >= 0 && slot < fragments.Length && fragments[slot] != null) { fragments[slot].localPosition = state.Position; fragments[slot].localRotation = state.Rotation; fragments[slot].gameObject.SetActive(state.Alive); }
                if (state.Alive) alive++;
            }
            if (alive == 0 && active) { active = false; eventSerial++; lastEventId = "fragment_complete"; }
        }
        public void ResetForPool()
        {
            CaptureInitial(); states.Clear(); fragmentSlots.Clear(); active = false; elapsed = 0f; activeSeed = canonicalSeed; eventSerial++; lastEventId = "reset";
            for (var index = 0; index < fragments.Length; index++) if (fragments[index] != null) { fragments[index].localPosition = originalPositions[index]; fragments[index].localRotation = originalRotations[index]; fragments[index].gameObject.SetActive(false); }
        }
        public W24SemanticTelemetry ReadSemanticTelemetry() { return new W24SemanticTelemetry { Module = "fragment_motion", State = active ? W24SemanticState.Impulse : W24SemanticState.Idle, Seed = activeSeed, EventSerial = eventSerial, ActiveItemCount = states.FindAll(state => state.Alive).Count, Elapsed = elapsed, CleanupComplete = !active, LastEventId = lastEventId }; }
        private void CaptureInitial()
        {
            if (originalPositions.Length == fragments.Length) return;
            originalPositions = new Vector3[fragments.Length]; originalRotations = new Quaternion[fragments.Length];
            for (var index = 0; index < fragments.Length; index++) if (fragments[index] != null) { originalPositions[index] = fragments[index].localPosition; originalRotations[index] = fragments[index].localRotation; }
        }
        private void OnDisable() { ResetForPool(); }
    }
}

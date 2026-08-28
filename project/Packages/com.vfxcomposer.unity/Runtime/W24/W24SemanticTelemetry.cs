using System;
using UnityEngine;

namespace VFXComposer.W24
{
    /// <summary>Semantic facts read from the runtime.  These are not a substitute for rendered-frame evidence.</summary>
    public enum W24SemanticState
    {
        Idle,
        Continuous,
        Impulse,
        Replacing,
        Clearing,
        Completed,
        Interrupted,
        Faulted
    }

    [Serializable]
    public struct W24SemanticTelemetry
    {
        public string Module;
        public W24SemanticState State;
        public uint Seed;
        public int EventSerial;
        public int ActiveItemCount;
        public float Elapsed;
        public bool CleanupComplete;
        public string LastEventId;
        public string FaultCode;
    }

    public interface IW24SemanticTelemetrySource
    {
        W24SemanticTelemetry ReadSemanticTelemetry();
    }

    /// <summary>Stable pseudo-random sequence used by W24 runtime modules. It has no Unity global-random dependency.</summary>
    public struct W24DeterministicRandom
    {
        private uint state;

        public W24DeterministicRandom(uint seed) { state = seed == 0u ? 0x6D2B79F5u : seed; }
        public uint NextUInt()
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return state;
        }
        public float Next01() { return (NextUInt() & 0x00FFFFFFu) / 16777215f; }
        public float Range(float min, float max) { return Mathf.Lerp(min, max, Next01()); }
    }
}

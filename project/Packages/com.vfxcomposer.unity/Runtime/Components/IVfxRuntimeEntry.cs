using UnityEngine;

namespace VFXComposer
{
    public enum VfxStopMode { Immediate, AllowTail }

    public struct VfxRuntimeContext
    {
        public Vector3 Position;
        public Quaternion Rotation;

        public VfxRuntimeContext(Vector3 position, Quaternion rotation) { Position = position; Rotation = rotation; }
    }

    public struct VfxRuntimeEvent
    {
        public Vector3 Position;
        public Quaternion Rotation;

        public VfxRuntimeEvent(Vector3 position, Quaternion rotation) { Position = position; Rotation = rotation; }
    }

    /// <summary>Player-safe contract implemented by every production Runtime Entry.</summary>
    public interface IVfxRuntimeEntry
    {
        bool IsAlive { get; }
        void Initialize(VfxRuntimeContext context);
        void Play();
        bool SendEvent(string eventId, VfxRuntimeEvent payload);
        void Stop(VfxStopMode mode);
        void ResetForPool();
    }
}

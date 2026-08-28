using System;
using UnityEngine;

namespace VFXComposer
{
    /// <summary>The currently visible compiler-generated stage. This is visual state only; it has no gameplay semantics.</summary>
    public enum VfxRuntimeStage { None, Launch, Travel, Impact }

    /// <summary>Player-safe controller placed on every compiler-managed VFX Prefab.</summary>
    [DisallowMultipleComponent]
    public sealed class GeneratedVfxController : MonoBehaviour, IVfxRuntimeEntry
    {
        [SerializeField] private GameObject launchRoot;
        [SerializeField] private GameObject travelRoot;
        [SerializeField] private GameObject impactRoot;
        [SerializeField] private bool launchEnabled = true;
        [SerializeField] private bool travelEnabled = true;
        [SerializeField] private bool impactEnabled = true;
        [SerializeField, Min(0f)] private float teleportClearDistance = 1.75f;

        private Vector3 initialPosition;
        private Quaternion initialRotation;
        private bool initialTransformCaptured;
        private GameObject fadingRoot;
        private bool hasTravelPosition;

        public VfxRuntimeStage CurrentStage { get; private set; }
        public bool IsAlive { get { return CurrentStage != VfxRuntimeStage.None || !IsFullyClear(launchRoot) || !IsFullyClear(travelRoot) || !IsFullyClear(impactRoot); } }
        public event Action<VfxRuntimeStage> StageChanged;

        private void Awake()
        {
            CaptureInitialTransform();
            ResetForPool();
        }

        private void Update()
        {
            if (fadingRoot != null && IsFullyClear(fadingRoot))
            {
                fadingRoot.SetActive(false);
                fadingRoot = null;
            }
        }

        public void PlayLaunch() { PlayStage(VfxRuntimeStage.Launch); }
        public void StartTravel() { PlayStage(VfxRuntimeStage.Travel); }
        public void Initialize(VfxRuntimeContext context) { ResetForPool(); transform.SetPositionAndRotation(context.Position, context.Rotation); }
        public void Play() { PlayLaunch(); }
        public bool SendEvent(string eventId, VfxRuntimeEvent payload)
        {
            switch (eventId)
            {
                case "launch": transform.SetPositionAndRotation(payload.Position, payload.Rotation); PlayLaunch(); return true;
                case "travel": SetTravelTransform(payload.Position, payload.Rotation); StartTravel(); return true;
                case "impact": PlayImpact(payload.Position); return true;
                default: return false;
            }
        }
        public void Stop(VfxStopMode mode) { StopEffect(mode == VfxStopMode.Immediate); }

        /// <summary>
        /// Updates the travel pose. The first position, movement outside Travel, and a displacement above
        /// teleportClearDistance are treated as teleports and clear trails after moving; normal Travel steps retain them.
        /// </summary>
        public void SetTravelTransform(Vector3 position, Quaternion rotation)
        {
            var clearForTeleport = !hasTravelPosition || CurrentStage != VfxRuntimeStage.Travel ||
                Vector3.SqrMagnitude(position - transform.position) > teleportClearDistance * teleportClearDistance;
            transform.SetPositionAndRotation(position, rotation);
            if (clearForTeleport) ClearTrails(travelRoot);
            hasTravelPosition = true;
        }

        public void PlayImpact(Vector3 position)
        {
            transform.position = position;
            ClearTrails(travelRoot);
            hasTravelPosition = false;
            PlayStage(VfxRuntimeStage.Impact);
        }

        /// <summary>Immediate clears synchronously; non-immediate stops emission and lets live particles fade.</summary>
        public void StopEffect(bool immediate)
        {
            StopStage(launchRoot, immediate);
            StopStage(travelRoot, immediate);
            StopStage(impactRoot, immediate);
            hasTravelPosition = false;
            SetStage(VfxRuntimeStage.None);
        }

        /// <summary>Returns this instance to its captured pose with no live particles or trail segments.</summary>
        public void ResetForPool()
        {
            CaptureInitialTransform();
            StopStage(launchRoot, true);
            StopStage(travelRoot, true);
            StopStage(impactRoot, true);
            transform.SetPositionAndRotation(initialPosition, initialRotation);
            ClearTrails(launchRoot);
            ClearTrails(travelRoot);
            ClearTrails(impactRoot);
            fadingRoot = null;
            hasTravelPosition = false;
            SetStage(VfxRuntimeStage.None);
        }

        private void PlayStage(VfxRuntimeStage stage)
        {
            StopStage(launchRoot, true);
            StopStage(travelRoot, true);
            StopStage(impactRoot, true);
            if (stage != VfxRuntimeStage.Travel) hasTravelPosition = false;
            if (!IsStageEnabled(stage)) { SetStage(VfxRuntimeStage.None); return; }
            var root = RootFor(stage);
            if (root == null) { SetStage(VfxRuntimeStage.None); return; }
            root.SetActive(true);
            ClearTrails(root);
            foreach (var particle in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                particle.Play(true);
            }
            SetStage(stage);
        }

        private void StopStage(GameObject root, bool immediate)
        {
            if (root == null) return;
            foreach (var particle in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                particle.Stop(true, immediate ? ParticleSystemStopBehavior.StopEmittingAndClear : ParticleSystemStopBehavior.StopEmitting);
                if (immediate) particle.Clear(true);
            }
            if (immediate)
            {
                ClearTrails(root);
                root.SetActive(false);
                if (fadingRoot == root) fadingRoot = null;
            }
            else if (root.activeSelf)
            {
                fadingRoot = root;
            }
        }

        private static void ClearTrails(GameObject root)
        {
            if (root == null) return;
            foreach (var trail in root.GetComponentsInChildren<TrailRenderer>(true)) trail.Clear();
        }

        private static bool IsFullyClear(GameObject root)
        {
            if (root == null) return true;
            foreach (var particle in root.GetComponentsInChildren<ParticleSystem>(true)) if (particle.IsAlive(true)) return false;
            foreach (var trail in root.GetComponentsInChildren<TrailRenderer>(true)) if (trail.positionCount > 0) return false;
            return true;
        }

        private GameObject RootFor(VfxRuntimeStage stage)
        {
            switch (stage)
            {
                case VfxRuntimeStage.Launch: return launchRoot;
                case VfxRuntimeStage.Travel: return travelRoot;
                case VfxRuntimeStage.Impact: return impactRoot;
                default: return null;
            }
        }

        private bool IsStageEnabled(VfxRuntimeStage stage)
        {
            switch (stage)
            {
                case VfxRuntimeStage.Launch: return launchEnabled;
                case VfxRuntimeStage.Travel: return travelEnabled;
                case VfxRuntimeStage.Impact: return impactEnabled;
                default: return false;
            }
        }

        private void SetStage(VfxRuntimeStage stage)
        {
            if (CurrentStage == stage) return;
            CurrentStage = stage;
            var changed = StageChanged;
            if (changed != null) changed(stage);
        }

        private void CaptureInitialTransform()
        {
            if (initialTransformCaptured) return;
            initialPosition = transform.position;
            initialRotation = transform.rotation;
            initialTransformCaptured = true;
        }
    }
}

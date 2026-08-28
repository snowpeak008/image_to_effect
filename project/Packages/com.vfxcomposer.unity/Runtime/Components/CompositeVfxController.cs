using System;
using UnityEngine;

namespace VFXComposer
{
    [Serializable] public sealed class CompositeRuntimeEvent
    {
        public float Time; public int SourceIndex; public string RefId; public string Action;
        public Vector3 Position; public Vector3 Rotation; public float Scale = 1f;
        public bool HasPalette; public Color Primary = Color.white; public Color Secondary = Color.white; public Color Accent = Color.white;
    }
    [Serializable] public sealed class CompositeCameraHintRuntime { public float Time; public string Type; public float Strength; }
    [Serializable] public sealed class CompositeGateRuntime { public float Time; public string WaitFor; }

    /// <summary>
    /// Player-safe orchestration Runtime Entry. Child Prefabs are dependency references and are
    /// instantiated into a reusable runtime pool; no child asset is copied into the Composite.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CompositeVfxController : MonoBehaviour, IVfxRuntimeEntry
    {
        [SerializeField] private string compositionId;
        [SerializeField, Min(.1f)] private float duration = 4f;
        [SerializeField] private GameObject[] sourcePrefabs = new GameObject[0];
        [SerializeField] private CompositeRuntimeEvent[] timeline = new CompositeRuntimeEvent[0];
        [SerializeField] private CompositeCameraHintRuntime[] cameraHints = new CompositeCameraHintRuntime[0];
        [SerializeField] private CompositeGateRuntime[] gates = new CompositeGateRuntime[0];
        [SerializeField] private TextAsset descriptor;

        private GameObject[] instances = new GameObject[0];
        private IVfxRuntimeEntry[] entries = new IVfxRuntimeEntry[0];
        private bool[] releasedGates = new bool[0];
        private bool playing, waiting;
        private int nextEvent, nextHint, nextGate, triggeredEvents, hintSerial;
        private float elapsed, playbackRate = 1f;
        private string waitingGateId, lastHintType;
        private float lastHintStrength;

        public string CompositionId { get { return compositionId; } }
        public float Duration { get { return duration; } }
        public float Elapsed { get { return elapsed; } }
        public bool IsAlive { get { return playing; } }
        public bool WaitingForGate { get { return waiting; } }
        public string WaitingGateId { get { return waitingGateId; } }
        public int TriggeredEventCount { get { return triggeredEvents; } }
        public int CameraHintSerial { get { return hintSerial; } }
        public string LastCameraHintType { get { return lastHintType; } }
        public float LastCameraHintStrength { get { return lastHintStrength; } }
        public int CreatedInstanceCount { get { return instances == null ? 0 : instances.Length; } }
        public int ActiveChildCount { get { var count = 0; if (instances != null) foreach (var item in instances) if (item != null && item.activeSelf) count++; return count; } }
        public float PlaybackRate { get { return playbackRate; } set { playbackRate = Mathf.Clamp(value, .1f, 4f); } }
        public TextAsset Descriptor { get { return descriptor; } }

        private void Update()
        {
            if (!playing || waiting) return;
            var proposed = elapsed + Mathf.Max(0f, Time.deltaTime) * playbackRate;
            if (nextGate < gates.Length && !releasedGates[nextGate] && proposed >= gates[nextGate].Time)
            {
                elapsed = gates[nextGate].Time; RunDueEvents(); RunDueHints(); waiting = true; waitingGateId = gates[nextGate].WaitFor; return;
            }
            elapsed = proposed; RunDueEvents(); RunDueHints();
            if (elapsed >= duration) Stop(VfxStopMode.AllowTail);
        }

        public void Initialize(VfxRuntimeContext context) { transform.SetPositionAndRotation(context.Position, context.Rotation); ResetForPool(); }
        public void Play()
        {
            EnsureInstances(); ResetChildren(); elapsed = 0; nextEvent = nextHint = nextGate = triggeredEvents = hintSerial = 0; waiting = false; waitingGateId = null; lastHintType = null; lastHintStrength = 0; playbackRate = 1f; releasedGates = new bool[gates.Length]; playing = true; RunDueEvents(); RunDueHints();
        }
        public bool SendEvent(string eventId, VfxRuntimeEvent payload)
        {
            if (eventId == "play" || eventId == "start" || eventId == "trigger") { transform.SetPositionAndRotation(payload.Position, payload.Rotation); Play(); return true; }
            if (eventId == "stop" || eventId == "cancel") { Stop(VfxStopMode.AllowTail); return true; }
            if (eventId == "reset") { ResetForPool(); return true; }
            const string prefix = "gate:"; if (eventId != null && eventId.StartsWith(prefix, StringComparison.Ordinal)) return ReleaseGate(eventId.Substring(prefix.Length));
            return false;
        }
        public bool ReleaseGate(string externalEvent)
        {
            if (!waiting || nextGate >= gates.Length || !string.Equals(waitingGateId, externalEvent, StringComparison.Ordinal)) return false;
            releasedGates[nextGate] = true; nextGate++; waiting = false; waitingGateId = null; return true;
        }
        public void Stop(VfxStopMode mode)
        {
            playing = false; waiting = false; waitingGateId = null;
            if (entries != null) foreach (var entry in entries) if (entry != null) entry.Stop(mode);
            if (mode == VfxStopMode.Immediate && instances != null) foreach (var instance in instances) if (instance != null) instance.SetActive(false);
        }
        public void ResetForPool()
        {
            playing = false; waiting = false; elapsed = 0; nextEvent = nextHint = nextGate = triggeredEvents = 0; waitingGateId = null; playbackRate = 1f;
            if (entries != null) foreach (var entry in entries) if (entry != null) entry.ResetForPool();
            if (instances != null) foreach (var instance in instances) if (instance != null) instance.SetActive(false);
        }
        public void ReleaseInstances()
        {
            ResetForPool(); if (instances != null) foreach (var instance in instances) if (instance != null) Destroy(instance); instances = new GameObject[0]; entries = new IVfxRuntimeEntry[0];
        }

        private void EnsureInstances()
        {
            if (instances != null && instances.Length == sourcePrefabs.Length && instances.Length > 0) return;
            instances = new GameObject[sourcePrefabs.Length]; entries = new IVfxRuntimeEntry[sourcePrefabs.Length];
            for (var i = 0; i < sourcePrefabs.Length; i++)
            {
                var prefab = sourcePrefabs[i]; if (prefab == null) continue;
                var instance = Instantiate(prefab, transform); instance.name = "RuntimeSlot_" + (i + 1).ToString("00") + "_" + prefab.name; instance.SetActive(false); instances[i] = instance;
                var behaviours = instance.GetComponents<MonoBehaviour>(); foreach (var behaviour in behaviours) if (behaviour is IVfxRuntimeEntry) { entries[i] = (IVfxRuntimeEntry)behaviour; break; }
            }
        }
        private void ResetChildren()
        {
            if (instances == null) return; for (var i = 0; i < instances.Length; i++) { if (entries[i] != null) entries[i].ResetForPool(); if (instances[i] != null) instances[i].SetActive(false); }
        }
        private void RunDueEvents()
        {
            while (nextEvent < timeline.Length && timeline[nextEvent].Time <= elapsed + .0001f)
            {
                var item = timeline[nextEvent++]; triggeredEvents++; if (item.SourceIndex < 0 || item.SourceIndex >= instances.Length || instances[item.SourceIndex] == null) continue;
                var instance = instances[item.SourceIndex]; var entry = entries[item.SourceIndex];
                if (item.Action == "stop") { if (entry != null) entry.Stop(VfxStopMode.Immediate); instance.SetActive(false); continue; }
                instance.SetActive(true); instance.transform.localPosition = item.Position; instance.transform.localRotation = Quaternion.Euler(item.Rotation); instance.transform.localScale = Vector3.one * Mathf.Max(.01f, item.Scale);
                if (entry != null) { entry.Initialize(new VfxRuntimeContext(instance.transform.position, instance.transform.rotation)); entry.Play(); }
                if (item.HasPalette) { var tint = instance.GetComponent<CompositePaletteOverride>(); if (tint == null) tint = instance.AddComponent<CompositePaletteOverride>(); tint.Set(item.Primary, item.Secondary, item.Accent); }
            }
        }
        private void RunDueHints()
        {
            while (nextHint < cameraHints.Length && cameraHints[nextHint].Time <= elapsed + .0001f) { var item = cameraHints[nextHint++]; lastHintType = item.Type; lastHintStrength = item.Strength; hintSerial++; }
        }
    }

    /// <summary>Runtime-only palette MPB adapter added to instantiated child entries.</summary>
    public sealed class CompositePaletteOverride : MonoBehaviour
    {
        private Color primary, secondary, accent; private Renderer[] renderers; private MaterialPropertyBlock block;
        public void Set(Color p, Color s, Color a) { primary = p; secondary = s; accent = a; renderers = GetComponentsInChildren<Renderer>(true); Apply(); }
        private void LateUpdate() { Apply(); }
        private void Apply() { if (renderers == null) return; if (block == null) block = new MaterialPropertyBlock(); foreach (var renderer in renderers) if (renderer != null) { renderer.GetPropertyBlock(block); block.SetColor("_PrimaryColor", primary); block.SetColor("_SecondaryColor", secondary); block.SetColor("_AccentColor", accent); renderer.SetPropertyBlock(block); } }
    }
}

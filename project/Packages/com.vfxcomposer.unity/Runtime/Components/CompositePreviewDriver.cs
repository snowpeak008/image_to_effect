using UnityEngine;

namespace VFXComposer
{
    /// <summary>Preview-scene-only selector and camera-hint consumer; forbidden in Runtime Entries.</summary>
    [DisallowMultipleComponent]
    public sealed class CompositePreviewDriver : MonoBehaviour
    {
        [SerializeField] private CompositeVfxController[] entries = new CompositeVfxController[0];
        [SerializeField] private Camera reviewCamera;
        [SerializeField, Min(.1f)] private float replayDelay = .7f;
        private int index, seenHint; private float idle, gateAge, shakeAge; private Vector3 cameraBase; private float baseOrtho, baseFov;
        public int SelectedIndex { get { return index; } }
        public CompositeVfxController Current { get { return entries != null && entries.Length > 0 ? entries[Mathf.Clamp(index, 0, entries.Length - 1)] : null; } }
        private void OnEnable() { if (reviewCamera != null) { cameraBase = reviewCamera.transform.localPosition; baseOrtho = reviewCamera.orthographicSize; baseFov = reviewCamera.fieldOfView; } Select(0); }
        private void Update()
        {
            var current = Current; if (current == null) return;
            if (current.WaitingForGate) { gateAge += Time.deltaTime; if (gateAge >= .2f) { current.ReleaseGate(current.WaitingGateId); gateAge = 0; } }
            if (current.CameraHintSerial != seenHint) { seenHint = current.CameraHintSerial; ApplyHint(current.LastCameraHintType, current.LastCameraHintStrength); }
            if (shakeAge > 0 && reviewCamera != null) { shakeAge -= Time.deltaTime; reviewCamera.transform.localPosition = cameraBase + (Vector3)(Random.insideUnitCircle * (.04f * Mathf.Clamp01(shakeAge / .18f))); } else if (reviewCamera != null) reviewCamera.transform.localPosition = cameraBase;
            if (!current.IsAlive) { idle += Time.deltaTime; if (idle >= replayDelay) Select((index + 1) % entries.Length); }
        }
        public void Select(int value)
        {
            if (entries == null || entries.Length == 0) return; for (var i = 0; i < entries.Length; i++) if (entries[i] != null) entries[i].ResetForPool(); index = Mathf.Clamp(value, 0, entries.Length - 1); idle = gateAge = 0; seenHint = 0; var current = entries[index]; if (current != null) current.Play();
        }
        private void ApplyHint(string type, float strength)
        {
            var current = Current; if (type == "shake") shakeAge = .18f + strength * .18f;
            else if (type == "zoom" && reviewCamera != null) { if (reviewCamera.orthographic) reviewCamera.orthographicSize = baseOrtho * Mathf.Lerp(1f, .86f, strength); else reviewCamera.fieldOfView = baseFov * Mathf.Lerp(1f, .86f, strength); }
            else if (type == "slowmo" && current != null) current.PlaybackRate = Mathf.Lerp(1f, .35f, strength);
        }
    }
}

using System;
using UnityEngine;

namespace VFXComposer.W11W13NextCandidate
{
    /// <summary>Scene-only replay/selection harness. Production prefab audits forbid this component.</summary>
    [DisallowMultipleComponent]
    public sealed class W11W13NextCandidatePreviewDriver : MonoBehaviour
    {
        [SerializeField] private W11W13NextCandidateController[] entries = new W11W13NextCandidateController[0];
        [SerializeField] private Renderer[] hitTargets = new Renderer[0];
        [SerializeField] private Camera reviewCamera = null;
        [SerializeField] private bool sequential = false;
        [SerializeField, Min(.25f)] private float replaySeconds = 3.2f;
        [SerializeField, Min(.5f)] private float selectionSeconds = 8f;

        private float elapsed;
        private float selectionElapsed;
        private float gateWait;
        private int index;
        private Vector3 cameraOrigin;
        private float cameraSize;
        private float cameraFieldOfView;
        private bool cameraCaptured;
        private float shakeRemaining;
        private float shakeStrength;
        private float zoomRemaining;
        private float zoomStrength;
        private float slowmoRemaining;
        private float previousTimeScale = 1f;
        private bool ownsTimeScale;
        private int consumedZoomHints;
        private int consumedShakeHints;
        private int consumedSlowmoHints;

        public int CurrentIndex { get { return index; } }
        public int EntryCount { get { return entries == null ? 0 : entries.Length; } }
        public int ConsumedZoomHints { get { return consumedZoomHints; } }
        public int ConsumedShakeHints { get { return consumedShakeHints; } }
        public int ConsumedSlowmoHints { get { return consumedSlowmoHints; } }
        public W11W13NextCandidateController Current { get { return entries != null && entries.Length > 0 ? entries[Mathf.Clamp(index, 0, entries.Length - 1)] : null; } }

        private void Start()
        {
            if (reviewCamera != null) { cameraOrigin = reviewCamera.transform.position; cameraSize = reviewCamera.orthographicSize; cameraFieldOfView = reviewCamera.fieldOfView; cameraCaptured = true; }
            if (entries != null) foreach (var entry in entries) if (entry != null) entry.CameraHintRaised += ConsumeCameraHint;
            Select(0);
        }

        private void Update()
        {
            elapsed += Mathf.Max(0f, Time.deltaTime);
            selectionElapsed += Mathf.Max(0f, Time.deltaTime);
            if (sequential && selectionElapsed >= selectionSeconds) Select(index + 1);
            else if (!sequential && elapsed >= replaySeconds) ReplayAll();
            DriveProtocols();
            UpdateCameraEffects(Mathf.Max(0f, Time.unscaledDeltaTime));
        }

        public void Select(int requested)
        {
            if (entries == null || entries.Length == 0) return;
            RestoreTransientEffects();
            index = ((requested % entries.Length) + entries.Length) % entries.Length;
            elapsed = 0f; selectionElapsed = 0f; gateWait = 0f;
            for (var itemIndex = 0; itemIndex < entries.Length; itemIndex++)
            {
                var entry = entries[itemIndex]; if (entry == null) continue;
                var active = !sequential || itemIndex == index;
                entry.ResetForPool();
                entry.gameObject.SetActive(active);
                if (active) PrepareAndPlay(entry, itemIndex);
            }
        }

        private void ReplayAll()
        {
            elapsed = 0f;
            if (entries == null) return;
            for (var itemIndex = 0; itemIndex < entries.Length; itemIndex++)
            {
                var entry = entries[itemIndex];
                if (entry != null && entry.gameObject.activeInHierarchy) PrepareAndPlay(entry, itemIndex);
            }
        }

        private void PrepareAndPlay(W11W13NextCandidateController entry, int itemIndex)
        {
            if (entry.Variant == W11W13NextVariant.HitFlash && hitTargets != null && itemIndex < hitTargets.Length && hitTargets[itemIndex] != null) entry.BindExternalRenderers(new[] { hitTargets[itemIndex] });
            if (entry.Variant == W11W13NextVariant.LifestealLink)
            {
                var center = entry.transform.position;
                entry.SetWorldEndpoints(center + Vector3.left * .9f, center + Vector3.right * .9f);
            }
            entry.Play();
        }

        private void DriveProtocols()
        {
            if (entries == null) return;
            for (var itemIndex = 0; itemIndex < entries.Length; itemIndex++)
            {
                var entry = entries[itemIndex]; if (entry == null || !entry.gameObject.activeInHierarchy) continue;
                if (entry.Family == W11W13NextFamily.Environment)
                {
                    var triangle = 1f - Mathf.Abs(Mathf.Repeat(elapsed / Mathf.Max(.5f, replaySeconds), 1f) * 2f - 1f);
                    entry.SetIntensity(triangle);
                    entry.SetLayerDensities(Mathf.Lerp(.5f, 1f, triangle), Mathf.Lerp(.35f, 1f, triangle), Mathf.Lerp(.2f, 1f, triangle));
                    entry.SetWind(new Vector3(Mathf.Sin(elapsed * .45f) * 4.5f, 0f, Mathf.Cos(elapsed * .31f) * 1.2f));
                }
                else if (entry.Variant == W11W13NextVariant.ComboSurge)
                {
                    entry.SetStackLevel(1 + Mathf.FloorToInt(Mathf.Repeat(elapsed / .75f, 5f)));
                }
                else if (entry.Variant == W11W13NextVariant.LifestealLink)
                {
                    var center = entry.transform.position;
                    entry.SetWorldEndpoints(center + new Vector3(-.95f, .18f + Mathf.Sin(elapsed) * .08f, 0f), center + new Vector3(.95f, -.08f, 0f));
                }
                if (entry.WaitingForGate)
                {
                    gateWait += Time.deltaTime;
                    if (gateWait >= .65f) { entry.ReleaseGate(entry.WaitingGateId); gateWait = 0f; }
                }
            }
        }

        private void ConsumeCameraHint(W11W13CameraHint hint)
        {
            if (string.Equals(hint.Type, "zoom", StringComparison.OrdinalIgnoreCase))
            {
                consumedZoomHints++;
                zoomRemaining = .45f;
                zoomStrength = Mathf.Clamp01(hint.Strength);
            }
            else if (string.Equals(hint.Type, "shake", StringComparison.OrdinalIgnoreCase))
            {
                consumedShakeHints++;
                shakeRemaining = .42f;
                shakeStrength = Mathf.Clamp01(hint.Strength);
            }
            else if (string.Equals(hint.Type, "slowmo", StringComparison.OrdinalIgnoreCase))
            {
                consumedSlowmoHints++;
                slowmoRemaining = .48f;
                if (!ownsTimeScale) { previousTimeScale = Time.timeScale; ownsTimeScale = true; }
                Time.timeScale = Mathf.Min(previousTimeScale, Mathf.Lerp(.58f, .32f, Mathf.Clamp01(hint.Strength)));
            }
        }

        private void UpdateCameraEffects(float unscaledDelta)
        {
            shakeRemaining = Mathf.Max(0f, shakeRemaining - unscaledDelta);
            zoomRemaining = Mathf.Max(0f, zoomRemaining - unscaledDelta);
            slowmoRemaining = Mathf.Max(0f, slowmoRemaining - unscaledDelta);
            if (reviewCamera != null)
            {
                var shakeEnvelope = Mathf.Clamp01(shakeRemaining / .42f);
                var shake = Mathf.Sin(Time.unscaledTime * 57f) * .075f * shakeStrength * shakeEnvelope;
                reviewCamera.transform.position = cameraOrigin + new Vector3(shake, -shake * .43f, 0f);
                var zoomEnvelope = Mathf.Sin(Mathf.Clamp01(zoomRemaining / .45f) * Mathf.PI);
                var zoomFactor = 1f - zoomStrength * zoomEnvelope * .16f;
                if (reviewCamera.orthographic) reviewCamera.orthographicSize = cameraSize * zoomFactor;
                else reviewCamera.fieldOfView = cameraFieldOfView * zoomFactor;
            }
            if (ownsTimeScale && slowmoRemaining <= 0f) { Time.timeScale = previousTimeScale; ownsTimeScale = false; }
        }

        private void RestoreTransientEffects()
        {
            shakeRemaining = 0f; zoomRemaining = 0f; slowmoRemaining = 0f;
            if (reviewCamera != null && cameraCaptured) { reviewCamera.transform.position = cameraOrigin; reviewCamera.orthographicSize = cameraSize; reviewCamera.fieldOfView = cameraFieldOfView; }
            if (ownsTimeScale) { Time.timeScale = previousTimeScale; ownsTimeScale = false; }
        }

        private void OnDisable() { RestoreTransientEffects(); }

        private void OnDestroy()
        {
            RestoreTransientEffects();
            if (entries != null) foreach (var entry in entries) if (entry != null) entry.CameraHintRaised -= ConsumeCameraHint;
        }
    }
}

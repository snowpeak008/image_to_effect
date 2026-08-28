using System;
using System.Collections.Generic;
using UnityEngine;

namespace VFXComposer.W24
{
    /// <summary>Optional adapter boundary. URP Light2D implementations live outside this runtime assembly to avoid a hard package dependency. SetLight2D returns true only when the adapter is actively emitting after the call.</summary>
    public interface IW24Light2DAdapter
    {
        bool IsAdapterAvailable { get; }
        bool SetLight2D(bool enabled, float intensity);
    }

    public static class W24LightBudget
    {
        public static int SelectEnabledCount(int requested, int budget) { return Mathf.Clamp(requested, 0, Mathf.Max(0, budget)); }
    }

    [DisallowMultipleComponent]
    public sealed class W24RealLightingModule : MonoBehaviour, IW24SemanticTelemetrySource
    {
        [SerializeField] private Light[] lights3D = new Light[0];
        [SerializeField] private MonoBehaviour[] optionalLight2DAdapters = new MonoBehaviour[0];
        [SerializeField, Min(0)] private int maximum3DLights = 2;
        [SerializeField, Min(0)] private int maximum2DLights = 1;
        [SerializeField, Min(0f)] private float maximumIntensity = 2f;
        private int enabled3D;
        private int enabled2D;
        private int eventSerial;
        private float intensity;

        private void Awake() { ResetForPool(); }

        /// <summary>
        /// Player-safe dependency injection used by generated Runtime Entries and runtime
        /// evidence fixtures.  Reconfiguration always turns the previous and replacement
        /// lights off first, so a pooled object cannot leak illumination from an earlier owner.
        /// </summary>
        public void Configure3DLights(Light[] values, int budget)
        {
            SetLights(false, 0f);
            lights3D = values ?? new Light[0];
            maximum3DLights = Mathf.Max(0, budget);
            SetLights(false, 0f);
        }

        public void SetLights(bool enabled, float requestedIntensity)
        {
            intensity = Mathf.Clamp(requestedIntensity, 0f, maximumIntensity); enabled3D = 0; enabled2D = 0; eventSerial++;
            var unique3DCount = CountUnique3DLights(lights3D);
            var allowed = W24LightBudget.SelectEnabledCount(unique3DCount, maximum3DLights);
            var visited3D = new HashSet<int>();
            for (var index = 0; lights3D != null && index < lights3D.Length; index++)
            {
                var light = lights3D[index]; if (light == null) continue;
                if (!visited3D.Add(light.GetInstanceID())) continue;
                var turnOn = enabled && enabled3D < allowed && intensity > .0001f;
                light.enabled = turnOn; light.intensity = turnOn ? intensity : 0f;
                if (turnOn) enabled3D++;
            }
            var allowed2D = W24LightBudget.SelectEnabledCount(CountUnique2DAdapters(optionalLight2DAdapters), maximum2DLights);
            var visited2D = new HashSet<int>();
            if (optionalLight2DAdapters != null) foreach (var component in optionalLight2DAdapters)
            {
                var adapter = component as IW24Light2DAdapter; if (adapter == null || !adapter.IsAdapterAvailable) continue;
                if (!visited2D.Add(component.GetInstanceID())) continue;
                var turnOn = enabled && enabled2D < allowed2D && intensity > .0001f;
                if (adapter.SetLight2D(turnOn, turnOn ? intensity : 0f) && turnOn) enabled2D++;
            }
        }
        public void ResetForPool() { SetLights(false, 0f); }
        public W24SemanticTelemetry ReadSemanticTelemetry() { return new W24SemanticTelemetry { Module = "real_lighting", State = enabled3D + enabled2D > 0 ? W24SemanticState.Continuous : W24SemanticState.Idle, EventSerial = eventSerial, ActiveItemCount = enabled3D + enabled2D, CleanupComplete = enabled3D + enabled2D == 0, LastEventId = enabled3D + enabled2D > 0 ? "real_light_on" : "real_light_off" }; }
        private void OnDisable() { ResetForPool(); }

        private static int CountUnique3DLights(Light[] values)
        {
            var identities = new HashSet<int>();
            if (values != null) foreach (var value in values) if (value != null) identities.Add(value.GetInstanceID());
            return identities.Count;
        }

        private static int CountUnique2DAdapters(MonoBehaviour[] values)
        {
            var identities = new HashSet<int>();
            if (values != null) foreach (var value in values)
                if (value != null && value is IW24Light2DAdapter && ((IW24Light2DAdapter)value).IsAdapterAvailable)
                    identities.Add(value.GetInstanceID());
            return identities.Count;
        }
    }
}

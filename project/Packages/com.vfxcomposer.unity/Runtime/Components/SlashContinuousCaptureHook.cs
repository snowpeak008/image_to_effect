using System;
using UnityEngine;

namespace VFXComposer
{
    /// <summary>Runtime-only after-frame signal used by the explicit S14 recorder. It observes normal player Update and never advances the effect.</summary>
    [DisallowMultipleComponent]
    public sealed class SlashContinuousCaptureHook : MonoBehaviour
    {
        public static Action<int, float> AfterFrame;
        [SerializeField] private SlashEffectController controller;
        public SlashEffectController Controller { get { return controller; } set { controller = value; } }
        private void LateUpdate() { if (controller != null) AfterFrame?.Invoke(Time.frameCount, controller.Elapsed); }
    }
}

using UnityEngine;

namespace VFXComposer
{
    /// <summary>Optional player-safe preview helper. It invokes only SlashEffectController's visual API and has no gameplay role.</summary>
    [DisallowMultipleComponent]
    public sealed class SlashPreviewPlaybackDriver : MonoBehaviour
    {
        [SerializeField] private SlashEffectController controller;
        [SerializeField] private bool replay = true;
        private float nextReplay;
        public SlashEffectController Controller { get { return controller; } set { controller = value; } }
        private void OnEnable() { Play(); }
        private void Update() { if (replay && controller != null && !controller.IsPlaying && Time.time >= nextReplay) Play(); }
        public void Play() { if (controller == null) return; controller.PlaySlash(controller.transform.position, controller.transform.rotation); nextReplay = Time.time + controller.TimelineDuration + .25f; }
    }
}

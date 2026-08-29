using System.Linq;
using UnityEngine;

namespace VFXComposer.W17W18NextCandidate
{
    /// <summary>Preview-only deterministic replay and clean-gap scheduler.</summary>
    [DisallowMultipleComponent]
    public sealed class W17W18NextCandidatePreviewDriver : MonoBehaviour
    {
        [SerializeField] private string compilerVersion = string.Empty;
        [SerializeField] private W17UiInteractionController[] uiEntries = new W17UiInteractionController[0];
        [SerializeField] private W18CharacterThemeController[] themeEntries = new W18CharacterThemeController[0];
        [SerializeField, Min(.2f)] private float playDuration = 8f;
        [SerializeField, Min(.04f)] private float cleanGap = .35f;

        private float elapsed;
        private bool inCleanGap;
        private int replayCount;

        public string CompilerVersion { get { return compilerVersion; } }
        public int UiEntryCount { get { return uiEntries == null ? 0 : uiEntries.Length; } }
        public int ThemeEntryCount { get { return themeEntries == null ? 0 : themeEntries.Length; } }
        public int ReplayCount { get { return replayCount; } }
        public bool InCleanGap { get { return inCleanGap; } }
        public bool AllEntriesIdle
        {
            get
            {
                return (uiEntries == null || uiEntries.All(value => value == null || !value.IsAlive)) &&
                       (themeEntries == null || themeEntries.All(value => value == null || !value.IsAlive));
            }
        }

        private void Start()
        {
            BeginReplay();
        }

        private void Update()
        {
            elapsed += Mathf.Max(0f, Time.deltaTime);
            if (!inCleanGap)
            {
                DriveInteractiveProtocols();
                if (elapsed >= playDuration) EnterCleanGap();
            }
            else if (elapsed >= cleanGap) BeginReplay();
        }

        public void BeginReplay()
        {
            elapsed = 0f;
            inCleanGap = false;
            replayCount++;
            if (uiEntries != null)
            {
                foreach (var entry in uiEntries)
                {
                    if (entry == null) continue;
                    entry.ResetForPool();
                    entry.Play();
                }
            }
            if (themeEntries != null)
            {
                foreach (var entry in themeEntries)
                {
                    if (entry == null) continue;
                    entry.ResetForPool();
                    entry.Play();
                }
            }
            DriveInteractiveProtocols();
        }

        public void EnterCleanGap()
        {
            elapsed = 0f;
            inCleanGap = true;
            if (uiEntries != null) foreach (var entry in uiEntries) if (entry != null) entry.Stop(VfxStopMode.Immediate);
            if (themeEntries != null) foreach (var entry in themeEntries) if (entry != null) entry.Stop(VfxStopMode.Immediate);
        }

        private void DriveInteractiveProtocols()
        {
            if (uiEntries == null) return;
            var phase = Mathf.Clamp01(elapsed / Mathf.Max(.2f, playDuration));
            foreach (var entry in uiEntries)
            {
                if (entry == null) continue;
                if (!entry.IsAlive) entry.Play();
                if (entry.Kind == W17UiEffectKind.ProgressCharge) entry.SetFillRatio(Mathf.Clamp01(phase * 1.25f));
                if (entry.Kind == W17UiEffectKind.GachaSingle) entry.SetRarity(5);
                if (entry.Kind == W17UiEffectKind.CardFlip) entry.SetRarity(4);
            }
        }
    }
}

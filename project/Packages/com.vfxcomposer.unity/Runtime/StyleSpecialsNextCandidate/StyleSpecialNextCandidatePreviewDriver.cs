using UnityEngine;

namespace VFXComposer
{
    /// <summary>Preview-only deterministic replay scheduler; gameplay prefabs never depend on it.</summary>
    [DisallowMultipleComponent]
    public sealed class StyleSpecialNextCandidatePreviewDriver : MonoBehaviour
    {
        [SerializeField] private StyleSpecialCandidateGroup group;
        [SerializeField] private StyleSpecialNextCandidateRuntimeEntry[] runtimeEntries = new StyleSpecialNextCandidateRuntimeEntry[0];
        [SerializeField, Min(.2f)] private float playDuration = 2.35f;
        [SerializeField, Min(.05f)] private float cleanGap = .28f;
        [SerializeField] private string compilerVersion = string.Empty;
        [SerializeField] private string candidateSignature = string.Empty;

        private float elapsed;
        private bool inCleanGap;
        private int replayCount;
        private int reviewViewpointIndex;

        public StyleSpecialCandidateGroup Group { get { return group; } }
        public int ConfiguredEntryCount { get { return runtimeEntries == null ? 0 : runtimeEntries.Length; } }
        public int ReplayCount { get { return replayCount; } }
        public int ReviewViewpointIndex { get { return reviewViewpointIndex; } }
        public bool InCleanGap { get { return inCleanGap; } }
        public string CompilerVersion { get { return compilerVersion; } }
        public string CandidateSignature { get { return candidateSignature; } }

        private void Start()
        {
            BeginReplay();
        }

        private void Update()
        {
            elapsed += Mathf.Max(0f, Time.deltaTime);
            if (!inCleanGap && elapsed >= playDuration)
            {
                StopAll();
                elapsed = 0f;
                inCleanGap = true;
            }
            else if (inCleanGap && elapsed >= cleanGap) BeginReplay();
        }

        public void BeginReplay()
        {
            elapsed = 0f;
            inCleanGap = false;
            replayCount++;
            if (group == StyleSpecialCandidateGroup.W10Style3D) reviewViewpointIndex = (replayCount - 1) & 1;
            else reviewViewpointIndex = 0;
            if (runtimeEntries == null) return;
            for (var index = 0; index < runtimeEntries.Length; index++)
            {
                var entry = runtimeEntries[index];
                if (entry == null) continue;
                if (group == StyleSpecialCandidateGroup.W10Style3D) entry.transform.localRotation = reviewViewpointIndex == 0 ? Quaternion.identity : Quaternion.Euler(17f, -18f, 0f);
                entry.ResetForPool();
                entry.Play();
            }
        }

        public void StopAll()
        {
            if (runtimeEntries == null) return;
            for (var index = 0; index < runtimeEntries.Length; index++) if (runtimeEntries[index] != null) runtimeEntries[index].Stop(VfxStopMode.Immediate);
        }

        private void OnDisable()
        {
            StopAll();
        }
    }
}

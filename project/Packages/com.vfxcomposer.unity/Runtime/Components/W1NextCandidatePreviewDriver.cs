using UnityEngine;

namespace VFXComposer
{
    /// <summary>Preview-only replay scheduler. It never creates or owns gameplay state.</summary>
    [DisallowMultipleComponent]
    public sealed class W1NextCandidatePreviewDriver : MonoBehaviour
    {
        public const string ScenePath = "Assets/VFX/Preview/VFXPREVIEW_W1_StyleSamples_NextCandidate.unity";

        [SerializeField] private W1NextCandidateRuntimeEntry[] runtimeEntries = new W1NextCandidateRuntimeEntry[0];
        [SerializeField, Min(.1f)] private float playDuration = 2.05f;
        [SerializeField, Min(.05f)] private float cleanGap = .3f;
        [SerializeField] private string compilerVersion = string.Empty;
        [SerializeField] private string candidateSignature = string.Empty;

        private float elapsed;
        private bool inCleanGap;
        private int replayCount;

        public int ConfiguredEntryCount { get { return runtimeEntries == null ? 0 : runtimeEntries.Length; } }
        public int ReplayCount { get { return replayCount; } }
        public bool InCleanGap { get { return inCleanGap; } }
        public string CompilerVersion { get { return compilerVersion; } }
        public string CandidateSignature { get { return candidateSignature; } }
        public bool AllEntriesIdle
        {
            get
            {
                if (runtimeEntries == null) return true;
                for (var index = 0; index < runtimeEntries.Length; index++)
                {
                    var entry = runtimeEntries[index];
                    if (entry != null && (entry.IsAlive || entry.VisibleRendererCount != 0)) return false;
                }
                return true;
            }
        }

        private void Start()
        {
            BeginReplay();
        }

        private void Update()
        {
            elapsed += Mathf.Max(0f, Time.deltaTime);
            if (!inCleanGap && elapsed >= playDuration)
            {
                EnterCleanGap();
                return;
            }
            if (inCleanGap && elapsed >= cleanGap) BeginReplay();
        }

        private void OnDisable()
        {
            ResetAll();
        }

        public void BeginReplay()
        {
            ResetAll();
            inCleanGap = false;
            elapsed = 0f;
            replayCount++;
            if (runtimeEntries == null) return;
            for (var index = 0; index < runtimeEntries.Length; index++) if (runtimeEntries[index] != null) runtimeEntries[index].Play();
        }

        public void EnterCleanGap()
        {
            ResetAll();
            inCleanGap = true;
            elapsed = 0f;
        }

        private void ResetAll()
        {
            if (runtimeEntries == null) return;
            for (var index = 0; index < runtimeEntries.Length; index++)
            {
                var entry = runtimeEntries[index];
                if (entry == null) continue;
                entry.Stop(VfxStopMode.Immediate);
                entry.ResetForPool();
            }
        }
    }
}

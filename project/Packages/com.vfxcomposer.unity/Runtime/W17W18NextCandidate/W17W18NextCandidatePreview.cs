using UnityEngine;
using UnityEngine.UI;

namespace VFXComposer.W17W18NextCandidate
{
    public enum W17W18PreviewFamily { W17Ui, W18Theme }

    /// <summary>Serialized machine-readable Preview cell boundary; it carries no acceptance verdict.</summary>
    [DisallowMultipleComponent]
    public sealed class W17W18NextCandidateCell : MonoBehaviour
    {
        [SerializeField] private W17W18PreviewFamily family;
        [SerializeField] private int cellIndex;
        [SerializeField] private string candidateId;
        [SerializeField] private Rect normalizedViewport;
        [SerializeField] private Rect worldClipRect;
        [SerializeField] private W17UiInteractionController uiEntry;
        [SerializeField] private W18CharacterThemeController themeEntry;
        [SerializeField] private RectMask2D canvasClip;

        public W17W18PreviewFamily Family { get { return family; } }
        public int CellIndex { get { return cellIndex; } }
        public string CandidateId { get { return candidateId; } }
        public Rect NormalizedViewport { get { return normalizedViewport; } }
        public Rect WorldClipRect { get { return worldClipRect; } }
        public W17UiInteractionController UiEntry { get { return uiEntry; } }
        public W18CharacterThemeController ThemeEntry { get { return themeEntry; } }
        public bool UsesRealHardClip
        {
            get
            {
                if (family == W17W18PreviewFamily.W17Ui) return uiEntry != null && canvasClip != null && canvasClip.enabled && uiEntry.HasHardClip;
                return themeEntry != null && themeEntry.PreviewHardClip && themeEntry.UsesHardClipShader();
            }
        }
    }
}

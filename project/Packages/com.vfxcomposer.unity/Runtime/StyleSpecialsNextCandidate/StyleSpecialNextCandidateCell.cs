using UnityEngine;

namespace VFXComposer
{
    /// <summary>Serialized hard-clipped review-cell contract shared by the three new Preview scenes.</summary>
    [DisallowMultipleComponent]
    public sealed class StyleSpecialNextCandidateCell : MonoBehaviour
    {
        [SerializeField, Min(1)] private int cellIndex = 1;
        [SerializeField] private StyleSpecialCandidateGroup group;
        [SerializeField] private string label = string.Empty;
        [SerializeField] private string pairFamily = string.Empty;
        [SerializeField] private string pairRole = string.Empty;
        [SerializeField] private Rect fullViewport;
        [SerializeField] private Rect effectViewport;
        [SerializeField] private Rect labelViewport;
        [SerializeField] private int isolatedLayer;
        [SerializeField] private Camera effectCamera;
        [SerializeField] private StyleSpecialNextCandidateRuntimeEntry runtimeEntry;

        public int CellIndex { get { return cellIndex; } }
        public StyleSpecialCandidateGroup Group { get { return group; } }
        public string Label { get { return label; } }
        public string PairFamily { get { return pairFamily; } }
        public string PairRole { get { return pairRole; } }
        public Rect FullViewport { get { return fullViewport; } }
        public Rect EffectViewport { get { return effectViewport; } }
        public Rect LabelViewport { get { return labelViewport; } }
        public int IsolatedLayer { get { return isolatedLayer; } }
        public Camera EffectCamera { get { return effectCamera; } }
        public StyleSpecialNextCandidateRuntimeEntry RuntimeEntry { get { return runtimeEntry; } }
        public bool EffectAndLabelAreDisjoint { get { return !effectViewport.Overlaps(labelViewport); } }
        public bool UsesExclusiveCullingMask { get { return effectCamera != null && effectCamera.cullingMask == 1 << isolatedLayer; } }
    }
}

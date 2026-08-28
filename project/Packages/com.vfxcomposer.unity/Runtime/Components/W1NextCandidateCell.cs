using UnityEngine;

namespace VFXComposer
{
    /// <summary>Serialized Preview contract for a hard-clipped W1 comparison cell.</summary>
    [DisallowMultipleComponent]
    public sealed class W1NextCandidateCell : MonoBehaviour
    {
        [SerializeField, Min(1)] private int cellIndex = 1;
        [SerializeField] private string label = string.Empty;
        [SerializeField] private string styleToken = string.Empty;
        [SerializeField] private Rect fullViewport;
        [SerializeField] private Rect effectViewport;
        [SerializeField] private Rect labelViewport;
        [SerializeField] private int isolatedLayer;
        [SerializeField] private Camera effectCamera;
        [SerializeField] private W1NextCandidateRuntimeEntry runtimeEntry;

        public int CellIndex { get { return cellIndex; } }
        public string Label { get { return label; } }
        public string StyleToken { get { return styleToken; } }
        public Rect FullViewport { get { return fullViewport; } }
        public Rect EffectViewport { get { return effectViewport; } }
        public Rect LabelViewport { get { return labelViewport; } }
        public int IsolatedLayer { get { return isolatedLayer; } }
        public Camera EffectCamera { get { return effectCamera; } }
        public W1NextCandidateRuntimeEntry RuntimeEntry { get { return runtimeEntry; } }
        public bool EffectAndLabelAreDisjoint { get { return !EffectViewport.Overlaps(LabelViewport); } }
        public bool UsesExclusiveCullingMask { get { return effectCamera != null && effectCamera.cullingMask == 1 << isolatedLayer; } }
    }
}

using UnityEngine;

namespace VFXComposer
{
    /// <summary>
    /// Serialized preview-cell contract.  It makes the effect, label and full cell envelopes
    /// independently inspectable instead of relying on a screenshot to reveal overlap.
    /// Rect values are local to the cell root.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ElementNextCandidateCell : MonoBehaviour
    {
        [SerializeField] private int cellIndex;
        [SerializeField] private string effectId = string.Empty;
        [SerializeField] private Rect fullBounds = new Rect(-1.55f, -1.12f, 3.1f, 2.24f);
        [SerializeField] private Rect effectBounds = new Rect(-1.34f, -.58f, 2.68f, 1.63f);
        [SerializeField] private Rect labelBounds = new Rect(-1.43f, -1.08f, 2.86f, .33f);
        [SerializeField] private float authoredDisplayScale = 1f;
        [SerializeField] private float compiledLocalExtent = 1f;
        [SerializeField] private ElementNextCandidateVisualExecutor entry;

        public int CellIndex { get { return cellIndex; } }
        public string EffectId { get { return effectId; } }
        public Rect FullBounds { get { return fullBounds; } }
        public Rect EffectBounds { get { return effectBounds; } }
        public Rect LabelBounds { get { return labelBounds; } }
        public float AuthoredDisplayScale { get { return authoredDisplayScale; } }
        public float CompiledLocalExtent { get { return compiledLocalExtent; } }
        public ElementNextCandidateVisualExecutor Entry { get { return entry; } }
        public bool EffectAndLabelAreDisjoint { get { return !effectBounds.Overlaps(labelBounds); } }
        public bool ScaledEnvelopeFitsEffectBounds
        {
            get
            {
                var half = Mathf.Max(.001f, compiledLocalExtent * authoredDisplayScale);
                return half <= effectBounds.width * .5f + .0001f && half <= effectBounds.height * .5f + .0001f;
            }
        }

        public bool ContainsEffectPoint(Vector2 localPoint) { return effectBounds.Contains(localPoint); }
        public bool ContainsLabelPoint(Vector2 localPoint) { return labelBounds.Contains(localPoint); }
    }
}


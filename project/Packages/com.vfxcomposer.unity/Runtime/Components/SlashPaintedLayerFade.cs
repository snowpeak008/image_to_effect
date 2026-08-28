using UnityEngine;

namespace VFXComposer
{
    /// <summary>Runtime MPB controller for a subordinate local painted crescent layer; no meshes/materials are created.</summary>
    [DisallowMultipleComponent]
    public sealed class SlashPaintedLayerFade : MonoBehaviour
    {
        private static readonly int RevealId = Shader.PropertyToID("_Reveal");
        private static readonly int DissolveId = Shader.PropertyToID("_Dissolve");
        private static readonly int ColorId = Shader.PropertyToID("_BaseColor");
        [SerializeField] private Color tint = new Color(1f, .24f, .05f, .42f);
        [SerializeField, Range(.1f, 12f)] private float revealRate = 7f;
        [SerializeField, Range(0f, 1f)] private float dissolveStart = .38f;
        [SerializeField, Range(0f, 1f)] private float dissolveEnd = .92f;

        public void Configure(Color value, float reveal, float start, float end) { tint = value; revealRate = reveal; dissolveStart = start; dissolveEnd = end; }
        public void SetPhaseProgress(float progress)
        {
            var reveal = Mathf.Clamp01(progress * revealRate); var dissolve = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(dissolveStart, dissolveEnd, progress));
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                for (var index = 0; index < materials.Length; index++)
                {
                    if (materials[index] == null || !materials[index].HasProperty(RevealId)) continue;
                    var block = new MaterialPropertyBlock(); renderer.GetPropertyBlock(block, index); block.SetFloat(RevealId, reveal); block.SetFloat(DissolveId, dissolve); block.SetColor(ColorId, tint); renderer.SetPropertyBlock(block, index);
                }
            }
        }
    }
}

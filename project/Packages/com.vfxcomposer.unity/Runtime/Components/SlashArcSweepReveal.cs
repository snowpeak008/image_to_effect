using UnityEngine;

namespace VFXComposer
{
    /// <summary>Runtime-only material-property reveal for the authored primary arc UV direction; it never creates meshes or materials.</summary>
    [DisallowMultipleComponent]
    public sealed class SlashArcSweepReveal : MonoBehaviour
    {
        private static readonly int RevealId = Shader.PropertyToID("_Reveal");
        private static readonly int DissolveId = Shader.PropertyToID("_Dissolve");
        [SerializeField, Range(0f, 1f)] private float reveal;
        public float Reveal { get { return reveal; } }

        private void OnEnable() { SetReveal(reveal); }

        public void SetReveal(float value)
        {
            reveal = Mathf.Clamp01(value);
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                for (var index = 0; index < materials.Length; index++)
                {
                    if (materials[index] == null || !materials[index].HasProperty(RevealId)) continue;
                    var block = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(block, index);
                    block.SetFloat(RevealId, reveal);
                    if (materials[index].HasProperty(DissolveId)) block.SetFloat(DissolveId, Mathf.SmoothStep(0f, .34f, Mathf.InverseLerp(.72f, 1f, reveal)));
                    renderer.SetPropertyBlock(block, index);
                }
            }
        }
    }
}

using UnityEngine;

namespace VFXComposer
{
    /// <summary>Serialized by the Slash compiler so alpha survives Prefab reload without cloning shared materials.</summary>
    [DisallowMultipleComponent]
    public sealed class SlashAfterimageAlpha : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] private float alpha = .32f;
        public float Alpha { get { return alpha; } set { alpha = Mathf.Clamp01(value); Apply(); } }
        private void Awake() { Apply(); }
        private void OnEnable() { Apply(); }
        private void Apply()
        {
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                var material = renderer.sharedMaterial; if (material == null) continue; var color = material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") : material.color; color.a = alpha; var block = new MaterialPropertyBlock(); renderer.GetPropertyBlock(block); if (material.HasProperty("_BaseColor")) block.SetColor("_BaseColor", color); else block.SetColor("_Color", color); renderer.SetPropertyBlock(block);
            }
        }
    }
}

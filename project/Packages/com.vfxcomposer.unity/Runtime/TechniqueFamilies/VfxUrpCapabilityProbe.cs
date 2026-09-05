using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace VFXComposer.TechniqueFamilies
{
    /// <summary>The refraction route actually taken at runtime.</summary>
    public enum VfxRefractionRoute { SceneColor, NormalPerturbation }

    /// <summary>
    /// Runtime URP capability probe (TECH_FAMILY_SPEC_MATERIAL.md section 6.2,
    /// ADR-010 section 4bis-1). Detects supportsCameraOpaqueTexture on the
    /// active pipeline asset once in Awake and switches the local
    /// _REFRACT_SCENECOLOR keyword on the per-prefab cloned materials
    /// (option B: one material, multi_compile_local, both variants kept in
    /// player builds). Camera-level overrides are deliberately not probed:
    /// per spec, a user turning the camera copy off is a legal degraded
    /// outcome, not an error.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VfxUrpCapabilityProbe : MonoBehaviour
    {
        private const string RefractKeyword = "_REFRACT_SCENECOLOR";

        [SerializeField] private Renderer[] refractionRenderers = new Renderer[0];

        public VfxRefractionRoute ActiveRoute { get; private set; } = VfxRefractionRoute.NormalPerturbation;

        public Renderer[] RefractionRenderers { get { return refractionRenderers; } }

        public void Configure(Renderer[] renderers)
        {
            refractionRenderers = renderers ?? new Renderer[0];
        }

        private void Awake()
        {
            bool opaqueAvailable = DetectOpaqueTexture();
            ApplyRefractionRoute(opaqueAvailable);
        }

        public static bool DetectOpaqueTexture()
        {
            RenderPipelineAsset asset = QualitySettings.renderPipeline != null
                ? QualitySettings.renderPipeline
                : GraphicsSettings.defaultRenderPipeline;
            var urp = asset as UniversalRenderPipelineAsset;
            return urp != null && urp.supportsCameraOpaqueTexture;
        }

        public void ApplyRefractionRoute(bool sceneColorAvailable)
        {
            ActiveRoute = sceneColorAvailable ? VfxRefractionRoute.SceneColor : VfxRefractionRoute.NormalPerturbation;
            for (int i = 0; i < refractionRenderers.Length; i++)
            {
                Renderer r = refractionRenderers[i];
                if (r == null) continue;
                // The compiler clones materials per prefab, so instances share
                // one material and one URP configuration; keyword flips are safe.
                Material[] mats = Application.isPlaying ? r.materials : r.sharedMaterials;
                for (int m = 0; m < mats.Length; m++)
                {
                    if (mats[m] == null) continue;
                    if (sceneColorAvailable) mats[m].EnableKeyword(RefractKeyword);
                    else mats[m].DisableKeyword(RefractKeyword);
                }
            }
        }
    }
}

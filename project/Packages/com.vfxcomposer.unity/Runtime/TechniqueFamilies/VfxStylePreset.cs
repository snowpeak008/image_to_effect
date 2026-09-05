using UnityEngine;

namespace VFXComposer.TechniqueFamilies
{
    public enum VfxStyleId { None = 0, Cartoon = 1, Pixel = 2 }

    /// <summary>
    /// Style preset (STYLE_IMPL_CARTOON_PIXEL.md / recipe-v2 style block).
    /// style.id selects the StyleStage keyword at compile time and never enters
    /// the parameter merge chain; this asset carries the per-style parameter
    /// defaults that are written into the material properties. T2b samples:
    /// cartoon + pixel.
    /// </summary>
    [CreateAssetMenu(menuName = "VFXComposer/Style Preset", fileName = "StylePreset")]
    public sealed class VfxStylePreset : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private VfxStyleId styleId = VfxStyleId.None;

        [Header("Cartoon parameters")]
        [SerializeField, Range(2, 4)] private int shadingSteps = 3;
        [SerializeField, Range(0f, 1f)] private float edgeSharpness = 0.85f;
        [SerializeField, Range(0f, 0.1f)] private float outlineWidth = 0.02f;
        [SerializeField, ColorUsage(false, false)] private Color outlineColor = new Color(0.1f, 0.1f, 0.1f, 1f);
        [SerializeField, Range(0.5f, 2f)] private float saturationMul = 1.3f;
        [SerializeField, Range(0.5f, 8f)] private float hdrClamp = 1.5f;
        [SerializeField, Range(0f, 1f)] private float detailMul = 1f;
        [SerializeField] private bool innerLine;

        [Header("Pixel parameters")]
        [SerializeField, Range(0.001f, 1f)] private float pixelSize = 0.0625f;
        [SerializeField, Range(3, 8)] private int colorSteps = 5;
        [SerializeField, Range(0f, 1f)] private float alphaCutoff = 0.5f;
        [SerializeField, Range(0, 4)] private int ditherLevels = 2;
        [SerializeField, Range(8f, 15f)] private float frameRate = 12f;
        [SerializeField] private bool outline1px = true;

        [Header("Light constraints (MESH_LIGHT section 6.9)")]
        [SerializeField, Range(0, 8)] private int lightIntensitySteps;
        [SerializeField] private bool lightFlickerQuantize;
        [SerializeField, Range(0f, 30f)] private float lightBeatFrameRate;
        [SerializeField] private bool allowShadows = true;

        public VfxStyleId StyleId { get { return styleId; } }
        public int ShadingSteps { get { return shadingSteps; } }
        public float PixelSize { get { return pixelSize; } }
        public int ColorSteps { get { return colorSteps; } }
        public float FrameRate { get { return frameRate; } }
        public int LightIntensitySteps { get { return lightIntensitySteps; } }
        public bool LightFlickerQuantize { get { return lightFlickerQuantize; } }
        public float LightBeatFrameRate { get { return lightBeatFrameRate; } }
        public bool AllowShadows { get { return allowShadows; } }

        public const string KeywordCartoon = "_STYLESTAGE_CARTOON";
        public const string KeywordPixel = "_STYLESTAGE_PIXEL";

        /// <summary>
        /// Compile-time application: sets exactly one StyleStage keyword state
        /// and writes the style parameter surface into the material.
        /// </summary>
        public void ApplyToMaterial(Material material)
        {
            if (material == null) return;
            switch (styleId)
            {
                case VfxStyleId.Cartoon:
                    material.EnableKeyword(KeywordCartoon);
                    material.DisableKeyword(KeywordPixel);
                    break;
                case VfxStyleId.Pixel:
                    material.DisableKeyword(KeywordCartoon);
                    material.EnableKeyword(KeywordPixel);
                    break;
                default:
                    material.DisableKeyword(KeywordCartoon);
                    material.DisableKeyword(KeywordPixel);
                    break;
            }
            SetIfPresent(material, "_ShadingSteps", shadingSteps);
            SetIfPresent(material, "_EdgeSharpness", edgeSharpness);
            SetIfPresent(material, "_OutlineWidth", outlineWidth);
            if (material.HasProperty("_OutlineColor")) material.SetColor("_OutlineColor", outlineColor);
            SetIfPresent(material, "_SaturationMul", saturationMul);
            SetIfPresent(material, "_HdrClamp", hdrClamp);
            SetIfPresent(material, "_DetailMul", detailMul);
            SetIfPresent(material, "_InnerLine", innerLine ? 1f : 0f);
            SetIfPresent(material, "_PixelSize", pixelSize);
            SetIfPresent(material, "_ColorSteps", colorSteps);
            SetIfPresent(material, "_AlphaCutoff", alphaCutoff);
            SetIfPresent(material, "_DitherLevels", ditherLevels);
            SetIfPresent(material, "_StyleFrameRate", frameRate);
            SetIfPresent(material, "_Outline1px", outline1px ? 1f : 0f);
        }

        /// <summary>Applies the style's light constraints to a beat driver.</summary>
        public void ApplyToLightBeat(VfxLightBeat beat)
        {
            if (beat == null) return;
            beat.ConfigureStyle(lightIntensitySteps, lightFlickerQuantize, lightBeatFrameRate, saturationMul);
        }

        public bool Validate(out string error)
        {
            if (styleId == VfxStyleId.Pixel && allowShadows)
            {
                error = "LT-7: pixel style must not allow shadows (STYLE_CATALOG_v1 section 3.5)";
                return false;
            }
            if (styleId == VfxStyleId.Pixel && pixelSize <= 0f)
            {
                error = "pixel style requires pixelSize > 0";
                return false;
            }
            error = null;
            return true;
        }

        private static void SetIfPresent(Material material, string property, float value)
        {
            if (material.HasProperty(property)) material.SetFloat(property, value);
        }
    }
}

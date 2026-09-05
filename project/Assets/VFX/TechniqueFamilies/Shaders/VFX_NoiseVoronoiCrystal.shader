// Variant: noise_voronoi_crystal. Voronoi cell lattice with facet contrast,
// interior scatter and a bindable crystal-growth progress (threshold advances
// cell by cell from the seed point).
Shader "VFXComposer/TechniqueFamilies/NoiseVoronoiCrystal"
{
    Properties
    {
        [HDR] _ColorPrimary("Palette Primary", Color) = (0.55, 0.85, 1, 1)
        [HDR] _ColorSecondary("Palette Secondary", Color) = (0.85, 0.95, 1, 1)
        [HDR] _ColorHot("Palette Hot", Color) = (1.6, 2, 2.4, 1)
        [HDR] _ColorCool("Palette Cool", Color) = (0.15, 0.35, 0.6, 1)
        [HDR] _ColorResidue("Palette Residue", Color) = (0.8, 0.9, 0.95, 1)
        _Intensity("Intensity", Range(0, 2)) = 1
        _Speed("Speed", Range(0.25, 3)) = 1
        _Seed("Seed", Float) = 0
        _CellDensity("Cell Density", Range(2, 24)) = 8
        _FacetContrast("Facet Contrast", Range(0, 1)) = 0.7
        _InteriorScatter("Interior Scatter", Range(0, 1)) = 0.35
        _GrowthProgress("Growth Progress", Range(0, 1)) = 1
        _ShadingSteps("Style Shading Steps", Float) = 3
        _EdgeSharpness("Style Edge Sharpness", Range(0, 1)) = 0.85
        _OutlineWidth("Style Outline Width", Float) = 0
        _OutlineColor("Style Outline Color", Color) = (0.1, 0.1, 0.1, 1)
        _SaturationMul("Style Saturation Mul", Float) = 1.3
        _HdrClamp("Style HDR Clamp", Float) = 1.5
        _DetailMul("Style Detail Mul", Range(0, 1)) = 1
        _InnerLine("Style Inner Line", Float) = 0
        _PixelSize("Style Pixel Size", Float) = 0.0625
        _ColorSteps("Style Color Steps", Float) = 5
        _AlphaCutoff("Style Alpha Cutoff", Range(0, 1)) = 0.5
        _DitherLevels("Style Dither Levels", Float) = 2
        _StyleFrameRate("Style Frame Rate", Float) = 12
        _Outline1px("Style Outline 1px", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            Name "Forward"
            HLSLPROGRAM
            #pragma vertex VfxVert
            #pragma fragment Frag
            #pragma multi_compile_local _ _STYLESTAGE_CARTOON _STYLESTAGE_PIXEL
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Includes/VfxCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
            VFX_COMMON_UNIFORMS
            float _CellDensity;
            float _FacetContrast;
            float _InteriorScatter;
            float _GrowthProgress;
            CBUFFER_END

            #include "Includes/VfxStyleStage.hlsl"

            // Returns: x = distance to nearest feature point, y = cell id hash, z = edge distance.
            float3 VfxVoronoi(float2 p, float seed)
            {
                float2 cell = floor(p);
                float2 f = frac(p);
                float minDist = 8.0;
                float secondDist = 8.0;
                float cellId = 0.0;
                for (int y = -1; y <= 1; y++)
                for (int x = -1; x <= 1; x++)
                {
                    float2 offset = float2(x, y);
                    float2 feature = offset + VfxHash22(cell + offset + seed) - f;
                    float d = dot(feature, feature);
                    if (d < minDist) { secondDist = minDist; minDist = d; cellId = VfxHash21(cell + offset + seed); }
                    else if (d < secondDist) { secondDist = d; }
                }
                return float3(sqrt(minDist), cellId, sqrt(secondDist) - sqrt(minDist));
            }

            half4 Frag(VfxVaryings input) : SV_Target
            {
                float2 uv = VfxStyleUV(input.uv);
                float t = VfxStyleTime(_Time.y * _Speed);
                float3 vor = VfxVoronoi(uv * _CellDensity, VfxHash11(_Seed) * 91.0);
                float2 polar = VfxPolar(uv);
                // Cell-by-cell crystal growth from the centre: a cell appears when
                // progress exceeds its radial rank plus a per-cell jitter (hard threshold).
                float cellRank = saturate(polar.x + (vor.y - 0.5) * 0.35);
                float grown = step(cellRank, _GrowthProgress);
                float facet = lerp(1.0 - vor.x, 1.0, _FacetContrast * vor.y);
                float scatter = VfxFbm(uv * 3.0 + t * 0.05, 2) * _InteriorScatter * _DetailMul;
                float edgeHighlight = 1.0 - smoothstep(0.0, 0.08, vor.z);
                float mask = 1.0 - smoothstep(0.75, 1.0, polar.x);
                float energy = saturate(facet * 0.6 + scatter + edgeHighlight * 0.6);
                float alpha = grown * mask * step(0.02, energy);
                float3 rgb = VfxPaletteRamp(energy, _ColorCool.rgb, _ColorPrimary.rgb, _ColorSecondary.rgb, _ColorHot.rgb) * _Intensity;
                half4 color = half4(rgb, alpha * 0.92);
                return VfxApplyStyleStage(color, uv, vor.z);
            }
            ENDHLSL
        }
    }
}

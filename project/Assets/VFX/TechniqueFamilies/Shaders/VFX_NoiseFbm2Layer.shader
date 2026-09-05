// Variant: noise_fbm_2layer (TECHNIQUE_VARIANT_LIBRARY_v1 section 3).
// Dual-layer FBM with an independent speed ratio and anisotropic stretch
// (reference group A: motion-axis low frequency, perpendicular high frequency).
Shader "VFXComposer/TechniqueFamilies/NoiseFbm2Layer"
{
    Properties
    {
        [HDR] _ColorPrimary("Palette Primary", Color) = (1, 0.35, 0.05, 1)
        [HDR] _ColorSecondary("Palette Secondary", Color) = (1, 0.7, 0.15, 1)
        [HDR] _ColorHot("Palette Hot", Color) = (4, 3.2, 1.6, 1)
        [HDR] _ColorCool("Palette Cool", Color) = (0.35, 0.05, 0, 1)
        [HDR] _ColorResidue("Palette Residue", Color) = (0.08, 0.06, 0.05, 1)
        _Intensity("Intensity", Range(0, 2)) = 1
        _Speed("Speed", Range(0.25, 3)) = 1
        _Seed("Seed", Float) = 0
        _ScrollDir("Scroll Direction (xy)", Vector) = (0, 1, 0, 0)
        _SpeedRatio("Layer Speed Ratio", Range(1, 4)) = 2.3
        _Stretch("Anisotropic Stretch", Range(1, 3)) = 1.5
        _DetailWeight("Detail Layer Weight", Range(0, 1)) = 0.5
        _ThresholdWidth("Threshold Width", Range(0.005, 0.5)) = 0.15
        _NoiseLayers("Noise Layers (tier)", Range(1, 3)) = 2
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
            float4 _ScrollDir;
            float _SpeedRatio;
            float _Stretch;
            float _DetailWeight;
            float _ThresholdWidth;
            float _NoiseLayers;
            CBUFFER_END

            #include "Includes/VfxStyleStage.hlsl"

            half4 Frag(VfxVaryings input) : SV_Target
            {
                float2 uv = VfxStyleUV(input.uv);
                float t = VfxStyleTime(_Time.y * _Speed);
                float2 dir = normalize(_ScrollDir.xy + 1e-5);
                // Anisotropy: compress sampling along the scroll axis, keep it high-frequency across.
                float2 anisoUv = float2(uv.x, uv.y / max(_Stretch, 1.0));
                float2 seedOffset = float2(VfxHash11(_Seed), VfxHash11(_Seed + 7.0)) * 37.0;
                float coarse = VfxFbm(anisoUv * 3.0 - dir * t + seedOffset, 3);
                float value = coarse;
                if (_NoiseLayers > 1.5)
                {
                    float fine = VfxFbm(anisoUv * 7.0 - dir * t * _SpeedRatio + seedOffset.yx, 3);
                    value = coarse + (fine - 0.5) * _DetailWeight * _DetailMul;
                }
                // Radial mask keeps the form inside the quad; tear increases with V (top tear).
                float2 polar = VfxPolar(uv);
                float mask = 1.0 - smoothstep(0.55, 1.0, polar.x);
                float threshold = 0.42 + 0.25 * uv.y;
                float alpha = smoothstep(threshold, threshold + _ThresholdWidth, value) * mask;
                float energy = saturate(alpha * (1.6 - polar.x));
                float3 rgb = VfxPaletteRamp(energy, _ColorCool.rgb, _ColorPrimary.rgb, _ColorSecondary.rgb, _ColorHot.rgb) * _Intensity;
                half4 color = half4(rgb, alpha);
                return VfxApplyStyleStage(color, uv, alpha < 0.001 ? -1.0 : alpha * 0.2);
            }
            ENDHLSL
        }
    }
}

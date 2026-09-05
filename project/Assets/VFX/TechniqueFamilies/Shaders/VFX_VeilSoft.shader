// Variant: mat_veil_soft (sg_comp_veil_layer). Low-alpha, oversized, very-soft
// haze layer (REFERENCE_ANALYSIS section 2-4). The alpha ceiling is clamped in
// the shader so element presets cannot turn the veil into a solid: maxAlpha is
// hard-limited to 0.2 regardless of material input.
Shader "VFXComposer/TechniqueFamilies/VeilSoft"
{
    Properties
    {
        [HDR] _ColorPrimary("Palette Primary", Color) = (0.9, 0.5, 0.25, 1)
        [HDR] _ColorSecondary("Palette Secondary", Color) = (1, 0.7, 0.4, 1)
        [HDR] _ColorHot("Palette Hot", Color) = (1.4, 1, 0.6, 1)
        [HDR] _ColorCool("Palette Cool", Color) = (0.25, 0.12, 0.06, 1)
        [HDR] _ColorResidue("Palette Residue", Color) = (0.08, 0.05, 0.03, 1)
        _Intensity("Intensity", Range(0, 2)) = 1
        _Speed("Speed", Range(0.25, 3)) = 1
        _Seed("Seed", Float) = 0
        _MaxAlpha("Max Alpha (hard cap 0.2)", Range(0.02, 0.2)) = 0.12
        _Softness("Edge Softness", Range(0.4, 1)) = 0.7
        _DriftDir("Drift Direction (xy)", Vector) = (0, 0.3, 0, 0)
        _NoiseScale("Noise Scale", Range(0.5, 6)) = 2
        _ShadingSteps("Style Shading Steps", Float) = 3
        _EdgeSharpness("Style Edge Sharpness", Range(0, 1)) = 0.3
        _OutlineWidth("Style Outline Width", Float) = 0
        _OutlineColor("Style Outline Color", Color) = (0.1, 0.1, 0.1, 1)
        _SaturationMul("Style Saturation Mul", Float) = 1.1
        _HdrClamp("Style HDR Clamp", Float) = 1.5
        _DetailMul("Style Detail Mul", Range(0, 1)) = 1
        _InnerLine("Style Inner Line", Float) = 0
        _PixelSize("Style Pixel Size", Float) = 0.0625
        _ColorSteps("Style Color Steps", Float) = 5
        _AlphaCutoff("Style Alpha Cutoff", Range(0, 1)) = 0.08
        _DitherLevels("Style Dither Levels", Float) = 3
        _StyleFrameRate("Style Frame Rate", Float) = 12
        _Outline1px("Style Outline 1px", Float) = 0
    }
    SubShader
    {
        // Queue -20: the veil is painted first, under every principal layer
        // (MATERIAL spec section 6.4 queue-offset table).
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-20" "RenderPipeline" = "UniversalPipeline" }
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
            float _MaxAlpha;
            float _Softness;
            float4 _DriftDir;
            float _NoiseScale;
            CBUFFER_END

            #include "Includes/VfxStyleStage.hlsl"

            half4 Frag(VfxVaryings input) : SV_Target
            {
                float2 uv = VfxStyleUV(input.uv);
                float t = VfxStyleTime(_Time.y * _Speed);
                float2 seedOffset = float2(VfxHash11(_Seed), VfxHash11(_Seed + 9.0)) * 41.0;
                float n = VfxFbm(uv * _NoiseScale + seedOffset - _DriftDir.xy * t, 1);
                float2 polar = VfxPolar(uv);
                // Very soft radial falloff; softness widens the fade band.
                float mask = 1.0 - smoothstep(1.0 - _Softness, 1.0, polar.x);
                // Hard discipline cap: a veil can never exceed alpha 0.2.
                float cap = min(_MaxAlpha, 0.2);
                float alpha = saturate(n * mask) * cap;
                float3 rgb = VfxPaletteRamp(n * 0.6, _ColorCool.rgb, _ColorPrimary.rgb, _ColorSecondary.rgb, _ColorHot.rgb) * _Intensity;
                half4 color = half4(rgb, alpha);
                // Volume-kind layer: no outline concept, edgeDist stays -1.
                return VfxApplyStyleStage(color, uv, -1.0);
            }
            ENDHLSL
        }
    }
}

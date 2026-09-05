// Variant: multiply_dark_core. The shadow-element inverse-emission structure
// (gate M-11): a multiplicative darkening core plus an additive coloured edge.
// Both channels live in one pass using premultiplied-style blending
// (Blend One OneMinusSrcAlpha: rgb adds, alpha multiplies the background down).
Shader "VFXComposer/TechniqueFamilies/MultiplyDarkCore"
{
    Properties
    {
        [HDR] _ColorPrimary("Palette Primary", Color) = (0.08, 0.02, 0.15, 1)
        [HDR] _ColorSecondary("Palette Secondary", Color) = (0.45, 0.05, 0.6, 1)
        [HDR] _ColorHot("Palette Hot", Color) = (1.2, 0.2, 1.8, 1)
        [HDR] _ColorCool("Palette Cool", Color) = (0, 0, 0.02, 1)
        [HDR] _ColorResidue("Palette Residue", Color) = (0.05, 0.02, 0.08, 1)
        _Intensity("Intensity", Range(0, 2)) = 1
        _Speed("Speed", Range(0.25, 3)) = 1
        _Seed("Seed", Float) = 0
        _Darkness("Core Darkness", Range(0, 1)) = 0.85
        _EdgeGlowWidth("Edge Glow Width", Range(0.01, 0.4)) = 0.12
        _TendrilAmount("Tendril Amount", Range(0, 1)) = 0.5
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
        Blend One OneMinusSrcAlpha
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
            float _Darkness;
            float _EdgeGlowWidth;
            float _TendrilAmount;
            CBUFFER_END

            #include "Includes/VfxStyleStage.hlsl"

            half4 Frag(VfxVaryings input) : SV_Target
            {
                float2 uv = VfxStyleUV(input.uv);
                float t = VfxStyleTime(_Time.y * _Speed);
                float2 polar = VfxPolar(uv);
                // Tendril silhouette: radius modulated by angular noise flowing inward.
                float tendril = VfxFbm(float2(polar.y * 6.0, polar.x * 2.0 - t * 0.3) + VfxHash11(_Seed) * 11.0, 3) - 0.5;
                float radius = polar.x + tendril * _TendrilAmount * 0.6;
                float core = 1.0 - smoothstep(0.45, 0.7, radius);
                float edge = (1.0 - smoothstep(0.0, _EdgeGlowWidth, abs(radius - 0.62))) * step(0.2, polar.x);
                // rgb = additive purple edge only; alpha = multiplicative darkening from the core.
                float3 rgb = _ColorHot.rgb * edge * _Intensity;
                float darkening = core * _Darkness;
                half4 color = half4(rgb, darkening);
                return VfxApplyStyleStage(color, uv, abs(radius - 0.62));
            }
            ENDHLSL
        }
    }
}

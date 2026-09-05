// Variant: sdf_scanline. Directional scan bands with duty cycle and a
// low-probability glitch (blocky UV offset), stepped in time.
Shader "VFXComposer/TechniqueFamilies/SdfScanline"
{
    Properties
    {
        [HDR] _ColorPrimary("Palette Primary", Color) = (0.1, 0.85, 1, 1)
        [HDR] _ColorSecondary("Palette Secondary", Color) = (1, 0.55, 0.1, 1)
        [HDR] _ColorHot("Palette Hot", Color) = (2, 3.5, 4, 1)
        [HDR] _ColorCool("Palette Cool", Color) = (0.02, 0.15, 0.25, 1)
        [HDR] _ColorResidue("Palette Residue", Color) = (0.2, 0.4, 0.5, 1)
        _Intensity("Intensity", Range(0, 2)) = 1
        _Speed("Speed", Range(0.25, 3)) = 1
        _Seed("Seed", Float) = 0
        _LineDensity("Line Density", Range(2, 60)) = 18
        _ScanSpeed("Scan Speed", Range(0, 4)) = 1
        _Duty("Duty Cycle", Range(0.05, 0.95)) = 0.5
        _GlitchChance("Glitch Chance", Range(0, 1)) = 0.15
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
            float _LineDensity;
            float _ScanSpeed;
            float _Duty;
            float _GlitchChance;
            CBUFFER_END

            #include "Includes/VfxStyleStage.hlsl"

            half4 Frag(VfxVaryings input) : SV_Target
            {
                float2 uv = VfxStyleUV(input.uv);
                float t = VfxStyleTime(_Time.y * _Speed);
                float glitchT = floor(t * 8.0);
                // Blocky UV glitch: whole horizontal bands offset for one step.
                float band = floor(uv.y * 6.0);
                float glitchRoll = VfxHash21(float2(band, glitchT) + VfxHash11(_Seed) * 29.0);
                float glitchOn = step(1.0 - _GlitchChance * 0.5, glitchRoll);
                uv.x = frac(uv.x + glitchOn * (VfxHash11(band + glitchT) - 0.5) * 0.25);
                float scan = frac(uv.y * _LineDensity - t * _ScanSpeed * 2.0);
                float lineOn = step(scan, _Duty);
                // Sweep highlight: one bright line travelling through.
                float sweepPos = frac(t * _ScanSpeed * 0.35);
                float sweep = 1.0 - smoothstep(0.0, 0.08, abs(uv.y - sweepPos));
                float2 polar = VfxPolar(uv);
                float mask = 1.0 - smoothstep(0.85, 1.0, polar.x);
                float energy = saturate(lineOn * 0.45 + sweep);
                float alpha = saturate(lineOn * 0.35 + sweep * 0.9) * mask;
                float3 rgb = lerp(_ColorPrimary.rgb, _ColorHot.rgb, sweep) * _Intensity;
                rgb = lerp(rgb, _ColorSecondary.rgb, glitchOn * 0.6);
                half4 color = half4(rgb, alpha);
                return VfxApplyStyleStage(color, uv, alpha > 0.001 ? scan * 0.05 : -1.0);
            }
            ENDHLSL
        }
    }
}

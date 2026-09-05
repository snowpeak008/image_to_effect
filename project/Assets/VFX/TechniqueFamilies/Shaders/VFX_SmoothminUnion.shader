// Variant: smoothmin_union. Liquid droplet merging via smooth-minimum union of
// up to 8 animated SDF blobs (gate M-10), with the tension highlight line.
Shader "VFXComposer/TechniqueFamilies/SmoothminUnion"
{
    Properties
    {
        [HDR] _ColorPrimary("Palette Primary", Color) = (0.25, 0.55, 0.9, 1)
        [HDR] _ColorSecondary("Palette Secondary", Color) = (0.6, 0.85, 1, 1)
        [HDR] _ColorHot("Palette Hot", Color) = (1.2, 1.4, 1.6, 1)
        [HDR] _ColorCool("Palette Cool", Color) = (0.05, 0.2, 0.45, 1)
        [HDR] _ColorResidue("Palette Residue", Color) = (0.1, 0.15, 0.2, 1)
        _Intensity("Intensity", Range(0, 2)) = 1
        _Speed("Speed", Range(0.25, 3)) = 1
        _Seed("Seed", Float) = 0
        _BlobCount("Blob Count", Range(1, 8)) = 5
        _SmoothK("Smooth Union K", Range(0.01, 0.5)) = 0.18
        _HighlightWidth("Highlight Width", Range(0.002, 0.08)) = 0.02
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
            float _BlobCount;
            float _SmoothK;
            float _HighlightWidth;
            CBUFFER_END

            #include "Includes/VfxStyleStage.hlsl"

            float VfxSmoothMin(float a, float b, float k)
            {
                float h = saturate(0.5 + 0.5 * (b - a) / k);
                return lerp(b, a, h) - k * h * (1.0 - h);
            }

            half4 Frag(VfxVaryings input) : SV_Target
            {
                float2 uv = VfxStyleUV(input.uv);
                float t = VfxStyleTime(_Time.y * _Speed);
                float seed = VfxHash11(_Seed) * 63.0;
                float2 p = (uv - 0.5) * 2.0;
                float d = 1e5;
                for (int i = 0; i < 8; i++)
                {
                    if (i >= (int)_BlobCount) break;
                    float fi = (float)i;
                    float2 centre = float2(
                        sin(t * (0.4 + VfxHash11(seed + fi) * 0.5) + fi * 2.1) * 0.45,
                        cos(t * (0.3 + VfxHash11(seed + fi + 9.0) * 0.5) + fi * 1.7) * 0.45 - 0.1 * frac(t * 0.2));
                    float radius = 0.14 + VfxHash11(seed + fi + 31.0) * 0.18;
                    d = VfxSmoothMin(d, length(p - centre) - radius, _SmoothK);
                }
                float alpha = 1.0 - smoothstep(0.0, 0.015, d);
                // Tension highlight: a thin bright line just inside the surface.
                float highlight = 1.0 - smoothstep(0.0, _HighlightWidth, abs(d + _HighlightWidth * 2.0));
                float body = saturate(-d * 2.5);
                float3 rgb = lerp(_ColorPrimary.rgb, _ColorCool.rgb, body);
                rgb = lerp(rgb, _ColorHot.rgb, highlight);
                half4 color = half4(rgb * _Intensity, alpha * 0.85);
                return VfxApplyStyleStage(color, uv, max(-d, 0.0));
            }
            ENDHLSL
        }
    }
}

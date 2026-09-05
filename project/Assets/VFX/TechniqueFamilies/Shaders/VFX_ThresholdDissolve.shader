// Variant: threshold_dissolve. The three-mode threshold stage (soft/hard/grow,
// gate M-7) over an FBM host field, with an emissive dissolve edge. grow mode
// advances the threshold outward from a seed point through Voronoi cells.
Shader "VFXComposer/TechniqueFamilies/ThresholdDissolve"
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
        _Mode("Mode (0 soft 1 hard 2 grow)", Range(0, 2)) = 0
        _ThresholdWidth("Threshold Width", Range(0.002, 0.5)) = 0.15
        _Progress("Progress", Range(0, 1)) = 0.5
        _GrowFrom("Grow From (0 point 1 edge 2 voronoiSeed)", Range(0, 2)) = 0
        _EdgeGlowWidth("Edge Glow Width", Range(0, 0.3)) = 0.08
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
            float _Mode;
            float _ThresholdWidth;
            float _Progress;
            float _GrowFrom;
            float _EdgeGlowWidth;
            CBUFFER_END

            #include "Includes/VfxStyleStage.hlsl"

            half4 Frag(VfxVaryings input) : SV_Target
            {
                float2 uv = VfxStyleUV(input.uv);
                float t = VfxStyleTime(_Time.y * _Speed);
                float2 seedOffset = float2(VfxHash11(_Seed), VfxHash11(_Seed + 3.0)) * 23.0;
                float field = VfxFbm(uv * 5.0 + seedOffset + t * 0.1, 3);
                float2 polar = VfxPolar(uv);
                float mask = 1.0 - smoothstep(0.6, 1.0, polar.x);
                float alpha;
                float edgeBand;
                if (_Mode < 0.5)
                {
                    // soft: wide smoothstep around progress
                    float th = _Progress;
                    alpha = smoothstep(th - _ThresholdWidth, th + _ThresholdWidth, field);
                    edgeBand = 1.0 - smoothstep(0.0, _EdgeGlowWidth, abs(field - th));
                }
                else if (_Mode < 1.5)
                {
                    // hard: near-zero width
                    float th = _Progress;
                    alpha = step(th, field);
                    edgeBand = 1.0 - smoothstep(0.0, max(_EdgeGlowWidth * 0.5, 0.005), abs(field - th));
                }
                else
                {
                    // grow: threshold advances spatially from the origin
                    float growField;
                    if (_GrowFrom < 0.5) growField = polar.x;                         // from centre point
                    else if (_GrowFrom < 1.5) growField = 1.0 - polar.x;              // from the outer edge inward
                    else growField = frac(field * 3.0 + VfxHash21(floor(uv * 6.0)));  // voronoi-seeded patches
                    float front = _Progress * 1.15;
                    alpha = step(growField, front) * step(0.35, field + 0.15);
                    edgeBand = 1.0 - smoothstep(0.0, _EdgeGlowWidth, abs(growField - front));
                }
                alpha *= mask;
                float energy = saturate(field * 0.8 + edgeBand);
                float3 rgb = VfxPaletteRamp(energy, _ColorCool.rgb, _ColorPrimary.rgb, _ColorSecondary.rgb, _ColorHot.rgb);
                rgb = lerp(rgb, _ColorHot.rgb, edgeBand * 0.8);
                half4 color = half4(rgb * _Intensity, alpha);
                return VfxApplyStyleStage(color, uv, alpha > 0.001 ? abs(field - _Progress) : -1.0);
            }
            ENDHLSL
        }
    }
}

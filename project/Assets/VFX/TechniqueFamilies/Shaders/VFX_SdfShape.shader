// Variant: sdf_shape. Analytic SDF shape family: circle / ring / star /
// polygon / fan / rounded rect / leaf / cross, all procedural (gate M-6).
Shader "VFXComposer/TechniqueFamilies/SdfShape"
{
    Properties
    {
        [HDR] _ColorPrimary("Palette Primary", Color) = (1, 1, 1, 1)
        [HDR] _ColorSecondary("Palette Secondary", Color) = (1, 1, 1, 1)
        [HDR] _ColorHot("Palette Hot", Color) = (2, 2, 2, 1)
        [HDR] _ColorCool("Palette Cool", Color) = (0.2, 0.2, 0.2, 1)
        [HDR] _ColorResidue("Palette Residue", Color) = (0.5, 0.5, 0.5, 1)
        _Intensity("Intensity", Range(0, 2)) = 1
        _Speed("Speed", Range(0.25, 3)) = 1
        _Seed("Seed", Float) = 0
        _Shape("Shape (0 circle 1 ring 2 star 3 polygon 4 fan 5 roundrect 6 leaf 7 cross)", Range(0, 7)) = 0
        _Sides("Sides / Points", Range(3, 12)) = 5
        _InnerRadius("Inner Radius", Range(0, 1)) = 0.5
        _Softness("Edge Softness", Range(0.001, 0.5)) = 0.02
        _Rotation("Rotation (turns)", Range(-1, 1)) = 0
        _FillRadius("Fill Radius", Range(0.05, 1)) = 0.8
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
            float _Shape;
            float _Sides;
            float _InnerRadius;
            float _Softness;
            float _Rotation;
            float _FillRadius;
            CBUFFER_END

            #include "Includes/VfxStyleStage.hlsl"

            float VfxShapeSdf(float2 p, float shape)
            {
                float r = length(p);
                float ang = atan2(p.y, p.x);
                if (shape < 0.5) return r - _FillRadius;                                   // circle
                if (shape < 1.5) return max(r - _FillRadius, _InnerRadius * _FillRadius - r); // ring
                if (shape < 2.5)                                                            // star
                {
                    float k = 6.28318530718 / _Sides;
                    float a = fmod(abs(ang), k) - k * 0.5;
                    float m = lerp(_InnerRadius, 1.0, abs(a) / (k * 0.5));
                    return r - _FillRadius * m;
                }
                if (shape < 3.5)                                                            // regular polygon
                {
                    float k = 6.28318530718 / _Sides;
                    float a = fmod(abs(ang), k) - k * 0.5;
                    return r * cos(a) - _FillRadius * cos(k * 0.5);
                }
                if (shape < 4.5)                                                            // fan (sector)
                {
                    float sector = step(abs(ang), _InnerRadius * 3.14159265);
                    return lerp(1.0, r - _FillRadius, sector);
                }
                if (shape < 5.5)                                                            // rounded rect
                {
                    float2 q = abs(p) - _FillRadius * float2(0.7, 0.45) + _InnerRadius * 0.2;
                    return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - _InnerRadius * 0.2;
                }
                if (shape < 6.5)                                                            // leaf (two arcs)
                {
                    float2 q = float2(abs(p.x), p.y);
                    float leaf = length(q - float2(-_FillRadius * 0.7, 0.0)) - _FillRadius * 1.15;
                    return max(leaf, -q.y - _FillRadius * 0.55);
                }
                float2 q = abs(p);                                                          // cross
                float arm = _FillRadius * max(0.12, _InnerRadius * 0.4);
                float bar1 = max(q.x - _FillRadius, q.y - arm);
                float bar2 = max(q.y - _FillRadius, q.x - arm);
                return min(bar1, bar2);
            }

            half4 Frag(VfxVaryings input) : SV_Target
            {
                float2 uv = VfxStyleUV(input.uv);
                float t = VfxStyleTime(_Time.y * _Speed);
                float rot = (_Rotation * t) * 6.28318530718;
                float2 p = (uv - 0.5) * 2.0;
                float cs = cos(rot); float sn = sin(rot);
                p = float2(p.x * cs - p.y * sn, p.x * sn + p.y * cs);
                float d = VfxShapeSdf(p, _Shape);
                float alpha = 1.0 - smoothstep(0.0, _Softness, d);
                float energy = saturate(1.0 - smoothstep(-_FillRadius * 0.6, 0.0, d));
                float3 rgb = VfxPaletteRamp(0.4 + energy * 0.6, _ColorCool.rgb, _ColorPrimary.rgb, _ColorSecondary.rgb, _ColorHot.rgb) * _Intensity;
                half4 color = half4(rgb, alpha);
                return VfxApplyStyleStage(color, uv, max(-d, 0.0));
            }
            ENDHLSL
        }
    }
}

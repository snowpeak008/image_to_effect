// Variant: sdf_sacred_pattern. Symmetric geometric array: concentric rings +
// radial rays + a central polygon, all analytic SDF driven by a seed.
Shader "VFXComposer/TechniqueFamilies/SdfSacredPattern"
{
    Properties
    {
        [HDR] _ColorPrimary("Palette Primary", Color) = (1, 0.92, 0.7, 1)
        [HDR] _ColorSecondary("Palette Secondary", Color) = (1, 0.98, 0.9, 1)
        [HDR] _ColorHot("Palette Hot", Color) = (5, 4.8, 4, 1)
        [HDR] _ColorCool("Palette Cool", Color) = (0.8, 0.6, 0.3, 1)
        [HDR] _ColorResidue("Palette Residue", Color) = (0.9, 0.85, 0.7, 1)
        _Intensity("Intensity", Range(0, 2)) = 1
        _Speed("Speed", Range(0.25, 3)) = 1
        _Seed("Seed", Float) = 0
        _Rings("Ring Count", Range(1, 6)) = 3
        _Rays("Ray Count", Range(0, 24)) = 12
        _PolygonSides("Polygon Sides", Range(3, 8)) = 6
        _LineWidth("Line Width", Range(0.002, 0.05)) = 0.01
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
        Blend One One
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
            float _Rings;
            float _Rays;
            float _PolygonSides;
            float _LineWidth;
            CBUFFER_END

            #include "Includes/VfxStyleStage.hlsl"

            half4 Frag(VfxVaryings input) : SV_Target
            {
                float2 uv = VfxStyleUV(input.uv);
                float t = VfxStyleTime(_Time.y * _Speed);
                float2 p = (uv - 0.5) * 2.0;
                float r = length(p);
                float ang = atan2(p.y, p.x) + t * 0.15;
                float seed = VfxHash11(_Seed);
                float line1 = 1e5;
                // Concentric rings with seed-driven radii.
                for (int i = 0; i < 6; i++)
                {
                    if (i >= (int)_Rings) break;
                    float ringR = lerp(0.25, 0.9, (i + VfxHash11(seed + i)) / max(_Rings, 1.0));
                    line1 = min(line1, abs(r - ringR));
                }
                // Radial rays between the innermost and outermost ring.
                float rays = 1e5;
                if (_Rays > 0.5)
                {
                    float k = 6.28318530718 / _Rays;
                    float a = fmod(abs(ang + seed * 6.28), k) - k * 0.5;
                    rays = abs(a) * r;
                    rays = max(rays, 0.28 - r);
                    rays = max(rays, r - 0.88);
                }
                // Central polygon outline.
                float kp = 6.28318530718 / _PolygonSides;
                float ap = fmod(abs(ang * 1.0), kp) - kp * 0.5;
                float poly = abs(r * cos(ap) - 0.22);
                float d = min(min(line1, rays), poly);
                float alpha = 1.0 - smoothstep(0.0, _LineWidth, d);
                float glow = (1.0 - smoothstep(0.0, _LineWidth * 8.0, d)) * 0.2;
                float mask = 1.0 - smoothstep(0.92, 1.0, r);
                float3 rgb = lerp(_ColorPrimary.rgb, _ColorHot.rgb, alpha) * _Intensity;
                half4 color = half4(rgb, saturate(alpha + glow) * mask);
                return VfxApplyStyleStage(color, uv, d);
            }
            ENDHLSL
        }
    }
}

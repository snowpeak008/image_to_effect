// Variant: mat_bubble_surface (sg_noise_bubble_field). Voronoi-cell bubbles that
// rise along +V and pop instantly past popThreshold (a snap, not a fade), per
// MATERIAL spec section 3.1. Serves core / body / surface / decal roles.
Shader "VFXComposer/TechniqueFamilies/BubbleField"
{
    Properties
    {
        [HDR] _ColorPrimary("Palette Primary", Color) = (0.35, 0.8, 0.25, 1)
        [HDR] _ColorSecondary("Palette Secondary", Color) = (0.6, 0.95, 0.35, 1)
        [HDR] _ColorHot("Palette Hot", Color) = (1.4, 2.2, 0.9, 1)
        [HDR] _ColorCool("Palette Cool", Color) = (0.08, 0.3, 0.1, 1)
        [HDR] _ColorResidue("Palette Residue", Color) = (0.05, 0.1, 0.04, 1)
        _Intensity("Intensity", Range(0, 2)) = 1
        _Speed("Speed", Range(0.25, 3)) = 1
        _Seed("Seed", Float) = 0
        _Density("Bubble Density", Range(2, 12)) = 6
        _RiseSpeed("Rise Speed", Range(0, 2)) = 0.4
        _PopThreshold("Pop Threshold (V)", Range(0.3, 1)) = 0.85
        _SizeMin("Size Min", Range(0.05, 0.5)) = 0.15
        _SizeMax("Size Max", Range(0.05, 0.5)) = 0.35
        _FillAlpha("Base Fill Alpha", Range(0, 1)) = 0.55
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
            float _Density;
            float _RiseSpeed;
            float _PopThreshold;
            float _SizeMin;
            float _SizeMax;
            float _FillAlpha;
            CBUFFER_END

            #include "Includes/VfxStyleStage.hlsl"

            half4 Frag(VfxVaryings input) : SV_Target
            {
                float2 uv = VfxStyleUV(input.uv);
                float t = VfxStyleTime(_Time.y * _Speed);

                // Base viscous fill.
                float2 seedOffset = float2(VfxHash11(_Seed), VfxHash11(_Seed + 5.0)) * 29.0;
                float fill = VfxFbm(uv * 3.0 + seedOffset - float2(0, t * 0.05), 2);
                float2 polar = VfxPolar(uv);
                float mask = 1.0 - smoothstep(0.6, 1.0, polar.x);
                float alpha = smoothstep(0.35, 0.55, fill) * _FillAlpha * mask;

                // Bubble layer: 9-neighbourhood cell search; each cell owns one
                // bubble whose centre rises with time and pops instantly.
                float holes = 0.0;
                float rim = 0.0;
                float2 g = uv * _Density;
                float2 cell = floor(g);
                [unroll]
                for (int j = -1; j <= 1; j++)
                {
                    [unroll]
                    for (int i = -1; i <= 1; i++)
                    {
                        float2 c = cell + float2(i, j);
                        float2 rnd = VfxHash22(c + _Seed * 13.0);
                        // Rising phase: repeats; V position climbs from spawn point.
                        float phase = frac(t * _RiseSpeed * (0.6 + rnd.y * 0.8) + rnd.x);
                        float2 centre = (c + float2(rnd.x, frac(rnd.y + phase))) / _Density;
                        float v = centre.y;
                        // Instant pop: past the threshold the radius is zero (snap).
                        float alive = step(v, _PopThreshold);
                        float size = lerp(_SizeMin, _SizeMax, VfxHash21(c * 1.7 + _Seed)) / _Density;
                        // Slight swell just before popping.
                        size *= 1.0 + 0.3 * smoothstep(_PopThreshold - 0.1, _PopThreshold, v);
                        float d = length(uv - centre);
                        float inside = (1.0 - smoothstep(size * 0.7, size, d)) * alive;
                        float edge = (smoothstep(size * 0.55, size * 0.8, d) - smoothstep(size * 0.8, size, d)) * alive;
                        holes = max(holes, inside);
                        rim = max(rim, edge);
                    }
                }

                // Bubbles punch holes in the fill; rims catch highlight.
                alpha = saturate(alpha * (1.0 - holes * 0.85) + rim * 0.5 * mask);
                float energy = saturate(fill * 0.6 + rim);
                float3 rgb = VfxHdrGrade(energy, _ColorCool.rgb, _ColorPrimary.rgb, _ColorSecondary.rgb, _ColorHot.rgb, _ColorResidue.rgb, 0.3) * _Intensity;
                half4 color = half4(rgb, alpha);
                return VfxApplyStyleStage(color, uv, alpha < 0.001 ? -1.0 : alpha * 0.25);
            }
            ENDHLSL
        }
    }
}

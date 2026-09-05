// Variant: sdf_rune_ring. Ring + tick marks + seeded glyph strokes with
// counter-rotating layers and a periodic phase pulse.
Shader "VFXComposer/TechniqueFamilies/SdfRuneRing"
{
    Properties
    {
        [HDR] _ColorPrimary("Palette Primary", Color) = (0.5, 0.25, 1, 1)
        [HDR] _ColorSecondary("Palette Secondary", Color) = (0.2, 0.8, 1, 1)
        [HDR] _ColorHot("Palette Hot", Color) = (2.5, 1.8, 4, 1)
        [HDR] _ColorCool("Palette Cool", Color) = (0.15, 0.05, 0.35, 1)
        [HDR] _ColorResidue("Palette Residue", Color) = (0.4, 0.3, 0.6, 1)
        _Intensity("Intensity", Range(0, 2)) = 1
        _Speed("Speed", Range(0.25, 3)) = 1
        _Seed("Seed", Float) = 0
        _RingCount("Ring Count", Range(1, 3)) = 2
        _TickCount("Tick Count", Range(4, 48)) = 24
        _GlyphSeed("Glyph Seed", Float) = 0
        _CounterRotate("Counter Rotate", Range(0, 1)) = 1
        _PulsePeriod("Pulse Period (s)", Range(0.2, 4)) = 1.4
        _LineWidth("Line Width", Range(0.002, 0.05)) = 0.012
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
            float _RingCount;
            float _TickCount;
            float _GlyphSeed;
            float _CounterRotate;
            float _PulsePeriod;
            float _LineWidth;
            CBUFFER_END

            #include "Includes/VfxStyleStage.hlsl"

            half4 Frag(VfxVaryings input) : SV_Target
            {
                float2 uv = VfxStyleUV(input.uv);
                float t = VfxStyleTime(_Time.y * _Speed);
                float2 p = (uv - 0.5) * 2.0;
                float r = length(p);
                float baseAng = atan2(p.y, p.x);
                float seed = VfxHash11(_Seed + _GlyphSeed);
                float d = 1e5;
                for (int i = 0; i < 3; i++)
                {
                    if (i >= (int)_RingCount) break;
                    float dir = (_CounterRotate > 0.5 && (i % 2) == 1) ? -1.0 : 1.0;
                    float ang = baseAng + dir * t * (0.2 + 0.1 * i);
                    float ringR = lerp(0.4, 0.85, i / max(_RingCount - 1.0, 1.0));
                    d = min(d, abs(r - ringR));
                    // Tick marks on the ring.
                    float k = 6.28318530718 / _TickCount;
                    float a = fmod(ang + 6.28318530718, k) - k * 0.5;
                    float tick = max(abs(a) * r - _LineWidth * 1.5, abs(r - ringR) - 0.035);
                    d = min(d, tick);
                    // Seeded glyph strokes: short radial+tangential dashes between rings.
                    float slot = floor((ang / 6.28318530718 + 0.5) * 12.0);
                    float glyphOn = step(0.45, VfxHash11(slot + seed * 91.0 + i * 17.0));
                    float slotCentre = (slot + 0.5) / 12.0 * 6.28318530718 - 3.14159265;
                    float da = abs(ang - slotCentre);
                    float glyph = max(da * r - _LineWidth * 2.0, abs(r - ringR + 0.08) - 0.03);
                    d = min(d, lerp(1e5, glyph, glyphOn));
                }
                float pulse = 0.7 + 0.3 * exp(-4.0 * frac(t / _PulsePeriod));
                float alpha = (1.0 - smoothstep(0.0, _LineWidth, d));
                float halo = (1.0 - smoothstep(0.0, _LineWidth * 10.0, d)) * 0.18;
                float3 rgb = lerp(_ColorPrimary.rgb, _ColorHot.rgb, alpha) * pulse * _Intensity;
                half4 color = half4(rgb, saturate(alpha + halo));
                return VfxApplyStyleStage(color, uv, d);
            }
            ENDHLSL
        }
    }
}

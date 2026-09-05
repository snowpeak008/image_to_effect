// Variant: mat_glow_stack (TECH_FAMILY_SPEC_MATERIAL.md section 5).
// Additive glow billboard used by Glow_k child nodes. Full ADR-010 section 4bis-6
// parameter surface: radius lives on the transform (compile-time scale), this
// shader owns falloff curve (4 static-keyword variants), breakup noise
// (angular + 2D detail, per-layer decorrelated seed), motion-axis anisotropy,
// inner/outer colour split and the beat coupling (_BeatValue written per frame
// by the light-beat driver via MaterialPropertyBlock; see MESH_LIGHT section 6).
Shader "VFXComposer/TechniqueFamilies/GlowStack"
{
    Properties
    {
        [HDR] _ColorPrimary("Palette Primary (outer default)", Color) = (1, 0.45, 0.1, 1)
        [HDR] _ColorSecondary("Palette Secondary", Color) = (1, 0.75, 0.3, 1)
        [HDR] _ColorHot("Palette Hot (inner default)", Color) = (4, 3.4, 2.2, 1)
        [HDR] _ColorCool("Palette Cool", Color) = (0.4, 0.1, 0, 1)
        [HDR] _ColorResidue("Palette Residue", Color) = (0.1, 0.05, 0.02, 1)
        [HDR] _InnerColor("Glow Inner Color", Color) = (4, 3.4, 2.2, 1)
        [HDR] _OuterColor("Glow Outer Color", Color) = (1, 0.45, 0.1, 1)
        _Intensity("Intensity", Range(0, 4)) = 1
        _Speed("Speed", Range(0.25, 3)) = 1
        _Seed("Seed", Float) = 0
        _GlowSize("Glow Size (radius multiple, drives coupling)", Range(1, 8)) = 1.8
        _SizeCouplingK("Size Coupling Exponent k", Range(0, 2)) = 0.85
        _ColorMixPower("Color Mix Power", Range(0.5, 3)) = 1.4
        _FalloffParams("Falloff (x power, y steps)", Vector) = (1.5, 3, 0, 0)
        _BreakupNoise("Breakup Noise", Range(0, 1)) = 0.35
        _BreakupAngularFreq("Breakup Angular Freq", Range(2, 12)) = 5
        _BreakupSeed("Breakup Seed (per layer)", Float) = 0
        _Anisotropy("Anisotropy", Range(0, 3)) = 0
        _GlowAxisWS("Glow Axis WS (xyz)", Vector) = (0, 1, 0, 0)
        _BeatValue("Beat Value (driver-written)", Range(0, 1)) = 1
        _FlickerCoupling("Flicker Coupling", Range(0, 1)) = 0.6
        _LayerIndex("Layer Index k", Float) = 0
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
        _Outline1px("Style Outline 1px", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+10" "RenderPipeline" = "UniversalPipeline" }
        Blend One One
        ZWrite Off
        Cull Off
        Pass
        {
            Name "Forward"
            HLSLPROGRAM
            #pragma vertex VfxVert
            #pragma fragment Frag
            #pragma multi_compile_local _FALLOFF_GAUSSIAN _FALLOFF_EXP _FALLOFF_LINEAR _FALLOFF_STEP
            #pragma multi_compile_local _ _STYLESTAGE_CARTOON _STYLESTAGE_PIXEL
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Includes/VfxCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
            VFX_COMMON_UNIFORMS
            half4 _InnerColor;
            half4 _OuterColor;
            float _GlowSize;
            float _SizeCouplingK;
            float _ColorMixPower;
            float4 _FalloffParams;
            float _BreakupNoise;
            float _BreakupAngularFreq;
            float _BreakupSeed;
            float _Anisotropy;
            float4 _GlowAxisWS;
            float _BeatValue;
            float _FlickerCoupling;
            float _LayerIndex;
            CBUFFER_END

            #include "Includes/VfxStyleStage.hlsl"

            float GlowFalloff(float d)
            {
#if defined(_FALLOFF_EXP)
                return exp(-5.5 * d);
#elif defined(_FALLOFF_LINEAR)
                return pow(saturate(1.0 - d), max(_FalloffParams.x, 0.01));
#elif defined(_FALLOFF_STEP)
                float s = max(_FalloffParams.y, 2.0);
                return saturate(1.0 - floor(saturate(d) * s) / s);
#else // gaussian (default)
                return exp(-5.5 * d * d);
#endif
            }

            half4 Frag(VfxVaryings input) : SV_Target
            {
                float2 uv = VfxStyleUV(input.uv);
                float t = VfxStyleTime(_Time.y * _Speed);
                float2 p = uv * 2.0 - 1.0;

                // Motion-axis anisotropy: project the world axis to the quad plane
                // and stretch iso-lines along it (MATERIAL section 5.6). Vanishes
                // when the axis is nearly perpendicular to the billboard.
                float3 axisWS = _GlowAxisWS.xyz;
                float2 axis2D = float2(dot(axisWS, normalize(mul((float3x3)unity_WorldToObject, float3(1, 0, 0)) + 1e-5)),
                                        dot(axisWS, normalize(mul((float3x3)unity_WorldToObject, float3(0, 1, 0)) + 1e-5)));
                float axisLen = length(axis2D);
                float aniso = _Anisotropy * saturate((axisLen - 0.15) / 0.85);
                if (aniso > 0.0001 && axisLen > 0.0001)
                {
                    float2 a = axis2D / axisLen;
                    float2 perp = float2(-a.y, a.x);
                    p = float2(dot(p, a) / (1.0 + aniso), dot(p, perp));
                }

                float d = length(p);

                // Breakup 1: angular radius modulation, per-layer decorrelated
                // (seedK and angularFreq_k shift with k so stacked layers never align).
                float theta = atan2(p.y, p.x) / 6.28318530718 + 0.5;
                float freqK = _BreakupAngularFreq * (1.0 + 0.23 * _LayerIndex);
                float seedK = _Seed + _BreakupSeed + _LayerIndex * 17.0;
                float r0 = VfxValueNoise(float2(theta * freqK + seedK, seedK * 0.37)) * 2.0 - 1.0;
                d *= 1.0 + _BreakupNoise * 0.3 * r0;

                float w = GlowFalloff(saturate(d));

                // Breakup 2: 2D detail modulation so the interior is uneven too.
                float detail = VfxValueNoise(p * 3.0 + t * 0.4 + seedK);
                w *= 1.0 - _BreakupNoise * 0.35 * detail;

                // Beat coupling: the light-beat driver owns the phase; this material
                // only consumes _BeatValue so light and glow breathe together.
                float beat = lerp(1.0, _BeatValue, _FlickerCoupling);

                // Size-intensity coupling compensation (M-13, REFERENCE_ANALYSIS
                // section 3bis): a larger glow quad spreads the same energy over
                // more pixels and reads dimmer; multiply by pow(size, k) so
                // turning glowSize up never silently fades the glow. The
                // compiler writes the transform-scale multiple into _GlowSize.
                float sizeCompensation = pow(max(_GlowSize, 1.0), _SizeCouplingK);

                float mixT = pow(saturate(w), _ColorMixPower);
                float3 rgb = lerp(_OuterColor.rgb, _InnerColor.rgb, mixT) * w * _Intensity * sizeCompensation * beat;
                half4 color = half4(rgb, saturate(w));
                return VfxApplyStyleStage(color, uv, w < 0.001 ? -1.0 : w);
            }
            ENDHLSL
        }
    }
}

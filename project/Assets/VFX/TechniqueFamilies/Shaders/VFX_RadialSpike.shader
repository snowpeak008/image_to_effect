// Variant: mat_radial_spike (sg_sdf_radial_burst, ADR-010 section 4bis-8).
// Radial spike/needle burst: count / length range / width range / angle jitter /
// taper all parametric, seed-deterministic. Additive blend (flash / shock / edge
// roles). The mesh-family radial_spike_array covers the true-geometry route;
// this is the zero-geometry material route.
Shader "VFXComposer/TechniqueFamilies/RadialSpike"
{
    Properties
    {
        [HDR] _ColorPrimary("Palette Primary", Color) = (1, 0.6, 0.2, 1)
        [HDR] _ColorSecondary("Palette Secondary", Color) = (1, 0.85, 0.4, 1)
        [HDR] _ColorHot("Palette Hot", Color) = (4, 3.4, 2, 1)
        [HDR] _ColorCool("Palette Cool", Color) = (0.4, 0.15, 0.02, 1)
        [HDR] _ColorResidue("Palette Residue", Color) = (0.1, 0.05, 0.02, 1)
        _Intensity("Intensity", Range(0, 4)) = 1
        _Speed("Speed", Range(0.25, 3)) = 1
        _Seed("Seed", Float) = 0
        _SpikeCount("Spike Count", Range(4, 64)) = 16
        _LengthMin("Length Min", Range(0.05, 1)) = 0.35
        _LengthMax("Length Max", Range(0.05, 1)) = 0.9
        _WidthMin("Width Min", Range(0.002, 0.2)) = 0.01
        _WidthMax("Width Max", Range(0.002, 0.2)) = 0.04
        _AngleJitter("Angle Jitter", Range(0, 1)) = 0.5
        _Taper("Taper (root wide, tip thin)", Range(0, 1)) = 0.8
        _Progress("Progress", Range(0, 1)) = 1
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
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+5" "RenderPipeline" = "UniversalPipeline" }
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
            float _SpikeCount;
            float _LengthMin;
            float _LengthMax;
            float _WidthMin;
            float _WidthMax;
            float _AngleJitter;
            float _Taper;
            float _Progress;
            CBUFFER_END

            #include "Includes/VfxStyleStage.hlsl"

            half4 Frag(VfxVaryings input) : SV_Target
            {
                float2 uv = VfxStyleUV(input.uv);
                float2 p = uv * 2.0 - 1.0;
                float r = length(p);
                float theta = atan2(p.y, p.x) / 6.28318530718 + 0.5;

                float count = max(_SpikeCount, 1.0);
                float slot = theta * count;
                float energy = 0.0;
                // Evaluate the spike in this angular slot and its two neighbours
                // so jittered spikes crossing slot borders stay continuous.
                [unroll]
                for (int k = -1; k <= 1; k++)
                {
                    float id = floor(slot) + k;
                    float idw = frac(id / count) * count; // wrap
                    float seedBase = VfxHash11(idw * 3.7 + _Seed * 19.1);
                    float jitter = (VfxHash11(idw * 7.3 + _Seed * 5.7) - 0.5) * _AngleJitter;
                    float centre = (id + 0.5 + jitter) / count;
                    float lenT = VfxHash11(idw * 11.9 + _Seed * 2.3);
                    float len = lerp(_LengthMin, _LengthMax, lenT) * _Progress;
                    float width = lerp(_WidthMin, _WidthMax, seedBase);
                    // Taper: width shrinks toward the tip.
                    float radialT = saturate(r / max(len, 1e-4));
                    float w = width * lerp(1.0, 1.0 - _Taper, radialT);
                    float dAng = abs(theta - centre);
                    dAng = min(dAng, 1.0 - dAng); // angular wrap
                    float arc = dAng * 6.28318530718 * r; // arc distance at radius r
                    float inLen = 1.0 - smoothstep(len * 0.85, len, r);
                    float spike = (1.0 - smoothstep(0.0, max(w, 1e-4), arc)) * inLen;
                    energy = max(energy, spike * lerp(1.0, 0.35, radialT));
                }
                float alpha = energy * smoothstep(1.0, 0.98, r);
                float3 rgb = VfxHdrGrade(energy, _ColorCool.rgb, _ColorPrimary.rgb, _ColorSecondary.rgb, _ColorHot.rgb, _ColorResidue.rgb, 0.2) * _Intensity;
                half4 color = half4(rgb, saturate(alpha));
                return VfxApplyStyleStage(color, uv, alpha < 0.001 ? -1.0 : alpha * 0.2);
            }
            ENDHLSL
        }
    }
}

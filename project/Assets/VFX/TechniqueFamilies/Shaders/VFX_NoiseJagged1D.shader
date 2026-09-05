// Variant: noise_jagged_1d. A jagged bolt band: 1D value noise drives the V
// offset along U, time is sampled through floor() so the bolt jumps between
// phases instead of interpolating (gate M-4), with the dual hard-core/soft-halo
// threshold structure (gate M-5).
Shader "VFXComposer/TechniqueFamilies/NoiseJagged1D"
{
    Properties
    {
        [HDR] _ColorPrimary("Palette Primary", Color) = (0.6, 0.7, 1, 1)
        [HDR] _ColorSecondary("Palette Secondary", Color) = (0.85, 0.6, 1, 1)
        [HDR] _ColorHot("Palette Hot", Color) = (6, 6, 8, 1)
        [HDR] _ColorCool("Palette Cool", Color) = (0.2, 0.15, 0.5, 1)
        [HDR] _ColorResidue("Palette Residue", Color) = (0.05, 0.05, 0.08, 1)
        _Intensity("Intensity", Range(0, 2)) = 1
        _Speed("Speed", Range(0.25, 3)) = 1
        _Seed("Seed", Float) = 0
        _Jitter("Jitter Amplitude", Range(0, 1)) = 0.6
        _RephaseRate("Rephase Rate (Hz)", Range(1, 30)) = 14
        _ForkWeight("Fork Channel Weight", Range(0, 1)) = 0.4
        _CoreWidth("Core Width", Range(0.001, 0.05)) = 0.006
        _HaloWidth("Halo Width", Range(0.02, 0.5)) = 0.3
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
            float _Jitter;
            float _RephaseRate;
            float _ForkWeight;
            float _CoreWidth;
            float _HaloWidth;
            CBUFFER_END

            #include "Includes/VfxStyleStage.hlsl"

            float VfxBoltPath(float u, float phase, float seed)
            {
                // Piecewise-linear 1D value noise: jagged, not smooth.
                float x = u * 9.0;
                float i = floor(x);
                float f = frac(x);
                float a = VfxHash11(i + phase * 131.7 + seed) - 0.5;
                float b = VfxHash11(i + 1.0 + phase * 131.7 + seed) - 0.5;
                return lerp(a, b, f);
            }

            half4 Frag(VfxVaryings input) : SV_Target
            {
                float2 uv = VfxStyleUV(input.uv);
                // Stepped time: the bolt re-randomizes at _RephaseRate with zero interpolation (M-4).
                float phase = floor(_Time.y * _Speed * _RephaseRate);
                float seed = VfxHash11(_Seed) * 517.0;
                float bolt = VfxBoltPath(uv.x, phase, seed) * _Jitter;
                float d = abs(uv.y - 0.5 - bolt * 0.5);
                // Fork channel: a second, weaker bolt splitting off mid-way.
                float forkBolt = VfxBoltPath(uv.x, phase + 0.5, seed + 43.0) * _Jitter;
                float forkStart = 0.3 + VfxHash11(phase + seed) * 0.4;
                float dFork = abs(uv.y - 0.5 - lerp(bolt, forkBolt, saturate((uv.x - forkStart) * 3.0)) * 0.5)
                              + step(uv.x, forkStart);
                float endFade = smoothstep(0.0, 0.06, uv.x) * smoothstep(1.0, 0.94, uv.x);
                // Dual threshold: razor core + wide soft halo (M-5).
                float core = 1.0 - smoothstep(0.0, _CoreWidth, d);
                float halo = (1.0 - smoothstep(0.0, _HaloWidth, d)) * 0.25;
                float fork = (1.0 - smoothstep(0.0, _CoreWidth * 2.0, dFork)) * _ForkWeight;
                float energy = saturate(core + fork) ;
                float alpha = saturate((core + halo + fork) * endFade);
                float3 rgb = lerp(_ColorCool.rgb, _ColorHot.rgb, energy);
                rgb = lerp(rgb, _ColorPrimary.rgb, halo * (1.0 - energy));
                half4 color = half4(rgb * _Intensity, alpha);
                return VfxApplyStyleStage(color, uv, d);
            }
            ENDHLSL
        }
    }
}

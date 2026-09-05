// Variant: mat_ring_polar (sg_flow_polar + ring SDF). Expanding / rotating ring
// with angular tiling; serves shock / ground / edge / frame roles. Progress
// drives the expansion so the controller can replay it deterministically.
Shader "VFXComposer/TechniqueFamilies/RingPolar"
{
    Properties
    {
        [HDR] _ColorPrimary("Palette Primary", Color) = (1, 0.6, 0.2, 1)
        [HDR] _ColorSecondary("Palette Secondary", Color) = (1, 0.8, 0.4, 1)
        [HDR] _ColorHot("Palette Hot", Color) = (3, 2.6, 1.6, 1)
        [HDR] _ColorCool("Palette Cool", Color) = (0.35, 0.12, 0.03, 1)
        [HDR] _ColorResidue("Palette Residue", Color) = (0.1, 0.06, 0.03, 1)
        _Intensity("Intensity", Range(0, 2)) = 1
        _Speed("Speed", Range(0.25, 3)) = 1
        _Seed("Seed", Float) = 0
        _Progress("Progress (expansion)", Range(0, 1)) = 0.5
        _RingWidth("Ring Width", Range(0.01, 0.5)) = 0.12
        _RingCount("Ring Count", Range(1, 4)) = 1
        _AngularSpeed("Angular Speed (turns/s)", Range(-2, 2)) = 0.2
        _AngularTiling("Angular Tiling (0 solid)", Range(0, 32)) = 0
        _DutyCycle("Angular Duty Cycle", Range(0.1, 1)) = 0.7
        _EdgeBias("Leading Edge Bias", Range(0, 1)) = 0.6
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
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+5" "RenderPipeline" = "UniversalPipeline" }
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
            float _Progress;
            float _RingWidth;
            float _RingCount;
            float _AngularSpeed;
            float _AngularTiling;
            float _DutyCycle;
            float _EdgeBias;
            CBUFFER_END

            #include "Includes/VfxStyleStage.hlsl"

            half4 Frag(VfxVaryings input) : SV_Target
            {
                float2 uv = VfxStyleUV(input.uv);
                float t = VfxStyleTime(_Time.y * _Speed);
                float2 polar = VfxPolar(uv);
                float theta = frac(polar.y + t * _AngularSpeed);

                float energy = 0.0;
                float edgeDist = 1.0;
                int count = (int)max(_RingCount, 1.0);
                [unroll(4)]
                for (int k = 0; k < 4; k++)
                {
                    if (k >= count) break;
                    // Trailing rings lag behind the leading one.
                    float lag = k * 0.18;
                    float radius = saturate(_Progress - lag) * 0.95;
                    if (radius <= 0.001) continue;
                    float width = _RingWidth * (1.0 - 0.2 * k);
                    float d = abs(polar.x - radius);
                    // Leading edge sharper than the trailing edge.
                    float inner = smoothstep(width, width * (1.0 - _EdgeBias * 0.8), polar.x - radius < 0 ? d : d * 0.6);
                    float ring = 1.0 - smoothstep(0.0, width, d);
                    ring = max(ring, inner * ring);
                    // Fade rings out as they reach full expansion.
                    ring *= 1.0 - smoothstep(0.75, 1.0, radius / 0.95);
                    energy = max(energy, ring * (1.0 - 0.3 * k));
                    edgeDist = min(edgeDist, d);
                }

                // Angular tiling: segments with a duty cycle (0 keeps a solid ring).
                if (_AngularTiling > 0.5)
                {
                    float seg = frac(theta * _AngularTiling);
                    float segMask = smoothstep(0.0, 0.08, seg) * smoothstep(_DutyCycle, _DutyCycle - 0.08, seg);
                    energy *= segMask;
                }

                float alpha = saturate(energy);
                float3 rgb = VfxHdrGrade(energy, _ColorCool.rgb, _ColorPrimary.rgb, _ColorSecondary.rgb, _ColorHot.rgb, _ColorResidue.rgb, 0.25) * _Intensity;
                half4 color = half4(rgb, alpha);
                return VfxApplyStyleStage(color, uv, alpha < 0.001 ? -1.0 : edgeDist);
            }
            ENDHLSL
        }
    }
}

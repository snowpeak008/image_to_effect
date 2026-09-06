// Variant: mat_liquid_blob (TECH_FAMILY_SPEC_MATERIAL.md §8) — smooth-minimum
// union of animated SDF blobs (VFX_SmoothminUnion body, gate M-10) combined with
// the dual-route refraction slot (VFX_RefractDual, ADR-010 §4bis-1):
//   _REFRACT_SCENECOLOR on  -> the blob interior refracts _CameraOpaqueTexture,
//                              offset driven by the SDF gradient (surface normal).
//   off                     -> the same gradient drives a specular tension line
//                              (degraded route; no background displacement).
// One material, both routes in player builds (multi_compile_local, gate RF-1);
// switching the keyword never changes the parameter surface.
Shader "VFXComposer/TechniqueFamilies/LiquidBlobRefract"
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
        _RefractStrength("Refract Strength", Range(0, 0.2)) = 0.05
        _RefractBlend("Refract Blend", Range(0, 1)) = 0.75
        _SpecPower("Spec Power (degraded route)", Range(1, 64)) = 24
        _SpecGain("Spec Gain (degraded route)", Range(0, 4)) = 1.2
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
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-5" "RenderPipeline" = "UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            Name "Forward"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_local _ _REFRACT_SCENECOLOR
            #pragma multi_compile_local _ _STYLESTAGE_CARTOON _STYLESTAGE_PIXEL
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Includes/VfxCommon.hlsl"
#if defined(_REFRACT_SCENECOLOR)
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
#endif

            CBUFFER_START(UnityPerMaterial)
            VFX_COMMON_UNIFORMS
            float _BlobCount;
            float _SmoothK;
            float _HighlightWidth;
            float _RefractStrength;
            float _RefractBlend;
            float _SpecPower;
            float _SpecGain;
            CBUFFER_END

            #include "Includes/VfxStyleStage.hlsl"

            struct BlobVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float4 screenPos : TEXCOORD2;
                half4 color : COLOR;
            };

            BlobVaryings Vert(VfxAttributes input)
            {
                BlobVaryings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.screenPos = ComputeScreenPos(positionInputs.positionCS);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            float VfxSmoothMin(float a, float b, float k)
            {
                float h = saturate(0.5 + 0.5 * (b - a) / k);
                return lerp(b, a, h) - k * h * (1.0 - h);
            }

            float BlobField(float2 p, float t, float seed)
            {
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
                return d;
            }

            half4 Frag(BlobVaryings input) : SV_Target
            {
                float2 uv = VfxStyleUV(input.uv);
                float t = VfxStyleTime(_Time.y * _Speed);
                float seed = VfxHash11(_Seed) * 63.0;
                float2 p = (uv - 0.5) * 2.0;
                float d = BlobField(p, t, seed);

                // The SDF gradient is the liquid surface normal shared by both routes.
                float2 e = float2(0.01, 0.0);
                float2 grad = float2(
                    BlobField(p + e.xy, t, seed) - BlobField(p - e.xy, t, seed),
                    BlobField(p + e.yx, t, seed) - BlobField(p - e.yx, t, seed)) / (2.0 * e.x);

                float alpha = 1.0 - smoothstep(0.0, 0.015, d);
                float highlight = 1.0 - smoothstep(0.0, _HighlightWidth, abs(d + _HighlightWidth * 2.0));
                float body = saturate(-d * 2.5);
                float3 rgb;
#if defined(_REFRACT_SCENECOLOR)
                // True refraction: the blob interior displaces the opaque scene by the
                // surface gradient (stronger near the rim, calm in the body).
                float2 screenUv = input.screenPos.xy / max(input.screenPos.w, 1e-5);
                float rim = 1.0 - saturate(-d * 4.0);
                float2 offset = grad * _RefractStrength * (0.35 + 0.65 * rim);
                float3 scene = SampleSceneColor(screenUv + offset);
                float3 tint = lerp(_ColorPrimary.rgb, _ColorCool.rgb, body);
                rgb = lerp(tint, scene * tint, _RefractBlend);
                rgb = lerp(rgb, _ColorHot.rgb, highlight);
                rgb *= _Intensity;
                alpha = max(alpha * 0.85, alpha * 0.6);
#else
                // Degraded route: the same gradient drives a specular tension streak.
                float3 normalApprox = normalize(float3(-grad, 1.0));
                float3 lightDirApprox = normalize(float3(0.4, 0.8, -0.45));
                float spec = pow(saturate(dot(normalApprox, lightDirApprox)), _SpecPower) * _SpecGain;
                float3 tint = lerp(_ColorPrimary.rgb, _ColorCool.rgb, body);
                rgb = lerp(tint, _ColorHot.rgb, highlight);
                rgb += spec * _ColorHot.rgb * saturate(-d * 3.0);
                rgb *= _Intensity;
                alpha *= 0.85;
#endif
                half4 color = half4(rgb, saturate(alpha));
                return VfxApplyStyleStage(color, uv, max(-d, 0.0));
            }
            ENDHLSL
        }
    }
}

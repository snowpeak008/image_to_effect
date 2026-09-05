// Variant: refraction dual-route (TECH_FAMILY_SPEC_MATERIAL.md section 6,
// ADR-010 section 4bis-1). One material, two routes on multi_compile_local:
//   _REFRACT_SCENECOLOR on  -> samples _CameraOpaqueTexture with a normal-driven
//                              screen-UV offset (true refraction).
//   off                     -> the same procedural normal drives a specular
//                              highlight + edge modulation (degraded route).
// Both routes share the same normalTS upstream and the same parameter surface
// (refractStrength / refractBlend / specPower / specGain), so switching the
// keyword at runtime (VfxUrpCapabilityProbe) never errors and never changes
// the recipe-facing parameters. multi_compile (not shader_feature) keeps both
// variants alive in player builds.
Shader "VFXComposer/TechniqueFamilies/RefractDual"
{
    Properties
    {
        [HDR] _ColorPrimary("Palette Primary", Color) = (0.4, 0.7, 1, 1)
        [HDR] _ColorSecondary("Palette Secondary", Color) = (0.7, 0.9, 1, 1)
        [HDR] _ColorHot("Palette Hot", Color) = (1.6, 1.9, 2.2, 1)
        [HDR] _ColorCool("Palette Cool", Color) = (0.05, 0.15, 0.3, 1)
        [HDR] _ColorResidue("Palette Residue", Color) = (0.1, 0.12, 0.16, 1)
        _Intensity("Intensity", Range(0, 2)) = 1
        _Speed("Speed", Range(0.25, 3)) = 1
        _Seed("Seed", Float) = 0
        _RefractStrength("Refract Strength", Range(0, 0.2)) = 0.05
        _RefractBlend("Refract Blend", Range(0, 1)) = 0.75
        _SpecPower("Spec Power (degraded route)", Range(1, 64)) = 24
        _SpecGain("Spec Gain (degraded route)", Range(0, 4)) = 1.2
        _NormalFreq("Normal Noise Frequency", Range(0.5, 12)) = 4
        _NormalAmp("Normal Noise Amplitude", Range(0, 1)) = 0.5
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
            float _RefractStrength;
            float _RefractBlend;
            float _SpecPower;
            float _SpecGain;
            float _NormalFreq;
            float _NormalAmp;
            CBUFFER_END

            #include "Includes/VfxStyleStage.hlsl"

            struct RefractVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float4 screenPos : TEXCOORD3;
                half4 color : COLOR;
            };

            RefractVaryings Vert(VfxAttributes input)
            {
                RefractVaryings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.screenPos = ComputeScreenPos(positionInputs.positionCS);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            // Shared upstream: procedural tangent-space normal from FBM gradient
            // (central differences). Both routes consume exactly this.
            float3 ProceduralNormalTS(float2 uv, float t)
            {
                float2 o = float2(0.01, 0.0);
                float2 q = uv * _NormalFreq + t * 0.35 + VfxHash11(_Seed) * 11.0;
                float h0 = VfxFbm(q, 2);
                float hx = VfxFbm(q + o.xy, 2);
                float hy = VfxFbm(q + o.yx, 2);
                float2 grad = float2(hx - h0, hy - h0) / o.x;
                return normalize(float3(-grad * _NormalAmp, 1.0));
            }

            half4 Frag(RefractVaryings input) : SV_Target
            {
                float2 uv = VfxStyleUV(input.uv);
                float t = VfxStyleTime(_Time.y * _Speed);
                float3 normalTS = ProceduralNormalTS(uv, t);

                float2 polar = VfxPolar(uv);
                float mask = 1.0 - smoothstep(0.7, 1.0, polar.x);
                float film = VfxFbm(uv * 3.0 - t * 0.2 + VfxHash11(_Seed + 3.0) * 5.0, 2);
                float alpha = mask * lerp(0.25, 0.6, film);

                float3 rgb;
#if defined(_REFRACT_SCENECOLOR)
                // True refraction: perturb screen UV by the tangent normal,
                // sample the opaque texture (copied before the transparent pass,
                // so no transparent layer -- including self -- is ever sampled).
                float2 screenUv = input.screenPos.xy / max(input.screenPos.w, 1e-5);
                float invDist = 1.0 / max(length(_WorldSpaceCameraPos - input.positionWS), 1.0);
                float2 offset = normalTS.xy * _RefractStrength * saturate(invDist * 4.0);
                float3 scene = SampleSceneColor(screenUv + offset);
                float3 tint = VfxPaletteRamp(film, _ColorCool.rgb, _ColorPrimary.rgb, _ColorSecondary.rgb, _ColorHot.rgb);
                rgb = lerp(tint, scene * tint, _RefractBlend) * _Intensity;
                alpha = max(alpha, 0.6 * mask);
#else
                // Degraded route: the same normal drives a fake specular streak and
                // edge brightness so the surface still reads as "not flat".
                float3 viewWS = normalize(_WorldSpaceCameraPos - input.positionWS);
                float3 perturbed = normalize(input.normalWS + float3(normalTS.xy, 0) * 0.8);
                float3 lightDirApprox = normalize(float3(0.4, 0.8, -0.45));
                float spec = pow(saturate(dot(reflect(-viewWS, perturbed), lightDirApprox)), _SpecPower) * _SpecGain;
                float edgeMod = saturate(length(normalTS.xy) * 2.0) * _RefractStrength * 10.0 * _RefractBlend;
                float3 tint = VfxPaletteRamp(film + edgeMod, _ColorCool.rgb, _ColorPrimary.rgb, _ColorSecondary.rgb, _ColorHot.rgb);
                rgb = (tint + spec * _ColorHot.rgb) * _Intensity;
#endif
                half4 color = half4(rgb, saturate(alpha));
                return VfxApplyStyleStage(color, uv, alpha < 0.001 ? -1.0 : alpha * 0.3);
            }
            ENDHLSL
        }
    }
}

// Variant: fresnel_edge. Dual path (gate M-8): the 3D path uses the true
// normal-view fresnel; the 2D path approximates rim with SDF edge distance
// from the quad centre. Selected by the _FRESNEL_SDF2D local keyword.
Shader "VFXComposer/TechniqueFamilies/FresnelEdge"
{
    Properties
    {
        [HDR] _ColorPrimary("Palette Primary", Color) = (0.55, 0.85, 1, 1)
        [HDR] _ColorSecondary("Palette Secondary", Color) = (0.85, 0.95, 1, 1)
        [HDR] _ColorHot("Palette Hot", Color) = (1.6, 2, 2.4, 1)
        [HDR] _ColorCool("Palette Cool", Color) = (0.15, 0.35, 0.6, 1)
        [HDR] _ColorResidue("Palette Residue", Color) = (0.8, 0.9, 0.95, 1)
        _Intensity("Intensity", Range(0, 2)) = 1
        _Speed("Speed", Range(0.25, 3)) = 1
        _Seed("Seed", Float) = 0
        _Power("Fresnel Power", Range(0.5, 8)) = 3
        _EdgeColorSlot("Edge Color Slot (0 hot 1 primary 2 cool)", Range(0, 2)) = 0
        _RimOnly("Rim Only", Range(0, 1)) = 0
        _InnerOpacity("Inner Opacity", Range(0, 1)) = 0.15
        [Toggle(_FRESNEL_SDF2D)] _Sdf2D("2D SDF Path", Float) = 0
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
        Cull Back
        Pass
        {
            Name "Forward"
            HLSLPROGRAM
            #pragma vertex VfxVert
            #pragma fragment Frag
            #pragma multi_compile_local _ _STYLESTAGE_CARTOON _STYLESTAGE_PIXEL
            #pragma shader_feature_local _FRESNEL_SDF2D
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Includes/VfxCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
            VFX_COMMON_UNIFORMS
            float _Power;
            float _EdgeColorSlot;
            float _RimOnly;
            float _InnerOpacity;
            float _Sdf2D;
            CBUFFER_END

            #include "Includes/VfxStyleStage.hlsl"

            half4 Frag(VfxVaryings input) : SV_Target
            {
                float2 uv = VfxStyleUV(input.uv);
                float rim;
#if defined(_FRESNEL_SDF2D)
                // 2D pseudo-fresnel: distance to the shape edge (circle inscribed in the quad).
                float r = length((uv - 0.5) * 2.0);
                rim = pow(saturate(r), _Power) * step(r, 1.0);
#else
                float3 viewDir = normalize(GetWorldSpaceViewDir(input.positionWS));
                float ndv = saturate(dot(normalize(input.normalWS), viewDir));
                rim = pow(1.0 - ndv, _Power);
#endif
                float3 edgeColor = _EdgeColorSlot < 0.5 ? _ColorHot.rgb : (_EdgeColorSlot < 1.5 ? _ColorPrimary.rgb : _ColorCool.rgb);
                float innerAlpha = _RimOnly > 0.5 ? 0.0 : _InnerOpacity;
                float alpha = saturate(rim + innerAlpha);
#if defined(_FRESNEL_SDF2D)
                alpha *= step(length((uv - 0.5) * 2.0), 1.0);
#endif
                float3 rgb = lerp(_ColorPrimary.rgb * 0.4, edgeColor, rim) * _Intensity;
                half4 color = half4(rgb, alpha);
                return VfxApplyStyleStage(color, uv, 1.0 - rim);
            }
            ENDHLSL
        }
    }
}

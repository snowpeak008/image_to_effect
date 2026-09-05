// Variant: vertex_displace. Six displacement modes (gate M-9): tongue / bulge /
// sway / rise / tension / gerstner. amplitude 0 is an exact identity so tier
// truncation is safe. Applies over an FBM-shaded surface.
Shader "VFXComposer/TechniqueFamilies/VertexDisplace"
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
        _Mode("Mode (0 tongue 1 bulge 2 sway 3 rise 4 tension 5 gerstner)", Range(0, 5)) = 5
        _Amplitude("Amplitude", Range(0, 1)) = 0.15
        _Frequency("Frequency", Range(0.1, 12)) = 2
        _AxisMask("Axis Mask", Vector) = (0, 1, 0, 0)
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
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_local _ _STYLESTAGE_CARTOON _STYLESTAGE_PIXEL
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Includes/VfxCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
            VFX_COMMON_UNIFORMS
            float _Mode;
            float _Amplitude;
            float _Frequency;
            float4 _AxisMask;
            CBUFFER_END

            #include "Includes/VfxStyleStage.hlsl"

            VfxVaryings Vert(VfxAttributes input)
            {
                float t = _Time.y * _Speed;
                float3 pos = input.positionOS.xyz;
                float3 axis = normalize(_AxisMask.xyz + float3(0, 1e-4, 0));
                float n = VfxValueNoise(input.uv * _Frequency + t + VfxHash11(_Seed) * 13.0) - 0.5;
                float3 offset = float3(0, 0, 0);
                if (_Mode < 0.5)        // tongue: top vertices stretch along the axis by noise
                    offset = axis * n * input.uv.y * 2.0;
                else if (_Mode < 1.5)   // bulge: outward along the normal, slow pulse
                    offset = input.normalOS * (0.5 + 0.5 * sin(t * _Frequency)) * n;
                else if (_Mode < 2.5)   // sway: whole surface bends, amplitude grows with V
                    offset = axis * sin(t * _Frequency + input.uv.y * 3.0) * input.uv.y;
                else if (_Mode < 3.5)   // rise: vertices ratchet upward with noise
                    offset = axis * (0.5 + n) * frac(t * 0.25);
                else if (_Mode < 4.5)   // tension: high-frequency jitter along the normal
                    offset = input.normalOS * n * sin(t * _Frequency * 6.0);
                else                    // gerstner: classic rolling wave on UV.x
                {
                    float k = _Frequency;
                    float phase = k * input.uv.x - t * 2.0;
                    offset = float3(cos(phase) * 0.3, sin(phase), 0) ;
                }
                pos += offset * _Amplitude;
                VfxAttributes displaced = input;
                displaced.positionOS = float4(pos, 1.0);
                return VfxVert(displaced);
            }

            half4 Frag(VfxVaryings input) : SV_Target
            {
                float2 uv = VfxStyleUV(input.uv);
                float t = VfxStyleTime(_Time.y * _Speed);
                float field = VfxFbm(uv * 4.0 + t * 0.2 + VfxHash11(_Seed) * 7.0, 3);
                float alpha = smoothstep(0.3, 0.55, field) * 0.85;
                float3 rgb = VfxPaletteRamp(field, _ColorCool.rgb, _ColorPrimary.rgb, _ColorSecondary.rgb, _ColorHot.rgb) * _Intensity;
                half4 color = half4(rgb, alpha);
                return VfxApplyStyleStage(color, uv, alpha > 0.001 ? field * 0.1 : -1.0);
            }
            ENDHLSL
        }
    }
}

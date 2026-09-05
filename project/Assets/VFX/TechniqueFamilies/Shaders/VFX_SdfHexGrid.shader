// Variant: sdf_hex_grid. Procedural hex lattice with cell-by-cell stepped
// lighting (stepped time, gate M-4) and an emissive edge outline.
Shader "VFXComposer/TechniqueFamilies/SdfHexGrid"
{
    Properties
    {
        [HDR] _ColorPrimary("Palette Primary", Color) = (0.1, 0.85, 1, 1)
        [HDR] _ColorSecondary("Palette Secondary", Color) = (1, 0.55, 0.1, 1)
        [HDR] _ColorHot("Palette Hot", Color) = (2, 3.5, 4, 1)
        [HDR] _ColorCool("Palette Cool", Color) = (0.02, 0.15, 0.25, 1)
        [HDR] _ColorResidue("Palette Residue", Color) = (0.2, 0.4, 0.5, 1)
        _Intensity("Intensity", Range(0, 2)) = 1
        _Speed("Speed", Range(0.25, 3)) = 1
        _Seed("Seed", Float) = 0
        _CellSize("Cell Size", Range(0.03, 0.4)) = 0.12
        _LitRatio("Lit Cell Ratio", Range(0, 1)) = 0.55
        _EdgeWidth("Edge Width", Range(0.002, 0.05)) = 0.012
        _StepRate("Step Rate (Hz)", Range(0.5, 20)) = 4
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
            float _CellSize;
            float _LitRatio;
            float _EdgeWidth;
            float _StepRate;
            CBUFFER_END

            #include "Includes/VfxStyleStage.hlsl"

            // Hex tiling: returns cell id (xy) and distance to the cell edge (z).
            float3 VfxHexCell(float2 p)
            {
                const float2 s = float2(1.0, 1.7320508);
                float4 hC = floor(float4(p, p - float2(0.5, 1.0)) / s.xyxy) + 0.5;
                float4 h = float4(p - hC.xy * s, p - (hC.zw + 0.5) * s);
                float2 gv = dot(h.xy, h.xy) < dot(h.zw, h.zw) ? h.xy : h.zw;
                float2 id = dot(h.xy, h.xy) < dot(h.zw, h.zw) ? hC.xy : hC.zw + 0.5;
                float2 a = abs(gv);
                float edge = 0.5 - max(dot(a, normalize(s)), a.x);
                return float3(id, edge);
            }

            half4 Frag(VfxVaryings input) : SV_Target
            {
                float2 uv = VfxStyleUV(input.uv);
                float stepT = floor(_Time.y * _Speed * _StepRate);
                float3 hex = VfxHexCell(uv / _CellSize);
                float cellHash = VfxHash21(hex.xy + VfxHash11(_Seed) * 53.0);
                // Cell-by-cell lighting: which cells are lit re-rolls at the step rate.
                float lit = step(1.0 - _LitRatio, frac(cellHash + VfxHash11(stepT) * 0.618));
                float edgeDist = hex.z * _CellSize;
                float edgeLine = 1.0 - smoothstep(0.0, _EdgeWidth, edgeDist);
                float2 polar = VfxPolar(uv);
                float mask = 1.0 - smoothstep(0.8, 1.0, polar.x);
                float fillEnergy = lit * 0.55;
                float energy = saturate(fillEnergy + edgeLine * 0.9);
                float alpha = saturate(fillEnergy * 0.5 + edgeLine) * mask;
                float3 rgb = lerp(_ColorCool.rgb, lerp(_ColorPrimary.rgb, _ColorHot.rgb, edgeLine), energy) * _Intensity;
                half4 color = half4(rgb, alpha);
                return VfxApplyStyleStage(color, uv, edgeDist);
            }
            ENDHLSL
        }
    }
}

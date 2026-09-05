// Variant: sdf_crack_branch. Recursive branching crack lines (Lichtenberg-like)
// grown from a seed point; progress 0..1 advances the crack front (gate M-3).
Shader "VFXComposer/TechniqueFamilies/SdfCrackBranch"
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
        _BranchDepth("Branch Depth", Range(1, 4)) = 3
        _BranchAngle("Branch Angle (rad)", Range(0.2, 1.4)) = 0.7
        _CrackWidth("Crack Width", Range(0.002, 0.05)) = 0.012
        _Progress("Progress", Range(0, 1)) = 1
        _SeedPoint("Seed Point (uv)", Vector) = (0.5, 0.5, 0, 0)
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
            float _BranchDepth;
            float _BranchAngle;
            float _CrackWidth;
            float _Progress;
            float4 _SeedPoint;
            CBUFFER_END

            #include "Includes/VfxStyleStage.hlsl"

            float VfxSegmentDist(float2 p, float2 a, float2 b)
            {
                float2 pa = p - a;
                float2 ba = b - a;
                float h = saturate(dot(pa, ba) / max(dot(ba, ba), 1e-6));
                return length(pa - ba * h);
            }

            half4 Frag(VfxVaryings input) : SV_Target
            {
                float2 uv = VfxStyleUV(input.uv);
                float seed = VfxHash11(_Seed) * 419.0;
                float d = 1e5;
                float reach = 1e5; // parametric distance along the tree, for progress gating
                // Iterative branch tree: trunk count 3, each trunk forks per depth level.
                // Loop bounds are static (shader-friendly); depth gates contributions.
                for (int trunk = 0; trunk < 3; trunk++)
                {
                    float baseAng = (trunk / 3.0 + VfxHash11(seed + trunk)) * 6.28318530718;
                    float2 pos = _SeedPoint.xy;
                    float ang = baseAng;
                    float segLen = 0.16;
                    float travelled = 0.0;
                    for (int depth = 0; depth < 4; depth++)
                    {
                        if (depth >= (int)_BranchDepth) break;
                        // Two forks per level; the main fork continues, the side fork is shorter.
                        for (int fork = 0; fork < 2; fork++)
                        {
                            float fa = ang + (fork == 0 ? 1.0 : -1.0) * _BranchAngle * (0.4 + VfxHash11(seed + trunk * 17 + depth * 5 + fork));
                            float fl = segLen * (fork == 0 ? 1.0 : 0.6);
                            float2 tip = pos + float2(cos(fa), sin(fa)) * fl;
                            float segStart = travelled;
                            float segEnd = travelled + fl;
                            // progress gate: the segment is visible only up to the crack front
                            float front = _Progress * (0.16 * 4.0 * 1.6);
                            if (segStart < front)
                            {
                                float visible = saturate((front - segStart) / max(fl, 1e-4));
                                float2 visTip = lerp(pos, tip, visible);
                                float sd = VfxSegmentDist(uv, pos, visTip);
                                d = min(d, sd);
                            }
                            if (fork == 0) { pos = tip; ang = fa; travelled = segEnd; }
                        }
                        segLen *= 0.72;
                    }
                }
                float width = _CrackWidth;
                float alpha = 1.0 - smoothstep(0.0, width, d);
                float glow = (1.0 - smoothstep(0.0, width * 6.0, d)) * 0.25;
                float3 rgb = lerp(_ColorPrimary.rgb, _ColorHot.rgb, alpha) * _Intensity;
                half4 color = half4(rgb, saturate(alpha + glow));
                return VfxApplyStyleStage(color, uv, d);
            }
            ENDHLSL
        }
    }
}

// Variant: mat_shell_ripple (closes the T2B_IMPL_REPORT section 6-2 gap).
// Shield-shell surface material: fresnel edge + procedural cell pattern base,
// up to 4 simultaneous hit ripples expanding from controller-written local hit
// points (_HitData0..3: xyz local point, w start time vs _LocalTime), and a
// crack channel driven by _Integrity (1 intact -> 0 fully cracked) using the
// same voronoi-edge field. All procedural; no textures (SG-5).
Shader "VFXComposer/TechniqueFamilies/ShellRipple"
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
        _FresnelPower("Fresnel Power", Range(0.5, 8)) = 2.6
        _InnerOpacity("Inner Opacity", Range(0, 1)) = 0.12
        _CellDensity("Surface Cell Density", Range(0, 24)) = 8
        _CellLineWidth("Cell Line Width", Range(0.002, 0.08)) = 0.02
        _RippleSpeed("Ripple Speed (units/s)", Range(0.2, 8)) = 2.4
        _RippleWidth("Ripple Width", Range(0.02, 0.6)) = 0.16
        _RippleDuration("Ripple Duration (s)", Range(0.1, 1.5)) = 0.45
        _LocalTime("Local Time (controller-written)", Float) = 0
        _HitData0("Hit 0 (xyz local, w t0)", Vector) = (0, 0, 0, -1000)
        _HitData1("Hit 1", Vector) = (0, 0, 0, -1000)
        _HitData2("Hit 2", Vector) = (0, 0, 0, -1000)
        _HitData3("Hit 3", Vector) = (0, 0, 0, -1000)
        _Integrity("Integrity (1 intact)", Range(0, 1)) = 1
        _CrackWidth("Crack Width", Range(0.002, 0.1)) = 0.03
        _Sdf2DPath("2D SDF Rim Path", Range(0, 1)) = 0
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
            #pragma vertex VfxVertObjectPos
            #pragma fragment Frag
            #pragma multi_compile_local _ _STYLESTAGE_CARTOON _STYLESTAGE_PIXEL
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Includes/VfxCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
            VFX_COMMON_UNIFORMS
            float _FresnelPower;
            float _InnerOpacity;
            float _CellDensity;
            float _CellLineWidth;
            float _RippleSpeed;
            float _RippleWidth;
            float _RippleDuration;
            float _LocalTime;
            float4 _HitData0;
            float4 _HitData1;
            float4 _HitData2;
            float4 _HitData3;
            float _Integrity;
            float _CrackWidth;
            float _Sdf2DPath;
            CBUFFER_END

            #include "Includes/VfxStyleStage.hlsl"

            struct ShellVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float3 positionOS : TEXCOORD3;
                half4 color : COLOR;
            };

            ShellVaryings VfxVertObjectPos(VfxAttributes input)
            {
                ShellVaryings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionOS = input.positionOS.xyz;
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            float RippleContribution(float3 posOS, float4 hit)
            {
                float age = _LocalTime - hit.w;
                if (age < 0.0 || age > _RippleDuration) return 0.0;
                float radius = age * _RippleSpeed;
                float d = distance(posOS, hit.xyz);
                float band = 1.0 - smoothstep(0.0, _RippleWidth, abs(d - radius));
                float envelope = 1.0 - saturate(age / _RippleDuration);
                return band * envelope * envelope;
            }

            half4 Frag(ShellVaryings input) : SV_Target
            {
                float2 uv = VfxStyleUV(input.uv);
                float t = VfxStyleTime(_LocalTime * _Speed + _Time.y * 0.13 * _Speed);

                // Fresnel base: 3D view-normal path; 2D quads degrade to an SDF rim.
                float fres;
                if (_Sdf2DPath > 0.5)
                {
                    float r = length(uv * 2.0 - 1.0);
                    fres = pow(saturate(r), max(_FresnelPower, 0.5));
                }
                else
                {
                    float3 viewDir = normalize(GetWorldSpaceViewDir(input.positionWS));
                    fres = pow(1.0 - saturate(abs(dot(normalize(input.normalWS), viewDir))), _FresnelPower);
                }

                // Surface cell pattern (voronoi edge distance), slow drift.
                float cellEdge = 0.0;
                if (_CellDensity > 0.5)
                {
                    float2 p = uv * _CellDensity + _Seed * 7.31 + t * 0.05;
                    float2 cell = floor(p);
                    float best = 1e5, second = 1e5;
                    [unroll] for (int oy = -1; oy <= 1; oy++)
                    [unroll] for (int ox = -1; ox <= 1; ox++)
                    {
                        float2 site = cell + float2(ox, oy);
                        float2 sp = site + VfxHash22(site);
                        float d = distance(p, sp);
                        if (d < best) { second = best; best = d; }
                        else if (d < second) { second = d; }
                    }
                    float edgeDistCell = second - best;
                    cellEdge = 1.0 - smoothstep(0.0, max(_CellLineWidth * _CellDensity, 1e-3), edgeDistCell);
                }

                // Hit ripples (up to 4 simultaneous, gate spec maxSimultaneousHits).
                float ripple = RippleContribution(input.positionOS, _HitData0)
                             + RippleContribution(input.positionOS, _HitData1)
                             + RippleContribution(input.positionOS, _HitData2)
                             + RippleContribution(input.positionOS, _HitData3);
                ripple = saturate(ripple);

                // Crack channel: integrity threshold over the same cell-edge field.
                float crack = 0.0;
                if (_Integrity < 0.999 && _CellDensity > 0.5)
                {
                    float2 pc = uv * (_CellDensity * 1.7) + _Seed * 3.77;
                    float2 cellC = floor(pc);
                    float bestC = 1e5, secondC = 1e5;
                    [unroll] for (int cy = -1; cy <= 1; cy++)
                    [unroll] for (int cx = -1; cx <= 1; cx++)
                    {
                        float2 site = cellC + float2(cx, cy);
                        float2 sp = site + VfxHash22(site);
                        float d = distance(pc, sp);
                        if (d < bestC) { secondC = bestC; bestC = d; }
                        else if (d < secondC) { secondC = d; }
                    }
                    float edgeD = secondC - bestC;
                    // Cracks appear on cell borders whose random weight exceeds integrity.
                    float borderRand = VfxHash21(cellC + floor(_Seed));
                    float open = step(_Integrity, borderRand * 0.85 + 0.1);
                    crack = open * (1.0 - smoothstep(0.0, _CrackWidth * _CellDensity, edgeD));
                }

                float field = saturate(fres + cellEdge * 0.35 + ripple);
                float3 rgb = VfxPaletteRamp(field, _ColorCool.rgb, _ColorPrimary.rgb, _ColorSecondary.rgb, _ColorHot.rgb);
                rgb += _ColorHot.rgb * ripple * 1.5;
                rgb = lerp(rgb, _ColorHot.rgb, crack * 0.9);
                float alpha = saturate(_InnerOpacity + fres * 0.85 + cellEdge * 0.25 + ripple * 0.9 + crack * 0.8);
                half4 color = half4(rgb * _Intensity, alpha);
                return VfxApplyStyleStage(color, uv, 1.0 - fres);
            }
            ENDHLSL
        }
    }
}

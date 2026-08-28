Shader "Universal Render Pipeline/VFXComposer Area 2D Fire Unlit"
{
    Properties
    {
        [MainTexture] _BaseMap("Mask Atlas", 2D) = "white" {}
        _UVRect("Atlas UV Rect", Vector) = (0,0,1,1)
        _ColorLow("Low Heat", Color) = (0.28,0.015,0.005,0.8)
        _ColorMid("Main Flame", Color) = (1.0,0.16,0.015,1)
        _ColorHigh("Hot Edge", Color) = (1.0,0.78,0.12,1)
        _FlowSpeed("Flow Speed", Float) = 1
        _Intensity("Intensity", Float) = 1
        _GlobalAlpha("Global Alpha", Range(0,1)) = 1
        _RuntimeTime("Runtime Time", Float) = 0
        _RuntimePhase("Runtime Phase", Float) = 0
        _GeometryMode("Geometry Mode", Float) = 0
        [HideInInspector] _SrcBlend("Source Blend", Float) = 5
        [HideInInspector] _DstBlend("Destination Blend", Float) = 10
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "Queue"="Transparent"
            "RenderType"="Transparent"
        }

        Blend [_SrcBlend] [_DstBlend]
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 localUv : TEXCOORD0;
                half4 color : COLOR;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _UVRect;
                half4 _ColorLow;
                half4 _ColorMid;
                half4 _ColorHigh;
                half _FlowSpeed;
                half _Intensity;
                half _GlobalAlpha;
                float _RuntimeTime;
                float _RuntimePhase;
                half _GeometryMode;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.localUv = input.uv;
                output.color = input.color;
                return output;
            }

            float Hash21(float2 value)
            {
                return frac(sin(dot(value, float2(127.1, 311.7))) * 43758.5453);
            }

            float ValueNoise(float2 value)
            {
                float2 cell = floor(value);
                float2 fraction = frac(value);
                fraction = fraction * fraction * (3.0 - 2.0 * fraction);
                float a = Hash21(cell);
                float b = Hash21(cell + float2(1.0, 0.0));
                float c = Hash21(cell + float2(0.0, 1.0));
                float d = Hash21(cell + float2(1.0, 1.0));
                return lerp(lerp(a, b, fraction.x), lerp(c, d, fraction.x), fraction.y);
            }

            half4 frag(Varyings input) : SV_Target
            {
                const float tau = 6.28318530718;
                float2 uv = input.localUv;
                float phase = _RuntimeTime * _FlowSpeed + _RuntimePhase;

                // Every angular term is an integer multiple of 2*pi*U. Ring meshes
                // therefore sample the same value at U=0 and U=1.
                float periodicA = sin(tau * (uv.x * 7.0 + phase * 0.21));
                float periodicB = sin(tau * (uv.x * 13.0 - phase * 0.13));
                float2 warped = uv;
                warped.x += (periodicA * 0.018 + periodicB * 0.009) * saturate(uv.y * (1.0 - uv.y) * 4.0);
                warped.y = saturate(warped.y + 0.035 * sin(tau * (uv.x * 5.0 + phase * 0.17)));
                warped = saturate(warped);
                float2 atlasUv = _UVRect.xy + warped * _UVRect.zw;
                half atlasMask = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, atlasUv).a;

                float angle = tau * uv.x;
                float2 circleUv = float2(cos(angle), sin(angle));
                half organicNoise = ValueNoise(circleUv * 3.7 + float2(uv.y * 2.1 + phase * .13, uv.y * 3.2 - phase * .09));
                half fineNoise = ValueNoise(circleUv.yx * 7.3 + float2(uv.y * 5.1 - phase * .17, uv.y * 1.7 + phase * .11));
                half angularBreakup = saturate(0.54h + periodicA * 0.13h + periodicB * 0.08h + organicNoise * 0.31h + fineNoise * 0.12h);
                // Mesh layers use continuous procedural coverage. Stretching a
                // compact particle mask across them exposes compression contours
                // and reads as braided wire rather than fire.
                half bandEdge = smoothstep(0.0h, .16h, uv.y) * (1.0h - smoothstep(.76h, 1.0h, uv.y));
                half tongueEdge = smoothstep(0.0h, .13h, uv.x) * (1.0h - smoothstep(.87h, 1.0h, uv.x));
                half tongueLife = smoothstep(0.0h, .12h, uv.y) * (1.0h - smoothstep(.70h, 1.0h, uv.y));
                float2 discPosition = uv * 2.0 - 1.0;
                float discRadius = length(discPosition);
                float discAngle = atan2(discPosition.y, discPosition.x);
                half discNoise = ValueNoise(float2(discAngle * 2.35 + phase * .19, discRadius * 7.5 - phase * .31));
                half spiralA = .5h + .5h * sin(discAngle * 6.0 - discRadius * 12.0 + phase * 5.2 + discNoise * 1.65);
                half spiralB = .5h + .5h * sin(discAngle * 11.0 + discRadius * 18.0 - phase * 3.3);
                half fireField = saturate(.12h + spiralA * .54h + spiralB * .16h + discNoise * .27h);
                half turbulentBody = saturate(.34h + organicNoise * .48h + fineNoise * .24h);
                fireField *= lerp(.68h, 1.0h, turbulentBody);
                half filament = pow(saturate(.5h + .5h * sin(discAngle * 17.0 - discRadius * 31.0 + discNoise * 2.0 - phase * 1.7)), 9.0h);
                fireField = saturate(fireField + filament * .18h);
                half centerHeat = 1.0h - smoothstep(.025h, .16h, discRadius);
                fireField = max(fireField, centerHeat * .92h);
                half flameTip = pow(saturate(.5h + .5h * sin(discAngle * 14.0 - phase * 2.1 + discNoise * 3.0)), 6.0h);
                half outerRadius = .85h + .06h * sin(discAngle * 9.0 + phase * .7) + (discNoise - .5h) * .11h + flameTip * .095h;
                half discCoverage = (1.0h - smoothstep(outerRadius - .11h, outerRadius, discRadius));
                half simpleProcedural = _GeometryMode < 1.5h ? bandEdge : tongueEdge * tongueLife;
                half proceduralMask = _GeometryMode < 2.5h ? simpleProcedural : discCoverage;
                float2 particlePosition = uv * 2.0 - 1.0;
                half roundParticle = 1.0h - smoothstep(.35h, 1.0h, length(particlePosition));
                half diamondParticle = 1.0h - smoothstep(.30h, .96h, abs(particlePosition.x) + abs(particlePosition.y));
                half particleMask = lerp(roundParticle, diamondParticle, step(5.5h, _GeometryMode));
                half thinPulse = 1.0h - smoothstep(.07h, .20h, abs(uv.y - .48h));
                half mask = _GeometryMode < .5h ? atlasMask : (_GeometryMode < 4.5h ? proceduralMask : (_GeometryMode < 6.5h ? particleMask : thinPulse));
                half edgeHeat = pow(saturate(mask), 1.7h);
                half breakup = _GeometryMode < 2.5h ? angularBreakup : (_GeometryMode < 4.5h ? fireField : 1.0h);
                half bodyHeat = saturate(mask * breakup);
                half3 flame = lerp(_ColorLow.rgb, _ColorMid.rgb, saturate(bodyHeat * 1.35h));
                flame = lerp(flame, _ColorHigh.rgb, edgeHeat * edgeHeat * .62h);
                half discBreakup = lerp(.24h + fireField * .76h, smoothstep(.62h, .93h, fireField), step(3.5h, _GeometryMode));
                half geometryBreakup = _GeometryMode < 2.5h ? (.72h + organicNoise * .28h) : (_GeometryMode < 4.5h ? discBreakup : 1.0h);
                half alpha = pow(saturate(mask), 1.08h) * lerp(angularBreakup, geometryBreakup, saturate(_GeometryMode)) * _GlobalAlpha * input.color.a;
                half3 rgb = flame * input.color.rgb * _Intensity * lerp(0.64h, 1.14h, edgeHeat);
                rgb *= saturate(alpha * 1.55h + .025h);
                clip(alpha - 0.003h);
                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }
}

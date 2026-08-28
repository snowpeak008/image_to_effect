Shader "Universal Render Pipeline/VFXComposer Impact 2D Procedural Unlit"
{
    Properties
    {
        [MainColor] _BaseColor("Tint", Color) = (1,1,1,1)
        _ShapeMode("Shape Mode", Float) = 0
        [HideInInspector] _SrcBlend("Source Blend", Float) = 5
        [HideInInspector] _DstBlend("Destination Blend", Float) = 1
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
                float2 localUv : TEXCOORD1;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 localUv : TEXCOORD1;
                half4 color : COLOR;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _ShapeMode;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.localUv = input.localUv;
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
                float2 p = input.uv * 2.0 - 1.0;
                half alpha = 0.0h;
                half intensity = 1.0h;

                if (_ShapeMode < 0.5h)
                {
                    float radius = length(p);
                    half glow = pow(saturate(1.0 - radius), 2.3);
                    half horizontal = pow(saturate(1.0 - abs(p.y) * 11.0), 3.0) * saturate(1.0 - abs(p.x));
                    half vertical = pow(saturate(1.0 - abs(p.x) * 11.0), 3.0) * saturate(1.0 - abs(p.y));
                    half diagonalA = pow(saturate(1.0 - abs(p.x - p.y) * 8.0), 3.0) * saturate(1.0 - radius * 0.82);
                    half diagonalB = pow(saturate(1.0 - abs(p.x + p.y) * 8.0), 3.0) * saturate(1.0 - radius * 0.82);
                    alpha = max(glow, max(max(horizontal, vertical), max(diagonalA, diagonalB)) * 0.72h);
                    intensity = lerp(0.62h, 1.0h, saturate(alpha * 1.5h));
                }
                else if (_ShapeMode < 1.5h)
                {
                    // Geometry owns the broken silhouette. UV.x is the global angular
                    // coordinate (not per-segment), so frost detail flows across gaps
                    // instead of making every reusable segment look like a gear tooth.
                    half u = input.uv.x;
                    half v = input.uv.y;
                    half caps = 1.0h;
                    half innerRidge = pow(saturate(1.0h - abs(v - 0.08h) * 10.0h), 3.0h);
                    half outerRidge = pow(saturate(1.0h - abs(v - 0.82h) * 6.0h), 2.0h);
                    float angle = u * 6.2831853;
                    float2 circleUv = float2(cos(angle), sin(angle));
                    half noiseA = ValueNoise(circleUv * 5.3h + float2(v * 2.1h, v * 3.7h));
                    half noiseB = ValueNoise(circleUv.yx * 8.1h + float2(v * 4.2h + 3.7h, v * 1.8h + 1.3h));
                    half frostGrain = saturate(noiseA * 0.62h + noiseB * 0.38h);
                    half crystalVein = smoothstep(0.70h, 0.91h, noiseA) + smoothstep(0.76h, 0.94h, 1.0h - noiseB);
                    half radialFeather = smoothstep(0.0h, 0.035h, v) * smoothstep(0.0h, 0.055h, 1.0h - v);
                    half body = saturate(0.24h + frostGrain * 0.28h + crystalVein * 0.12h);
                    alpha = caps * radialFeather * saturate(body + innerRidge * 0.58h + outerRidge * 0.12h);
                    intensity = 0.70h + frostGrain * 0.42h + innerRidge * 0.84h + crystalVein * 0.18h;
                }
                else if (_ShapeMode < 2.5h)
                {
                    float radius = length(p);
                    half soft = pow(saturate(1.0 - radius), 2.0);
                    half breakup = 0.72h + 0.18h * sin(p.x * 8.0 + p.y * 5.0) + 0.10h * sin(p.x * 17.0 - p.y * 13.0);
                    alpha = soft * saturate(breakup);
                    intensity = 0.72h + 0.28h * soft;
                }
                else
                {
                    half diamond = saturate(1.0h - abs(p.x) - abs(p.y));
                    half horizontal = pow(saturate(1.0 - abs(p.y) * 8.0), 3.0) * saturate(1.0 - abs(p.x));
                    half vertical = pow(saturate(1.0 - abs(p.x) * 8.0), 3.0) * saturate(1.0 - abs(p.y));
                    alpha = max(diamond, max(horizontal, vertical) * 0.6h);
                    intensity = lerp(0.7h, 1.0h, diamond);
                }

                half4 result = input.color * _BaseColor;
                result.rgb *= intensity;
                result.a *= alpha;
                clip(result.a - 0.003h);
                return result;
            }
            ENDHLSL
        }
    }
}

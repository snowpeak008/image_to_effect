Shader "VFXComposer/URP/FlameParticle"
{
    Properties
    {
        [HDR] _BaseColor ("Base Color", Color) = (1, 0.12, 0.01, 1)
        [HDR] _TipColor ("Tip Color", Color) = (1, 0.85, 0.18, 1)
        _Emission ("Emission", Range(0, 8)) = 2
        _NoiseScale ("Noise Scale", Range(1, 24)) = 8
        _NoiseStrength ("Noise Strength", Range(0, 0.8)) = 0.25
        _EdgeSoftness ("Edge Softness", Range(0.01, 0.5)) = 0.12
        _ScrollSpeed ("Scroll Speed", Range(-8, 8)) = 1.4
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Source Blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Destination Blend", Float) = 10
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "Queue"="Transparent"
            "RenderPipeline"="UniversalPipeline"
            "IgnoreProjector"="True"
        }

        Pass
        {
            Name "FlameParticle"
            Blend [_SrcBlend] [_DstBlend]
            Cull Off
            ZWrite Off
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _TipColor;
                half _Emission;
                half _NoiseScale;
                half _NoiseStrength;
                half _EdgeSoftness;
                half _ScrollSpeed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float Hash21(float2 value)
            {
                value = frac(value * float2(123.34, 456.21));
                value += dot(value, value + 45.32);
                return frac(value.x * value.y);
            }

            float ValueNoise(float2 value)
            {
                float2 cell = floor(value);
                float2 local = frac(value);
                local = local * local * (3.0 - 2.0 * local);
                float a = Hash21(cell);
                float b = Hash21(cell + float2(1, 0));
                float c = Hash21(cell + float2(0, 1));
                float d = Hash21(cell + float2(1, 1));
                return lerp(lerp(a, b, local.x), lerp(c, d, local.x), local.y);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float y = saturate(input.uv.y);
                float timeOffset = _Time.y * _ScrollSpeed;
                float coarse = ValueNoise(float2(input.uv.x * _NoiseScale, y * _NoiseScale - timeOffset));
                float fine = ValueNoise(float2(input.uv.x * _NoiseScale * 2.17 + 3.7, y * _NoiseScale * 2.17 - timeOffset * 1.41));
                float noise = coarse * 0.68 + fine * 0.32;
                float sway = (noise - 0.5) * _NoiseStrength * (0.25 + y * 0.75);
                float center = 0.5 + sway;
                float halfWidth = lerp(0.46, 0.018, pow(y, 0.72));
                halfWidth *= lerp(0.78, 1.18, noise);
                float normalizedDistance = abs(input.uv.x - center) / max(halfWidth, 0.002);
                float edge = 1.0 - smoothstep(1.0 - _EdgeSoftness, 1.0, normalizedDistance);
                float baseFade = smoothstep(0.0, 0.075, y);
                float tipFade = 1.0 - smoothstep(0.82, 1.0, y);
                float breakup = smoothstep(0.08, 0.52, noise + (1.0 - y) * 0.28);
                float alpha = saturate(edge * baseFade * tipFade * breakup * input.color.a * _BaseColor.a);
                half3 gradient = lerp(_BaseColor.rgb, _TipColor.rgb, saturate(y * 0.82 + noise * 0.18));
                return half4(gradient * input.color.rgb * _Emission, alpha);
            }
            ENDHLSL
        }
    }
}

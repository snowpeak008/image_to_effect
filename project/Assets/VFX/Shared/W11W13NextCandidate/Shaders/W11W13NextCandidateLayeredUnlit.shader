Shader "VFXComposer/W11W13NextCandidate/LayeredUnlit"
{
    Properties
    {
        _PrimaryColor("Primary", Color) = (0.2,0.65,1,1)
        _SecondaryColor("Secondary", Color) = (0.72,0.92,1,1)
        _AccentColor("Accent", Color) = (1,1,1,1)
        _Intensity("Intensity", Range(0,4)) = 1
        _GlobalAlpha("Global Alpha", Range(0,1)) = 1
        _Phase("Phase", Float) = 0
        _FlashAmount("External Flash", Range(0,1)) = 0
        _HitTint("Hit Tint", Color) = (1,0.2,0.15,1)
        _HitEdgeWidth("Hit Edge Width", Range(0,1)) = 0.15
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Name "Forward"
            Blend SrcAlpha One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
            CBUFFER_START(UnityPerMaterial)
            float4 _PrimaryColor, _SecondaryColor, _AccentColor, _HitTint;
            float _Intensity, _GlobalAlpha, _Phase, _FlashAmount, _HitEdgeWidth;
            CBUFFER_END
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color.a <= 0.0001 && dot(input.color.rgb,input.color.rgb) <= 0.0001 ? float4(1,1,1,1) : input.color;
                return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                float2 centered = input.uv * 2 - 1;
                float radial = saturate(1 - length(centered));
                float edge = saturate(1 - abs(input.uv.y - .5) * 2);
                float flow = .76 + .24 * sin((_Phase + input.uv.x * 1.8 + input.uv.y * .37) * 6.28318);
                float3 color = lerp(_PrimaryColor.rgb, _SecondaryColor.rgb, saturate(input.uv.y + flow * .16));
                color = lerp(color, _AccentColor.rgb, pow(max(radial, edge), 5) * .3);
                float uvBoundaryDistance = min(min(input.uv.x, 1 - input.uv.x), min(input.uv.y, 1 - input.uv.y));
                float hitEdge = 1 - smoothstep(0, max(.001, _HitEdgeWidth), uvBoundaryDistance);
                float3 hitFlash = lerp(_AccentColor.rgb, _HitTint.rgb, hitEdge);
                color = lerp(color, hitFlash, saturate(_FlashAmount));
                float alpha = saturate(_GlobalAlpha * input.color.a * (.3 + .7 * max(radial, edge)));
                return half4(color * input.color.rgb * _Intensity * flow, alpha);
            }
            ENDHLSL
        }
    }
}

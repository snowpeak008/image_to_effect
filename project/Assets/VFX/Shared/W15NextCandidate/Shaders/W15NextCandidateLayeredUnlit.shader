Shader "VFXComposer/W15NextCandidate/LayeredUnlit"
{
    Properties
    {
        _PrimaryColor("Primary", Color) = (0.2,0.65,1,1)
        _SecondaryColor("Secondary", Color) = (0.75,0.95,1,1)
        _AccentColor("Accent", Color) = (1,1,1,1)
        _Intensity("Intensity", Range(0,8)) = 1
        _GlobalAlpha("Global Alpha", Range(0,1)) = 1
        _Phase("Phase", Float) = 0
        _Rarity("Rarity", Range(1,5)) = 1
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
            float4 _PrimaryColor, _SecondaryColor, _AccentColor;
            float _Intensity, _GlobalAlpha, _Phase, _Rarity;
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
                float edge = saturate(1-abs(input.uv.y-.5)*2);
                float shimmer = .72 + .28*sin((_Phase+input.uv.x*1.7)*6.28318);
                float3 gradient = lerp(_PrimaryColor.rgb,_SecondaryColor.rgb,saturate(input.uv.y+shimmer*.18));
                gradient = lerp(gradient,_AccentColor.rgb,pow(saturate(edge),3)*(.08+.035*_Rarity));
                float alpha = saturate(_GlobalAlpha * input.color.a * (.46+.54*edge));
                return half4(gradient * input.color.rgb * _Intensity * shimmer, alpha);
            }
            ENDHLSL
        }
    }
}

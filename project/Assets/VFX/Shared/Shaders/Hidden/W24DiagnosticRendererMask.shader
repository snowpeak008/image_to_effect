Shader "Hidden/VFXComposer/W24/RendererMask"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Name "W24RendererMask"
            ZWrite On
            ZTest LEqual
            Cull Off
            Blend Off
            ColorMask R

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4x4 _W24ViewProjection;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = mul(_W24ViewProjection, float4(positionWS, 1.0));
                return output;
            }
            float4 Frag(Varyings input) : SV_Target { return float4(1, 0, 0, 1); }
            ENDHLSL
        }
    }
}

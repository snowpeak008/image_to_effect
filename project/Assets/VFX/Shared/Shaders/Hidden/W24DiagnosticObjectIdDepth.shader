Shader "Hidden/VFXComposer/W24/ObjectIdDepth"
{
    Properties { _W24ObjectId ("W24 Object Id", Integer) = 1 }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        HLSLINCLUDE
        #pragma target 4.5
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            uint _W24ObjectId;
            float4x4 _W24ViewProjection;
            float4x4 _W24WorldToCamera;
        CBUFFER_END

        struct Attributes { float4 positionOS : POSITION; };
        struct Varyings { float4 positionCS : SV_POSITION; float3 positionVS : TEXCOORD0; };

        Varyings Vert(Attributes input)
        {
            Varyings output;
            float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
            output.positionCS = mul(_W24ViewProjection, float4(positionWS, 1.0));
            output.positionVS = mul(_W24WorldToCamera, float4(positionWS, 1.0)).xyz;
            return output;
        }

        uint FragObjectId(Varyings input) : SV_Target0 { return _W24ObjectId; }
        float FragLinearDepth(Varyings input) : SV_Target0 { return max(1e-6, -input.positionVS.z); }
        ENDHLSL

        Pass
        {
            Name "W24ObjectId"
            ZWrite On
            ZTest LEqual
            Cull Back
            Blend Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragObjectId
            ENDHLSL
        }

        Pass
        {
            Name "W24LinearDepth"
            ZWrite On
            ZTest LEqual
            Cull Back
            Blend Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragLinearDepth
            ENDHLSL
        }
    }
}

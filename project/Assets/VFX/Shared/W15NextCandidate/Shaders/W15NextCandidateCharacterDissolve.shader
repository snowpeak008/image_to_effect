Shader "VFXComposer/W15NextCandidate/CharacterDissolve"
{
    Properties
    {
        _PrimaryColor("Body", Color) = (0.12,0.18,0.28,1)
        _SecondaryColor("Body Highlight", Color) = (0.3,0.52,0.8,1)
        _DissolveEdgeColor("Dissolve Edge", Color) = (1,0.48,0.08,1)
        _Dissolve("Dissolve", Range(0,1)) = 0
        _DissolveMinY("Bounds Min Y", Float) = -1
        _DissolveMaxY("Bounds Max Y", Float) = 1
        _DissolveDirection("Direction", Float) = 1
        _GlobalAlpha("Global Alpha", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="AlphaTest" "RenderType"="TransparentCutout" }
        Pass
        {
            Name "Forward"
            ZWrite On
            Cull Back
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float3 normalWS:TEXCOORD1; };
            CBUFFER_START(UnityPerMaterial)
            float4 _PrimaryColor, _SecondaryColor, _DissolveEdgeColor;
            float _Dissolve, _DissolveMinY, _DissolveMaxY, _DissolveDirection, _GlobalAlpha;
            CBUFFER_END
            float Hash31(float3 p) { return frac(sin(dot(p,float3(12.9898,78.233,37.719)))*43758.5453); }
            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                float height = saturate((input.positionWS.y-_DissolveMinY)/max(.001,_DissolveMaxY-_DissolveMinY));
                if (_DissolveDirection < 0) height = 1-height;
                float radial = length(input.positionWS.xz)*.35;
                if (_DissolveDirection > 1.5) height = saturate(radial);
                float noise = (Hash31(floor(input.positionWS*18))-.5)*.12;
                float signedDistance = height + noise - _Dissolve;
                clip(signedDistance-.005);
                float edge = 1-smoothstep(.005,.075,signedDistance);
                float facing = .35+.65*abs(normalize(input.normalWS).z);
                float3 body = lerp(_PrimaryColor.rgb,_SecondaryColor.rgb,facing);
                float3 color = lerp(body,_DissolveEdgeColor.rgb,edge);
                return half4(color*_GlobalAlpha,1);
            }
            ENDHLSL
        }
    }
}

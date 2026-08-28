Shader "Universal Render Pipeline/VFXComposer Slash Reveal Unlit"
{
    Properties
    {
        [MainColor] _BaseColor("Color", Color) = (1, 0.2, 0.02, 0.86)
        _Reveal("Arc Reveal", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha One
        ZWrite Off
        Cull Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _Reveal;
            CBUFFER_END
            Varyings vert(Attributes input) { Varyings output; output.positionCS = TransformObjectToHClip(input.positionOS.xyz); output.uv = input.uv; return output; }
            half4 frag(Varyings input) : SV_Target { clip(_Reveal - input.uv.x); return _BaseColor; }
            ENDHLSL
        }
    }
}

Shader "Universal Render Pipeline/VFXComposer S15 Spark Atlas Unlit"
{
    Properties { [MainTexture] _BaseMap("Spark atlas", 2D) = "white" {} [MainColor] _BaseColor("Tint", Color) = (1,1,1,1) }
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
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial) half4 _BaseColor; CBUFFER_END
            Varyings vert(Attributes input) { Varyings output; output.positionCS = TransformObjectToHClip(input.positionOS.xyz); output.uv = input.uv; output.color = input.color; return output; }
            half4 frag(Varyings input) : SV_Target { half4 spark = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * input.color * _BaseColor; clip(spark.a - .004); return spark; }
            ENDHLSL
        }
    }
}

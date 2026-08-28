Shader "Universal Render Pipeline/VFXComposer S15 Painted Crescent Unlit"
{
    Properties
    {
        [MainTexture] _MainTex("Fiery crescent RGBA", 2D) = "white" {}
        _BreakupNoise("Breakup noise", 2D) = "white" {}
        [MainColor] _BaseColor("Tint", Color) = (1,1,1,1)
        _Reveal("Sweep reveal", Range(0,1)) = 0
        _Dissolve("Edge dissolve", Range(0,1)) = 0
        _NoiseScale("Noise scale", Range(0.1,8)) = 2
        _Emission("Emission", Range(0,3)) = 1.15
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
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
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_BreakupNoise); SAMPLER(sampler_BreakupNoise);
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _Reveal;
                float _Dissolve;
                float _NoiseScale;
                float _Emission;
            CBUFFER_END
            Varyings vert(Attributes input) { Varyings output; output.positionCS = TransformObjectToHClip(input.positionOS.xyz); output.uv = input.uv; return output; }
            half4 frag(Varyings input) : SV_Target
            {
                // The mapped texture itself is local VFX paint.  This polar progress follows its
                // lower-left ignition around the broad right-side turn to the upper terminus.
                float theta = atan2(input.uv.y - .48, input.uv.x - .44);
                float sweep = saturate((theta + 2.52) / 3.82);
                float reveal = smoothstep(sweep - .10, sweep + .025, _Reveal);
                half4 paint = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * _BaseColor;
                float noise = SAMPLE_TEXTURE2D(_BreakupNoise, sampler_BreakupNoise, input.uv * _NoiseScale + float2(_Reveal * .17, -_Reveal * .11)).r;
                float edge = smoothstep(_Dissolve - .22, _Dissolve + .30, noise);
                paint.a *= reveal * (1 - _Dissolve * edge);
                clip(paint.a - .004);
                return half4(paint.rgb * _Emission, paint.a);
            }
            ENDHLSL
        }
    }
}

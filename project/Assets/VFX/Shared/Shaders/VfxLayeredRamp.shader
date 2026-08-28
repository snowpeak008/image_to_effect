Shader "VFXComposer/Style/LayeredRamp"
{
    Properties
    {
        _PrimaryColor("Primary", Color) = (0.3,0.7,1,1)
        _SecondaryColor("Secondary", Color) = (0.8,0.95,1,1)
        _AccentColor("Accent", Color) = (1,1,1,1)
        _Intensity("Intensity", Range(0,8)) = 1
        _GlobalAlpha("Global Alpha", Range(0,1)) = 1
        _StyleMode("Style Mode", Range(0,7)) = 0
        _Phase("Phase", Range(0,1)) = 0
        _NoiseScale("Noise Scale", Range(.01,32)) = 1
        _Outline("Outline", Range(0,1)) = .1
        _ShadingSteps("Shading Steps", Range(1,8)) = 3
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend("Src Blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("Dst Blend", Float) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Name "Forward"
            Blend [_SrcBlend] [_DstBlend]
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; };
            CBUFFER_START(UnityPerMaterial)
            float4 _PrimaryColor, _SecondaryColor, _AccentColor;
            float _Intensity, _GlobalAlpha, _StyleMode, _Phase, _NoiseScale, _Outline, _ShadingSteps, _SrcBlend, _DstBlend;
            CBUFFER_END
            Varyings Vert(Attributes input) { Varyings output; output.positionCS=TransformObjectToHClip(input.positionOS.xyz); output.uv=input.uv; return output; }
            float Hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float Noise(float2 p) { float2 i=floor(p),f=frac(p); f=f*f*(3-2*f); return lerp(lerp(Hash(i),Hash(i+float2(1,0)),f.x),lerp(Hash(i+float2(0,1)),Hash(i+1),f.x),f.y); }
            half4 Frag(Varyings input):SV_Target
            {
                float2 p=input.uv*2-1; float radial=length(p); float angle=atan2(p.y,p.x); float n=Noise(input.uv*(4+_NoiseScale*3)+_Phase*2);
                float body=saturate(1-radial); float edge=saturate(1-abs(radial-.72)/max(.015,.18+_Outline*.2)); float filament=saturate(1-abs(sin(angle*6+radial*10+_Phase*6))*.7);
                float mode=floor(_StyleMode+.5); float mask=body; float3 color=lerp(_PrimaryColor.rgb,_SecondaryColor.rgb,saturate(body+n*.35));
                if(mode==1) { float steps=max(2,floor(_ShadingSteps+.5)); float q=floor(saturate(body+n*.18)*steps)/max(1,steps-1); color=lerp(_PrimaryColor.rgb,_SecondaryColor.rgb,q); mask=saturate(body+edge*.35); }
                else if(mode==2) { mask=saturate(body*(.45+.75*n)); color=lerp(_PrimaryColor.rgb,_SecondaryColor.rgb,n); }
                else if(mode==3) { float2 q=floor(input.uv*32)/32; float qp=length(q*2-1); mask=step(qp,.92)*step(.18,Hash(q*41)); color=lerp(_PrimaryColor.rgb,_AccentColor.rgb,step(.62,Hash(q*23))); }
                else if(mode==4) { mask=saturate(body*(.35+1.1*n)-step(.72,n)*.35); color=lerp(_PrimaryColor.rgb,_SecondaryColor.rgb,n*.35); }
                else if(mode==5) { float scan=.45+.55*step(.5,frac(input.uv.y*24+_Phase*8)); mask=saturate(edge+body*.28)*scan; color=lerp(_PrimaryColor.rgb,_AccentColor.rgb,edge); }
                else if(mode==6) { float rune=step(.78,filament*n); mask=saturate(edge*.7+rune); color=lerp(_PrimaryColor.rgb,_AccentColor.rgb,rune); }
                else if(mode==7) { mask=saturate(edge*1.2+filament*.35); color=lerp(_PrimaryColor.rgb,_AccentColor.rgb,edge); }
                float alpha=saturate(mask*_GlobalAlpha); return half4(color*_Intensity,alpha);
            }
            ENDHLSL
        }
    }
}

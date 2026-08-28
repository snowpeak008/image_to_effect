Shader "Universal Render Pipeline/VFXComposer Coverage Gallery B Unlit"
{
    Properties
    {
        _PrimaryColor("Primary", Color) = (0.2,0.7,1,1)
        _SecondaryColor("Secondary", Color) = (0.9,1,1,1)
        _GlobalAlpha("Global Alpha", Range(0,1)) = 1
        _Intensity("Intensity", Range(0,4)) = 1
        _ShapeMode("Shape Mode", Float) = 0
        _RuntimeTime("Runtime Time", Float) = 0
        _Progress("Progress", Range(0,1)) = 0
        _Pulse("Pulse", Range(0,1)) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend("Src Blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("Dst Blend", Float) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Name "CoverageGalleryB"
            Blend [_SrcBlend] [_DstBlend]
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; float2 uv : TEXCOORD2; float3 viewWS : TEXCOORD3; };
            CBUFFER_START(UnityPerMaterial)
            float4 _PrimaryColor, _SecondaryColor;
            float _GlobalAlpha, _Intensity, _ShapeMode, _RuntimeTime, _Progress, _Pulse, _SrcBlend, _DstBlend;
            CBUFFER_END

            float hash21(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float noise(float2 p)
            {
                float2 i=floor(p),f=frac(p); f=f*f*(3.0-2.0*f);
                return lerp(lerp(hash21(i),hash21(i+float2(1,0)),f.x),lerp(hash21(i+float2(0,1)),hash21(i+1),f.x),f.y);
            }
            float lineMask(float value,float width) { return 1.0-smoothstep(width,width*2.0,abs(value)); }

            Varyings vert(Attributes input)
            {
                Varyings output; VertexPositionInputs p=GetVertexPositionInputs(input.positionOS.xyz); output.positionCS=p.positionCS; output.positionWS=p.positionWS; output.normalWS=TransformObjectToWorldNormal(input.normalOS); output.uv=input.uv; output.viewWS=GetWorldSpaceNormalizeViewDir(p.positionWS); return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv=input.uv; float2 centered=uv-.5; float radial=length(centered)*2.0; float angle=atan2(centered.y,centered.x)/6.2831853+.5;
                float fresnel=pow(saturate(1.0-abs(dot(normalize(input.normalWS),normalize(input.viewWS)))),2.2);
                float movingNoise=noise(input.positionWS.xy*3.1+float2(_RuntimeTime*.32,-_RuntimeTime*.21));
                float mask=1.0; float hot=0.45;

                if (_ShapeMode < .5) { mask=saturate(.22+fresnel*1.25+movingNoise*.28); hot=fresnel; }
                else if (_ShapeMode < 1.5) { float broken=step(.2,frac(uv.x*10.0+_RuntimeTime*.36)); mask=saturate((.35+lineMask(uv.y-.5,.32))*broken); hot=lineMask(uv.y-.5,.18); }
                else if (_ShapeMode < 2.5) { float grid=max(lineMask(frac(uv.x*7)-.5,.055),lineMask(frac(uv.y*7)-.5,.055)); mask=saturate(grid*.72+fresnel*.55); hot=grid; }
                else if (_ShapeMode < 3.5) { float wave=sin(uv.x*24+_RuntimeTime*8)*.055; mask=lineMask(uv.y-.5-wave,.09)+lineMask(uv.y-.5+wave*.55,.035)*.55; hot=lineMask(uv.y-.5-wave,.032); }
                else if (_ShapeMode < 4.5) { float bands=lineMask(frac(angle*12+_RuntimeTime*.2)-.5,.18); mask=saturate(fresnel*.95+bands*(1-radial)*.48); hot=fresnel; }
                else if (_ShapeMode < 5.5) { float ring=lineMask(radial-.68,.11); float runes=step(.55,frac(angle*12-_RuntimeTime*.45))*lineMask(radial-.93,.16); mask=saturate(ring+runes*.85+(1-radial)*.16); hot=ring; }
                else if (_ShapeMode < 6.5) { float cloud=noise(uv*6+float2(_RuntimeTime*.1,-_RuntimeTime*.18)); mask=saturate((1-smoothstep(.48,1.1,radial))*cloud*1.5); hot=cloud*.45; }
                else if (_ShapeMode < 7.5) { float edge=smoothstep(.35,.95,radial); float warning=.65+.35*sin(_RuntimeTime*7); mask=edge*warning; hot=edge; }
                else if (_ShapeMode < 8.5) { float diamond=1-smoothstep(.13,.48,abs(centered.x)+abs(centered.y)); mask=diamond; hot=diamond; }
                else if (_ShapeMode < 9.5) { float body=(1-smoothstep(.18,.78,abs(centered.x)*1.8+abs(centered.y)*.42)); float tongues=.55+.45*sin(centered.y*22+noise(uv*5)*5-_RuntimeTime*3); mask=body*saturate(tongues); hot=body*(1-abs(centered.x)*2); }
                else if (_ShapeMode < 10.5) { float edgeFade=1-smoothstep(.58,.98,radial); float pool=noise(uv*4+float2(_RuntimeTime*.08,-_RuntimeTime*.05)); mask=edgeFade*smoothstep(.24,.68,pool); hot=edgeFade*smoothstep(.5,.82,pool); }
                else if (_ShapeMode < 11.5) { float facets=step(.72,frac((angle+radial*.18)*14))+step(.82,frac(radial*8)); mask=saturate(fresnel*.9+facets*.22); hot=saturate(fresnel+_Pulse*.7); }
                else { float edgeFade=smoothstep(0,.22,uv.x)*smoothstep(0,.22,uv.y)*smoothstep(0,.22,1-uv.x)*smoothstep(0,.22,1-uv.y); float ellipse=1-smoothstep(.48,1.0,length(centered*float2(1,.72))*2); float cloud=smoothstep(.30,.72,noise(uv*5+float2(_RuntimeTime*.08,-_RuntimeTime*.12))); mask=edgeFade*ellipse*cloud; hot=cloud*.32; }

                mask=saturate(mask*(.8+.2*movingNoise)+_Pulse*.18); float3 color=lerp(_PrimaryColor.rgb,_SecondaryColor.rgb,saturate(hot+.18*_Pulse)); float alpha=saturate(mask*_GlobalAlpha*_PrimaryColor.a); return half4(color*_Intensity,alpha);
            }
            ENDHLSL
        }
    }
}

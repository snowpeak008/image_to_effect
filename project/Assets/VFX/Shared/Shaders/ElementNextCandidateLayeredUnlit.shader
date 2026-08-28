Shader "VFXComposer/ElementNextCandidate/LayeredUnlit"
{
    Properties
    {
        _PrimaryColor("Primary", Color) = (1,.22,.02,1)
        _SecondaryColor("Secondary", Color) = (1,.75,.12,1)
        _AccentColor("Accent", Color) = (1,1,1,1)
        _Intensity("Intensity", Range(0,4)) = 1
        _GlobalAlpha("Global Alpha", Range(0,1)) = 1
        _Phase("Phase", Range(0,1)) = 0
        _SemanticProgress("Semantic Progress", Range(0,1)) = 0
        _FamilyMode("Family: Fire..Arcane", Range(0,10)) = 0
        _CarrierMode("Carrier Role", Range(0,20)) = 0
        _Seed("Deterministic Seed", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend("Src Blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("Dst Blend", Float) = 10
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Name "ElementNextForward"
            Blend [_SrcBlend] [_DstBlend]
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionOS : TEXCOORD0; float3 normalWS : TEXCOORD1; float2 uv : TEXCOORD2; };

            CBUFFER_START(UnityPerMaterial)
            float4 _PrimaryColor, _SecondaryColor, _AccentColor;
            float _Intensity, _GlobalAlpha, _Phase, _SemanticProgress, _FamilyMode, _CarrierMode, _Seed, _SrcBlend, _DstBlend;
            CBUFFER_END

            float Hash21(float2 p) { return frac(sin(dot(p, float2(127.1,311.7)) + _Seed*.017) * 43758.5453); }
            float ValueNoise(float2 p)
            {
                float2 i=floor(p), f=frac(p); f=f*f*(3-2*f);
                return lerp(lerp(Hash21(i),Hash21(i+float2(1,0)),f.x),lerp(Hash21(i+float2(0,1)),Hash21(i+1),f.x),f.y);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 position=input.positionOS.xyz;
                // Atmosphere roles move vertices only within their authored carrier.  Bounds and
                // topology are still compiler-owned; this is not screen-space distortion.
                if (_CarrierMode > .5 && _CarrierMode < 1.5)
                    position.x += sin(position.y*7 + _Phase*12 + _Seed*.01) * .018;
                if (_FamilyMode > 2.5 && _FamilyMode < 4.5)
                    position.y += sin(position.x*9 + _Phase*18 + _Seed*.01) * (_FamilyMode < 3.5 ? .012 : .025);
                if (_FamilyMode > 5.5 && _FamilyMode < 7.5)
                    position.xy *= 1 + sin((position.x+position.y)*8 + _Phase*10) * .012;
                output.positionCS=TransformObjectToHClip(position);
                output.positionOS=position;
                output.normalWS=TransformObjectToWorldNormal(input.normalOS);
                output.uv=input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 p=input.uv*2-1;
                float radial=length(p);
                float angle=atan2(p.y,p.x);
                float family=floor(_FamilyMode+.5);
                float role=floor(_CarrierMode+.5);
                float noise=ValueNoise(input.uv*float2(6,9)+float2(0,_Phase*3));
                float alpha=1;
                float3 color=_PrimaryColor.rgb;

                if (family < .5)
                {
                    // Fire: opaque/alpha body with hotter inner core, separate wispy heat carrier
                    // and charred residue.  Only highlight materials use additive blending.
                    float vertical=saturate(input.uv.y);
                    float lick=saturate(1-abs(p.x*(1.15+vertical)-sin(vertical*12+angle*2+_Phase*9)*.18));
                    float breakup=saturate((noise-.28)*1.5 + (1-vertical)*.35);
                    alpha=saturate(lick*breakup*(1-radial*.28));
                    color=lerp(_PrimaryColor.rgb,_SecondaryColor.rgb,saturate(vertical*.7+noise*.25));
                    color=lerp(color,_AccentColor.rgb,saturate((1-vertical)*.75-noise*.2));
                    if (role==1) { alpha*=.34+.35*noise; color=lerp(_PrimaryColor.rgb,_SecondaryColor.rgb,.35); }
                    if (role==2) { alpha=saturate(alpha*1.18); color=lerp(_SecondaryColor.rgb,_AccentColor.rgb,.72); }
                    if (role==3) { alpha*=.56; color=lerp(_PrimaryColor.rgb,float3(.12,.025,.01),.62); }
                }
                else if (family < 1.5)
                {
                    // Frost: hard facets and edge light keep crystal geometry readable.  Mist is
                    // alpha blended and deliberately softer than the shard carrier.
                    float3 normal=normalize(input.normalWS+float3(.0001,.0001,.0001));
                    float facet=pow(saturate(abs(normal.x)*.45+abs(normal.y)*.35+abs(normal.z)*.8),2.2);
                    float ridge=saturate(1-abs(frac((angle/6.28318+1)*8)-.5)*5);
                    alpha=saturate(.58+facet*.36+ridge*.16);
                    color=lerp(_PrimaryColor.rgb,_SecondaryColor.rgb,facet);
                    color=lerp(color,_AccentColor.rgb,saturate(ridge*.58+pow(1-saturate(normal.z),3)*.35));
                    if (role==4 || role==1) { alpha=saturate((1-radial)*(.22+.55*noise)); color=lerp(_PrimaryColor.rgb,_SecondaryColor.rgb,.62); }
                    if (role==2) { color=lerp(_SecondaryColor.rgb,_AccentColor.rgb,.78); alpha=saturate(alpha*1.12); }
                }
                else if (family < 2.5)
                {
                    // Lightning is rendered by compiler-bounded discrete polylines.  The shader
                    // adds no smooth spatial wandering; it only supplies a sharp white core and
                    // short-lived colored fringe/afterglow.
                    float core=saturate(1-abs(p.y)*4.2);
                    float fringe=saturate(1-abs(p.y)*1.7);
                    alpha=saturate(core+fringe*.42);
                    color=lerp(_PrimaryColor.rgb,_AccentColor.rgb,core);
                    if (role==6) { alpha*=.3+.25*noise; color=_PrimaryColor.rgb; }
                    else if (role==5) { alpha=saturate(alpha*1.2); color=lerp(_SecondaryColor.rgb,_AccentColor.rgb,.82); }
                }
                else if (family < 3.5)
                {
                    // Water: translucent volume, moving internal streaks, a bright foam lip and
                    // a darker alpha-blended residue. Geometry supplies the jet/wall/crown/curl.
                    float flow=sin((input.uv.x*12-input.uv.y*5)-_Phase*22+noise*2)*.5+.5;
                    float fresnel=pow(1-saturate(abs(normalize(input.normalWS).z)),2);
                    alpha=saturate(.28+flow*.24+fresnel*.28);
                    color=lerp(_PrimaryColor.rgb,_SecondaryColor.rgb,flow*.55+fresnel*.25);
                    if(role==2){alpha=saturate(.5+flow*.4);color=lerp(_SecondaryColor.rgb,_AccentColor.rgb,.7);}
                    if(role==8){alpha*=.52;color=lerp(_PrimaryColor.rgb,_SecondaryColor.rgb,.28);}
                    if(role==1){alpha*=.32;color=lerp(_PrimaryColor.rgb,_AccentColor.rgb,.35);}
                }
                else if (family < 4.5)
                {
                    // Wind has no opaque body. Thin anisotropic streaks and particulate breakup
                    // are intentionally what make the medium visible.
                    float streak=saturate(1-abs(frac(input.uv.y*9+input.uv.x*2-_Phase*8)-.5)*7);
                    alpha=saturate((streak*.38+noise*.16)*(.55+_GlobalAlpha*.45));
                    color=lerp(_PrimaryColor.rgb,_SecondaryColor.rgb,noise*.45);
                    if(role==9){alpha*=.58;color=lerp(color,_AccentColor.rgb,streak*.18);}
                    if(role==1){alpha*=.34;}
                }
                else if (family < 5.5)
                {
                    // Earth is weighty and facet-led: broad alpha body, mineral face separation,
                    // dark cracks and optional warm magma energy.
                    float3 normal=normalize(input.normalWS+float3(.001,.001,.001));
                    float facet=saturate(abs(normal.x)*.32+abs(normal.y)*.5+abs(normal.z)*.72);
                    float grain=step(.53,noise)*.16;
                    alpha=saturate(.72+facet*.2-grain*.2);
                    if(role==16) alpha*=saturate((_SemanticProgress-input.uv.x)*14+.5);
                    color=lerp(_PrimaryColor.rgb,_SecondaryColor.rgb,facet*.5+grain);
                    if(role==10){color=lerp(color,float3(.09,.055,.025),.28);}
                    if(role==2){color=lerp(_SecondaryColor.rgb,_AccentColor.rgb,.72);alpha*=.82;}
                    if(role==1){alpha*=.3;color=lerp(_PrimaryColor.rgb,float3(.16,.1,.05),.5);}
                }
                else if (family < 6.5)
                {
                    // Nature uses a directional reveal vein instead of a generic dissolve.
                    float vein=saturate(1-abs(sin((input.uv.x+input.uv.y*.55)*15))*2.8);
                    float reveal=saturate(input.uv.x*1.25-_Phase*.18+.16);
                    alpha=saturate(.48+vein*.42)*reveal;
                    if(role==11) alpha*=saturate((_SemanticProgress-input.uv.x)*12+.5);
                    color=lerp(_PrimaryColor.rgb,_SecondaryColor.rgb,vein*.52+input.uv.y*.2);
                    if(role==11){color=lerp(color,_AccentColor.rgb,vein*.2);}
                    if(role==1){alpha*=.3;}
                }
                else if (family < 7.5)
                {
                    // Toxic is viscous/cellular: swollen islands, oily highlight and a dense pool.
                    float cells=saturate((noise-.34)*2.2);
                    float rim=saturate(1-abs(radial-.64)*5);
                    alpha=saturate(.34+cells*.42+rim*.2);
                    color=lerp(_PrimaryColor.rgb,_SecondaryColor.rgb,cells*.54);
                    color=lerp(color,_AccentColor.rgb,rim*.25);
                    if(role==12){alpha=saturate(alpha*.9);color=lerp(color,float3(.18,.25,.025),.22);}
                    if(role==1){alpha*=.38;}
                }
                else if (family < 8.5)
                {
                    // Holy light reveals in vertical ordered bands with cross/ring highlights.
                    float vertical=saturate((_SemanticProgress-(1-input.uv.y))*12+.5);
                    float band=saturate(1-abs(frac(input.uv.y*5-_Phase*2)-.5)*4);
                    alpha=saturate(vertical*(.54+band*.44));
                    color=lerp(_PrimaryColor.rgb,_SecondaryColor.rgb,input.uv.y*.45);
                    color=lerp(color,_AccentColor.rgb,band*.55);
                    if(role==13){alpha=saturate(alpha*.88);}
                    if(role==1){alpha*=.28;}
                }
                else if (family < 9.5)
                {
                    // Shadow preserves a near-solid negative-space core and a purple event-horizon
                    // edge; outer mist remains soft alpha, never a recolored bright additive body.
                    float tear=saturate(1-abs(sin((input.uv.x*1.7+input.uv.y)*18+noise*3))*3);
                    float horizon=saturate(1-abs(radial-.66)*7);
                    alpha=(role==14 || role==17)?saturate(.82+tear*.15):saturate(horizon*.72+noise*.18);
                    if(role==17)alpha*=saturate((_SemanticProgress-input.uv.x)*14+.5);
                    color=(role==14 || role==17)?lerp(float3(.005,.003,.009),_PrimaryColor.rgb,tear*.16):lerp(_PrimaryColor.rgb,_AccentColor.rgb,horizon*.58);
                    if(role==1){alpha*=.24;color=_PrimaryColor.rgb;}
                    if(role==2){alpha=saturate(alpha*1.18);}
                }
                else
                {
                    // Arcane is glyph/rune energy: discrete cells, thin counter-rotating bands and
                    // a bright ordered activation edge.
                    float gridX=saturate(1-abs(frac(input.uv.x*10)-.5)*8);
                    float gridY=saturate(1-abs(frac(input.uv.y*10)-.5)*8);
                    float glyph=saturate(max(gridX,gridY)+noise*.16);
                    float order=saturate(input.uv.x+_Phase*.45);
                    alpha=saturate(glyph*(.42+order*.5));
                    if(role==15) alpha*=saturate((_SemanticProgress-input.uv.x)*14+.5);
                    color=lerp(_PrimaryColor.rgb,_SecondaryColor.rgb,order*.48);
                    color=lerp(color,_AccentColor.rgb,glyph*.38);
                    if(role==15){alpha=saturate(alpha*.92);}
                    if(role==1){alpha*=.26;}
                }

                alpha=saturate(alpha*_GlobalAlpha);
                return half4(color*_Intensity,alpha);
            }
            ENDHLSL
        }
    }
}

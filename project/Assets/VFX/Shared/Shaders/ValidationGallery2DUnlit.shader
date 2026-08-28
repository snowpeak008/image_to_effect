Shader "Universal Render Pipeline/VFXComposer Validation Gallery 2D Unlit"
{
    Properties
    {
        _PrimaryColor("Primary", Color) = (0.2,0.8,1,1)
        _SecondaryColor("Secondary", Color) = (0.8,1,1,1)
        _ShapeMode("Shape Mode", Float) = 0
        _RuntimeTime("Runtime Time", Float) = 0
        _Progress("Progress", Range(0,1)) = 0
        _Pulse("Pulse", Range(0,1)) = 0
        _GlobalAlpha("Global Alpha", Range(0,1)) = 1
        _Intensity("Intensity", Float) = 1
        [NoScaleOffset] _MaskAtlas("Shared Mask Atlas", 2D) = "white" {}
        [HideInInspector] _SrcBlend("Source Blend", Float) = 5
        [HideInInspector] _DstBlend("Destination Blend", Float) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Blend [_SrcBlend] [_DstBlend]
        ZWrite Off Cull Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            CBUFFER_START(UnityPerMaterial)
                half4 _PrimaryColor; half4 _SecondaryColor;
                half _ShapeMode; float _RuntimeTime; half _Progress; half _Pulse; half _GlobalAlpha; half _Intensity;
            CBUFFER_END
            TEXTURE2D(_MaskAtlas); SAMPLER(sampler_MaskAtlas);

            Varyings vert(Attributes input) { Varyings output; output.positionCS=TransformObjectToHClip(input.positionOS.xyz); output.uv=input.uv; output.color=input.color; return output; }
            float Hash21(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float Noise(float2 p) { float2 i=floor(p),f=frac(p); f=f*f*(3-2*f); return lerp(lerp(Hash21(i),Hash21(i+float2(1,0)),f.x),lerp(Hash21(i+float2(0,1)),Hash21(i+1),f.x),f.y); }
            half Ring(float r, float radius, float width) { return 1.0h-smoothstep(width,width*1.8h,abs(r-radius)); }
            half Disc(float r, float radius) { return 1.0h-smoothstep(radius*.72h,radius,r); }
            float HexDistance(float2 samplePoint) { samplePoint=abs(samplePoint); return max(samplePoint.y,samplePoint.x*.866025+samplePoint.y*.5); }
            half AtlasMask(float2 localUv,float2 tileOffset) { return SAMPLE_TEXTURE2D(_MaskAtlas,sampler_MaskAtlas,saturate(localUv)*.5+tileOffset).a; }

            half4 frag(Varyings input):SV_Target
            {
                const float tau=6.28318530718;
                float2 uv=input.uv, p=uv*2-1;
                float r=length(p), a=atan2(p.y,p.x);
                float t=_RuntimeTime;
                half mask=0, heat=0;
                half mode=_ShapeMode;

                if (mode < .5h) // Aura: asymmetric translucent field
                {
                    float boundary=.80+.045*sin(a*5-t*.9)+.024*sin(a*13+t*1.4);
                    half organic=AtlasMask(uv,float2(.5,.5));mask=Disc(r,boundary)*(.15+.22*organic+.1*Noise(p*3+t*.25))+Ring(r,boundary,.13)*(.38+.22*organic);mask=max(mask,organic*Disc(r,.82h)*.3h); heat=saturate(.32+r*.58);
                }
                else if (mode < 1.5h) // Aura: broken rotating runes
                {
                    half broad=smoothstep(.35h,.86h,.5h+.5h*sin(a*3-t*.8h));half fine=step(.84h,frac((a/tau+.5h)*18.0h+t*.045h));
                    mask=Ring(r,.69h,.045h)*broad+Ring(r,.88h,.025h)*fine*.55h; heat=.82h+.18h*sin(a*5+t);
                }
                else if (mode < 2.5h) // Aura: inward energy swirl
                {
                    half flow=.5h+.5h*sin(a*7-t*2.4+r*15); half cut=smoothstep(.52h,.9h,flow);
                    mask=Disc(r,.58h)*cut*(.35h+.65h*saturate(r*1.7h)); heat=flow;
                }
                else if (mode < 3.5h) // Aura: orbiting arcs and nodes
                {
                    half arc=pow(saturate(.5h+.5h*cos(a*3-t*2.1)),10); half nodes=pow(saturate(.5h+.5h*cos(a*5+t*1.3)),24);
                    mask=Ring(r,.77h,.06h)*arc+Ring(r,.61h,.075h)*nodes; heat=1;
                }
                else if (mode < 4.5h) // Aura: event pulse
                {
                    float radius=lerp(.38,.98,_Pulse);mask=Ring(r,radius,.055h)*_Pulse;heat=1;
                }
                else if (mode < 5.5h) // Beam: broad electric glow
                {
                    float center=.5+(Noise(float2(uv.x*13-t*.7,4.1))-.5)*.18+.025*sin(uv.x*34-t*11);float d=abs(uv.y-center);
                    mask=(1-smoothstep(.08,.26,d))*smoothstep(.04,.11,uv.x)*(1-smoothstep(.89,.96,uv.x));heat=.35h;
                }
                else if (mode < 6.5h) // Beam: hot continuous core
                {
                    float center=.5+(Noise(float2(uv.x*13-t*.7,4.1))-.5)*.18+.025*sin(uv.x*34-t*11);float d=abs(uv.y-center);
                    mask=(1-smoothstep(.018,.052,d))*smoothstep(.055,.1,uv.x)*(1-smoothstep(.9,.945,uv.x));heat=1;
                }
                else if (mode < 7.5h) // Beam: two branching filaments
                {
                    float center=.5+(Noise(float2(uv.x*13-t*.7,4.1))-.5)*.18;float branchA=center+.18*sin(uv.x*5+t*2)*smoothstep(.16,.45,uv.x)*(1-smoothstep(.56,.88,uv.x));
                    float branchB=center-.15*sin(uv.x*7-t*1.7)*smoothstep(.3,.58,uv.x)*(1-smoothstep(.72,.94,uv.x));
                    mask=(1-smoothstep(.015,.043,abs(uv.y-branchA)))+(1-smoothstep(.015,.043,abs(uv.y-branchB)));mask*=step(.13,uv.x)*step(uv.x,.9);heat=.82h;
                }
                else if (mode < 8.5h) // Beam: endpoint blooms
                {
                    float left=length((uv-float2(.105,.5))*float2(1,1.35)),right=length((uv-float2(.895,.5))*float2(1,1.35));mask=max(Disc(left,.085h),Disc(right,.085h))+max(Ring(left,.105h,.025h),Ring(right,.105h,.025h));heat=1;
                }
                else if (mode < 9.5h) // Beam: travelling charge beads
                {
                    float x=frac(t*.75+float(floor(uv.y*6))*.17);float center=.5+(Noise(float2(x*13-t*.7,4.1))-.5)*.18;float2 q=(uv-float2(x,center))*float2(1,1.8);mask=Disc(length(q),.045h);heat=1;
                }
                else if (mode < 10.5h) // Trail: broad tapered wake
                {
                    float head=.73+.035*sin(t*.9),center=.5+.065*sin(t*2.1);float behind=saturate((head-uv.x)*1.5)*step(uv.x,head);float taper=saturate(1-(head-uv.x)*1.18);float width=.045+.19*taper;
                    half brush=AtlasMask(float2(1-uv.x,uv.y),float2(0,.5));mask=(1-smoothstep(width,width*1.55,abs(uv.y-(center+.035*sin(uv.x*17-t*5)))))*behind*(.32+.5*brush+.28*Noise(float2(uv.x*8-t,uv.y*5)));mask=max(mask,brush*.62h);heat=saturate(.25+taper*.65);
                }
                else if (mode < 11.5h) // Trail: narrow hot spine
                {
                    float head=.73+.035*sin(t*.9),center=.5+.065*sin(t*2.1);float behind=saturate((head-uv.x)*1.8)*step(uv.x,head);mask=(1-smoothstep(.018,.06,abs(uv.y-(center+.02*sin(uv.x*15-t*4)))))*behind;heat=1;
                }
                else if (mode < 12.5h) // Trail: directional comet head
                {
                    float head=.73+.035*sin(t*.9),center=.5+.065*sin(t*2.1);float2 q=(uv-float2(head,center))*float2(1.15,1.9);float diamond=abs(q.x)+abs(q.y);
                    mask=(1-smoothstep(.08,.19,diamond))+Disc(length(q),.12h)*.8h;heat=1;
                }
                else if (mode < 13.5h) // Trail: wake ribs
                {
                    float head=.73+.035*sin(t*.9);float bands=abs(sin((head-uv.x)*42));float wake=step(uv.x,head)*smoothstep(head-.68,head-.08,uv.x);mask=(1-smoothstep(.025,.075,bands))*wake*(1-smoothstep(.10,.34,abs(uv.y-.5)));heat=.55h;
                }
                else if (mode < 14.5h) // Trail: detached fragments
                {
                    float head=.73+.035*sin(t*.9);float2 grid=float2(15,8);float2 q=frac(uv*grid)-.5;float id=floor(uv.x*grid.x)+floor(uv.y*grid.y)*grid.x;half mote=1-smoothstep(.08,.28,abs(q.x)+abs(q.y));mask=mote*step(Hash21(float2(id,8)),.17)*step(uv.x,head-.04);heat=1;
                }
                else if (mode < 15.5h) // Shield: translucent faceted plate
                {
                    float hex=HexDistance(p);half shards=AtlasMask(uv,float2(0,0));half facet=.18h+.1h*abs(sin(a*3+t*.45))+.12h*shards;mask=step(hex,.72h)*facet*(1-smoothstep(.2,.76,hex)*.3h);mask=max(mask,shards*step(hex,.72h)*.24h);heat=.38h+.34h*abs(sin(a*3));
                }
                else if (mode < 16.5h) // Shield: double hex border
                {
                    float hex=HexDistance(p);mask=(1-smoothstep(.025,.07,abs(hex-.73)))+(1-smoothstep(.012,.035,abs(hex-.57)))*.72h;heat=.8h;
                }
                else if (mode < 17.5h) // Shield: internal energy lattice
                {
                    float hex=HexDistance(p);float l1=abs(frac((p.x*.5+p.y*.866)*3.0+.5)-.5);float l2=abs(frac((-p.x*.5+p.y*.866)*3.0+.5)-.5);float l3=abs(frac(p.x*3.0+.5)-.5);
                    half grid=1-smoothstep(.025,.07,min(l1,min(l2,l3)));mask=grid*step(hex,.56h)*.7h;heat=.68h;
                }
                else if (mode < 18.5h) // Shield: border currents
                {
                    float hex=HexDistance(p);half dash=step(.62h,frac((a/tau+.5h)*18-t*.22h));mask=(1-smoothstep(.012,.04,abs(hex-.66)))*dash;heat=1;
                }
                else if (mode < 19.5h) // Shield: impact pulse and fracture rays
                {
                    float radius=lerp(.12,.92,_Pulse);float pulseHex=HexDistance(p);half rays=(1-smoothstep(.02,.06,abs(sin(a*6+.3))))*step(r,.66h);mask=((1-smoothstep(.025,.07,abs(pulseHex-radius)))+rays*.32h)*_Pulse;heat=1;
                }
                else if (mode < 20.5h) // Spawn: outer unstable portal field
                {
                    half wobble=.018h*sin(a*8-t*1.8)+.012h*sin(a*19+t*.7);half arcs=smoothstep(.22h,.78h,.5h+.5h*sin(a*4-t*.7));half smoke=AtlasMask(uv,float2(.5,.5));mask=Ring(r,.78h+wobble,.09h)*arcs+Ring(r,.62h-wobble,.05h)*.45h+Disc(r,.58h)*(.06h+.18h*smoke);mask=max(mask,smoke*Disc(r,.67h)*.28h);heat=.4h+.35h*sin(a*5-t);
                }
                else if (mode < 21.5h) // Spawn: counter-rotating sigils
                {
                    half outer=step(.82h,frac((a/tau+.5h)*18-t*.11h));half inner=smoothstep(.15h,.82h,.5h+.5h*sin(a*6+t*1.1));mask=Ring(r,.72h,.03h)*outer+Ring(r,.49h,.04h)*inner;heat=1;
                }
                else if (mode < 22.5h) // Spawn: inner vortex
                {
                    half spiral=.5h+.5h*sin(a*7-t*3-r*17);half smoke=AtlasMask(uv,float2(.5,.5));mask=Disc(r,.54h)*smoothstep(.52h,.9h,spiral)*(.45h+.55h*smoke)*(1-smoothstep(.05,.22,r));heat=spiral;
                }
                else if (mode < 23.5h) // Spawn: vertical summoning column
                {
                    half column=(1-smoothstep(.08h,.43h,abs(p.x)))*smoothstep(-.78h,-.18h,p.y)*(1-smoothstep(.28h,.94h,p.y));half wisps=.35h+.65h*Noise(float2(p.x*7+t,p.y*6-t*1.7));mask=column*wisps*smoothstep(0,.18,_Progress)*(1-smoothstep(.76,1,_Progress));heat=.55h+.45h*wisps;
                }
                else if (mode < 24.5h) // Spawn: ignition/completion flash
                {
                    half early=1-smoothstep(.035,.16,_Progress);half late=smoothstep(.72,.84,_Progress)*(1-smoothstep(.91,1,_Progress));half rays=pow(abs(cos(a*5+.35)),26)*(1-smoothstep(.08,.62,r));mask=(Disc(r,.16h)+rays*.38h)*(early+late);heat=1;
                }
                else // Shared real-particle diamond sprite
                {
                    float diamond=abs(p.x)+abs(p.y);mask=(1-smoothstep(.22,.62,diamond))+(1-smoothstep(.05,.22,r))*.65h;heat=1;
                }

                mask=saturate(mask)*_GlobalAlpha*input.color.a;
                half3 color=lerp(_PrimaryColor.rgb,_SecondaryColor.rgb,saturate(heat));
                // Materials use SrcAlpha blending. Multiplying RGB by mask here would apply
                // coverage twice and erase every soft/organic layer.
                half3 rgb=color*_Intensity*input.color.rgb;
                clip(mask-.003h);
                return half4(rgb,mask);
            }
            ENDHLSL
        }
    }
}

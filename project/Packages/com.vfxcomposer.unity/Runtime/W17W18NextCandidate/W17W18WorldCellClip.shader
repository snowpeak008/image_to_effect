Shader "VFXComposer/NextCandidate/WorldCellClip"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _PrimaryColor ("Primary", Color) = (1,1,1,1)
        _SecondaryColor ("Secondary", Color) = (0,1,1,1)
        _AccentColor ("Accent", Color) = (1,1,1,1)
        _GlobalAlpha ("Global Alpha", Range(0,1)) = 1
        _Phase ("Phase", Range(0,1)) = 0
        _Dissolve ("Dissolve", Range(0,1)) = 0
        _ClipRect ("World Clip Rect", Vector) = (-10000,-10000,10000,10000)
        _UseClip ("Use Clip", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 world : TEXCOORD0;
                float2 uv : TEXCOORD1;
                float4 color : COLOR;
            };

            float4 _Color;
            float4 _PrimaryColor;
            float4 _SecondaryColor;
            float4 _AccentColor;
            float _GlobalAlpha;
            float _Phase;
            float _Dissolve;
            float4 _ClipRect;
            float _UseClip;

            v2f vert(appdata value)
            {
                v2f output;
                float4 world = mul(unity_ObjectToWorld, value.vertex);
                output.vertex = mul(UNITY_MATRIX_VP, world);
                output.world = world.xyz;
                output.uv = value.uv;
                output.color = value.color;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                if (_UseClip > .5)
                {
                    clip(input.world.x - _ClipRect.x);
                    clip(input.world.y - _ClipRect.y);
                    clip(_ClipRect.z - input.world.x);
                    clip(_ClipRect.w - input.world.y);
                }

                float radial = saturate(1.18 - length(input.uv - .5) * 1.5);
                float stripe = .72 + .28 * sin((input.uv.x + input.uv.y + _Phase) * 12.56637);
                float dissolveMask = frac(sin(dot(floor(input.world.xy * 18), float2(12.9898, 78.233))) * 43758.5453);
                clip(dissolveMask - _Dissolve * .92);
                float4 palette = lerp(_PrimaryColor, _SecondaryColor, saturate(input.uv.y + .18 * stripe));
                palette = lerp(palette, _AccentColor, saturate(stripe - .82) * 2.6);
                palette *= input.color;
                palette.a *= _GlobalAlpha * radial;
                return palette;
            }
            ENDCG
        }
    }
}

#ifndef VFX_COMMON_INCLUDED
#define VFX_COMMON_INCLUDED

// Subgraph library: shared hashing, noise, polar UV, palette ramp and the
// common vertex stage for every technique-family material variant.
// All form is computed procedurally; no texture sampling exists in this library (gate G-1).

float VfxHash11(float p)
{
    p = frac(p * 0.1031);
    p *= p + 33.33;
    p *= p + p;
    return frac(p);
}

float VfxHash21(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float2 VfxHash22(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.xx + p3.yz) * p3.zy);
}

float VfxValueNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    float2 u = f * f * (3.0 - 2.0 * f);
    float a = VfxHash21(i);
    float b = VfxHash21(i + float2(1.0, 0.0));
    float c = VfxHash21(i + float2(0.0, 1.0));
    float d = VfxHash21(i + float2(1.0, 1.0));
    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
}

float VfxFbm(float2 p, int octaves)
{
    float v = 0.0;
    float amp = 0.5;
    for (int i = 0; i < octaves; i++)
    {
        v += amp * VfxValueNoise(p);
        p = p * 2.03 + 17.13;
        amp *= 0.5;
    }
    return v;
}

// Polar coordinates around the quad centre: x = radius (0 centre, 1 at the inscribed edge), y = angle in [0,1).
float2 VfxPolar(float2 uv)
{
    float2 c = uv - 0.5;
    return float2(length(c) * 2.0, atan2(c.y, c.x) / 6.28318530718 + 0.5);
}

float VfxLuminance(float3 c)
{
    return dot(c, float3(0.2126, 0.7152, 0.0722));
}

float3 VfxAdjustSaturation(float3 c, float mul)
{
    float l = VfxLuminance(c);
    return lerp(float3(l, l, l), c, mul);
}

// Four-stop HDR palette ramp: cool -> primary -> secondary -> hot.
float3 VfxPaletteRamp(float x, float3 cool, float3 primary, float3 secondary, float3 hot)
{
    x = saturate(x);
    float3 c = lerp(cool, primary, saturate(x * 3.0));
    c = lerp(c, secondary, saturate(x * 3.0 - 1.0));
    c = lerp(c, hot, saturate(x * 3.0 - 2.0));
    return c;
}

// Beat evaluation shared by the glow layer and the local-light beat driver
// (same formula keeps glowFlickerCoupling in sync when phase is driven externally).
// mode: 0 steady, 1 breathe, 2 flicker, 3 pulse, 4 strobe.
float VfxPulse(float mode, float rate, float depth, float t, float seed)
{
    float phase = t * rate;
    if (mode < 0.5) return 1.0;
    if (mode < 1.5) return 1.0 - depth * (0.5 + 0.5 * sin(phase * 6.28318530718));
    if (mode < 2.5) return 1.0 - depth * VfxHash11(floor(phase * 2.0) + seed);
    if (mode < 3.5) { float p = frac(phase); return 1.0 - depth + depth * exp(-6.0 * p); }
    return 1.0 - depth * step(0.5, frac(phase));
}

// Common per-material uniforms: the five-colour HDR palette, the standard
// parameters (intensity/speed/seed) and the StyleStage parameter surface.
// Expanded inside each variant shader's UnityPerMaterial CBUFFER.
#define VFX_COMMON_UNIFORMS \
    half4 _ColorPrimary; \
    half4 _ColorSecondary; \
    half4 _ColorHot; \
    half4 _ColorCool; \
    half4 _ColorResidue; \
    float _Intensity; \
    float _Speed; \
    float _Seed; \
    float _ShadingSteps; \
    float _EdgeSharpness; \
    float _OutlineWidth; \
    half4 _OutlineColor; \
    float _SaturationMul; \
    float _HdrClamp; \
    float _DetailMul; \
    float _InnerLine; \
    float _PixelSize; \
    float _ColorSteps; \
    float _AlphaCutoff; \
    float _DitherLevels; \
    float _StyleFrameRate; \
    float _Outline1px;

struct VfxAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float2 uv : TEXCOORD0;
    half4 color : COLOR;
};

struct VfxVaryings
{
    float4 positionCS : SV_POSITION;
    float2 uv : TEXCOORD0;
    float3 positionWS : TEXCOORD1;
    float3 normalWS : TEXCOORD2;
    half4 color : COLOR;
};

VfxVaryings VfxVert(VfxAttributes input)
{
    VfxVaryings output;
    VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
    output.positionCS = positionInputs.positionCS;
    output.positionWS = positionInputs.positionWS;
    output.normalWS = TransformObjectToWorldNormal(input.normalOS);
    output.uv = input.uv;
    output.color = input.color;
    return output;
}

#endif

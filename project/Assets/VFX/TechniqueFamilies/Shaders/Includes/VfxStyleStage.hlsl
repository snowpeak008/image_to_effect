#ifndef VFX_STYLE_STAGE_INCLUDED
#define VFX_STYLE_STAGE_INCLUDED

// StyleStage slot (STYLE_CATALOG_v1 sections 1.2/2.2/3.2). Every material
// variant ends in this stage; the style axis selects one of the three
// implementations through the _STYLESTAGE_* keyword set:
//   _STYLESTAGE_NONE    -> realistic baseline pass-through
//   _STYLESTAGE_CARTOON -> cel quantization + palette remap + SDF outline
//   _STYLESTAGE_PIXEL   -> colour banding + alpha binarization/Bayer dither + 1px outline
// The slot has three hook points because pixel-style quantization must happen
// before sampling: VfxStyleUV (grid snap), VfxStyleTime (frame-rate snap),
// VfxApplyStyleStage (final colour stage). Include after the UnityPerMaterial
// CBUFFER (uses the VFX_COMMON_UNIFORMS surface).
// Pixel style keeps the native render resolution: only material-space
// coordinates are snapped, never a RenderTexture (ADR-010 section 2).

float VfxStyleTime(float t)
{
#if defined(_STYLESTAGE_PIXEL)
    return floor(t * _StyleFrameRate) / max(_StyleFrameRate, 0.0001);
#else
    return t;
#endif
}

float2 VfxStyleUV(float2 uv)
{
#if defined(_STYLESTAGE_PIXEL)
    float px = max(_PixelSize, 0.0001);
    return (floor(uv / px) + 0.5) * px;
#else
    return uv;
#endif
}

float VfxBayer4(float2 cell)
{
    static const float bayer[16] =
    {
        0.0, 8.0, 2.0, 10.0,
        12.0, 4.0, 14.0, 6.0,
        3.0, 11.0, 1.0, 9.0,
        15.0, 7.0, 13.0, 5.0
    };
    int2 p = int2(fmod(abs(cell), 4.0));
    return (bayer[p.x + p.y * 4] + 0.5) / 16.0;
}

// color: linear HDR colour + alpha computed by the element/prototype stages.
// uv: styled UV (already grid-snapped under pixel style).
// edgeDist: distance from the shape edge in UV units (>= 0 inside, negative if unknown).
half4 VfxApplyStyleStage(half4 color, float2 uv, float edgeDist)
{
#if defined(_STYLESTAGE_CARTOON)
    float steps = max(_ShadingSteps, 2.0);
    float lum = VfxLuminance(color.rgb);
    float q = floor(saturate(lum / max(_HdrClamp, 0.0001)) * steps) / max(steps - 1.0, 1.0);
    float3 ramp = VfxPaletteRamp(q, _ColorCool.rgb, _ColorPrimary.rgb, _ColorSecondary.rgb, _ColorHot.rgb);
    color.rgb = lum >= _HdrClamp ? _ColorHot.rgb : ramp;
    color.rgb = VfxAdjustSaturation(color.rgb, _SaturationMul);
    float sharpened = saturate((color.a - 0.5) / max(1.0 - _EdgeSharpness, 0.005) + 0.5);
    color.a = lerp(color.a, sharpened, _EdgeSharpness);
    if (_OutlineWidth > 0.0 && edgeDist >= 0.0 && edgeDist < _OutlineWidth)
        color.rgb = _OutlineColor.rgb;
    if (_InnerLine > 0.5)
    {
        float band = frac(saturate(lum / max(_HdrClamp, 0.0001)) * steps);
        if (band < 0.06 && lum > 0.02)
            color.rgb *= 0.55;
    }
    return color;
#elif defined(_STYLESTAGE_PIXEL)
    float stepsC = max(_ColorSteps, 2.0);
    float lum = VfxLuminance(color.rgb);
    float q = floor(saturate(lum / max(_HdrClamp, 0.0001)) * stepsC) / max(stepsC - 1.0, 1.0);
    color.rgb = VfxPaletteRamp(q, _ColorCool.rgb, _ColorPrimary.rgb, _ColorSecondary.rgb, _ColorHot.rgb);
    color.rgb = VfxAdjustSaturation(color.rgb, _SaturationMul);
    float px = max(_PixelSize, 0.0001);
    float threshold = _AlphaCutoff;
    if (_DitherLevels > 0.5)
    {
        float d = VfxBayer4(floor(uv / px));
        threshold = saturate(_AlphaCutoff + (d - 0.5) * (_DitherLevels / 4.0));
    }
    color.a = color.a >= threshold ? 1.0 : 0.0;
    if (_Outline1px > 0.5 && edgeDist >= 0.0 && edgeDist < px && color.a > 0.5)
        color.rgb = _ColorCool.rgb * 0.4;
    return color;
#else
    return color;
#endif
}

#endif

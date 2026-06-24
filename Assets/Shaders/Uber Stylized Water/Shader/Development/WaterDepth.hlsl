#ifndef WATER_SAMPLE_INCLUDED
#define WATER_SAMPLE_INCLUDED

// One reconstruction pass that feeds both jobs:
//   WaterDepth     : true vertical depth (UNREFRACTED) -> shallow/deep tint + foam width
//   GroundUV       : world XZ of the ground as seen THROUGH refraction -> caustics / shadows
//   GroundWorldPos : full ground world position (handy for triplanar / slope masks)
//
// Wire two Scene Depth nodes, both set to Raw:
//   RawDepthScreen : Scene Depth sampled at ScreenUV     (plain screen UV)
//   RawDepthRefr   : Scene Depth sampled at RefractedUV  (your opaque-refraction UV)
// ScreenUV        : Screen Position (Default).xy
// RefractedUV     : ScreenUV + the same normal-based offset that feeds your Scene Color sample
// SurfaceWorldPos : Position (World) of the water fragment (use the still-water level Y if you
//                   want a non-wobbling foam line; see notes)

void WaterSample_float(
    float2 ScreenUV,  float RawDepthScreen,
    float2 RefractedUV, float RawDepthRefr,
    float3 SurfaceWorldPos,
    out float WaterDepth, out float2 GroundUV, out float3 GroundWorldPos)
{
    // Tint / foam: true vertical depth, NOT refracted, so the foam line stays put on the shore.
    if (RawDepthScreen == UNITY_RAW_FAR_CLIP_VALUE)
        WaterDepth = 0.0;
    else
    {
        float3 g = ComputeWorldSpacePosition(ScreenUV, RawDepthScreen, UNITY_MATRIX_I_VP);
        WaterDepth = max(SurfaceWorldPos.y - g.y, 0.0);
    }

    // Projection: reconstruct at the SAME UV the opaque texture is refracted with, so
    // caustics/shadows sit exactly where the refracted ground is drawn on screen.
    bool refrSky = (RawDepthRefr == UNITY_RAW_FAR_CLIP_VALUE);
    float2 uv = refrSky ? ScreenUV       : RefractedUV;   // fall back if refraction hits sky
    float  d  = refrSky ? RawDepthScreen : RawDepthRefr;
    GroundWorldPos = ComputeWorldSpacePosition(uv, d, UNITY_MATRIX_I_VP);
    GroundUV = GroundWorldPos.xz;
}

#endif

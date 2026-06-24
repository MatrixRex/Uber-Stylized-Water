#ifndef WATER_REFRACTION_INCLUDED
#define WATER_REFRACTION_INCLUDED

// =====================================================================
// Screen-space refraction for URP water, ortho + perspective safe.
// Single responsibility: produce ONE clean RefractedUV. Nothing else.
// Two pure-ALU nodes with a Scene Depth (Raw) node between them (so no
// opaque/depth texture re-declaration clash):
//   1) RefractionOffsetUV -> OffsetUV
//   2) Scene Depth (Raw) @ OffsetUV
//   3) RefractionClampUV  -> RefractedUV   (only output)
// Ground reconstruction, depth, ground UV, surface UV all happen
// downstream as their own nodes, fed by RefractedUV.
// Each function has a _half forwarder.
// =====================================================================

// ---------------------------------------------------------------------
// 1. Build the raw refracted UV. Normal-driven screen offset, strength
//    lerped near->far by camera distance, scaled by a 0..1 mask.
// ---------------------------------------------------------------------
void RefractionOffsetUV_float(float2 ScreenUV, float3 Normal, float3 SurfaceWorldPos,
                              float RefractionStrength, float FarRefractionStrength,
                              float FarDistance, float Mask,
                              out float2 OffsetUV)
{
    float camDist  = distance(_WorldSpaceCameraPos, SurfaceWorldPos);
    float farT     = saturate(camDist / max(FarDistance, 1e-3));
    float strength = lerp(RefractionStrength, FarRefractionStrength, farT);
    OffsetUV = ScreenUV + Normal.xy * (strength * Mask);
}

void RefractionOffsetUV_half(float2 ScreenUV, float3 Normal, float3 SurfaceWorldPos,
                             float RefractionStrength, float FarRefractionStrength,
                             float FarDistance, float Mask,
                             out float2 OffsetUV)
{
    RefractionOffsetUV_float(ScreenUV, Normal, SurfaceWorldPos, RefractionStrength,
                             FarRefractionStrength, FarDistance, Mask, OffsetUV);
}

// ---------------------------------------------------------------------
// 2. Foreground-bleed clamp -> RefractedUV (the ONLY output).
//    Compares view-space eye depth (linear in both projections, no
//    _ZBufferParams) of the offset sample vs the water surface. If the
//    offset sample is in front of the water (or sky), fall back to BaseUV.
//    Reconstructs a scalar eye depth internally; it does NOT emit a
//    ground position - that's the ground module's job.
//      RawDepthAtOffset : Scene Depth (Raw) sampled at OffsetUV
// ---------------------------------------------------------------------
void RefractionClampUV_float(float2 OffsetUV, float2 BaseUV,
                             float RawDepthAtOffset, float3 SurfaceWorldPos,
                             out float2 RefractedUV)
{
    float3 sceneWS  = ComputeWorldSpacePosition(OffsetUV, RawDepthAtOffset, UNITY_MATRIX_I_VP);
    float  sceneEye = -mul(UNITY_MATRIX_V, float4(sceneWS,        1.0)).z;
    float  surfEye  = -mul(UNITY_MATRIX_V, float4(SurfaceWorldPos,1.0)).z;

    float fg = (sceneEye < surfEye) ? 1.0 : 0.0;                       // sample in front of water
    fg = max(fg, (RawDepthAtOffset == UNITY_RAW_FAR_CLIP_VALUE) ? 1.0 : 0.0); // or sky

    RefractedUV = lerp(OffsetUV, BaseUV, fg);
}

void RefractionClampUV_half(float2 OffsetUV, float2 BaseUV,
                            float RawDepthAtOffset, float3 SurfaceWorldPos,
                            out float2 RefractedUV)
{
    RefractionClampUV_float(OffsetUV, BaseUV, RawDepthAtOffset, SurfaceWorldPos, RefractedUV);
}

#endif

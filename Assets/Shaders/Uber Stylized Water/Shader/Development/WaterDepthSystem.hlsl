#ifndef WATER_DEPTH_SYSTEM_INCLUDED
#define WATER_DEPTH_SYSTEM_INCLUDED

// =====================================================================
// Modular water depth / projection system for URP. Ortho + perspective safe.
// Three independent nodes so you can branch and post-process between them.
// World-space ground UV (flowing-water foam + caustics) is just the .xz of
// the position from node 1 — use a Split, no extra node needed.
// All run in the fragment stage. Keep node Precision = Single.
// =====================================================================

// ---------------------------------------------------------------------
// 1. Reconstruct the opaque ground world position behind a water pixel.
//    Call it twice: with the plain screen UV (depth/foam) and with your
//    refracted UV (caustics/shadow). IsValid = 0 on sky / no geometry.
//    Inverse view-projection handles the perspective divide and the
//    platform depth convention; same path for ortho (w==1) and perspective.
// ---------------------------------------------------------------------
void ReconstructGroundWS_float(float2 ScreenUV, float RawDepth,
                               out float3 GroundWorldPos, out float IsValid)
{
    if (RawDepth == UNITY_RAW_FAR_CLIP_VALUE)
    {
        GroundWorldPos = float3(0, 0, 0);
        IsValid = 0.0;
        return;
    }
    GroundWorldPos = ComputeWorldSpacePosition(ScreenUV, RawDepth, UNITY_MATRIX_I_VP);
    IsValid = 1.0;
}

// ---------------------------------------------------------------------
// 2. Vertical (straight top-down) water depth. View-angle independent.
//    Feed the plain-UV ground so the foam line stays put. (It is just
//    max(surfaceY - groundY, 0) — exposed as a node for a clean graph.)
// ---------------------------------------------------------------------
void WaterColumnDepth_float(float3 SurfaceWorldPos, float3 GroundWorldPos,
                            out float Depth)
{
    Depth = max(SurfaceWorldPos.y - GroundWorldPos.y, 0.0);
}

// ---------------------------------------------------------------------
// 3. Map a ground XZ back to the MESH UV of the surface point above it,
//    so mesh-UV foam (basin water) and its projected shadow stay in sync.
//    Straight overhead: LightDirWS = (0,-1,0), Depth ignored.
//    Angled sun: pass the real (downward) light dir to skew the caster.
//    Locally inverts (world XZ -> mesh UV) via screen-space derivatives:
//    exact on a flat planar-UV surface, first-order accurate otherwise.
//    Feed GroundXZ from the REFRACTED reconstruction so it matches the
//    refracted ground the player sees.
// ---------------------------------------------------------------------
void GroundToMeshUV_float(float2 GroundXZ, float2 SurfaceWorldXZ, float2 SurfaceUV,
                          float3 LightDirWS, float Depth,
                          out float2 ProjectedUV)
{
    float down = max(-LightDirWS.y, 1e-3);
    float2 targetXZ = GroundXZ - (LightDirWS.xz / down) * Depth;

    float2 dUVx = ddx(SurfaceUV);
    float2 dUVy = ddy(SurfaceUV);
    float2 dWx  = ddx(SurfaceWorldXZ);
    float2 dWy  = ddy(SurfaceWorldXZ);

    float det    = dWx.x * dWy.y - dWx.y * dWy.x;
    float invDet = 1.0 / (det + (det >= 0 ? 1e-8 : -1e-8));
    float2x2 invW = float2x2(dWy.y, -dWy.x, -dWx.y, dWx.x) * invDet;

    float2 screenDelta = mul(invW, targetXZ - SurfaceWorldXZ);
    ProjectedUV = SurfaceUV + dUVx * screenDelta.x + dUVy * screenDelta.y;
}

#endif

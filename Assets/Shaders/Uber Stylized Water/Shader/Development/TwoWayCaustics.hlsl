#ifndef TWO_WAY_CAUSTICS_INCLUDED
#define TWO_WAY_CAUSTICS_INCLUDED

#include "ProceduralDistortion.hlsl"

// Caustics counterpart of TwoWayNormalBlend. Same two-way panning idea, but this
// runs on the UNDERWATER surface: the base UV is the underwater ground UV that
// WaterDepthSystem provides (world ground XZ in world mode, or the ground mapped
// back to mesh UV via GroundToMeshUV in mesh mode). We don't reselect the UV here
// — we only branch how the two dual-sample taps PAN within it:
//   world  -> opposed + rotated taps, drifts cancel, no flow direction
//   meshUV -> both taps travel downstream, so the caustic web flows with the river
// The dual sampling (min of two taps) is the standard caustic-web + anti-tiling trick.

void TwoWayCaustics_float(
    bool UseMeshUV,
    float2 UnderWaterUV,     
    float Time,
    UnityTexture2D CausticsMap,
    UnitySamplerState sampler_CausticsMap,
    float CausticsPan,
    float CausticsScale,
    float DistortionScale,
    float DistortionStrength,
    out float3 Caustics
)
{
    float2 baseUV = UnderWaterUV;

    float2 distortion;
    ProceduralDistortion_float(
        baseUV,
        Time,
        CausticsPan,
        DistortionScale,
        DistortionStrength,
        UseMeshUV,
        distortion
    );

    // 2. Both modes share the same decorrelated, cancelling tap pair for a chaotic
    //    non-directional churn: rotate tap 2's domain ~60 deg to break the shared
    //    texture, then pan it at the exact opposite apparent velocity to tap 1 (undoing
    //    that rotation and the tiling ratio) so the two drifts cancel to no net flow.
    float2 tile1 = CausticsScale * 0.5;
    float2 tile2 = CausticsScale * 0.87;
    float2 uv1, uv2;

    if (UseMeshUV)
    {
        // River mode: Scroll both taps downstream along the V-axis at ±45 degree angles.
        float2 speed1 = float2( 0, -0.7) * CausticsPan;
        float2 speed2 = float2(0, 0.1) * CausticsPan;

        uv1 = baseUV *  tile1                       + distortion + speed1 * 0.1 * Time;
        uv2 = baseUV * (tile2 * float2(-1.0, 1.0))  + distortion + speed2 * 0.1 * Time;
    }
    else
    {
        // Still water mode: Cancel drifts using a 60-degree rotated tap 2 and opposite panning.
        const float2x2 rot2 = float2x2(0.5, -0.866, 0.866, 0.5);
        float2 speed1 = float2(-0.5, -0.35) * CausticsPan;
        float2 speed2 = -mul(rot2, speed1) * (tile2 / tile1);

        uv1 =           baseUV  * tile1 + distortion + speed1 * 0.1 * Time;
        uv2 = mul(rot2, baseUV) * tile2 + distortion + speed2 * 0.1 * Time;
    }

    float3 c1 = SAMPLE_TEXTURE2D(CausticsMap.tex, sampler_CausticsMap.samplerstate, uv1).rgb;
    float3 c2 = SAMPLE_TEXTURE2D(CausticsMap.tex, sampler_CausticsMap.samplerstate, uv2).rgb;

    Caustics = min(c1, c2);
}

void TwoWayCaustics_half(
    bool UseMeshUV,
    half2 UnderWaterUV,
    half Time,
    UnityTexture2D CausticsMap,
    UnitySamplerState sampler_CausticsMap,
    half CausticsPan,
    half CausticsScale,
    half DistortionScale,
    half DistortionStrength,
    out half3 Caustics
)
{
    float3 causticsFloat;

    TwoWayCaustics_float(
        UseMeshUV,
        float2(UnderWaterUV),
        float(Time),
        CausticsMap,
        sampler_CausticsMap,
        float(CausticsPan),
        float(CausticsScale),
        float(DistortionScale),
        float(DistortionStrength),
        causticsFloat
    );

    Caustics = half3(causticsFloat);
}

#endif // TWO_WAY_CAUSTICS_INCLUDED

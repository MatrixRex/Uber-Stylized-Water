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
    float2 FlowDir,
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
        FlowDir,
        distortion
    );

    // 2. Both modes share the same decorrelated, cancelling tap pair for a chaotic
    //    non-directional churn: rotate tap 2's domain ~60 deg to break the shared
    //    texture, then pan it at the exact opposite apparent velocity to tap 1 (undoing
    //    that rotation and the tiling ratio) so the two drifts cancel to no net flow.
    float2 tile1 = CausticsScale * 0.5;
    float2 tile2 = CausticsScale * 0.87;
    float2 uv1, uv2;

    float flowStrength = length(FlowDir);
    float flowWeight = saturate(flowStrength);
    float3 c1, c2;

    if (flowWeight > 0.001)
    {
        // 2-Phase Flow Mapping to avoid infinite UV stretching over time
        float2 flowDirNormalized = FlowDir / (flowStrength + 0.0001);

        // Rotate flowDirNormalized for each layer to match default crossing angles:
        // Layer 1 default speed is (0, 0.7)
        // Layer 2 default speed is (0, -0.1)
        float2 flowDir1;
        float2 s1 = float2(0.0, 0.7);
        flowDir1.x = s1.x * flowDirNormalized.y + s1.y * flowDirNormalized.x;
        flowDir1.y = -s1.x * flowDirNormalized.x + s1.y * flowDirNormalized.y;
        flowDir1 = normalize(flowDir1);

        float2 flowDir2;
        float2 s2 = float2(0.0, -0.1);
        flowDir2.x = s2.x * flowDirNormalized.y + s2.y * flowDirNormalized.x;
        flowDir2.y = -s2.x * flowDirNormalized.x + s2.y * flowDirNormalized.y;
        flowDir2 = normalize(flowDir2);

        float speedScale = CausticsPan * 0.1;
        float phase0 = frac(Time * speedScale);
        float phase1 = frac(Time * speedScale + 0.5);

        float maxDistortion = 0.15 * flowWeight;
        float2 flowVector1 = flowDir1 * maxDistortion;
        float2 flowVector2 = flowDir2 * maxDistortion;

        uv1 = baseUV * tile1 + distortion - flowVector1 * phase0;
        uv2 = baseUV * tile2 + distortion - flowVector2 * phase1;

        c1 = SAMPLE_TEXTURE2D(CausticsMap.tex, sampler_CausticsMap.samplerstate, uv1).rgb;
        c2 = SAMPLE_TEXTURE2D(CausticsMap.tex, sampler_CausticsMap.samplerstate, uv2).rgb;

        float blend = abs(0.5 - phase0) / 0.5;
        float3 blendedCaustics = lerp(c1, c2, blend);
        c1 = blendedCaustics;
        c2 = blendedCaustics;
    }
    else
    {
        // Unpainted: default continuous panning
        float2 speed1 = float2(0.0, 0.7) * CausticsPan;
        float2 speed2 = float2(0.0, -0.1) * CausticsPan;

        if (UseMeshUV)
        {
            uv1 = baseUV * tile1 + distortion - speed1 * 0.1 * Time;
            uv2 = baseUV * (tile2 * float2(-1.0, 1.0)) + distortion - speed2 * 0.1 * Time;
        }
        else
        {
            const float2x2 rot2 = float2x2(0.5, -0.866, 0.866, 0.5);
            float2 defaultSpeed1 = float2(0.5, 0.35) * CausticsPan;
            float2 defaultSpeed2 = mul(rot2, defaultSpeed1) * (tile2 / tile1);

            uv1 =           baseUV  * tile1 + distortion - defaultSpeed1 * 0.1 * Time;
            uv2 = mul(rot2, baseUV) * tile2 + distortion - defaultSpeed2 * 0.1 * Time;
        }

        c1 = SAMPLE_TEXTURE2D(CausticsMap.tex, sampler_CausticsMap.samplerstate, uv1).rgb;
        c2 = SAMPLE_TEXTURE2D(CausticsMap.tex, sampler_CausticsMap.samplerstate, uv2).rgb;
    }

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
    half2 FlowDir,
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
        float2(FlowDir),
        causticsFloat
    );

    Caustics = half3(causticsFloat);
}

#endif // TWO_WAY_CAUSTICS_INCLUDED

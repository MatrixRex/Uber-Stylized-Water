#ifndef PROCEDURAL_DISTORTION_INCLUDED
#define PROCEDURAL_DISTORTION_INCLUDED

// Computes procedural liquid warp distortion offset using a sum of panned/churning sine/cosine waves.
// This requires 0 texture samples and is highly optimized for mobile and web.
void ProceduralDistortion_float(
    float2 baseUV,
    float Time,
    float PanSpeed,
    float DistortionScale,
    float DistortionStrength,
    bool UseMeshUV,
    float2 FlowDir,
    out float2 distortion
)
{
    float flowStrength = length(FlowDir);
    float flowWeight = saturate(flowStrength);

    if (flowWeight > 0.001)
    {
        // 2-Phase Flow Mapping to avoid infinite UV stretching over time
        float2 flowDirNormalized = FlowDir / (flowStrength + 0.0001);
        float speedScale = PanSpeed * 0.4;
        
        float phase0 = frac(Time * speedScale);
        float phase1 = frac(Time * speedScale + 0.5);

        float maxDistortion = 0.15 * flowWeight;
        float2 flowVector = flowDirNormalized * maxDistortion;

        float2 pUV0 = baseUV * DistortionScale - flowVector * phase0;
        float2 pUV1 = baseUV * DistortionScale - flowVector * phase1;

        float churn = PanSpeed * 0.05 * Time; 

        float2 dist0 = float2(
            sin(pUV0.y * 10.0 + churn) + cos(pUV0.x * 6.0 - churn),
            cos(pUV0.x * 8.0 + churn * 1.2) + sin(pUV0.y * 7.0 - churn * 0.8)
        );

        float2 dist1 = float2(
            sin(pUV1.y * 10.0 + churn) + cos(pUV1.x * 6.0 - churn),
            cos(pUV1.x * 8.0 + churn * 1.2) + sin(pUV1.y * 7.0 - churn * 0.8)
        );

        float blend = abs(0.5 - phase0) / 0.5;
        distortion = lerp(dist0, dist1, blend) * DistortionStrength * 0.05;
    }
    else
    {
        if (UseMeshUV)
        {
            // River mode: Flow downstream (negative V-axis) and scale with CausticsPan
            float2 flowSpeed = float2(0.0, -0.4) * PanSpeed;
            float2 pUV = baseUV * DistortionScale + flowSpeed * 0.1 * Time;
            
            // Internal slow churning that doesn't overpower the flow
            float churn = PanSpeed * 0.05 * Time; 
            
            distortion = float2(
                sin(pUV.y * 10.0 + churn) + cos(pUV.x * 6.0 - churn),
                cos(pUV.x * 8.0 + churn * 1.2) + sin(pUV.y * 7.0 - churn * 0.8)
            ) * DistortionStrength * 0.05;
        }
        else
        {
            // Still water: non-directional, churning look with zero net drift
            float waveSpeed = PanSpeed * 2.0 * Time;
            distortion = float2(
                sin(baseUV.y * 10.0 * DistortionScale + waveSpeed) + cos(baseUV.x * 6.0 * DistortionScale - waveSpeed * 0.75),
                cos(baseUV.x * 8.0 * DistortionScale + waveSpeed * 0.9) + sin(baseUV.y * 7.0 * DistortionScale - waveSpeed * 0.6)
            ) * DistortionStrength * 0.05;
        }
    }
}

void ProceduralDistortion_half(
    half2 baseUV,
    half Time,
    half PanSpeed,
    half DistortionScale,
    half DistortionStrength,
    bool UseMeshUV,
    half2 FlowDir,
    out half2 distortion
)
{
    float2 distortionFloat;
    ProceduralDistortion_float(
        float2(baseUV),
        float(Time),
        float(PanSpeed),
        float(DistortionScale),
        float(DistortionStrength),
        UseMeshUV,
        float2(FlowDir),
        distortionFloat
    );
    distortion = half2(distortionFloat);
}

#endif // PROCEDURAL_DISTORTION_INCLUDED

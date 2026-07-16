#ifndef FLOW_MAP_HELPERS_INCLUDED
#define FLOW_MAP_HELPERS_INCLUDED

// Helper 1: Visualizes the painted flow direction on the water mesh.
// RG channels visualize the direction mapped from [-1, 1] to [0, 1].
// Blue is 0. Magnitude/strength scales the overall brightness.
void ShowFlowDirection_float(
    float2 FlowDir,
    float ShowFlow,
    float3 BaseColor,
    out float3 OutColor
)
{
    if (ShowFlow > 0.5)
    {
        float len = length(FlowDir);
        if (len > 0.001)
        {
            float2 normalizedDir = FlowDir / len;
            OutColor = float3(normalizedDir * 0.5 + 0.5, 0.0) * saturate(len);
        }
        else
        {
            // Unpainted/default flow area is shown as dark grey
            OutColor = float3(0.1, 0.1, 0.1);
        }
    }
    else
    {
        OutColor = BaseColor;
    }
}

void ShowFlowDirection_half(
    half2 FlowDir,
    half ShowFlow,
    half3 BaseColor,
    out half3 OutColor
)
{
    float3 outColorFloat;
    ShowFlowDirection_float(
        float2(FlowDir),
        float(ShowFlow),
        float3(BaseColor),
        outColorFloat
    );
    OutColor = half3(outColorFloat);
}

// Helper 2: Redirects a default Vector2 panning speed vector to align with the
// flow direction, preserving the default speed magnitude.
void AdjustFoamPanSpeed_float(
    float2 DefaultPanSpeed,
    float2 ResolvedFlowDir,
    out float2 OutPanSpeed
)
{
    float flowStrength = length(ResolvedFlowDir);
    float weight = saturate(flowStrength);
    if (flowStrength > 0.001)
    {
        float defaultSpeed = length(DefaultPanSpeed);
        float2 flowDirNormalized = ResolvedFlowDir / flowStrength;
        float2 flowPanSpeed = -flowDirNormalized * defaultSpeed;
        // Do not lerp speed magnitude to avoid time-dependent coordinate pinching
        OutPanSpeed = flowPanSpeed;
    }
    else
    {
        OutPanSpeed = DefaultPanSpeed;
    }
}

void AdjustFoamPanSpeed_half(
    half2 DefaultPanSpeed,
    half2 ResolvedFlowDir,
    out half2 OutPanSpeed
)
{
    float2 outPanSpeedFloat;
    AdjustFoamPanSpeed_float(
        float2(DefaultPanSpeed),
        float2(ResolvedFlowDir),
        outPanSpeedFloat
    );
    OutPanSpeed = half2(outPanSpeedFloat);
}

#endif // FLOW_MAP_HELPERS_INCLUDED

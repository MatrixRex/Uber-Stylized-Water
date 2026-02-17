// DEBUG HLSL - Use this to diagnose the issue

#ifndef DEBUG_HELPER_INCLUDED
#define DEBUG_HELPER_INCLUDED

void DebugNormalAndView_float(
    float3 WorldNormal,
    float3 WorldPosition,
    out float3 DebugColor
)
{
    // Calculate view direction
    float3 viewDir = normalize(_WorldSpaceCameraPos.xyz - WorldPosition);
    float3 normal = normalize(WorldNormal);
    
    // Visualize the dot product
    float NdotV = dot(normal, viewDir);
    
    // Output as color:
    // Red channel: Raw dot product (will go negative on back facing)
    // Green channel: Absolute dot product
    // Blue channel: Normal Y component
    DebugColor = float3(
        NdotV * 0.5 + 0.5,           // Raw dot, remapped to 0-1
        abs(NdotV),                   // Absolute dot
        normal.y * 0.5 + 0.5          // Normal Y component
    );
}

void DebugWaveNormal_float(
    float3 ObjectSpaceNormal,
    out float3 DebugColor
)
{
    // Visualize the wave normal in object space
    // Should be mostly blue (pointing up in Y)
    DebugColor = ObjectSpaceNormal * 0.5 + 0.5;
}

#endif

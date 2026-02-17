// UNITY_SHADER_NO_UPGRADE
// FRESNEL WITH EXTERNAL CAMERA POSITION

#ifndef FRESNEL_EXTERNAL_CAM_INCLUDED
#define FRESNEL_EXTERNAL_CAM_INCLUDED

void FresnelWithCameraPos_float(
    float3 WorldNormal,
    float3 WorldPosition,
    float3 CameraPosition,
    float Power,
    out float FresnelValue
)
{
    // Calculate view direction FROM surface TO camera
    float3 viewDir = CameraPosition - WorldPosition;
    viewDir = normalize(viewDir);
    
    // Normalize the normal
    float3 normal = normalize(WorldNormal);
    
    // Use abs to handle both viewing directions
    float NdotV = abs(dot(normal, viewDir));
    NdotV = saturate(NdotV);
    
    // Fresnel effect
    FresnelValue = pow(1.0 - NdotV, Power);
}

// Debug version to see what's happening
void DebugFresnelInputs_float(
    float3 WorldNormal,
    float3 WorldPosition,
    float3 CameraPosition,
    out float3 DebugColor
)
{
    float3 viewDir = normalize(CameraPosition - WorldPosition);
    float3 normal = normalize(WorldNormal);
    
    float dotProduct = dot(normal, viewDir);
    float absDot = abs(dotProduct);
    
    // R = raw dot (-1 to 1, remapped to 0-1)
    // G = abs dot (0 to 1)
    // B = constant 0.5 for reference
    DebugColor = float3(
        dotProduct * 0.5 + 0.5,
        absDot,
        0.5
    );
}

#endif

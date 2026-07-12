// Include in a .hlsl file, use via Custom Function node or #include
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

// positionVS: view-space position of water pixel
// reflectDirVS: normalized view-space reflection direction
// Returns: float4(color.rgb, hitMask)
float4 TraceSSR(float3 positionVS, float3 reflectDirVS, int steps, float maxDist, float thickness)
{
    float stepSize = maxDist / steps;
    float3 rayPos = positionVS;
    float2 hitUV = 0;
    float hit = 0;

    UNITY_LOOP
    for (int i = 0; i < steps; i++)
    {
        rayPos += reflectDirVS * stepSize;

        // Project to clip space -> screen UV
        float4 clipPos = mul(UNITY_MATRIX_P, float4(rayPos, 1.0));
        float2 uv = (clipPos.xy / clipPos.w) * 0.5 + 0.5;
        #if UNITY_UV_STARTS_AT_TOP
            uv.y = 1.0 - uv.y;
        #endif

        // Ray left the screen -> no hit
        if (any(uv < 0) || any(uv > 1)) break;

        // Compare ray depth vs scene depth (view space, -Z forward)
        float sceneRawDepth = SampleSceneDepth(uv);
        float sceneEyeDepth = LinearEyeDepth(sceneRawDepth, _ZBufferParams);
        float rayEyeDepth   = -rayPos.z;

        float diff = rayEyeDepth - sceneEyeDepth;
        if (diff > 0 && diff < thickness)
        {
            // Binary search refinement for a sharper hit
            float3 lo = rayPos - reflectDirVS * stepSize;
            float3 hi = rayPos;
            for (int j = 0; j < 5; j++)
            {
                float3 mid = (lo + hi) * 0.5;
                float4 cp = mul(UNITY_MATRIX_P, float4(mid, 1.0));
                #if UNITY_UV_STARTS_AT_TOP
                    float2 muv = (cp.xy / cp.w) * 0.5 + 0.5;
                    muv.y = 1.0 - muv.y;
                #else
                    float2 muv = (cp.xy / cp.w) * 0.5 + 0.5;
                #endif
                float sd = LinearEyeDepth(SampleSceneDepth(muv), _ZBufferParams);
                if (-mid.z > sd) hi = mid; else lo = mid;
            }
            float4 cp2 = mul(UNITY_MATRIX_P, float4(hi, 1.0));
            #if UNITY_UV_STARTS_AT_TOP
                hitUV = (cp2.xy / cp2.w) * 0.5 + 0.5;
                hitUV.y = 1.0 - hitUV.y;
            #else
                hitUV = (cp2.xy / cp2.w) * 0.5 + 0.5;
            #endif
            hit = 1;
            break;
        }
    }

    // Fade near screen edges so misses blend smoothly into fallback
    float2 edge = smoothstep(0.0, 0.1, hitUV) * (1.0 - smoothstep(0.9, 1.0, hitUV));
    float fade = edge.x * edge.y * hit;

    float3 color = SampleSceneColor(hitUV);
    return float4(color, fade);
}

// Entry point for Unity Shader Graph Custom Function node (split Vector3 color and Vector1 mask)
void TraceSSR_float(
    float3 positionVS, 
    float3 reflectDirVS, 
    float steps, 
    float maxDist, 
    float thickness, 
    out float3 OutColor, 
    out float OutMask)
{
    float4 result = TraceSSR(positionVS, reflectDirVS, (int)steps, maxDist, thickness);
    OutColor = result.rgb;
    OutMask = result.a;
}
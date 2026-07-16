#ifndef TWO_WAY_NORMAL_BLEND_INCLUDED
#define TWO_WAY_NORMAL_BLEND_INCLUDED

void TwoWayNormalBlend_float(
    bool UseMeshUV,
    float2 MeshUV,
    float3 WorldPosition,
    float3 WorldNormal,
    float3 WorldTangent,
    float3 WorldBitangent,
    float Time,
    UnityTexture2D NormalMap,
    UnitySamplerState sampler_NormalMap,
    float NormalPan,
    float NormalTile,
    float NormalStrength,
    float DistancelStrength,
    float DistanceMask,
    float2 FlowDir,
    out float3 UnscaledNormal,
    out float3 Normal
)
{
    // 1. Determine base UV coordinates based on UseMeshUV toggle
    float2 baseUV = UseMeshUV ? MeshUV : (WorldPosition.xz * 0.1);

    // 2. Build the two panning UV sets. World and mesh-UV want opposite things
    //    (ambient churn vs. directional flow), so they branch here.
    float2 tile1 = NormalTile * 0.5;
    float2 tile2 = NormalTile * 0.87;
    float2 uv1, uv2;

    float flowStrength = length(FlowDir);
    float flowWeight = saturate(flowStrength);
    float3 n1, n2;

    if (flowWeight > 0.001)
    {
        // 2-Phase Flow Mapping to avoid infinite UV stretching over time
        float2 flowDirNormalized = FlowDir / (flowStrength + 0.0001);
        
        // Rotate flowDirNormalized for each layer to match the default crossing wave angles:
        // Layer 1 default speed is float2(0.35, 0.9)
        // Layer 2 default speed is float2(-0.4, 0.7)
        float2 flowDir1;
        float2 s1 = float2(0.35, 0.9);
        flowDir1.x = s1.x * flowDirNormalized.y + s1.y * flowDirNormalized.x;
        flowDir1.y = -s1.x * flowDirNormalized.x + s1.y * flowDirNormalized.y;
        flowDir1 = normalize(flowDir1);

        float2 flowDir2;
        float2 s2 = float2(-0.4, 0.7);
        flowDir2.x = s2.x * flowDirNormalized.y + s2.y * flowDirNormalized.x;
        flowDir2.y = -s2.x * flowDirNormalized.x + s2.y * flowDirNormalized.y;
        flowDir2 = normalize(flowDir2);

        // Set the phase speeds (constant speed scale to avoid time-dependent spatial stretching)
        float speedScale = NormalPan * 0.1;
        float phase0 = frac(Time * speedScale);
        float phase1 = frac(Time * speedScale + 0.5);

        // Scale maximum distortion offset by flow weight so speed matches brush strength
        float maxDistortion = 0.15 * flowWeight;
        float2 flowVector1 = flowDir1 * maxDistortion;
        float2 flowVector2 = flowDir2 * maxDistortion;

        uv1 = baseUV * tile1 - flowVector1 * phase0;
        uv2 = baseUV * tile2 - flowVector2 * phase1;

        n1 = UnpackNormal(SAMPLE_TEXTURE2D(NormalMap.tex, sampler_NormalMap.samplerstate, uv1));
        n2 = UnpackNormal(SAMPLE_TEXTURE2D(NormalMap.tex, sampler_NormalMap.samplerstate, uv2));

        float blend = abs(0.5 - phase0) / 0.5;
        float3 blendedNormal = lerp(n1, n2, blend);
        n1 = blendedNormal;
        n2 = blendedNormal;
    }
    else
    {
        // Unpainted: default continuous panning
        float2 speed1 = float2( 0.35, 0.9) * NormalPan;
        float2 speed2 = float2(-0.40, 0.7) * NormalPan;

        if (UseMeshUV)
        {
            uv1 = baseUV * tile1 - speed1 * 0.1 * Time;
            uv2 = baseUV * (tile2 * float2(-1.0, 1.0)) - speed2 * 0.1 * Time;
        }
        else
        {
            const float2x2 rot2 = float2x2(0.5, -0.866, 0.866, 0.5);
            uv1 =           baseUV  * tile1 - speed1 * 0.1 * Time;
            uv2 = mul(rot2, baseUV) * tile2 - speed2 * 0.1 * Time;
        }

        n1 = UnpackNormal(SAMPLE_TEXTURE2D(NormalMap.tex, sampler_NormalMap.samplerstate, uv1));
        n2 = UnpackNormal(SAMPLE_TEXTURE2D(NormalMap.tex, sampler_NormalMap.samplerstate, uv2));
    }

    float3 finalNormal1 = n1;
    float3 finalNormal2 = n2;

    // 4. If using World Space projected normals, reconstruct and transform to Tangent Space
    if (!UseMeshUV)
    {
        // Reconstruct world-space normal for Sample 1
        // NormalMap.r -> World X perturbation
        // NormalMap.g -> World Z perturbation
        // NormalMap.b -> World Y (upwards perpendicular component)
        float3 worldNormalSample1 = normalize(float3(WorldNormal.x + n1.r, n1.b, WorldNormal.z + n1.g));
        
        // Transform World Normal 1 to Tangent Space
        finalNormal1 = float3(
            dot(WorldTangent, worldNormalSample1),
            dot(WorldBitangent, worldNormalSample1),
            dot(WorldNormal, worldNormalSample1)
        );

        // Reconstruct world-space normal for Sample 2
        float3 worldNormalSample2 = normalize(float3(WorldNormal.x + n2.r, n2.b, WorldNormal.z + n2.g));
        
        // Transform World Normal 2 to Tangent Space
        finalNormal2 = float3(
            dot(WorldTangent, worldNormalSample2),
            dot(WorldBitangent, worldNormalSample2),
            dot(WorldNormal, worldNormalSample2)
        );
    }

    // 5. Blend the two tangent-space normals with a whiteout/UDN blend.
    // Summing xy and multiplying z keeps ripple amplitude (a linear average
    // would cancel opposing layers toward flat), then normalize to a unit normal.
    UnscaledNormal = normalize(float3(finalNormal1.xy + finalNormal2.xy,
                                      finalNormal1.z  * finalNormal2.z));

    // 6. Calculate and apply interpolated normal strength
    float strength = lerp(NormalStrength, DistancelStrength, DistanceMask);
    
    // Scale tangent space normal components (x, y) by strength, and lerp z component towards 1.0
    Normal = normalize(float3(UnscaledNormal.xy * strength, lerp(1.0, UnscaledNormal.z, saturate(strength))));
}

void TwoWayNormalBlend_half(
    bool UseMeshUV,
    half2 MeshUV,
    half3 WorldPosition,
    half3 WorldNormal,
    half3 WorldTangent,
    half3 WorldBitangent,
    half Time,
    UnityTexture2D NormalMap,
    UnitySamplerState sampler_NormalMap,
    half NormalPan,
    half NormalTile,
    half NormalStrength,
    half DistancelStrength,
    half DistanceMask,
    half2 FlowDir,
    out half3 UnscaledNormal,
    out half3 Normal
)
{
    float3 unscaledNormalFloat;
    float3 normalFloat;
    
    TwoWayNormalBlend_float(
        UseMeshUV,
        float2(MeshUV),
        float3(WorldPosition),
        float3(WorldNormal),
        float3(WorldTangent),
        float3(WorldBitangent),
        float(Time),
        NormalMap,
        sampler_NormalMap,
        float(NormalPan),
        float(NormalTile),
        float(NormalStrength),
        float(DistancelStrength),
        float(DistanceMask),
        float2(FlowDir),
        unscaledNormalFloat,
        normalFloat
    );
    
    UnscaledNormal = half3(unscaledNormalFloat);
    Normal = half3(normalFloat);
}

#endif // TWO_WAY_NORMAL_BLEND_INCLUDED

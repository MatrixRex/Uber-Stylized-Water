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
    out float3 UnscaledNormal,
    out float3 Normal
)
{
    // 1. Determine base UV coordinates based on UseMeshUV toggle
    float2 baseUV = UseMeshUV ? MeshUV : (WorldPosition.xz * 0.1);

    // 2. Calculate Panned UVs for the two samples
    // Sample 1: Tiling = NormalTile * 0.5, Panning Speed = NormalPan * -0.5 (scaled by 0.1 * Time)
    float2 speed1 = float2(NormalPan * -0.5, NormalPan * -0.5);
    float2 tile1 = float2(NormalTile * 0.5, NormalTile * 0.5);
    float2 uv1 = baseUV * tile1 + speed1 * 0.1 * Time;

    // Sample 2: Tiling = NormalTile * 0.73, Panning Speed = NormalPan * 1.0 (scaled by 0.1 * Time)
    float2 speed2 = float2(NormalPan, NormalPan);
    float2 tile2 = float2(NormalTile * 0.73, NormalTile * 0.73);
    float2 uv2 = baseUV * tile2 + speed2 * 0.1 * Time;

    // 3. Sample and Unpack Normals using URP/HDRP Texture2D sampling macros
    float3 n1 = UnpackNormal(SAMPLE_TEXTURE2D(NormalMap.tex, sampler_NormalMap.samplerstate, uv1));
    float3 n2 = UnpackNormal(SAMPLE_TEXTURE2D(NormalMap.tex, sampler_NormalMap.samplerstate, uv2));

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

    // 5. Blend the two tangent-space normal vectors (linear average)
    UnscaledNormal = lerp(finalNormal1, finalNormal2, 0.5);

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
        unscaledNormalFloat,
        normalFloat
    );
    
    UnscaledNormal = half3(unscaledNormalFloat);
    Normal = half3(normalFloat);
}

#endif // TWO_WAY_NORMAL_BLEND_INCLUDED

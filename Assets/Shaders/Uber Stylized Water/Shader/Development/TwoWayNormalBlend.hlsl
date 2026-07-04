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

    // 2. Build the two panning UV sets. World and mesh-UV want opposite things
    //    (ambient churn vs. directional flow), so they branch here.
    float2 tile1 = NormalTile * 0.5;
    float2 tile2 = NormalTile * 0.87;
    float2 uv1, uv2;

    if (UseMeshUV)
    {
        // River: both layers travel downstream (dominant V = uv.y) but at
        // different angles, so they cross like two overlapping wave trains
        // instead of merging into one uniform smear. Opposite U components make
        // them diverge; different tiling + a U mirror on layer 2 break the
        // shared-texture look further. Net flow still clearly reads downstream.
        // (Flip the sign of the V speeds to reverse upstream/downstream.)
        float2 speed1 = float2( 0.35, 0.9) * NormalPan;
        float2 speed2 = float2(-0.40, 0.7) * NormalPan;
        uv1 = baseUV *  tile1                       + speed1 * 0.1 * Time;
        uv2 = baseUV * (tile2 * float2(-1.0, 1.0))  + speed2 * 0.1 * Time;
    }
    else
    {
        // Open water: no net flow. Rotate layer 2's domain ~60 deg to decorrelate
        // the shared texture, then pan it at the exact opposite apparent world
        // velocity to layer 1 (compensating for that rotation and the tiling
        // ratio) so the two drifts cancel and nothing reads as a flow direction.
        const float2x2 rot2 = float2x2(0.5, -0.866, 0.866, 0.5);
        float2 speed1 = float2(-0.5, -0.35) * NormalPan;
        float2 speed2 = -mul(rot2, speed1) * (tile2 / tile1);
        uv1 =           baseUV  * tile1 + speed1 * 0.1 * Time;
        uv2 = mul(rot2, baseUV) * tile2 + speed2 * 0.1 * Time;
    }

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

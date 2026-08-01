#ifndef TWO_WAY_NORMAL_BLEND_INCLUDED
#define TWO_WAY_NORMAL_BLEND_INCLUDED

// Full implementation with Macro Normal Map support and distance culling
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
    UnityTexture2D MacroNormalMap,
    UnitySamplerState sampler_MacroNormalMap,
    float NormalPan,
    float NormalTile,
    float NormalStrength,
    float MacroTile,
    float MacroPan,
    float MacroStrength,
    float DistanceStrength,
    float DistanceMask,
    out float3 UnscaledNormal,
    out float3 Normal
)
{
    // 1. Determine base UV coordinates based on UseMeshUV toggle
    float2 baseUV = UseMeshUV ? MeshUV : (WorldPosition.xz * 0.1);
    float distMask = saturate(DistanceMask);

    // 2. Micro Normals (Sampled when near/mid distance, distMask < 0.99)
    float3 microNormal = float3(0.0, 0.0, 1.0);
    if (distMask < 0.99)
    {
        float2 tile1 = NormalTile * 0.5;
        float2 tile2 = NormalTile * 0.87;
        float2 uv1, uv2;

        if (UseMeshUV)
        {
            float2 speed1 = float2( 0.35, 0.9) * NormalPan;
            float2 speed2 = float2(-0.40, 0.7) * NormalPan;
            uv1 = baseUV *  tile1                       + speed1 * 0.1 * Time;
            uv2 = baseUV * (tile2 * float2(-1.0, 1.0))  + speed2 * 0.1 * Time;
        }
        else
        {
            const float2x2 rot2 = float2x2(0.5, -0.866, 0.866, 0.5);
            float2 speed1 = float2(-0.5, -0.35) * NormalPan;
            float2 speed2 = -mul(rot2, speed1) * (tile2 / tile1);
            uv1 =           baseUV  * tile1 + speed1 * 0.1 * Time;
            uv2 = mul(rot2, baseUV) * tile2 + speed2 * 0.1 * Time;
        }

        float3 n1 = UnpackNormal(SAMPLE_TEXTURE2D(NormalMap.tex, sampler_NormalMap.samplerstate, uv1));
        float3 n2 = UnpackNormal(SAMPLE_TEXTURE2D(NormalMap.tex, sampler_NormalMap.samplerstate, uv2));

        float3 finalN1 = n1;
        float3 finalN2 = n2;

        if (!UseMeshUV)
        {
            float3 worldNormalSample1 = normalize(float3(WorldNormal.x + n1.r, n1.b, WorldNormal.z + n1.g));
            finalN1 = float3(
                dot(WorldTangent, worldNormalSample1),
                dot(WorldBitangent, worldNormalSample1),
                dot(WorldNormal, worldNormalSample1)
            );

            float3 worldNormalSample2 = normalize(float3(WorldNormal.x + n2.r, n2.b, WorldNormal.z + n2.g));
            finalN2 = float3(
                dot(WorldTangent, worldNormalSample2),
                dot(WorldBitangent, worldNormalSample2),
                dot(WorldNormal, worldNormalSample2)
            );
        }

        microNormal = normalize(float3(finalN1.xy + finalN2.xy, finalN1.z * finalN2.z));
    }

    // 3. Macro Normals (Sampled when macro is enabled or distance mask > 0.01)
    float3 macroNormal = float3(0.0, 0.0, 1.0);
    if (distMask > 0.01 || MacroStrength > 0.001)
    {
        float2 macroTile1 = MacroTile * 0.3;
        float2 macroTile2 = MacroTile * 0.55;
        float2 macroUV1, macroUV2;

        if (UseMeshUV)
        {
            float2 speed1 = float2( 0.20, 0.6) * MacroPan;
            float2 speed2 = float2(-0.25, 0.5) * MacroPan;
            macroUV1 = baseUV *  macroTile1                      + speed1 * 0.1 * Time;
            macroUV2 = baseUV * (macroTile2 * float2(-1.0, 1.0)) + speed2 * 0.1 * Time;
        }
        else
        {
            const float2x2 rot2 = float2x2(0.5, -0.866, 0.866, 0.5);
            float2 speed1 = float2(-0.3, -0.2) * MacroPan;
            float2 speed2 = -mul(rot2, speed1) * (macroTile2 / macroTile1);
            macroUV1 =           baseUV  * macroTile1 + speed1 * 0.1 * Time;
            macroUV2 = mul(rot2, baseUV) * macroTile2 + speed2 * 0.1 * Time;
        }

        float3 mn1 = UnpackNormal(SAMPLE_TEXTURE2D(MacroNormalMap.tex, sampler_MacroNormalMap.samplerstate, macroUV1));
        float3 mn2 = UnpackNormal(SAMPLE_TEXTURE2D(MacroNormalMap.tex, sampler_MacroNormalMap.samplerstate, macroUV2));

        float3 finalMN1 = mn1;
        float3 finalMN2 = mn2;

        if (!UseMeshUV)
        {
            float3 worldNormalSample1 = normalize(float3(WorldNormal.x + mn1.r, mn1.b, WorldNormal.z + mn1.g));
            finalMN1 = float3(
                dot(WorldTangent, worldNormalSample1),
                dot(WorldBitangent, worldNormalSample1),
                dot(WorldNormal, worldNormalSample1)
            );

            float3 worldNormalSample2 = normalize(float3(WorldNormal.x + mn2.r, mn2.b, WorldNormal.z + mn2.g));
            finalMN2 = float3(
                dot(WorldTangent, worldNormalSample2),
                dot(WorldBitangent, worldNormalSample2),
                dot(WorldNormal, worldNormalSample2)
            );
        }

        macroNormal = normalize(float3(finalMN1.xy + finalMN2.xy, finalMN1.z * finalMN2.z));
    }

    // 4. Combine Micro + Macro Normals
    // Near distance: UDN blend of scaled Micro and Macro normals
    float3 nearMicroScaled = float3(microNormal.xy * NormalStrength, lerp(1.0, microNormal.z, saturate(NormalStrength)));
    float3 macroScaled     = float3(macroNormal.xy * MacroStrength, lerp(1.0, macroNormal.z, saturate(MacroStrength)));
    
    float3 combinedNear = normalize(float3(nearMicroScaled.xy + macroScaled.xy, nearMicroScaled.z * macroScaled.z));

    // Far distance: Macro normal scaled by DistanceStrength
    float3 farMacro = normalize(float3(macroNormal.xy * DistanceStrength, lerp(1.0, macroNormal.z, saturate(DistanceStrength))));

    // Transition smoothly from Near (Micro + Macro) to Far (Macro only) based on DistanceMask
    UnscaledNormal = normalize(lerp(microNormal, macroNormal, distMask));
    Normal         = normalize(lerp(combinedNear, farMacro, distMask));
}

// Overload for backwards compatibility (reuses NormalMap as MacroNormalMap with default scale)
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
    TwoWayNormalBlend_float(
        UseMeshUV,
        MeshUV,
        WorldPosition,
        WorldNormal,
        WorldTangent,
        WorldBitangent,
        Time,
        NormalMap,
        sampler_NormalMap,
        NormalMap,
        sampler_NormalMap,
        NormalPan,
        NormalTile,
        NormalStrength,
        NormalTile * 0.25, // Default macro tile scale
        NormalPan * 0.5,   // Default macro pan speed
        NormalStrength * 0.5, // Default macro strength
        DistancelStrength,
        DistanceMask,
        UnscaledNormal,
        Normal
    );
}

// Half precision overloads
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
    UnityTexture2D MacroNormalMap,
    UnitySamplerState sampler_MacroNormalMap,
    half NormalPan,
    half NormalTile,
    half NormalStrength,
    half MacroTile,
    half MacroPan,
    half MacroStrength,
    half DistanceStrength,
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
        MacroNormalMap,
        sampler_MacroNormalMap,
        float(NormalPan),
        float(NormalTile),
        float(NormalStrength),
        float(MacroTile),
        float(MacroPan),
        float(MacroStrength),
        float(DistanceStrength),
        float(DistanceMask),
        unscaledNormalFloat,
        normalFloat
    );

    UnscaledNormal = half3(unscaledNormalFloat);
    Normal         = half3(normalFloat);
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

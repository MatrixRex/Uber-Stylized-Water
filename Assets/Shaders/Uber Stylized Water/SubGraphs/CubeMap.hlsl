#ifndef SHADERGRAPH_PREVIEW
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#endif

// Material Keywords
#pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
#pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION

// Unity 6.0 (URP 17.0): the Unlit target doesn't declare these, so declare them here.
// Unity 6.1+ (URP 17.1+): _FORWARD_PLUS was renamed to _CLUSTER_LIGHT_LOOP.
// SHADOWS_SHADOWMASK is intentionally not declared: the water doesn't use baked lighting.
#if UNITY_VERSION < 600010
#pragma multi_compile _ _FORWARD_PLUS
#else
#pragma multi_compile _ _CLUSTER_LIGHT_LOOP
#endif


void GetCubemap_float(float3 ViewDirWS, float3 PositionWS, float3 NormalWS, float Roughness, out float3 Cubemap)
{
    #ifdef SHADERGRAPH_PREVIEW
    Cubemap = 0;
    #else

    half3 reflectionVector = reflect(-ViewDirWS, NormalWS);
    Cubemap = GlossyEnvironmentReflection(reflectionVector, PositionWS, Roughness, 1.0, float2(0,0));
    //Cubemap = GlossyEnvironmentReflection(reflectionVector, Roughness, 1.0);

    #endif
}
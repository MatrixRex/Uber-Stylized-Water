#ifndef SHADERGRAPH_PREVIEW
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#endif

// Material Keywords
#pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
#pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
#pragma multi_compile _ _FORWARD_PLUS


void GetCubemap_float(float3 ViewDirWS, float3 PositionWS, float3 NormalWS, float Roughness, out float3 Cubemap)
{
    #ifdef SHADERGRAPH_PREVIEW
    Cubemap = 0;
    #else

   
    float3 V = normalize(ViewDirWS);
    float3 N = normalize(NormalWS);

    half3 reflectionVector = reflect(-V, N);
    Cubemap = GlossyEnvironmentReflection(reflectionVector, PositionWS, Roughness, 1.0, float2(0,0));

    #endif
}
#ifndef SCENE_WORLD_POS_INCLUDED
#define SCENE_WORLD_POS_INCLUDED

// URP depth texture access -> provides SampleSceneDepth().
// ComputeWorldSpacePosition() and UNITY_MATRIX_I_VP come from the Core SRP
// shader library, which the Shader Graph URP context already includes.
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

// Reconstructs the world-space position of the scene behind this pixel.
// Works for BOTH perspective and orthographic (isometric) cameras, because
// UNITY_MATRIX_I_VP already encodes the projection. Reversed-Z and the
// platform clip-space Y flip are handled inside ComputeWorldSpacePosition.
//
// ScreenUV : normalized 0..1 screen UV (feed a Screen Position node, Default mode)
// WorldPos : reconstructed world-space position of the depth sample

void SceneWorldPos_float(float2 ScreenUV, out float3 WorldPos)
{
    float rawDepth = SampleSceneDepth(ScreenUV);          // raw device depth, do not linearize
    WorldPos = ComputeWorldSpacePosition(ScreenUV, rawDepth, UNITY_MATRIX_I_VP);
}

// Half variant only exists so Shader Graph can resolve the node when its
// precision is set to Half. Position is still computed at full float precision
// to avoid world-space banding on large maps. Prefer setting the node to Single.
void SceneWorldPos_half(float2 ScreenUV, out half3 WorldPos)
{
    float rawDepth = SampleSceneDepth(ScreenUV);
    WorldPos = (half3)ComputeWorldSpacePosition(ScreenUV, rawDepth, UNITY_MATRIX_I_VP);
}

#endif

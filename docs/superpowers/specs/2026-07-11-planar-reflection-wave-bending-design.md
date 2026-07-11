# Design Document: Planar Reflection Wave Bending

This design document outlines the plan to modify the planar reflection rendering logic in the Shader Graph so that reflections are distorted (bent) by both the high-frequency surface ripples (normal maps) and the low-frequency wave geometry displacement.

## Goal
Currently, the planar reflection in `UberStylizedWaterGraph.shadergraph` is only distorted by the high-frequency normal maps, while the wave geometry deformation (which displaces vertices and alters vertex normals in the vertex shader) is ignored. We want the planar reflection to bend and warp according to the wave shape as well.

## Context
1. **Planar Reflection Texture**: Rendered by a camera mirrored across the flat water plane.
2. **Sub Graph `SamplePlannerReflection.shadersubgraph`**: Samples the reflection texture using screen space coordinates (NDC Position) offset by the tangent space normal map.
3. **The Issue**: Tangent space normal map represents only the high-frequency ripples. The vertex wave slopes (geometry deformation) are only present in the world-space vertex normal, which is not factored into the planar reflection offset calculation.

## Proposed Changes
We will modify the sub-graph `SamplePlannerReflection.shadersubgraph` to perform the following:
1. **Transform to World Space**: Transform the tangent-space normal map to world space using the vertex TBN matrix. This automatically incorporates the wave geometry slopes (via the vertex normals).
2. **Transform to View Space**: Transform the combined world-space normal to view space (camera space).
3. **Offset Screen UVs**: Use the `xy` components of the view-space normal, scaled by the distortion strength, to offset the screen coordinates used to sample the reflection texture.

### Sub-Graph Connections
We will insert two `Transform` nodes in the path of the normal map inside the `SamplePlannerReflection.shadersubgraph` graph:
* **Original Connection**:
  `Property: NormalMap` (tangent space) $\rightarrow$ `Normal Strength` (In)
* **New Connection**:
  `Property: NormalMap` (tangent space) $\rightarrow$ `Transform` (Tangent $\rightarrow$ World) $\rightarrow$ `Transform` (World $\rightarrow$ View) $\rightarrow$ `Normal Strength` (In)

This is simple, elegant, and leverages Unity's built-in space transformation pipelines. Because the `Transform` node from Tangent space to World space relies on the vertex TBN matrix, Unity's compiler will automatically fetch and interpolate the wave-deformed vertex normal, tangent, and bitangent, and pass them into the fragment shader.

## Verification Plan
1. Check that `UberStylizedWaterGraph` recompiles in the Unity Editor without errors.
2. Verify that the planar reflections dynamically warp in response to both small ripples and large wave shapes.

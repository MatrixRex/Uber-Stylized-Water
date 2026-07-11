# Design Document: Planar Reflection Wave Bending with Shore Masking

This document outlines the design for distorting planar reflections based on both normal map ripples and wave geometry deformation, while preventing shoreline projection disconnection by masking the distortion at intersections.

## Goal
Modify `SamplePlannerReflection.shadersubgraph` to:
1. Incorporate low-frequency wave geometry slopes in addition to high-frequency ripples.
2. Fade the distortion offset smoothly to zero at water-land intersections/shorelines.

## Proposed Solution

### 1. View Space Space Transformation
To combine wave slopes and ripples, the tangent space normal map is transformed to world space (using the vertex TBN matrix) and then to view space:
$$\text{normalVS} = \text{TransformWorldToView}(\text{TransformTangentToWorld}(\text{NormalMap}))$$

### 2. Shoreline Masking
To prevent the planar reflection from disconnecting at contact points (such as shorelines, rocks, and intersections), we retrieve the globally registered `shorefade` depth variable (which is `0.0` at intersections and climbs to `1.0` in deep water) using a `Get Variable` node.

We then multiply the distortion offset by `shorefade`:
$$\text{Offset} = \text{normalVS}.xy \times \text{ReflectionDistortion} \times \text{Multiplier} \times \text{shorefade}$$

This ensures:
- **At shorelines/intersections (`shorefade` = 0)**: The offset becomes exactly zero. The reflection aligns perfectly with the solid objects (zero projection gap).
- **In deep water (`shorefade` = 1)**: The offset is fully driven by the waves and ripples, producing beautiful dynamic warping.

### 3. Sub-Graph Connections
Inside `SamplePlannerReflection.shadersubgraph`:
- Add `Get Variable` node for `shorefade`.
- Add `Multiply` node to multiply the distortion offset by `shorefade`.
- Wire the connections:
  `Property: NormalMap` $\rightarrow$ `Transform (Tangent $\rightarrow$ World)` $\rightarrow$ `Transform (World $\rightarrow$ View)` $\rightarrow$ `Normal Strength` $\rightarrow$ `Multiply` (A)
  `Get Variable (shorefade)` $\rightarrow$ `Multiply` (B)
  `Multiply` (Out) $\rightarrow$ `Add` $\rightarrow$ `Sample Texture 2D` (UV)

## Verification Plan
1. Check that the sub-graph and main graph compile in Unity.
2. Verify that reflections remain perfectly attached to shorelines and rocks, while bending naturally in deep water with waves.

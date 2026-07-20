# Planar Reflection System Guide

## Overview

The **Planar Reflection System** in this asset provides realtime mirrorlike reflections for flat surfaces. This guide walks through the setup, key features, and how to use the `PlanarReflectionVolume` prefab in your Unity project.

---

## Features

1. **Dynamic Reflections**: Renders a reflection camera based on the real camera's position and orientation.
2. **Volume-Based Blending**: Blends planar reflections to/from ambient reflection probes based on the camera's proximity to the volume. Note that blending occurs between planar reflections and reflection probes, not between multiple overlapping planar volumes.
3. **Reflection Controls**:
   - Customizable render scale for performance.
   - Optional skybox reflections.
   - Layer mask filtering.
4. **Reflection Camera Settings**: Adjustable visibility and clipping.
5. **Gizmos**: Visualize reflection volumes and blending areas in the scene view.
6. **Multiple Volume Support**: Support for multiple volumes in a single scene, utilizing a coordinated manager to share a single camera and texture, reducing performance overhead.

---

## How to Use

### Step 1: Add the 'PlanarReflectionVolume' prefab to the scene

- Drag `Assets/Shaders/Uber Stylized Water/Prefabs/Planner Reflection/PlannerReflectionVolume.prefab` to your scene.

### Step 2: Set up the Centralized Manager

The system requires a **PlanarReflectionManager** in the scene to coordinate the shared camera and rendering:

- If a manager is missing, one will be created automatically at runtime.
- Alternatively, you can click the **Create Planar Reflection Manager** button in the volume's inspector to customize global settings.

### Step 3: Configure the Volume and Targets

The volume can be local or global:

- **Global Volume**: Check **Is Global** to make the volume cover the entire scene. Boundaries and blending will be ignored.
- **Local Volume**: Define boundaries using **Volume Size** and **Blend Distance** (distance where reflections gradually blend out).
- **Reflection Targets**: Add your water plane mesh GameObjects to the **Reflection Targets** list. This determines the plane height and assigns the reflection texture to their materials. Only one target per unique material is needed to apply the reflection settings.

> [!TIP]
> **Water planes at multiple heights:** Placing water planes at different global Y heights within the same volume will result in incorrect/false reflection alignments on some surfaces, as a single flat plane is calculated for the reflection camera. Ensure all targets in a single volume are at the same Y height.

### Step 4: Enable Planar Reflection in the Shader

- Select your water material, and under the **Reflection** category, toggle `EnablePlanerReflection` to true.
- Ensure the `Reflection_Strength` parameter is above zero.

[Full Reflection Properties Guide ↗](usage-guide/shader-properties/shader-prop-reflection.md)

> Now when the camera is inside the volume's range (or anywhere if global), you should see planar reflections.

#### **Script Settings**

- **Is Global**: Toggles whether the volume applies scene-wide.
- **Render Scale** (`0.01 - 1.0`): Adjusts the resolution of the reflection texture. Lower means pixelated reflections but better performance.
- **Reflection Layer**: Choose the layers to include in the reflection camera's rendering.
- **Reflect Skybox**: Toggle whether the skybox is reflected.
- **Reflection Targets**: List of target objects (e.g., water surfaces) for the reflection. Only one target per unique material is needed.
- **Reflection Plane Offset**: Adjusts the height offset of the calculated reflection plane.
- **Hide Reflection Camera**: Toggles visibility of the reflection camera in the Hierarchy.
- **Priority**: Precedence value used when multiple volumes overlap (highest priority wins).

#### **Volume Settings** (Only visible when Is Global is disabled)

- **Volume Size**: Defines the boundaries of the reflection volume.
- **Blend Distance**: Specifies the area around the volume where reflections gradually blend out.

---

## Troubleshooting

- **Reflection Not Visible**: Ensure the reflection target and volume settings are configured correctly. And the reflection power is more then 0.
- **Gap Between Water and reflection**: Adjust the **Reflection Plane Offset.** Make sure to use proper reflection target.
- **Performance Issues**: Lower the **Render Scale** or reduce the layer mask complexity.

---

---

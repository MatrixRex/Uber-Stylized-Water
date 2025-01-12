# Planar Reflection System Guide

## Overview

The **Planar Reflection System** in this asset provides realtime mirrorlike reflections for flat surfaces. This guide walks through the setup, key features, and how to use the `PlanarReflectionVolume` prefab in your Unity project.

---

## Features

1. **Dynamic Reflections**: Renders a reflection camera based on the real camera's position and orientation.
2. **Volume-Based Blending**: Blends reflections based on the camera's proximity to the specified volume.
3. **Reflection Controls**:
   - Customizable render scale for performance.
   - Optional skybox reflections.
   - Layer mask filtering.
4. **Reflection Camera Settings**: Adjustable visibility and clipping.
5. **Gizmos**: Visualize reflection volumes and blending areas in the scene view.

---

## How to Use

### Step 1: Add the 'PlanerRefectionVolume' prefab onto the scene.

- Drag `Assets/Shaders/PolyStream/Uber Stylized Water/Prefabs/Planner Reflection/PlannerReflectionVolume.prefab` to your scene.

### Step 2: Adjust the Volume postion and bounds

The volume has two bounds

- **Inner Volume** (Aqua Box): Displays the reflection volume where reflections are fully visible.
- **Blend Area** (Wireframe Box): Shows the blending area with a fade effect.

### Step 3: Set the reflection target

- In the **'Reflection Target'**, assign your water plane mesh. This will determine the mirror point for reflection camera.

### Step 4: Enable Planner reflection in the shader
- In the water shader under **'Reflection'** catagory set the `EnablePlanerReflection` keyward to true.
- Make sure the `Reflection_Strength` parameter is above zero.

[Full Reflection Properties Guide ↗](https://)

> Now when the camera is inside the Inner volume you should see planer reflection

#### **Script Settings**

- **Render Scale** (`0.01 - 1.0`): Adjusts the resolution of the reflection texture. Lower means jaggeed reflection, but better performance.
- **Reflection Layer**: Choose the layers to include in the reflection.
- **Reflect Skybox**: Toggle whether the skybox is reflected.
- **Reflection Target**: Assign a target object (e.g., water surface) for the reflection.
- **Reflection Plane Offset**: Adjust the reflection plane’s height.
- **Hide Reflection Camera**: Toggle visibility of the reflection camera in the hierarchy.

#### **Volume Settings**

- **Volume Size**: Defines the boundaries of the reflection volume.
- **Blend Distance**: Specifies the area around the volume where reflections gradually blend out.

---

## Troubleshooting

- **Reflection Not Visible**: Ensure the reflection target and volume settings are configured correctly.
- **Artifacts or Clipping Issues**: Adjust the **Reflection Plane Offset** or the clipping plane in the script.
- **Performance Issues**: Lower the **Render Scale** or reduce the layer mask complexity.

---


---

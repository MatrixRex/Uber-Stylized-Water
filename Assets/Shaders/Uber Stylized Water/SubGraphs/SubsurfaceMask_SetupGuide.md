# Subsurface Mask — Shader Graph Setup Guide

## Overview

`SubsurfaceMask.hlsl` generates a mask (0–1) that controls **where** subsurface scattering (SSS) appears on a surface. It outputs three signals:

| Output | Description |
|---|---|
| **Mask** | Combined subsurface mask — plug this into your SSS color multiply |
| **ViewComponent** | View-dependent translucency highlight only |
| **LightThrough** | Back-lit light-through contribution only |

---

## Quick Setup (5 Steps)

### 1. Add a Custom Function Node

In your Shader Graph:

- Right-click → **Create Node** → search **"Custom Function"**
- Set **Type** to **File**
- Drag `SubsurfaceMask.hlsl` into the **Source** field (or browse to `SubGraphs/SubsurfaceMask.hlsl`)
- Set **Name** to `SubsurfaceMask` (must match the function name before `_float`)

### 2. Choose Your Function

| Function Name | Use Case |
|---|---|
| `SubsurfaceMask` | Full control — all inputs exposed |
| `SubsurfaceMaskSimple` | Quick prototype — fewer inputs, uses defaults |

Set the **Name** field on the Custom Function node to whichever you want.

### 3. Configure Inputs (Full Version)

Add these inputs on the Custom Function node **(order matters)**:

| Input | Type | Description | Suggested Default |
|---|---|---|---|
| `WorldNormal` | Vector3 | Surface normal in world space | From Normal Vector node (World) |
| `WorldPosition` | Vector3 | Fragment world position | From Position node (World) |
| `LightDirection` | Vector3 | Direction **to** the main light | Main Light Direction node (negate if needed) |
| `CameraPosition` | Vector3 | Camera world position | Camera Position node |
| `Thickness` | Float | 0 = thin/translucent, 1 = opaque | 0.3 |
| `Distortion` | Float | Normal distortion on light wrap | 0.5 |
| `Power` | Float | Sharpness/tightness of the SSS falloff | 3.0 |
| `Scale` | Float | Intensity multiplier | 1.0 |
| `Attenuation` | Float | Light shadow/attenuation value | 1.0 |
| `AmbientContribution` | Float | Ambient floor for the effect | 0.1 |

Add these outputs:

| Output | Type |
|---|---|
| `Mask` | Float |
| `ViewComponent` | Float |
| `LightThrough` | Float |

### 4. Configure Inputs (Simple Version)

| Input | Type | Suggested Default |
|---|---|---|
| `WorldNormal` | Vector3 | Normal Vector (World) |
| `WorldPosition` | Vector3 | Position (World) |
| `LightDirection` | Vector3 | Main Light Direction |
| `Power` | Float | 3.0 |
| `Scale` | Float | 1.0 |

| Output | Type |
|---|---|
| `Mask` | Float |

### 5. Wire the Mask Output

Typical usage:

```
[SubsurfaceMask] → Mask → Multiply → [SSS Color]
                                 ↓
                          Add → [Base Color / Emission]
```

- **Multiply** the `Mask` output with your desired **SSS Color** (e.g., warm teal/green for water).
- **Add** the result to your base color or feed it into **Emission** for a glow effect.

---

## Getting the Light Direction

In URP Shader Graph, use one of these approaches:

**Option A — Main Light Node (URP)**
- Use the built-in **Main Light Direction** node (available in URP 12+).
- Note: the direction points **from** the light. You may need to **Negate** it so it points **to** the light.

**Option B — Manual Property**
- Create a `Vector3` property called `_LightDir`.
- Set it from a script via `Shader.SetGlobalVector("_LightDir", -mainLight.transform.forward)`.

---

## Parameter Tuning Tips

| Parameter | Low Value Effect | High Value Effect |
|---|---|---|
| **Distortion** | Tight, focused SSS highlight | Broad, diffused SSS spread |
| **Power** | Soft, wide falloff | Sharp, concentrated highlight |
| **Scale** | Subtle effect | Intense/blown-out |
| **Thickness** | Very translucent (thin skin/water) | Opaque (no SSS) |
| **AmbientContribution** | SSS only in direct light | SSS persists in shadow |

### Recommended Starting Values for Water

```
Thickness:            0.2
Distortion:           0.5
Power:                3.0
Scale:                1.5
Attenuation:          1.0
AmbientContribution:  0.15
SSS Color:            #00BFA5 (teal-green)
```

---

## Notes

- Both functions use the `_float` suffix required by Shader Graph for **float** precision.
- The `#ifndef` include guard prevents double-inclusion.
- `SubsurfaceMaskSimple` uses `_WorldSpaceCameraPos` internally so it doesn't need a camera input.
- The full version takes `CameraPosition` as an explicit input for flexibility (e.g., VR per-eye cameras, custom camera setups).

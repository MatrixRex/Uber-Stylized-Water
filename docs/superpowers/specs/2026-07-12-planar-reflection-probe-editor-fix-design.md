# Design Document: Coordinated Planar Reflection Manager & Volumes

This document outlines the design for resolving planar reflection rendering and lifecycle conflicts in the Unity Editor when multiple reflection probes (volumes) are present in the same scene.

## Goal
Modify the Planar Reflection system to support multiple `PlanarReflectionVolume` components on the same scene without conflicts, lifecycle crashes, or flickering.

## Proposed Solution

Instead of each `PlanarReflectionVolume` managing its own camera and rendering to a shared static texture independently, we split the architecture into two components:
1. **`PlanarReflectionVolume`**: Stores spatial boundaries, blending distance, priorities, and rendering settings. It has no rendering/update logic.
2. **`PlanarReflectionManager`**: A centralized, auto-instantiated singleton that manages the lifecycle of the single reflection camera and texture. It coordinates which volume is active and executes the actual rendering once per camera.

### 1. `PlanarReflectionVolume` Changes
- Expose `priority` (int): Determines precedence when volumes overlap (default `0`).
- Register / Unregister to/from the `PlanarReflectionManager` on `OnEnable` / `OnDisable` / `OnDestroy`.
- Expose target material reference `targetMaterial` and update it in `OnValidate()`.

### 2. `PlanarReflectionManager` Implementation
- Configurable settings:
  - `runOnEditMode` (bool): Toggles editor camera rendering (default `true`). Exposed in the manager's inspector.
- Auto-created dynamically in the scene as a standard GameObject when any volume registers and it doesn't already exist. This allows the user to select the manager in the Hierarchy to toggle its settings.
- Cleans up the reflection camera and texture only when the last volume is unregistered.
- Centralizes the rendering logic inside `LateUpdate()`.
- Implements active volume selection:
  - Iterates through registered volumes.
  - Determines the active volume for each camera (Scene View or Game View) based on `priority` (highest first) and `blendFactor` (lowest first).
- Implements flexible camera selection using its `runOnEditMode` property:
  - If `runOnEditMode` is `true`: Editor camera is used in Edit Mode, Game camera (`Camera.main`) in Play Mode.
  - If `runOnEditMode` is `false`: Game camera is used in both Play and Edit Mode. If not found, log a warning (throttled to once) and fallback to the editor Scene View camera.
- Sets the blend factor `_PlannerReflectionBlend` on each volume's material:
  - Active volume gets its calculated blend factor.
  - Inactive volumes get `1f` (disabled), unless they share the same material with the active volume, in which case the active volume's blend factor takes precedence.
- Why `LateUpdate` instead of `beginCameraRendering`:
  - Submitting camera render requests inside URP's `beginCameraRendering` event can cause rendering loops, recursion, or pipeline state issues. Running in `LateUpdate` (along with Editor updates) renders safely outside the active pipeline loop.

## Verification Plan
1. Check that the script compiles successfully in Unity.
2. Place multiple `PlanarReflectionVolume` components in a scene at different heights/positions:
   - Ensure they do not delete/recreate the reflection camera constantly.
   - Verify that moving the camera near different volumes correctly transitions the reflections.
3. Test overlapping volumes with different priorities to verify priority selection.
4. Verify `runOnEditMode` toggling on the `PlanarReflectionManager` component:
   - When `true`, Scene View camera drives reflection positions in the editor.
   - When `false`, Game View camera drives reflection positions in the editor (or falls back with a warning if no game camera exists).

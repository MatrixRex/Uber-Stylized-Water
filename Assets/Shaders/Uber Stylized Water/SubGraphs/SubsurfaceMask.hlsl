// UNITY_SHADER_NO_UPGRADE
// SUBSURFACE SCATTERING MASK FOR STYLIZED WATER
// Generates a mask indicating where sunlight shines through wave crests.

#ifndef SUBSURFACE_MASK_INCLUDED
#define SUBSURFACE_MASK_INCLUDED

// ─────────────────────────────────────────────────────────────────────
// Main Function
// Computes a stylized SSS mask based on view direction, light direction,
// and wave height (to restrict SSS to wave tips).
// ─────────────────────────────────────────────────────────────────────
void SubsurfaceMask_float(
    float3 WorldNormal,          // Wave-perturbed normal
    float3 ViewDirection,        // Normalized direction from surface to camera
    float3 LightDirection,       // Normalized direction TO the light
    float  NormalDistortion,     // How much the normal distorts the light wrap (0.0 - 1.0)
    float  Power,                // Sharpness of the scattering (higher = tighter spot)
    float  Scale,                // Overall intensity multiplier
    float  WaveHeight,           // Height of the wave at this pixel (from WaveGenerator)
    float  HeightMaskStrength,   // How strongly to mask SSS to the tips (0 = valid everywhere, 1 = tips only)
    float  HeightMaskOffset,     // Offset for the height mask (controls "how deep" the SSS goes)
    float  ShadowAttenuation,    // 0 = in shadow, 1 = fully lit
    out float Mask               // Final SSS intensity (0-1)
)
{
    // 1. Normalize Vectors
    float3 N = normalize(WorldNormal);
    float3 V = normalize(ViewDirection);
    float3 L = normalize(LightDirection);

    // 2. Distorted Back-Light Vector
    // We distort the light vector by the normal to simulate light wrapping around the wave volume.
    // H represents the "perfect" direction for SSS.
    float3 H = normalize(-L + N * NormalDistortion);

    // 3. View-Dependent Scattering (The "Core" SSS)
    // Measures how much we are looking "through" the wave towards the light.
    float VdotH = saturate(dot(V, H));
    float scattering = pow(VdotH, Power);

    // 4. Height Masking
    // Restricts SSS to the upper parts of the waves (crests).
    // Assumes WaveHeight is generally positive (e.g., -1 to 1 or 0 to 1 range).
    // We want higher values to have SSS, lower values to have none.
    float heightMask = saturate((WaveHeight + HeightMaskOffset) * HeightMaskStrength);
    
    // Optional: Smoothstep for softer height transition
    heightMask = smoothstep(0.0, 1.0, heightMask);

    // 5. Back-Face Logic
    // SSS should primarily be visible when looking *into* the light.
    // If the light is behind the viewer (dot(V, L) > 0), SSS should diminish.
    // (Optional: simple version just uses VdotH, but we can enforce back-lighting)
    // float backLightLogic = saturate(-dot(V, L)); // 1 when looking at light, 0 when looking away

    // 6. Combine
    // Scattering * Intensity * HeightMask * Shadow * LightColorLogic (handled externally or here)
    float mask = scattering * Scale * heightMask * ShadowAttenuation;

    Mask = mask;
}

#endif

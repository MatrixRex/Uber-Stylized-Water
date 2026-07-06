// StylizedAdditionalLights.hlsl
// Custom lighting calculations for additional lights (point and spot lights) in the stylized water shader.

#ifndef STYLIZED_ADDITIONAL_LIGHTS_INCLUDED
#define STYLIZED_ADDITIONAL_LIGHTS_INCLUDED

// ==========================================
// URP additional-light keywords
// ==========================================
// Declared here (instead of the Shader Graph Blackboard) so any graph using this
// Custom Function node compiles the additional-light variants automatically.
// Without these, GetAdditionalLightsCount() returns 0 and the light loops output black.
#pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
#pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
#pragma multi_compile_fragment _ _SHADOWS_SOFT
#pragma multi_compile _ _FORWARD_PLUS

// ==========================================
// Main Float Precision Implementation
// ==========================================

void StylizedAdditionalLights_float(
    float Smoothness, 
    float3 WorldPosition, 
    float3 WorldNormal, 
    float3 WorldView, 
    float Distortion,
    float SpecularSize, 
    float SpecularHardness, 
    half4 Shadowmask,
    float3 GeoNormal,
    out float3 Diffuse, 
    out float3 Specular) 
{
    float3 diffuseColor = 0;
    float3 specularColor = 0;

#ifndef SHADERGRAPH_PREVIEW
    Smoothness = exp2(10.0f * Smoothness + 1.0f);
    float3 Ngeo = normalize(GeoNormal);
    float3 Nperturbed = normalize(WorldNormal);
    
    // Distort the normal based on Distortion and the difference between perturbed and geometry normal
    float3 distortedNormal = normalize(Ngeo + (Nperturbed - Ngeo) * Distortion);
    
    WorldView = SafeNormalize(WorldView);
    
    // Pre-calculate threshold values to match your graph logic
    float edge1 = 1.0f - SpecularSize;
    float edge2 = edge1 + SpecularHardness;

    uint pixelLightCount = GetAdditionalLightsCount();
    uint meshRenderingLayers = GetMeshRenderingLayer();

    #if USE_FORWARD_PLUS
    for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++) {
        FORWARD_PLUS_SUBTRACTIVE_LIGHT_CHECK
        Light light = GetAdditionalLight(lightIndex, WorldPosition, Shadowmask);
        #ifdef _LIGHT_LAYERS
        if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
        #endif
        {
            float3 attenuatedLightColor = light.color * (light.distanceAttenuation * light.shadowAttenuation);
            
            // Diffuse (Standard Lambert)
            diffuseColor += LightingLambert(attenuatedLightColor, light.direction, Nperturbed);

            // Specular (Custom Stylized with distortion)
            float3 halfVec = SafeNormalize(light.direction + WorldView);
            float NdotH = saturate(dot(distortedNormal, halfVec));
            float modifier = pow(NdotH, Smoothness);
            
            // Apply Hardness/Step Logic here (on scalar intensity)
            modifier = smoothstep(edge1, edge2, modifier);
            
            specularColor += attenuatedLightColor * modifier;
        }
    }
    #endif

    // Standard Light Loop (Non-Forward+)
    InputData inputData = (InputData)0;
    float4 screenPos = ComputeScreenPos(TransformWorldToHClip(WorldPosition));
    inputData.normalizedScreenSpaceUV = screenPos.xy / screenPos.w;
    inputData.positionWS = WorldPosition;

    LIGHT_LOOP_BEGIN(pixelLightCount)
        Light light = GetAdditionalLight(lightIndex, WorldPosition, Shadowmask);
        #ifdef _LIGHT_LAYERS
        if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
        #endif
        {
            float3 attenuatedLightColor = light.color * (light.distanceAttenuation * light.shadowAttenuation);
            
            // Diffuse
            diffuseColor += LightingLambert(attenuatedLightColor, light.direction, Nperturbed);

            // Specular
            float3 halfVec = SafeNormalize(light.direction + WorldView);
            float NdotH = saturate(dot(distortedNormal, halfVec));
            float modifier = pow(NdotH, Smoothness);
            
            // Apply Hardness
            modifier = smoothstep(edge1, edge2, modifier);
            
            specularColor += attenuatedLightColor * modifier;
        }
    LIGHT_LOOP_END
#endif

    Diffuse = diffuseColor;
    Specular = specularColor;
}

// ==========================================
// Float Precision Output Overloads (Shadowmask version)
// ==========================================

void StylizedAdditionalLights_float(
    float Smoothness, float3 WorldPosition, float3 WorldNormal, float3 WorldView, float Distortion,
    float SpecularSize, float SpecularHardness, half4 Shadowmask, float3 GeoNormal,
    out float Diffuse, out float3 Specular)
{
    float3 diff, spec;
    StylizedAdditionalLights_float(Smoothness, WorldPosition, WorldNormal, WorldView, Distortion, SpecularSize, SpecularHardness, Shadowmask, GeoNormal, diff, spec);
    Diffuse = diff.x;
    Specular = spec;
}

void StylizedAdditionalLights_float(
    float Smoothness, float3 WorldPosition, float3 WorldNormal, float3 WorldView, float Distortion,
    float SpecularSize, float SpecularHardness, half4 Shadowmask, float3 GeoNormal,
    out float3 Diffuse, out float Specular)
{
    float3 diff, spec;
    StylizedAdditionalLights_float(Smoothness, WorldPosition, WorldNormal, WorldView, Distortion, SpecularSize, SpecularHardness, Shadowmask, GeoNormal, diff, spec);
    Diffuse = diff;
    Specular = spec.x;
}

void StylizedAdditionalLights_float(
    float Smoothness, float3 WorldPosition, float3 WorldNormal, float3 WorldView, float Distortion,
    float SpecularSize, float SpecularHardness, half4 Shadowmask, float3 GeoNormal,
    out float Diffuse, out float Specular)
{
    float3 diff, spec;
    StylizedAdditionalLights_float(Smoothness, WorldPosition, WorldNormal, WorldView, Distortion, SpecularSize, SpecularHardness, Shadowmask, GeoNormal, diff, spec);
    Diffuse = diff.x;
    Specular = spec.x;
}

void StylizedAdditionalLights_float(
    float Smoothness, float3 WorldPosition, float3 WorldNormal, float3 WorldView, float Distortion,
    float SpecularSize, float SpecularHardness, half4 Shadowmask, float3 GeoNormal,
    out half3 Diffuse, out half3 Specular)
{
    float3 diff, spec;
    StylizedAdditionalLights_float(Smoothness, WorldPosition, WorldNormal, WorldView, Distortion, SpecularSize, SpecularHardness, Shadowmask, GeoNormal, diff, spec);
    Diffuse = (half3)diff;
    Specular = (half3)spec;
}

void StylizedAdditionalLights_float(
    float Smoothness, float3 WorldPosition, float3 WorldNormal, float3 WorldView, float Distortion,
    float SpecularSize, float SpecularHardness, half4 Shadowmask, float3 GeoNormal,
    out half Diffuse, out half3 Specular)
{
    float3 diff, spec;
    StylizedAdditionalLights_float(Smoothness, WorldPosition, WorldNormal, WorldView, Distortion, SpecularSize, SpecularHardness, Shadowmask, GeoNormal, diff, spec);
    Diffuse = (half)diff.x;
    Specular = (half3)spec;
}

void StylizedAdditionalLights_float(
    float Smoothness, float3 WorldPosition, float3 WorldNormal, float3 WorldView, float Distortion,
    float SpecularSize, float SpecularHardness, half4 Shadowmask, float3 GeoNormal,
    out half3 Diffuse, out half Specular)
{
    float3 diff, spec;
    StylizedAdditionalLights_float(Smoothness, WorldPosition, WorldNormal, WorldView, Distortion, SpecularSize, SpecularHardness, Shadowmask, GeoNormal, diff, spec);
    Diffuse = (half3)diff;
    Specular = (half)spec.x;
}

void StylizedAdditionalLights_float(
    float Smoothness, float3 WorldPosition, float3 WorldNormal, float3 WorldView, float Distortion,
    float SpecularSize, float SpecularHardness, half4 Shadowmask, float3 GeoNormal,
    out half Diffuse, out half Specular)
{
    float3 diff, spec;
    StylizedAdditionalLights_float(Smoothness, WorldPosition, WorldNormal, WorldView, Distortion, SpecularSize, SpecularHardness, Shadowmask, GeoNormal, diff, spec);
    Diffuse = (half)diff.x;
    Specular = (half)spec.x;
}

// ==========================================
// Float Precision Output Overloads (Backwards Compatible 10-param version)
// ==========================================

void StylizedAdditionalLights_float(
    float Smoothness, float3 WorldPosition, float3 WorldNormal, float3 WorldView, float Distortion,
    float SpecularSize, float SpecularHardness, float3 GeoNormal,
    out float3 Diffuse, out float3 Specular)
{
    StylizedAdditionalLights_float(Smoothness, WorldPosition, WorldNormal, WorldView, Distortion, SpecularSize, SpecularHardness, half4(1,1,1,1), GeoNormal, Diffuse, Specular);
}

void StylizedAdditionalLights_float(
    float Smoothness, float3 WorldPosition, float3 WorldNormal, float3 WorldView, float Distortion,
    float SpecularSize, float SpecularHardness, float3 GeoNormal,
    out float Diffuse, out float3 Specular)
{
    float3 diff, spec;
    StylizedAdditionalLights_float(Smoothness, WorldPosition, WorldNormal, WorldView, Distortion, SpecularSize, SpecularHardness, half4(1,1,1,1), GeoNormal, diff, spec);
    Diffuse = diff.x;
    Specular = spec;
}

void StylizedAdditionalLights_float(
    float Smoothness, float3 WorldPosition, float3 WorldNormal, float3 WorldView, float Distortion,
    float SpecularSize, float SpecularHardness, float3 GeoNormal,
    out float3 Diffuse, out float Specular)
{
    float3 diff, spec;
    StylizedAdditionalLights_float(Smoothness, WorldPosition, WorldNormal, WorldView, Distortion, SpecularSize, SpecularHardness, half4(1,1,1,1), GeoNormal, diff, spec);
    Diffuse = diff;
    Specular = spec.x;
}

void StylizedAdditionalLights_float(
    float Smoothness, float3 WorldPosition, float3 WorldNormal, float3 WorldView, float Distortion,
    float SpecularSize, float SpecularHardness, float3 GeoNormal,
    out float Diffuse, out float Specular)
{
    float3 diff, spec;
    StylizedAdditionalLights_float(Smoothness, WorldPosition, WorldNormal, WorldView, Distortion, SpecularSize, SpecularHardness, half4(1,1,1,1), GeoNormal, diff, spec);
    Diffuse = diff.x;
    Specular = spec.x;
}

void StylizedAdditionalLights_float(
    float Smoothness, float3 WorldPosition, float3 WorldNormal, float3 WorldView, float Distortion,
    float SpecularSize, float SpecularHardness, float3 GeoNormal,
    out half3 Diffuse, out half3 Specular)
{
    float3 diff, spec;
    StylizedAdditionalLights_float(Smoothness, WorldPosition, WorldNormal, WorldView, Distortion, SpecularSize, SpecularHardness, half4(1,1,1,1), GeoNormal, diff, spec);
    Diffuse = (half3)diff;
    Specular = (half3)spec;
}

void StylizedAdditionalLights_float(
    float Smoothness, float3 WorldPosition, float3 WorldNormal, float3 WorldView, float Distortion,
    float SpecularSize, float SpecularHardness, float3 GeoNormal,
    out half Diffuse, out half3 Specular)
{
    float3 diff, spec;
    StylizedAdditionalLights_float(Smoothness, WorldPosition, WorldNormal, WorldView, Distortion, SpecularSize, SpecularHardness, half4(1,1,1,1), GeoNormal, diff, spec);
    Diffuse = (half)diff.x;
    Specular = (half3)spec;
}

void StylizedAdditionalLights_float(
    float Smoothness, float3 WorldPosition, float3 WorldNormal, float3 WorldView, float Distortion,
    float SpecularSize, float SpecularHardness, float3 GeoNormal,
    out half3 Diffuse, out half Specular)
{
    float3 diff, spec;
    StylizedAdditionalLights_float(Smoothness, WorldPosition, WorldNormal, WorldView, Distortion, SpecularSize, SpecularHardness, half4(1,1,1,1), GeoNormal, diff, spec);
    Diffuse = (half3)diff;
    Specular = (half)spec.x;
}

void StylizedAdditionalLights_float(
    float Smoothness, float3 WorldPosition, float3 WorldNormal, float3 WorldView, float Distortion,
    float SpecularSize, float SpecularHardness, float3 GeoNormal,
    out half Diffuse, out half Specular)
{
    float3 diff, spec;
    StylizedAdditionalLights_float(Smoothness, WorldPosition, WorldNormal, WorldView, Distortion, SpecularSize, SpecularHardness, half4(1,1,1,1), GeoNormal, diff, spec);
    Diffuse = (half)diff.x;
    Specular = (half)spec.x;
}


// ==========================================
// Main Half Precision Implementation
// ==========================================

void StylizedAdditionalLights_half(
    half Smoothness, 
    half3 WorldPosition, 
    half3 WorldNormal, 
    half3 WorldView, 
    half Distortion,
    half SpecularSize, 
    half SpecularHardness, 
    half4 Shadowmask,
    half3 GeoNormal,
    out half3 Diffuse, 
    out half3 Specular) 
{
    half3 diffuseColor = 0.0h;
    half3 specularColor = 0.0h;

#ifndef SHADERGRAPH_PREVIEW
    Smoothness = exp2(10.0h * Smoothness + 1.0h);
    half3 Ngeo = normalize(GeoNormal);
    half3 Nperturbed = normalize(WorldNormal);
    
    // Distort the normal based on Distortion and the difference between perturbed and geometry normal
    half3 distortedNormal = normalize(Ngeo + (Nperturbed - Ngeo) * Distortion);
    
    WorldView = SafeNormalize(WorldView);
    
    // Pre-calculate threshold values to match your graph logic
    half edge1 = 1.0h - SpecularSize;
    half edge2 = edge1 + SpecularHardness;

    uint pixelLightCount = GetAdditionalLightsCount();
    uint meshRenderingLayers = GetMeshRenderingLayer();

    #if USE_FORWARD_PLUS
    for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++) {
        FORWARD_PLUS_SUBTRACTIVE_LIGHT_CHECK
        Light light = GetAdditionalLight(lightIndex, WorldPosition, Shadowmask);
        #ifdef _LIGHT_LAYERS
        if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
        #endif
        {
            half3 attenuatedLightColor = half3(light.color) * half(light.distanceAttenuation * light.shadowAttenuation);
            
            // Diffuse (Standard Lambert)
            diffuseColor += half3(LightingLambert(attenuatedLightColor, light.direction, Nperturbed));

            // Specular (Custom Stylized with distortion)
            half3 halfVec = SafeNormalize(half3(light.direction) + WorldView);
            half NdotH = saturate(dot(distortedNormal, halfVec));
            half modifier = pow(NdotH, Smoothness);
            
            // Apply Hardness/Step Logic here (on scalar intensity)
            modifier = smoothstep(edge1, edge2, modifier);
            
            specularColor += attenuatedLightColor * modifier;
        }
    }
    #endif

    // Standard Light Loop (Non-Forward+)
    InputData inputData = (InputData)0;
    float4 screenPos = ComputeScreenPos(TransformWorldToHClip(WorldPosition));
    inputData.normalizedScreenSpaceUV = screenPos.xy / screenPos.w;
    inputData.positionWS = WorldPosition;

    LIGHT_LOOP_BEGIN(pixelLightCount)
        Light light = GetAdditionalLight(lightIndex, WorldPosition, Shadowmask);
        #ifdef _LIGHT_LAYERS
        if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
        #endif
        {
            half3 attenuatedLightColor = half3(light.color) * half(light.distanceAttenuation * light.shadowAttenuation);
            
            // Diffuse
            diffuseColor += half3(LightingLambert(attenuatedLightColor, light.direction, Nperturbed));

            // Specular
            half3 halfVec = SafeNormalize(half3(light.direction) + WorldView);
            half NdotH = saturate(dot(distortedNormal, halfVec));
            half modifier = pow(NdotH, Smoothness);
            
            // Apply Hardness
            modifier = smoothstep(edge1, edge2, modifier);
            
            specularColor += attenuatedLightColor * modifier;
        }
    LIGHT_LOOP_END
#endif

    Diffuse = diffuseColor;
    Specular = specularColor;
}

// ==========================================
// Half Precision Output Overloads (Shadowmask version)
// ==========================================

void StylizedAdditionalLights_half(
    half Smoothness, half3 WorldPosition, half3 WorldNormal, half3 WorldView, half Distortion,
    half SpecularSize, half SpecularHardness, half4 Shadowmask, half3 GeoNormal,
    out half Diffuse, out half3 Specular)
{
    half3 diff, spec;
    StylizedAdditionalLights_half(Smoothness, WorldPosition, WorldNormal, WorldView, Distortion, SpecularSize, SpecularHardness, Shadowmask, GeoNormal, diff, spec);
    Diffuse = diff.x;
    Specular = spec;
}

void StylizedAdditionalLights_half(
    half Smoothness, half3 WorldPosition, half3 WorldNormal, half3 WorldView, half Distortion,
    half SpecularSize, half SpecularHardness, half4 Shadowmask, half3 GeoNormal,
    out half3 Diffuse, out half Specular)
{
    half3 diff, spec;
    StylizedAdditionalLights_half(Smoothness, WorldPosition, WorldNormal, WorldView, Distortion, SpecularSize, SpecularHardness, Shadowmask, GeoNormal, diff, spec);
    Diffuse = diff;
    Specular = spec.x;
}

void StylizedAdditionalLights_half(
    half Smoothness, half3 WorldPosition, half3 WorldNormal, half3 WorldView, half Distortion,
    half SpecularSize, half SpecularHardness, half4 Shadowmask, half3 GeoNormal,
    out half Diffuse, out half Specular)
{
    half3 diff, spec;
    StylizedAdditionalLights_half(Smoothness, WorldPosition, WorldNormal, WorldView, Distortion, SpecularSize, SpecularHardness, Shadowmask, GeoNormal, diff, spec);
    Diffuse = diff.x;
    Specular = spec.x;
}

void StylizedAdditionalLights_half(
    half Smoothness, half3 WorldPosition, half3 WorldNormal, half3 WorldView, half Distortion,
    half SpecularSize, half SpecularHardness, half4 Shadowmask, half3 GeoNormal,
    out float3 Diffuse, out float3 Specular)
{
    half3 diff, spec;
    StylizedAdditionalLights_half(Smoothness, WorldPosition, WorldNormal, WorldView, Distortion, SpecularSize, SpecularHardness, Shadowmask, GeoNormal, diff, spec);
    Diffuse = (float3)diff;
    Specular = (float3)spec;
}

void StylizedAdditionalLights_half(
    half Smoothness, half3 WorldPosition, half3 WorldNormal, half3 WorldView, half Distortion,
    half SpecularSize, half SpecularHardness, half4 Shadowmask, half3 GeoNormal,
    out float Diffuse, out float3 Specular)
{
    half3 diff, spec;
    StylizedAdditionalLights_half(Smoothness, WorldPosition, WorldNormal, WorldView, Distortion, SpecularSize, SpecularHardness, Shadowmask, GeoNormal, diff, spec);
    Diffuse = (float)diff.x;
    Specular = (float3)spec;
}

void StylizedAdditionalLights_half(
    half Smoothness, half3 WorldPosition, half3 WorldNormal, half3 WorldView, half Distortion,
    half SpecularSize, half SpecularHardness, half4 Shadowmask, half3 GeoNormal,
    out float3 Diffuse, out float Specular)
{
    half3 diff, spec;
    StylizedAdditionalLights_half(Smoothness, WorldPosition, WorldNormal, WorldView, Distortion, SpecularSize, SpecularHardness, Shadowmask, GeoNormal, diff, spec);
    Diffuse = (float3)diff;
    Specular = (float)spec.x;
}

void StylizedAdditionalLights_half(
    half Smoothness, half3 WorldPosition, half3 WorldNormal, half3 WorldView, half Distortion,
    half SpecularSize, half SpecularHardness, half4 Shadowmask, half3 GeoNormal,
    out float Diffuse, out float Specular)
{
    half3 diff, spec;
    StylizedAdditionalLights_half(Smoothness, WorldPosition, WorldNormal, WorldView, Distortion, SpecularSize, SpecularHardness, Shadowmask, GeoNormal, diff, spec);
    Diffuse = (float)diff.x;
    Specular = (float)spec.x;
}

// ==========================================
// Half Precision Output Overloads (Backwards Compatible 10-param version)
// ==========================================

void StylizedAdditionalLights_half(
    half Smoothness, half3 WorldPosition, half3 WorldNormal, half3 WorldView, half Distortion,
    half SpecularSize, half SpecularHardness, half3 GeoNormal,
    out half3 Diffuse, out half3 Specular)
{
    StylizedAdditionalLights_half(Smoothness, WorldPosition, WorldNormal, WorldView, Distortion, SpecularSize, SpecularHardness, half4(1,1,1,1), GeoNormal, Diffuse, Specular);
}

void StylizedAdditionalLights_half(
    half Smoothness, half3 WorldPosition, half3 WorldNormal, half3 WorldView, half Distortion,
    half SpecularSize, half SpecularHardness, half3 GeoNormal,
    out half Diffuse, out half3 Specular)
{
    half3 diff, spec;
    StylizedAdditionalLights_half(Smoothness, WorldPosition, WorldNormal, WorldView, Distortion, SpecularSize, SpecularHardness, half4(1,1,1,1), GeoNormal, diff, spec);
    Diffuse = diff.x;
    Specular = spec;
}

void StylizedAdditionalLights_half(
    half Smoothness, half3 WorldPosition, half3 WorldNormal, half3 WorldView, half Distortion,
    half SpecularSize, half SpecularHardness, half3 GeoNormal,
    out half3 Diffuse, out half Specular)
{
    half3 diff, spec;
    StylizedAdditionalLights_half(Smoothness, WorldPosition, WorldNormal, WorldView, Distortion, SpecularSize, SpecularHardness, half4(1,1,1,1), GeoNormal, diff, spec);
    Diffuse = diff;
    Specular = spec.x;
}

void StylizedAdditionalLights_half(
    half Smoothness, half3 WorldPosition, half3 WorldNormal, half3 WorldView, half Distortion,
    half SpecularSize, half SpecularHardness, half3 GeoNormal,
    out half Diffuse, out half Specular)
{
    half3 diff, spec;
    StylizedAdditionalLights_half(Smoothness, WorldPosition, WorldNormal, WorldView, Distortion, SpecularSize, SpecularHardness, half4(1,1,1,1), GeoNormal, diff, spec);
    Diffuse = diff.x;
    Specular = spec.x;
}

void StylizedAdditionalLights_half(
    half Smoothness, half3 WorldPosition, half3 WorldNormal, half3 WorldView, half Distortion,
    half SpecularSize, half SpecularHardness, half3 GeoNormal,
    out float3 Diffuse, out float3 Specular)
{
    half3 diff, spec;
    StylizedAdditionalLights_half(Smoothness, WorldPosition, WorldNormal, WorldView, Distortion, SpecularSize, SpecularHardness, half4(1,1,1,1), GeoNormal, diff, spec);
    Diffuse = (float3)diff;
    Specular = (float3)spec;
}

void StylizedAdditionalLights_half(
    half Smoothness, half3 WorldPosition, half3 WorldNormal, half3 WorldView, half Distortion,
    half SpecularSize, half SpecularHardness, half3 GeoNormal,
    out float Diffuse, out float3 Specular)
{
    half3 diff, spec;
    StylizedAdditionalLights_half(Smoothness, WorldPosition, WorldNormal, WorldView, Distortion, SpecularSize, SpecularHardness, half4(1,1,1,1), GeoNormal, diff, spec);
    Diffuse = (float)diff.x;
    Specular = (float3)spec;
}

void StylizedAdditionalLights_half(
    half Smoothness, half3 WorldPosition, half3 WorldNormal, half3 WorldView, half Distortion,
    half SpecularSize, half SpecularHardness, half3 GeoNormal,
    out float3 Diffuse, out float Specular)
{
    half3 diff, spec;
    StylizedAdditionalLights_half(Smoothness, WorldPosition, WorldNormal, WorldView, Distortion, SpecularSize, SpecularHardness, half4(1,1,1,1), GeoNormal, diff, spec);
    Diffuse = (float3)diff;
    Specular = (float)spec.x;
}

void StylizedAdditionalLights_half(
    half Smoothness, half3 WorldPosition, half3 WorldNormal, half3 WorldView, half Distortion,
    half SpecularSize, half SpecularHardness, half3 GeoNormal,
    out float Diffuse, out float Specular)
{
    half3 diff, spec;
    StylizedAdditionalLights_half(Smoothness, WorldPosition, WorldNormal, WorldView, Distortion, SpecularSize, SpecularHardness, half4(1,1,1,1), GeoNormal, diff, spec);
    Diffuse = (float)diff.x;
    Specular = (float)spec.x;
}

#endif // STYLIZED_ADDITIONAL_LIGHTS_INCLUDED

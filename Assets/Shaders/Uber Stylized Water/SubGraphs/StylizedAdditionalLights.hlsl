// StylizedAdditionalLights.hlsl
// Custom lighting calculations for additional lights (point and spot lights) in the stylized water shader.

#ifndef STYLIZED_ADDITIONAL_LIGHTS_INCLUDED
#define STYLIZED_ADDITIONAL_LIGHTS_INCLUDED

// ==========================================
// URP additional-light keywords
// ==========================================
#pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
#pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
#pragma multi_compile_fragment _ _SHADOWS_SOFT
#pragma multi_compile _ _FORWARD_PLUS

// ==========================================
// Spot-cone fit tuning
// ==========================================
// Fraction of the spot cone's outer half-angle that the specular highlight is
// allowed to occupy. 1.0 = highlight may fill the cone; lower values tuck the
// highlight further inside so narrow spotlights get a proportionally smaller blob.
#define STYLIZED_SPOT_FIT_FRACTION 0.5

// ==========================================
// Main Float Precision Implementation
// ==========================================

void StylizedAdditionalLights_float(
    float3 WorldPosition, 
    float3 WorldNormal, 
    float3 WorldView, 
    float Distortion,
    float SpecularSize, 
    float SpecularHardness, 
    half4 Shadowmask,
    float3 GeoNormal,
    out float3 Specular) 
{
    float3 specularColor = 0;

    float clampedSpecularSize = min(SpecularSize, 0.1f);
    if (clampedSpecularSize <= 0.00001f)
    {
        Specular = 0.0f;
        return;
    }

#if defined(SHADERGRAPH_PREVIEW)
    // Preview simulation: mock a light direction and color for the editor
    float3 Ngeo = normalize(GeoNormal);
    float3 Nperturbed = normalize(WorldNormal);
    float3 distortedNormal = normalize(Ngeo + (Nperturbed - Ngeo) * Distortion);
    WorldView = SafeNormalize(WorldView);

    float sizeSquared = max(1e-8f, clampedSpecularSize * clampedSpecularSize);
    float smoothness = max(0.001f, 1.0f - SpecularHardness);

    float3 lightDir = normalize(float3(-0.5f, 0.5f, -0.5f));
    float3 lightColor = float3(1.0f, 0.8f, 0.6f); // Warm point light

    float3 halfVec = SafeNormalize(lightDir + WorldView);
    float NdotH = saturate(dot(distortedNormal, halfVec));
    
    // Gaussian Stylized Specular
    float specDist = 1.0f - NdotH;
    float rawGradient = exp(-specDist / sizeSquared);
    float modifier = smoothstep(0.01f, 0.01f + smoothness, rawGradient);
    
    specularColor = lightColor * modifier;
#else
    float3 Ngeo = normalize(GeoNormal);
    float3 Nperturbed = normalize(WorldNormal);
    
    // Distort the normal based on Distortion and the difference between perturbed and geometry normal
    float3 distortedNormal = normalize(Ngeo + (Nperturbed - Ngeo) * Distortion);
    
    WorldView = SafeNormalize(WorldView);
    
    float smoothness = max(0.001f, 1.0f - SpecularHardness);
    uint pixelLightCount = GetAdditionalLightsCount();

    #if USE_FORWARD_PLUS
    for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++) {
        FORWARD_PLUS_SUBTRACTIVE_LIGHT_CHECK
        Light light = GetAdditionalLight(lightIndex, WorldPosition, Shadowmask);
        #ifdef _LIGHT_LAYERS
        if (IsMatchingLightLayer(light.layerMask, GetMeshRenderingLayer()))
        #endif
        {
            uint actualLightIndex = lightIndex;
            #if USE_STRUCTURED_BUFFER_FOR_LIGHT_DATA
                float4 lightPositionWS = _AdditionalLightsBuffer[actualLightIndex].position;
                half4 spotDirection = _AdditionalLightsBuffer[actualLightIndex].spotDirection;
                half4 distanceAndSpotAttenuation = _AdditionalLightsBuffer[actualLightIndex].attenuation;
            #else
                float4 lightPositionWS = _AdditionalLightsPosition[actualLightIndex];
                half4 spotDirection = _AdditionalLightsSpotDir[actualLightIndex];
                half4 distanceAndSpotAttenuation = _AdditionalLightsAttenuation[actualLightIndex];
            #endif

            float3 lightVector = lightPositionWS.xyz - WorldPosition * lightPositionWS.w;
            float distanceSqr = max(dot(lightVector, lightVector), 6.103515625e-5f);
            half distanceAttenuation = DistanceAttenuation(distanceSqr, distanceAndSpotAttenuation.xy);
            
            // Spotlight angle attenuation: calculate to scale SpecularSize instead of masking intensity
            half3 lightDirection = half3(lightVector * rsqrt(distanceSqr));
            half SdotL = dot(spotDirection.xyz, lightDirection);
            
            bool isSpot = (distanceAndSpotAttenuation.z > 0.0f);
            float effectiveSize = clampedSpecularSize;
            if (isSpot)
            {
                // Reconstruct the spot's outer half-angle from URP's packed attenuation.
                float cosOuter = -distanceAndSpotAttenuation.w / distanceAndSpotAttenuation.z;
                float theta_outer = acos(clamp(cosOuter, -1.0f, 1.0f));
                float theta = acos(clamp(SdotL, -1.0f, 1.0f));

                // 1) Cap the highlight radius so it can fit inside the cone: a narrow
                //    spotlight gets a proportionally smaller highlight (radius is roughly
                //    sqrt(2)*effectiveSize radians, so this keeps the blob within the cone).
                effectiveSize = min(effectiveSize, theta_outer * STYLIZED_SPOT_FIT_FRACTION);

                // 2) Smoothly taper the highlight to zero as the pixel approaches the cone
                //    edge, so it dissolves inside the cone instead of being hard-clipped.
                float remainingAngle = max(0.0f, theta_outer - theta);
                effectiveSize *= smoothstep(0.0f, max(effectiveSize, 1e-5f), remainingAngle);
            }

            if (effectiveSize > 0.00001f)
            {
                float sizeSquared = max(1e-8f, effectiveSize * effectiveSize);
                float3 specularAttenuatedColor = light.color * (distanceAttenuation * light.shadowAttenuation);

                // Specular (Custom Stylized with distortion)
                float3 halfVec = SafeNormalize(light.direction + WorldView);
                float NdotH = saturate(dot(distortedNormal, halfVec));
                
                float specDist = 1.0f - NdotH;
                float rawGradient = exp(-specDist / sizeSquared);
                float modifier = smoothstep(0.01f, 0.01f + smoothness, rawGradient);
                
                specularColor += specularAttenuatedColor * modifier;
            }
        }
    }
    #endif

    // Standard Light Loop (Non-Forward+ or Forward+ Clustered)
    InputData inputData = (InputData)0;
    float4 screenPos = ComputeScreenPos(TransformWorldToHClip(WorldPosition));
    inputData.normalizedScreenSpaceUV = screenPos.xy / screenPos.w;
    inputData.positionWS = WorldPosition;

    LIGHT_LOOP_BEGIN(pixelLightCount)
        Light light = GetAdditionalLight(lightIndex, WorldPosition, Shadowmask);
        #ifdef _LIGHT_LAYERS
        if (IsMatchingLightLayer(light.layerMask, GetMeshRenderingLayer()))
        #endif
        {
            #if USE_FORWARD_PLUS
                uint actualLightIndex = lightIndex;
            #else
                uint actualLightIndex = GetPerObjectLightIndex(lightIndex);
            #endif

            #if USE_STRUCTURED_BUFFER_FOR_LIGHT_DATA
                float4 lightPositionWS = _AdditionalLightsBuffer[actualLightIndex].position;
                half4 spotDirection = _AdditionalLightsBuffer[actualLightIndex].spotDirection;
                half4 distanceAndSpotAttenuation = _AdditionalLightsBuffer[actualLightIndex].attenuation;
            #else
                float4 lightPositionWS = _AdditionalLightsPosition[actualLightIndex];
                half4 spotDirection = _AdditionalLightsSpotDir[actualLightIndex];
                half4 distanceAndSpotAttenuation = _AdditionalLightsAttenuation[actualLightIndex];
            #endif

            float3 lightVector = lightPositionWS.xyz - WorldPosition * lightPositionWS.w;
            float distanceSqr = max(dot(lightVector, lightVector), 6.103515625e-5f);
            half distanceAttenuation = DistanceAttenuation(distanceSqr, distanceAndSpotAttenuation.xy);
            
            // Spotlight angle attenuation: calculate to scale SpecularSize instead of masking intensity
            half3 lightDirection = half3(lightVector * rsqrt(distanceSqr));
            half SdotL = dot(spotDirection.xyz, lightDirection);
            
            bool isSpot = (distanceAndSpotAttenuation.z > 0.0f);
            float effectiveSize = clampedSpecularSize;
            if (isSpot)
            {
                // Reconstruct the spot's outer half-angle from URP's packed attenuation.
                float cosOuter = -distanceAndSpotAttenuation.w / distanceAndSpotAttenuation.z;
                float theta_outer = acos(clamp(cosOuter, -1.0f, 1.0f));
                float theta = acos(clamp(SdotL, -1.0f, 1.0f));

                // 1) Cap the highlight radius so it can fit inside the cone: a narrow
                //    spotlight gets a proportionally smaller highlight (radius is roughly
                //    sqrt(2)*effectiveSize radians, so this keeps the blob within the cone).
                effectiveSize = min(effectiveSize, theta_outer * STYLIZED_SPOT_FIT_FRACTION);

                // 2) Smoothly taper the highlight to zero as the pixel approaches the cone
                //    edge, so it dissolves inside the cone instead of being hard-clipped.
                float remainingAngle = max(0.0f, theta_outer - theta);
                effectiveSize *= smoothstep(0.0f, max(effectiveSize, 1e-5f), remainingAngle);
            }

            if (effectiveSize > 0.00001f)
            {
                float sizeSquared = max(1e-8f, effectiveSize * effectiveSize);
                float3 specularAttenuatedColor = light.color * (distanceAttenuation * light.shadowAttenuation);

                // Specular
                float3 halfVec = SafeNormalize(light.direction + WorldView);
                float NdotH = saturate(dot(distortedNormal, halfVec));
                
                float specDist = 1.0f - NdotH;
                float rawGradient = exp(-specDist / sizeSquared);
                float modifier = smoothstep(0.01f, 0.01f + smoothness, rawGradient);
                
                specularColor += specularAttenuatedColor * modifier;
            }
        }
    LIGHT_LOOP_END
#endif

    Specular = specularColor;
}

// ==========================================
// Float Precision Output Overloads (Shadowmask version)
// ==========================================

void StylizedAdditionalLights_float(
    float3 WorldPosition, float3 WorldNormal, float3 WorldView, float Distortion,
    float SpecularSize, float SpecularHardness, half4 Shadowmask, float3 GeoNormal,
    out float Specular)
{
    float3 spec;
    StylizedAdditionalLights_float(WorldPosition, WorldNormal, WorldView, Distortion, SpecularSize, SpecularHardness, Shadowmask, GeoNormal, spec);
    Specular = spec.x;
}

void StylizedAdditionalLights_float(
    float3 WorldPosition, float3 WorldNormal, float3 WorldView, float Distortion,
    float SpecularSize, float SpecularHardness, half4 Shadowmask, float3 GeoNormal,
    out half3 Specular)
{
    float3 spec;
    StylizedAdditionalLights_float(WorldPosition, WorldNormal, WorldView, Distortion, SpecularSize, SpecularHardness, Shadowmask, GeoNormal, spec);
    Specular = (half3)spec;
}

void StylizedAdditionalLights_float(
    float3 WorldPosition, float3 WorldNormal, float3 WorldView, float Distortion,
    float SpecularSize, float SpecularHardness, half4 Shadowmask, float3 GeoNormal,
    out half Specular)
{
    float3 spec;
    StylizedAdditionalLights_float(WorldPosition, WorldNormal, WorldView, Distortion, SpecularSize, SpecularHardness, Shadowmask, GeoNormal, spec);
    Specular = (half)spec.x;
}

// ==========================================
// Float Precision Output Overloads (Backwards Compatible 8-param version)
// ==========================================

void StylizedAdditionalLights_float(
    float3 WorldPosition, float3 WorldNormal, float3 WorldView, float Distortion,
    float SpecularSize, float SpecularHardness, float3 GeoNormal,
    out float3 Specular)
{
    StylizedAdditionalLights_float(WorldPosition, WorldNormal, WorldView, Distortion, SpecularSize, SpecularHardness, half4(1,1,1,1), GeoNormal, Specular);
}

void StylizedAdditionalLights_float(
    float3 WorldPosition, float3 WorldNormal, float3 WorldView, float Distortion,
    float SpecularSize, float SpecularHardness, float3 GeoNormal,
    out float Specular)
{
    float3 spec;
    StylizedAdditionalLights_float(WorldPosition, WorldNormal, WorldView, Distortion, SpecularSize, SpecularHardness, half4(1,1,1,1), GeoNormal, spec);
    Specular = spec.x;
}

void StylizedAdditionalLights_float(
    float3 WorldPosition, float3 WorldNormal, float3 WorldView, float Distortion,
    float SpecularSize, float SpecularHardness, float3 GeoNormal,
    out half3 Specular)
{
    float3 spec;
    StylizedAdditionalLights_float(WorldPosition, WorldNormal, WorldView, Distortion, SpecularSize, SpecularHardness, half4(1,1,1,1), GeoNormal, spec);
    Specular = (half3)spec;
}

void StylizedAdditionalLights_float(
    float3 WorldPosition, float3 WorldNormal, float3 WorldView, float Distortion,
    float SpecularSize, float SpecularHardness, float3 GeoNormal,
    out half Specular)
{
    float3 spec;
    StylizedAdditionalLights_float(WorldPosition, WorldNormal, WorldView, Distortion, SpecularSize, SpecularHardness, half4(1,1,1,1), GeoNormal, spec);
    Specular = (half)spec.x;
}


// ==========================================
// Main Half Precision Implementation
// ==========================================

void StylizedAdditionalLights_half(
    half3 WorldPosition, 
    half3 WorldNormal, 
    half3 WorldView, 
    half Distortion,
    half SpecularSize, 
    half SpecularHardness, 
    half4 Shadowmask,
    half3 GeoNormal,
    out half3 Specular) 
{
    half3 specularColor = 0.0h;

    half clampedSpecularSize = min(SpecularSize, 0.1h);
    if (clampedSpecularSize <= 0.00001h)
    {
        Specular = 0.0h;
        return;
    }

#if defined(SHADERGRAPH_PREVIEW)
    // Preview simulation: mock a light direction and color for the editor
    half3 Ngeo = normalize(GeoNormal);
    half3 Nperturbed = normalize(WorldNormal);
    half3 distortedNormal = normalize(Ngeo + (Nperturbed - Ngeo) * Distortion);
    WorldView = SafeNormalize(WorldView);

    half sizeSquared = max(0.0001h, clampedSpecularSize * clampedSpecularSize);
    half smoothness = max(0.0001h, 1.0h - SpecularHardness);

    half3 lightDir = normalize(half3(-0.5h, 0.5h, -0.5h));
    half3 lightColor = half3(1.0h, 0.8h, 0.6h); // Warm point light

    half3 halfVec = SafeNormalize(lightDir + WorldView);
    half NdotH = saturate(dot(distortedNormal, halfVec));
    
    // Gaussian Stylized Specular
    half specDist = 1.0h - NdotH;
    half rawGradient = exp(-specDist / sizeSquared);
    half modifier = smoothstep(0.01h, 0.01h + smoothness, rawGradient);
    
    specularColor = lightColor * modifier;
#else
    half3 Ngeo = normalize(GeoNormal);
    half3 Nperturbed = normalize(WorldNormal);
    
    // Distort the normal based on Distortion and the difference between perturbed and geometry normal
    half3 distortedNormal = normalize(Ngeo + (Nperturbed - Ngeo) * Distortion);
    
    WorldView = SafeNormalize(WorldView);
    
    half smoothness = max(0.0001h, 1.0h - SpecularHardness);
    uint pixelLightCount = GetAdditionalLightsCount();

    #if USE_FORWARD_PLUS
    for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++) {
        FORWARD_PLUS_SUBTRACTIVE_LIGHT_CHECK
        Light light = GetAdditionalLight(lightIndex, WorldPosition, Shadowmask);
        #ifdef _LIGHT_LAYERS
        if (IsMatchingLightLayer(light.layerMask, GetMeshRenderingLayer()))
        #endif
        {
            uint actualLightIndex = lightIndex;
            #if USE_STRUCTURED_BUFFER_FOR_LIGHT_DATA
                float4 lightPositionWS = _AdditionalLightsBuffer[actualLightIndex].position;
                half4 spotDirection = _AdditionalLightsBuffer[actualLightIndex].spotDirection;
                half4 distanceAndSpotAttenuation = _AdditionalLightsBuffer[actualLightIndex].attenuation;
            #else
                float4 lightPositionWS = _AdditionalLightsPosition[actualLightIndex];
                half4 spotDirection = _AdditionalLightsSpotDir[actualLightIndex];
                half4 distanceAndSpotAttenuation = _AdditionalLightsAttenuation[actualLightIndex];
            #endif

            float3 lightVector = lightPositionWS.xyz - (float3)WorldPosition * lightPositionWS.w;
            float distanceSqr = max(dot(lightVector, lightVector), 6.103515625e-5f);
            half distanceAttenuation = DistanceAttenuation(distanceSqr, distanceAndSpotAttenuation.xy);
            
            // Spotlight angle attenuation: calculate to scale SpecularSize instead of masking intensity
            half3 lightDirection = half3(lightVector * rsqrt(distanceSqr));
            half SdotL = dot(spotDirection.xyz, lightDirection);
            
            bool isSpot = (distanceAndSpotAttenuation.z > 0.0h);
            half effectiveSize = clampedSpecularSize;
            if (isSpot)
            {
                // Reconstruct the spot's outer half-angle from URP's packed attenuation.
                half cosOuter = -distanceAndSpotAttenuation.w / distanceAndSpotAttenuation.z;
                half theta_outer = acos(clamp(cosOuter, -1.0h, 1.0h));
                half theta = acos(clamp(SdotL, -1.0h, 1.0h));

                // 1) Cap the highlight radius so it can fit inside the cone: a narrow
                //    spotlight gets a proportionally smaller highlight.
                effectiveSize = min(effectiveSize, theta_outer * STYLIZED_SPOT_FIT_FRACTION);

                // 2) Smoothly taper the highlight to zero as the pixel approaches the cone
                //    edge, so it dissolves inside the cone instead of being hard-clipped.
                half remainingAngle = max(0.0h, theta_outer - theta);
                effectiveSize *= smoothstep(0.0h, max(effectiveSize, 1e-4h), remainingAngle);
            }

            if (effectiveSize > 0.00001h)
            {
                half sizeSquared = max(0.0001h, effectiveSize * effectiveSize);
                half3 specularAttenuatedColor = half3(light.color) * half(distanceAttenuation * light.shadowAttenuation);

                // Specular (Custom Stylized with distortion)
                half3 halfVec = SafeNormalize(half3(light.direction) + WorldView);
                half NdotH = saturate(dot(distortedNormal, halfVec));
                
                half specDist = 1.0h - NdotH;
                half rawGradient = exp(-specDist / sizeSquared);
                half modifier = smoothstep(0.01h, 0.01h + smoothness, rawGradient);
                
                specularColor += specularAttenuatedColor * modifier;
            }
        }
    }
    #endif

    // Standard Light Loop (Non-Forward+ or Forward+ Clustered)
    InputData inputData = (InputData)0;
    float4 screenPos = ComputeScreenPos(TransformWorldToHClip(WorldPosition));
    inputData.normalizedScreenSpaceUV = screenPos.xy / screenPos.w;
    inputData.positionWS = WorldPosition;

    LIGHT_LOOP_BEGIN(pixelLightCount)
        Light light = GetAdditionalLight(lightIndex, WorldPosition, Shadowmask);
        #ifdef _LIGHT_LAYERS
        if (IsMatchingLightLayer(light.layerMask, GetMeshRenderingLayer()))
        #endif
        {
            #if USE_FORWARD_PLUS
                uint actualLightIndex = lightIndex;
            #else
                uint actualLightIndex = GetPerObjectLightIndex(lightIndex);
            #endif

            #if USE_STRUCTURED_BUFFER_FOR_LIGHT_DATA
                float4 lightPositionWS = _AdditionalLightsBuffer[actualLightIndex].position;
                half4 spotDirection = _AdditionalLightsBuffer[actualLightIndex].spotDirection;
                half4 distanceAndSpotAttenuation = _AdditionalLightsBuffer[actualLightIndex].attenuation;
            #else
                float4 lightPositionWS = _AdditionalLightsPosition[actualLightIndex];
                half4 spotDirection = _AdditionalLightsSpotDir[actualLightIndex];
                half4 distanceAndSpotAttenuation = _AdditionalLightsAttenuation[actualLightIndex];
            #endif

            float3 lightVector = lightPositionWS.xyz - (float3)WorldPosition * lightPositionWS.w;
            float distanceSqr = max(dot(lightVector, lightVector), 6.103515625e-5f);
            half distanceAttenuation = DistanceAttenuation(distanceSqr, distanceAndSpotAttenuation.xy);
            
            // Spotlight angle attenuation: calculate to scale SpecularSize instead of masking intensity
            half3 lightDirection = half3(lightVector * rsqrt(distanceSqr));
            half SdotL = dot(spotDirection.xyz, lightDirection);
            
            bool isSpot = (distanceAndSpotAttenuation.z > 0.0h);
            half effectiveSize = clampedSpecularSize;
            if (isSpot)
            {
                // Reconstruct the spot's outer half-angle from URP's packed attenuation.
                half cosOuter = -distanceAndSpotAttenuation.w / distanceAndSpotAttenuation.z;
                half theta_outer = acos(clamp(cosOuter, -1.0h, 1.0h));
                half theta = acos(clamp(SdotL, -1.0h, 1.0h));

                // 1) Cap the highlight radius so it can fit inside the cone: a narrow
                //    spotlight gets a proportionally smaller highlight.
                effectiveSize = min(effectiveSize, theta_outer * STYLIZED_SPOT_FIT_FRACTION);

                // 2) Smoothly taper the highlight to zero as the pixel approaches the cone
                //    edge, so it dissolves inside the cone instead of being hard-clipped.
                half remainingAngle = max(0.0h, theta_outer - theta);
                effectiveSize *= smoothstep(0.0h, max(effectiveSize, 1e-4h), remainingAngle);
            }

            if (effectiveSize > 0.00001h)
            {
                half sizeSquared = max(0.0001h, effectiveSize * effectiveSize);
                half3 specularAttenuatedColor = half3(light.color) * half(distanceAttenuation * light.shadowAttenuation);

                // Specular
                half3 halfVec = SafeNormalize(half3(light.direction) + WorldView);
                half NdotH = saturate(dot(distortedNormal, halfVec));
                
                half specDist = 1.0h - NdotH;
                half rawGradient = exp(-specDist / sizeSquared);
                half modifier = smoothstep(0.01h, 0.01h + smoothness, rawGradient);
                
                specularColor += specularAttenuatedColor * modifier;
            }
        }
    LIGHT_LOOP_END
#endif

    Specular = specularColor;
}

// ==========================================
// Half Precision Output Overloads (Shadowmask version)
// ==========================================

void StylizedAdditionalLights_half(
    half3 WorldPosition, half3 WorldNormal, half3 WorldView, half Distortion,
    half SpecularSize, half SpecularHardness, half4 Shadowmask, half3 GeoNormal,
    out half Specular)
{
    half3 spec;
    StylizedAdditionalLights_half(WorldPosition, WorldNormal, WorldView, Distortion, SpecularSize, SpecularHardness, Shadowmask, GeoNormal, spec);
    Specular = spec.x;
}

void StylizedAdditionalLights_half(
    half3 WorldPosition, half3 WorldNormal, half3 WorldView, half Distortion,
    half SpecularSize, half SpecularHardness, half4 Shadowmask, half3 GeoNormal,
    out float3 Specular)
{
    half3 spec;
    StylizedAdditionalLights_half(WorldPosition, WorldNormal, WorldView, Distortion, SpecularSize, SpecularHardness, Shadowmask, GeoNormal, spec);
    Specular = (float3)spec;
}

void StylizedAdditionalLights_half(
    half3 WorldPosition, half3 WorldNormal, half3 WorldView, half Distortion,
    half SpecularSize, half SpecularHardness, half4 Shadowmask, half3 GeoNormal,
    out float Specular)
{
    half3 spec;
    StylizedAdditionalLights_half(WorldPosition, WorldNormal, WorldView, Distortion, SpecularSize, SpecularHardness, Shadowmask, GeoNormal, spec);
    Specular = (float)spec.x;
}

// ==========================================
// Half Precision Output Overloads (Backwards Compatible 8-param version)
// ==========================================

void StylizedAdditionalLights_half(
    half3 WorldPosition, half3 WorldNormal, half3 WorldView, half Distortion,
    half SpecularSize, half SpecularHardness, half3 GeoNormal,
    out half3 Specular)
{
    StylizedAdditionalLights_half(WorldPosition, WorldNormal, WorldView, Distortion, SpecularSize, SpecularHardness, half4(1,1,1,1), GeoNormal, Specular);
}

void StylizedAdditionalLights_half(
    half3 WorldPosition, half3 WorldNormal, half3 WorldView, half Distortion,
    half SpecularSize, half SpecularHardness, half3 GeoNormal,
    out half Specular)
{
    half3 spec;
    StylizedAdditionalLights_half(WorldPosition, WorldNormal, WorldView, Distortion, SpecularSize, SpecularHardness, half4(1,1,1,1), GeoNormal, spec);
    Specular = spec.x;
}

void StylizedAdditionalLights_half(
    half3 WorldPosition, half3 WorldNormal, half3 WorldView, half Distortion,
    half SpecularSize, half SpecularHardness, half3 GeoNormal,
    out float3 Specular)
{
    half3 spec;
    StylizedAdditionalLights_half(WorldPosition, WorldNormal, WorldView, Distortion, SpecularSize, SpecularHardness, half4(1,1,1,1), GeoNormal, spec);
    Specular = (float3)spec;
}

void StylizedAdditionalLights_half(
    half3 WorldPosition, half3 WorldNormal, half3 WorldView, half Distortion,
    half SpecularSize, half SpecularHardness, half3 GeoNormal,
    out float Specular)
{
    half3 spec;
    StylizedAdditionalLights_half(WorldPosition, WorldNormal, WorldView, Distortion, SpecularSize, SpecularHardness, half4(1,1,1,1), GeoNormal, spec);
    Specular = (float)spec.x;
}

#endif // STYLIZED_ADDITIONAL_LIGHTS_INCLUDED

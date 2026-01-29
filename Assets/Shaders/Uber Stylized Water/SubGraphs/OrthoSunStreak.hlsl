// 1. Float Version
void FakeSunSpec_float(
    float3 WorldPos, 
    float3 WorldNormal, 
    float3 CamPos, 
    float3 OrthoViewDir, 
    float SunAzimuth, 
    float SunAltitude, 
    float SunSize, 
    float Anisotropy, 
    float AutoStretch, 
    float Hardness, 
    float Distortion, 
    out float Out)
{
    float pi = 3.14159265359;
    
    // 1. Sun Direction
    float azRad = SunAzimuth * (pi / 180.0);
    float altRad = SunAltitude * (pi / 180.0);
    
    float3 sunDir;
    sunDir.x = cos(altRad) * sin(azRad);
    sunDir.y = sin(altRad);
    sunDir.z = cos(altRad) * cos(azRad);
    sunDir = normalize(sunDir);
    
    // 2. Virtual View 
    // We do NOT distort this. We keep the "Eye" position stable.
    float3 virtualView = normalize(CamPos - WorldPos);
    
    // 3. Half Vector (H)
    float3 H = normalize(sunDir + virtualView);
    
    // 4. APPLY DISTORTION HERE (XZ Only!)
    // We only nudge the H vector using the wave slopes (Normal.xz).
    // We explicitly zero out the Y so the sun doesn't move up/down/sideways permanently.
    float3 wavePerturb = float3(WorldNormal.x, 0, WorldNormal.z) * Distortion * 5.0;
    
    // Add waves to H. This breaks up the reflection shape without moving the hotspot.
    float3 distortedH = H + wavePerturb;
    
    // 5. Anisotropic Basis (Sun Frame)
    // We flatten the sun direction to create a stable grid on the water
    float3 flatSun = normalize(float3(sunDir.x, 0, sunDir.z));
    float3 tangent = normalize(cross(float3(0,1,0), flatSun)); 
    float3 bitangent = flatSun; 
    
    // 6. Auto-Stretch Logic
    float NdotV = abs(dot(normalize(OrthoViewDir), float3(0,1,0)));
    float viewStretch = lerp(AutoStretch * 10.0, 1.0, NdotV);
    
    // 7. Project Distorted H onto Sun Frame
    float dWidth = dot(distortedH, tangent);
    float dLength = dot(distortedH, bitangent);
    
    float widthFactor = 1.0 + Anisotropy; 
    float lengthFactor = 1.0 / (viewStretch + 0.001);
    
    float specDist = (dWidth * dWidth * widthFactor) + (dLength * dLength * lengthFactor);
    
    // 8. Facing Check (Use undistorted H for cleaner masking)
    float facing = dot(float3(0,1,0), H); 
    
    // 9. Final Shaping
    float sunSpot = exp(-specDist / (SunSize * 0.01 + 0.0001));
    
    sunSpot = smoothstep(1.0 - Hardness, 1.0, sunSpot);
    Out = sunSpot * saturate(facing * 10.0);
}

// 2. Half Version (Mobile Optimized)
void FakeSunSpec_half(
    half3 WorldPos, 
    half3 WorldNormal, 
    half3 CamPos, 
    half3 OrthoViewDir, 
    half SunAzimuth, 
    half SunAltitude, 
    half SunSize, 
    half Anisotropy, 
    half AutoStretch, 
    half Hardness, 
    half Distortion, 
    out half Out)
{
    half pi = 3.14159265359h;
    
    half azRad = SunAzimuth * (pi / 180.0h);
    half altRad = SunAltitude * (pi / 180.0h);
    
    half3 sunDir;
    sunDir.x = cos(altRad) * sin(azRad);
    sunDir.y = sin(altRad);
    sunDir.z = cos(altRad) * cos(azRad);
    sunDir = normalize(sunDir);
    
    half3 virtualView = normalize(CamPos - WorldPos);
    
    half3 H = normalize(sunDir + virtualView);
    
    // XZ Only Distortion
    half3 wavePerturb = half3(WorldNormal.x, 0, WorldNormal.z) * Distortion * 5.0h;
    half3 distortedH = H + wavePerturb;
    
    half3 flatSun = normalize(half3(sunDir.x, 0, sunDir.z));
    half3 tangent = normalize(cross(half3(0,1,0), flatSun));
    half3 bitangent = flatSun;
    
    half NdotV = abs(dot(normalize(OrthoViewDir), half3(0,1,0)));
    half viewStretch = lerp(AutoStretch * 10.0h, 1.0h, NdotV);
    
    half dWidth = dot(distortedH, tangent);
    half dLength = dot(distortedH, bitangent);
    
    half widthFactor = 1.0h + Anisotropy; 
    half lengthFactor = 1.0h / (viewStretch + 0.001h);
    
    half specDist = (dWidth * dWidth * widthFactor) + (dLength * dLength * lengthFactor);
    
    half facing = dot(half3(0,1,0), H);
    
    half sunSpot = exp(-specDist / (SunSize * 0.01h + 0.0001h));
    
    sunSpot = smoothstep(1.0h - Hardness, 1.0h, sunSpot);
    Out = sunSpot * saturate(facing * 10.0h);
}
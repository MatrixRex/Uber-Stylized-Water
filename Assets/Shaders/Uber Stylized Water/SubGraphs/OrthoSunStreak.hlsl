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
    
    // 2. Virtual View & Distortion (Applied FIRST)
    float3 rawView = normalize(CamPos - WorldPos);
    
    // XZ Only Distortion
    float3 wavePerturb = float3(WorldNormal.x, 0, WorldNormal.z) * Distortion * 5.0;
    float3 H = normalize(sunDir + rawView + wavePerturb);
    
    // 3. Coordinate System
    float3 flatSun = normalize(float3(sunDir.x, 0, sunDir.z));
    float3 tangent = normalize(cross(float3(0,1,0), flatSun)); 
    float3 bitangent = flatSun; 
    
    // 4. Project
    float dWidth = dot(H, tangent);
    float dLength = dot(H, bitangent);
    
    // 5. Artistic Stretch
    float grazing = saturate(abs(dot(normalize(OrthoViewDir), float3(0,1,0))));
    float horizonFactor = 1.0 - grazing;
    
    float stretchMult = 1.0 + (AutoStretch * 100.0 * (horizonFactor * horizonFactor));
    
    // 6. Shape & Sizing (SENSITIVITY FIX)
    float widthFactor = 1.0 + Anisotropy;
    float finalLength = dLength / stretchMult;
    
    float specDist = (dWidth * dWidth * widthFactor) + (finalLength * finalLength);
    
    // NEW SIZING LOGIC:
    // We square the SunSize to treat it as a proper Radius.
    // This gives you smooth control from 0.0 to 1.0+.
    float sizeSquared = SunSize * SunSize;
    float rawGradient = exp(-specDist / (sizeSquared + 0.0001));
    
    // 7. Hardness (Step Logic)
    // Hardness 1.0 -> Sharp edge. Hardness 0.0 -> Soft glow.
    float smoothness = max(0.001, 1.0 - Hardness);
    Out = smoothstep(0.01, 0.01 + smoothness, rawGradient);
}

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
    
    half3 rawView = normalize(CamPos - WorldPos);
    
    // Distortion
    half3 wavePerturb = half3(WorldNormal.x, 0, WorldNormal.z) * Distortion * 5.0h;
    half3 H = normalize(sunDir + rawView + wavePerturb);
    
    half3 flatSun = normalize(half3(sunDir.x, 0, sunDir.z));
    half3 tangent = normalize(cross(half3(0,1,0), flatSun));
    half3 bitangent = flatSun; 
    
    half dWidth = dot(H, tangent);
    half dLength = dot(H, bitangent);
    
    half grazing = saturate(abs(dot(normalize(OrthoViewDir), half3(0,1,0))));
    half horizonFactor = 1.0h - grazing;
    
    half stretchMult = 1.0h + (AutoStretch * 100.0h * (horizonFactor * horizonFactor));
    
    half widthFactor = 1.0h + Anisotropy;
    half finalLength = dLength / stretchMult;
    
    half specDist = (dWidth * dWidth * widthFactor) + (finalLength * finalLength);
    
    // Sensitivity Fix (Half Precision)
    half sizeSquared = SunSize * SunSize;
    half rawGradient = exp(-specDist / (sizeSquared + 0.0001h));
    
    // Hardness Logic
    half smoothness = max(0.001h, 1.0h - Hardness);
    Out = smoothstep(0.01h, 0.01h + smoothness, rawGradient);
}
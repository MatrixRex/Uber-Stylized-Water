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
    float OrthoCamMode, 
    out float Out)
{
    float pi = 3.14159265359;
    
    // 1. Sun Direction
    float3 sunDir;
    if (SunAzimuth == 0.0 && SunAltitude == 0.0)
    {
        #if defined(SHADERGRAPH_PREVIEW)
            sunDir = normalize(float3(1.0, 1.0, -0.4));
        #else
            Light mainLight = GetMainLight();
            sunDir = mainLight.direction;
        #endif
    }
    else
    {
        float azRad = SunAzimuth * (pi / 180.0);
        float altRad = SunAltitude * (pi / 180.0);
        
        sunDir.x = cos(altRad) * sin(azRad);
        sunDir.y = sin(altRad);
        sunDir.z = cos(altRad) * cos(azRad);
        sunDir = normalize(sunDir);
    }
    
    // 2. Virtual View & Distortion (Applied FIRST)
    float3 rawView;
    if (OrthoCamMode > 0.5)
    {
        // Orthographic camera view direction calculation (with virtual camera projection)
        float viewDirY = OrthoViewDir.y;
        if (abs(viewDirY) < 0.001) viewDirY = 0.001 * (viewDirY >= 0 ? 1 : -1);
        
        float t = (CamPos.y - WorldPos.y) / viewDirY;
        float3 viewCenter = CamPos - OrthoViewDir * t;
        
        // Scale the virtual distance with orthographic camera size for consistency when zooming
        float orthoSize = unity_OrthoParams.y;
        float virtualDistance = max(50.0, orthoSize * 2.0);
        
        float3 virtualCamPos = viewCenter + OrthoViewDir * virtualDistance;
        rawView = normalize(virtualCamPos - WorldPos);
    }
    else
    {
        // Perspective camera
        rawView = normalize(CamPos - WorldPos);
    }
    
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
    
    // 6.5 Facing-Sun Mask
    float3 flatView = float3(OrthoViewDir.x, 0.0, OrthoViewDir.z);
    float lenXZ = length(flatView.xz);
    if (lenXZ > 0.001) flatView = flatView / lenXZ;
    else flatView = float3(0.0, 0.0, 0.0);
    
    float facing = dot(flatView, flatSun);
    float verticalFactor = smoothstep(0.8, 0.95, grazing);
    float facingMask = lerp(smoothstep(-0.2, 0.2, -facing), 1.0, verticalFactor);
    
    // 7. Hardness (Step Logic)
    // Hardness 1.0 -> Sharp edge. Hardness 0.0 -> Soft glow.
    float smoothness = max(0.001, 1.0 - Hardness);
    Out = smoothstep(0.01, 0.01 + smoothness, rawGradient) * facingMask;
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
    half OrthoCamMode, 
    out half Out)
{
    half pi = 3.14159265359h;
    
    // 1. Sun Direction
    half3 sunDir;
    if (SunAzimuth == 0.0h && SunAltitude == 0.0h)
    {
        #if defined(SHADERGRAPH_PREVIEW)
            sunDir = normalize(half3(1.0h, 1.0h, -0.4h));
        #else
            Light mainLight = GetMainLight();
            sunDir = (half3)mainLight.direction;
        #endif
    }
    else
    {
        half azRad = SunAzimuth * (pi / 180.0h);
        half altRad = SunAltitude * (pi / 180.0h);
        
        sunDir.x = cos(altRad) * sin(azRad);
        sunDir.y = sin(altRad);
        sunDir.z = cos(altRad) * cos(azRad);
        sunDir = normalize(sunDir);
    }
    
    // 2. Virtual View & Distortion (Applied FIRST)
    half3 rawView;
    if (OrthoCamMode > 0.5h)
    {
        // Orthographic camera view direction calculation (with virtual camera projection)
        half viewDirY = OrthoViewDir.y;
        if (abs(viewDirY) < 0.001h) viewDirY = 0.001h * (viewDirY >= 0.0h ? 1.0h : -1.0h);
        
        half t = (CamPos.y - WorldPos.y) / viewDirY;
        half3 viewCenter = CamPos - OrthoViewDir * t;
        
        // Scale the virtual distance with orthographic camera size for consistency when zooming
        half orthoSize = (half)unity_OrthoParams.y;
        half virtualDistance = max(50.0h, orthoSize * 2.0h);
        
        half3 virtualCamPos = viewCenter + OrthoViewDir * virtualDistance;
        rawView = normalize(virtualCamPos - WorldPos);
    }
    else
    {
        // Perspective camera
        rawView = normalize(CamPos - WorldPos);
    }
    
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
    
    // Facing-Sun Mask
    half3 flatView = half3(OrthoViewDir.x, 0.0h, OrthoViewDir.z);
    half lenXZ = length(flatView.xz);
    if (lenXZ > 0.001h) flatView = flatView / lenXZ;
    else flatView = half3(0.0h, 0.0h, 0.0h);
    
    half facing = dot(flatView, flatSun);
    half verticalFactor = smoothstep(0.8h, 0.95h, grazing);
    half facingMask = lerp(smoothstep(-0.2h, 0.2h, -facing), 1.0h, verticalFactor);
    
    // Hardness Logic
    half smoothness = max(0.001h, 1.0h - Hardness);
    Out = smoothstep(0.01h, 0.01h + smoothness, rawGradient) * facingMask;
}
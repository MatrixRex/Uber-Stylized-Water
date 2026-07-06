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
    // Early exit if size is close to zero
    if (SunSize <= 0.00001)
    {
        Out = 0.0;
        return;
    }

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
    
    // 2. Virtual View (Applied FIRST)
    float3 rawView;
    if (unity_OrthoParams.w > 0.5)
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
        // Perspective camera: use actual view vector
        rawView = normalize(CamPos - WorldPos);
    }
    
    // 3. Coordinate System (based on reflection vector R to match Phong and be perfectly circular)
    float3 R = float3(-sunDir.x, sunDir.y, -sunDir.z);
    float3 crossVec = cross(float3(0, 1, 0), R);
    if (length(crossVec) < 0.001) crossVec = float3(1, 0, 0);
    float3 tangent = normalize(crossVec);
    float3 bitangent = cross(R, tangent);
    
    // 4. Project view vector relative to R
    float dWidth = dot(rawView, tangent);
    float dLength = dot(rawView, bitangent);
    
    // 5. Artistic Stretch
    float grazing = saturate(abs(dot(normalize(OrthoViewDir), float3(0,1,0))));
    float horizonFactor = 1.0 - grazing;
    
    float stretchMult = 1.0 + (AutoStretch * 100.0 * (horizonFactor * horizonFactor));
    
    // 6. Shape, Sizing & Distortion (UNIFORM DISTORTION FIX)
    float3 wavePerturb = float3(WorldNormal.x, 0, WorldNormal.z) * Distortion * 5.0;
    float distortWidth = dot(wavePerturb, tangent);
    float distortLength = dot(wavePerturb, bitangent);
    
    float finalWidth = dWidth + distortWidth;
    float finalLength = (dLength / stretchMult) + distortLength;
    
    float widthFactor = 1.0 + Anisotropy;
    float specDist = (finalWidth * finalWidth * widthFactor) + (finalLength * finalLength);
    
    // NEW SIZING LOGIC:
    // We square the SunSize to treat it as a proper Radius.
    // This gives you smooth control from 0.0 to 1.0+.
    float sizeSquared = SunSize * SunSize;
    float rawGradient = exp(-specDist / max(1e-8, sizeSquared));
    
    // 6.5 Facing-Sun Mask
    float3 flatSun = float3(sunDir.x, 0, sunDir.z);
    float lenSunXZ = length(flatSun);
    if (lenSunXZ > 0.001) flatSun = flatSun / lenSunXZ;
    else flatSun = float3(0, 0, 1);
    
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
    out half Out)
{
    // Early exit if size is close to zero
    if (SunSize <= 0.00001h)
    {
        Out = 0.0h;
        return;
    }

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
    
    // 2. Virtual View (Applied FIRST)
    half3 rawView;
    if (unity_OrthoParams.w > 0.5h)
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
        // Perspective camera: use actual view vector
        rawView = normalize(CamPos - WorldPos);
    }
    
    // Coordinate System (based on reflection vector R to match Phong and be perfectly circular)
    half3 R = half3(-sunDir.x, sunDir.y, -sunDir.z);
    half3 crossVec = cross(half3(0.0h, 1.0h, 0.0h), R);
    if (length(crossVec) < 0.001h) crossVec = half3(1.0h, 0.0h, 0.0h);
    half3 tangent = normalize(crossVec);
    half3 bitangent = cross(R, tangent);
    
    // Project view vector relative to R
    half dWidth = dot(rawView, tangent);
    half dLength = dot(rawView, bitangent);
    
    half grazing = saturate(abs(dot(normalize(OrthoViewDir), half3(0,1,0))));
    half horizonFactor = 1.0h - grazing;
    
    half stretchMult = 1.0h + (AutoStretch * 100.0h * (horizonFactor * horizonFactor));
    
    // Uniform Distortion Fix
    half3 wavePerturb = half3(WorldNormal.x, 0, WorldNormal.z) * Distortion * 5.0h;
    half distortWidth = dot(wavePerturb, tangent);
    half distortLength = dot(wavePerturb, bitangent);
    
    half finalWidth = dWidth + distortWidth;
    half finalLength = (dLength / stretchMult) + distortLength;
    
    half widthFactor = 1.0h + Anisotropy;
    half specDist = (finalWidth * finalWidth * widthFactor) + (finalLength * finalLength);
    
    // Sensitivity Fix (Half Precision)
    half sizeSquared = SunSize * SunSize;
    half rawGradient = exp(-specDist / max(0.0001h, sizeSquared));
    
    // Facing-Sun Mask
    half3 flatSun = half3(sunDir.x, 0.0h, sunDir.z);
    half lenSunXZ = length(flatSun);
    if (lenSunXZ > 0.001h) flatSun = flatSun / lenSunXZ;
    else flatSun = half3(0.0h, 0.0h, 1.0h);
    
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

void FakeSunSpecCurved_float(
    float3 WorldPos, 
    float3 WorldNormal, 
    float3 GeoNormal, 
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
    // Early exit if size is close to zero
    if (SunSize <= 0.00001)
    {
        Out = 0.0;
        return;
    }

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
    }
    
    // 2. Virtual View (Applied FIRST)
    float3 rawView;
    if (unity_OrthoParams.w > 0.5)
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
        // Perspective camera: use actual view vector
        rawView = normalize(CamPos - WorldPos);
    }
    
    // 3. Coordinate System (based on reflection vector R off the geometry normal)
    float3 Ngeo = normalize(GeoNormal);
    float3 R = reflect(-sunDir, Ngeo);
    
    float3 crossVec = cross(Ngeo, R);
    if (dot(crossVec, crossVec) < 1e-6)
    {
        // Fallback for retroreflection
        crossVec = abs(R.z) < 0.999 ? float3(-R.y, R.x, 0.0) : float3(0.0, -R.z, R.y);
    }
    float3 tangent = normalize(crossVec);
    float3 bitangent = cross(R, tangent);
    
    // 4. Project view vector relative to R
    float dWidth = dot(rawView, tangent);
    float dLength = dot(rawView, bitangent);
    
    // 5. Artistic Stretch
    float grazing = saturate(abs(dot(normalize(OrthoViewDir), Ngeo)));
    float horizonFactor = 1.0 - grazing;
    
    float stretchMult = 1.0 + (AutoStretch * 100.0 * (horizonFactor * horizonFactor));
    
    // 6. Shape, Sizing & Distortion (UNIFORM DISTORTION FIX with curved surface support)
    // We use (WorldNormal - Ngeo) to extract the wave-map perturbation vector in world space,
    // avoiding the geometry's curvature from being interpreted as wave distortion.
    float3 wavePerturb = (WorldNormal - Ngeo) * Distortion * 5.0;
    float distortWidth = dot(wavePerturb, tangent);
    float distortLength = dot(wavePerturb, bitangent);
    
    float finalWidth = dWidth + distortWidth;
    float finalLength = (dLength / stretchMult) + distortLength;
    
    float widthFactor = 1.0 + Anisotropy;
    float specDist = (finalWidth * finalWidth * widthFactor) + (finalLength * finalLength);
    
    float sizeSquared = SunSize * SunSize;
    float rawGradient = exp(-specDist / max(1e-8, sizeSquared));
    
    // 6.5 Facing-Sun Mask
    float3 flatSun = float3(sunDir.x, 0, sunDir.z);
    float lenSunXZ = length(flatSun);
    if (lenSunXZ > 0.001) flatSun = flatSun / lenSunXZ;
    else flatSun = float3(0, 0, 1);
    
    float3 flatView = float3(OrthoViewDir.x, 0.0, OrthoViewDir.z);
    float lenXZ = length(flatView.xz);
    if (lenXZ > 0.001) flatView = flatView / lenXZ;
    else flatView = float3(0.0, 0.0, 0.0);
    
    float facing = dot(flatView, flatSun);
    float verticalFactor = smoothstep(0.8, 0.95, grazing);
    float facingMask = lerp(smoothstep(-0.2, 0.2, -facing), 1.0, verticalFactor);
    
    // 7. Hardness (Step Logic)
    float smoothness = max(0.001, 1.0 - Hardness);
    Out = smoothstep(0.01, 0.01 + smoothness, rawGradient) * facingMask;
}

void FakeSunSpecCurved_half(
    half3 WorldPos, 
    half3 WorldNormal, 
    half3 GeoNormal, 
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
    // Early exit if size is close to zero
    if (SunSize <= 0.00001h)
    {
        Out = 0.0h;
        return;
    }

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
    }
    
    // 2. Virtual View (Applied FIRST)
    // Cast to float3 internally to prevent 16-bit half precision overflow on large coordinates or t values
    half3 rawView;
    float3 worldPosF = (float3)WorldPos;
    float3 camPosF = (float3)CamPos;
    float3 viewDirF = (float3)OrthoViewDir;
    
    if (unity_OrthoParams.w > 0.5h)
    {
        float viewDirY = viewDirF.y;
        if (abs(viewDirY) < 0.001) viewDirY = 0.001 * (viewDirY >= 0 ? 1 : -1);
        
        float t = (camPosF.y - worldPosF.y) / viewDirY;
        float3 viewCenter = camPosF - viewDirF * t;
        
        float orthoSize = (float)unity_OrthoParams.y;
        float virtualDistance = max(50.0, orthoSize * 2.0);
        
        float3 virtualCamPos = viewCenter + viewDirF * virtualDistance;
        rawView = (half3)normalize(virtualCamPos - worldPosF);
    }
    else
    {
        rawView = (half3)normalize(camPosF - worldPosF);
    }
    
    // 3. Coordinate System (based on reflection vector R off the geometry normal)
    half3 Ngeo = normalize(GeoNormal);
    half3 R = reflect(-sunDir, Ngeo);
    
    half3 crossVec = cross(Ngeo, R);
    if (dot(crossVec, crossVec) < 1e-4h)
    {
        // Fallback for retroreflection
        crossVec = abs(R.z) < 0.999h ? half3(-R.y, R.x, 0.0h) : half3(0.0h, -R.z, R.y);
    }
    half3 tangent = normalize(crossVec);
    half3 bitangent = cross(R, tangent);
    
    // 4. Project view vector relative to R
    half dWidth = dot(rawView, tangent);
    half dLength = dot(rawView, bitangent);
    
    // 5. Artistic Stretch
    half grazing = saturate(abs(dot(normalize(OrthoViewDir), Ngeo)));
    half horizonFactor = 1.0h - grazing;
    
    half stretchMult = 1.0h + (AutoStretch * 100.0h * (horizonFactor * horizonFactor));
    
    // 6. Shape, Sizing & Distortion (UNIFORM DISTORTION FIX with curved surface support)
    // We use (WorldNormal - Ngeo) to extract the wave-map perturbation vector in world space,
    // avoiding the geometry's curvature from being interpreted as wave distortion.
    half3 wavePerturb = (WorldNormal - Ngeo) * Distortion * 5.0h;
    half distortWidth = dot(wavePerturb, tangent);
    half distortLength = dot(wavePerturb, bitangent);
    
    half finalWidth = dWidth + distortWidth;
    half finalLength = (dLength / stretchMult) + distortLength;
    
    half widthFactor = 1.0h + Anisotropy;
    half specDist = (finalWidth * finalWidth * widthFactor) + (finalLength * finalLength);
    
    // Sizing (CRITICAL FIX: Changed clamp value from 1e-8h to 0.0001h to avoid half-precision division by zero/NaN)
    half sizeSquared = SunSize * SunSize;
    half rawGradient = exp(-specDist / max(0.0001h, sizeSquared));
    
    // 6.5 Facing-Sun Mask
    half3 flatSun = half3(sunDir.x, 0.0h, sunDir.z);
    half lenSunXZ = length(flatSun);
    if (lenSunXZ > 0.001h) flatSun = flatSun / lenSunXZ;
    else flatSun = half3(0.0h, 0.0h, 1.0h);
    
    half3 flatView = half3(OrthoViewDir.x, 0.0h, OrthoViewDir.z);
    half lenXZ = length(flatView.xz);
    if (lenXZ > 0.001h) flatView = flatView / lenXZ;
    else flatView = half3(0.0h, 0.0h, 0.0h);
    
    half facing = dot(flatView, flatSun);
    half verticalFactor = smoothstep(0.8h, 0.95h, grazing);
    half facingMask = lerp(smoothstep(-0.2h, 0.2h, -facing), 1.0h, verticalFactor);
    
    // 7. Hardness (Step Logic)
    half smoothness = max(0.001h, 1.0h - Hardness);
    Out = smoothstep(0.01h, 0.01h + smoothness, rawGradient) * facingMask;
}
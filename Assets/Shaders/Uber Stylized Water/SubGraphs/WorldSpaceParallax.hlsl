// World-Space Y-Projection Parallax Mapping for Unity
// Single height value in world units
// Projects surface to conform to shape regardless of mesh rotation

void WorldSpaceParallax_float(
    float HeightWorldUnits,    // Height displacement in world units (e.g., 0.5 = 0.5 meters)
    float2 WorldUV,            // World-space UV coordinates (Position.xz)
    float3 WorldViewDir,       // World-space view direction
    out float2 ParallaxUV)
{
    // Normalize view direction
    float3 viewDir = normalize(WorldViewDir);
    
    // Calculate parallax offset in world-space XZ plane (Y-projection)
    // This projects the view ray to find where it intersects the displaced surface
    // offset = (viewDir.xz / viewDir.y) * height
    float2 parallaxOffset = (viewDir.xz / max(abs(viewDir.y), 0.001)) * HeightWorldUnits;
    
    // Apply offset to world UVs
    // Subtract because we're offsetting in the direction the viewer is looking
    ParallaxUV = WorldUV - parallaxOffset;
}

// Half precision variant (required by Shader Graph to prevent console errors)
void WorldSpaceParallax_half(
    half HeightWorldUnits,
    half2 WorldUV,
    half3 WorldViewDir,
    out half2 ParallaxUV)
{
    half3 viewDir = normalize(WorldViewDir);
    half2 parallaxOffset = (viewDir.xz / max(abs(viewDir.y), 0.001)) * HeightWorldUnits;
    ParallaxUV = WorldUV - parallaxOffset;
}

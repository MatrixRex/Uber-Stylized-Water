// Parallax Mapping for Unity Shader Graph
// Includes both World-Space Y-Projection and Tangent-Space Mesh UV Parallax

// ==========================================
// 1. World-Space Y-Projection Parallax Mapping
// Projects surface to conform to shape regardless of mesh rotation
// Suitable for flat water/terrain surfaces using World Position (Position.xz) as UVs
// ==========================================
void WorldSpaceParallax_float(
    float HeightWorldUnits,    // Height displacement in world units (e.g., 0.5 = 0.5 meters)
    float2 WorldUV,            // World-space UV coordinates (Position.xz)
    float3 WorldViewDir,       // World-space view direction (Surface to Camera)
    out float2 ParallaxUV)
{
    float3 viewDir = normalize(WorldViewDir);
    float2 parallaxOffset = (viewDir.xz / max(abs(viewDir.y), 0.001)) * HeightWorldUnits;
    ParallaxUV = WorldUV - parallaxOffset;
}

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

// ==========================================
// 2. Tangent-Space Parallax Mapping
// Suitable for any mesh orientation and standard mesh UV mapping
// Requires Tangent-Space View Direction and Mesh UVs
// ==========================================
void TangentSpaceParallax_float(
    float Height,             // Displacement height/depth
    float2 UV,                // Mesh UV coordinates
    float3 TangentViewDir,    // Tangent-space view direction (Surface to Camera)
    out float2 ParallaxUV)
{
    float3 viewDir = normalize(TangentViewDir);
    float2 parallaxOffset = (viewDir.xy / max(viewDir.z, 0.0001)) * Height;
    ParallaxUV = UV - parallaxOffset;
}

void TangentSpaceParallax_half(
    half Height,
    half2 UV,
    half3 TangentViewDir,
    out half2 ParallaxUV)
{
    half3 viewDir = normalize(TangentViewDir);
    half2 parallaxOffset = (viewDir.xy / max(viewDir.z, 0.0001)) * Height;
    ParallaxUV = UV - parallaxOffset;
}

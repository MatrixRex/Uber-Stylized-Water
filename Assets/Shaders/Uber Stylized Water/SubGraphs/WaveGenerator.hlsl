// UNITY_SHADER_NO_UPGRADE
// GERSTNER WAVES - WORLD SPACE VERSION

#ifndef GERSTNER_WAVES_WORLD_INCLUDED
#define GERSTNER_WAVES_WORLD_INCLUDED

static const float pi = 3.14159265359;

void GerstnerWavesWorld_float(
    float3 WorldPosition,
    float Direction,
    float Steepness,
    float Wavelength,
    float Speed,
    float Time,
    float Iterations,
    out float3 Offset,
    out float3 WorldNormal
)
{
    float3 finalOffset = float3(0, 0, 0);
    float3 derivatives = float3(0, 0, 0);

    // Fixed wavelength in world units (meters) independent of object scale
    float currentWavelength = max(Wavelength, 0.001);
    float currentSteepness = max(Steepness, 0.0);
    float currentSpeed = Speed;
    float baseAngle = Direction * (pi / 180.0);
    float goldenAngle = 2.39996323;

    for (int i = 0; i < (int)Iterations; i++)
    {
        float angle = baseAngle + i * goldenAngle;
        float2 dir = float2(cos(angle), sin(angle));

        float k = 2.0 * pi / currentWavelength;
        float f = dot(dir, WorldPosition.xz) * k + Time * currentSpeed * k;

        float valCos = cos(f);
        float valSin = sin(f);
        float amplitude = currentSteepness / k;

        finalOffset.x += dir.x * (amplitude * valCos);
        finalOffset.y += amplitude * valSin;
        finalOffset.z += dir.y * (amplitude * valCos);

        float wa = k * amplitude;
        derivatives.x += dir.x * wa * valSin;
        derivatives.z += dir.y * wa * valSin;
        derivatives.y += wa * valCos;

        // Next octave
        currentWavelength *= 0.618;
        currentSteepness *= 0.5;
        currentSpeed *= 1.2;
    }

    // Transform world-space offset to object space for vertex displacement
    Offset = mul((float3x3)unity_WorldToObject, finalOffset);
    
    // Output true World Space Normal
    float3 worldNrm = normalize(float3(
        -derivatives.x,
        1.0 - derivatives.y,
        -derivatives.z
    ));
    WorldNormal = worldNrm;
}

// GERSTNER WAVES - UV SPACE VERSION
void GerstnerWavesUV_float(
    float2 UV,
    float3 NormalWS,
    float3 TangentWS,
    float3 BitangentWS,
    float Direction,
    float Steepness,
    float Wavelength,
    float Speed,
    float Time,
    float Iterations,
    out float3 Offset,
    out float3 WorldNormal
)
{
    float3 finalOffset = float3(0, 0, 0);
    float3 derivatives = float3(0, 0, 0);

    // Fixed wavelength in world units (meters) independent of object scale
    float currentWavelength = max(Wavelength, 0.001);
    float currentSteepness = max(Steepness, 0.0);
    float currentSpeed = Speed;
    float baseAngle = Direction * (pi / 180.0);
    float goldenAngle = 2.39996323;

    // Calculate physical world meter scaling per UV unit
    float scaleU = max(length(TangentWS), 0.0001);
    float scaleV = max(length(BitangentWS), 0.0001);

    // Normalize vectors for orientation
    float3 normWS = normalize(NormalWS);
    float3 tangWS = TangentWS / scaleU;
    float3 bitangWS = BitangentWS / scaleV;

    // Convert UV to physical world meters
    float2 worldUV = UV * float2(scaleU, scaleV);

    for (int i = 0; i < (int)Iterations; i++)
    {
        float angle = baseAngle + i * goldenAngle;
        float2 dir = float2(cos(angle), sin(angle));

        float k = 2.0 * pi / currentWavelength;
        float phase = dot(dir, worldUV) * k + Time * currentSpeed * k;

        float valCos = cos(phase);
        float valSin = sin(phase);
        float amplitude = currentSteepness / k;

        // Height displacement along surface normal
        finalOffset += normWS * (amplitude * valSin);

        // Horizontal displacement along Tangent (U) and Bitangent (V)
        finalOffset += tangWS   * (dir.x * amplitude * valCos);
        finalOffset += bitangWS * (dir.y * amplitude * valCos);

        float wa = k * amplitude;
        derivatives.x += dir.x * wa * valSin;
        derivatives.z += dir.y * wa * valSin;
        derivatives.y += wa * valCos;

        // Next octave
        currentWavelength *= 0.618;
        currentSteepness  *= 0.5;
        currentSpeed      *= 1.2;
    }

    // Transform world-space offset to object space for vertex displacement
    Offset = mul((float3x3)unity_WorldToObject, finalOffset);

    // Output true World Space Normal
    float3 worldNrm = normalize(normWS * (1.0 - derivatives.y) - tangWS * derivatives.x - bitangWS * derivatives.z);
    WorldNormal = worldNrm;
}

// HALF PRECISION OVERLOADS FOR SHADER GRAPH
void GerstnerWavesWorld_half(
    half3 WorldPosition,
    half Direction,
    half Steepness,
    half Wavelength,
    half Speed,
    half Time,
    half Iterations,
    out half3 Offset,
    out half3 WorldNormal
)
{
    float3 off; float3 nrm;
    GerstnerWavesWorld_float(
        (float3)WorldPosition, (float)Direction, (float)Steepness, (float)Wavelength,
        (float)Speed, (float)Time, (float)Iterations, off, nrm);
    Offset = (half3)off;
    WorldNormal = (half3)nrm;
}

void GerstnerWavesUV_half(
    half2 UV,
    half3 NormalWS,
    half3 TangentWS,
    half3 BitangentWS,
    half Direction,
    half Steepness,
    half Wavelength,
    half Speed,
    half Time,
    half Iterations,
    out half3 Offset,
    out half3 WorldNormal
)
{
    float3 off; float3 nrm;
    GerstnerWavesUV_float(
        (float2)UV, (float3)NormalWS, (float3)TangentWS, (float3)BitangentWS,
        (float)Direction, (float)Steepness, (float)Wavelength, (float)Speed,
        (float)Time, (float)Iterations, off, nrm);
    Offset = (half3)off;
    WorldNormal = (half3)nrm;
}

#endif

// Water Waves HLSL Function for Shader Graph Custom Function Node
// This function generates realistic water waves using multiple sine waves

void GenerateWaves_float(float2 UV, float Time, float WaveStrength, float WaveFrequency, float WaveSpeed, float2 WaveDirection, out float3 WaveOffset, out float3 Normal)
{
    // Normalize wave direction
    WaveDirection = normalize(WaveDirection);
    
    // Initialize output values
    WaveOffset = float3(0, 0, 0);
    float3 tangent = float3(1, 0, 0);
    float3 binormal = float3(0, 0, 1);
    
    // Wave parameters for multiple waves
    // You can adjust these for different wave characteristics
    float4 waveAmplitudes = float4(1.0, 0.5, 0.25, 0.125) * WaveStrength;
    float4 waveFrequencies = float4(1.0, 2.0, 4.0, 8.0) * WaveFrequency;
    float4 waveSpeeds = float4(1.0, 1.2, 1.5, 2.0) * WaveSpeed;
    
    // Wave directions (in radians from the main direction)
    float4 waveDirectionOffsets = float4(0.0, 0.3, -0.2, 0.5);
    
    // Generate 4 waves
    for (int i = 0; i < 4; i++)
    {
        // Calculate wave direction for this wave
        float2 currentWaveDir;
        if (i == 0) {
            currentWaveDir = WaveDirection;
        } else if (i == 1) {
            float angle = waveDirectionOffsets.y;
            currentWaveDir = float2(WaveDirection.x * cos(angle) - WaveDirection.y * sin(angle),
                                   WaveDirection.x * sin(angle) + WaveDirection.y * cos(angle));
        } else if (i == 2) {
            float angle = waveDirectionOffsets.z;
            currentWaveDir = float2(WaveDirection.x * cos(angle) - WaveDirection.y * sin(angle),
                                   WaveDirection.x * sin(angle) + WaveDirection.y * cos(angle));
        } else {
            float angle = waveDirectionOffsets.w;
            currentWaveDir = float2(WaveDirection.x * cos(angle) - WaveDirection.y * sin(angle),
                                   WaveDirection.x * sin(angle) + WaveDirection.y * cos(angle));
        }
        
        // Wave calculation
        float amplitude = waveAmplitudes[i];
        float frequency = waveFrequencies[i];
        float speed = waveSpeeds[i];
        
        float phase = dot(currentWaveDir, UV) * frequency + Time * speed;
        float wave = sin(phase);
        float waveDerivative = cos(phase) * frequency;
        
        // Add to height
        WaveOffset.y += amplitude * wave;
        
        // Calculate partial derivatives for normal calculation
        float dPhaseDx = currentWaveDir.x * waveDerivative;
        float dPhaseDz = currentWaveDir.y * waveDerivative;
        
        tangent.y += amplitude * dPhaseDx;
        binormal.y += amplitude * dPhaseDz;
    }
    
    // Calculate normal from tangent and binormal
    Normal = normalize(cross(binormal, tangent));
}

// Simplified version with fewer waves for better performance
void GenerateWavesSimple_float(float2 UV, float Time, float WaveStrength, float WaveFrequency, float WaveSpeed, float2 WaveDirection, out float HeightOffset, out float3 Normal)
{
    WaveDirection = normalize(WaveDirection);
    
    HeightOffset = 0;
    float3 tangent = float3(1, 0, 0);
    float3 binormal = float3(0, 0, 1);
    
    // Two waves for simplicity
    float2 wave1Dir = WaveDirection;
    float2 wave2Dir = float2(WaveDirection.x * 0.707 - WaveDirection.y * 0.707, 
                            WaveDirection.x * 0.707 + WaveDirection.y * 0.707);
    
    // Wave 1
    float phase1 = dot(wave1Dir, UV) * WaveFrequency + Time * WaveSpeed;
    float wave1 = sin(phase1) * WaveStrength;
    float derivative1 = cos(phase1) * WaveFrequency * WaveStrength;
    
    HeightOffset += wave1;
    tangent.y += wave1Dir.x * derivative1;
    binormal.y += wave1Dir.y * derivative1;
    
    // Wave 2
    float phase2 = dot(wave2Dir, UV) * WaveFrequency * 2.0 + Time * WaveSpeed * 1.5;
    float wave2 = sin(phase2) * WaveStrength * 0.5;
    float derivative2 = cos(phase2) * WaveFrequency * 2.0 * WaveStrength * 0.5;
    
    HeightOffset += wave2;
    tangent.y += wave2Dir.x * derivative2;
    binormal.y += wave2Dir.y * derivative2;
    
    Normal = normalize(cross(binormal, tangent));
}

// Gerstner waves (more realistic wave motion)
void GenerateGerstnerWaves_float(float2 UV, float Time, float WaveStrength, float WaveFrequency, float WaveSpeed, float2 WaveDirection, float Steepness, out float3 WaveOffset, out float3 Normal)
{
    WaveDirection = normalize(WaveDirection);
    
    WaveOffset = float3(0, 0, 0);
    float3 tangent = float3(1, 0, 0);
    float3 binormal = float3(0, 0, 1);
    
    // Clamp steepness to prevent loops
    Steepness = clamp(Steepness, 0, 0.9);
    
    // Two Gerstner waves
    for (int i = 0; i < 2; i++)
    {
        float2 dir = (i == 0) ? WaveDirection : 
                     float2(WaveDirection.x * 0.8 - WaveDirection.y * 0.6,
                           WaveDirection.x * 0.6 + WaveDirection.y * 0.8);
        
        float amplitude = WaveStrength * ((i == 0) ? 1.0 : 0.6);
        float frequency = WaveFrequency * ((i == 0) ? 1.0 : 1.8);
        float speed = WaveSpeed * ((i == 0) ? 1.0 : 1.3);
        
        float phase = dot(dir, UV) * frequency + Time * speed;
        float sinPhase = sin(phase);
        float cosPhase = cos(phase);
        
        float Q = Steepness / (frequency * amplitude);
        
        // Horizontal displacement
        WaveOffset.x += Q * amplitude * dir.x * cosPhase;
        WaveOffset.z += Q * amplitude * dir.y * cosPhase;
        
        // Vertical displacement
        WaveOffset.y += amplitude * sinPhase;
        
        // Calculate derivatives for normals
        float dPhaseDx = dir.x * frequency;
        float dPhaseDz = dir.y * frequency;
        
        tangent.x += -Q * dPhaseDx * dir.x * sinPhase;
        tangent.y += dPhaseDx * cosPhase;
        tangent.z += -Q * dPhaseDx * dir.y * sinPhase;
        
        binormal.x += -Q * dPhaseDz * dir.x * sinPhase;
        binormal.y += dPhaseDz * cosPhase;
        binormal.z += -Q * dPhaseDz * dir.y * sinPhase;
    }
    
    Normal = normalize(cross(binormal, tangent));
}
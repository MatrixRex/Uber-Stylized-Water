// UNITY_SHADER_NO_UPGRADE

#ifndef GERSTNER_WAVES_INCLUDED
#define GERSTNER_WAVES_INCLUDED

// User Preference: constants in lowercase
static const float pi = 3.14159265359;

void GerstnerWaves_float(
    float3 Position,
    float2 Direction,
    float Steepness,
    float Wavelength,
    float Speed,
    float Time,
    float Iterations,
    out float3 Offset,
    out float3 Normal
)
{
    // Initialize outputs
    float3 finalOffset = float3(0, 0, 0);
    // For normal calculation, we sum the gradients (derivatives)
    // x and z accumulate horizontal drag, y accumulates vertical compression
    float3 accumulatedDerivatives = float3(0, 0, 0);

    // Loop Variables
    float currentWavelength = Wavelength;
    float currentSteepness = Steepness;
    float currentSpeed = Speed;
    float2 currentDirection = normalize(Direction);
    
    // Rotation matrix values for the "next wave" (approx 57 degrees rotation)
    // This prevents waves from lining up perfectly
    float rotateCos = cos(1.0);
    float rotateSin = sin(1.0);

    // Iteration Loop
    // We cast Iterations to int to use it in the loop
    for(int i = 0; i < (int)Iterations; i++) 
    {
        // 1. Calculate Wave Parameters
        // k = frequency (2pi / L)
        float k = 2.0 * pi / currentWavelength;
        // c = phase speed
        float c = sqrt(9.8 / k) * currentSpeed; // Physical speed approximation based on gravity
        
        // The driver function: (D . xz * k + t * c * k)
        // We use Time * c * k which simplifies to Time * SpeedFactor
        float f = dot(currentDirection, Position.xz) * k + Time * c;

        // Precompute Trig
        float valCos = cos(f);
        float valSin = sin(f);

        // Amplitude Calculation
        // To prevent looping (self-intersection), Steepness should decrease as waves get added.
        // A = Steepness / k
        float amplitude = currentSteepness / k;

        // 2. Accumulate Displacement (The Shape)
        finalOffset.x += currentDirection.x * (amplitude * valCos);
        finalOffset.y += amplitude * valSin;
        finalOffset.z += currentDirection.y * (amplitude * valCos);

        // 3. Accumulate Derivatives (The Normal)
        // This is based on summing the partial derivatives of the Gerstner function
        float wa = k * amplitude; // This effectively equals currentSteepness
        
        accumulatedDerivatives.x += currentDirection.x * wa * valSin;
        accumulatedDerivatives.z += currentDirection.y * wa * valSin;
        accumulatedDerivatives.y += wa * valCos;

        // 4. Prepare Variables for Next Loop (FBM)
        currentWavelength *= 0.5;   // Half the size
        currentSteepness *= 0.5;    // Half the steepness
        currentSpeed *= 1.2;        // Slight speed increase for small ripples
        
        // Rotate direction
        float2 newDir;
        newDir.x = currentDirection.x * rotateCos - currentDirection.y * rotateSin;
        newDir.y = currentDirection.x * rotateSin + currentDirection.y * rotateCos;
        currentDirection = normalize(newDir);
    }

    // Final Output Assignment
    Offset = finalOffset;

    // Reconstruct Normal from Derivatives
    // The actual normal is (-dx, 1 - dy, -dz)
    Normal = normalize(float3(
        -accumulatedDerivatives.x,
        1.0 - accumulatedDerivatives.y,
        -accumulatedDerivatives.z
    ));
}

#endif
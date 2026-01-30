#ifndef OKLAB_LERP_INCLUDED
#define OKLAB_LERP_INCLUDED

// ---------------------------------------------------------------------------
// Helper Functions
// ---------------------------------------------------------------------------

float3 oklab_cube(float3 v) {
    return v * v * v;
}

float3 oklab_cbrt(float3 v) {
    return sign(v) * pow(abs(v), 1.0 / 3.0);
}

// Converts Linear RGB to Oklab
float3 linear_rgb_to_oklab(float3 c) {
    float l = 0.4122214708 * c.r + 0.5363325363 * c.g + 0.0514459929 * c.b;
    float m = 0.2119034982 * c.r + 0.6806995451 * c.g + 0.1073969566 * c.b;
    float s = 0.0883024619 * c.r + 0.2817188376 * c.g + 0.6299787005 * c.b;

    float l_ = oklab_cbrt(l).x;
    float m_ = oklab_cbrt(m).x;
    float s_ = oklab_cbrt(s).x;

    return float3(
        0.2104542553 * l_ + 0.7936177850 * m_ - 0.0040720468 * s_,
        1.9779984951 * l_ - 2.4285922050 * m_ + 0.4505937099 * s_,
        0.0259040371 * l_ + 0.7827717662 * m_ - 0.8086757660 * s_
    );
}

// Converts Oklab to Linear RGB
float3 oklab_to_linear_rgb(float3 c) {
    float l_ = c.x + 0.3963377774 * c.y + 0.2158037573 * c.z;
    float m_ = c.x - 0.1055613458 * c.y - 0.0638541728 * c.z;
    float s_ = c.x - 0.0894841775 * c.y - 1.2914855480 * c.z;

    float3 lms = oklab_cube(float3(l_, m_, s_));

    return float3(
        4.0767416621 * lms.x - 3.3077115913 * lms.y + 0.2309699292 * lms.z,
        -1.2684380046 * lms.x + 2.6097574011 * lms.y - 0.3413193965 * lms.z,
        -0.0041960863 * lms.x - 0.7034186147 * lms.y + 1.7076147010 * lms.z
    );
}

// ---------------------------------------------------------------------------
// Main Shader Graph Functions
// ---------------------------------------------------------------------------

// Single Precision (Float)
void OklabLerp_float(float3 ColorA, float3 ColorB, float T, out float3 Out) {
    float3 ok_a = linear_rgb_to_oklab(ColorA);
    float3 ok_b = linear_rgb_to_oklab(ColorB);
    
    // Lerp in Oklab space
    float3 mix = lerp(ok_a, ok_b, T);
    
    // Convert back
    Out = oklab_to_linear_rgb(mix);
}

// Half Precision (Half)
// Note: We force internal calculation to float to preserve color accuracy 
// before casting back to half for the output.
void OklabLerp_half(half3 ColorA, half3 ColorB, half T, out half3 Out) {
    float3 ok_a = linear_rgb_to_oklab((float3)ColorA);
    float3 ok_b = linear_rgb_to_oklab((float3)ColorB);
    
    float3 mix = lerp(ok_a, ok_b, (float)T);
    
    Out = (half3)oklab_to_linear_rgb(mix);
}

#endif
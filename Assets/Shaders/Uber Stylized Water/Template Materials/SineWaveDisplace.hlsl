#ifndef SINE_WAVE_DISPLACE_INCLUDED
#define SINE_WAVE_DISPLACE_INCLUDED

// Core implementation (float precision)
inline void SineWaveDisplace_impl_float(
    float3 positionWS,
    float2 direction,
    float  amplitude,
    float  wavelength,
    float  phaseSpeed,
    float  timeSeconds,
    out float3 displacedPositionWS,
    out float3 normalWSOut)
{
    float wl = max(wavelength, 1e-4);
    float2 dir = direction;
    float len = max(length(dir), 1e-6);
    dir /= len;

    const float TWO_PI = 6.28318530718;
    float k  = TWO_PI / wl;

    float2 xz = positionWS.xz;
    float phase = k * (dot(dir, xz) - phaseSpeed * timeSeconds);

    float s = sin(phase);
    float c = cos(phase);

    float  h    = amplitude * s;
    float  dhdx = amplitude * c * k * dir.x;
    float  dhdz = amplitude * c * k * dir.y;

    displacedPositionWS = float3(positionWS.x, positionWS.y + h, positionWS.z);
    normalWSOut = normalize(float3(-dhdx, 1.0, -dhdz));
}

// Shader Graph will call either of these, based on precision:

// Float entry point
inline void SineWaveDisplace_float(
    float3 positionWS,
    float2 direction,
    float  amplitude,
    float  wavelength,
    float  phaseSpeed,
    float  timeSeconds,
    out float3 displacedPositionWS,
    out float3 normalWSOut)
{
    SineWaveDisplace_impl_float(
        positionWS, direction, amplitude, wavelength, phaseSpeed, timeSeconds,
        displacedPositionWS, normalWSOut);
}

// Half entry point
inline void SineWaveDisplace_half(
    half3 positionWS,
    half2 direction,
    half  amplitude,
    half  wavelength,
    half  phaseSpeed,
    half  timeSeconds,
    out half3 displacedPositionWS,
    out half3 normalWSOut)
{
    float3 dp; float3 nw;
    SineWaveDisplace_impl_float(
        (float3)positionWS, (float2)direction, (float)amplitude, (float)wavelength,
        (float)phaseSpeed, (float)timeSeconds, dp, nw);
    displacedPositionWS = (half3)dp;
    normalWSOut = (half3)normalize(nw);
}

#endif // SINE_WAVE_DISPLACE_INCLUDED

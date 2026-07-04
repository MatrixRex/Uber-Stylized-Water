// Drop this on a quad (or plane) laid roughly over your water region.
// It samples the SDF by *world position*, so the quad doesn't need to be
// aligned to the bake bounds — anywhere it overlaps the region shows data.
// Needs a WaterSDFBinder active in the scene (it sets the globals).
//
// Modes:
//   0 Gradient  — blue->white by signed distance, land tinted brown
//   1 Direction — shore direction encoded as color (RG = dir * 0.5 + 0.5)
//   2 Depth     — water depth grayscale
//
// The pass has no LightMode tag so it renders as SRPDefaultUnlit in URP
// and as a normal unlit pass in Built-in.

Shader "WaterSDF/Debug"
{
    Properties
    {
        [KeywordEnum(Gradient, Direction, Depth)] _Mode ("Mode", Float) = 0
        _Range      ("Distance Range (m)", Float) = 25
        _DepthRange ("Depth Range (m)",    Float) = 10
        _Bands      ("Contour Bands",  Range(0,1)) = 0.35
        _BandStep   ("Band Spacing (m)",   Float) = 2
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry+10" }
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _MODE_GRADIENT _MODE_DIRECTION _MODE_DEPTH
            #include "UnityCG.cginc"

            sampler2D _WaterSDF;
            float4    _WaterSDF_Params; // xy = bounds min XZ, z = 1/size
            float     _Range, _DepthRange, _Bands, _BandStep;

            struct v2f
            {
                float4 pos     : SV_POSITION;
                float3 worldPos: TEXCOORD0;
            };

            v2f vert (appdata_base v)
            {
                v2f o;
                o.pos      = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 uv = (i.worldPos.xz - _WaterSDF_Params.xy) * _WaterSDF_Params.z;
                float4 s  = tex2D(_WaterSDF, uv);

                float  d     = s.r;
                float  depth = s.g;
                float2 dir   = s.ba;

                float3 col;

            #if _MODE_DIRECTION
                col = float3(dir * 0.5 + 0.5, 0);
            #elif _MODE_DEPTH
                col = saturate(depth / max(_DepthRange, 0.001)).xxx;
            #else // gradient
                float t = saturate(d / max(_Range, 0.001));
                float3 water = lerp(float3(0.05, 0.25, 0.55),   // shallow
                                    float3(0.85, 0.95, 1.0), t); // far from shore
                float3 land  = lerp(float3(0.45, 0.30, 0.15),
                                    float3(0.15, 0.10, 0.05),
                                    saturate(-d / max(_Range, 0.001)));
                col = d >= 0 ? water : land;

                // near-free contour bands (frac of distance)
                float band = abs(frac(d / max(_BandStep, 0.001)) - 0.5) * 2.0;
                col *= 1.0 - _Bands * smoothstep(0.85, 1.0, band);
            #endif

                // shoreline zero-crossing in red, ~1 texel wide
                float texelMeters = 1.0 / (_WaterSDF_Params.z * 1024.0); // approx
                col = lerp(float3(1, 0.1, 0.1), col,
                           smoothstep(0.0, texelMeters * 2.0, abs(d)));

                return fixed4(col, 1);
            }
            ENDCG
        }
    }
    Fallback Off
}

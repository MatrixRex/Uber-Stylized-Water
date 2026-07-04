// WaterSDFBinder.cs — put this anywhere in the scene (or on the water object).
// Pushes the baked SDF and its world-space mapping to the shaders as globals,
// so any water material can sample it without per-material setup.

using UnityEngine;

[ExecuteAlways]
public class WaterSDFBinder : MonoBehaviour
{
    public Texture2D sdfTexture;

    [Tooltip("World-space XZ of the bake region's min corner (logged by the baker).")]
    public Vector2 boundsMinXZ;

    [Tooltip("World-space size of the (square) bake region.")]
    public float boundsSize = 200f;

    static readonly int TexID    = Shader.PropertyToID("_WaterSDF");
    static readonly int ParamsID = Shader.PropertyToID("_WaterSDF_Params");

    void OnEnable()  => Apply();
    void OnValidate() => Apply();

    void Apply()
    {
        if (sdfTexture == null || boundsSize <= 0f) return;
        Shader.SetGlobalTexture(TexID, sdfTexture);
        Shader.SetGlobalVector(ParamsID,
            new Vector4(boundsMinXZ.x, boundsMinXZ.y, 1f / boundsSize, 0f));
    }
}

/* ---------------------------------------------------------------------------
   HLSL side — include in your water shader (URP/HDRP/Built-in all fine):

   TEXTURE2D(_WaterSDF);
   SAMPLER(sampler_WaterSDF);
   float4 _WaterSDF_Params; // xy = bounds min XZ, z = 1/boundsSize

   void SampleWaterSDF(float3 positionWS,
                       out float shoreDist, out float waterDepth, out float2 shoreDir)
   {
       float2 uv = (positionWS.xz - _WaterSDF_Params.xy) * _WaterSDF_Params.z;
       float4 s  = SAMPLE_TEXTURE2D_LOD(_WaterSDF, sampler_WaterSDF, uv, 0);
       shoreDist  = s.r;   // meters, + in water, - on land (signed)
       waterDepth = s.g;   // meters
       shoreDir   = s.ba;  // world XZ, normalized, points away from shore
   }

   Typical usage in the water fragment shader:

       float shoreDist, waterDepth; float2 shoreDir;
       SampleWaterSDF(input.positionWS, shoreDist, waterDepth, shoreDir);

       // shoreline foam band — width is YOUR parameter, independent of the
       // slope of whatever geometry pierces the water (poles, walls, cliffs)
       float foam = 1.0 - saturate(shoreDist / _FoamWidth);
       foam *= foam; // sharper falloff

       // animated ripple rings radiating from every intersection.
       // A thin pole produces the same wide ripples as a beach would,
       // because these are rings of constant *distance*, not depth:
       float rings = sin((shoreDist - _Time.y * _RippleSpeed) * _RippleFreq);
       rings = saturate(rings) * (1.0 - saturate(shoreDist / _RippleExtent));
       foam = max(foam, rings);

       // break up the line with noise sampled along the shore tangent
       float2 tangent = float2(-shoreDir.y, shoreDir.x);
       // e.g. offset foam threshold by noise(positionWS.xz + tangent * _Time)

       // soft edge fade (replaces depth-buffer soft intersection)
       float edgeFade = saturate(shoreDist / _EdgeFadeWidth);

       // absorption / color by depth (replaces scene-depth reconstruction)
       float3 waterColor = lerp(_ShallowColor, _DeepColor,
                                saturate(waterDepth / _DepthFalloff));

   Flow map starter: shoreDir is the "downhill into water" direction; for a
   river, flow along the channel = the tangent float2(-shoreDir.y, shoreDir.x)
   (flip sign per bank using which side of the centerline you're on, or blend
   the tangents sampled from both banks).

   Shader Graph: Sample Texture 2D LOD with a custom UV of
   (WorldPos.xz - Params.xy) * Params.z, then use R and G as above.
--------------------------------------------------------------------------- */

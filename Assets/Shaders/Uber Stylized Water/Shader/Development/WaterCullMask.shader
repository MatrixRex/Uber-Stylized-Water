Shader "Universal Render Pipeline/WaterCullMask"
{
    Properties
    {
        [IntRange] _StencilID ("Stencil ID", Range(0, 255)) = 1
    }
    SubShader
    {
        Tags 
        { 
            "RenderType"="Opaque" 
            "Queue"="Geometry-1" 
            "RenderPipeline"="UniversalPipeline"
        }

        Pass
        {
            Name "WaterCullMaskPass"
            
            ColorMask 0
            ZWrite Off
            Cull Back
            ZTest LEqual
            
            Stencil
            {
                Ref [_StencilID]
                Comp Always
                Pass Replace
            }
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

              struct Attributes
              {
                  float4 positionOS   : POSITION;
              };

              struct Varyings
              {
                  float4 positionCS   : SV_POSITION;
              };

              Varyings vert(Attributes input)
              {
                  Varyings output;
                  output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                  return output;
              }

              half4 frag(Varyings input) : SV_Target
              {
                  return half4(0, 0, 0, 0);
              }
            ENDHLSL
        }
    }
}

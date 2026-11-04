Shader "Intercom/Frontend/Workshop Preview"
{
    Properties { _BaseColor ("Muted color", Color) = (0.5,0.5,0.5,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "WorkshopPreview"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Back ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 2.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; half3 normalWS : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                // Deliberate studio lighting, independent of the yard sun, shadows and post-processing.
                half key = dot(normalize(input.normalWS), normalize(half3(-0.5,0.85,-0.6)));
                half shade = key > 0.55h ? 1.05h : (key > 0.05h ? 0.84h : 0.63h);
                return half4(_BaseColor.rgb * shade, 1.0h);
            }
            ENDHLSL
        }
    }
}

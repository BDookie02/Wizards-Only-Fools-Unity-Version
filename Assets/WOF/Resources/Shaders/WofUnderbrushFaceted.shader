Shader "WOF/Underbrush Faceted"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        _BushLineColor ("Line Color", Color) = (0,0,0,1)
        _BushLineWidth ("Line Width", Float) = 0.044
        _BushLineOpacity ("Line Opacity", Float) = 0.74
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode"="UniversalForward" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _BushLineColor;
                half _BushLineWidth;
                half _BushLineOpacity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 barycentric : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half3 barycentric : TEXCOORD0;
                half fogFactor : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positions.positionCS;
                output.barycentric = input.barycentric;
                output.fogFactor = ComputeFogFactor(positions.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half edgeDistance = min(min(input.barycentric.x, input.barycentric.y), input.barycentric.z);
                half edge = 1.0h - smoothstep(_BushLineWidth, _BushLineWidth + 0.055h, edgeDistance);
                half3 color = lerp(_BaseColor.rgb, _BushLineColor.rgb, edge * _BushLineOpacity);
                return half4(MixFog(color, input.fogFactor), _BaseColor.a);
            }
            ENDHLSL
        }
    }
}

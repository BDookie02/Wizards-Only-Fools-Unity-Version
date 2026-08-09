Shader "WOF/Instanced Foliage"
{
    Properties { _Color ("Color", Color) = (1,1,1,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="AlphaTest" "RenderType"="Opaque" }
        Pass
        {
            Name "FoliageUnlit"
            Tags { "LightMode"="UniversalForward" }
            ZWrite On
            Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            half4 _Color;
            UNITY_INSTANCING_BUFFER_START(FoliageInstances)
                UNITY_DEFINE_INSTANCED_PROP(float4, _InstanceColor)
            UNITY_INSTANCING_BUFFER_END(FoliageInstances)

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color * UNITY_ACCESS_INSTANCED_PROP(FoliageInstances, _InstanceColor) * _Color;
                return output;
            }
            half4 frag(Varyings input) : SV_Target { return input.color; }
            ENDHLSL
        }
    }
}

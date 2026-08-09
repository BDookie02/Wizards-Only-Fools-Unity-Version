Shader "WOF/BOTW Grass"
{
    Properties
    {
        _MainTex ("Blade Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,0.98)
        _ViewerXZ ("Viewer XZ", Vector) = (0,0,0,0)
        _Radius ("Radius", Float) = 224
        _FadeWidth ("Fade Width", Float) = 34
        _WindTime ("Wind Time", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="TransparentCutout" }
        Pass
        {
            Name "BotwGrass"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
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
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                float3 positionWS : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            half4 _Color;
            float4 _ViewerXZ;
            float _Radius;
            float _FadeWidth;
            float _WindTime;

            UNITY_INSTANCING_BUFFER_START(GrassInstances)
                UNITY_DEFINE_INSTANCED_PROP(float4, _InstanceColor)
            UNITY_INSTANCING_BUFFER_END(GrassInstances)

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                float3 position = input.positionOS.xyz;
                float bendWeight = input.uv.y;
                float windA = sin(_WindTime * 1.65 + position.x * 2.1 + position.z * 1.2);
                float windB = cos(_WindTime * 2.15 + position.x * 0.7 - position.z * 1.8);
                position.x += (windA * 0.085 + windB * 0.045) * bendWeight;
                position.z += windB * 0.07 * bendWeight;
                output.positionWS = TransformObjectToWorld(position);
                output.positionHCS = TransformWorldToHClip(output.positionWS);
                output.uv = input.uv;
                output.color = input.color * UNITY_ACCESS_INSTANCED_PROP(GrassInstances, _InstanceColor);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half4 sampled = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                clip(sampled.a - 0.14);
                float distanceXZ = distance(input.positionWS.xz, _ViewerXZ.xy);
                float edgeFade = 1.0 - smoothstep(max(0.0, _Radius - _FadeWidth), _Radius, distanceXZ);
                half4 result = sampled * input.color * _Color;
                result.a *= edgeFade;
                clip(result.a - 0.018);
                return result;
            }
            ENDHLSL
        }
    }
}

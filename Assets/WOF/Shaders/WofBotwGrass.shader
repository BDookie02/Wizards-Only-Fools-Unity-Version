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
        _Cutoff ("Blade Cutoff", Range(0,1)) = 0.14
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
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
            float _Cutoff;

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
                float3 baseWS = TransformObjectToWorld(position);
                // World-space phases keep separate clumps from swaying in lockstep.
                // Three incommensurate fields read as local gusts instead of rows.
                float windA = sin(_WindTime * 0.82 + baseWS.x * 0.071 + baseWS.z * 0.043);
                float windB = sin(_WindTime * 1.19 + baseWS.x * 0.137 - baseWS.z * 0.093 +
                    sin(baseWS.z * 0.019) * 1.7);
                float windC = cos(_WindTime * 1.71 - baseWS.x * 0.223 + baseWS.z * 0.181);
                float gust = windA * 0.055 + windB * 0.036 + windC * 0.019;
                float2 windDirection = normalize(float2(
                    0.84 + sin(baseWS.z * 0.031 + _WindTime * 0.17) * 0.24,
                    0.36 + cos(baseWS.x * 0.027 - _WindTime * 0.13) * 0.28));
                output.positionWS = baseWS;
                output.positionWS.xz += windDirection * gust * bendWeight;
                output.positionHCS = TransformWorldToHClip(output.positionWS);
                output.uv = input.uv;
                output.color = input.color * UNITY_ACCESS_INSTANCED_PROP(GrassInstances, _InstanceColor);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half4 sampled = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                clip(sampled.a - _Cutoff);
                float distanceXZ = distance(input.positionWS.xz, _ViewerXZ.xy);
                float edgeFade = 1.0 - smoothstep(max(0.0, _Radius - _FadeWidth), _Radius, distanceXZ);
                half4 result = sampled * input.color * _Color;
                result.a *= edgeFade;
                return result;
            }
            ENDHLSL
        }
    }
}

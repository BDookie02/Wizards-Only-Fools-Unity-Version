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
        _SlopeUprightBlend ("Slope Upright Blend", Range(0,1)) = 0.82
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="AlphaTest" "RenderType"="Opaque" }
        Pass
        {
            Name "BotwGrass"
            Tags { "LightMode"="UniversalForward" }
            ZWrite On
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

            half4 _Color;
            float4 _ViewerXZ;
            float _Radius;
            float _FadeWidth;
            float _WindTime;
            float _Cutoff;
            float _SlopeUprightBlend;

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
                // The instance base plane stays tangent to the sampled slope so
                // every broad tuft remains rooted. Only upper vertices are
                // corrected toward gravity-up, preventing hillside cards from
                // lying across the camera as long brushed contour streaks.
                float3 scaledObjectUpWS = mul((float3x3)unity_ObjectToWorld, float3(0.0, 1.0, 0.0));
                float bladeHeightWS = length(scaledObjectUpWS);
                float3 surfaceGrowthWS = scaledObjectUpWS / max(0.0001, bladeHeightWS);
                baseWS += (float3(0.0, 1.0, 0.0) - surfaceGrowthWS) *
                    bladeHeightWS * input.positionOS.y * _SlopeUprightBlend;
                float windA = sin(_WindTime * 1.65 + position.x * 2.1 + position.z * 1.2 + baseWS.x * 0.027);
                float windB = cos(_WindTime * 2.15 + position.x * 0.7 - position.z * 1.8 + baseWS.z * 0.031);
                output.positionWS = baseWS;
                output.positionWS.x += (windA * 0.085 + windB * 0.045) * bendWeight;
                output.positionWS.z += windB * 0.07 * bendWeight;
                output.positionHCS = TransformWorldToHClip(output.positionWS);
                output.uv = input.uv;
                output.color = input.color * UNITY_ACCESS_INSTANCED_PROP(GrassInstances, _InstanceColor);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float distanceXZ = distance(input.positionWS.xz, _ViewerXZ.xy);
                float edgeFade = 1.0 - smoothstep(max(0.0, _Radius - _FadeWidth), _Radius, distanceXZ);
                clip(edgeFade - 0.04);
                return half4(input.color.rgb * _Color.rgb, 1.0);
            }
            ENDHLSL
        }
    }
}

Shader "WOF/Vertex Color Texture"
{
    Properties
    {
        _BaseMap("Texture", 2D) = "white" {}
        _BaseColor("Color", Color) = (1,1,1,1)
        _SurvivalGrassDetailStrength("Survival Grass Detail Strength", Range(0,1)) = 0
        _SurvivalGrassDetailScale("Survival Grass Detail Scale", Float) = 0.2
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Cull Off
        ZWrite On
        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                float3 positionWS : TEXCOORD1;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float _SurvivalGrassDetailStrength;
                float _SurvivalGrassDetailScale;
            CBUFFER_END
            float4 _WofSurvivalTerrainTint;

            float Hash21(float2 samplePosition)
            {
                samplePosition = frac(samplePosition * float2(123.34, 456.21));
                samplePosition += dot(samplePosition, samplePosition + 45.32);
                return frac(samplePosition.x * samplePosition.y);
            }

            float ValueNoise(float2 samplePosition)
            {
                float2 cell = floor(samplePosition);
                float2 local = frac(samplePosition);
                local = local * local * (3.0 - 2.0 * local);
                float first = lerp(Hash21(cell), Hash21(cell + float2(1.0, 0.0)), local.x);
                float second = lerp(Hash21(cell + float2(0.0, 1.0)), Hash21(cell + 1.0), local.x);
                return lerp(first, second, local.y);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 result = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor * input.color * _WofSurvivalTerrainTint;
                float greenMask = saturate((input.color.g - max(input.color.r, input.color.b)) * 7.0);
                float broad = ValueNoise(input.positionWS.xz * _SurvivalGrassDetailScale);
                float fine = ValueNoise((input.positionWS.xz + float2(37.0, -19.0)) * _SurvivalGrassDetailScale * 3.17);
                float detail = lerp(broad, fine, 0.34);
                float variation = lerp(
                    1.0 - _SurvivalGrassDetailStrength * 0.58,
                    1.0 + _SurvivalGrassDetailStrength * 0.34,
                    detail);
                result.rgb *= lerp(1.0, variation, greenMask);
                return result;
            }
            ENDHLSL
        }
    }
}

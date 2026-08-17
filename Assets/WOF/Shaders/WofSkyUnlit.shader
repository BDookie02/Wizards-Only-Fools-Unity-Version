Shader "WOF/Sky Unlit"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        [HideInInspector] _Cull ("Cull", Float) = 0
        [HideInInspector] _UseFog ("Use Fog", Float) = 0
        [HideInInspector] _UseClassicAtmosphere ("Use Classic Atmosphere", Float) = 0
        [HideInInspector] _ClassicTurbidity ("Classic Turbidity", Float) = 0.3
        [HideInInspector] _ClassicRayleigh ("Classic Rayleigh", Float) = 0.5
        [HideInInspector] _ClassicMieCoefficient ("Classic Mie Coefficient", Float) = 0.005
        [HideInInspector] _ClassicMieDirectionalG ("Classic Mie Directional G", Float) = 0.8
        [HideInInspector] _ClassicSunPosition ("Classic Sun Position", Vector) = (50,20,50,0)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-100" "RenderType"="Transparent" }
        Pass
        {
            Name "SkyUnlit"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half fogFactor : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                float3 classicSunDirection : TEXCOORD3;
                float3 classicBetaR : TEXCOORD4;
                float3 classicBetaM : TEXCOORD5;
                float classicSunE : TEXCOORD6;
                float classicSunfade : TEXCOORD7;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_ST;
            half4 _Color;
            half _UseFog;
            half _UseClassicAtmosphere;
            float _ClassicTurbidity;
            float _ClassicRayleigh;
            float _ClassicMieCoefficient;
            float _ClassicMieDirectionalG;
            float4 _ClassicSunPosition;

            static const float WOF_PI = 3.14159265358979323846;
            static const float WOF_E = 2.71828182845904523536;
            static const float3 WOF_TOTAL_RAYLEIGH = float3(
                5.804542996261093E-6,
                1.3562911419845635E-5,
                3.0265902468824876E-5);
            static const float3 WOF_MIE_CONST = float3(
                1.8399918514433978E14,
                2.7798023919660528E14,
                4.0790479543861094E14);

            float ClassicSunIntensity(float zenithAngleCos)
            {
                const float cutoffAngle = 1.6110731556870734;
                const float steepness = 1.5;
                const float energy = 1000.0;
                zenithAngleCos = clamp(zenithAngleCos, -1.0, 1.0);
                return energy * max(0.0, 1.0 - pow(WOF_E, -((cutoffAngle - acos(zenithAngleCos)) / steepness)));
            }

            float3 ClassicTotalMie(float turbidity)
            {
                float c = (0.2 * turbidity) * 10E-18;
                return 0.434 * c * WOF_MIE_CONST;
            }

            float ClassicRayleighPhase(float cosine)
            {
                return 0.05968310365946075 * (1.0 + cosine * cosine);
            }

            float ClassicHgPhase(float cosine, float g)
            {
                float g2 = g * g;
                float inverse = rcp(pow(1.0 - 2.0 * g * cosine + g2, 1.5));
                return 0.07957747154594767 * ((1.0 - g2) * inverse);
            }

            float3 EvaluateClassicAtmosphere(Varyings input)
            {
                float3 direction = normalize(input.positionWS - GetCameraPositionWS());
                float zenithAngle = acos(max(0.0, direction.y));
                float opticalInverse = rcp(
                    cos(zenithAngle) +
                    0.15 * pow(93.885 - ((zenithAngle * 180.0) / WOF_PI), -1.253));
                float rayleighLength = 8.4E3 * opticalInverse;
                float mieLength = 1.25E3 * opticalInverse;
                float3 extinction = exp(-(input.classicBetaR * rayleighLength + input.classicBetaM * mieLength));
                float cosTheta = dot(direction, input.classicSunDirection);
                float rayleighPhase = ClassicRayleighPhase(cosTheta * 0.5 + 0.5);
                float3 betaRTheta = input.classicBetaR * rayleighPhase;
                float miePhase = ClassicHgPhase(cosTheta, _ClassicMieDirectionalG);
                float3 betaMTheta = input.classicBetaM * miePhase;
                float3 scattering = input.classicSunE *
                    ((betaRTheta + betaMTheta) / (input.classicBetaR + input.classicBetaM));
                float3 light = pow(scattering * (1.0 - extinction), 1.5);
                light *= lerp(
                    1.0.xxx,
                    pow(scattering * extinction, 0.5),
                    saturate(pow(1.0 - input.classicSunDirection.y, 5.0)));
                float3 night = 0.1.xxx * extinction;
                float sunDisk = smoothstep(0.9999566769464484, 0.9999766769464484, cosTheta);
                night += (input.classicSunE * 19000.0 * extinction) * sunDisk;
                float3 textureColor = (light + night) * 0.04 + float3(0.0, 0.0003, 0.00075);
                return pow(max(textureColor, 0.0), rcp(1.2 + 1.2 * input.classicSunfade));
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.fogFactor = ComputeFogFactor(output.positionHCS.z);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.classicSunDirection = normalize(_ClassicSunPosition.xyz);
                output.classicSunE = ClassicSunIntensity(output.classicSunDirection.y);
                output.classicSunfade = 1.0 - saturate(1.0 - exp(_ClassicSunPosition.y / 450000.0));
                float rayleighCoefficient = _ClassicRayleigh - (1.0 - output.classicSunfade);
                output.classicBetaR = WOF_TOTAL_RAYLEIGH * rayleighCoefficient;
                output.classicBetaM = ClassicTotalMie(_ClassicTurbidity) * _ClassicMieCoefficient;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * _Color;
                if (_UseClassicAtmosphere > 0.5)
                {
                    color.rgb = EvaluateClassicAtmosphere(input);
                }
                color.rgb = lerp(color.rgb, MixFog(color.rgb, input.fogFactor), saturate(_UseFog));
                return color;
            }
            ENDHLSL
        }
    }
}

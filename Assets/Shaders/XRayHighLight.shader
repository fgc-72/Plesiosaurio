Shader "Custom/XRayHighlight"
{
    Properties
    {
        _Color ("Color", Color) = (1, 0.6, 0, 1)
        _MaxRange ("Rango Máximo", Float) = 15
        _FadeDistance ("Distancia de Desvanecido", Float) = 3
        _FresnelPower ("Suavidad del Borde", Range(0.5, 5)) = 2
        _NoiseScale ("Escala del Ruido", Float) = 50
        _NoiseStrength ("Fuerza del Ruido", Range(0, 1)) = 0.5
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            ZTest Always
            ZWrite Off
            Cull Back
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float4 _Color;
            float _MaxRange;
            float _FadeDistance;
            float _FresnelPower;
            float _NoiseScale;
            float _NoiseStrength;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionHCS = TransformWorldToHClip(OUT.positionWS);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                return OUT;
            }

            // Función simple de ruido pseudo-random basada en la posición
            float noise(float3 pos)
            {
                return frac(sin(dot(pos, float3(12.9898, 78.233, 45.164))) * 43758.5453);
            }

            float4 frag(Varyings IN) : SV_Target
            {
                float dist = distance(IN.positionWS, _WorldSpaceCameraPos);

                if (dist > _MaxRange)
                    discard;

                float distFade = 1.0 - saturate((dist - (_MaxRange - _FadeDistance)) / _FadeDistance);

                // Fresnel: qué tan "de canto" está la superficie respecto a la cámara
                float3 viewDir = normalize(_WorldSpaceCameraPos - IN.positionWS);
                float fresnel = 1.0 - saturate(dot(normalize(IN.normalWS), viewDir));
                fresnel = pow(fresnel, _FresnelPower);

                // Ruido basado en la posición del objeto en el mundo
                float n = noise(IN.positionWS * _NoiseScale);

                // Combinamos: el fresnel reduce el alpha en los bordes,
                // y el ruido rompe ese borde para que no sea un fade limpio
                float edgeAlpha = 1.0 - (fresnel * _NoiseStrength * n);

                float finalAlpha = _Color.a * distFade * edgeAlpha;

                return float4(_Color.rgb, finalAlpha);
            }
            ENDHLSL
        }
    }
}
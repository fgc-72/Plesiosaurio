Shader "Hidden/UnderwaterFX/CausticsProjection"
{
    // Pase de pantalla completa: reconstruye la posicion en el mundo de cada pixel con la
    // textura de profundidad, la proyecta a lo largo de la luz principal hasta la superficie
    // del agua y muestrea ahi la textura de caustics (_WaterCausticsTex, generada en la fase 1).
    // Se mezcla multiplicando el color de la escena: resultado = escena * (1 + caustics).
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "CausticsProjection"
            ZTest Always
            ZWrite Off
            Cull Off
            Blend DstColor One, Zero One

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

            TEXTURE2D(_WaterCausticsTex);
            SAMPLER(sampler_WaterCausticsTex);

            float4x4 _CausticsWorldToVolume;
            float _CausticsScale;
            float _SurfaceY;
            float _NearSurface;
            float _NearSurfaceWidth;
            float _CausticsIntensity;
            float2 _ColorShift;
            float _LightSaturation;
            float _NormalAtten;
            float _ReceiveShadows;
            float _EdgeFade;

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(uint vertexID : SV_VertexID)
            {
                Varyings o;
                o.positionCS = GetFullScreenTriangleVertexPosition(vertexID);
                o.uv = GetFullScreenTriangleTexCoord(vertexID);
                return o;
            }

            float4 Frag(Varyings i) : SV_Target
            {
                float2 uv = i.uv;

                float depth = SampleSceneDepth(uv);
                #if UNITY_REVERSED_Z
                    if (depth <= 0.0) discard; // cielo / fondo lejano
                #else
                    if (depth >= 1.0) discard;
                    depth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, depth);
                #endif

                float3 worldPos = ComputeWorldSpacePosition(uv, depth, UNITY_MATRIX_I_VP);

                // Recorte por volumen: cubo unitario en espacio local del objeto.
                float3 lp = mul(_CausticsWorldToVolume, float4(worldPos, 1.0)).xyz;
                float3 q = abs(lp);
                if (q.x > 0.5 || q.y > 0.5 || q.z > 0.5) discard;
                float3 edge = saturate((0.5 - q) / max(_EdgeFade, 1e-4));
                float volMask = edge.x * edge.y * edge.z;

                float depthBelow = _SurfaceY - worldPos.y;
                if (depthBelow < 0.0) discard;

                float4 shadowCoord = TransformWorldToShadowCoord(worldPos);
                Light mainLight = GetMainLight(shadowCoord);
                float3 L = mainLight.direction; // apunta hacia la luz
                if (L.y <= 0.02) discard;        // luz por debajo del horizonte

                // Proyeccion a lo largo del rayo de luz hasta el plano del agua.
                float dist = depthBelow / L.y;
                float3 hit = worldPos + L * dist;
                float2 cuv = hit.xz / max(_CausticsScale, 1e-3);

                float2 shift = _ColorShift * 0.01;
                float3 caustic;
                caustic.r = SAMPLE_TEXTURE2D(_WaterCausticsTex, sampler_WaterCausticsTex, cuv + shift).r;
                caustic.g = SAMPLE_TEXTURE2D(_WaterCausticsTex, sampler_WaterCausticsTex, cuv).g;
                caustic.b = SAMPLE_TEXTURE2D(_WaterCausticsTex, sampler_WaterCausticsTex, cuv - shift).b;

                // Atenuaciones.
                float nearAtt = lerp(1.0, saturate(depthBelow / max(_NearSurfaceWidth, 1e-3)), _NearSurface);

                float3 N = SampleSceneNormals(uv);
                float normalAtt = lerp(1.0, saturate(dot(N, L)), _NormalAtten);

                float shadow = lerp(1.0, mainLight.shadowAttenuation, _ReceiveShadows);

                float lum = dot(mainLight.color, float3(0.2126, 0.7152, 0.0722));
                float3 lightCol = lerp(lum.xxx, mainLight.color, _LightSaturation);

                float3 result = caustic * lightCol * _CausticsIntensity
                              * nearAtt * normalAtt * shadow * volMask;

                return float4(result, 1.0);
            }
            ENDHLSL
        }
    }
}

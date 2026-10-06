Shader "Hidden/UnderwaterFX/GodRays"
{
    // Rayos de luz volumetricos bajo el agua (raymarching de pantalla completa, aditivo).
    // Para cada pixel se avanza por el rayo camara -> geometria, solo por el tramo bajo la superficie.
    // En cada muestra se acumula luz dispersada por el medio:
    //   - sombra de la luz principal (shadow map): los objetos recortan los rayos
    //   - patron de causticas proyectado hasta la superficie: los haces ondulan y bailan
    //   - absorcion de la luz al bajar desde la superficie y de vuelta a la camara
    //   - funcion de fase Henyey-Greenstein: mas brillo al mirar hacia la luz
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "GodRays"
            ZTest Always
            ZWrite Off
            Cull Off
            Blend One One

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE2D(_WaterCausticsTex);
            SAMPLER(sampler_WaterCausticsTex);

            float _GRSurfaceY;
            float _GRIntensity;
            float _GRSteps;
            float _GRMaxDist;
            float _GRAniso;
            float3 _GRColor;
            float _GRPattern;
            float _GRScale;
            float3 _GRAbsorption;

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

            float HenyeyGreenstein(float cosTheta, float g)
            {
                float g2 = g * g;
                float denom = 1.0 + g2 - 2.0 * g * cosTheta;
                return (1.0 - g2) / (4.0 * PI * pow(max(denom, 1e-4), 1.5));
            }

            float4 Frag(Varyings i) : SV_Target
            {
                float rawDepth = SampleSceneDepth(i.uv);
                #if !UNITY_REVERSED_Z
                    rawDepth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, rawDepth);
                #endif

                float3 worldPos = ComputeWorldSpacePosition(i.uv, rawDepth, UNITY_MATRIX_I_VP);
                float3 cam = _WorldSpaceCameraPos;

                float3 rayDir = worldPos - cam;
                float dist = length(rayDir);
                rayDir /= max(dist, 1e-4);
                dist = min(dist, _GRMaxDist);

                // Tramo del rayo que queda bajo la superficie del agua: [tEnter, tExit].
                float tEnter = 0.0;
                float tExit = dist;
                float dy = rayDir.y;
                if (dy > 1e-5)
                {
                    tExit = min(dist, (_GRSurfaceY - cam.y) / dy);
                }
                else if (dy < -1e-5)
                {
                    tEnter = max(0.0, (_GRSurfaceY - cam.y) / dy);
                }
                else if (cam.y >= _GRSurfaceY)
                {
                    return 0.0;
                }
                if (tExit <= tEnter) return 0.0;

                Light mainLight = GetMainLight();
                float3 L = mainLight.direction; // apunta hacia la luz
                if (L.y <= 0.02) return 0.0;

                int steps = clamp((int)_GRSteps, 4, 64);
                float segLen = tExit - tEnter;
                float stepLen = segLen / steps;
                float jitter = InterleavedGradientNoise(i.positionCS.xy, 0);

                float phase = HenyeyGreenstein(dot(rayDir, L), _GRAniso) * 4.0 * PI;

                float3 accum = 0.0;
                for (int s = 0; s < steps; s++)
                {
                    float t = tEnter + (s + jitter) * stepLen;
                    float3 p = cam + rayDir * t;

                    float depthBelow = max(0.0, _GRSurfaceY - p.y);
                    float lightPath = depthBelow / max(L.y, 0.05);

                    // Luz que llega al punto desde la superficie y luz que vuelve hasta la camara.
                    float3 lightAtt = exp(-_GRAbsorption * lightPath);
                    float3 viewAtt = exp(-_GRAbsorption * (t - tEnter));

                    float4 shadowCoord = TransformWorldToShadowCoord(p);
                    Light shadowLight = GetMainLight(shadowCoord, p, half4(1, 1, 1, 1));
                    float shadow = shadowLight.shadowAttenuation;

                    // Punto donde este rayo de luz cruza la superficie: ahi se muestrea el patron.
                    float2 suv = (p + L * lightPath).xz / max(_GRScale, 1e-3);
                    float pat = SAMPLE_TEXTURE2D_LOD(_WaterCausticsTex, sampler_WaterCausticsTex, suv, 3.0).g;
                    pat = lerp(1.0, pat, _GRPattern);

                    accum += lightAtt * viewAtt * shadow * pat;
                }

                float3 result = accum * stepLen * 0.02 * phase * _GRIntensity
                              * mainLight.color * _GRColor;
                return float4(result, 1.0);
            }
            ENDHLSL
        }
    }
}

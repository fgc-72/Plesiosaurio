Shader "UnderwaterFX/WaterSurface"
{
    // Superficie del agua de doble cara para URP.
    // Desde abajo: ventana de Snell (se ve el cielo dentro de ~48 grados) y reflexion
    // interna total fuera de ella, con Fresnel real. Desde arriba: Fresnel simple.
    // Las ondas son solo normales (no desplaza vertices), asi que usa un plano normal.
    Properties
    {
        [HDR] _SkyColor ("Sky Color (visto desde abajo)", Color) = (0.55, 0.85, 1.0, 1)
        [HDR] _SunColor ("Sun Color", Color) = (1.0, 0.95, 0.8, 1)
        _SunIntensity ("Sun Intensity", Float) = 6
        _SunSharpness ("Sun Sharpness", Float) = 300
        [HDR] _UnderColor ("Reflexion interna (color del agua)", Color) = (0.02, 0.18, 0.30, 1)
        [HDR] _AboveColor ("Color del agua visto desde arriba", Color) = (0.0, 0.25, 0.35, 1)
        _WaveScale ("Wave Scale", Float) = 0.4
        _WaveSpeed ("Wave Speed", Float) = 0.6
        _WaveStrength ("Wave Strength", Range(0, 1.5)) = 0.35
        _IOR ("Refraction Index", Range(1.01, 2)) = 1.333
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            ZWrite On

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _SkyColor;
                float4 _SunColor;
                float _SunIntensity;
                float _SunSharpness;
                float4 _UnderColor;
                float4 _AboveColor;
                float _WaveScale;
                float _WaveSpeed;
                float _WaveStrength;
                float _IOR;
            CBUFFER_END

            float _RainIntensity; // global, lo escribe WaterRainController (0 = seco, 1 = lluvia)

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                return o;
            }

            static const float2 kDirs[5]  = { float2(1.0, 0.0), float2(0.7, 0.7), float2(-0.4, 0.9), float2(0.9, -0.4), float2(-0.8, -0.6) };
            static const float kFreqs[5]  = { 1.0, 1.9, 3.1, 5.3, 8.7 };
            static const float kAmps[5]   = { 1.0, 0.6, 0.35, 0.2, 0.1 };
            static const float kSpeeds[5] = { 0.8, 1.1, 1.5, 2.1, 2.8 };

            float Hash21(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            // Anillos de gotas de lluvia: devuelve la pendiente (x, z) de la superficie.
            // Cada celda tiene una gota que se expande y se desvanece; con mas lluvia
            // se activan mas celdas.
            float2 RainRipples(float2 p, float t)
            {
                float2 g = 0.0;
                [unroll]
                for (int layer = 0; layer < 2; layer++)
                {
                    float cellSize = (layer == 0) ? 1.3 : 0.85;
                    float2 q = p / cellSize + layer * 7.3;
                    float2 cell = floor(q);
                    float2 f = frac(q) - 0.5;

                    float gate = step(Hash21(cell), _RainIntensity);
                    float2 c = (float2(Hash21(cell + 3.1), Hash21(cell + 9.7)) - 0.5) * 0.5;
                    float phase = frac(t * 0.8 + Hash21(cell + 1.7));

                    float2 d = f - c;
                    float r = length(d) + 1e-4;
                    float ringR = phase * 0.45;
                    float w = 0.06;
                    float x = (r - ringR) / w;
                    float dH = -2.0 * x / w * exp(-x * x); // derivada radial del anillo

                    g += (d / r) * dH * (1.0 - phase) * gate * 0.03 / cellSize;
                }
                return g;
            }

            // Cielo con nubes: mas gris y mas oscuro al subir la lluvia.
            float3 StormSky(float3 c)
            {
                float lum = dot(c, float3(0.2126, 0.7152, 0.0722));
                float3 gray = lum * float3(0.6, 0.68, 0.75);
                return lerp(c, gray, _RainIntensity * 0.75) * lerp(1.0, 0.5, _RainIntensity);
            }

            // Normal de la superficie (apunta hacia arriba) a partir de una suma de ondas.
            float3 WaveNormal(float2 p, float t)
            {
                float2 g = 0.0;
                [unroll]
                for (int i = 0; i < 5; i++)
                {
                    float2 d = normalize(kDirs[i]);
                    float phase = dot(d, p) * kFreqs[i] * _WaveScale * 6.2831853 + t * kSpeeds[i] * _WaveSpeed * 2.0;
                    g += d * cos(phase) * kFreqs[i] * kAmps[i];
                }
                g *= _WaveStrength * (1.0 + _RainIntensity * 0.8) * 0.15;
                g += RainRipples(p, t) * _RainIntensity;
                return normalize(float3(-g.x, 1.0, -g.y));
            }

            float FresnelExact(float cosI, float n1, float n2)
            {
                float sinT2 = (n1 / n2) * (n1 / n2) * (1.0 - cosI * cosI);
                if (sinT2 >= 1.0) return 1.0; // reflexion interna total
                float cosT = sqrt(1.0 - sinT2);
                float rs = (n1 * cosI - n2 * cosT) / (n1 * cosI + n2 * cosT);
                float rp = (n2 * cosI - n1 * cosT) / (n2 * cosI + n1 * cosT);
                return 0.5 * (rs * rs + rp * rp);
            }

            float4 Frag(Varyings i) : SV_Target
            {
                float3 posWS = i.positionWS;
                float3 V = normalize(_WorldSpaceCameraPos - posWS); // hacia la camara
                bool below = _WorldSpaceCameraPos.y < posWS.y;

                float3 N = WaveNormal(posWS.xz, _Time.y);
                float3 viewN = below ? -N : N; // normal orientada hacia el observador
                float cosI = saturate(dot(V, viewN));

                float3 I = -V;
                Light mainLight = GetMainLight();
                float3 L = mainLight.direction;

                float3 color;
                if (below)
                {
                    float F = FresnelExact(cosI, _IOR, 1.0);
                    float3 T = refract(I, viewN, _IOR); // agua -> aire
                    float3 transmitted = 0.0;
                    if (dot(T, T) > 1e-5)
                    {
                        T = normalize(T);
                        float3 sky = StormSky(_SkyColor.rgb) * (0.7 + 0.3 * saturate(T.y));
                        float sun = pow(saturate(dot(T, L)), _SunSharpness) * _SunIntensity * (1.0 - _RainIntensity);
                        transmitted = sky + _SunColor.rgb * sun;
                    }
                    color = lerp(transmitted, _UnderColor.rgb, F);
                }
                else
                {
                    float F = FresnelExact(cosI, 1.0, _IOR);
                    float3 R = reflect(I, viewN);
                    float sun = pow(saturate(dot(R, L)), _SunSharpness) * _SunIntensity * (1.0 - _RainIntensity);
                    float3 reflected = StormSky(_SkyColor.rgb) * (0.7 + 0.3 * saturate(R.y)) + _SunColor.rgb * sun;
                    color = lerp(_AboveColor.rgb, reflected, F);
                }

                return float4(color, 1.0);
            }
            ENDHLSL
        }

        // Estos pases escriben la superficie en el prepass de profundidad/normales. Hacen falta
        // para que las causticas y la niebla vean el plano (usan la textura de profundidad).
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Off

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct DepthAttributes
            {
                float4 positionOS : POSITION;
            };

            struct DepthVaryings
            {
                float4 positionCS : SV_POSITION;
            };

            DepthVaryings DepthVert(DepthAttributes v)
            {
                DepthVaryings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                return o;
            }

            half4 DepthFrag(DepthVaryings i) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull Off

            HLSLPROGRAM
            #pragma vertex NormVert
            #pragma fragment NormFrag
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct NormAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct NormVaryings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
            };

            NormVaryings NormVert(NormAttributes v)
            {
                NormVaryings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }

            half4 NormFrag(NormVaryings i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                #if defined(_GBUFFER_NORMALS_OCT)
                    float3 packedN = PackNormalOctQuadEncode(n);
                    float2 remapped = saturate(packedN.xy * 0.5 + 0.5);
                    return half4(PackFloat2To888(remapped), 0.0);
                #else
                    return half4(n, 0.0);
                #endif
            }
            ENDHLSL
        }
    }
}

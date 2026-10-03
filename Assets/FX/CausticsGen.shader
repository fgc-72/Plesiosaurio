Shader "Hidden/UnderwaterFX/CausticsGen"
{
    // Genera una textura de caustics tileable en una sola pasada.
    // Idea: un campo de alturas periodico (ruido de valor) deforma la superficie del agua.
    // La luz refractada llega al fondo desplazada segun la pendiente; la intensidad
    // en cada punto es 1 / |det(J)|, donde J es el jacobiano del mapeo superficie -> fondo.
    // Donde det(J) -> 0 los rayos convergen y aparece la linea brillante de la caustica.
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "CausticsGen"
            ZTest Always
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #define MAX_WAVES 4

            int _WaveCount;
            float4 _WaveA[MAX_WAVES];
            float4 _WaveB[MAX_WAVES];
            float _Depth;
            float _IOR;
            float _Chroma;
            float _FillGap;
            float _Brightness;
            float _Gamma;
            float _Clamp;
            float _Eps;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.uv = input.uv;
                return o;
            }

            float Hash13(float3 p3)
            {
                p3 = frac(p3 * 0.1031);
                p3 += dot(p3, p3.zyx + 31.32);
                return frac((p3.x + p3.y) * p3.z) * 2.0 - 1.0;
            }

            // Valor del reticulado con envoltura (wrap) para que el patron sea tileable.
            float Lattice(float2 cell, float period, float z)
            {
                cell -= period * floor(cell / period);
                return Hash13(float3(cell, z));
            }

            float Quintic(float t)
            {
                return t * t * t * (t * (t * 6.0 - 15.0) + 10.0);
            }

            // Ruido de valor periodico en xy y suave en el tiempo (z).
            float PeriodicNoise(float2 uv, float period, float2 offset, float t)
            {
                float2 x = uv * period + offset;
                float2 i = floor(x);
                float2 f = frac(x);
                float2 u = float2(Quintic(f.x), Quintic(f.y));

                float z0 = floor(t);
                float z1 = z0 + 1.0;
                float uz = Quintic(frac(t));

                float n0 = lerp(
                    lerp(Lattice(i, period, z0), Lattice(i + float2(1, 0), period, z0), u.x),
                    lerp(Lattice(i + float2(0, 1), period, z0), Lattice(i + float2(1, 1), period, z0), u.x),
                    u.y);

                float n1 = lerp(
                    lerp(Lattice(i, period, z1), Lattice(i + float2(1, 0), period, z1), u.x),
                    lerp(Lattice(i + float2(0, 1), period, z1), Lattice(i + float2(1, 1), period, z1), u.x),
                    u.y);

                return lerp(n0, n1, uz);
            }

            float Height(float2 uv)
            {
                float h = 0.0;
                for (int w = 0; w < MAX_WAVES; w++)
                {
                    if (w >= _WaveCount) break;
                    float4 a = _WaveA[w];
                    float4 b = _WaveB[w];
                    float period = max(1.0, round(a.x));
                    float2 offset = b.xy * b.w;
                    float tz = a.z * b.w + b.z;
                    // Se divide por period^2 para que "height" controle la curvatura
                    // y no cambie al variar la densidad.
                    h += a.w * a.y * PeriodicNoise(uv, period, offset, tz) / (period * period);
                }
                return h;
            }

            float4 Frag(Varyings i) : SV_Target
            {
                float e = _Eps;
                float2 uv = i.uv;

                float h = Height(uv);
                float hxx = (Height(uv + float2(e, 0)) - 2.0 * h + Height(uv - float2(e, 0))) / (e * e);
                float hyy = (Height(uv + float2(0, e)) - 2.0 * h + Height(uv - float2(0, e))) / (e * e);
                float hxy = (Height(uv + float2(e, e)) - Height(uv + float2(e, -e))
                           - Height(uv + float2(-e, e)) + Height(uv - float2(e, e))) / (4.0 * e * e);

                // Un indice de refraccion distinto por canal da la aberracion cromatica (R < G < B).
                float3 ior = _IOR + float3(-1.0, 0.0, 1.0) * _Chroma;
                float3 a = _Depth * (1.0 - 1.0 / ior);

                float3 det = 1.0 + a * (hxx + hyy) + a * a * (hxx * hyy - hxy * hxy);

                float3 c = _Brightness / (abs(det) + _FillGap);
                c = pow(max(c, 0.0), _Gamma);
                c = min(c, _Clamp);

                return float4(c, 1.0);
            }
            ENDHLSL
        }
    }
}

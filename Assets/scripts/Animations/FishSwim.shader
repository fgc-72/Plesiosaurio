Shader "Fish/FishSwim"
{
    // Pez para cardumenes en GPU (URP). Sin huesos ni Animator:
    //  - La posicion, direccion, fase e intensidad de nado de cada pez vienen del buffer _Fish
    //    (lo escribe FishSchool.compute) y se dibujan todos con UNA llamada indirecta.
    //  - El nado se hace en el vertex shader combinando cuatro movimientos simples
    //    (idea del equipo de Abzu):
    //      1) giro (yaw) alrededor del pivote
    //      2) desplazamiento lateral (se aplica DESPUES del giro)
    //      3) balanceo (roll) a lo largo de la columna, desfasado segun la distancia al pivote
    //      4) segundo giro desfasado por distancia (efecto bandera en la cola)
    //
    // Convencion de la malla: el pez mira hacia +Z, arriba es +Y, el pivote esta en la cabeza/centro
    // y la cola queda hacia -Z. Si tu modelo mira hacia otro lado usa "Model Yaw".
    Properties
    {
        _BaseMap ("Textura", 2D) = "white" {}
        _BaseColor ("Color", Color) = (1, 1, 1, 1)
        _Brightness ("Brillo", Range(0, 3)) = 1

        [Header(Malla)]
        _Scale ("Escala", Float) = 1
        _BodyLength ("Largo del cuerpo (pivote a cola)", Float) = 1
        _ModelYaw ("Model Yaw (0 = mira a +Z, -90 = mira a +X, 180 = mira a -Z, 90 = mira a -X)", Float) = 0

        [Header(Nado)]
        _YawAmp ("1) Giro alrededor del pivote (rad)", Range(0, 1)) = 0.12
        _SideAmp ("2) Desplazamiento lateral (fraccion del cuerpo)", Range(0, 0.5)) = 0.06
        _SidePhase ("2) Desfase lateral", Float) = 1.2
        _RollAmp ("3) Balanceo (rad)", Range(0, 1.5)) = 0.25
        _RollWave ("3) Desfase del balanceo a lo largo del cuerpo", Range(0, 8)) = 3
        _FlagAmp ("4) Giro tipo bandera (rad)", Range(0, 1.5)) = 0.5
        _FlagWave ("4) Desfase tipo bandera a lo largo del cuerpo", Range(0, 8)) = 4
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            float4 _BaseColor;
            float _Brightness;
            float _Scale;
            float _BodyLength;
            float _ModelYaw;
            float _YawAmp;
            float _SideAmp;
            float _SidePhase;
            float _RollAmp;
            float _RollWave;
            float _FlagAmp;
            float _FlagWave;
        CBUFFER_END

        // Debe coincidir con FishData en FishSchoolManager.cs
        struct FishData
        {
            float3 pos;
            float phase;
            float3 vel;
            float intensity;
            uint species;
            float pad0;
            float pad1;
            float pad2;
        };

        StructuredBuffer<FishData> _Fish;
        int _SpeciesOffset; // lo pone el manager por especie

        float3 RotY(float3 p, float a)
        {
            float s, c;
            sincos(a, s, c);
            return float3(c * p.x + s * p.z, p.y, -s * p.x + c * p.z);
        }

        float3 RotZ(float3 p, float a)
        {
            float s, c;
            sincos(a, s, c);
            return float3(c * p.x - s * p.y, s * p.x + c * p.y, p.z);
        }

        float3 SwimDeform(float3 p, float phase, float k)
        {
            float tail = saturate(-p.z / max(_BodyLength, 1e-3)); // 0 en la cabeza/pivote, 1 en la cola

            p = RotY(p, sin(phase) * _YawAmp * k);                                       // 1) giro
            p.x += sin(phase + _SidePhase) * _SideAmp * _BodyLength * k * (0.4 + 0.6 * tail); // 2) lateral
            p = RotZ(p, sin(phase + tail * _RollWave) * _RollAmp * k);                    // 3) balanceo
            p = RotY(p, sin(phase + tail * _FlagWave) * _FlagAmp * k * tail);             // 4) bandera
            return p;
        }

        void FishTransform(float3 positionOS, float3 normalOS, uint instanceID,
                           out float3 positionWS, out float3 normalWS)
        {
            FishData f = _Fish[instanceID + (uint)_SpeciesOffset];

            float yaw = radians(_ModelYaw);
            float3 p = RotY(positionOS, yaw);
            float3 n = RotY(normalOS, yaw);

            p = SwimDeform(p, f.phase, f.intensity) * _Scale;

            // Base orientada segun la direccion de nado.
            float speed = length(f.vel);
            float3 fwd = speed > 1e-4 ? f.vel / speed : float3(0, 0, 1);
            float3 right = cross(float3(0, 1, 0), fwd);
            float rl = length(right);
            right = rl > 1e-4 ? right / rl : float3(1, 0, 0);
            float3 up = cross(fwd, right);

            positionWS = f.pos + right * p.x + up * p.y + fwd * p.z;
            normalWS = normalize(right * n.x + up * n.y + fwd * n.z);
        }
        ENDHLSL

        // ------------------------------------------------------------ ForwardLit
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag

            struct Attributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                uint instanceID : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
            };

            Varyings Vert(Attributes v)
            {
                float3 positionWS, normalWS;
                FishTransform(v.positionOS, v.normalOS, v.instanceID, positionWS, normalWS);

                Varyings o;
                o.positionCS = TransformWorldToHClip(positionWS);
                o.normalWS = normalWS;
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).rgb * _BaseColor.rgb;
                float3 n = normalize(i.normalWS);

                Light mainLight = GetMainLight();
                half ndl = saturate(dot(n, mainLight.direction) * 0.5 + 0.5); // luz envolvente: suave bajo el agua
                half3 ambient = SampleSH(n);

                half3 color = albedo * (mainLight.color * ndl + ambient) * _Brightness;
                return half4(color, 1.0);
            }
            ENDHLSL
        }

        // ------------------------------------------------------------ DepthOnly
        // Hace falta para que la niebla, las causticas y los rayos de luz vean a los peces.
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex DepthVert
            #pragma fragment DepthFrag

            struct DepthAttributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                uint instanceID : SV_InstanceID;
            };

            struct DepthVaryings
            {
                float4 positionCS : SV_POSITION;
            };

            DepthVaryings DepthVert(DepthAttributes v)
            {
                float3 positionWS, normalWS;
                FishTransform(v.positionOS, v.normalOS, v.instanceID, positionWS, normalWS);

                DepthVaryings o;
                o.positionCS = TransformWorldToHClip(positionWS);
                return o;
            }

            half4 DepthFrag(DepthVaryings i) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        // ------------------------------------------------------------ DepthNormals
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex NormVert
            #pragma fragment NormFrag
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT

            struct NormAttributes
            {
                float3 positionOS : POSITION;
                float3 normalOS : NORMAL;
                uint instanceID : SV_InstanceID;
            };

            struct NormVaryings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
            };

            NormVaryings NormVert(NormAttributes v)
            {
                float3 positionWS, normalWS;
                FishTransform(v.positionOS, v.normalOS, v.instanceID, positionWS, normalWS);

                NormVaryings o;
                o.positionCS = TransformWorldToHClip(positionWS);
                o.normalWS = normalWS;
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

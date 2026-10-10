Shader "Custom/XRayHighlight"
{
    // Aura estilo modo escucha de The Last of Us, en espacio de pantalla.
    //
    // Pase 0 (Mask):  dibuja los objetos marcados SIN test de profundidad en una mascara:
    //                   R = intensidad (se apaga con la distancia)
    //                   G = 1 si esa parte del objeto es visible (no hay pared delante)
    //                   B = 1 en toda la silueta
    // Pase 1 (Blur):  desenfoque gaussiano separable de la mascara (resolucion media).
    // Pase 2 (Composite): suma al color de la escena SOLO el halo que rodea la silueta
    //                   (el cuerpo del personaje no se toca cuando se ve), mas un relleno
    //                   tenue de la silueta cuando esta tapado por una pared.
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

        struct FSVaryings
        {
            float4 positionCS : SV_POSITION;
            float2 uv : TEXCOORD0;
        };

        FSVaryings FSVert(uint vertexID : SV_VertexID)
        {
            FSVaryings o;
            o.positionCS = GetFullScreenTriangleVertexPosition(vertexID);
            o.uv = GetFullScreenTriangleTexCoord(vertexID);
            return o;
        }
        ENDHLSL

        // ---------------------------------------------------------------- 0: Mask
        Pass
        {
            Name "Mask"
            ZTest Always
            ZWrite Off
            Cull Off
            Blend One One
            BlendOp Max

            HLSLPROGRAM
            #pragma vertex MaskVert
            #pragma fragment MaskFrag

            float _MaxRange;
            float _FadeDistance;
            float _DepthBias;

            struct MaskAttributes
            {
                float4 positionOS : POSITION;
            };

            struct MaskVaryings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float eyeDepth : TEXCOORD1;
            };

            MaskVaryings MaskVert(MaskAttributes v)
            {
                MaskVaryings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.eyeDepth = -TransformWorldToView(o.positionWS).z;
                return o;
            }

            float4 MaskFrag(MaskVaryings i) : SV_Target
            {
                float dist = distance(i.positionWS, _WorldSpaceCameraPos);
                if (dist > _MaxRange) discard;

                float fade = 1.0 - saturate((dist - (_MaxRange - _FadeDistance)) / max(_FadeDistance, 1e-3));

                float2 uv = GetNormalizedScreenSpaceUV(i.positionCS);
                float sceneEye = LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);
                float visible = (i.eyeDepth <= sceneEye + _DepthBias) ? 1.0 : 0.0;

                return float4(fade, visible, 1.0, 0.0); // alfa = 0: el alfa solo lo escriben los bloqueadores
            }
            ENDHLSL
        }

        // ---------------------------------------------------------------- 1: Blur
        Pass
        {
            Name "Blur"
            ZTest Always
            ZWrite Off
            Cull Off
            Blend Off

            HLSLPROGRAM
            #pragma vertex FSVert
            #pragma fragment BlurFrag

            TEXTURE2D(_ListenSrc);
            float2 _ListenBlurDir; // paso entre muestras en UV

            float4 BlurFrag(FSVaryings i) : SV_Target
            {
                const float w[5] = { 0.2270270270, 0.1945945946, 0.1216216216, 0.0540540541, 0.0162162162 };

                float sum = SAMPLE_TEXTURE2D(_ListenSrc, sampler_LinearClamp, i.uv).r * w[0];
                [unroll]
                for (int k = 1; k < 5; k++)
                {
                    float2 o = _ListenBlurDir * k;
                    sum += SAMPLE_TEXTURE2D(_ListenSrc, sampler_LinearClamp, i.uv + o).r * w[k];
                    sum += SAMPLE_TEXTURE2D(_ListenSrc, sampler_LinearClamp, i.uv - o).r * w[k];
                }
                return float4(sum, 0.0, 0.0, 1.0);
            }
            ENDHLSL
        }

        // ---------------------------------------------------------------- 2: Composite
        Pass
        {
            Name "Composite"
            ZTest Always
            ZWrite Off
            Cull Off
            Blend One One

            HLSLPROGRAM
            #pragma vertex FSVert
            #pragma fragment CompositeFrag

            TEXTURE2D(_ListenMaskTex);
            TEXTURE2D(_ListenBlurTex);

            float3 _AuraColor;
            float3 _CoreColor;
            float _AuraIntensity;
            float _AuraFalloff;
            float _OccludedFill;
            float _PulseAmount;
            float _PulseSpeed;
            float _DebugView; // 0 = normal, 1 = ver mascara, 2 = ver desenfoque

            float4 CompositeFrag(FSVaryings i) : SV_Target
            {
                float4 m = SAMPLE_TEXTURE2D(_ListenMaskTex, sampler_LinearClamp, i.uv);
                float blur = SAMPLE_TEXTURE2D(_ListenBlurTex, sampler_LinearClamp, i.uv).r;

                if (_DebugView > 0.5 && _DebugView < 1.5) return float4(m.rgb, 1.0);
                if (_DebugView > 1.5) return float4(blur.xxx, 1.0);

                float silhouette = m.b;

                // En el borde de la silueta el desenfoque vale ~0.5; se reescala para que el
                // halo sea 1 justo en el borde y se apague hacia afuera.
                float a = pow(saturate(blur * 2.0), _AuraFalloff);
                float aura = a * (1.0 - silhouette); // nunca sobre el cuerpo del personaje

                // Relleno tenue de la silueta solo cuando esta tapada por una pared.
                float hidden = silhouette * (1.0 - m.g) * m.r * _OccludedFill;

                float pulse = 1.0 + _PulseAmount * sin(_Time.y * _PulseSpeed);

                float3 haloColor = lerp(_AuraColor, _CoreColor, a * a); // nucleo mas blanco en el borde
                float3 col = (haloColor * aura + _AuraColor * hidden) * _AuraIntensity * pulse;
                col *= (1.0 - m.a); // m.a = 1 donde se ve un objeto bloqueador (el jugador): ahi no hay aura

                return float4(col, 1.0);
            }
            ENDHLSL
        }

        // ---------------------------------------------------------------- 3: Blocker
        // Marca (alfa = 1) los pixeles donde SE VE un objeto bloqueador, por ejemplo el jugador.
        // Asi el aura de otros objetos no se dibuja encima de su cuerpo.
        Pass
        {
            Name "Blocker"
            ZTest Always
            ZWrite Off
            Cull Off
            Blend One One
            BlendOp Max

            HLSLPROGRAM
            #pragma vertex BlockVert
            #pragma fragment BlockFrag

            float _DepthBias;

            struct BlockAttributes
            {
                float4 positionOS : POSITION;
            };

            struct BlockVaryings
            {
                float4 positionCS : SV_POSITION;
                float eyeDepth : TEXCOORD0;
            };

            BlockVaryings BlockVert(BlockAttributes v)
            {
                BlockVaryings o;
                float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(positionWS);
                o.eyeDepth = -TransformWorldToView(positionWS).z;
                return o;
            }

            float4 BlockFrag(BlockVaryings i) : SV_Target
            {
                float2 uv = GetNormalizedScreenSpaceUV(i.positionCS);
                float sceneEye = LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);
                float visible = (i.eyeDepth <= sceneEye + _DepthBias) ? 1.0 : 0.0;
                return float4(0.0, 0.0, 0.0, visible);
            }
            ENDHLSL
        }
    }
}

Shader "Hidden/UnderwaterFX/UnderwaterFog"
{
    // Niebla submarina fisica (Beer-Lambert) en dos pases de pantalla completa:
    //  0) Transmittance: multiplica la escena por T = exp(-absorcion * distancia_en_agua).
    //     La absorcion es distinta por canal, asi el rojo se pierde primero y todo vira a azul.
    //  1) InScatter: suma el color de la niebla * (1 - T), oscurecido segun la profundidad de la camara.
    // Solo cuenta el tramo del rayo que queda por debajo de la superficie del agua, asi
    // funciona tanto con la camara dentro como fuera del agua.
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

        float _UWSurfaceY;
        float3 _UWAbsorption;
        float3 _UWFogColor;
        float _UWDepthFalloff;

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

        // Distancia del rayo camara -> pixel que queda bajo la superficie.
        float WaterDistance(float2 uv)
        {
            float rawDepth = SampleSceneDepth(uv);
            #if !UNITY_REVERSED_Z
                rawDepth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, rawDepth);
            #endif

            float3 wp = ComputeWorldSpacePosition(uv, rawDepth, UNITY_MATRIX_I_VP);
            float3 cam = _WorldSpaceCameraPos;
            float dist = distance(wp, cam);

            float d0 = _UWSurfaceY - cam.y; // > 0: el inicio del rayo esta bajo el agua
            float d1 = _UWSurfaceY - wp.y;  // > 0: el final del rayo esta bajo el agua

            float below;
            if (d0 >= 0.0 && d1 >= 0.0) below = 1.0;
            else if (d0 < 0.0 && d1 < 0.0) below = 0.0;
            else
            {
                float t = d0 / (d0 - d1); // fraccion del rayo donde cruza la superficie
                below = (d0 >= 0.0) ? t : 1.0 - t;
            }
            return dist * below;
        }
        ENDHLSL

        Pass
        {
            Name "Transmittance"
            ZTest Always
            ZWrite Off
            Cull Off
            Blend DstColor Zero

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            float4 Frag(Varyings i) : SV_Target
            {
                float waterDist = WaterDistance(i.uv);
                float3 T = exp(-_UWAbsorption * waterDist);
                return float4(T, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "InScatter"
            ZTest Always
            ZWrite Off
            Cull Off
            Blend One One

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            float4 Frag(Varyings i) : SV_Target
            {
                float waterDist = WaterDistance(i.uv);
                float3 T = exp(-_UWAbsorption * waterDist);

                float camDepth = max(0.0, _UWSurfaceY - _WorldSpaceCameraPos.y);
                float darkening = exp(-_UWDepthFalloff * camDepth);

                float3 fog = _UWFogColor * (1.0 - T) * darkening;
                return float4(fog, 1.0);
            }
            ENDHLSL
        }
    }
}

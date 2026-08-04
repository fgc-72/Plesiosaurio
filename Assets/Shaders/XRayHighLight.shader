Shader "Custom/XRayHighlight"
{
    Properties
    {
        _Color ("Color", Color) = (1, 0.6, 0, 1)
        _MaxRange ("Rango Máximo", Float) = 15
        _FadeDistance ("Transición", Float) = 3
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

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionHCS = TransformWorldToHClip(OUT.positionWS);
                return OUT;
            }

            float4 frag(Varyings IN) : SV_Target
            {
                float dist = distance(IN.positionWS, _WorldSpaceCameraPos);

                if (dist > _MaxRange)
                    discard;

                float alpha = 1.0 - saturate((dist - (_MaxRange - _FadeDistance)) / _FadeDistance);

                return float4(_Color.rgb, _Color.a * alpha);
            }
            ENDHLSL
        }
    }
}
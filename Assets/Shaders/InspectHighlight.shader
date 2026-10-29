Shader "Custom/InspectHighlight"
{
    Properties
    {
        [HDR] _HighlightColor ("Highlight Color", Color) = (0.1, 1.6, 2.2, 0.85)
        [HDR] _RimColor ("Rim Color", Color) = (1.2, 2.6, 3.0, 1.0)
        _FresnelPower ("Fresnel Power", Range(0.5, 6.0)) = 2.2
        _ShellPush ("Shell Normal Offset", Range(0.0, 0.1)) = 0.012
        _BaseAlpha ("Fill Alpha", Range(0.0, 1.0)) = 0.22
        _XRayAlpha ("Occluded XRay Alpha", Range(0.0, 1.0)) = 0.35
        _PulseSpeed ("Pulse Speed", Range(0.0, 10.0)) = 3.0
        _PulseAmount ("Pulse Amount", Range(0.0, 0.5)) = 0.2
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent+50"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _HighlightColor;
            float4 _RimColor;
            float _FresnelPower;
            float _ShellPush;
            float _BaseAlpha;
            float _XRayAlpha;
            float _PulseSpeed;
            float _PulseAmount;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS   : NORMAL;
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 normalWS   : TEXCOORD0;
            float3 viewDirWS  : TEXCOORD1;
        };

        Varyings Vert(Attributes v)
        {
            Varyings o;
            float3 normOS = normalize(v.normalOS);
            float3 pushedOS = v.positionOS.xyz + normOS * _ShellPush;
            float3 posWS = TransformObjectToWorld(pushedOS);
            o.positionCS = TransformWorldToHClip(posWS);
            o.normalWS = TransformObjectToWorldNormal(normOS);
            o.viewDirWS = GetWorldSpaceNormalizeViewDir(posWS);
            return o;
        }
        ENDHLSL

        // Pase 0: Silueta X-Ray cuando está detrás de objetos/paredes
        Pass
        {
            Name "InspectXRay"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            ZTest Greater
            ZWrite Off
            Cull Back
            Blend SrcAlpha One

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragXRay

            half4 FragXRay(Varyings i) : SV_Target
            {
                float3 N = normalize(i.normalWS);
                float3 V = normalize(i.viewDirWS);
                float ndotv = saturate( abs(dot(N, V)) );
                float fresnel = pow(1.0 - ndotv, max(0.5, _FresnelPower * 0.75));
                float pulse = 1.0 + _PulseAmount * sin(_Time.y * _PulseSpeed);

                float3 col = lerp(_HighlightColor.rgb, _RimColor.rgb, fresnel) * pulse;
                float alpha = saturate((_XRayAlpha * 0.4 + fresnel * _XRayAlpha) * _HighlightColor.a);
                return half4(col, alpha);
            }
            ENDHLSL
        }

        // Pase 1: Copia brillante externa visible directamente sobre el objeto
        Pass
        {
            Name "InspectShell"
            Tags { "LightMode" = "UniversalForward" }
            ZTest LEqual
            ZWrite Off
            Cull Back
            Blend SrcAlpha One

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragVisible

            half4 FragVisible(Varyings i) : SV_Target
            {
                float3 N = normalize(i.normalWS);
                float3 V = normalize(i.viewDirWS);
                float ndotv = saturate(abs(dot(N, V)));
                float fresnel = pow(1.0 - ndotv, _FresnelPower);
                float pulse = 1.0 + _PulseAmount * sin(_Time.y * _PulseSpeed);

                float3 col = lerp(_HighlightColor.rgb, _RimColor.rgb, fresnel) * pulse;
                float alpha = saturate((_BaseAlpha + fresnel * (1.0 - _BaseAlpha)) * _HighlightColor.a);
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
}

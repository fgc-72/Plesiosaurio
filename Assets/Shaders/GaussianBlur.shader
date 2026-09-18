Shader "Custom/GaussianBlur"
{
    Properties
    {
        _BlitTexture ("Texture", 2D) = "white" {}
        _BlurSize ("Blur Size", Float) = 2
        _Direction ("Direction", Vector) = (1, 0, 0, 0)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        Cull Off ZWrite Off ZTest Always

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        float _BlurSize;
        float4 _Direction;

        Varyings vert(Attributes IN)
        {
            Varyings OUT;
            OUT.positionCS = GetFullScreenTriangleVertexPosition(IN.vertexID);
            OUT.texcoord = GetFullScreenTriangleTexCoord(IN.vertexID);
            return OUT;
        }
        ENDHLSL

        Pass
        {
            Name "Blur"
            Blend Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment fragBlur

            float4 fragBlur(Varyings IN) : SV_Target
            {
                float2 texel = _BlitTexture_TexelSize.xy * _BlurSize;
                float2 dir = _Direction.xy;
                float weights[5] = { 0.227027, 0.1945946, 0.1216216, 0.054054, 0.016216 };

                float4 result = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, IN.texcoord) * weights[0];
                for (int i = 1; i < 5; i++)
                {
                    float2 offset = dir * texel * i;
                    result += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, IN.texcoord + offset) * weights[i];
                    result += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, IN.texcoord - offset) * weights[i];
                }
                return result;
            }
            ENDHLSL
        }

        Pass
        {
            Name "Composite"
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment fragComposite

            float4 fragComposite(Varyings IN) : SV_Target
            {
                return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, IN.texcoord);
            }
            ENDHLSL
        }
    }
}
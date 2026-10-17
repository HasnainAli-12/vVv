Shader "Custom/EmberParticle"
{
    Properties
    {
        _Intensity ("Intensity", Range(0, 4)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
        }

        // additive: embers add light rather than occlude
        Blend SrcAlpha One
        ZWrite Off
        ZTest LEqual
        Cull Off

        Pass
        {
            Name "EmberParticleUnlit"

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex EmberParticleVertex
            #pragma fragment EmberParticleFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float _Intensity;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
            };

            Varyings EmberParticleVertex(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                output.uv = input.uv;
                return output;
            }

            // soft round sprite generated from the quad UV, so no texture asset is needed
            half4 EmberParticleFragment(Varyings input) : SV_Target
            {
                float2 centred = input.uv - 0.5;
                float mask = saturate(1.0 - length(centred) * 2.0);
                mask *= mask;

                float3 rgb = input.color.rgb * input.color.a * mask * _Intensity;
                return half4(rgb, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}

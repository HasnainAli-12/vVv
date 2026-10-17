Shader "Custom/EmberAdditiveMap"
{
    Properties
    {
        _ColorMap ("Colour Map", 2D) = "white" {}
        _AlphaMap ("Opacity Map", 2D) = "white" {}
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        _Intensity ("Intensity", Range(0, 8)) = 1
        _ScrollSpeed ("Scroll (XY)", Vector) = (0, 0, 0, 0)
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

        // additive: flame and smoke add light rather than occlude what is behind them
        Blend SrcAlpha One
        ZWrite Off
        Cull Off

        Pass
        {
            Name "EmberAdditiveForward"

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex AdditiveMapVertex
            #pragma fragment AdditiveMapFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_ColorMap); SAMPLER(sampler_ColorMap);
            TEXTURE2D(_AlphaMap); SAMPLER(sampler_AlphaMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _ColorMap_ST;
                float4 _AlphaMap_ST;
                float4 _Tint;
                float4 _ScrollSpeed;
                float _Intensity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            Varyings AdditiveMapVertex(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 AdditiveMapFragment(Varyings input) : SV_Target
            {
                float2 uv = input.uv + _ScrollSpeed.xy * _Time.y;

                float3 colour = SAMPLE_TEXTURE2D(_ColorMap, sampler_ColorMap, uv).rgb;
                float opacity = SAMPLE_TEXTURE2D(_AlphaMap, sampler_AlphaMap, uv).r;

                float3 rgb = colour * _Tint.rgb * opacity * _Intensity * input.color.rgb;
                return half4(rgb, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}

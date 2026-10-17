Shader "Custom/EmberEdgeGlow"
{
    Properties
    {
        [MainColor] _BaseColor ("Base Colour", Color) = (0.045, 0.04, 0.045, 1)
        [HDR] _EdgeColor ("Edge Colour", Color) = (1.0, 0.32, 0.06, 1)
        [HDR] _CornerColor ("Corner Colour", Color) = (1.0, 0.62, 0.18, 1)
        _EdgeWidth ("Edge Width (world units)", Range(0.001, 0.5)) = 0.035
        _CornerWidth ("Corner Width (world units)", Range(0.001, 0.5)) = 0.06
        _GlowIntensity ("Edge Intensity", Range(0, 30)) = 3.5
        _CornerIntensity ("Corner Intensity", Range(0, 30)) = 6.5
        _ObjectScale ("Object Scale (per material)", Vector) = (1, 1, 1, 0)
        _Smoothness ("Smoothness", Range(0, 1)) = 0.15
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }
        LOD 100

        // ------------------------------------------------------------------ ForwardLit
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex EmberVertex
            #pragma fragment EmberFragment

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _EdgeColor;
                float4 _CornerColor;
                float  _EdgeWidth;
                float  _CornerWidth;
                float  _GlowIntensity;
                float  _CornerIntensity;
                float4 _ObjectScale;
                float  _Smoothness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float3 positionOS  : TEXCOORD0;
                float3 positionWS  : TEXCOORD1;
                float3 normalWS    : TEXCOORD2;
                float4 shadowCoord : TEXCOORD3;
                float  fogCoord    : TEXCOORD4;
            };

            Varyings EmberVertex(Attributes input)
            {
                Varyings output = (Varyings)0;

                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);

                output.positionCS  = positions.positionCS;
                output.positionWS  = positions.positionWS;
                output.positionOS  = input.positionOS.xyz;
                output.normalWS    = TransformObjectToWorldNormal(input.normalOS);
                output.shadowCoord = GetShadowCoord(positions);
                output.fogCoord    = ComputeFogFactor(positions.positionCS.z);

                return output;
            }

            // Distance from a point on a box to its nearest edge / corner.
            //
            // The mesh is Unity's unit cube (+/-0.5) scaled by the transform, so object-space
            // position alone is always +/-0.5 and an edge line drawn from it would stretch with
            // the platform. Multiplying by _ObjectScale (the transform's localScale) converts
            // object space into world-unit offsets from the centre, and _ObjectScale * 0.5 gives
            // the true half extents - so edge thickness stays constant no matter the aspect ratio,
            // and still works for rotated cubes since it is all in the object's own frame.
            void EdgeAndCornerMask(float3 positionOS, out float edge, out float corner)
            {
                float3 scale = max(abs(_ObjectScale.xyz), 1e-4);
                float3 p = positionOS * scale;          // offset from centre, world units
                float3 h = scale * 0.5;                 // half extents, world units
                float3 d = h - abs(p);                  // per-axis distance to each face

                float minD = min(min(d.x, d.y), d.z);
                float maxD = max(max(d.x, d.y), d.z);
                float midD = d.x + d.y + d.z - minD - maxD;

                edge   = 1.0 - smoothstep(0.0, max(1e-4, _EdgeWidth),   midD);
                corner = 1.0 - smoothstep(0.0, max(1e-4, _CornerWidth), maxD);
            }

            half4 EmberFragment(Varyings input) : SV_Target
            {
                float edge, corner;
                EdgeAndCornerMask(input.positionOS, edge, corner);

                float3 emission = _EdgeColor.rgb   * edge   * _GlowIntensity
                                + _CornerColor.rgb * corner * _CornerIntensity;

                InputData lightingInput = (InputData)0;
                lightingInput.positionWS        = input.positionWS;
                lightingInput.normalWS          = normalize(input.normalWS);
                lightingInput.viewDirectionWS   = GetWorldSpaceNormalizeViewDir(input.positionWS);
                lightingInput.shadowCoord       = input.shadowCoord;
                lightingInput.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                lightingInput.bakedGI           = SampleSH(lightingInput.normalWS);
                lightingInput.shadowMask        = half4(1, 1, 1, 1);

                SurfaceData surface = (SurfaceData)0;
                surface.albedo     = _BaseColor.rgb;
                surface.metallic   = 0.0;
                surface.smoothness = _Smoothness;
                surface.occlusion  = 1.0;
                surface.alpha      = 1.0;
                surface.normalTS   = half3(0, 0, 1);
                surface.emission   = emission;

                half4 color = UniversalFragmentPBR(lightingInput, surface);
                color.rgb = MixFog(color.rgb, input.fogCoord);
                return color;
            }
            ENDHLSL
        }

        // ------------------------------------------------------------------ ShadowCaster
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ShadowVertex
            #pragma fragment ShadowFragment
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct ShadowAttributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct ShadowVaryings   { float4 positionCS : SV_POSITION; };

            ShadowVaryings ShadowVertex(ShadowAttributes input)
            {
                ShadowVaryings output;

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS   = TransformObjectToWorldNormal(input.normalOS);

                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif

                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));

                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif

                output.positionCS = positionCS;
                return output;
            }

            half4 ShadowFragment(ShadowVaryings input) : SV_Target { return 0; }
            ENDHLSL
        }

        // ------------------------------------------------------------------ DepthOnly
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthVertex
            #pragma fragment DepthFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct DepthAttributes { float4 positionOS : POSITION; };
            struct DepthVaryings   { float4 positionCS : SV_POSITION; };

            DepthVaryings DepthVertex(DepthAttributes input)
            {
                DepthVaryings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 DepthFragment(DepthVaryings input) : SV_Target { return 0; }
            ENDHLSL
        }
    }

    Fallback Off
}

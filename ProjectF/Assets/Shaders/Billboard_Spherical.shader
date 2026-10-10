Shader "Imposter/URP/Billboard Spherical"
{
    Properties
    {
        [NoScaleOffset] _MainTex("Albedo", 2D) = "white" {}
        _Color("Color", Color) = (1, 1, 1, 1)
        _ColorIntensity("Color Intensity", Range(0, 10)) = 1
        _AlphaClip("Alpha Clip", Range(0, 1)) = 0.5
        _Smoothness("Smoothness", Range(0, 1)) = 0
        [Toggle(_RECEIVE_SHADOWS_OFF)] _ReceiveShadows("Receive Shadows", Float) = 0

        [Header(Wind)]
        [Toggle] _WindEnabled("Enable Wind", Float) = 0
        _WindStrength("Wind Strength", Range(0, 2)) = 0.15
        _WindSpeed("Wind Speed", Range(0, 10)) = 1
        _WindFrequency("Wind Frequency", Range(0, 10)) = 1
        _WindSpatialScale("Wind Spatial Scale", Range(0, 0.1)) = 0.015
        _WindDirection("Wind Direction", Vector) = (1, 0, 0, 0)
        _WindBase("Wind Base", Range(0, 1)) = 0.05

        [HideInInspector] _AlphaCutoff("Alpha Cutoff", Range(0, 1)) = 0.5
        [HideInInspector] _EmissionColor("Emission Color", Color) = (0, 0, 0, 1)
        [HideInInspector] _QueueOffset("_QueueOffset", Float) = 0
        [HideInInspector] _QueueControl("_QueueControl", Float) = -1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "TransparentCutout"
            "Queue" = "AlphaTest"
            "UniversalMaterialType" = "Lit"
        }

        Cull Back
        ZWrite On
        ZTest LEqual

        HLSLINCLUDE
        #pragma target 4.5
        #pragma multi_compile_instancing
        #pragma shader_feature_local _WINDENABLED_ON
        #pragma shader_feature_local _RECEIVE_SHADOWS_OFF
        #pragma multi_compile_fog

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);

        CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            float4 _Color;
            float _ColorIntensity;
            float _AlphaClip;
            float _Smoothness;
            float _WindEnabled;
            float _WindStrength;
            float _WindSpeed;
            float _WindFrequency;
            float _WindSpatialScale;
            float4 _WindDirection;
            float _WindBase;
        CBUFFER_END

        struct BillboardAttributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float4 tangentOS : TANGENT;
            float2 uv : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct BillboardVaryings
        {
            float4 positionCS : SV_POSITION;
            float2 uv : TEXCOORD0;
            float3 positionWS : TEXCOORD1;
            float3 normalWS : TEXCOORD2;
            half fogFactor : TEXCOORD3;
            #if defined(_MAIN_LIGHT_SHADOWS) || defined(_MAIN_LIGHT_SHADOWS_CASCADE) || defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                float4 shadowCoord : TEXCOORD4;
            #endif
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };

        float3 BillboardCenterWS()
        {
            return UNITY_MATRIX_M._m03_m13_m23;
        }

        float3 BillboardRightWS()
        {
            return normalize(float3(UNITY_MATRIX_V._m00, UNITY_MATRIX_V._m01, UNITY_MATRIX_V._m02));
        }

        float3 BillboardUpWS()
        {
            return normalize(float3(UNITY_MATRIX_V._m10, UNITY_MATRIX_V._m11, UNITY_MATRIX_V._m12));
        }

        float3 BillboardForwardWS()
        {
            return normalize(float3(-UNITY_MATRIX_V._m20, -UNITY_MATRIX_V._m21, -UNITY_MATRIX_V._m22));
        }

        float InstancePhase(float3 centerWS)
        {
            return frac(dot(centerWS, float3(0.1031, 0.11369, 0.13787))) * 6.28318530718;
        }

        float3 ApplyWind(float3 positionWS, float3 centerWS, float2 uv)
        {
            #if defined(_WINDENABLED_ON)
                float heightMask = saturate((uv.y - _WindBase) /
                    max(0.001, 1.0 - _WindBase));
                heightMask *= heightMask;

                float phase = InstancePhase(centerWS);
                float spatialPhase = dot(centerWS, _WindDirection.xyz) *
                    _WindSpatialScale;
                float timePhase = _Time.y * _WindSpeed + phase + spatialPhase;
                float primary = sin(timePhase + uv.y * _WindFrequency * 6.2831853);
                float secondary = sin(timePhase * 1.71 + uv.x * 4.0);
                float wave = primary * 0.7 + secondary * 0.3;

                float3 direction = normalize(_WindDirection.xyz +
                    float3(0.0, 0.001, 0.0));
                positionWS += direction * (wave * _WindStrength * heightMask);
                positionWS.y += abs(wave) * _WindStrength * 0.08 * heightMask;
            #endif
            return positionWS;
        }

        float3 BillboardPositionWS(float3 positionOS, float2 uv)
        {
            float3 centerWS = BillboardCenterWS();
            float3 rightWS = BillboardRightWS();
            float3 upWS = BillboardUpWS();
            float3 forwardWS = BillboardForwardWS();

            float3 objectRight = UNITY_MATRIX_M._m00_m10_m20;
            float3 objectUp = UNITY_MATRIX_M._m01_m11_m21;
            float3 objectForward = UNITY_MATRIX_M._m02_m12_m22;
            float3 localOffset = float3(
                positionOS.x * length(objectRight),
                positionOS.y * length(objectUp),
                positionOS.z * length(objectForward));

            float3 positionWS = centerWS +
                rightWS * localOffset.x +
                upWS * localOffset.y +
                forwardWS * localOffset.z;
            return ApplyWind(positionWS, centerWS, uv);
        }

        BillboardVaryings BillboardVertex(BillboardAttributes input)
        {
            BillboardVaryings output = (BillboardVaryings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, output);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

            float3 positionWS = BillboardPositionWS(input.positionOS.xyz, input.uv);
            float3 normalWS = BillboardForwardWS();
            output.positionWS = positionWS;
            output.normalWS = normalWS;
            output.positionCS = TransformWorldToHClip(positionWS);
            output.uv = TRANSFORM_TEX(input.uv, _MainTex);
            output.fogFactor = ComputeFogFactor(output.positionCS.z);

            #if defined(_MAIN_LIGHT_SHADOWS) || defined(_MAIN_LIGHT_SHADOWS_CASCADE) || defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                output.shadowCoord = TransformWorldToShadowCoord(positionWS);
            #endif
            return output;
        }

        half4 SampleBillboard(float2 uv)
        {
            half4 sample = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
            clip(sample.a - _AlphaClip);
            return sample;
        }
        ENDHLSL

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex BillboardVertex
            #pragma fragment BillboardFragment

            half4 BillboardFragment(BillboardVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half4 albedo = SampleBillboard(input.uv);
                half3 normalWS = normalize(input.normalWS);
                half3 ambient = SampleSH(normalWS);
                half3 lighting = ambient;

                #if !defined(_RECEIVE_SHADOWS_OFF)
                    Light mainLight;
                    #if defined(_MAIN_LIGHT_SHADOWS) || defined(_MAIN_LIGHT_SHADOWS_CASCADE) || defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                        mainLight = GetMainLight(input.shadowCoord);
                    #else
                        mainLight = GetMainLight();
                    #endif
                    half diffuse = saturate(dot(normalWS, mainLight.direction));
                    lighting += mainLight.color * (diffuse * mainLight.distanceAttenuation *
                        mainLight.shadowAttenuation);
                #endif

                half3 color = albedo.rgb * _Color.rgb * _ColorIntensity * lighting;
                color = MixFog(color, input.fogFactor);
                return half4(color, albedo.a * _Color.a);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowVertex
            #pragma fragment ShadowFragment
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            float3 _LightDirection;
            float3 _LightPosition;

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            ShadowVaryings ShadowVertex(BillboardAttributes input)
            {
                ShadowVaryings output = (ShadowVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 positionWS = BillboardPositionWS(input.positionOS.xyz, input.uv);
                float3 normalWS = BillboardForwardWS();
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif
                positionWS = ApplyShadowBias(positionWS, normalWS, lightDirectionWS);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }

            half4 ShadowFragment(ShadowVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                SampleBillboard(input.uv);
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex BillboardVertex
            #pragma fragment DepthFragment

            half4 DepthFragment(BillboardVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                SampleBillboard(input.uv);
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On

            HLSLPROGRAM
            #pragma vertex BillboardVertex
            #pragma fragment DepthNormalsFragment

            half4 DepthNormalsFragment(BillboardVaryings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                SampleBillboard(input.uv);
                return half4(TransformWorldToViewDir(normalize(input.normalWS), true) * 0.5 + 0.5, 0);
            }
            ENDHLSL
        }
    }
}

Shader "Custom/ElectricChargeFlow"
{
    Properties
    {
        [Header(Rendering Mode)]
        [Enum(Opaque, 0, Transparent, 1)] _Surface("Surface Type", Float) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend("Source Blend", Float) = 1.0
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("Destination Blend", Float) = 0.0
        [Enum(Off, 0, On, 1)] _ZWrite("ZWrite", Float) = 1.0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull Mode", Float) = 2.0

        [Header(Base Appearance)]
        _BaseMap("Base Map (Optional)", 2D) = "white" {}
        _BaseColor("Base Color", Color) = (0.05, 0.07, 0.1, 1.0)
        _Metallic("Metallic", Range(0.0, 1.0)) = 0.3
        _Smoothness("Smoothness", Range(0.0, 1.0)) = 0.7

        [Header(Coordinate System)]
        [KeywordEnum(ObjectSpace, WorldSpace, UVSpace)] _Coords("Coordinate Space", Float) = 0

        [Header(Electric Current Flow)]
        _FlowDirection("Flow Direction (XYZ)", Vector) = (1.0, 0.0, 0.0, 0.0)
        _FlowSpeed("Flow Speed", Float) = 2.5
        _FlowTiling("Flow Density (Tiling)", Float) = 6.0
        [HDR] _FlowColor("Current / Spark Color", Color) = (0.0, 0.85, 1.0, 1.0)
        _FlowIntensity("Flow Brightness / Glow", Range(0.0, 10.0)) = 3.0
        _FlowPulseSharpness("Pulse Sharpness", Range(1.0, 20.0)) = 5.0
        _NoiseStrength("Electric Jitter Distortion", Range(0.0, 1.0)) = 0.3
        _NoiseScale("Noise Scale", Float) = 8.0
        _NoiseSpeed("Noise Jitter Speed", Float) = 4.0

        [Header(Battery Charge Settings)]
        _ChargeProgress("Charge Progress (0 to 1)", Range(0.0, 1.0)) = 0.65
        _ChargeDirection("Charge Fill Direction (XYZ)", Vector) = (1.0, 0.0, 0.0, 0.0)
        _ChargeAxisMin("Charge Bounds Min (Object/World)", Float) = -0.5
        _ChargeAxisMax("Charge Bounds Max (Object/World)", Float) = 0.5
        [HDR] _ChargedColor("Charged Background Glow", Color) = (0.0, 0.9, 0.45, 1.0)
        _ChargedGlowIntensity("Charged Color Intensity", Range(0.0, 5.0)) = 1.2
        _UnchargedColor("Uncharged / Empty Color", Color) = (0.02, 0.03, 0.05, 1.0)
        _FlowInUncharged("Flow Visibility in Uncharged Area", Range(0.0, 1.0)) = 0.05

        [Header(Charge Leading Edge Spark)]
        _ChargeEdgeWidth("Leading Edge Width", Range(0.001, 0.2)) = 0.035
        [HDR] _ChargeEdgeColor("Leading Edge Spark Color", Color) = (1.0, 0.95, 0.3, 1.0)
        _ChargeEdgeIntensity("Leading Edge Spark Glow", Range(0.0, 15.0)) = 5.0
        _ChargeEdgePulseSpeed("Edge Pulse Flicker Speed", Float) = 8.0

        [Header(Rim Fresnel Glow)]
        [Toggle(_USE_RIM)] _UseRim("Enable Edge Rim Glow", Float) = 1.0
        [HDR] _RimColor("Rim Glow Color", Color) = (0.1, 0.6, 1.0, 1.0)
        _RimPower("Rim Power", Range(0.5, 10.0)) = 3.0
        _RimIntensity("Rim Intensity", Range(0.0, 5.0)) = 1.5
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            float4 _BaseColor;
            float4 _FlowColor;
            float4 _ChargedColor;
            float4 _UnchargedColor;
            float4 _ChargeEdgeColor;
            float4 _RimColor;
            float4 _FlowDirection;
            float4 _ChargeDirection;

            float _Surface;
            float _SrcBlend;
            float _DstBlend;
            float _ZWrite;
            float _Cull;

            float _Metallic;
            float _Smoothness;
            float _Coords;

            float _FlowSpeed;
            float _FlowTiling;
            float _FlowIntensity;
            float _FlowPulseSharpness;
            float _NoiseStrength;
            float _NoiseScale;
            float _NoiseSpeed;

            float _ChargeProgress;
            float _ChargeAxisMin;
            float _ChargeAxisMax;
            float _ChargedGlowIntensity;
            float _FlowInUncharged;

            float _ChargeEdgeWidth;
            float _ChargeEdgeIntensity;
            float _ChargeEdgePulseSpeed;

            float _UseRim;
            float _RimPower;
            float _RimIntensity;
        CBUFFER_END

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        // Helper: Simple procedural electrical hash/noise
        float Hash11(float p)
        {
            p = frac(p * 0.1031);
            p *= p + 33.33;
            p *= p + p;
            return frac(p);
        }

        float Noise1D(float x)
        {
            float i = floor(x);
            float f = frac(x);
            float u = f * f * (3.0 - 2.0 * f);
            return lerp(Hash11(i), Hash11(i + 1.0), u);
        }

        // Multi-frequency electric perturbation
        float ElectricJitter(float3 p, float t)
        {
            float n1 = sin(p.y * _NoiseScale + t * _NoiseSpeed) * cos(p.z * _NoiseScale - t * _NoiseSpeed * 0.7);
            float n2 = sin(p.x * (_NoiseScale * 1.7) - t * (_NoiseSpeed * 1.3)) * 0.5;
            float n3 = (Noise1D(p.x * _NoiseScale * 2.0 + t * _NoiseSpeed * 2.5) - 0.5) * 0.8;
            return (n1 + n2 + n3);
        }

        ENDHLSL

        // ------------------------------------------------------------------
        // Pass 1: Forward Lit (EMS Electric Charge Flow)
        // ------------------------------------------------------------------
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull [_Cull]
            ZWrite [_ZWrite]
            ZTest LEqual
            Blend [_SrcBlend] [_DstBlend]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma shader_feature_local _COORDS_OBJECTSPACE _COORDS_WORLDSPACE _COORDS_UVSPACE
            #pragma shader_feature_local _USE_RIM
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 positionOS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float2 uv : TEXCOORD3;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);

                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.positionOS = input.positionOS.xyz;
                output.normalWS = normalInput.normalWS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);

                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                // 1. Determine Coordinate Reference (Object, World, or UV)
                float3 coordPos = input.positionOS;
                #if defined(_COORDS_WORLDSPACE)
                    coordPos = input.positionWS;
                #elif defined(_COORDS_UVSPACE)
                    coordPos = float3(input.uv.x, input.uv.y, 0.0);
                #else
                    if (_Coords > 1.5) // UV
                        coordPos = float3(input.uv.x, input.uv.y, 0.0);
                    else if (_Coords > 0.5) // World
                        coordPos = input.positionWS;
                    else // Object
                        coordPos = input.positionOS;
                #endif

                // 2. Charge Normalized Position calculation
                float3 chargeDir = _ChargeDirection.xyz;
                float chargeLenSq = dot(chargeDir, chargeDir);
                if (chargeLenSq < 0.0001)
                    chargeDir = float3(1.0, 0.0, 0.0);
                else
                    chargeDir = normalize(chargeDir);

                float chargeCoord = dot(coordPos, chargeDir);
                float boundsMin = _ChargeAxisMin;
                float boundsMax = _ChargeAxisMax;
                if (_Coords > 1.5) // In UV space, default bounds are 0 to 1
                {
                    boundsMin = 0.0;
                    boundsMax = 1.0;
                }
                float boundsRange = max(0.0001, boundsMax - boundsMin);
                float chargeNormPos = saturate((chargeCoord - boundsMin) / boundsRange);

                // Charge State Mask (1 = Charged, 0 = Uncharged)
                // Use smooth transition at wavefront
                float chargeDelta = _ChargeProgress - chargeNormPos;
                float chargeMask = smoothstep(-0.005, 0.005, chargeDelta);

                // Leading Edge Glow (Peak energy spark right at the charging boundary)
                float edgeDist = abs(chargeDelta);
                float edgeShape = smoothstep(_ChargeEdgeWidth, 0.0, edgeDist);
                float edgePulse = 1.0 + 0.35 * sin(_Time.y * _ChargeEdgePulseSpeed) + 0.15 * cos(_Time.y * (_ChargeEdgePulseSpeed * 1.7));
                float edgeGlowFactor = edgeShape * edgePulse;
                // Only show edge if progress is between 0 and 1
                edgeGlowFactor *= smoothstep(0.0, 0.02, _ChargeProgress) * smoothstep(1.0, 0.98, _ChargeProgress);

                // 3. Current Flow Calculation (+X axis or custom direction)
                float3 flowDir = _FlowDirection.xyz;
                float flowLenSq = dot(flowDir, flowDir);
                if (flowLenSq < 0.0001)
                    flowDir = float3(1.0, 0.0, 0.0);
                else
                    flowDir = normalize(flowDir);

                float flowCoord = dot(coordPos, flowDir);

                // Electrical jitter noise
                float jitter = ElectricJitter(coordPos, _Time.y) * _NoiseStrength;
                float animatedFlow = (flowCoord + jitter) * _FlowTiling - (_Time.y * _FlowSpeed);

                // Flow wave patterns:
                // Primary wave pulse
                float wave1 = frac(animatedFlow);
                float pulse1 = pow(saturate(sin(wave1 * 3.1415926)), _FlowPulseSharpness);

                // Secondary harmonics for electric richness (faster, smaller pulses)
                float wave2 = frac(animatedFlow * 2.3 + 0.4);
                float pulse2 = pow(saturate(sin(wave2 * 3.1415926)), _FlowPulseSharpness * 1.5) * 0.45;

                // Micro spark pulses
                float sparkPulse = pow(saturate(sin((animatedFlow * 5.0) * 3.1415926)), 12.0) * 0.3;

                float totalFlowPulse = pulse1 + pulse2 + sparkPulse;

                // Mask flow by charging state (flowing brightly in charged region, optional faint flow in uncharged)
                float flowVisibility = lerp(_FlowInUncharged, 1.0, chargeMask);
                float3 flowEmission = _FlowColor.rgb * (totalFlowPulse * _FlowIntensity * flowVisibility);

                // 4. Base Surface Color & Charge Background
                float4 texColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                float3 baseAlbedo = texColor.rgb * _BaseColor.rgb;

                // Charged background color vs uncharged dark color
                float3 chargedBg = _ChargedColor.rgb * _ChargedGlowIntensity;
                float3 unchargedBg = _UnchargedColor.rgb;
                float3 surfaceStateColor = lerp(unchargedBg, chargedBg, chargeMask);

                // Lighting
                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = normalize(GetCameraPositionWS() - input.positionWS);

                Light mainLight = GetMainLight();
                float NdotL = saturate(dot(normalWS, mainLight.direction));
                float3 directDiffuse = mainLight.color * (NdotL * mainLight.shadowAttenuation * mainLight.distanceAttenuation);

                #if defined(_ADDITIONAL_LIGHTS)
                uint pixelLightCount = GetAdditionalLightsCount();
                for (uint lightIndex = 0u; lightIndex < pixelLightCount; ++lightIndex)
                {
                    Light addLight = GetAdditionalLight(lightIndex, input.positionWS);
                    float addNdotL = saturate(dot(normalWS, addLight.direction));
                    directDiffuse += addLight.color * (addNdotL * addLight.shadowAttenuation * addLight.distanceAttenuation);
                }
                #endif

                float3 ambient = SampleSH(normalWS);
                float3 totalLight = max(directDiffuse + ambient, float3(0.2, 0.2, 0.2));

                // Combine diffuse lighting with base & charged surface
                float3 diffuseColor = (baseAlbedo + surfaceStateColor * 0.5) * totalLight;

                // Leading edge spark emission
                float3 edgeEmission = _ChargeEdgeColor.rgb * (edgeGlowFactor * _ChargeEdgeIntensity);

                // Rim Fresnel Glow
                float3 rimEmission = float3(0.0, 0.0, 0.0);
                #if defined(_USE_RIM)
                if (_UseRim > 0.5)
                {
                    float NdotV = saturate(dot(normalWS, viewDirWS));
                    float rim = pow(1.0 - NdotV, _RimPower) * _RimIntensity;
                    // Rim glows with flow color or charged color depending on chargeMask
                    float3 dynamicRimCol = lerp(_RimColor.rgb * 0.5, _RimColor.rgb, chargeMask);
                    rimEmission = dynamicRimCol * rim;
                }
                #endif

                // Total Composite Color
                float3 finalRgb = diffuseColor + flowEmission + edgeEmission + rimEmission;

                float alpha = _BaseColor.a;
                if (_Surface > 0.5)
                {
                    alpha = saturate(_BaseColor.a * lerp(0.4, 1.0, chargeMask) + edgeGlowFactor);
                }

                return float4(finalRgb, alpha);
            }
            ENDHLSL
        }

        // ------------------------------------------------------------------
        // Pass 2: Shadow Caster
        // ------------------------------------------------------------------
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings ShadowPassVertex(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _MainLightPosition.xyz));
                return output;
            }

            float4 ShadowPassFragment(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        // ------------------------------------------------------------------
        // Pass 3: Depth Only
        // ------------------------------------------------------------------
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings DepthOnlyVertex(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            float4 DepthOnlyFragment(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
    CustomEditor "ElectricChargeFlowGUI"
}

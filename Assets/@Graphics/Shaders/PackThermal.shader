Shader "Custom/PackThermal"
{
    Properties
    {
        [Header(Surface Options)]
        [Enum(Opaque, 0, Transparent, 1)] _Surface("Surface Type", Float) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend("Source Blend", Float) = 1.0
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("Destination Blend", Float) = 0.0
        [Enum(Off, 0, On, 1)] _ZWrite("ZWrite", Float) = 1.0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull Mode", Float) = 2.0

        [Header(Depth Offset)]
        _DepthOffsetFactor("Depth Offset Factor", Range(-5.0, 5.0)) = 0.0
        _DepthOffsetUnits("Depth Offset Units", Range(-5.0, 5.0)) = 0.0

        [Header(Surface Properties)]
        _BaseMap("Base Map (Albedo)", 2D) = "white" {}
        _BaseColor("Base Color", Color) = (1.0, 1.0, 1.0, 1.0)
        _Metallic("Metallic", Range(0.0, 1.0)) = 0.0
        _Smoothness("Smoothness", Range(0.0, 1.0)) = 0.5

        [Header(Outline Settings)]
        [HDR] _OutlineColor("Outline Color", Color) = (1.0, 0.4, 0.0, 1.0)
        _OutlineWidth("Outline Width", Range(0.0, 0.3)) = 0.04
        [KeywordEnum(PositionExtrusion, NormalExtrusion, ScreenSpaceNormal)] _OutlineMode("Outline Mode", Float) = 0
        [Toggle] _ScreenSpaceOutline("Constant Screen-Space Width", Float) = 0.0
        _CenterOffset("Center Offset (Position Mode)", Vector) = (0.0, 0.0, 0.0, 0.0)
        _OutlineZOffset("Outline Depth Offset", Range(-0.05, 0.05)) = 0.001

        [Header(Inner Rim Outline Settings)]
        [Toggle(_USE_RIM)] _UseRim("Enable Inner Rim Outline", Float) = 0.0
        [HDR] _RimColor("Rim Color", Color) = (1.0, 1.0, 1.0, 1.0)
        _RimPower("Rim Power", Range(0.5, 10.0)) = 3.0
        _RimIntensity("Rim Intensity", Range(0.0, 5.0)) = 1.0

        [Header(Thermal)]
        // 0이면 원래 모습, 1이면 완전한 열화상 화면. 모드 전환 연출에 쓴다.
        _ThermalAmount("Thermal Amount", Range(0.0, 1.0)) = 0.0
        // 열원 개수. 위치/반경/세기 배열(_HeatPoints, _HeatParams)은 MaterialPropertyBlock으로 전달한다.
        _HeatCount("Heat Source Count", Range(0, 8)) = 0
        // 열이 전혀 없는 부분의 바탕색(열화상의 차가운 색).
        _ThermalColdColor("Thermal Cold Color", Color) = (0.05, 0.02, 0.35, 1.0)
        // 열화상 전환 시에도 원래 조명 음영을 얼마나 남길지(0=음영 없음, 1=원래 음영 그대로).
        _ThermalShading("Thermal Shading Keep", Range(0.0, 1.0)) = 0.4
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

        #define HEAT_MAX 8

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            float4 _BaseColor;
            float4 _OutlineColor;
            float4 _CenterOffset;
            float4 _RimColor;
            float4 _ThermalColdColor;
            float _Surface;
            float _SrcBlend;
            float _DstBlend;
            float _ZWrite;
            float _Cull;
            float _DepthOffsetFactor;
            float _DepthOffsetUnits;
            float _Metallic;
            float _Smoothness;
            float _OutlineWidth;
            float _OutlineMode;
            float _ScreenSpaceOutline;
            float _OutlineZOffset;
            float _UseRim;
            float _RimPower;
            float _RimIntensity;
            float _ThermalAmount;
            float _HeatCount;
            float _ThermalShading;
        CBUFFER_END

        // 열원 배열: Properties 블록에 선언할 수 없어 CBUFFER 밖에 둔다.
        // _HeatPoints[i] = (월드 좌표 xyz, 반경 w), _HeatParams[i] = (세기 x, 예비 yzw)
        float4 _HeatPoints[HEAT_MAX];
        float4 _HeatParams[HEAT_MAX];

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);
        ENDHLSL

        // ------------------------------------------------------------------
        // Pass 1: Forward Lit (Base object rendering + thermal)
        // ------------------------------------------------------------------
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull [_Cull]
            ZWrite [_ZWrite]
            ZTest LEqual
            Offset [_DepthOffsetFactor], [_DepthOffsetUnits]
            Blend [_SrcBlend] [_DstBlend]

            Stencil
            {
                Ref 1
                WriteMask 255
                ReadMask 255
                Comp Always
                Pass Replace
            }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _USE_RIM
            #pragma shader_feature_local _SURFACE_TYPE_TRANSPARENT
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

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
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
            };

            // 열원 전체의 열 세기(0~1). 열원마다 중심에서 반경 끝까지 부드럽게 줄어든다.
            float ComputeHeat(float3 positionWS)
            {
                float heat = 0.0;
                int count = min((int)round(_HeatCount), HEAT_MAX);

                [loop]
                for (int i = 0; i < count; ++i)
                {
                    float radius = max(_HeatPoints[i].w, 0.0001);
                    float d = distance(positionWS, _HeatPoints[i].xyz);
                    float f = 1.0 - saturate(d / radius);
                    f = f * f * (3.0 - 2.0 * f);
                    heat += f * _HeatParams[i].x;
                }

                return saturate(heat);
            }

            // 열 세기 -> 열화상 팔레트 (보라 -> 빨강 -> 주황 -> 노랑).
            float3 HeatGradient(float t)
            {
                const float3 c0 = float3(0.20, 0.00, 0.45);
                const float3 c1 = float3(0.75, 0.05, 0.20);
                const float3 c2 = float3(1.00, 0.15, 0.00);
                const float3 c3 = float3(1.00, 0.60, 0.00);
                const float3 c4 = float3(1.00, 1.00, 0.35);

                float3 color = lerp(c0, c1, saturate(t * 4.0));
                color = lerp(color, c2, saturate(t * 4.0 - 1.0));
                color = lerp(color, c3, saturate(t * 4.0 - 2.0));
                color = lerp(color, c4, saturate(t * 4.0 - 3.0));
                return color;
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);

                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.normalWS = normalInput.normalWS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);

                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                float4 texColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                float4 albedo = texColor * _BaseColor;

                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = normalize(GetCameraPositionWS() - input.positionWS);

                // Main directional light
                Light mainLight = GetMainLight();
                float NdotL = saturate(dot(normalWS, mainLight.direction));
                float3 directDiffuse = mainLight.color * (NdotL * mainLight.shadowAttenuation * mainLight.distanceAttenuation);

                // Additional lights
                #if defined(_ADDITIONAL_LIGHTS)
                uint pixelLightCount = GetAdditionalLightsCount();
                for (uint lightIndex = 0u; lightIndex < pixelLightCount; ++lightIndex)
                {
                    Light addLight = GetAdditionalLight(lightIndex, input.positionWS);
                    float addNdotL = saturate(dot(normalWS, addLight.direction));
                    directDiffuse += addLight.color * (addNdotL * addLight.shadowAttenuation * addLight.distanceAttenuation);
                }
                #endif

                // Ambient spherical harmonics
                float3 ambient = SampleSH(normalWS);

                // Total lighting with subtle ambient floor so objects are visible even without lights
                float3 totalLight = directDiffuse + ambient;
                totalLight = max(totalLight, float3(0.25, 0.25, 0.25));

                float3 finalColor = albedo.rgb * totalLight;

                // Optional Inner Rim Outline (Fresnel effect)
                #if defined(_USE_RIM)
                bool applyRim = true;
                #else
                bool applyRim = (_UseRim > 0.5);
                #endif

                if (applyRim)
                {
                    float NdotV = saturate(dot(normalWS, viewDirWS));
                    float rim = pow(1.0 - NdotV, _RimPower) * _RimIntensity;
                    finalColor += _RimColor.rgb * rim;
                }

                // Thermal: 열이 없는 곳은 차가운 색, 열이 있는 곳은 팔레트를 따라 붉게.
                if (_ThermalAmount > 0.0)
                {
                    float heat = ComputeHeat(input.positionWS);

                    float luminance = dot(finalColor, float3(0.299, 0.587, 0.114));
                    float shade = lerp(1.0, saturate(luminance * 2.0), _ThermalShading);

                    float3 coldColor = _ThermalColdColor.rgb;
                    float3 hotColor = HeatGradient(heat);
                    float3 thermalColor = lerp(coldColor, hotColor, smoothstep(0.0, 0.2, heat)) * shade;

                    finalColor = lerp(finalColor, thermalColor, _ThermalAmount);
                    // 열화상일 때는 반투명 Pack도 불투명하게 보여 열 분포가 또렷하게 읽히도록 한다.
                    albedo.a = lerp(albedo.a, 1.0, _ThermalAmount);
                }

                return float4(finalColor, albedo.a);
            }
            ENDHLSL
        }

        // ------------------------------------------------------------------
        // Pass 2: Outline (Inverted Hull pass with flexible extrusion)
        // ------------------------------------------------------------------
        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "UniversalForwardOnly" }

            Cull Front
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

            Stencil
            {
                Ref 1
                WriteMask 255
                ReadMask 255
                Comp NotEqual
            }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _OUTLINEMODE_POSITIONEXTRUSION _OUTLINEMODE_NORMALEXTRUSION _OUTLINEMODE_SCREENSPACENORMAL

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;

                int activeMode = 0;
                #if defined(_OUTLINEMODE_NORMALEXTRUSION)
                    activeMode = 1;
                #elif defined(_OUTLINEMODE_SCREENSPACENORMAL)
                    activeMode = 2;
                #elif defined(_OUTLINEMODE_POSITIONEXTRUSION)
                    activeMode = 0;
                #else
                    activeMode = (int)round(_OutlineMode);
                #endif

                if (activeMode == 1)
                {
                    // Normal Extrusion: Ideal for smooth, curved, or organic meshes (Spheres, Characters)
                    float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                    float3 posWS = TransformObjectToWorld(input.positionOS.xyz);
                    float width = _OutlineWidth;
                    if (_ScreenSpaceOutline > 0.5)
                    {
                        float4 tempCS = TransformWorldToHClip(posWS);
                        width *= tempCS.w * 0.02;
                    }
                    posWS += normalize(normalWS) * width;
                    output.positionCS = TransformWorldToHClip(posWS);
                }
                else if (activeMode == 2)
                {
                    // Screen-Space Normal Extrusion: Constant pixel width via projection offset
                    float4 posCS = TransformObjectToHClip(input.positionOS.xyz);
                    float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                    float3 normalCS = TransformWorldToHClipDir(normalWS);
                    float2 offset = normalize(normalCS.xy);
                    offset.x *= _ScreenParams.y / _ScreenParams.x;
                    posCS.xy += offset * (_OutlineWidth * 0.1 * posCS.w);
                    output.positionCS = posCS;
                }
                else
                {
                    // Mode 0 (Default): Position / Centroid Extrusion
                    // Extrudes radially from object center in world space.
                    // Guarantees all shared vertices at hard edges (cubes, cylinders, polyhedra) move together.
                    float3 posWS = TransformObjectToWorld(input.positionOS.xyz);
                    float3 centerWS = TransformObjectToWorld(_CenterOffset.xyz);
                    float3 dirWS = posWS - centerWS;
                    float len = length(dirWS);
                    float3 normDirWS = len > 0.0001 ? (dirWS / len) : float3(0.0, 1.0, 0.0);

                    float width = _OutlineWidth;
                    if (_ScreenSpaceOutline > 0.5)
                    {
                        float4 tempCS = TransformWorldToHClip(posWS);
                        width *= tempCS.w * 0.02;
                    }

                    posWS += normDirWS * width;
                    output.positionCS = TransformWorldToHClip(posWS);
                }

                // Depth bias / offset to prevent z-fighting
                #if UNITY_REVERSED_Z
                    output.positionCS.z -= _OutlineZOffset * output.positionCS.w;
                #else
                    output.positionCS.z += _OutlineZOffset * output.positionCS.w;
                #endif

                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                return _OutlineColor;
            }
            ENDHLSL
        }

        // ------------------------------------------------------------------
        // Pass 3: ShadowCaster
        // ------------------------------------------------------------------
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

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

            float3 _LightDirection;

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

                output.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));
                #if UNITY_REVERSED_Z
                    output.positionCS.z = min(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    output.positionCS.z = max(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        // ------------------------------------------------------------------
        // Pass 4: DepthOnly
        // ------------------------------------------------------------------
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        // ------------------------------------------------------------------
        // Pass 5: DepthNormals
        // ------------------------------------------------------------------
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return half4(NormalizeNormalPerPixel(input.normalWS), 0.0);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}

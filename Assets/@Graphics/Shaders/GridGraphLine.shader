// DeviceGridGraphManager 전용 선 셰이더.
// 모든 선을 메시 하나로 합쳐 그리고, 선 굵기는 셰이더가 화면 픽셀 기준으로 계산한다(줌이 바뀌어도 메시를 다시 만들지 않는다).
// 각 선은 정점 4개(양 끝점 x 양쪽 가장자리)로 이루어진다.
//   POSITION  : 이 정점이 속한 끝점(오브젝트 공간)
//   TEXCOORD0 : xyz = 선의 반대쪽 끝점, w = 가장자리 방향(+1 / -1)
//   TEXCOORD1 : x = 굵기(_WidthScale을 곱해 픽셀로 변환), y = 끝점 방향 부호(시작점 +1, 끝점 -1)
//   COLOR     : 선 색
// 가장자리는 픽셀 거리 기반으로 부드럽게 처리해 MSAA 없이도 가는 선이 들쭉날쭉해지지 않게 한다.
Shader "Incheol/GridGraphLine"
{
    Properties
    {
        [HideInInspector] _WidthScale ("Width Scale", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "GridGraphLine"

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
                float4 other : TEXCOORD0;
                float2 widthInfo : TEXCOORD1;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
                float2 edge : TEXCOORD0; // x = 중심선에서 가장자리까지의 부호 있는 픽셀 거리, y = 절반 굵기(픽셀)
            };

            CBUFFER_START(UnityPerMaterial)
                float _WidthScale;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;

                float4 clipSelf = TransformObjectToHClip(input.positionOS);
                float4 clipOther = TransformObjectToHClip(input.other.xyz);

                float2 halfScreen = _ScreenParams.xy * 0.5;
                float2 screenSelf = clipSelf.xy / max(abs(clipSelf.w), 1e-5) * halfScreen;
                float2 screenOther = clipOther.xy / max(abs(clipOther.w), 1e-5) * halfScreen;

                // 선 방향(화면 픽셀 공간)과 그 수직 방향. 반대쪽 끝점에서 보면 방향이 뒤집히므로 widthInfo.y로 보정한다.
                float2 dir = screenOther - screenSelf;
                float len = length(dir);
                dir = len > 1e-4 ? dir / len : float2(1.0, 0.0);
                float2 normal = float2(-dir.y, dir.x);

                float halfWidth = input.widthInfo.x * _WidthScale * 0.5;
                // 가장자리 부드러움(안티에일리어싱)을 위해 양쪽으로 1픽셀씩 더 키운다.
                float extent = halfWidth + 1.0;
                float signedSide = input.other.w * input.widthInfo.y;
                float2 offsetPx = normal * signedSide * extent;

                clipSelf.xy += offsetPx / halfScreen * clipSelf.w;

                output.positionCS = clipSelf;
                output.color = input.color;
                output.edge = float2(input.other.w * extent, halfWidth);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // 중심선에서의 거리가 절반 굵기 안쪽이면 1, 바깥쪽 1픽셀 구간에서 0으로 줄어든다.
                float coverage = saturate(input.edge.y + 0.5 - abs(input.edge.x));
                return half4(input.color.rgb, input.color.a * coverage);
            }
            ENDHLSL
        }
    }

    Fallback Off
}

using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public class ElectricChargeFlowGUI : ShaderGUI
{
    public enum SurfaceType
    {
        Opaque = 0,
        Transparent = 1
    }

    private static bool _showTransparencyGroup = true;
    private static bool _showChargeGroup = true;
    private static bool _showFlowGroup = true;
    private static bool _showCoordsGroup = true;
    private static bool _showBaseGroup = true;
    private static bool _showRimGroup = true;

    public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
    {
        Material material = materialEditor.target as Material;
        if (material == null) return;

        EditorGUI.BeginChangeCheck();

        // 1. Transparency & Interior Visibility Group
        _showTransparencyGroup = EditorGUILayout.BeginFoldoutHeaderGroup(_showTransparencyGroup, "🔮 투명도 및 내부 투과 설정 (Transparency & Interior)");
        if (_showTransparencyGroup)
        {
            EditorGUILayout.HelpBox("오브젝트 내부(배터리 셀, 모듈 등)가 비쳐보이도록 투명도와 블렌딩을 제어합니다.", MessageType.Info);

            MaterialProperty surfaceProp = FindProperty("_Surface", properties);
            SurfaceType currentSurface = (SurfaceType)(int)surfaceProp.floatValue;
            SurfaceType newSurface = (SurfaceType)EditorGUILayout.EnumPopup("표면 모드 (Surface Type)", currentSurface);
            if (newSurface != currentSurface)
            {
                surfaceProp.floatValue = (float)newSurface;
                ApplySurfaceType(material, newSurface);
            }

            if (newSurface == SurfaceType.Transparent)
            {
                MaterialProperty baseAlpha = FindProperty("_BaseAlpha", properties);
                MaterialProperty unchargedAlpha = FindProperty("_UnchargedAlpha", properties);
                MaterialProperty chargedAlpha = FindProperty("_ChargedAlpha", properties);
                MaterialProperty fresnelAlpha = FindProperty("_FresnelAlpha", properties);
                MaterialProperty sparkAlpha = FindProperty("_SparkAlpha", properties);

                materialEditor.ShaderProperty(baseAlpha, "기본 표면 불투명도 (Base Opacity)");
                materialEditor.ShaderProperty(unchargedAlpha, "미충전 영역 불투명도 (낮을수록 내부가 훤히 투과)");
                materialEditor.ShaderProperty(chargedAlpha, "충전 완료 영역 불투명도 (에너지 참)");
                materialEditor.ShaderProperty(fresnelAlpha, "외곽 실루엣 투명도 (Fresnel Silhouette)");
                materialEditor.ShaderProperty(sparkAlpha, "전류/스파크 투명도 부스트 (Glow Boost)");

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("블렌딩 프리셋 (Blend Presets)", EditorStyles.miniBoldLabel);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("일반 알파 투명 (Alpha Blend)", EditorStyles.miniButtonLeft))
                {
                    material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                    material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                }
                if (GUILayout.Button("네온 가산 투명 (Additive)", EditorStyles.miniButtonMid))
                {
                    material.SetFloat("_SrcBlend", (float)BlendMode.One);
                    material.SetFloat("_DstBlend", (float)BlendMode.One);
                }
                if (GUILayout.Button("프리멀티플라이 (Premultiply)", EditorStyles.miniButtonRight))
                {
                    material.SetFloat("_SrcBlend", (float)BlendMode.One);
                    material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(2);
                MaterialProperty cullProp = FindProperty("_Cull", properties);
                materialEditor.ShaderProperty(cullProp, "컬링 모드 (Back: 앞면만 / Off: 양면 투과)");

                MaterialProperty zwriteProp = FindProperty("_ZWrite", properties);
                materialEditor.ShaderProperty(zwriteProp, "ZWrite (깊이 버퍼 쓰기)");
            }
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.Space(8);

        // 2. Battery Charge Status Group
        _showChargeGroup = EditorGUILayout.BeginFoldoutHeaderGroup(_showChargeGroup, "🔋 배터리 충전 상태 (Charge Status)");
        if (_showChargeGroup)
        {
            EditorGUILayout.HelpBox("충전 진행도(0~1)와 충전이 채워지는 축 방향을 설정합니다.", MessageType.Info);

            MaterialProperty chargeProgress = FindProperty("_ChargeProgress", properties);
            materialEditor.ShaderProperty(chargeProgress, "충전 진행도 (0% ~ 100%)");

            MaterialProperty chargeDir = FindProperty("_ChargeDirection", properties);
            materialEditor.ShaderProperty(chargeDir, "충전 방향 (Charge Direction)");

            // Preset Buttons for Charge Direction
            DrawDirectionPresets(chargeDir);

            MaterialProperty boundsMin = FindProperty("_ChargeAxisMin", properties);
            MaterialProperty boundsMax = FindProperty("_ChargeAxisMax", properties);
            EditorGUILayout.BeginHorizontal();
            materialEditor.ShaderProperty(boundsMin, "충전 시작점 (Min)");
            materialEditor.ShaderProperty(boundsMax, "충전 끝점 (Max)");
            EditorGUILayout.EndHorizontal();

            MaterialProperty chargedColor = FindProperty("_ChargedColor", properties);
            MaterialProperty chargedIntensity = FindProperty("_ChargedGlowIntensity", properties);
            MaterialProperty unchargedColor = FindProperty("_UnchargedColor", properties);
            MaterialProperty flowInUncharged = FindProperty("_FlowInUncharged", properties);

            materialEditor.ShaderProperty(chargedColor, "충전 완료 영역 색상 (HDR)");
            materialEditor.ShaderProperty(chargedIntensity, "충전 영역 발광 강도");
            materialEditor.ShaderProperty(unchargedColor, "미충전 / 빈 공간 색상");
            materialEditor.ShaderProperty(flowInUncharged, "미충전 영역 전류 잔여율");

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("⚡ 충전 선단 스파크 (Leading Edge Glow)", EditorStyles.miniBoldLabel);
            MaterialProperty edgeColor = FindProperty("_ChargeEdgeColor", properties);
            MaterialProperty edgeWidth = FindProperty("_ChargeEdgeWidth", properties);
            MaterialProperty edgeIntensity = FindProperty("_ChargeEdgeIntensity", properties);
            MaterialProperty edgePulseSpeed = FindProperty("_ChargeEdgePulseSpeed", properties);

            materialEditor.ShaderProperty(edgeColor, "스파크 엣지 색상 (HDR)");
            materialEditor.ShaderProperty(edgeWidth, "스파크 엣지 두께");
            materialEditor.ShaderProperty(edgeIntensity, "스파크 엣지 발광 강도");
            materialEditor.ShaderProperty(edgePulseSpeed, "스파크 깜빡임 속도");
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.Space(8);

        // 3. Electric Current Flow Group
        _showFlowGroup = EditorGUILayout.BeginFoldoutHeaderGroup(_showFlowGroup, "⚡ 전류 흐름 설정 (Electric Flow)");
        if (_showFlowGroup)
        {
            EditorGUILayout.HelpBox("전류가 이동하는 방향과 속도, 색상, 노이즈 왜곡을 설정합니다. 기본값은 +X축입니다.", MessageType.Info);

            MaterialProperty flowDir = FindProperty("_FlowDirection", properties);
            materialEditor.ShaderProperty(flowDir, "흐름 방향 (Flow Direction)");

            // Preset Buttons for Flow Direction
            DrawDirectionPresets(flowDir);

            MaterialProperty flowSpeed = FindProperty("_FlowSpeed", properties);
            MaterialProperty flowTiling = FindProperty("_FlowTiling", properties);
            materialEditor.ShaderProperty(flowSpeed, "흐름 속도 (Flow Speed)");
            materialEditor.ShaderProperty(flowTiling, "흐름 밀도 / 주기 (Flow Tiling)");

            MaterialProperty flowColor = FindProperty("_FlowColor", properties);
            MaterialProperty flowIntensity = FindProperty("_FlowIntensity", properties);
            MaterialProperty pulseSharpness = FindProperty("_FlowPulseSharpness", properties);

            materialEditor.ShaderProperty(flowColor, "전류 흐름 색상 (HDR)");
            materialEditor.ShaderProperty(flowIntensity, "전류 발광 밝기");
            materialEditor.ShaderProperty(pulseSharpness, "전류 펄스 선명도");

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("전기 지직거림 노이즈 (Electric Jitter)", EditorStyles.miniBoldLabel);
            MaterialProperty noiseStrength = FindProperty("_NoiseStrength", properties);
            MaterialProperty noiseScale = FindProperty("_NoiseScale", properties);
            MaterialProperty noiseSpeed = FindProperty("_NoiseSpeed", properties);

            materialEditor.ShaderProperty(noiseStrength, "노이즈 왜곡 세기");
            materialEditor.ShaderProperty(noiseScale, "노이즈 크기 스케일");
            materialEditor.ShaderProperty(noiseSpeed, "노이즈 지직거림 속도");
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.Space(8);

        // 4. Coordinate System
        _showCoordsGroup = EditorGUILayout.BeginFoldoutHeaderGroup(_showCoordsGroup, "📐 좌표계 설정 (Coordinate Space)");
        if (_showCoordsGroup)
        {
            MaterialProperty coords = FindProperty("_Coords", properties);
            materialEditor.ShaderProperty(coords, "기준 좌표계 (Object / World / UV)");
            EditorGUILayout.HelpBox("• ObjectSpace: 3D 모델의 로컬 +X축 기준 (회전 시 함께 회전)\n• UVSpace: 텍스처 UV의 U축(+X) 기준\n• WorldSpace: 월드 절대 좌표계 기준", MessageType.None);
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.Space(8);

        // 5. Base Surface
        _showBaseGroup = EditorGUILayout.BeginFoldoutHeaderGroup(_showBaseGroup, "🎨 기본 표면 (Base Surface)");
        if (_showBaseGroup)
        {
            MaterialProperty baseMap = FindProperty("_BaseMap", properties);
            MaterialProperty baseColor = FindProperty("_BaseColor", properties);
            MaterialProperty metallic = FindProperty("_Metallic", properties);
            MaterialProperty smoothness = FindProperty("_Smoothness", properties);

            materialEditor.ShaderProperty(baseMap, "베이스 텍스처");
            materialEditor.ShaderProperty(baseColor, "베이스 색상 (Alpha 조절 가능)");
            materialEditor.ShaderProperty(metallic, "메탈릭 (Metallic)");
            materialEditor.ShaderProperty(smoothness, "스무스니스 (Smoothness)");
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.Space(8);

        // 6. Rim Fresnel Glow
        _showRimGroup = EditorGUILayout.BeginFoldoutHeaderGroup(_showRimGroup, "✨ 외곽 림 라이트 (Rim Glow)");
        if (_showRimGroup)
        {
            MaterialProperty useRim = FindProperty("_UseRim", properties);
            materialEditor.ShaderProperty(useRim, "외곽 림 라이트 활성화");

            if (useRim.floatValue > 0.5f)
            {
                MaterialProperty rimColor = FindProperty("_RimColor", properties);
                MaterialProperty rimPower = FindProperty("_RimPower", properties);
                MaterialProperty rimIntensity = FindProperty("_RimIntensity", properties);

                materialEditor.ShaderProperty(rimColor, "림 라이트 색상 (HDR)");
                materialEditor.ShaderProperty(rimPower, "림 지수 (Power)");
                materialEditor.ShaderProperty(rimIntensity, "림 밝기 (Intensity)");
            }
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("고급 설정 (Advanced Options)", EditorStyles.boldLabel);
        materialEditor.RenderQueueField();

        if (EditorGUI.EndChangeCheck())
        {
            materialEditor.PropertiesChanged();
            EditorUtility.SetDirty(material);
        }
    }

    public static void ApplySurfaceType(Material material, SurfaceType surfaceType)
    {
        if (surfaceType == SurfaceType.Transparent)
        {
            material.SetFloat("_Surface", 1.0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0.0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;
            material.SetShaderPassEnabled("ShadowCaster", false);
        }
        else
        {
            material.SetFloat("_Surface", 0.0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.One);
            material.SetFloat("_DstBlend", (float)BlendMode.Zero);
            material.SetFloat("_ZWrite", 1.0f);
            material.SetOverrideTag("RenderType", "Opaque");
            material.renderQueue = (int)RenderQueue.Geometry;
            material.SetShaderPassEnabled("ShadowCaster", true);
        }
    }

    private void DrawDirectionPresets(MaterialProperty prop)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel("방향 프리셋");
        if (GUILayout.Button("+X (기본)", EditorStyles.miniButtonLeft))
        {
            prop.vectorValue = new Vector4(1f, 0f, 0f, 0f);
        }
        if (GUILayout.Button("-X (역방향)", EditorStyles.miniButtonMid))
        {
            prop.vectorValue = new Vector4(-1f, 0f, 0f, 0f);
        }
        if (GUILayout.Button("+Y (상승)", EditorStyles.miniButtonMid))
        {
            prop.vectorValue = new Vector4(0f, 1f, 0f, 0f);
        }
        if (GUILayout.Button("+Z", EditorStyles.miniButtonRight))
        {
            prop.vectorValue = new Vector4(0f, 0f, 1f, 0f);
        }
        EditorGUILayout.EndHorizontal();
    }
}


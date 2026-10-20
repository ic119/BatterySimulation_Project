using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public class CustomOutlineGUI : ShaderGUI
{
    public enum SurfaceType
    {
        Opaque = 0,
        Transparent = 1
    }

    public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
    {
        Material material = materialEditor.target as Material;
        if (material == null) return;

        MaterialProperty surfaceProp = FindProperty("_Surface", properties, false);
        MaterialProperty srcBlendProp = FindProperty("_SrcBlend", properties, false);
        MaterialProperty dstBlendProp = FindProperty("_DstBlend", properties, false);
        MaterialProperty zwriteProp = FindProperty("_ZWrite", properties, false);
        MaterialProperty cullProp = FindProperty("_Cull", properties, false);

        MaterialProperty baseMapProp = FindProperty("_BaseMap", properties, false);
        MaterialProperty baseColorProp = FindProperty("_BaseColor", properties, false);
        MaterialProperty metallicProp = FindProperty("_Metallic", properties, false);
        MaterialProperty smoothnessProp = FindProperty("_Smoothness", properties, false);

        MaterialProperty outlineColorProp = FindProperty("_OutlineColor", properties, false);
        MaterialProperty outlineWidthProp = FindProperty("_OutlineWidth", properties, false);
        MaterialProperty outlineModeProp = FindProperty("_OutlineMode", properties, false);
        MaterialProperty screenSpaceOutlineProp = FindProperty("_ScreenSpaceOutline", properties, false);
        MaterialProperty centerOffsetProp = FindProperty("_CenterOffset", properties, false);
        MaterialProperty outlineZOffsetProp = FindProperty("_OutlineZOffset", properties, false);

        MaterialProperty useRimProp = FindProperty("_UseRim", properties, false);
        MaterialProperty rimColorProp = FindProperty("_RimColor", properties, false);
        MaterialProperty rimPowerProp = FindProperty("_RimPower", properties, false);
        MaterialProperty rimIntensityProp = FindProperty("_RimIntensity", properties, false);

        EditorGUI.BeginChangeCheck();

        // 1. Surface Options
        EditorGUILayout.LabelField("Surface Options", EditorStyles.boldLabel);
        if (surfaceProp != null)
        {
            SurfaceType currentSurface = (SurfaceType)(int)surfaceProp.floatValue;
            SurfaceType newSurface = (SurfaceType)EditorGUILayout.EnumPopup("Surface Type", currentSurface);
            if (newSurface != currentSurface)
            {
                surfaceProp.floatValue = (float)newSurface;
                ApplySurfaceType(material, newSurface);
            }
        }

        if (cullProp != null)
        {
            materialEditor.ShaderProperty(cullProp, "Cull Mode");
        }

        if (zwriteProp != null)
        {
            materialEditor.ShaderProperty(zwriteProp, "ZWrite");
        }

        // Show warning if user set low alpha but surface is still Opaque
        if (surfaceProp != null && (SurfaceType)(int)surfaceProp.floatValue == SurfaceType.Opaque && baseColorProp != null)
        {
            if (baseColorProp.colorValue.a < 0.99f)
            {
                EditorGUILayout.HelpBox("Base Color의 Alpha값이 1보다 작지만 현재 Opaque 모드입니다.\n투명하게 적용하려면 Surface Type을 'Transparent'로 변경하세요.", MessageType.Warning);
                if (GUILayout.Button("Switch to Transparent Mode"))
                {
                    surfaceProp.floatValue = (float)SurfaceType.Transparent;
                    ApplySurfaceType(material, SurfaceType.Transparent);
                }
            }
        }

        EditorGUILayout.Space(8);

        // 2. Base Surface Properties
        EditorGUILayout.LabelField("Surface Properties", EditorStyles.boldLabel);
        if (baseMapProp != null) materialEditor.TexturePropertySingleLine(new GUIContent("Base Map"), baseMapProp, baseColorProp);
        else if (baseColorProp != null) materialEditor.ShaderProperty(baseColorProp, "Base Color");

        if (metallicProp != null) materialEditor.ShaderProperty(metallicProp, "Metallic");
        if (smoothnessProp != null) materialEditor.ShaderProperty(smoothnessProp, "Smoothness");

        EditorGUILayout.Space(8);

        // 3. Outline Settings
        EditorGUILayout.LabelField("Outline Settings", EditorStyles.boldLabel);
        if (outlineColorProp != null) materialEditor.ShaderProperty(outlineColorProp, "Outline Color (HDR)");
        if (outlineWidthProp != null) materialEditor.ShaderProperty(outlineWidthProp, "Outline Width");
        if (outlineModeProp != null) materialEditor.ShaderProperty(outlineModeProp, "Outline Mode");
        if (screenSpaceOutlineProp != null) materialEditor.ShaderProperty(screenSpaceOutlineProp, "Constant Screen-Space Width");
        if (centerOffsetProp != null) materialEditor.ShaderProperty(centerOffsetProp, "Center Offset");
        if (outlineZOffsetProp != null) materialEditor.ShaderProperty(outlineZOffsetProp, "Outline Depth Offset");

        EditorGUILayout.Space(8);

        // 4. Inner Rim Outline Settings
        EditorGUILayout.LabelField("Inner Rim Outline Settings", EditorStyles.boldLabel);
        if (useRimProp != null) materialEditor.ShaderProperty(useRimProp, "Enable Inner Rim Outline");
        if (useRimProp != null && useRimProp.floatValue > 0.5f)
        {
            if (rimColorProp != null) materialEditor.ShaderProperty(rimColorProp, "Rim Color (HDR)");
            if (rimPowerProp != null) materialEditor.ShaderProperty(rimPowerProp, "Rim Power");
            if (rimIntensityProp != null) materialEditor.ShaderProperty(rimIntensityProp, "Rim Intensity");
        }

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Advanced Options", EditorStyles.boldLabel);
        materialEditor.RenderQueueField();

        if (EditorGUI.EndChangeCheck())
        {
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
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        }
        else
        {
            material.SetFloat("_Surface", 0.0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.One);
            material.SetFloat("_DstBlend", (float)BlendMode.Zero);
            material.SetFloat("_ZWrite", 1.0f);
            material.SetOverrideTag("RenderType", "Opaque");
            material.renderQueue = (int)RenderQueue.Geometry;
            material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        }
    }
}

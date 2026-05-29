// Assets/Editor/PBRGUI.cs

using UnityEditor;
using UnityEngine;

namespace bentoBAUX
{
    public class SSSSGUI : ShaderGUI
    {
        private enum LightingModel
        {
            Lambert = 0,
            BlinnPhong = 1,
            PBR = 2
        }

        // Common
        private MaterialProperty _BaseMap;
        private MaterialProperty _BaseColor;
        private MaterialProperty _NormalMap;
        private MaterialProperty _NormalStrength;
        private MaterialProperty _MicroNormalMap;
        private MaterialProperty _MicroNormalStrength;
        private MaterialProperty _MicroNormalScale;
        private MaterialProperty _MixNormals;
        private MaterialProperty _LightingModel;

        // Blinn-Phong
        private MaterialProperty _k;
        private MaterialProperty _SpecularExponent;

        // PBR
        private MaterialProperty _Roughness;
        private MaterialProperty _UseRoughnessMap;
        private MaterialProperty _RoughnessMap;
        private MaterialProperty _RoughnessStrength;

        private MaterialProperty _Metallic;
        private MaterialProperty _UseMetallicMap;
        private MaterialProperty _MetallicMap;
        private MaterialProperty _MetallicStrength;

        private MaterialProperty _Specular;
        private MaterialProperty _UseSpecularMap;
        private MaterialProperty _SpecularMap;
        private MaterialProperty _SpecularStrength;

        private MaterialProperty _AOMap;
        private MaterialProperty _AOStrength;

        private MaterialProperty _ThicknessMap;
        private MaterialProperty _ThicknessMultiplier;

        // private MaterialProperty _UseToneMapping;

        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] props)
        {
            CacheProperties(props);

            DrawGeneralSection(materialEditor);
            EditorGUILayout.Space();

            DrawLightingModelSection(materialEditor);
            EditorGUILayout.Space();

            // DrawToneMappingSection(materialEditor);
            // EditorGUILayout.Space();

            DrawTilingOffsetSection(materialEditor);

            SyncAllTargetMaterials(materialEditor);
        }

        public override void AssignNewShaderToMaterial(Material material, Shader oldShader, Shader newShader)
        {
            base.AssignNewShaderToMaterial(material, oldShader, newShader);

            LightingModel model = GetLightingModel(material);
            ApplyKeywords(material, model);
        }

        private void CacheProperties(MaterialProperty[] props)
        {
            // Common
            _BaseMap = FindProperty("_BaseMap", props);
            _BaseColor = FindProperty("_BaseColor", props);
            _NormalMap = FindProperty("_NormalMap", props);
            _NormalStrength = FindProperty("_NormalStrength", props);
            _MicroNormalMap = FindProperty("_MicroNormalMap", props);
            _MicroNormalStrength = FindProperty("_MicroNormalStrength", props);
            _MicroNormalScale = FindProperty("_MicroNormalScale", props);
            _MixNormals = FindProperty("_MixNormals", props);
            _LightingModel = FindProperty("_LightingModel", props);

            // Blinn-Phong
            _k = FindProperty("_k", props, false);
            _SpecularExponent = FindProperty("_SpecularExponent", props, false);

            // PBR
            _Roughness = FindProperty("_Roughness", props, false);
            _UseRoughnessMap = FindProperty("_UseRoughnessMap", props, false);
            _RoughnessMap = FindProperty("_RoughnessMap", props, false);
            _RoughnessStrength = FindProperty("_RoughnessStrength", props, false);

            _Metallic = FindProperty("_Metallic", props, false);
            _UseMetallicMap = FindProperty("_UseMetallicMap", props, false);
            _MetallicMap = FindProperty("_MetallicMap", props, false);
            _MetallicStrength = FindProperty("_MetallicStrength", props, false);

            _Specular = FindProperty("_Specular", props, false);
            _UseSpecularMap = FindProperty("_UseSpecularMap", props, false);
            _SpecularMap = FindProperty("_SpecularMap", props, false);
            _SpecularStrength = FindProperty("_SpecularStrength", props, false);

            _AOMap = FindProperty("_AOMap", props, false);
            _AOStrength = FindProperty("_AOStrength", props, false);

            _ThicknessMap = FindProperty("_ThicknessMap", props, false);
            _ThicknessMultiplier = FindProperty("_ThicknessMultiplier", props, false);

            // _UseToneMapping = FindProperty("_UseToneMapping", props, false);
        }

        private void DrawGeneralSection(MaterialEditor materialEditor)
        {
            EditorGUILayout.LabelField("General Settings", EditorStyles.boldLabel);

            materialEditor.TexturePropertySingleLine(new GUIContent("Base Map"), _BaseMap, _BaseColor);
            materialEditor.TexturePropertySingleLine(new GUIContent("Normal Map"), _NormalMap, _NormalStrength);
            materialEditor.TexturePropertySingleLine(new GUIContent("Micro Normal Map"), _MicroNormalMap, _MicroNormalStrength);

            if (_MicroNormalScale != null)
                materialEditor.FloatProperty(_MicroNormalScale, "Micro Normal Scale");

            if (_MixNormals != null)
                materialEditor.RangeProperty(_MixNormals, "Mix Normals");
        }

        private void DrawLightingModelSection(MaterialEditor materialEditor)
        {
            EditorGUILayout.LabelField("Lighting Model", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            LightingModel selectedModel = (LightingModel)Mathf.RoundToInt(_LightingModel.floatValue);
            selectedModel = (LightingModel)EditorGUILayout.EnumPopup("Model", selectedModel);
            if (EditorGUI.EndChangeCheck())
            {
                _LightingModel.floatValue = (float)selectedModel;
            }

            switch (selectedModel)
            {
                case LightingModel.Lambert:
                    EditorGUILayout.HelpBox("Lambert: diffuse only.", MessageType.None);
                    break;

                case LightingModel.BlinnPhong:
                    DrawBlinnPhongSection(materialEditor);
                    break;

                case LightingModel.PBR:
                    DrawPBRSection(materialEditor);
                    break;
            }
        }

        private void DrawBlinnPhongSection(MaterialEditor materialEditor)
        {
            if (_k != null)
            {
                Vector4 k4 = _k.vectorValue;
                Vector3 k3 = new Vector3(k4.x, k4.y, k4.z);

                EditorGUI.BeginChangeCheck();
                k3 = EditorGUILayout.Vector3Field("K Factors (ambient, diffuse, spec)", k3);
                if (EditorGUI.EndChangeCheck())
                {
                    _k.vectorValue = new Vector4(k3.x, k3.y, k3.z, 0f);
                }
            }

            if (_SpecularExponent != null)
            {
                materialEditor.ShaderProperty(_SpecularExponent, _SpecularExponent.displayName);
            }
        }

        private void DrawPBRSection(MaterialEditor materialEditor)
        {
            DrawMapOrScalar(materialEditor, "Roughness Map (R)", _RoughnessMap, _Roughness, _RoughnessStrength);
            DrawMapOrScalar(materialEditor, "Metallic Map (R)", _MetallicMap, _Metallic, _MetallicStrength);
            DrawMapOrScalar(materialEditor, "Specular Map (R)", _SpecularMap, _Specular, _SpecularStrength);

            if (_AOMap != null)
            {
                materialEditor.TexturePropertySingleLine(new GUIContent("AO Map (R)"), _AOMap, _AOStrength);
            }

            if (_ThicknessMap != null)
            {
                materialEditor.TexturePropertySingleLine(new GUIContent("Thickness Map (R)"), _ThicknessMap, _ThicknessMultiplier);
            }
        }

        private void DrawMapOrScalar(MaterialEditor materialEditor, string label, MaterialProperty textureProp, MaterialProperty scalarProp, MaterialProperty strengthProp)
        {
            if (textureProp == null)
                return;

            bool hasTexture = textureProp.textureValue != null;

            if (hasTexture || scalarProp == null)
            {
                materialEditor.TexturePropertySingleLine(new GUIContent(label), textureProp, strengthProp);
            }
            else
            {
                materialEditor.TexturePropertySingleLine(new GUIContent(label), textureProp, scalarProp);
            }
        }

        /* private void DrawToneMappingSection(MaterialEditor materialEditor)
        {
            if (_UseToneMapping == null)
                return;

            bool enabled = _UseToneMapping.floatValue > 0.5f;

            EditorGUI.BeginChangeCheck();
            enabled = EditorGUILayout.Toggle("Tone Map (Gamma Correction)", enabled);
            if (EditorGUI.EndChangeCheck())
            {
                _UseToneMapping.floatValue = enabled ? 1f : 0f;
            }
        } */

        private void DrawTilingOffsetSection(MaterialEditor materialEditor)
        {
            EditorGUILayout.LabelField("Texture Scale Offset", EditorStyles.boldLabel);

            if (_BaseMap != null)
            {
                materialEditor.TextureScaleOffsetProperty(_BaseMap);
            }
        }

        private void SyncAllTargetMaterials(MaterialEditor materialEditor)
        {
            foreach (Object target in materialEditor.targets)
            {
                if (target is Material mat)
                {
                    LightingModel model = GetLightingModel(mat);
                    ApplyKeywords(mat, model);
                }
            }
        }

        private static LightingModel GetLightingModel(Material material)
        {
            if (material == null || !material.HasProperty("_LightingModel"))
                return LightingModel.Lambert;

            return (LightingModel)Mathf.RoundToInt(material.GetFloat("_LightingModel"));
        }

        private static void ApplyKeywords(Material mat, LightingModel model)
        {
            // Lighting model keywords
            SetKeyword(mat, "_LM_LAMBERT", model == LightingModel.Lambert);
            SetKeyword(mat, "_LM_BLINNPHONG", model == LightingModel.BlinnPhong);
            SetKeyword(mat, "_LM_PBR", model == LightingModel.PBR);

            // Texture-driven keywords
            bool hasRoughnessMap = HasTexture(mat, "_RoughnessMap");
            bool hasMetallicMap = HasTexture(mat, "_MetallicMap");
            bool hasSpecularMap = HasTexture(mat, "_SpecularMap");

            SetKeyword(mat, "_USE_ROUGHNESS_MAP", hasRoughnessMap);
            SetKeyword(mat, "_USE_METALLIC_MAP", hasMetallicMap);
            SetKeyword(mat, "_USE_SPECULAR_MAP", hasSpecularMap);

            // Keep float properties aligned with actual map presence
            SetFloatIfPresent(mat, "_UseRoughnessMap", hasRoughnessMap ? 1f : 0f);
            SetFloatIfPresent(mat, "_UseMetallicMap", hasMetallicMap ? 1f : 0f);
            SetFloatIfPresent(mat, "_UseSpecularMap", hasSpecularMap ? 1f : 0f);

            // Tone mapping keyword
            bool toneMappingOn = mat.HasProperty("_UseToneMapping") && mat.GetFloat("_UseToneMapping") > 0.5f;
            SetKeyword(mat, "_USE_TONEMAPPING", toneMappingOn);

            EditorUtility.SetDirty(mat);
        }

        private static bool HasTexture(Material mat, string propertyName)
        {
            return mat != null && mat.HasProperty(propertyName) && mat.GetTexture(propertyName) != null;
        }

        private static void SetFloatIfPresent(Material mat, string propertyName, float value)
        {
            if (mat != null && mat.HasProperty(propertyName))
            {
                mat.SetFloat(propertyName, value);
            }
        }

        private static void SetKeyword(Material mat, string keyword, bool enabled)
        {
            if (enabled)
                mat.EnableKeyword(keyword);
            else
                mat.DisableKeyword(keyword);
        }
    }
}
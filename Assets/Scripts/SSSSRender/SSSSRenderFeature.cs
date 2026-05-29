using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public enum SSSSMaterialPreset
{
    Custom,
    Skin,
    Wax,
    Marble,
    MilkCream,
    AppleFlesh,
    Potato,
    Ketchup,
    ChickenRawMeat,
    Jade,
    Soap
}

[System.Serializable]
public struct SSSSPresetData
{
    public SSSSMaterialPreset preset;

    public bool overrideSubsurfaceWeight;
    [Range(0.0f, 1.0f)] public float subsurfaceWeight;

    [Range(0.0f, 1.0f)] public float nearFarBalance;

    public Vector4 nearSigma;
    public Vector4 farSigma;
    public float scatterScale;
}

[System.Serializable]
public class SSSSSettings
{
    [Header("Preset")] public SSSSMaterialPreset materialPreset = SSSSMaterialPreset.Custom;

    [Header("Scattering")]
    [Range(0.0f, 1.0f)] public float subsurfaceWeight = 1f;
    [Range(0.0f, 10.0f)] public float scatterScale = 2.0f;
    [Range(0.0f, 1.0f)] public float nearFarBalance = 0.5f;

    public Vector4 nearSigma = new Vector4(10.0f, 5.0f, 2.0f, 1.0f);
    public Vector4 farSigma = new Vector4(30.0f, 15.0f, 10.0f, 1.0f);

    public int sampleCount = 32;
}

public class SSSSRenderFeature : ScriptableRendererFeature
{
    [SerializeField] private Material SSSSMaterial;
    [SerializeField] private Material compositeMaterial;
    [SerializeField] private SSSSSettings settings = new SSSSSettings();

    private static readonly int SSSSGlobalEnabled = Shader.PropertyToID("_SSSS_GlobalEnabled");

    [SerializeField, HideInInspector] private SSSSMaterialPreset previousPreset = SSSSMaterialPreset.Custom;
    [SerializeField, HideInInspector] private float cachedSubsurfaceWeight;
    [SerializeField, HideInInspector] private float cachedNearFarBalance;
    [SerializeField, HideInInspector] private Vector4 cachedNearSigma;
    [SerializeField, HideInInspector] private Vector4 cachedFarSigma;
    [SerializeField, HideInInspector] private float cachedScatterScale;

    private SSSSRenderPass renderPass;

    private static readonly SSSSPresetData[] presets =
    {
        new SSSSPresetData
        {
            preset = SSSSMaterialPreset.Skin,
            overrideSubsurfaceWeight = true,
            subsurfaceWeight = 1.0f,
            nearFarBalance = 0.25f,
            nearSigma = new Vector4(0.35f, 0.07f, 0.035f, 1.0f),
            farSigma = new Vector4(1.00f, 0.12f, 0.10f, 1.0f),
            scatterScale = 10.0f
        },

        new SSSSPresetData
        {
            preset = SSSSMaterialPreset.Wax,
            overrideSubsurfaceWeight = true,
            subsurfaceWeight = 0.8f,
            nearFarBalance = 0.35f,
            nearSigma = new Vector4(1.4f, 1.1f, 0.8f, 1.0f),
            farSigma = new Vector4(4.5f, 3.5f, 2.2f, 1.0f),
            scatterScale = 2.0f
        },

        new SSSSPresetData
        {
            preset = SSSSMaterialPreset.Marble,
            overrideSubsurfaceWeight = true,
            subsurfaceWeight = 0.6f,
            nearFarBalance = 0.30f,
            nearSigma = new Vector4(0.8f, 0.9f, 1.0f, 1.0f),
            farSigma = new Vector4(3.2f, 3.8f, 4.2f, 1.0f),
            scatterScale = 2.0f
        },

        new SSSSPresetData
        {
            preset = SSSSMaterialPreset.MilkCream,
            overrideSubsurfaceWeight = false,
            nearFarBalance = 0.30f,
            nearSigma = new Vector4(1.0f, 1.1f, 1.2f, 1.0f),
            farSigma = new Vector4(3.8f, 4.2f, 4.6f, 1.0f),
            scatterScale = 2.0f
        },

        new SSSSPresetData
        {
            preset = SSSSMaterialPreset.AppleFlesh,
            overrideSubsurfaceWeight = false,
            nearFarBalance = 0.35f,
            nearSigma = new Vector4(1.3f, 0.8f, 0.35f, 1.0f),
            farSigma = new Vector4(4.0f, 2.2f, 0.75f, 1.0f),
            scatterScale = 2.0f
        },

        new SSSSPresetData
        {
            preset = SSSSMaterialPreset.Potato,
            overrideSubsurfaceWeight = false,
            nearFarBalance = 0.45f,
            nearSigma = new Vector4(0.9f, 0.75f, 0.35f, 1.0f),
            farSigma = new Vector4(3.0f, 2.3f, 0.9f, 1.0f),
            scatterScale = 2.0f
        },

        new SSSSPresetData
        {
            preset = SSSSMaterialPreset.Ketchup,
            overrideSubsurfaceWeight = false,
            nearFarBalance = 0.20f,
            nearSigma = new Vector4(1.8f, 0.18f, 0.05f, 1.0f),
            farSigma = new Vector4(5.5f, 0.35f, 0.08f, 1.0f),
            scatterScale = 2.0f
        },

        new SSSSPresetData
        {
            preset = SSSSMaterialPreset.ChickenRawMeat,
            overrideSubsurfaceWeight = false,
            nearFarBalance = 0.25f,
            nearSigma = new Vector4(1.8f, 0.55f, 0.20f, 1.0f),
            farSigma = new Vector4(5.0f, 1.10f, 0.35f, 1.0f),
            scatterScale = 2.0f
        },

        new SSSSPresetData
        {
            preset = SSSSMaterialPreset.Jade,
            overrideSubsurfaceWeight = true,
            subsurfaceWeight = 0.6f,
            nearFarBalance = 0.25f,
            nearSigma = new Vector4(0.35f, 1.4f, 0.75f, 1.0f),
            farSigma = new Vector4(1.0f, 5.0f, 2.2f, 1.0f),
            scatterScale = 2.0f
        },

        new SSSSPresetData
        {
            preset = SSSSMaterialPreset.Soap,
            overrideSubsurfaceWeight = false,
            nearFarBalance = 0.35f,
            nearSigma = new Vector4(1.2f, 1.1f, 0.95f, 1.0f),
            farSigma = new Vector4(4.2f, 3.5f, 2.8f, 1.0f),
            scatterScale = 2.0f
        }
    };

    public override void Create()
    {
        SyncRenderFeatureEnabled();
        renderPass = new SSSSRenderPass(SSSSMaterial, compositeMaterial, settings);
        renderPass.renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        SyncRenderFeatureEnabled();

        if (!isActive)
            return;

        if (SSSSMaterial == null)
        {
            Debug.LogError("SSSSRenderFeature: SSSS material is null.");
            return;
        }

        if (compositeMaterial == null)
        {
            Debug.LogError("SSSSRenderFeature: Composite material is null.");
            return;
        }

        if (renderPass == null)
        {
            renderPass = new SSSSRenderPass(SSSSMaterial, compositeMaterial, settings);
            renderPass.renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
        }

        renderPass.SetMaterials(SSSSMaterial, compositeMaterial);
        renderPass.SetSettings(settings);

        renderer.EnqueuePass(renderPass);
    }

    private void SyncRenderFeatureEnabled()
    {
        bool enabled = isActive;
        Shader.SetGlobalFloat(SSSSGlobalEnabled, enabled ? 1.0f : 0.0f);
    }

    private void OnValidate()
    {
        if (settings == null)
            return;

        SyncRenderFeatureEnabled();

        // User selected a different preset from the dropdown.
        if (settings.materialPreset != previousPreset)
        {
            ApplyMaterialPreset(settings.materialPreset);
            CacheCurrentSettings();

            previousPreset = settings.materialPreset;
            return;
        }

        // User manually changed material-profile values while a preset is active.
        // Switch to Custom so the inspector honestly reflects that this is no longer the untouched preset.
        if (settings.materialPreset != SSSSMaterialPreset.Custom && SettingsChangedFromCache())
        {
            settings.materialPreset = SSSSMaterialPreset.Custom;
            previousPreset = SSSSMaterialPreset.Custom;

            CacheCurrentSettings();
        }
    }

    private void ApplyMaterialPreset(SSSSMaterialPreset preset)
    {
        if (preset == SSSSMaterialPreset.Custom)
            return;

        for (int i = 0; i < presets.Length; i++)
        {
            if (presets[i].preset == preset)
            {
                ApplyPreset(presets[i]);
                return;
            }
        }

        Debug.LogWarning($"SSSS preset '{preset}' has no preset data assigned.");
    }

    private void ApplyPreset(SSSSPresetData preset)
    {
        if (preset.overrideSubsurfaceWeight)
            settings.subsurfaceWeight = preset.subsurfaceWeight;

        settings.nearFarBalance = preset.nearFarBalance;
        settings.nearSigma = preset.nearSigma;
        settings.farSigma = preset.farSigma;
        settings.scatterScale = preset.scatterScale;
        CacheCurrentSettings();
    }

    [ContextMenu("Reapply Current Preset")]
    private void ReapplyCurrentPreset()
    {
        if (settings == null)
            return;

        ApplyMaterialPreset(settings.materialPreset);
        CacheCurrentSettings();
        previousPreset = settings.materialPreset;
    }

    private void CacheCurrentSettings()
    {
        cachedSubsurfaceWeight = settings.subsurfaceWeight;
        cachedNearFarBalance = settings.nearFarBalance;
        cachedNearSigma = settings.nearSigma;
        cachedFarSigma = settings.farSigma;
        cachedScatterScale = settings.scatterScale;
    }

    private bool SettingsChangedFromCache()
    {
        return
            !Mathf.Approximately(settings.subsurfaceWeight, cachedSubsurfaceWeight) ||
            !Mathf.Approximately(settings.nearFarBalance, cachedNearFarBalance) ||
            !Approximately(settings.nearSigma, cachedNearSigma) ||
            !Approximately(settings.farSigma, cachedFarSigma) ||
            !Mathf.Approximately(settings.scatterScale, cachedScatterScale);
    }

    private bool Approximately(Vector4 a, Vector4 b)
    {
        return
            Mathf.Approximately(a.x, b.x) &&
            Mathf.Approximately(a.y, b.y) &&
            Mathf.Approximately(a.z, b.z) &&
            Mathf.Approximately(a.w, b.w);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && renderPass != null)
            renderPass.Dispose();
    }
}
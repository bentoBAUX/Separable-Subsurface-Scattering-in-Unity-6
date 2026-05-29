using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

public class SSSSRenderPass : ScriptableRenderPass
{
    private Material compositeMaterial;
    private Material SSSSMaterial;
    private SSSSSettings settings;

    // Shared global SSSS properties.
    // These are read by ArtistFriendlyKernel.shader, Compositor.shader and SSSSTransmission.hlsl.
    private static readonly int GlobalSubsurfaceWeight = Shader.PropertyToID("_SSSS_SubsurfaceWeight");
    private static readonly int GlobalNearFarBalanceID = Shader.PropertyToID("_SSSS_NearFarBalance");
    private static readonly int GlobalScatterScaleID = Shader.PropertyToID("_SSSS_ScatterScale");
    private static readonly int GlobalNearSigmaID = Shader.PropertyToID("_SSSS_NearSigma");
    private static readonly int GlobalFarSigmaID = Shader.PropertyToID("_SSSS_FarSigma");
    private static readonly int GlobalStepCountID = Shader.PropertyToID("_SSSS_StepCount");

    // Stores MRT textures so the later composite pass can read them in the same frame.
    private class SSSSFrameData : ContextItem
    {
        public TextureHandle diffuseTexture;
        public TextureHandle processedDiffuseTexture;
        public TextureHandle specularTexture;
        public TextureHandle ambientTexture;

        public override void Reset()
        {
            diffuseTexture = TextureHandle.nullHandle;
            specularTexture = TextureHandle.nullHandle;
            ambientTexture = TextureHandle.nullHandle;
            processedDiffuseTexture = TextureHandle.nullHandle;
        }
    }

    // Data containers used by the individual RenderGraph passes.
    // These carry textures, materials, or renderer lists from pass setup into execution.

    private class MRTPassData
    {
        public RendererListHandle rendererList;
    }

    private class SSSSPassData
    {
        public TextureHandle inputTexture;
        public Material material;
    }

    private class CompositePassData
    {
        public TextureHandle sceneTexture;
        public TextureHandle diffuseTexture;
        public TextureHandle processedDiffuseTexture;
        public TextureHandle specularTexture;
        public TextureHandle ambientTexture;
        public Material compositeMaterial;
    }

    private class FinalBlitPassData
    {
        public TextureHandle sourceTexture;
        public Material blitMaterial;
    }

    private void ApplyGlobalSettings()
    {
        Shader.SetGlobalFloat(GlobalSubsurfaceWeight, settings.subsurfaceWeight);
        Shader.SetGlobalFloat(GlobalNearFarBalanceID, settings.nearFarBalance);
        Shader.SetGlobalVector(GlobalNearSigmaID, settings.nearSigma);
        Shader.SetGlobalVector(GlobalFarSigmaID, settings.farSigma);
        Shader.SetGlobalFloat(GlobalScatterScaleID, settings.scatterScale);
        Shader.SetGlobalInt(GlobalStepCountID, settings.sampleCount);
    }

    public SSSSRenderPass(Material SSSSMaterial, Material compositeMaterial, SSSSSettings settings)
    {
        this.SSSSMaterial = SSSSMaterial;
        this.compositeMaterial = compositeMaterial;
        this.settings = settings;
    }

    public void SetMaterials(Material ssssMaterial, Material compositeMaterial)
    {
        this.SSSSMaterial = ssssMaterial;
        this.compositeMaterial = compositeMaterial;
    }

    public void SetSettings(SSSSSettings settings)
    {
        this.settings = settings;
    }

    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        // 1) Apply global settings to ArtistFriendlyKernel.shader, SSSSTransmission.hlsl and Compositor.shader
        ApplyGlobalSettings();

        UniversalRenderingData renderingData = frameData.Get<UniversalRenderingData>();
        UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
        UniversalLightData lightData = frameData.Get<UniversalLightData>();
        UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();

        // 2) Setup: Create MRT textures matching the camera target.
        RenderTextureDescriptor desc = cameraData.cameraTargetDescriptor;
        desc.depthBufferBits = 0;

        TextureHandle ambientTexture = UniversalRenderer.CreateRenderGraphTexture(renderGraph, desc, "_SSSSAmbientTexture", false);
        TextureHandle diffuseTexture = UniversalRenderer.CreateRenderGraphTexture(renderGraph, desc, "_SSSSDiffuseTexture", false);
        TextureHandle horizontalTempTexture = UniversalRenderer.CreateRenderGraphTexture(renderGraph, desc, "_SSSSHorizontalTempTexture", false);
        TextureHandle processedDiffuseTexture = UniversalRenderer.CreateRenderGraphTexture(renderGraph, desc, "_SSSSProcessedDiffuseTexture", false);
        TextureHandle specularTexture = UniversalRenderer.CreateRenderGraphTexture(renderGraph, desc, "_SSSSSpecularTexture", false);
        TextureHandle compositeOutputTexture = UniversalRenderer.CreateRenderGraphTexture(renderGraph, desc, "_SSSSCompositeOutputTexture", false);

        // Make them available to later passes in this frame.
        SSSSFrameData customData = frameData.Create<SSSSFrameData>();
        customData.diffuseTexture = diffuseTexture;
        customData.processedDiffuseTexture = processedDiffuseTexture;
        customData.specularTexture = specularTexture;
        customData.ambientTexture = ambientTexture;

        // 3) MRT pass: attach ambient, diffuse, specular into separate render textures for SSS processing
        using (var builder = renderGraph.AddRasterRenderPass<MRTPassData>("SSSS MRT Pass", out var passData))
        {
            ShaderTagId shaderTag = new ShaderTagId("UniversalForward");

            SortingCriteria sortFlags = cameraData.defaultOpaqueSortFlags;
            FilteringSettings filteringSettings = new FilteringSettings(RenderQueueRange.opaque, LayerMask.GetMask("SSSS"));

            DrawingSettings drawingSettings = RenderingUtils.CreateDrawingSettings(
                shaderTag,
                renderingData,
                cameraData,
                lightData,
                sortFlags
            );

            RendererListParams rendererListParams = new RendererListParams(
                renderingData.cullResults,
                drawingSettings,
                filteringSettings
            );

            // Create the renderer list, store it in passData, then register it with the builder.
            passData.rendererList = renderGraph.CreateRendererList(rendererListParams);
            builder.UseRendererList(passData.rendererList);

            // MRT bindings:
            // SV_Target0 -> diffuseTexture
            // SV_Target1 -> specularTexture
            // SV_Target2 -> ambientTexture
            builder.SetRenderAttachment(diffuseTexture, 0);
            builder.SetRenderAttachment(specularTexture, 1);
            builder.SetRenderAttachment(ambientTexture, 2);

            // Use the active depth buffer so geometry depth-tests correctly.
            builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture, AccessFlags.ReadWrite);

            // Keep this on while debugging so RenderGraph does not cull the pass away.
            builder.AllowPassCulling(false);

            // Define the actual GPU commands for this pass.
            // The MRTPassData filled above is received here as 'data'.
            builder.SetRenderFunc(static (MRTPassData data, RasterGraphContext context) =>
            {
                context.cmd.ClearRenderTarget(false, true, Color.black);
                context.cmd.DrawRendererList(data.rendererList);
            });
        }

        // 4a) SSS horizontal pass: Apply horizontal SSS pass onto diffuse texture and output it to horizontalTempTexture.
        using (var builder = renderGraph.AddRasterRenderPass<SSSSPassData>("SSSS SSS Horizontal Pass", out var passData))
        {
            // Setup the data needed when the pass executes.
            passData.inputTexture = diffuseTexture;
            passData.material = SSSSMaterial;

            // Declare the texture read and output target.
            builder.UseTexture(passData.inputTexture);
            builder.SetRenderAttachment(horizontalTempTexture, 0);

            builder.AllowPassCulling(false);

            // Execute material pass 0, which performs the horizontal SSS blur.
            builder.SetRenderFunc(static (SSSSPassData data, RasterGraphContext context) =>
            {
                data.material.SetTexture("_MainTex", data.inputTexture);
                Blitter.BlitTexture(context.cmd, data.inputTexture, new Vector4(1, 1, 0, 0), data.material, 0);
            });
        }

        // 4b) SSS vertical pass: Apply vertical SSS pass onto 3a's texture and output it to processedDiffuseTexture.
        using (var builder = renderGraph.AddRasterRenderPass<SSSSPassData>("SSSS SSS Vertical Pass", out var passData))
        {
            passData.inputTexture = horizontalTempTexture;
            passData.material = SSSSMaterial;

            builder.UseTexture(passData.inputTexture);
            builder.SetRenderAttachment(processedDiffuseTexture, 0);
            builder.AllowPassCulling(false);

            builder.SetRenderFunc(static (SSSSPassData data, RasterGraphContext context) =>
            {
                data.material.SetTexture("_MainTex", data.inputTexture);
                Blitter.BlitTexture(context.cmd, data.inputTexture, new Vector4(1, 1, 0, 0), data.material, 1);
            });
        }

        // 5) Composite pass: Combine processedDiffuseTexture with specularTexture and apply the masked result into compositeOutputTexture
        using (var builder = renderGraph.AddRasterRenderPass<CompositePassData>("SSSS Composite Pass", out var passData))
        {
            // Retrieve the textures created earlier in this frame.
            // These were written by the MRT and blur passes.
            SSSSFrameData custom = frameData.Get<SSSSFrameData>();

            // Data needed when the composite pass executes.
            passData.sceneTexture = resourceData.activeColorTexture;
            passData.diffuseTexture = custom.diffuseTexture;
            passData.processedDiffuseTexture = custom.processedDiffuseTexture;
            passData.specularTexture = custom.specularTexture;
            passData.ambientTexture = custom.ambientTexture;
            passData.compositeMaterial = compositeMaterial;

            // Declare all texture reads.
            builder.UseTexture(passData.sceneTexture);
            builder.UseTexture(passData.diffuseTexture);
            builder.UseTexture(passData.processedDiffuseTexture);
            builder.UseTexture(passData.specularTexture);
            builder.UseTexture(passData.ambientTexture);

            // Store the composited result in compositeOutputTexture.
            builder.SetRenderAttachment(compositeOutputTexture, 0);

            builder.AllowPassCulling(false);

            // Run composite material pass 0.
            builder.SetRenderFunc(static (CompositePassData data, RasterGraphContext context) =>
            {
                data.compositeMaterial.SetTexture("_SceneTex", data.sceneTexture);
                data.compositeMaterial.SetTexture("_DiffuseTex", data.diffuseTexture);
                data.compositeMaterial.SetTexture("_ProcessedDiffuseTex", data.processedDiffuseTexture);
                data.compositeMaterial.SetTexture("_SpecularTex", data.specularTexture);
                data.compositeMaterial.SetTexture("_AmbientTex", data.ambientTexture);
                Blitter.BlitTexture(context.cmd, data.sceneTexture, new Vector4(1, 1, 0, 0), data.compositeMaterial, 0);
            });
        }

        // 6) Final pass: Blit compositeOutputTexture into resourceData.activeColorTexture
        // We cannot sample activeColorTexture and write back into it in the same composite pass.
        // Therefore, we write the composite pass to compositeOutputTexture in 4) first,
        // then use this pass to copy that result back into activeColorTexture.
        using (var builder = renderGraph.AddRasterRenderPass<FinalBlitPassData>("SSSS Final Blit Pass", out var passData))
        {
            passData.sourceTexture = compositeOutputTexture;
            passData.blitMaterial = compositeMaterial;

            builder.UseTexture(passData.sourceTexture);
            builder.SetRenderAttachment(resourceData.activeColorTexture, 0);
            builder.AllowPassCulling(false);

            // Material pass 1 handles the copying.
            builder.SetRenderFunc(static (FinalBlitPassData data, RasterGraphContext context) =>
            {
                Blitter.BlitTexture(
                    context.cmd,
                    data.sourceTexture,
                    new Vector4(1, 1, 0, 0),
                    data.blitMaterial,
                    1
                );
            });
        }
    }

    public void Dispose()
    {
        // Nothing manual to release here. RenderGraph owns the temporary textures.
    }
}
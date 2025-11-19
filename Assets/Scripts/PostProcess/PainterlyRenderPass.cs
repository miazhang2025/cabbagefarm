using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

public class PainterlyRenderPass : ScriptableRenderPass
{
    private Material m_Material;
    private PainterlyPostProcess m_VolumeComponent;
    
    private static readonly int s_PainterlySmoothness = Shader.PropertyToID("_PainterlySmoothness");
    private static readonly int s_ShadingGradient = Shader.PropertyToID("_ShadingGradient");
    private static readonly int s_PainterlyGuide = Shader.PropertyToID("_PainterlyGuide");
    private static readonly int s_SpecularColor = Shader.PropertyToID("_SpecularColor");
    private static readonly int s_Smoothness = Shader.PropertyToID("_Smoothness");
    private static readonly int s_NormalBias = Shader.PropertyToID("_NormalBias");
    private static readonly int s_Intensity = Shader.PropertyToID("_Intensity");
    private static readonly int s_BrushTexture = Shader.PropertyToID("_BrushTexture");
    private static readonly int s_BrushScale = Shader.PropertyToID("_BrushScale");
    private static readonly int s_BrushStrength = Shader.PropertyToID("_BrushStrength");
    private static readonly int s_EdgeThreshold = Shader.PropertyToID("_EdgeThreshold");
    private static readonly int s_EdgeWidth = Shader.PropertyToID("_EdgeWidth");
    private static readonly int s_ErosionStrength = Shader.PropertyToID("_ErosionStrength");

    private class PassData
    {
        internal Material material;
        internal TextureHandle source;
        internal TextureHandle destination;
    }

    public PainterlyRenderPass(Material material)
    {
        m_Material = material;
        renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
    }

    public void Setup(PainterlyPostProcess volumeComponent)
    {
        m_VolumeComponent = volumeComponent;
    }

    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        if (m_Material == null || m_VolumeComponent == null || !m_VolumeComponent.IsActive())
            return;

        UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
        UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

        if (resourceData.isActiveTargetBackBuffer)
            return;

        // Update material properties
        m_Material.SetFloat(s_PainterlySmoothness, m_VolumeComponent.painterlySmoothness.value);
        m_Material.SetFloat(s_Smoothness, m_VolumeComponent.smoothness.value);
        m_Material.SetFloat(s_NormalBias, m_VolumeComponent.normalBias.value);
        m_Material.SetFloat(s_Intensity, m_VolumeComponent.intensity.value);
        m_Material.SetColor(s_SpecularColor, m_VolumeComponent.specularColor.value);
        
        // Brush texture properties
        m_Material.SetFloat(s_BrushScale, m_VolumeComponent.brushScale.value);
        m_Material.SetFloat(s_BrushStrength, m_VolumeComponent.brushStrength.value);
        
        // Edge erosion properties
        m_Material.SetFloat(s_EdgeThreshold, m_VolumeComponent.edgeThreshold.value);
        m_Material.SetFloat(s_EdgeWidth, m_VolumeComponent.edgeWidth.value);
        m_Material.SetFloat(s_ErosionStrength, m_VolumeComponent.erosionStrength.value);
        
        if (m_VolumeComponent.shadingGradient.value != null)
            m_Material.SetTexture(s_ShadingGradient, m_VolumeComponent.shadingGradient.value);
        
        if (m_VolumeComponent.painterlyGuide.value != null)
            m_Material.SetTexture(s_PainterlyGuide, m_VolumeComponent.painterlyGuide.value);
        
        if (m_VolumeComponent.brushTexture.value != null)
            m_Material.SetTexture(s_BrushTexture, m_VolumeComponent.brushTexture.value);

        TextureHandle source = resourceData.activeColorTexture;
        
        var descriptor = cameraData.cameraTargetDescriptor;
        descriptor.depthBufferBits = 0;
        
        TextureHandle destination = UniversalRenderer.CreateRenderGraphTexture(
            renderGraph, 
            descriptor, 
            "_PainterlyTemp", 
            false
        );

        using (var builder = renderGraph.AddRasterRenderPass<PassData>("Painterly Post Process", out var passData))
        {
            passData.material = m_Material;
            passData.source = source;
            passData.destination = destination;

            builder.UseTexture(source, AccessFlags.Read);
            builder.SetRenderAttachment(destination, 0, AccessFlags.Write);

            builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
            {
                Blitter.BlitTexture(context.cmd, data.source, new Vector4(1, 1, 0, 0), data.material, 0);
            });
        }

        resourceData.cameraColor = destination;
    }
}
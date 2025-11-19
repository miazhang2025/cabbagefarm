using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class PainterlyRendererFeature : ScriptableRendererFeature
{
    [SerializeField] private Shader m_Shader;
    private Material m_Material;
    private PainterlyRenderPass m_RenderPass;

    public override void Create()
    {
        if (m_Shader == null)
        {
            Debug.LogError("Painterly Shader is not assigned in the Renderer Feature");
            return;
        }

        m_Material = CoreUtils.CreateEngineMaterial(m_Shader);
        m_RenderPass = new PainterlyRenderPass(m_Material);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (m_RenderPass == null || m_Material == null)
            return;

        var volumeStack = VolumeManager.instance.stack;
        var customEffect = volumeStack.GetComponent<PainterlyPostProcess>();

        if (customEffect != null && customEffect.IsActive())
        {
            m_RenderPass.Setup(customEffect);
            renderer.EnqueuePass(m_RenderPass);
        }
    }

    protected override void Dispose(bool disposing)
    {
        CoreUtils.Destroy(m_Material);
    }
}
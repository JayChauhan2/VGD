using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

public class EcholocationFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        public Material material;
        public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
    }

    public Settings settings = new Settings();

    class EcholocationPass : ScriptableRenderPass
    {
        public Material material;
        
        public EcholocationPass(Material material)
        {
            this.material = material;
        }

        // --- Compatible/Non-RenderGraph Path ---
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (material == null) return;
            CommandBuffer cmd = CommandBufferPool.Get("Echolocation");
            cmd.DrawMesh(RenderingUtils.fullscreenMesh, Matrix4x4.identity, material, 0, 0);
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        // --- RenderGraph Path (URP 17+) ---
        private class PassData
        {
            public Material material;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (material == null) return;

            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();

            using (var builder = renderGraph.AddRasterRenderPass<PassData>("Echolocation Pass", out var passData))
            {
                passData.material = material;

                // Declare that we need the Depth Texture
                if (resourceData.cameraDepthTexture.IsValid())
                    builder.UseTexture(resourceData.cameraDepthTexture, AccessFlags.Read);

                // Set the active color buffer as the render target
                builder.SetRenderAttachment(resourceData.activeColorTexture, 0, AccessFlags.Write);

                builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                {
                    context.cmd.DrawMesh(RenderingUtils.fullscreenMesh, Matrix4x4.identity, data.material, 0, 0);
                });
            }
        }
    }

    EcholocationPass m_ScriptablePass;

    public override void Create()
    {
        m_ScriptablePass = new EcholocationPass(settings.material);
        m_ScriptablePass.renderPassEvent = settings.renderPassEvent;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (settings.material != null)
        {
            renderer.EnqueuePass(m_ScriptablePass);
        }
    }
}

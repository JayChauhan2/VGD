using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using System.Collections.Generic;

public class EcholocationFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        public Material material;
        public RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
    }

    public Settings settings = new Settings();

    class EcholocationPass : ScriptableRenderPass
    {
        public Material material;
        
        // Caching the full screen mesh
        private Mesh m_FullscreenMesh;
        private Mesh FullscreenMesh
        {
            get
            {
                if (m_FullscreenMesh != null) return m_FullscreenMesh;
                m_FullscreenMesh = new Mesh { name = "Fullscreen Quad" };
                m_FullscreenMesh.SetVertices(new List<Vector3>
                {
                    new Vector3(-1, -1, 0), new Vector3(-1, 1, 0), new Vector3(1, -1, 0), new Vector3(1, 1, 0)
                });
                m_FullscreenMesh.SetUVs(0, new List<Vector2>
                {
                    new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 0), new Vector2(1, 1)
                });
                m_FullscreenMesh.SetIndices(new[] { 0, 1, 2, 2, 1, 3 }, MeshTopology.Triangles, 0, false);
                return m_FullscreenMesh;
            }
        }

        public EcholocationPass(Material material)
        {
            this.material = material;
        }

        // --- Deprecated / Non-RenderGraph Path (Fallback) ---
        [System.Obsolete]
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
        }

        [System.Obsolete]
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (material == null) return;
            CommandBuffer cmd = CommandBufferPool.Get("Echolocation");
            cmd.DrawMesh(FullscreenMesh, Matrix4x4.identity, material, 0, 0);
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        // --- RenderGraph Path (Unity 6 / URP 17+) ---
        private class PassData
        {
            public Material material;
            public Mesh mesh;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (material == null) return;

            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            TextureHandle activeColor = resourceData.activeColorTexture;
            
            using (var builder = renderGraph.AddRasterRenderPass<PassData>("Echolocation Pass", out var passData))
            {
                passData.material = material;
                passData.mesh = FullscreenMesh;
                
                builder.SetRenderAttachment(activeColor, 0, AccessFlags.Write);
                
                builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                {
                    // Force identity matrices so the full screen quad (-1 to 1) covers the screen
                    context.cmd.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.identity);
                    
                    // Draw the mesh
                    context.cmd.DrawMesh(data.mesh, Matrix4x4.identity, data.material, 0, 0);
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
            // Only apply post-processing to Base cameras, skip Overlay cameras so World UI stays clean
            if (renderingData.cameraData.renderType == CameraRenderType.Base)
            {
                renderer.EnqueuePass(m_ScriptablePass);
            }
        }
    }
}

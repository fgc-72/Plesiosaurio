using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

public class BlurredHighlightFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        public LayerMask highlightLayer;
        public LayerMask occluderLayer;
        public Material depthOnlyMaterial;
        public Material blurMaterial;
        [Range(0.5f, 4f)] public float blurSize = 1.5f;
        [Range(1, 4)] public int downsample = 2;
    }

    public Settings settings = new Settings();
    private BlurredHighlightPass pass;

    public override void Create()
    {
        pass = new BlurredHighlightPass(settings)
        {
            renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing
        };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (settings.blurMaterial == null) return;
        renderer.EnqueuePass(pass);
    }

    class BlurredHighlightPass : ScriptableRenderPass
    {
        private Settings settings;
        private static readonly ShaderTagId highlightTag = new ShaderTagId("SRPDefaultUnlit");
        private static readonly ShaderTagId[] occluderTags =
        {
            new ShaderTagId("UniversalForward"),
            new ShaderTagId("UniversalForwardOnly"),
            new ShaderTagId("SRPDefaultUnlit")
        };

        private class DrawPassData
        {
            public RendererListHandle occluders;
            public RendererListHandle highlights;
        }
        private class BlitPassData { public TextureHandle input; public Material material; }

        public BlurredHighlightPass(Settings settings) { this.settings = settings; }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resourceData = frameData.Get<UniversalResourceData>();
            var cameraData = frameData.Get<UniversalCameraData>();
            var lightData = frameData.Get<UniversalLightData>();
            var renderingData = frameData.Get<UniversalRenderingData>();

            var desc = cameraData.cameraTargetDescriptor;
            desc.depthBufferBits = 0;
            desc.msaaSamples = 1;
            desc.width = Mathf.Max(1, desc.width / settings.downsample);
            desc.height = Mathf.Max(1, desc.height / settings.downsample);

            var depthDesc = desc;
            depthDesc.graphicsFormat = GraphicsFormat.None;
            depthDesc.depthBufferBits = 24;

            TextureHandle highlightTex = UniversalRenderer.CreateRenderGraphTexture(renderGraph, desc, "_HighlightRT", true);
            TextureHandle highlightDepth = UniversalRenderer.CreateRenderGraphTexture(renderGraph, depthDesc, "_HighlightDepth", true);
            TextureHandle blurA = UniversalRenderer.CreateRenderGraphTexture(renderGraph, desc, "_BlurA", true);
            TextureHandle blurB = UniversalRenderer.CreateRenderGraphTexture(renderGraph, desc, "_BlurB", true);

            using (var builder = renderGraph.AddRasterRenderPass<DrawPassData>("Draw Highlight Objects", out var passData))
            {
                var occluderFilter = new FilteringSettings(RenderQueueRange.opaque, settings.occluderLayer);
                var occluderDraw = RenderingUtils.CreateDrawingSettings(
                    new System.Collections.Generic.List<ShaderTagId>(occluderTags),
                    renderingData, cameraData, lightData, SortingCriteria.CommonOpaque);
                occluderDraw.overrideMaterial = settings.depthOnlyMaterial;
                occluderDraw.overrideMaterialPassIndex = 0;
                passData.occluders = renderGraph.CreateRendererList(
                    new RendererListParams(renderingData.cullResults, occluderDraw, occluderFilter));

                var highlightFilter = new FilteringSettings(RenderQueueRange.all, settings.highlightLayer);
                var highlightDraw = RenderingUtils.CreateDrawingSettings(
                    highlightTag, renderingData, cameraData, lightData, SortingCriteria.CommonOpaque);
                passData.highlights = renderGraph.CreateRendererList(
                    new RendererListParams(renderingData.cullResults, highlightDraw, highlightFilter));

                builder.UseRendererList(passData.occluders);
                builder.UseRendererList(passData.highlights);
                builder.SetRenderAttachment(highlightTex, 0);
                builder.SetRenderAttachmentDepth(highlightDepth, AccessFlags.ReadWrite);

                builder.SetRenderFunc((DrawPassData data, RasterGraphContext ctx) =>
                {
                    ctx.cmd.ClearRenderTarget(true, true, Color.clear);
                    ctx.cmd.DrawRendererList(data.occluders);
                    ctx.cmd.DrawRendererList(data.highlights);
                });
            }

            using (var builder = renderGraph.AddRasterRenderPass<BlitPassData>("Blur Horizontal", out var passData))
            {
                passData.input = highlightTex;
                passData.material = settings.blurMaterial;
                builder.UseTexture(highlightTex);
                builder.SetRenderAttachment(blurA, 0);
                builder.SetRenderFunc((BlitPassData data, RasterGraphContext ctx) =>
                {
                    data.material.SetFloat("_BlurSize", settings.blurSize);
                    data.material.SetVector("_Direction", new Vector4(1, 0, 0, 0));
                    Blitter.BlitTexture(ctx.cmd, data.input, new Vector4(1, 1, 0, 0), data.material, 0);
                });
            }

            using (var builder = renderGraph.AddRasterRenderPass<BlitPassData>("Blur Vertical", out var passData))
            {
                passData.input = blurA;
                passData.material = settings.blurMaterial;
                builder.UseTexture(blurA);
                builder.SetRenderAttachment(blurB, 0);
                builder.SetRenderFunc((BlitPassData data, RasterGraphContext ctx) =>
                {
                    data.material.SetFloat("_BlurSize", settings.blurSize);
                    data.material.SetVector("_Direction", new Vector4(0, 1, 0, 0));
                    Blitter.BlitTexture(ctx.cmd, data.input, new Vector4(1, 1, 0, 0), data.material, 0);
                });
            }

            using (var builder = renderGraph.AddRasterRenderPass<BlitPassData>("Composite Highlight", out var passData))
            {
                passData.input = blurB;
                passData.material = settings.blurMaterial;
                builder.UseTexture(blurB);
                builder.SetRenderAttachment(resourceData.cameraColor, 0, AccessFlags.ReadWrite);
                builder.SetRenderFunc((BlitPassData data, RasterGraphContext ctx) =>
                {
                    Blitter.BlitTexture(ctx.cmd, data.input, new Vector4(1, 1, 0, 0), data.material, 1);
                });
            }
        }
    }
}
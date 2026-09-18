using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

public class PlayerOccludedHighlightFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        public LayerMask highlightLayer;
        public Material highlightMaterial;
        public LayerMask playerLayer;
        public Material depthOnlyMaterial;
    }

    public Settings settings = new Settings();
    private HighlightPass pass;

    public override void Create()
    {
        pass = new HighlightPass(settings)
        {
            renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing
        };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (settings.depthOnlyMaterial == null || settings.highlightMaterial == null) return;
        renderer.EnqueuePass(pass);
    }

    class HighlightPass : ScriptableRenderPass
    {
        private Settings settings;

        // Lista amplia de tags para reconocer objetos sin importar qué shader tengan puesto de verdad
        private static readonly List<ShaderTagId> commonTags = new List<ShaderTagId>
        {
            new ShaderTagId("UniversalForward"),
            new ShaderTagId("UniversalForwardOnly"),
            new ShaderTagId("SRPDefaultUnlit"),
            new ShaderTagId("LightweightForward")
        };

        private class PassData
        {
            public RendererListHandle playerDepth;
            public RendererListHandle highlights;
        }

        public HighlightPass(Settings settings) { this.settings = settings; }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resourceData = frameData.Get<UniversalResourceData>();
            var cameraData = frameData.Get<UniversalCameraData>();
            var lightData = frameData.Get<UniversalLightData>();
            var renderingData = frameData.Get<UniversalRenderingData>();

            var depthDesc = cameraData.cameraTargetDescriptor;
            depthDesc.graphicsFormat = GraphicsFormat.None;
            depthDesc.depthBufferBits = 24;
            depthDesc.msaaSamples = 1;

            TextureHandle playerOnlyDepth = UniversalRenderer.CreateRenderGraphTexture(
                renderGraph, depthDesc, "_PlayerOnlyDepth", true);

            using (var builder = renderGraph.AddRasterRenderPass<PassData>("Highlight (Player Occlusion)", out var passData))
            {
                var playerFilter = new FilteringSettings(RenderQueueRange.opaque, settings.playerLayer);
                var playerDraw = RenderingUtils.CreateDrawingSettings(
                    commonTags, renderingData, cameraData, lightData, SortingCriteria.CommonOpaque);
                playerDraw.overrideMaterial = settings.depthOnlyMaterial;
                playerDraw.overrideMaterialPassIndex = 0;
                passData.playerDepth = renderGraph.CreateRendererList(
                    new RendererListParams(renderingData.cullResults, playerDraw, playerFilter));

                var highlightFilter = new FilteringSettings(RenderQueueRange.all, settings.highlightLayer);
                var highlightDraw = RenderingUtils.CreateDrawingSettings(
                    commonTags, renderingData, cameraData, lightData, SortingCriteria.CommonOpaque);
                highlightDraw.overrideMaterial = settings.highlightMaterial;
                highlightDraw.overrideMaterialPassIndex = 0;
                passData.highlights = renderGraph.CreateRendererList(
                    new RendererListParams(renderingData.cullResults, highlightDraw, highlightFilter));

                builder.UseRendererList(passData.playerDepth);
                builder.UseRendererList(passData.highlights);
                builder.SetRenderAttachment(resourceData.cameraColor, 0, AccessFlags.ReadWrite);
                builder.SetRenderAttachmentDepth(playerOnlyDepth, AccessFlags.ReadWrite);

                builder.SetRenderFunc((PassData data, RasterGraphContext ctx) =>
                {
                    ctx.cmd.ClearRenderTarget(true, false, Color.clear);
                    ctx.cmd.DrawRendererList(data.playerDepth);
                    ctx.cmd.DrawRendererList(data.highlights);
                });
            }
        }
    }
}
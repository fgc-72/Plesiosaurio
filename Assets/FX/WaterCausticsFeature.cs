using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace UnderwaterFX
{
    /// <summary>
    /// Renderer Feature de URP (Render Graph). Anadelo en tu URP Renderer asset:
    /// Add Renderer Feature > Water Caustics Feature.
    /// </summary>
    public class WaterCausticsFeature : ScriptableRendererFeature
    {
        [SerializeField] Shader shader;

        Material material;
        CausticsPass pass;
        bool warnedNoVolume;

        public override void Create()
        {
            Debug.Log("[WaterCaustics] Create() llamado: el Renderer Feature SI esta en el Renderer activo.");

            if (shader == null) shader = Shader.Find("Hidden/UnderwaterFX/CausticsProjection");
            if (shader == null)
            {
                Debug.LogError("[WaterCaustics] No se encontro el shader Hidden/UnderwaterFX/CausticsProjection. Revisa que CausticsProjection.shader este en el proyecto y sin errores.");
                return;
            }

            CoreUtils.Destroy(material);
            material = CoreUtils.CreateEngineMaterial(shader);
            pass = new CausticsPass(material)
            {
                renderPassEvent = RenderPassEvent.AfterRenderingSkybox
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (pass == null || material == null) return;

            CameraType type = renderingData.cameraData.cameraType;
            if (type == CameraType.Preview || type == CameraType.Reflection) return;

            WaterCausticsVolume volume = WaterCausticsVolume.Active;
            if (volume == null)
                volume = Object.FindFirstObjectByType<WaterCausticsVolume>();

            if (volume == null)
            {
                if (!warnedNoVolume)
                {
                    Debug.LogWarning("[WaterCaustics] No hay ningun WaterCausticsVolume activo en la escena.");
                    warnedNoVolume = true;
                }
                return;
            }
            warnedNoVolume = false;

            volume.ApplyTo(material);

            // Pide profundidad y normales a URP (activa el prepass DepthNormals).
            pass.ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal);
            renderer.EnqueuePass(pass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(material);
            material = null;
            pass = null;
        }

        class CausticsPass : ScriptableRenderPass
        {
            readonly Material material;

            class PassData
            {
                public Material material;
            }

            public CausticsPass(Material material)
            {
                this.material = material;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();

                using (var builder = renderGraph.AddRasterRenderPass<PassData>("Water Caustics", out PassData passData))
                {
                    passData.material = material;

                    builder.SetRenderAttachment(resourceData.activeColorTexture, 0, AccessFlags.ReadWrite);

                    if (resourceData.cameraDepthTexture.IsValid())
                        builder.UseTexture(resourceData.cameraDepthTexture);
                    if (resourceData.cameraNormalsTexture.IsValid())
                        builder.UseTexture(resourceData.cameraNormalsTexture);
                    if (resourceData.mainShadowsTexture.IsValid())
                        builder.UseTexture(resourceData.mainShadowsTexture);

                    builder.AllowPassCulling(false);

                    builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                    {
                        context.cmd.DrawProcedural(Matrix4x4.identity, data.material, 0, MeshTopology.Triangles, 3, 1);
                    });
                }
            }
        }
    }
}
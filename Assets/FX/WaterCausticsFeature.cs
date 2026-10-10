using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace UnderwaterFX
{
    public class WaterCausticsFeature : ScriptableRendererFeature
    {
        [SerializeField] Shader causticsShader;
        [SerializeField] Shader fogShader;
        [SerializeField] Shader godRaysShader;

        Material causticsMaterial;
        Material fogMaterial;
        Material godRaysMaterial;
        FullscreenPass causticsPass;
        FullscreenPass fogPass;
        FullscreenPass godRaysPass;
        bool warnedNoVolume;

        public override void Create()
        {
            if (causticsShader == null) causticsShader = Shader.Find("Hidden/UnderwaterFX/CausticsProjection");
            if (fogShader == null) fogShader = Shader.Find("Hidden/UnderwaterFX/UnderwaterFog");
            if (godRaysShader == null) godRaysShader = Shader.Find("Hidden/UnderwaterFX/GodRays");

            CoreUtils.Destroy(causticsMaterial);
            CoreUtils.Destroy(fogMaterial);
            CoreUtils.Destroy(godRaysMaterial);
            causticsMaterial = null;
            fogMaterial = null;
            godRaysMaterial = null;
            causticsPass = null;
            fogPass = null;
            godRaysPass = null;

            if (causticsShader != null)
            {
                causticsMaterial = CoreUtils.CreateEngineMaterial(causticsShader);
                causticsPass = new FullscreenPass(causticsMaterial, "Water Caustics", 1)
                {
                    renderPassEvent = RenderPassEvent.AfterRenderingSkybox
                };
            }
            else
            {
                Debug.LogError("[WaterCaustics] No se encontro el shader Hidden/UnderwaterFX/CausticsProjection.");
            }

            if (fogShader != null)
            {
                fogMaterial = CoreUtils.CreateEngineMaterial(fogShader);
                fogPass = new FullscreenPass(fogMaterial, "Underwater Fog", 2)
                {
                    renderPassEvent = RenderPassEvent.AfterRenderingSkybox + 1
                };
            }
            else
            {
                Debug.LogError("[WaterCaustics] No se encontro el shader Hidden/UnderwaterFX/UnderwaterFog.");
            }

            if (godRaysShader != null)
            {
                godRaysMaterial = CoreUtils.CreateEngineMaterial(godRaysShader);
                godRaysPass = new FullscreenPass(godRaysMaterial, "Underwater God Rays", 1)
                {
                    renderPassEvent = RenderPassEvent.AfterRenderingSkybox + 2
                };
            }
            else
            {
                Debug.LogError("[WaterCaustics] No se encontro el shader Hidden/UnderwaterFX/GodRays.");
            }
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
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

            if (causticsPass != null && causticsMaterial != null)
            {
                volume.ApplyTo(causticsMaterial);
                // Pide profundidad y normales a URP (activa el prepass DepthNormals).
                causticsPass.ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal);
                renderer.EnqueuePass(causticsPass);
            }

            if (volume.underwaterFog && fogPass != null && fogMaterial != null)
            {
                volume.ApplyFogTo(fogMaterial);
                fogPass.ConfigureInput(ScriptableRenderPassInput.Depth);
                renderer.EnqueuePass(fogPass);
            }

            if (volume.godRays && godRaysPass != null && godRaysMaterial != null)
            {
                volume.ApplyGodRaysTo(godRaysMaterial);
                godRaysPass.ConfigureInput(ScriptableRenderPassInput.Depth);
                renderer.EnqueuePass(godRaysPass);
            }
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(causticsMaterial);
            CoreUtils.Destroy(fogMaterial);
            CoreUtils.Destroy(godRaysMaterial);
            causticsMaterial = null;
            fogMaterial = null;
            godRaysMaterial = null;
            causticsPass = null;
            fogPass = null;
            godRaysPass = null;
        }

        class FullscreenPass : ScriptableRenderPass
        {
            readonly Material material;
            readonly string passName;
            readonly int shaderPassCount;

            class PassData
            {
                public Material material;
                public int shaderPassCount;
            }

            public FullscreenPass(Material material, string passName, int shaderPassCount)
            {
                this.material = material;
                this.passName = passName;
                this.shaderPassCount = shaderPassCount;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();

                using (var builder = renderGraph.AddRasterRenderPass<PassData>(passName, out PassData passData))
                {
                    passData.material = material;
                    passData.shaderPassCount = shaderPassCount;

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
                        for (int p = 0; p < data.shaderPassCount; p++)
                            context.cmd.DrawProcedural(Matrix4x4.identity, data.material, p, MeshTopology.Triangles, 3, 1);
                    });
                }
            }
        }
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace ListenMode
{
    public class ListenModeFeature : ScriptableRendererFeature
    {
        // ------------------------------------------------------------ API estatica
        public static bool Listening { get; set; }
        public static void SetListening(bool value) { Listening = value; }
        public static void ToggleListening() { Listening = !Listening; }

        static float s_Intensity;
        static int s_LastFrame = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Listening = false;
            s_Intensity = 0f;
            s_LastFrame = -1;
        }

        // ------------------------------------------------------------ Parametros
        [Header("Objetivos")]
        [Tooltip("Capas que reciben el aura (por ejemplo Highlightable).")]
        public LayerMask layerMask = 0;
        [Tooltip("Capas que NO deben recibir aura encima, normalmente el jugador. Usa una capa propia (por ejemplo Player), no Default.")]
        public LayerMask blockerLayerMask = 0;

        public enum DebugView { Off, Mask, Blur }

        [Header("Estado")]
        [Tooltip("Marcalo en Play para probar el aura sin escribir codigo.")]
        public bool debugListening = false;
        [Min(0.05f)] public float fadeTime = 0.35f;
        [Tooltip("Diagnostico: Mask muestra en pantalla lo que se dibuja de los objetos marcados; Blur muestra el desenfoque.")]
        public DebugView debugView = DebugView.Off;

        [Header("Aura")]
        [ColorUsage(false, true)] public Color auraColor = new Color(2.0f, 1.45f, 0.45f, 1f);
        [Tooltip("Color del borde interno del halo (mas blanco da el brillo tipo TLOU).")]
        [ColorUsage(false, true)] public Color coreColor = new Color(3.0f, 2.8f, 2.2f, 1f);
        [Tooltip("Ancho del halo en pixeles.")]
        [Range(2f, 80f)] public float radius = 22f;
        [Range(1, 3)] public int blurIterations = 2;
        [Range(0.1f, 10f)] public float intensity = 2f;
        [Tooltip("Mayor = halo mas pegado al borde. Menor = halo mas difuso y ancho.")]
        [Range(0.3f, 4f)] public float falloff = 1.4f;
        [Tooltip("Relleno tenue de la silueta cuando el objeto esta tapado por una pared.")]
        [Range(0f, 1f)] public float occludedFill = 0.2f;
        [Range(0f, 0.6f)] public float pulseAmount = 0.15f;
        [Min(0f)] public float pulseSpeed = 2.5f;

        [Header("Alcance")]
        [Min(1f)] public float maxRange = 40f;
        [Min(0.1f)] public float fadeDistance = 10f;
        [Tooltip("Tolerancia al decidir si una parte del objeto esta tapada.")]
        [Min(0f)] public float depthBias = 0.08f;

        [SerializeField, HideInInspector] Shader shader;

        Material material;
        ListenPass pass;
        bool failed;
        bool wasActive;

        // ------------------------------------------------------------ Feature
        public override void Create()
        {
            failed = false;

            if (shader == null) shader = Shader.Find("Custom/XRayHighlight");
            if (shader == null)
            {
                Debug.LogError("[ListenMode] No se encontro el shader Custom/XRayHighlight (archivo XRayHighLight.shader).");
                return;
            }

            CoreUtils.Destroy(material);
            material = CoreUtils.CreateEngineMaterial(shader);
            pass = new ListenPass(this, material)
            {
                // Antes del post-proceso: asi el Bloom (si lo tienes) realza el brillo del aura.
                renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (pass == null || material == null || failed) return;

            CameraType type = renderingData.cameraData.cameraType;
            if (type == CameraType.Preview || type == CameraType.Reflection) return;

            UpdateFade();

            bool active = s_Intensity > 0.001f;
            if (active && !wasActive) LogTargets();
            wasActive = active;

            if (!active || layerMask.value == 0) return;

            pass.ConfigureInput(ScriptableRenderPassInput.Depth);
            renderer.EnqueuePass(pass);
        }

        void UpdateFade()
        {
            if (Time.frameCount == s_LastFrame) return; // varias camaras por frame: avanzar solo una vez
            s_LastFrame = Time.frameCount;

            float target = (Listening || debugListening) ? 1f : 0f;
            s_Intensity = Mathf.MoveTowards(s_Intensity, target, Time.unscaledDeltaTime / Mathf.Max(0.05f, fadeTime));
        }

        // Se ejecuta una vez cada vez que se activa el modo escucha: dice cuantos renderers hay en la capa.
        void LogTargets()
        {
            if (layerMask.value == 0)
            {
                Debug.LogWarning("[ListenMode] El Layer Mask del Listen Mode Feature esta vacio (Nothing): elige la capa Highlightable.");
                return;
            }

            int total = 0;
            int meshes = 0;
            Renderer[] all = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            foreach (Renderer r in all)
            {
                if ((layerMask.value & (1 << r.gameObject.layer)) == 0) continue;
                if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
                total++;
                if (r is MeshRenderer || r is SkinnedMeshRenderer) meshes++;
            }

            if (meshes == 0)
                Debug.LogWarning("[ListenMode] Ningun MeshRenderer/SkinnedMeshRenderer activo esta en la capa elegida. Ojo: la capa se aplica por objeto; la MALLA (normalmente un hijo del personaje) tambien debe tener la capa.");
            else
                Debug.Log("[ListenMode] Modo escucha activado. Renderers de malla en la capa elegida: " + meshes);
        }

        void Fail(System.Exception e)
        {
            failed = true;
            Debug.LogError("[ListenMode] Error al dibujar el aura; se desactiva hasta recargar. Detalle: " + e);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(material);
            material = null;
            pass = null;
        }

        // ------------------------------------------------------------ Pase
        class ListenPass : ScriptableRenderPass
        {
            static readonly int SrcId = Shader.PropertyToID("_ListenSrc");
            static readonly int MaskId = Shader.PropertyToID("_ListenMaskTex");
            static readonly int BlurId = Shader.PropertyToID("_ListenBlurTex");
            static readonly int BlurDirId = Shader.PropertyToID("_ListenBlurDir");

            static readonly List<ShaderTagId> ShaderTags = new List<ShaderTagId>
            {
                new ShaderTagId("UniversalForward"),
                new ShaderTagId("UniversalForwardOnly"),
                new ShaderTagId("SRPDefaultUnlit"),
                new ShaderTagId("LightweightForward"),
            };

            readonly ListenModeFeature owner;
            readonly Material material;

            class MaskData
            {
                public RendererListHandle rendererList;
                public RendererListHandle blockerList;
                public bool hasBlockers;
            }

            class BlurData
            {
                public Material material;
                public TextureHandle src;
                public Vector4 dir;
            }

            class CompositeData
            {
                public Material material;
                public TextureHandle mask;
                public TextureHandle blur;
            }

            public ListenPass(ListenModeFeature owner, Material material)
            {
                this.owner = owner;
                this.material = material;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                try
                {
                    Record(renderGraph, frameData);
                }
                catch (System.Exception e)
                {
                    owner.Fail(e);
                }
            }

            void Record(RenderGraph renderGraph, ContextContainer frameData)
            {
                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                UniversalRenderingData renderingData = frameData.Get<UniversalRenderingData>();
                UniversalLightData lightData = frameData.Get<UniversalLightData>();

                ListenModeFeature f = owner;

                // Parametros del efecto (se leen al ejecutar los pases).
                material.SetFloat("_MaxRange", f.maxRange);
                material.SetFloat("_FadeDistance", f.fadeDistance);
                material.SetFloat("_DepthBias", f.depthBias);

                Color a = f.auraColor;
                Color c = f.coreColor;
                float smooth = Mathf.SmoothStep(0f, 1f, s_Intensity);
                material.SetVector("_AuraColor", new Vector4(a.r, a.g, a.b, 1f));
                material.SetVector("_CoreColor", new Vector4(c.r, c.g, c.b, 1f));
                material.SetFloat("_AuraIntensity", f.intensity * smooth);
                material.SetFloat("_AuraFalloff", f.falloff);
                material.SetFloat("_OccludedFill", f.occludedFill);
                material.SetFloat("_PulseAmount", f.pulseAmount);
                material.SetFloat("_PulseSpeed", f.pulseSpeed);
                material.SetFloat("_DebugView", (float)f.debugView);

                // Texturas temporales: mascara a resolucion completa y dos de resolucion media para el desenfoque.
                RenderTextureDescriptor desc = cameraData.cameraTargetDescriptor;
                desc.msaaSamples = 1;
                desc.depthBufferBits = 0;
                desc.depthStencilFormat = GraphicsFormat.None;
                desc.useMipMap = false;
                desc.graphicsFormat = GraphicsFormat.R8G8B8A8_UNorm;
                desc.sRGB = false;

                var maskDesc = new TextureDesc(desc.width, desc.height)
                {
                    name = "_ListenMask",
                    colorFormat = GraphicsFormat.R8G8B8A8_UNorm,
                    clearBuffer = true,
                    clearColor = Color.clear,
                    filterMode = FilterMode.Bilinear,
                };
                TextureHandle mask = renderGraph.CreateTexture(maskDesc);

                RenderTextureDescriptor half = desc;
                half.width = Mathf.Max(1, desc.width / 2);
                half.height = Mathf.Max(1, desc.height / 2);
                TextureHandle blurA = UniversalRenderer.CreateRenderGraphTexture(renderGraph, half, "_ListenBlurA", false, FilterMode.Bilinear);
                TextureHandle blurB = UniversalRenderer.CreateRenderGraphTexture(renderGraph, half, "_ListenBlurB", false, FilterMode.Bilinear);

                // 1) Mascara: todo lo que este en la capa, sin test de profundidad (se ve a traves de paredes).
                using (var builder = renderGraph.AddRasterRenderPass<MaskData>("Listen Mask", out MaskData data))
                {
                    DrawingSettings drawSettings = RenderingUtils.CreateDrawingSettings(
                        ShaderTags, renderingData, cameraData, lightData, SortingCriteria.CommonOpaque);
                    drawSettings.overrideMaterial = material;
                    drawSettings.overrideMaterialPassIndex = 0;

                    var filtering = new FilteringSettings(RenderQueueRange.all, f.layerMask);
                    var listParams = new RendererListParams(renderingData.cullResults, drawSettings, filtering);
                    data.rendererList = renderGraph.CreateRendererList(listParams);
                    builder.UseRendererList(data.rendererList);

                    // Bloqueadores (el jugador): pase 3 del shader, marcan donde NO debe haber aura.
                    data.hasBlockers = f.blockerLayerMask.value != 0;
                    if (data.hasBlockers)
                    {
                        DrawingSettings blockSettings = RenderingUtils.CreateDrawingSettings(
                            ShaderTags, renderingData, cameraData, lightData, SortingCriteria.CommonOpaque);
                        blockSettings.overrideMaterial = material;
                        blockSettings.overrideMaterialPassIndex = 3;

                        var blockFiltering = new FilteringSettings(RenderQueueRange.all, f.blockerLayerMask);
                        var blockParams = new RendererListParams(renderingData.cullResults, blockSettings, blockFiltering);
                        data.blockerList = renderGraph.CreateRendererList(blockParams);
                        builder.UseRendererList(data.blockerList);
                    }

                    builder.SetRenderAttachment(mask, 0);
                    if (resourceData.cameraDepthTexture.IsValid())
                        builder.UseTexture(resourceData.cameraDepthTexture);
                    builder.AllowPassCulling(false);

                    builder.SetRenderFunc((MaskData d, RasterGraphContext context) =>
                    {
                        context.cmd.DrawRendererList(d.rendererList);
                        if (d.hasBlockers)
                            context.cmd.DrawRendererList(d.blockerList);
                    });
                }

                // 2) Desenfoque separable (H y V). Cada iteracion suma varianza, asi que el paso se reduce.
                int iterations = Mathf.Clamp(f.blurIterations, 1, 3);
                float step = f.radius / (8f * Mathf.Sqrt(iterations)); // en texels de resolucion media

                TextureHandle src = mask;
                for (int it = 0; it < iterations; it++)
                {
                    AddBlur(renderGraph, "Listen Blur H", src, blurA, new Vector4(step / half.width, 0f, 0f, 0f));
                    AddBlur(renderGraph, "Listen Blur V", blurA, blurB, new Vector4(0f, step / half.height, 0f, 0f));
                    src = blurB;
                }

                // 3) Composicion: suma el halo al color de la camara.
                using (var builder = renderGraph.AddRasterRenderPass<CompositeData>("Listen Composite", out CompositeData data))
                {
                    data.material = material;
                    data.mask = mask;
                    data.blur = blurB;

                    builder.UseTexture(mask);
                    builder.UseTexture(blurB);
                    builder.SetRenderAttachment(resourceData.activeColorTexture, 0, AccessFlags.ReadWrite);
                    builder.AllowGlobalStateModification(true); // el pase usa SetGlobalTexture
                    builder.AllowPassCulling(false);

                    builder.SetRenderFunc((CompositeData d, RasterGraphContext context) =>
                    {
                        context.cmd.SetGlobalTexture(MaskId, d.mask);
                        context.cmd.SetGlobalTexture(BlurId, d.blur);
                        context.cmd.DrawProcedural(Matrix4x4.identity, d.material, 2, MeshTopology.Triangles, 3, 1);
                    });
                }
            }

            void AddBlur(RenderGraph renderGraph, string name, TextureHandle src, TextureHandle dst, Vector4 dir)
            {
                using (var builder = renderGraph.AddRasterRenderPass<BlurData>(name, out BlurData data))
                {
                    data.material = material;
                    data.src = src;
                    data.dir = dir;

                    builder.UseTexture(src);
                    builder.SetRenderAttachment(dst, 0);
                    builder.AllowGlobalStateModification(true); // el pase usa SetGlobalTexture / SetGlobalVector
                    builder.AllowPassCulling(false);

                    builder.SetRenderFunc((BlurData d, RasterGraphContext context) =>
                    {
                        context.cmd.SetGlobalTexture(SrcId, d.src);
                        context.cmd.SetGlobalVector(BlurDirId, d.dir);
                        context.cmd.DrawProcedural(Matrix4x4.identity, d.material, 1, MeshTopology.Triangles, 3, 1);
                    });
                }
            }
        }
    }
}
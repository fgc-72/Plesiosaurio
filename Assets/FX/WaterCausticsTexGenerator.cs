using System;
using UnityEngine;

namespace UnderwaterFX
{
    /// <summary>
    /// Fase 1: genera una textura de caustics tileable y animada en una RenderTexture.
    /// La textura queda disponible como global "_WaterCausticsTex" para la fase 2 (proyeccion).
    /// </summary>
    [ExecuteAlways]
    public class WaterCausticsTexGenerator : MonoBehaviour
    {
        const int MaxWaves = 4;

        [Serializable]
        public class Wave
        {
            public bool active = true;
            public bool pause = false;
            [Range(1, 32)] public int density = 5;          // periodos de ruido en la textura
            [Range(0f, 2f)] public float height = 0.4f;     // curvatura de la onda
            [Range(0f, 2f)] public float fluctuation = 0.6f; // velocidad de cambio de forma
            [Range(-3f, 3f)] public float flowU = 0.7f;
            [Range(-3f, 3f)] public float flowV = 0f;
            [NonSerialized] public float time;
        }

        public enum Resolution { R128 = 128, R256 = 256, R512 = 512, R1024 = 1024 }

        [Header("Salida")]
        [Tooltip("Opcional. Si se deja vacio se crea una RenderTexture interna.")]
        public RenderTexture outputRenderTexture;
        public Resolution resolution = Resolution.R256;
        public string globalTextureName = "_WaterCausticsTex";

        [Header("Ondas")]
        [Range(0f, 5f)] public float speed = 1f;
        public Wave[] waves =
        {
            new Wave { density = 5, height = 0.4f, fluctuation = 0.6f, flowU = 0.7f, flowV = 0f },
            new Wave { density = 11, height = 0.25f, fluctuation = 0.6f, flowU = -0.7f, flowV = 0f },
        };

        [Header("Refraccion")]
        [Tooltip("Distancia superficie -> fondo en unidades de textura. Mas alto = mas lineas y mas contraste.")]
        [Range(0.1f, 8f)] public float depth = 2f;
        [Range(1.01f, 2f)] public float refractionIndex = 1.333f;
        [Tooltip("Separacion de indices por canal RGB. 0 = sin aberracion cromatica.")]
        [Range(0f, 0.1f)] public float chromaticAberration = 0.02f;

        [Header("Ajuste")]
        [Range(0.001f, 0.5f)] public float fillGap = 0.08f;
        [Range(0.1f, 3f)] public float brightness = 0.7f;
        [Range(0.2f, 3f)] public float gamma = 1f;
        [Range(0.5f, 8f)] public float clamp = 2f;

        [SerializeField, HideInInspector] Shader shader;

        static readonly int WaveCountId = Shader.PropertyToID("_WaveCount");
        static readonly int WaveAId = Shader.PropertyToID("_WaveA");
        static readonly int WaveBId = Shader.PropertyToID("_WaveB");
        static readonly int DepthId = Shader.PropertyToID("_Depth");
        static readonly int IorId = Shader.PropertyToID("_IOR");
        static readonly int ChromaId = Shader.PropertyToID("_Chroma");
        static readonly int FillGapId = Shader.PropertyToID("_FillGap");
        static readonly int BrightnessId = Shader.PropertyToID("_Brightness");
        static readonly int GammaId = Shader.PropertyToID("_Gamma");
        static readonly int ClampId = Shader.PropertyToID("_Clamp");
        static readonly int EpsId = Shader.PropertyToID("_Eps");

        Material material;
        RenderTexture ownedRT;
        readonly Vector4[] waveA = new Vector4[MaxWaves];
        readonly Vector4[] waveB = new Vector4[MaxWaves];

        public RenderTexture Output => outputRenderTexture != null ? outputRenderTexture : ownedRT;

        void Reset()
        {
            shader = Shader.Find("Hidden/UnderwaterFX/CausticsGen");
        }

        void OnEnable()
        {
            if (shader == null) shader = Shader.Find("Hidden/UnderwaterFX/CausticsGen");
            Render(0f);
        }

        void OnDisable()
        {
            if (material != null) DestroyImmediate(material);
            if (ownedRT != null)
            {
                ownedRT.Release();
                DestroyImmediate(ownedRT);
                ownedRT = null;
            }
        }

        void Update()
        {
            Render(Time.deltaTime * speed);
        }

        void EnsureResources()
        {
            if (material == null && shader != null)
                material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };

            int res = (int)resolution;

            if (outputRenderTexture != null)
            {
                if (ownedRT != null)
                {
                    ownedRT.Release();
                    DestroyImmediate(ownedRT);
                    ownedRT = null;
                }
                return;
            }

            if (ownedRT != null && ownedRT.width != res)
            {
                ownedRT.Release();
                DestroyImmediate(ownedRT);
                ownedRT = null;
            }

            if (ownedRT == null)
            {
                ownedRT = new RenderTexture(res, res, 0, RenderTextureFormat.ARGBHalf)
                {
                    name = "WaterCausticsTexture",
                    wrapMode = TextureWrapMode.Repeat,
                    filterMode = FilterMode.Trilinear,
                    useMipMap = true,
                    autoGenerateMips = true,
                    anisoLevel = 4,
                    hideFlags = HideFlags.HideAndDontSave
                };
                ownedRT.Create();
            }
        }

        void Render(float dt)
        {
            EnsureResources();
            if (material == null) return;

            RenderTexture rt = Output;
            if (rt == null) return;

            int count = Mathf.Min(waves != null ? waves.Length : 0, MaxWaves);
            for (int i = 0; i < count; i++)
            {
                Wave w = waves[i];
                if (!w.pause) w.time += dt;
                waveA[i] = new Vector4(w.density, w.height, w.fluctuation, w.active ? 1f : 0f);
                // seed distinto por onda para que no sean identicas
                waveB[i] = new Vector4(w.flowU, w.flowV, i * 37f, w.time);
            }

            material.SetInt(WaveCountId, count);
            material.SetVectorArray(WaveAId, waveA);
            material.SetVectorArray(WaveBId, waveB);
            material.SetFloat(DepthId, depth);
            material.SetFloat(IorId, refractionIndex);
            material.SetFloat(ChromaId, chromaticAberration);
            material.SetFloat(FillGapId, fillGap);
            material.SetFloat(BrightnessId, brightness);
            material.SetFloat(GammaId, gamma);
            material.SetFloat(ClampId, clamp);
            material.SetFloat(EpsId, 1.5f / rt.width);

            Graphics.Blit(null, rt, material);

            if (!string.IsNullOrEmpty(globalTextureName))
                Shader.SetGlobalTexture(globalTextureName, rt);
        }

        void OnValidate()
        {
            if (waves != null && waves.Length > MaxWaves)
                Array.Resize(ref waves, MaxWaves);
        }
    }
}

using UnityEngine;

namespace UnderwaterFX
{
    /// <summary>
    /// Define la zona del agua y los parametros de causticas y niebla submarina.
    /// El Transform de este objeto es el volumen (cubo unitario escalado).
    /// La superficie del agua es la cara superior del volumen (mas el offset).
    /// </summary>
    [ExecuteAlways]
    public class WaterCausticsVolume : MonoBehaviour
    {
        public static WaterCausticsVolume Active { get; private set; }

        [Header("Volumen")]
        [Tooltip("Ajuste fino de la altura de la superficie respecto a la cara superior del volumen.")]
        public float surfaceOffset = 0f;
        [Range(0f, 0.5f)] public float edgeFade = 0.05f;

        [Header("Causticas")]
        [Tooltip("Unidades de mundo que cubre una repeticion de la textura.")]
        public float scale = 5f;
        [Range(0f, 10f)] public float intensity = 1.5f;
        [Tooltip("Desfase por canal de color (X, Z). Extra a la aberracion cromatica de la textura.")]
        public Vector2 colorShift = Vector2.zero;
        [Range(0f, 1f)] public float lightColorSaturation = 0.5f;

        [Header("Atenuacion de causticas")]
        public bool nearSurfaceAttenuation = true;
        [Min(0.01f)] public float attenuationWidth = 0.35f;
        [Range(0f, 1f)] public float normalAttenuation = 1f;
        public bool receiveShadows = true;

        [Header("Niebla submarina")]
        public bool underwaterFog = true;
        [Tooltip("Absorcion por canal RGB (por unidad de mundo). El rojo se pierde primero.")]
        public Vector3 absorption = new Vector3(0.45f, 0.07f, 0.03f);
        [Tooltip("Multiplica la absorcion. Mas alto = menos visibilidad.")]
        [Min(0f)] public float fogDensity = 0.15f;
        public Color fogColor = new Color(0.02f, 0.22f, 0.32f, 1f);
        [Tooltip("Cuanto se oscurece el ambiente al bajar la camara.")]
        [Min(0f)] public float depthDarkening = 0.03f;

        [Header("Rayos de luz (god rays)")]
        public bool godRays = true;
        [Tooltip("Brillo de los rayos. Sube si no se ven, baja si queman la imagen.")]
        [Range(0f, 5f)] public float godRayIntensity = 0.5f;
        [Tooltip("Pasos de raymarching. Mas pasos = menos bandas pero mas costo.")]
        [Range(8, 64)] public int godRaySteps = 24;
        [Min(1f)] public float godRayMaxDistance = 60f;
        [Tooltip("Cuanto se concentran los rayos al mirar hacia la luz (0 = parejo).")]
        [Range(0f, 0.95f)] public float godRayAnisotropy = 0.6f;
        public Color godRayColor = new Color(0.7f, 0.9f, 1f, 1f);
        [Tooltip("Cuanto modulan las ondas de la superficie a los haces (0 = continuos, 1 = muy marcados).")]
        [Range(0f, 1f)] public float godRayPattern = 0.7f;

        public float SurfaceY => transform.position.y + transform.lossyScale.y * 0.5f + surfaceOffset;

        void OnEnable() { Active = this; }

        void OnDisable()
        {
            if (Active == this) Active = null;
        }

        public void ApplyTo(Material m)
        {
            m.SetMatrix("_CausticsWorldToVolume", transform.worldToLocalMatrix);
            m.SetFloat("_CausticsScale", scale);
            m.SetFloat("_SurfaceY", SurfaceY);
            m.SetFloat("_NearSurface", nearSurfaceAttenuation ? 1f : 0f);
            m.SetFloat("_NearSurfaceWidth", attenuationWidth);
            m.SetFloat("_CausticsIntensity", intensity);
            m.SetVector("_ColorShift", colorShift);
            m.SetFloat("_LightSaturation", lightColorSaturation);
            m.SetFloat("_NormalAtten", normalAttenuation);
            m.SetFloat("_ReceiveShadows", receiveShadows ? 1f : 0f);
            m.SetFloat("_EdgeFade", edgeFade);
        }

        public void ApplyFogTo(Material m)
        {
            Color fc = fogColor.linear;
            m.SetFloat("_UWSurfaceY", SurfaceY);
            m.SetVector("_UWAbsorption", absorption * fogDensity);
            m.SetVector("_UWFogColor", new Vector4(fc.r, fc.g, fc.b, 1f));
            m.SetFloat("_UWDepthFalloff", depthDarkening);
        }

        public void ApplyGodRaysTo(Material m)
        {
            Color c = godRayColor.linear;
            m.SetFloat("_GRSurfaceY", SurfaceY);
            m.SetFloat("_GRIntensity", godRayIntensity);
            m.SetFloat("_GRSteps", godRaySteps);
            m.SetFloat("_GRMaxDist", godRayMaxDistance);
            m.SetFloat("_GRAniso", godRayAnisotropy);
            m.SetVector("_GRColor", new Vector4(c.r, c.g, c.b, 1f));
            m.SetFloat("_GRPattern", godRayPattern);
            m.SetFloat("_GRScale", scale);
            m.SetVector("_GRAbsorption", absorption * fogDensity);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0.3f, 0.7f, 1f, 1f);
            Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
        }
    }
}

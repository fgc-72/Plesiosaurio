using UnityEngine;

namespace UnderwaterFX
{
    /// <summary>
    /// Define la zona donde se ven las caustics y sus parametros.
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

        [Header("Atenuacion")]
        public bool nearSurfaceAttenuation = true;
        [Min(0.01f)] public float attenuationWidth = 0.35f;
        [Range(0f, 1f)] public float normalAttenuation = 1f;
        public bool receiveShadows = true;

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

        void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0.3f, 0.7f, 1f, 1f);
            Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
        }
    }
}

using UnityEngine;

namespace UnderwaterFX
{
    /// <summary>
    /// Activa y desactiva la lluvia en el mar con una transicion suave.
    /// Escribe el global "_RainIntensity" (0..1) que lee el shader de la superficie
    /// (ondas mas fuertes, anillos de gotas, cielo gris) y ajusta la luz, la niebla y los
    /// rayos de luz. Al desactivarse restaura los valores originales.
    ///
    /// Uso: marca "Raining" en el Inspector (tambien en Play) o llama a SetRaining(bool)
    /// / ToggleRain() desde tu codigo o desde un boton de UI.
    /// </summary>
    public class WaterRainController : MonoBehaviour
    {
        static readonly int RainId = Shader.PropertyToID("_RainIntensity");

        [Header("Estado")]
        public bool raining = false;
        [Tooltip("Segundos que tarda en pasar de seco a lluvia completa (y al reves).")]
        [Min(0.1f)] public float transitionTime = 6f;
        [Range(0f, 1f)] public float maxIntensity = 1f;

        [Header("Luz")]
        [Tooltip("La Directional Light principal. Si se deja vacia se usa la luz solar de la escena.")]
        public Light sun;
        [Range(0.05f, 1f)] public float sunIntensityWhenRaining = 0.45f;

        [Header("Ambiente submarino")]
        [Tooltip("Si se deja vacio se usa el WaterCausticsVolume activo.")]
        public WaterCausticsVolume volume;
        public Color stormFogColor = new Color(0.04f, 0.09f, 0.11f, 1f);
        [Range(1f, 3f)] public float fogDensityMultiplier = 1.4f;
        [Range(0f, 1f)] public float godRayIntensityMultiplier = 0.35f;
        [Tooltip("Las causticas se debilitan con cielo nublado (ademas de bajar la luz y desenfocarse).")]
        [Range(0f, 1f)] public float causticsIntensityMultiplier = 0.5f;

        [Header("Opcional")]
        [Tooltip("Particulas de lluvia (visibles si la camara sale del agua).")]
        public ParticleSystem rainParticles;
        public AudioSource rainAudio;
        [Range(0f, 1f)] public float rainAudioVolume = 0.6f;

        public float CurrentIntensity { get; private set; }

        bool captured;
        float baseSunIntensity;
        Color baseFogColor;
        float baseFogDensity;
        float baseGodRayIntensity;
        float baseCausticsIntensity;
        float baseParticleRate;
        WaterCausticsVolume capturedVolume;
        Light capturedSun;

        public void SetRaining(bool value) { raining = value; }
        public void ToggleRain() { raining = !raining; }

        void OnEnable()
        {
            captured = false;
        }

        void OnDisable()
        {
            Restore();
            Shader.SetGlobalFloat(RainId, 0f);
        }

        void Update()
        {
            ResolveTargets();

            float target = raining ? maxIntensity : 0f;
            CurrentIntensity = Mathf.MoveTowards(CurrentIntensity, target, Time.deltaTime / Mathf.Max(0.1f, transitionTime));

            Shader.SetGlobalFloat(RainId, CurrentIntensity);
            Apply(Mathf.SmoothStep(0f, 1f, CurrentIntensity));
        }

        void ResolveTargets()
        {
            Light currentSun = sun != null ? sun : RenderSettings.sun;
            WaterCausticsVolume currentVolume = volume != null ? volume : WaterCausticsVolume.Active;

            // Si cambia el objetivo (o es la primera vez) se guardan sus valores base.
            if (!captured || currentSun != capturedSun || currentVolume != capturedVolume)
            {
                Restore();
                capturedSun = currentSun;
                capturedVolume = currentVolume;

                if (capturedSun != null) baseSunIntensity = capturedSun.intensity;
                if (capturedVolume != null)
                {
                    baseFogColor = capturedVolume.fogColor;
                    baseFogDensity = capturedVolume.fogDensity;
                    baseGodRayIntensity = capturedVolume.godRayIntensity;
                    baseCausticsIntensity = capturedVolume.intensity;
                }
                if (rainParticles != null)
                    baseParticleRate = rainParticles.emission.rateOverTimeMultiplier;

                captured = true;
            }
        }

        void Apply(float k)
        {
            if (capturedSun != null)
                capturedSun.intensity = Mathf.Lerp(baseSunIntensity, baseSunIntensity * sunIntensityWhenRaining, k);

            if (capturedVolume != null)
            {
                capturedVolume.fogColor = Color.Lerp(baseFogColor, stormFogColor, k);
                capturedVolume.fogDensity = Mathf.Lerp(baseFogDensity, baseFogDensity * fogDensityMultiplier, k);
                capturedVolume.godRayIntensity = Mathf.Lerp(baseGodRayIntensity, baseGodRayIntensity * godRayIntensityMultiplier, k);
                capturedVolume.intensity = Mathf.Lerp(baseCausticsIntensity, baseCausticsIntensity * causticsIntensityMultiplier, k);
            }

            if (rainParticles != null)
            {
                var emission = rainParticles.emission;
                emission.rateOverTimeMultiplier = baseParticleRate * k;
                if (k > 0.001f && !rainParticles.isPlaying) rainParticles.Play();
                else if (k <= 0.001f && rainParticles.isPlaying) rainParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }

            if (rainAudio != null)
            {
                rainAudio.volume = rainAudioVolume * k;
                if (k > 0.001f && !rainAudio.isPlaying) rainAudio.Play();
                else if (k <= 0.001f && rainAudio.isPlaying) rainAudio.Stop();
            }
        }

        void Restore()
        {
            if (!captured) return;

            if (capturedSun != null) capturedSun.intensity = baseSunIntensity;
            if (capturedVolume != null)
            {
                capturedVolume.fogColor = baseFogColor;
                capturedVolume.fogDensity = baseFogDensity;
                capturedVolume.godRayIntensity = baseGodRayIntensity;
                capturedVolume.intensity = baseCausticsIntensity;
            }
            if (rainParticles != null)
            {
                var emission = rainParticles.emission;
                emission.rateOverTimeMultiplier = baseParticleRate;
            }
            captured = false;
        }
    }
}

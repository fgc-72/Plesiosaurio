using UnityEngine;

public class ListenModeVisualizer : MonoBehaviour
{
    public enum FrequencyFocusWindow
    {
        Entire = 1,
        FirstHalf = 2,
        FirstQuarter = 4,
        FirstEighth = 8,
        FirstSixteenth = 16
    }

    [Header("Referencias")]
    [SerializeField] private ListenableSoundRegistry registry;
    [SerializeField] private RectTransform[] bars;
    [SerializeField] private Transform listener; // Nami o la cámara

    [Header("Ajustes")]
    [SerializeField] private FrequencyFocusWindow frequencyFocus = FrequencyFocusWindow.FirstQuarter;
    [SerializeField] private FFTWindow fftWindow = FFTWindow.BlackmanHarris;
    [SerializeField] private float amplification = 800f;
    [SerializeField] private float baseHeight = 0.05f;
    [SerializeField] private float smoothing = 12f;
    [SerializeField] private float maxRange = 40f;

    [Header("Debug")]
    [SerializeField] private bool debugLog = false;

    // Debe ser potencia de 2 entre 64 y 8192
    private readonly float[] spectrum = new float[1024];
    private float nextLogTime;

    private void Start()
    {
        if (registry == null) Debug.LogWarning("[Visualizer] Falta asignar el Registry.");
        if (listener == null) Debug.LogWarning("[Visualizer] Falta asignar el Listener.");
        if (bars == null || bars.Length == 0) Debug.LogWarning("[Visualizer] La lista Bars está vacía.");
    }

    private void Update()
    {
        if (bars == null || bars.Length == 0) return;

        float proximity = 0f;
        AudioSource nearest = FindNearest(out float distance);

        if (nearest != null)
        {
            nearest.GetSpectrumData(spectrum, 0, fftWindow);
            proximity = 1f - Mathf.Clamp01(distance / maxRange);
        }
        else
        {
            System.Array.Clear(spectrum, 0, spectrum.Length);
        }

        if (debugLog && Time.time >= nextLogTime)
        {
            nextLogTime = Time.time + 1f;

            float peak = 0f;
            for (int k = 0; k < spectrum.Length; k++)
                if (spectrum[k] > peak) peak = spectrum[k];

            int total = registry != null ? registry.Sources.Count : -1;
            string nearestName = nearest != null ? nearest.name : "NINGUNA";

            Debug.Log($"[Visualizer] fuentes en registro: {total} | más cercana: {nearestName} | distancia: {distance:F1} | pico del espectro: {peak:F5}");
        }

        int count = Mathf.Min(bars.Length, spectrum.Length);
        int blockSize = Mathf.Max(1, spectrum.Length / count / (int)frequencyFocus);

        for (int i = 0; i < count; i++)
        {
            float sum = 0f;
            for (int j = 0; j < blockSize; j++)
                sum += spectrum[i * blockSize + j];
            sum /= blockSize;

            float target = baseHeight + sum * amplification * proximity;

            Vector3 scale = bars[i].localScale;
            scale.y = Mathf.Lerp(scale.y, target, smoothing * Time.deltaTime);
            bars[i].localScale = scale;
        }
    }

    private AudioSource FindNearest(out float distance)
    {
        distance = float.MaxValue;
        if (listener == null || registry == null) return null;

        AudioSource best = null;

        foreach (AudioSource s in registry.Sources)
        {
            // Puede haberse destruido entre un escaneo y otro
            if (s == null || !s.isPlaying) continue;

            float d = Vector3.Distance(listener.position, s.transform.position);
            if (d <= maxRange && d < distance)
            {
                best = s;
                distance = d;
            }
        }

        return best;
    }
}
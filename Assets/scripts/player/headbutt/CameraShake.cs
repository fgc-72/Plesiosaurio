using UnityEngine;

[DefaultExecutionOrder(10000)] // corre después de Cinemachine
public class CameraShake : MonoBehaviour
{
    private static CameraShake instance;

    [Tooltip("Grados de rotación por cada 1.0 de intensidad. Esto es lo que se nota de verdad. " +
             "Con intensidad 0.5 y valor 4, la cámara se sacude ~2 grados.")]
    [SerializeField] private float rotationDegreesPerUnit = 4f;
    [Tooltip("Cuánto del shake va a la posición (en metros por 1.0 de intensidad). 0 = solo rotación.")]
    [SerializeField] private float positionMultiplier = 1f;
    [Tooltip("Qué tan rápido vibra (más alto = más nervioso).")]
    [SerializeField] private float frequency = 25f;

    private float duration;
    private float elapsed;
    private float magnitude;
    private float seed;

    private Vector3 lastPositionOffset;
    private Quaternion lastRotationOffset = Quaternion.identity;

    public static void Shake(float duration, float magnitude)
    {
        if (instance == null)
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                Debug.LogWarning("CameraShake: no hay Camera.main (¿falta el tag MainCamera?)");
                return;
            }

            instance = cam.GetComponent<CameraShake>();
            if (instance == null)
                instance = cam.gameObject.AddComponent<CameraShake>();
        }

        instance.Begin(duration, magnitude);
    }

    private void Begin(float newDuration, float newMagnitude)
    {
        // Un shake chiquito no debe pisar a uno grande que sigue en curso.
        float remaining = duration <= 0f ? 0f : magnitude * (1f - elapsed / duration);
        if (newMagnitude < remaining) return;

        duration = newDuration;
        magnitude = newMagnitude;
        elapsed = 0f;
        seed = Random.value * 100f;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private void Update()
    {
        // Deshacemos lo del frame anterior antes de que Cinemachine reposicione la cámara.
        transform.position -= lastPositionOffset;
        transform.rotation = transform.rotation * Quaternion.Inverse(lastRotationOffset);

        lastPositionOffset = Vector3.zero;
        lastRotationOffset = Quaternion.identity;
    }

    private void LateUpdate()
    {
        if (elapsed >= duration) return;

        elapsed += Time.unscaledDeltaTime;
        float amplitude = magnitude * (1f - Mathf.Clamp01(elapsed / duration));
        float t = elapsed * frequency;

        float nx = Noise(0f, t);
        float ny = Noise(1f, t);
        float nz = Noise(2f, t);

        float degrees = amplitude * rotationDegreesPerUnit;

        lastPositionOffset = (transform.right * nx + transform.up * ny) * amplitude * positionMultiplier;
        lastRotationOffset = Quaternion.Euler(ny * degrees, nx * degrees, nz * degrees * 0.5f);

        transform.position += lastPositionOffset;
        transform.rotation = transform.rotation * lastRotationOffset;
    }

    // Ruido suave entre -1 y 1, un canal distinto por eje.
    private float Noise(float channel, float t)
    {
        return (Mathf.PerlinNoise(seed + channel * 17.3f, t) - 0.5f) * 2f;
    }
}
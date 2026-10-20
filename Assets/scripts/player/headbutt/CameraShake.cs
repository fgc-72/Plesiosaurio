using UnityEngine;

[DefaultExecutionOrder(10000)] // corre al final, después de que la cámara ya se posicionó
public class CameraShake : MonoBehaviour
{
    private static CameraShake instance;

    private float duration;
    private float elapsed;
    private float magnitude;
    private Vector3 lastOffset;

    public static void Shake(float duration, float magnitude)
    {
        if (instance == null)
        {
            Camera cam = Camera.main;
            if (cam == null) return;

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
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private void Update()
    {
        // Quitamos el offset del frame anterior antes de que la cámara se actualice.
        transform.position -= lastOffset;
        lastOffset = Vector3.zero;
    }

    private void LateUpdate()
    {
        if (elapsed >= duration) return;

        elapsed += Time.unscaledDeltaTime;
        float falloff = 1f - Mathf.Clamp01(elapsed / duration);

        Vector2 random = Random.insideUnitCircle * magnitude * falloff;
        lastOffset = transform.right * random.x + transform.up * random.y;
        transform.position += lastOffset;
    }
}
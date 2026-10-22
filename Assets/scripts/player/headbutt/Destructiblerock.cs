using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class DestructibleRock : MonoBehaviour
{
    [Tooltip("Cuántos cabezazos aguanta esta roca antes de romperse.")]
    [SerializeField] private int hitsToBreak = 1;

    [Header("Animación de rotura")]
    [Tooltip("Duración total de la animación antes de destruir el objeto.")]
    [SerializeField] private float breakDuration = 0.6f;
    [Tooltip("Escala máxima del 'pop' inicial (1 = sin pop).")]
    [SerializeField] private float popScale = 1.15f;
    [Tooltip("Qué fracción de la duración dura el pop de ida antes de encogerse.")]
    [Range(0.05f, 0.9f)]
    [SerializeField] private float popPortion = 0.2f;
    [SerializeField] private int debrisCount = 8;
    [Tooltip("Tamaño de cada fragmento como fracción del tamaño de la roca (mín, máx).")]
    [SerializeField] private Vector2 debrisSizeRange = new Vector2(0.08f, 0.2f);
    [SerializeField] private float debrisSpeed = 4f;
    [Tooltip("Qué tan rápido se frenan los fragmentos (sensación de estar bajo el agua).")]
    [SerializeField] private float debrisDrag = 3f;
    [SerializeField] private float debrisSpin = 360f;

    [Header("Shake de cámara")]
    [SerializeField] private float shakeDuration = 0.25f;
    [Tooltip("Intensidad cuando el jugador la rompe con cabezazo.")]
    [SerializeField] private float playerBreakShake = 0.15f;
    [Tooltip("Intensidad cuando se rompe sola (le llegó a Nami / a Mara). Pon 0 para desactivar.")]
    [SerializeField] private float forcedBreakShake = 0.08f;
    [Tooltip("Más lejos de la cámara que esto, el shake es 0 (así una roca lejana no sacude la pantalla).")]
    [SerializeField] private float shakeMaxDistance = 30f;

    [Header("Eventos")]
    [Tooltip("Se dispara SIEMPRE que la roca se rompe.")]
    [SerializeField] private UnityEvent onBroken;
    [Tooltip("Se dispara SOLO cuando el jugador la rompió a cabezazos.")]
    [SerializeField] private UnityEvent onBrokenByPlayer;

    public event System.Action BrokenByPlayer;

    private int currentHits;
    private bool isBroken;

    public bool IsBroken => isBroken;

    public void TakeHit()
    {
        if (isBroken) return;

        currentHits++;
        Debug.Log($"🪨 Roca golpeada: {currentHits}/{hitsToBreak}"); // TEMPORAL

        if (currentHits >= hitsToBreak)
            Break(wasPlayerAction: true);
    }

    public void ForceBreak()
    {
        if (isBroken) return;
        Break(wasPlayerAction: false);
    }

    private void Break(bool wasPlayerAction)
    {
        isBroken = true;

        onBroken?.Invoke();

        if (wasPlayerAction)
        {
            onBrokenByPlayer?.Invoke();
            BrokenByPlayer?.Invoke();
        }

        ShakeCamera(wasPlayerAction ? playerBreakShake : forcedBreakShake, applyDistanceFalloff: !wasPlayerAction);

        StartCoroutine(BreakRoutine());
    }

    private void ShakeCamera(float magnitude, bool applyDistanceFalloff)
    {
        if (magnitude <= 0f) return;

        Camera cam = Camera.main;
        if (cam == null) return;

        if (applyDistanceFalloff)
        {
            float distance = Vector3.Distance(cam.transform.position, transform.position);
            float falloff = 1f - Mathf.Clamp01(distance / shakeMaxDistance);
            if (falloff <= 0f) return;
            magnitude *= falloff;
        }

        CameraShake.Shake(shakeDuration, magnitude);
    }

    private IEnumerator BreakRoutine()
    {
        // Ya no debe poder golpearse ni golpear nada.
        foreach (Collider c in GetComponentsInChildren<Collider>())
            c.enabled = false;

        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        Material debrisMaterial = renderers.Length > 0 ? renderers[0].sharedMaterial : null;

        Bounds bounds = transform.position == Vector3.zero && renderers.Length == 0
            ? new Bounds(transform.position, Vector3.one)
            : new Bounds(transform.position, Vector3.zero);
        if (renderers.Length > 0)
        {
            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
        }

        float rockSize = Mathf.Max(0.1f, bounds.size.magnitude);

        // Fragmentos
        Transform[] pieces = new Transform[debrisCount];
        Vector3[] velocities = new Vector3[debrisCount];
        Vector3[] spins = new Vector3[debrisCount];
        Vector3[] startScales = new Vector3[debrisCount];

        for (int i = 0; i < debrisCount; i++)
        {
            GameObject piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(piece.GetComponent<Collider>());

            if (debrisMaterial != null)
                piece.GetComponent<Renderer>().sharedMaterial = debrisMaterial;

            Vector3 offset = Vector3.Scale(Random.insideUnitSphere, bounds.extents);
            piece.transform.position = bounds.center + offset;
            piece.transform.rotation = Random.rotation;

            float s = rockSize * Random.Range(debrisSizeRange.x, debrisSizeRange.y);
            startScales[i] = new Vector3(s, s * Random.Range(0.6f, 1f), s * Random.Range(0.6f, 1f));
            piece.transform.localScale = startScales[i];

            Vector3 dir = offset.sqrMagnitude > 0.0001f ? offset.normalized : Random.onUnitSphere;
            velocities[i] = dir * debrisSpeed * Random.Range(0.5f, 1.2f);
            spins[i] = Random.onUnitSphere * debrisSpin * Random.Range(0.5f, 1f);

            pieces[i] = piece.transform;
        }

        // Animación
        Vector3 originalScale = transform.localScale;
        float elapsed = 0f;

        while (elapsed < breakDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / breakDuration);

            // Roca: pop hacia afuera y luego se encoge a 0.
            float scaleFactor = t < popPortion
                ? Mathf.Lerp(1f, popScale, t / popPortion)
                : Mathf.Lerp(popScale, 0f, Mathf.SmoothStep(0f, 1f, (t - popPortion) / (1f - popPortion)));
            transform.localScale = originalScale * scaleFactor;

            // Fragmentos: salen disparados, se frenan, giran y se encogen.
            for (int i = 0; i < pieces.Length; i++)
            {
                if (pieces[i] == null) continue;

                velocities[i] = Vector3.Lerp(velocities[i], Vector3.zero, debrisDrag * Time.deltaTime);
                pieces[i].position += velocities[i] * Time.deltaTime;
                pieces[i].Rotate(spins[i] * Time.deltaTime, Space.Self);
                pieces[i].localScale = startScales[i] * (1f - t);
            }

            yield return null;
        }

        foreach (Transform p in pieces)
            if (p != null) Destroy(p.gameObject);

        Destroy(gameObject);
    }
}
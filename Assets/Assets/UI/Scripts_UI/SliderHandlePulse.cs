using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Ponlo en el mismo objeto que el Slider.
/// Mientras el slider está seleccionado, su handle "palpita" (escala un poco y vuelve a la normal en loop).
/// Al deseleccionarse, vuelve suavemente a su escala base.
/// </summary>
[RequireComponent(typeof(Slider))]
public class SliderHandlePulse : MonoBehaviour, ISelectHandler, IDeselectHandler
{
    [SerializeField] private float pulseAmount = 0.15f;   // 0.15 = hasta +15% de escala
    [SerializeField] private float pulseSpeed  = 1.6f;    // pulsos por segundo
    [SerializeField] private float blendSpeed  = 12f;     // qué tan rápido entra/sale del efecto

    private RectTransform handle;
    private Vector3 baseScale = Vector3.one;
    private bool selected;
    private float pulseTime;
    private float intensity;   // 0 = sin efecto, 1 = efecto completo

    private void Awake()
    {
        var slider = GetComponent<Slider>();
        handle = slider.handleRect;
        if (handle != null) baseScale = handle.localScale;
    }

    private void OnDisable()
    {
        selected = false;
        intensity = 0f;
        pulseTime = 0f;
        if (handle != null) handle.localScale = baseScale;
    }

    public void OnSelect(BaseEventData eventData)
    {
        selected = true;
        pulseTime = 0f; // empieza en la escala normal
    }

    public void OnDeselect(BaseEventData eventData)
    {
        selected = false;
    }

    private void Update()
    {
        if (handle == null) return;

        float dt = Time.unscaledDeltaTime; // funciona con Time.timeScale = 0
        float k = 1f - Mathf.Exp(-blendSpeed * dt);
        intensity = Mathf.Lerp(intensity, selected ? 1f : 0f, k);

        if (selected) pulseTime += dt;

        // Oscila entre 0 y 1, empezando en 0 (escala normal).
        float wave = 0.5f + 0.5f * Mathf.Sin(pulseTime * pulseSpeed * Mathf.PI * 2f - Mathf.PI * 0.5f);

        handle.localScale = baseScale * (1f + pulseAmount * wave * intensity);
    }
}

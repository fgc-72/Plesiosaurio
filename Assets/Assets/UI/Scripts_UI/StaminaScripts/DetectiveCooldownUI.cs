using UnityEngine;
using UnityEngine.UI;

public class DetectiveCooldownUI : MonoBehaviour
{
    [SerializeField] private Image fillImage;
    [SerializeField] private CanvasGroup canvasGroup; // opcional

    [Header("Apariencia")]
    [SerializeField] private float fillSmoothSpeed = 10f;
    [Range(0.1f, 1f)] [SerializeField] private float arcAmount = 1f;
    [SerializeField] private Color activeColor = new Color(0.4f, 0.8f, 1f);   // escuchando (se vacía)
    [SerializeField] private Color cooldownColor = new Color(0.5f, 0.5f, 0.5f); // recargando (se llena)
    [SerializeField] private Color readyColor = Color.white;                    // listo

    [Header("Visibilidad")]
    [SerializeField] private bool hideWhenReady = true;
    [SerializeField] private float fadeSpeed = 4f;

    private float displayedFill = 1f;

    private void Update()
    {
        DetectiveController detective = DetectiveController.Instance;
        if (detective == null) return;

        float target;
        Color color;
        bool visible;

        if (detective.IsOnCooldown)
        {
            target = detective.GetCooldownProgress(); // 0 → 1
            color = cooldownColor;
            visible = true;
        }
        else if (detective.IsActive)
        {
            target = detective.GetActiveRemainingNormalized(); // 1 → 0
            color = activeColor;
            visible = true;
        }
        else
        {
            target = 1f;
            color = readyColor;
            visible = !hideWhenReady;
        }

        displayedFill = Mathf.Lerp(displayedFill, target, fillSmoothSpeed * Time.deltaTime);
        fillImage.fillAmount = displayedFill * arcAmount;
        fillImage.color = color;

        if (canvasGroup != null)
        {
            canvasGroup.alpha = Mathf.MoveTowards(
                canvasGroup.alpha, visible ? 1f : 0f, fadeSpeed * Time.deltaTime);
        }
    }
}
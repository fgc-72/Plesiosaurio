using UnityEngine;
using UnityEngine.UI;

public class StaminaBarUI : MonoBehaviour
{
    [SerializeField] private PlayerControls player; // ya no hace falta asignarlo a mano
    [SerializeField] private Image fillImage;
    [SerializeField] private CanvasGroup canvasGroup; // opcional

    [Header("Búsqueda del Player")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private float retryInterval = 0.5f;

    [Header("Apariencia")]
    [SerializeField] private float fillSmoothSpeed = 10f;
    [Range(0.1f, 1f)] [SerializeField] private float arcAmount = 1f;
    [SerializeField] private Gradient colorByStamina;
    [SerializeField] private bool hideWhenFull = true;
    [SerializeField] private float fadeSpeed = 4f;

    private float displayedFill = 1f;
    private float nextSearchTime;

    private void Update()
    {
        // Unity sobrecarga == para objetos destruidos, así que esto también
        // detecta al Player de la escena anterior que ya no existe.
        if (player == null)
        {
            TryFindPlayer();
            if (player == null) return;
        }

        float target = player.GetStaminaNormalized();
        displayedFill = Mathf.Lerp(displayedFill, target, fillSmoothSpeed * Time.deltaTime);

        fillImage.fillAmount = displayedFill * arcAmount;
        fillImage.color = colorByStamina.Evaluate(displayedFill);

        if (hideWhenFull && canvasGroup != null)
        {
            float targetAlpha = target >= 0.999f ? 0f : 1f;
            canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, targetAlpha, fadeSpeed * Time.deltaTime);
        }
    }

    private void TryFindPlayer()
    {
        // Reintenta cada cierto tiempo en vez de cada frame
        if (Time.unscaledTime < nextSearchTime) return;
        nextSearchTime = Time.unscaledTime + retryInterval;

        GameObject go = GameObject.FindGameObjectWithTag(playerTag);
        if (go == null) return;

        // Por si el script está en el objeto con el tag o en uno de sus hijos/padres
        if (!go.TryGetComponent(out player))
            player = go.GetComponentInChildren<PlayerControls>();
        if (player == null)
            player = go.GetComponentInParent<PlayerControls>();
    }
}
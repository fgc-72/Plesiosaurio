using UnityEngine;
using UnityEngine.UI;
using System.Collections;

// Pantalla negra simple y reutilizable (CanvasGroup) para momentos
// cinematográficos DENTRO de la misma escena — a diferencia de SceneFader,
// que es específicamente para tapar un cambio de escena. Se autoconstruye su
// propio Canvas, así no hay que armarlo a mano cada vez que se necesita.
public class ScreenBlackoutEffect : MonoBehaviour
{
    [SerializeField] private float fadeDuration = 1f;

    private CanvasGroup canvasGroup;

    public float FadeDuration => fadeDuration;

    private void Awake()
    {
        BuildCanvas();
    }

    private void BuildCanvas()
    {
        if (canvasGroup != null) return;

        GameObject canvasObject = new GameObject("BlackoutCanvas");
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000; // por encima de todo lo demás

        canvasObject.AddComponent<CanvasScaler>();

        GameObject imageObject = new GameObject("Black");
        imageObject.transform.SetParent(canvasObject.transform, false);

        Image image = imageObject.AddComponent<Image>();
        image.color = Color.black;

        RectTransform rect = image.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        canvasGroup = canvasObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
    }

    public static ScreenBlackoutEffect GetOrCreate()
    {
        ScreenBlackoutEffect instance = Object.FindFirstObjectByType<ScreenBlackoutEffect>();
        if (instance == null)
        {
            GameObject go = new GameObject("ScreenBlackoutEffect");
            instance = go.AddComponent<ScreenBlackoutEffect>();
        }
        return instance;
    }

    public Coroutine FadeToBlack(float overrideDuration = -1f) => StartCoroutine(Fade(1f, overrideDuration));
    public Coroutine FadeFromBlack(float overrideDuration = -1f) => StartCoroutine(Fade(0f, overrideDuration));

    private IEnumerator Fade(float targetAlpha, float overrideDuration = -1f)
    {
        if (canvasGroup == null) BuildCanvas();
        float duration = overrideDuration > 0f ? overrideDuration : fadeDuration;
        float startAlpha = canvasGroup.alpha;
        float elapsed = 0f;

        // Time.unscaledDeltaTime: así el fade funciona aunque en el futuro
        // pausen el juego con Time.timeScale = 0 durante la cinemática.
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / duration);
            yield return null;
        }

        canvasGroup.alpha = targetAlpha;
    }
}

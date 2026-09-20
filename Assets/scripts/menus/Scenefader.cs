using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;

// Singleton que persiste entre escenas y envuelve cualquier carga de escena
// con un fade a negro, para que nunca haya un corte brusco entre pantallas.
// Se auto-construye su propio Canvas, así no hay que armarlo a mano en el editor.
public class SceneFader : MonoBehaviour
{
    public static SceneFader Instance { get; private set; }

    [SerializeField] private float fadeDuration = 1f;

    private CanvasGroup fadeCanvasGroup;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        BuildFadeCanvas();
    }

    private void BuildFadeCanvas()
    {
        GameObject canvasObj = new GameObject("SceneFadeCanvas");
        canvasObj.transform.SetParent(transform);

        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999; // siempre por encima de cualquier otro Canvas

        canvasObj.AddComponent<CanvasScaler>();
        canvasObj.AddComponent<GraphicRaycaster>();

        GameObject imageObj = new GameObject("FadeImage");
        imageObj.transform.SetParent(canvasObj.transform, false);

        Image image = imageObj.AddComponent<Image>();
        image.color = Color.black;

        RectTransform rt = image.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        fadeCanvasGroup = canvasObj.AddComponent<CanvasGroup>();
        fadeCanvasGroup.alpha = 0f;
        fadeCanvasGroup.blocksRaycasts = false;
    }

    public void LoadScene(string sceneName)
    {
        StartCoroutine(FadeAndLoad(sceneName));
    }

    private IEnumerator FadeAndLoad(string sceneName)
    {
        yield return Fade(0f, 1f); // a negro

        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
        while (!operation.isDone)
            yield return null;

        yield return Fade(1f, 0f); // de negro a la nueva escena
    }

    private IEnumerator Fade(float from, float to)
    {
        fadeCanvasGroup.blocksRaycasts = true; // evita clicks accidentales durante el fade
        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime; // no depende de Time.timeScale
            fadeCanvasGroup.alpha = Mathf.Lerp(from, to, elapsed / fadeDuration);
            yield return null;
        }

        fadeCanvasGroup.alpha = to;
        fadeCanvasGroup.blocksRaycasts = to > 0.99f;
    }
}
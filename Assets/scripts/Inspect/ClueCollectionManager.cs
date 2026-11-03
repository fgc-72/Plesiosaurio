using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// Cuenta las pistas que el jugador inspecciona (InspectViewer) y lo muestra en el texto de la
// misión ("Busca pistas (0/4)"). Al juntarlas todas corre el final de la demo:
//   fundido a negro -> (en negro) se enciende el panel de fondo, que tapa todo ->
//   vuelve de negro y sale el diálogo -> "Continuará..." -> créditos -> "Fin" -> menú.
// La cámara NO cambia: el final termina con la que esté activa en ese momento.
// Va en un GameObject vacío de la escena (ej. "ClueDirector").
public class ClueCollectionManager : MonoBehaviour
{
    [Header("Objetivo")]
    [SerializeField] private int requiredClues = 4;

    [Header("Pistas (objetos que se inspeccionan con InspectViewer)")]
    [Tooltip("Objetos que cuentan como pista. Si lo dejas VACÍO, cuenta cualquier InspectableObject " +
             "que el jugador inspeccione. Cada objeto suma solo la primera vez.")]
    [SerializeField] private InspectableObject[] clues;

    [Header("Misión (ID en MissionDatabase)")]
    [SerializeField] private string missionId;
    [Tooltip("Si es true, el contador sale apenas carga la escena. Si es false, sale cuando " +
             "llames a ShowCounter() (por ejemplo desde ClueCounterTrigger).")]
    [SerializeField] private bool showCounterOnStart = true;

    [Header("Final: efectos")]
    [Tooltip("Si lo dejas vacío, se busca un ScreenBlackoutEffect en la escena.")]
    [SerializeField] private ScreenBlackoutEffect blackout;
    [SerializeField] private float blackHoldSeconds = 0.3f;

    [Header("Final: diálogo")]
    [Tooltip("Diálogo final (ID en DialogueDatabase). Sale cuando vuelve de negro, sobre el panel.")]
    [SerializeField] private string endDialogueId;

    [Header("Panel de fondo (se enciende en negro, apenas termina el fundido)")]
    [Tooltip("Panel que TAPA todo el fondo. Déjalo DESACTIVADO al inicio. Aparece de golpe, mientras " +
             "la pantalla está en negro, y se queda hasta salir al menú. Su Canvas debe quedar " +
             "DEBAJO del Canvas del diálogo (menor Sort Order) para que el diálogo se vea encima.")]
    [SerializeField] private GameObject coverPanel;

    [Header("Textos finales (hijos del panel de fondo, DESACTIVADOS al inicio)")]
    [Tooltip("Primero sale 'Continuará...', se apaga, y aparece el siguiente.")]
    [SerializeField] private GameObject continuedObject;
    [SerializeField] private float continuedSeconds = 4f;
    [SerializeField] private GameObject creditsObject;
    [SerializeField] private float creditsSeconds = 10f;
    [Tooltip("'Fin' sale al final y se queda hasta ir al menú.")]
    [SerializeField] private GameObject finObject;
    [SerializeField] private float finSeconds = 4f;
    [Tooltip("Duración del fundido de entrada/salida de cada texto (si tiene CanvasGroup).")]
    [SerializeField] private float textFadeSeconds = 1f;

    [Header("Salida al menú")]
    [Tooltip("Nombre EXACTO de la escena del menú (debe estar en Build Profiles → Scene List). " +
             "Si no se puede cargar, se prueba con 'MENU' y 'MainMenu'.")]
    [SerializeField] private string mainMenuScene = "MENU";
    [Tooltip("Nombre de la música del menú en MusicManager. Vacío = no cambia la música.")]
    [SerializeField] private string menuMusicName = "MenuMusic";

    private readonly System.Collections.Generic.HashSet<InspectableObject> counted =
        new System.Collections.Generic.HashSet<InspectableObject>();
    private int clueCount;
    private bool finished;

    public int ClueCount => clueCount;

    private void OnEnable()
    {
        InspectViewer.Inspected += OnInspected;
    }

    private void OnDisable()
    {
        InspectViewer.Inspected -= OnInspected;
    }

    // Se llama cada vez que el jugador abre la inspección de un objeto.
    private void OnInspected(InspectableObject obj)
    {
        if (finished || obj == null) return;

        if (clues != null && clues.Length > 0 && System.Array.IndexOf(clues, obj) < 0)
            return; // no es una de las pistas

        if (!counted.Add(obj))
            return; // ya lo había mirado: no suma dos veces

        RegisterClue();
    }

    private void Start()
    {
        if (coverPanel != null) coverPanel.SetActive(false);
        if (continuedObject != null) continuedObject.SetActive(false);
        if (creditsObject != null) creditsObject.SetActive(false);
        if (finObject != null) finObject.SetActive(false);

        if (showCounterOnStart)
            ShowCounter();
    }

    // Muestra la misión con el contador al final de la pista, ej. "Busca pistas (0/4)".
    public void ShowCounter()
    {
        if (string.IsNullOrEmpty(missionId))
        {
            Debug.LogWarning("ClueCollectionManager: missionId está vacío en el Inspector.");
            return;
        }

        if (MissionUI.Instance == null)
        {
            Debug.LogWarning("ClueCollectionManager: no hay MissionUI.Instance en la escena.");
            return;
        }

        // SetProgress guarda el avance pero SOLO redibuja si esta misión ya es la activa.
        // Por eso: primero el avance, y si la misión todavía no está en pantalla, se muestra.
        MissionUI.Instance.SetProgress(missionId, clueCount, requiredClues);

        if (MissionState.CurrentMissionId != missionId)
            MissionUI.Instance.ShowMission(missionId);
    }

    public void RegisterClue()
    {
        if (finished) return;

        clueCount++;
        Debug.Log($"Pistas: {clueCount}/{requiredClues}");
        ShowCounter();

        if (clueCount >= requiredClues)
        {
            finished = true;
            StartCoroutine(EndSequence());
        }
    }

    private IEnumerator EndSequence()
    {
        // Si la última pista se acaba de abrir, esperamos a que el jugador cierre la vista
        // antes de empezar el final (si no, el fundido taparía el visor abierto).
        while (InspectViewer.IsOpen)
            yield return null;
        yield return null;

        // 1) Nami quieta.
        GameObject player = GameObject.FindWithTag("Player");
        PlayerControls controls = player != null ? player.GetComponent<PlayerControls>() : null;
        PlayerAnimation playerAnimation = player != null ? player.GetComponent<PlayerAnimation>() : null;
        Animator namiAnimator = player != null ? player.GetComponent<Animator>() : null;

        if (controls != null) controls.SetControlEnabled(false);
        if (playerAnimation != null) playerAnimation.SetAnimationOverride(true);
        if (namiAnimator != null) namiAnimator.SetInteger("AnimationState", 0);

        if (blackout == null) blackout = FindFirstObjectByType<ScreenBlackoutEffect>();

        // 2) A negro.
        if (blackout != null)
            yield return blackout.FadeToBlack();
        else
            Debug.LogWarning("ClueCollectionManager: no hay ScreenBlackoutEffect, no habrá fundidos.");

        // 3) Apenas termina el fundido, y con la pantalla aún en negro: el panel de fondo
        //    se enciende de golpe, así al volver de negro no se ve nada del escenario.
        if (coverPanel != null)
            yield return Show(coverPanel, 0f);
        else
            Debug.LogWarning("ClueCollectionManager: falta asignar 'Cover Panel'.");

        if (blackHoldSeconds > 0f)
            yield return new WaitForSecondsRealtime(blackHoldSeconds);

        // 4) Vuelve de negro: se ve el panel, no el juego.
        if (blackout != null)
            yield return blackout.FadeFromBlack();

        // 5) Diálogo sobre el panel. Al terminar se apaga solo y el panel se queda.
        yield return ShowDialogueAndWait(endDialogueId);

        // 6) "Continuará..." -> créditos -> "Fin".
        yield return ShowThenHide(continuedObject, continuedSeconds);
        yield return ShowThenHide(creditsObject, creditsSeconds);

        if (finObject != null)
        {
            yield return Show(finObject, textFadeSeconds);
            yield return new WaitForSecondsRealtime(finSeconds);
        }

        // 7) Menú principal.
        GoToMainMenu();
    }

    private void GoToMainMenu()
    {
        Time.timeScale = 1f;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        BigRockCinematic.DestroyPersisted();

        if (!string.IsNullOrEmpty(menuMusicName) && MusicManager.Instance != null)
            MusicManager.Instance.Play(menuMusicName, 0f);

        // Prueba el nombre del Inspector y, si no está en el Build, los nombres típicos.
        string[] candidates = { mainMenuScene, "MENU", "MainMenu" };
        foreach (string candidate in candidates)
        {
            if (!string.IsNullOrEmpty(candidate) && Application.CanStreamedLevelBeLoaded(candidate))
            {
                SceneManager.LoadScene(candidate);
                return;
            }
        }

        Debug.LogError($"ClueCollectionManager: no pude cargar la escena del menú ('{mainMenuScene}'). " +
                       "Revisa que el nombre sea EXACTO y que la escena esté en Build Profiles → Scene List.");
    }

    // Asegura que el objeto se vea de verdad: enciende también a sus padres si estaban
    // apagados, y activa el Canvas si estaba desactivado. (SetActive en un hijo no
    // muestra nada si el padre o el Canvas están apagados.)
    private static void EnsureVisible(GameObject target)
    {
        for (Transform t = target.transform; t != null; t = t.parent)
        {
            if (!t.gameObject.activeSelf)
                t.gameObject.SetActive(true);

            Canvas canvas = t.GetComponent<Canvas>();
            if (canvas != null && !canvas.enabled)
                canvas.enabled = true;
        }
    }

    // Enciende un objeto; si tiene CanvasGroup y fadeSeconds > 0, aparece con fundido.
    private IEnumerator Show(GameObject target, float fadeSeconds)
    {
        if (target == null) yield break;

        CanvasGroup group = target.GetComponent<CanvasGroup>();
        if (group != null) group.alpha = fadeSeconds > 0f ? 0f : 1f;

        EnsureVisible(target);

        if (group == null || fadeSeconds <= 0f)
            yield break;

        yield return Fade(group, 0f, 1f, fadeSeconds);
    }

    // Lo muestra, espera 'seconds', y lo apaga (con fundido si tiene CanvasGroup).
    private IEnumerator ShowThenHide(GameObject target, float seconds)
    {
        if (target == null) yield break;

        yield return Show(target, textFadeSeconds);
        yield return new WaitForSecondsRealtime(seconds);

        CanvasGroup group = target.GetComponent<CanvasGroup>();
        yield return Fade(group, 1f, 0f, textFadeSeconds);

        target.SetActive(false);
    }

    private static IEnumerator Fade(CanvasGroup group, float from, float to, float seconds)
    {
        if (group == null) yield break;

        if (seconds <= 0f)
        {
            group.alpha = to;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < seconds)
        {
            elapsed += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(from, to, elapsed / seconds);
            yield return null;
        }
        group.alpha = to;
    }

    // Muestra un diálogo y espera a que termine. Si el ID no existe, espera 1 s y sigue.
    private IEnumerator ShowDialogueAndWait(string id)
    {
        if (string.IsNullOrEmpty(id) || ThoughtUI.Instance == null)
            yield break;

        ThoughtUI.Instance.ShowById(id);

        float waited = 0f;
        while (!ThoughtUI.Instance.IsShowing && waited < 1f)
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
        }

        while (ThoughtUI.Instance.IsShowing)
            yield return null;
    }
}
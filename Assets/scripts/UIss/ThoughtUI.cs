using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ThoughtUI : MonoBehaviour
{
    public static ThoughtUI Instance { get; private set; }

    [SerializeField] private DialogueDatabase database;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private GameObject canvasGroupStamina;
    [SerializeField] private TMP_Text textLabel;
    [SerializeField] private Button button1;
    [SerializeField] private Button button2;
    [SerializeField] private float fadeSpeed = 4f;

    [Header("Búsqueda automática al cargar una escena")]
    [Tooltip("Nombre EXACTO del GameObject de la stamina. Si la referencia se pierde al cambiar " +
             "de escena, se busca por este nombre (también lo encuentra si está desactivado).")]
    [SerializeField] private string staminaObjectName = "Stamina";

    private readonly Queue<DialogueLine> queue = new();
    private bool isShowing;

    // Para que otros scripts (como una cinemática) puedan esperar a que
    // termine de mostrarse TODO el diálogo en cola antes de continuar.
    public bool IsShowing => isShowing;

    void Awake()
    {
        Instance = this;
        canvasGroup.alpha = 0f;
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Start()
    {
        ResolveStamina();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ResolveStamina();
    }

    // Si la referencia se perdió (el objeto de la escena anterior se destruyó), la busca
    // por nombre. Usa includeInactive porque la stamina se apaga mientras sale un diálogo
    // y GameObject.Find no encuentra objetos desactivados.
    private void ResolveStamina()
    {
        if (canvasGroupStamina != null || string.IsNullOrEmpty(staminaObjectName))
            return;

        foreach (Transform t in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t.name == staminaObjectName)
            {
                canvasGroupStamina = t.gameObject;
                return;
            }
        }

        Debug.LogWarning($"ThoughtUI: no encontré un objeto llamado '{staminaObjectName}' en esta escena. " +
                          "Los diálogos funcionan, pero no se ocultará la stamina.");
    }

    private void SetStaminaActive(bool active)
    {
        if (canvasGroupStamina == null)
            ResolveStamina(); // por si se perdió a mitad de la partida

        if (canvasGroupStamina != null)
            canvasGroupStamina.SetActive(active);
    }

    // Permite a otros scripts (ej. ScriptedEvent) saber cuánto va a durar una línea
    // SIN mostrarla todavía — útil para sincronizar algo (como que empiecen a caer
    // rocas) con la MITAD de un diálogo que ni siquiera ha arrancado.
    public float GetDurationById(string id)
    {
        var line = database.GetById(id);
        return line != null ? line.duration : 0f;
    }

    public void ShowById(string id)
    {
        var line = database.GetById(id);
        if (line == null) return;

        queue.Enqueue(line);
        if (!isShowing)
            StartCoroutine(ProcessQueue());
    }

    private IEnumerator ProcessQueue()
    {
        isShowing = true;

        while (queue.Count > 0)
        {
            var line = queue.Dequeue();
            string lang = LocalizationManager.Instance != null ? LocalizationManager.Instance.CurrentLanguage : "es";
            textLabel.text = line.GetText(lang);

            if (button1 != null) button1.gameObject.SetActive(false);
            if (button2 != null) button2.gameObject.SetActive(false);
            SetStaminaActive(false);
            yield return Fade(1f);
            yield return new WaitForSeconds(line.duration);
            yield return Fade(0f);
            if (button1 != null) button1.gameObject.SetActive(true);
            if (button2 != null) button2.gameObject.SetActive(true);
            SetStaminaActive(true);
        }

        isShowing = false;
    }

    private IEnumerator Fade(float target)
    {
        while (!Mathf.Approximately(canvasGroup.alpha, target))
        {
            canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, target, fadeSpeed * Time.deltaTime);
            yield return null;
        }
    }
}
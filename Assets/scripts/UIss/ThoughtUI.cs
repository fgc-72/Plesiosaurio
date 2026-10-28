using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
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

            button1.gameObject.SetActive(false);
            button2.gameObject.SetActive(false);
            canvasGroupStamina.SetActive(false);
            yield return Fade(1f);
            yield return new WaitForSeconds(line.duration);
            yield return Fade(0f);
            button1.gameObject.SetActive(true);
            button2.gameObject.SetActive(true);
            canvasGroupStamina.SetActive(true);
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

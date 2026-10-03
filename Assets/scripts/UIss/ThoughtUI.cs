using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class ThoughtUI : MonoBehaviour
{
    public static ThoughtUI Instance { get; private set; }

    [SerializeField] private DialogueDatabase database;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TMP_Text textLabel;
    [SerializeField] private float fadeSpeed = 4f;

    private readonly Queue<DialogueLine> queue = new();
    private bool isShowing;

    void Awake()
    {
        Instance = this;
        if (canvasGroup != null)
            canvasGroup.alpha = 0f;
    }

    public void ShowById(string id)
    {
        if (database == null)
        {
            Debug.LogWarning("ThoughtUI no tiene asignada una DialogueDatabase.");
            return;
        }

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

            string lang = "es";
            if (LocalizationManager.Instance != null)
                lang = LocalizationManager.Instance.CurrentLanguage;
            else if (LanguageManager.Instance != null)
                lang = LanguageManager.Instance.CurrentLanguage;

            if (textLabel != null)
                textLabel.text = line.GetText(lang);

            yield return Fade(1f);

            // Si duration es 0 o menor, aseguramos un tiempo de lectura seguro (3.5s)
            float displayDuration = line.duration > 0f ? line.duration : 3.5f;
            yield return new WaitForSeconds(displayDuration);

            yield return Fade(0f);
        }

        isShowing = false;
    }

    private IEnumerator Fade(float target)
    {
        if (canvasGroup == null) yield break;

        while (!Mathf.Approximately(canvasGroup.alpha, target))
        {
            canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, target, fadeSpeed * Time.deltaTime);
            yield return null;
        }
    }
}
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
        canvasGroup.alpha = 0f;
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
            string lang = LanguageManager.Instance != null ? LanguageManager.Instance.CurrentLanguage : "es";
            textLabel.text = line.GetText(lang);

            yield return Fade(1f);
            yield return new WaitForSeconds(line.duration);
            yield return Fade(0f);
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
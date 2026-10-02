using UnityEngine;

[System.Serializable]
public class DialogueTranslation
{
    public string languageCode;
    [TextArea(2, 4)]
    public string text;
}

[System.Serializable]
public class DialogueLine
{
    public string id;
    public string speaker;
    public float duration = 3f;
    public DialogueTranslation[] translations;

    public string GetText(string languageCode)
    {
        foreach (var t in translations)
            if (t.languageCode == languageCode) return t.text;

        return translations.Length > 0 ? translations[0].text : "[missing text]";
    }
}
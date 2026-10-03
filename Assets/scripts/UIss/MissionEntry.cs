using UnityEngine;

[System.Serializable]
public class LocalizedMissionText
{
    public string languageCode; // "es", "en", etc.
    [TextArea(1, 2)] public string title;
    [TextArea(2, 3)] public string description;
    [TextArea(2, 3)] public string hint;
}

[System.Serializable]
public class MissionEntry
{
    public string id; // ej: "M1_BUSCAR_MARA"
    public LocalizedMissionText[] translations;

    public LocalizedMissionText GetLocalized(string languageCode)
    {
        foreach (var t in translations)
            if (t.languageCode == languageCode) return t;

        return translations.Length > 0 ? translations[0] : null;
    }
}
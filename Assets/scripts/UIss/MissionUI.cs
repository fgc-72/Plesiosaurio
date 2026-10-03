using TMPro;
using UnityEngine;

public class MissionUI : MonoBehaviour
{
    public static MissionUI Instance { get; private set; }

    [SerializeField] private MissionDatabase database;
    [SerializeField] private TMP_Text titleLabel;
    [SerializeField] private TMP_Text descriptionLabel;
    [SerializeField] private TMP_Text hintLabel;

    void Awake()
    {
        Instance = this;
    }

    public void ShowMission(string id)
    {
        var mission = database.GetById(id);
        if (mission == null) return;

        string lang = LanguageManager.Instance != null ? LanguageManager.Instance.CurrentLanguage : "es";
        var localized = mission.GetLocalized(lang);
        if (localized == null) return;

        titleLabel.text = localized.title;
        descriptionLabel.text = localized.description;
        hintLabel.text = localized.hint;
    }

    public void Hide()
    {
        titleLabel.text = "";
        descriptionLabel.text = "";
        hintLabel.text = "";
    }
}
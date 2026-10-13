using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MissionUI : MonoBehaviour
{
    public static MissionUI Instance { get; private set; }

    [SerializeField] private MissionDatabase database;
    [SerializeField] private TMP_Text titleLabel;
    [SerializeField] private TMP_Text descriptionLabel;
    [SerializeField] private TMP_Text hintLabel;
    [SerializeField] private Image pointer1;
    [SerializeField] private Image pointer2;
    [SerializeField] private Image pointer3;

    void Awake()
    {
        Instance = this;
    }

    public void ShowMission(string id)
    {
        var mission = database.GetById(id);
        if (mission == null) return;

        string lang = LocalizationManager.Instance != null ? LocalizationManager.Instance.CurrentLanguage : "es";
        var localized = mission.GetLocalized(lang);
        if (localized == null) return;

        titleLabel.text = localized.title;
        descriptionLabel.text = localized.description;
        hintLabel.text = localized.hint;
        if (titleLabel.text != "") pointer1.enabled = true;
        if (descriptionLabel.text != "") pointer2.enabled = true;
        if (hintLabel.text != "") pointer3.enabled = true;
    }

    public void Hide()
    {
        titleLabel.text = "";
        descriptionLabel.text = "";
        hintLabel.text = "";
        pointer1.enabled = false;
        pointer2.enabled = false;
        pointer3.enabled = false;
    }
}
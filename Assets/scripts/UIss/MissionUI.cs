using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Muestra la misión activa (título, descripción y pista).
/// Va una vez en cada escena (conviene hacerlo un prefab). Al arrancar, si hay una misión
/// guardada en MissionState, la vuelve a mostrar, así no se pierde al cambiar de escena.
/// </summary>
public class MissionUI : MonoBehaviour
{
    public static MissionUI Instance { get; private set; }

    [SerializeField] private MissionDatabase database;
    [SerializeField] private TMP_Text titleLabel;
    [SerializeField] private TMP_Text descriptionLabel;
    [SerializeField] private TMP_Text hintLabel;
    [SerializeField] private Image button1;
    [SerializeField] private Image button2;
    [SerializeField] private Image button3;
    private string progressMissionId;
    private int progressCurrent;
    private int progressRequired;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        if (!string.IsNullOrEmpty(MissionState.CurrentMissionId))
            Render(MissionState.CurrentMissionId);
    }

    public void SetProgress(string missionId, int current, int required)
    {
        progressMissionId = missionId;
        progressCurrent = current;
        progressRequired = required;

        if (MissionState.CurrentMissionId == missionId)
        Render(missionId);
    }

    private void OnDestroy()
    {
        // Evita que Instance apunte a un objeto destruido al cambiar de escena
        if (Instance == this)
            Instance = null;
    }

    public void ShowMission(string id)
    {
        // Solo la guardamos como activa si realmente se pudo mostrar
        if (Render(id))
            MissionState.CurrentMissionId = id;
        
        if (titleLabel.text == "")
            button1.enabled = false;
        else
            button1.enabled = true;
        if (descriptionLabel.text == "")
            button2.enabled = false;
        else
            button2.enabled = true;
        if (hintLabel.text == "")
            button3.enabled = false;
        else
            button3.enabled = true;
    }

    public void Hide()
    {
        MissionState.CurrentMissionId = null;
        titleLabel.text = "";
        descriptionLabel.text = "";
        hintLabel.text = "";
        button1.enabled = false;
        button2.enabled = false;
        button3.enabled = false;
    }

    private bool Render(string id)
    {
        var mission = database.GetById(id);
        if (mission == null) return false;

        string lang = LanguageManager.Instance != null ? LanguageManager.Instance.CurrentLanguage : "es";
        var localized = mission.GetLocalized(lang);
        if (localized == null) return false;

        titleLabel.text = localized.title;
        descriptionLabel.text = localized.description;

        string hint = localized.hint;
        if (!string.IsNullOrEmpty(progressMissionId) && id == progressMissionId)
            hint += $" ({progressCurrent}/{progressRequired})";
        hintLabel.text = hint;
        return true;
    }
}
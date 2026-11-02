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
        
        if (button1 != null)
            button1.enabled = titleLabel != null && !string.IsNullOrEmpty(titleLabel.text);
        if (button2 != null)
            button2.enabled = descriptionLabel != null && !string.IsNullOrEmpty(descriptionLabel.text);
        if (button3 != null)
            button3.enabled = hintLabel != null && !string.IsNullOrEmpty(hintLabel.text);
    }

    public void Hide()
    {
        MissionState.CurrentMissionId = null;
        if (titleLabel != null) titleLabel.text = "";
        if (descriptionLabel != null) descriptionLabel.text = "";
        if (hintLabel != null) hintLabel.text = "";
        if (button1 != null) button1.enabled = false;
        if (button2 != null) button2.enabled = false;
        if (button3 != null) button3.enabled = false;
    }

    private bool Render(string id)
    {
        if (database == null) return false;
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
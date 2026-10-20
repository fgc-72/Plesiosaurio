using TMPro;
using UnityEditor;
using UnityEngine;

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

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        // Si venimos de otra escena con una misión activa, la volvemos a mostrar
        if (!string.IsNullOrEmpty(MissionState.CurrentMissionId))
            Render(MissionState.CurrentMissionId);
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
    }

    public void Hide()
    {
        MissionState.CurrentMissionId = null;
        titleLabel.text = "";
        descriptionLabel.text = "";
        hintLabel.text = "";
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
        hintLabel.text = localized.hint;
        return true;
    }
}
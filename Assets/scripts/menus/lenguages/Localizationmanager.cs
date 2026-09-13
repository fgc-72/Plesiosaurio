using UnityEngine;
using System;

// Vive en la primera escena que se carga (ej. MainMenu) y persiste durante
// toda la sesión de juego, así el idioma se mantiene al cambiar de escena.
public class LocalizationManager : MonoBehaviour
{
    public static LocalizationManager Instance { get; private set; }

    [SerializeField] private LocalizationDatabase database;

    private const string SavedLanguageKey = "SelectedLanguage";
    public string CurrentLanguage { get; private set; } = "es";

    // Cualquier LocalizedText se suscribe a esto para saber cuándo refrescarse.
    public event Action OnLanguageChanged;

    private void Awake()
    {
        // Patrón singleton clásico: si ya existe uno (venimos de otra escena), este se destruye.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        CurrentLanguage = PlayerPrefs.GetString(SavedLanguageKey, "es");
    }

    public void SetLanguage(string languageCode)
    {
        CurrentLanguage = languageCode;

        PlayerPrefs.SetString(SavedLanguageKey, languageCode);
        PlayerPrefs.Save();

        OnLanguageChanged?.Invoke();
    }

    public string GetText(string key)
    {
        if (database == null)
        {
            Debug.LogError("LocalizationManager no tiene una LocalizationDatabase asignada.");
            return key;
        }

        return database.GetText(key, CurrentLanguage);
    }
}
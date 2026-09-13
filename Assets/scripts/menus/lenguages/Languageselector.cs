using UnityEngine;
using UnityEngine.UI;

// Poner este script en el GameObject "IdiomaGroup" (el mismo que tiene el ToggleGroup).
public class LanguageSelector : MonoBehaviour
{
    [System.Serializable]
    public class LanguageOption
    {
        public Toggle toggle;
        [Tooltip("Código del idioma, ej. 'es' o 'en'. Debe coincidir con LocalizationDatabase.")]
        public string languageCode;
    }

    [SerializeField] private LanguageOption[] languages;

    private void Start()
    {
        string currentLanguage = LocalizationManager.Instance != null
            ? LocalizationManager.Instance.CurrentLanguage
            : "es";

        foreach (var lang in languages)
        {
            // SetIsOnWithoutNotify: marca el toggle correcto sin disparar el evento,
            // así no "reaplicamos" el idioma que ya estaba cargado.
            lang.toggle.SetIsOnWithoutNotify(lang.languageCode == currentLanguage);
            lang.toggle.onValueChanged.AddListener(isOn => OnToggleChanged(lang, isOn));
        }
    }

    private void OnToggleChanged(LanguageOption lang, bool isOn)
    {
        if (!isOn) return; // el ToggleGroup ya se encarga de apagar los demás

        if (LocalizationManager.Instance != null)
            LocalizationManager.Instance.SetLanguage(lang.languageCode);
    }
}
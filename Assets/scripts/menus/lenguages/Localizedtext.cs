using UnityEngine;
using TMPro;

// Poner este componente en CUALQUIER TMP_Text que deba traducirse
// (títulos de botones, labels de Ajustes, descripciones, etc.).
[RequireComponent(typeof(TMP_Text))]
public class LocalizedText : MonoBehaviour
{
    [Tooltip("Debe coincidir EXACTO con la clave en LocalizationDatabase.")]
    [SerializeField] private string key;

    private TMP_Text text;

    private void Awake()
    {
        text = GetComponent<TMP_Text>();
    }

    private void OnEnable()
    {
        Refresh();

        if (LocalizationManager.Instance != null)
            LocalizationManager.Instance.OnLanguageChanged += Refresh;
    }

    private void OnDisable()
    {
        if (LocalizationManager.Instance != null)
            LocalizationManager.Instance.OnLanguageChanged -= Refresh;
    }

    private void Refresh()
    {
        if (LocalizationManager.Instance == null) return;
        text.text = LocalizationManager.Instance.GetText(key);
    }
}
using UnityEngine;

public class LanguageManager : MonoBehaviour
{
    public static LanguageManager Instance { get; private set; }

    [SerializeField] private string currentLanguage = "es";
    public string CurrentLanguage => currentLanguage;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void SetLanguage(string languageCode)
    {
        currentLanguage = languageCode;
    }
}
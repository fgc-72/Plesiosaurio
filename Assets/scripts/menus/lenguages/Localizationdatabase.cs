using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "LocalizationDatabase", menuName = "Nami/Localization Database")]
public class LocalizationDatabase : ScriptableObject
{
    [System.Serializable]
    public class LocalizedEntry
    {
        [Tooltip("Identificador único, ej. 'menu_jugar', 'ajustes_titulo'. No se muestra en pantalla.")]
        public string key;
        [TextArea] public string es;
        [TextArea] public string en;
    }

    [SerializeField] private List<LocalizedEntry> entries = new List<LocalizedEntry>();

    private Dictionary<string, LocalizedEntry> lookup;

    private void BuildLookupIfNeeded()
    {
        if (lookup != null) return;

        lookup = new Dictionary<string, LocalizedEntry>();
        foreach (var entry in entries)
        {
            if (!lookup.ContainsKey(entry.key))
                lookup.Add(entry.key, entry);
        }
    }

    public string GetText(string key, string languageCode)
    {
        BuildLookupIfNeeded();

        if (!lookup.TryGetValue(key, out LocalizedEntry entry))
        {
            Debug.LogWarning($"Falta la clave de localización: '{key}'");
            return $"[{key}]"; // visible en pantalla para que sea obvio que falta traducir
        }

        return languageCode == "en" ? entry.en : entry.es;
    }
}
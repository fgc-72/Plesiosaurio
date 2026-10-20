using System;
using UnityEngine;
using UnityEngine.Audio;

public enum VolumeChannel { Music, UI, Environment }

/// <summary>Datos serializables: lo que se guarda en disco.</summary>
[Serializable]
public class VolumeSettings
{
    [Range(0f, 1f)] public float music = 1f;
    [Range(0f, 1f)] public float ui = 1f;
    [Range(0f, 1f)] public float environment = 1f;

    public float Get(VolumeChannel c) => c switch
    {
        VolumeChannel.Music => music,
        VolumeChannel.UI => ui,
        _ => environment
    };

    public void Set(VolumeChannel c, float v)
    {
        switch (c)
        {
            case VolumeChannel.Music: music = v; break;
            case VolumeChannel.UI: ui = v; break;
            default: environment = v; break;
        }
    }
}

/// <summary>
/// Fuente única de verdad de los volúmenes. Aplica los valores al AudioMixer,
/// los guarda en PlayerPrefs (JSON) y avisa a cualquier slider suscrito.
/// Poner en un Empty (primera escena). Sobrevive entre escenas.
/// </summary>
public class AudioSettingsManager : MonoBehaviour
{
    public static AudioSettingsManager Instance { get; private set; }

    [SerializeField] AudioMixer mixer;
    [Header("Nombres de parámetros expuestos en el Mixer")]
    [SerializeField] string musicParam = "MusicVolume";
    [SerializeField] string uiParam = "UIVolume";
    [SerializeField] string environmentParam = "EnvironmentVolume";

    const string PrefsKey = "AudioVolumeSettings";

    VolumeSettings settings;

    /// <summary>Se dispara cuando cambia un volumen (para sincronizar los sliders).</summary>
    public event Action<VolumeChannel, float> VolumeChanged;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        Load();
    }

    void Start()
    {
        // SetFloat del mixer es más fiable en Start que en Awake.
        foreach (VolumeChannel c in Enum.GetValues(typeof(VolumeChannel)))
            ApplyToMixer(c, settings.Get(c));
    }

    void OnApplicationPause(bool paused) { if (paused) Save(); }
    void OnApplicationQuit() => Save();
    void OnDestroy() { if (Instance == this) Instance = null; }

    public float Get(VolumeChannel channel) => settings.Get(channel);

    public void Set(VolumeChannel channel, float value01)
    {
        value01 = Mathf.Clamp01(value01);
        settings.Set(channel, value01);
        ApplyToMixer(channel, value01);
        VolumeChanged?.Invoke(channel, value01);
    }

    public void Save()
    {
        PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(settings));
        PlayerPrefs.Save();
    }

    void Load()
    {
        string json = PlayerPrefs.GetString(PrefsKey, "");
        settings = string.IsNullOrEmpty(json)
            ? new VolumeSettings()
            : JsonUtility.FromJson<VolumeSettings>(json);
    }

    void ApplyToMixer(VolumeChannel channel, float v)
    {
        // Slider lineal (0..1) -> decibelios (el mixer trabaja en dB).
        float dB = v <= 0.0001f ? -80f : Mathf.Log10(v) * 20f;
        mixer.SetFloat(ParamFor(channel), dB);
    }

    string ParamFor(VolumeChannel c) => c switch
    {
        VolumeChannel.Music => musicParam,
        VolumeChannel.UI => uiParam,
        _ => environmentParam
    };
}

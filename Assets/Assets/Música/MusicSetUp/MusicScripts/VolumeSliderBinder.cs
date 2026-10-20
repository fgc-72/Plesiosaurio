using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ponlo en cada Slider (menú principal Y menú de pausa) y elige el canal.
/// Ambos paneles muestran siempre el mismo valor porque leen del AudioSettingsManager.
/// </summary>
[RequireComponent(typeof(Slider))]
public class VolumeSliderBinder : MonoBehaviour
{
    [SerializeField] VolumeChannel channel;

    Slider slider;

    void Awake()
    {
        slider = GetComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
    }

    void OnEnable()
    {
        var mgr = AudioSettingsManager.Instance;
        if (mgr == null) { Debug.LogWarning("[VolumeSliderBinder] No hay AudioSettingsManager."); return; }

        // Al abrirse el panel, refleja el valor global actual sin disparar el evento.
        slider.SetValueWithoutNotify(mgr.Get(channel));
        slider.onValueChanged.AddListener(OnSliderChanged);
        mgr.VolumeChanged += OnVolumeChanged;
    }

    void OnDisable()
    {
        slider.onValueChanged.RemoveListener(OnSliderChanged);
        if (AudioSettingsManager.Instance != null)
            AudioSettingsManager.Instance.VolumeChanged -= OnVolumeChanged;
    }

    void OnSliderChanged(float value) => AudioSettingsManager.Instance.Set(channel, value);

    void OnVolumeChanged(VolumeChannel c, float value)
    {
        if (c == channel) slider.SetValueWithoutNotify(value);
    }
}

using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(TMP_Text))]
public class SliderPercentText : MonoBehaviour
{
    [SerializeField] private Slider slider;
    [SerializeField] private string format = "{0}%";

    private TMP_Text label;

    private void Awake()
    {
        label = GetComponent<TMP_Text>();
    }

    private void OnEnable()
    {
        if (slider == null) return;
        slider.onValueChanged.AddListener(UpdateText);
        UpdateText(slider.value); // muestra el valor inicial
    }

    private void OnDisable()
    {
        if (slider != null)
            slider.onValueChanged.RemoveListener(UpdateText);
    }

    private void UpdateText(float value)
    {
        // Convierte el valor a 0-1 sin importar el min/max del slider
        float normalized = Mathf.InverseLerp(slider.minValue, slider.maxValue, value);
        int percent = Mathf.RoundToInt(normalized * 100f);
        label.text = string.Format(format, percent);
    }
}
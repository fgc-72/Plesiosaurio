using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Script genérico y reutilizable: muestra el valor de CUALQUIER Slider en un
// texto al lado. Se conecta desde el Inspector, sin tocar código de nuevo.
[RequireComponent(typeof(Slider))]
public class SliderValueDisplay : MonoBehaviour
{
    [Tooltip("El texto donde se muestra el número (ej. el '100' al lado de la barra).")]
    [SerializeField] private TMP_Text valueText;

    [Tooltip("Si quieres que se muestre como '100%' en vez de solo '100', actívalo.")]
    [SerializeField] private bool showPercentSign = false;

    private Slider slider;

    private void Awake()
    {
        slider = GetComponent<Slider>();
    }

    private void Start()
    {
        // Actualiza el texto una vez al inicio con el valor actual del slider.
        UpdateText(slider.value);
        slider.onValueChanged.AddListener(UpdateText);
    }

    private void UpdateText(float value)
    {
        string number = Mathf.RoundToInt(value).ToString();
        valueText.text = showPercentSign ? number + "%" : number;
    }
}
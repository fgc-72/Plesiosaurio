using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.InputSystem;
using System.Linq;

public class DetectiveController : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private Volume grayscaleVolume;
    [SerializeField] private ScriptableRendererData rendererData;
    [SerializeField] private string highlightFeatureName = "HighlightPass";

    [Header("Config")]
    [SerializeField] private Key activationKey = Key.Q;
    [SerializeField] private float fadeSpeed = 5f;

    private ScriptableRendererFeature highlightFeature;
    private float targetWeight;

    void Start()
    {
        if (rendererData != null)
        {
            highlightFeature = rendererData.rendererFeatures
                .FirstOrDefault(f => f.name == highlightFeatureName);

            if (highlightFeature == null)
                Debug.LogWarning($"No hay feature '{highlightFeatureName}'.");
        }
    }

    void Update()
    {
        bool isPressed = Keyboard.current != null && Keyboard.current[activationKey].isPressed;

        if (highlightFeature != null)
            highlightFeature.SetActive(isPressed);

        targetWeight = isPressed ? 1f : 0f;

        if (grayscaleVolume != null)
        {
            grayscaleVolume.weight = Mathf.MoveTowards(
                grayscaleVolume.weight,
                targetWeight,
                fadeSpeed * Time.deltaTime
            );
        }
    }
}
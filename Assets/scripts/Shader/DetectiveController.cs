using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.InputSystem;
using System.Linq;

public class DetectiveController : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private InputManagerBueno inputManager;
    [SerializeField] private Volume grayscaleVolume;
    [SerializeField] private ScriptableRendererData rendererData;
    [SerializeField] private string highlightFeatureName = "HighlightPass";

    [Header("Config")]
    [SerializeField] private float fadeSpeed = 5f;

    private ScriptableRendererFeature highlightFeature;
    private float targetWeight;
    private bool previousState;

    private void Start()
    {
        if (rendererData != null)
        {
            highlightFeature = rendererData.rendererFeatures
                .FirstOrDefault(f => f.name == highlightFeatureName);

            if (highlightFeature == null)
            {
                Debug.LogWarning(
                    $"No hay feature '{highlightFeatureName}'."
                );
            }
        }
    }

    private void Update()
    {
        // LT + RT al mismo tiempo
        bool isPressed = inputManager != null &&
                         inputManager.detectiveInput;

        // Solo activa/desactiva el Highlight cuando cambia el estado
        if (isPressed != previousState)
        {
            if (highlightFeature != null)
                highlightFeature.SetActive(isPressed);

            previousState = isPressed;
        }

        // Grayscale
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
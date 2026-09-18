using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Linq;

public class DetectiveController : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private InputManagerBueno inputManager;
    [SerializeField] private Volume grayscaleVolume;
    [SerializeField] private ScriptableRendererData rendererData;
    [SerializeField] private string highlightFeatureName = "PlayerOccludedHighlightFeature";

    [Header("Config")]
    [SerializeField] private float fadeSpeed = 5f;
    [SerializeField] private float maxDuration = 5f;
    [SerializeField] private float cooldownDuration = 8f;

    private ScriptableRendererFeature highlightFeature;
    private float targetWeight;
    private bool previousState;

    private bool isActive;
    private float activeTimer;
    private bool isOnCooldown;
    private float cooldownTimer;

    private void Start()
    {
        if (rendererData != null)
        {
            highlightFeature = rendererData.rendererFeatures
                .FirstOrDefault(f => f.name == highlightFeatureName);

            if (highlightFeature == null)
                Debug.LogWarning($"No hay feature '{highlightFeatureName}'.");
        }
    }

    private void Update()
    {
        bool keyHeld = inputManager != null && inputManager.detectiveInput;

        // ----- Cooldown -----
        if (isOnCooldown)
        {
            cooldownTimer -= Time.deltaTime;
            if (cooldownTimer <= 0f)
                isOnCooldown = false;
        }

        bool wantsActive = keyHeld && !isOnCooldown;

        if (wantsActive)
        {
            isActive = true;
            activeTimer += Time.deltaTime;

            if (activeTimer >= maxDuration)
            {
                isActive = false;
                StartCooldown();
            }
        }
        else
        {
            if (isActive)
            {
                isActive = false;
                StartCooldown();
            }
            activeTimer = 0f;
        }

        // Solo tocar la Feature cuando el estado realmente cambia
        if (isActive != previousState)
        {
            if (highlightFeature != null)
                highlightFeature.SetActive(isActive);

            previousState = isActive;
        }

        // Grayscale
        targetWeight = isActive ? 1f : 0f;

        if (grayscaleVolume != null)
        {
            grayscaleVolume.weight = Mathf.MoveTowards(
                grayscaleVolume.weight,
                targetWeight,
                fadeSpeed * Time.deltaTime
            );
        }
    }

    private void StartCooldown()
    {
        isOnCooldown = true;
        cooldownTimer = cooldownDuration;
        activeTimer = 0f;
    }

    public float GetCooldownProgress() => isOnCooldown ? 1f - (cooldownTimer / cooldownDuration) : 1f;
    public bool IsOnCooldown => isOnCooldown;
}
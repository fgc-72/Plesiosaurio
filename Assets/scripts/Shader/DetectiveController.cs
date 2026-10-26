using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using System.Linq;

public class DetectiveController : MonoBehaviour
{
    [Header("Referencias")]
    public static DetectiveController  Instance { get; private set; }
    [SerializeField] private InputManagerBueno inputManager;
    [SerializeField] private Volume grayscaleVolume;
    [SerializeField] private ScriptableRendererData rendererData;
    [SerializeField] private string highlightFeatureName = "PlayerOccludedHighlightFeature";

    [Header("Búsqueda automática al cargar una escena")]
    [Tooltip("Nombre EXACTO del GameObject del Volume del modo escucha (el del gris) en cada " +
             "escena. Si la referencia de arriba se pierde al cambiar de escena, se busca por " +
             "este nombre (el objeto debe estar activo).")]
    [SerializeField] private string grayscaleVolumeName = "GrayscaleVolume";

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

    private void Awake()
    {
        // Patrón singleton clásico: si ya existe uno (venimos de otra escena), este se destruye.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Un duplicado que se está destruyendo no debe hacer nada.
        if (Instance != this) return;

        ResolveReferences();
    }

    // Vuelve a buscar por código lo que se pudo perder al cambiar de escena:
    // el InputManagerBueno (el de Nami) y el Volume del gris. Solo busca lo que
    // esté vacío o destruido; si la referencia sigue viva, no la toca.
    private void ResolveReferences()
    {
        if (inputManager == null)
        {
            inputManager = FindFirstObjectByType<InputManagerBueno>();
            if (inputManager == null)
                Debug.LogWarning("DetectiveController: no encontré ningún InputManagerBueno en la escena.");
        }

        if (grayscaleVolume == null && !string.IsNullOrEmpty(grayscaleVolumeName))
        {
            GameObject found = GameObject.Find(grayscaleVolumeName);
            if (found != null)
                grayscaleVolume = found.GetComponent<Volume>();

            if (grayscaleVolume == null)
                Debug.LogWarning($"DetectiveController: no encontré un Volume activo llamado '{grayscaleVolumeName}' " +
                                  "en esta escena. El modo escucha funciona, pero sin el efecto de gris.");
        }
    }

    private void Start()
    {
        // Para la primera escena (donde Awake/Start corren normal), por si algo está vacío.
        ResolveReferences();

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

            ListenMode.ListenModeFeature.SetListening(isActive);

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

    public bool IsActive => isActive;
    public float GetActiveRemainingNormalized() => isActive ? 1f - (activeTimer / maxDuration) : 1f;
}
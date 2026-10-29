using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class HeadbuttController : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private InputManagerBueno inputManager;
    [SerializeField] private Animator animator;
    [SerializeField] private HeadbuttHitbox hitbox;

    [Header("Cabezazo")]
    [Tooltip("Empuje hacia adelante al golpear (le da sensación de peso/impacto).")]
    [SerializeField] private float lungeForce = 8f;
    [Tooltip("Cuánto tiempo queda activa la hitbox tras presionar el botón.")]
    [SerializeField] private float hitboxActiveDuration = 0.35f;
    [Tooltip("Tiempo mínimo entre cabezazos, para que no se pueda spammear.")]
    [SerializeField] private float cooldown = 1f;

    private Rigidbody rb;
    private float cooldownTimer;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        if (inputManager == null)
            inputManager = GetComponent<InputManagerBueno>();

        if (inputManager == null)
            Debug.LogError(
                "HeadbuttController no encontró un InputManagerBueno. " +
                "Asegúrate de que esté en el mismo GameObject o asígnalo manualmente en el Inspector.",
                this
            );
    }

    public void HandleHeadbutt()
    {
        if (cooldownTimer > 0f)
            cooldownTimer -= Time.deltaTime;

        // ConsumeHeadbuttInput() se evalúa SIEMPRE (aunque esté en cooldown) para que
        // el flag no se quede "atascado" en true esperando a que el cooldown termine.
        bool wantsToHeadbutt = inputManager.ConsumeHeadbuttInput();

        if (wantsToHeadbutt && cooldownTimer <= 0f)
        {
            PerformHeadbutt();
            cooldownTimer = cooldown;
        }
    }

    private void PerformHeadbutt()
    {
        Debug.Log("🦕 CABEZAZO ejecutado"); // TEMPORAL: borrar cuando tengas la animación

        rb.AddForce(transform.forward * lungeForce, ForceMode.Impulse);

        if (animator != null)
            animator.SetTrigger("Headbutt");

        if (hitbox != null)
            hitbox.Activate(hitboxActiveDuration);
    }
}
using UnityEngine;

/// <summary>
/// Barrera de superficie de agua:
/// Detecta cuando Nami se acerca a la superficie, frena su avance hacia arriba,
/// la empuja suavemente hacia abajo y dispara el pensamiento de advertencia
/// (ej. "Mamá dijo que no debería acercarme a la superficie...").
/// 
/// Diseñado para colocarse en un GameObject independiente (NO en los planos del agua directamente).
/// Puede funcionar por coordenada Y / referencia al plano, o como un Trigger de BoxCollider.
/// </summary>
[DefaultExecutionOrder(100)] // Se ejecuta después de PlayerControls para interceptar y limitar el movimiento vertical
public class WaterSurfaceBarrier : MonoBehaviour
{
    [Header("Superficie del Agua")]
    [Tooltip("Opcional: arrastra aquí el GameObject del plano de agua ('Plane'). Si se asigna, toma su altura automáticamente.")]
    [SerializeField] private Transform surfacePlane;

    [Tooltip("Altura Y de la superficie usada si no se arrastra ningún plano (en Acto 1 y 2 es ~143.7).")]
    [SerializeField] private float defaultSurfaceY = 143.7f;

    [Tooltip("Profundidad de la zona de advertencia y frenado (metros por debajo de la superficie).")]
    [SerializeField] private float barrierDepth = 6f;

    [Tooltip("Velocidad con la que se empuja a Nami hacia abajo cuando intenta subir a la superficie.")]
    [SerializeField] private float pushDownSpeed = 5f;

    [Header("Diálogo de Pensamiento")]
    [Tooltip("ID de la línea en DialogueDatabase (DB_Dialogos).")]
    [SerializeField] private string dialogueId = "nami_superficie";

    [Tooltip("Tiempo de enfriamiento (en segundos) para no repetir el diálogo continuamente si Nami se queda cerca.")]
    [SerializeField] private float dialogueCooldown = 25f;

    [Header("Detección")]
    [Tooltip("Tag del jugador para validar la colisión si se usa como Trigger Collider.")]
    [SerializeField] private string playerTag = "Player";

    private Rigidbody namiRigidbody;
    private PlayerControls playerControls;
    private float nextDialogueTime;

    public float SurfaceY => surfacePlane != null ? surfacePlane.position.y : defaultSurfaceY;
    public float BarrierLimitY => SurfaceY - barrierDepth;

    private void Start()
    {
        ResolvePlayer();

        // Si no se asignó surfacePlane a mano, intenta auto-detectar un objeto llamado "Plane"
        if (surfacePlane == null)
        {
            GameObject planeObj = GameObject.Find("Plane");
            if (planeObj != null)
                surfacePlane = planeObj.transform;
        }
    }

    private void FixedUpdate()
    {
        if (namiRigidbody == null)
        {
            ResolvePlayer();
            if (namiRigidbody == null) return;
        }

        // Si el control está desactivado (ej. cinemática), no interferimos
        if (playerControls != null && !playerControls.IsControlEnabled)
            return;

        float currentY = namiRigidbody.position.y;
        float limitY = BarrierLimitY;
        float surfaceY = SurfaceY;

        // Comprueba si Nami entró en la zona de barrera bajo la superficie
        if (currentY >= limitY)
        {
            ApplyDownwardsConstraint(currentY, limitY, surfaceY);
        }
    }

    private void ApplyDownwardsConstraint(float currentY, float limitY, float surfaceY)
    {
        // 1. Mostrar diálogo con cooldown
        TryTriggerDialogue();

        // 2. Anular cualquier velocidad ascendente y aplicar empuje descendente
        Vector3 vel = namiRigidbody.linearVelocity;
        if (vel.y > 0f)
        {
            vel.y = 0f;
        }

        // Entre más cerca de la superficie, mayor el empuje descendente
        float t = Mathf.InverseLerp(limitY, surfaceY, currentY);
        float currentPush = Mathf.Lerp(pushDownSpeed * 0.5f, pushDownSpeed, t);
        vel.y = -currentPush;
        namiRigidbody.linearVelocity = vel;

        // 3. Toque firme: Nami nunca puede sobrepasar la superficie física
        float absoluteCeiling = surfaceY - 1.2f;
        if (currentY > absoluteCeiling)
        {
            Vector3 pos = namiRigidbody.position;
            pos.y = absoluteCeiling;
            namiRigidbody.position = pos;
        }
    }

    private void TryTriggerDialogue()
    {
        if (Time.time < nextDialogueTime) return;

        if (ThoughtUI.Instance != null && !string.IsNullOrEmpty(dialogueId))
        {
            ThoughtUI.Instance.ShowById(dialogueId);
            nextDialogueTime = Time.time + dialogueCooldown;
            Debug.Log($"[WaterSurfaceBarrier] Activado diálogo '{dialogueId}'.");
        }
    }

    private void ResolvePlayer()
    {
        playerControls = Object.FindFirstObjectByType<PlayerControls>();
        if (playerControls != null)
        {
            namiRigidbody = playerControls.GetComponent<Rigidbody>();
        }
    }

    // Soporte opcional si el usuario prefiere montar el script en un GameObject con BoxCollider (Trigger)
    private void OnTriggerStay(Collider other)
    {
        if (!other.CompareTag(playerTag) && !other.transform.root.CompareTag(playerTag))
            return;

        if (namiRigidbody == null) ResolvePlayer();
        if (namiRigidbody != null)
        {
            ApplyDownwardsConstraint(namiRigidbody.position.y, BarrierLimitY, SurfaceY);
        }
    }

    private void OnDrawGizmosSelected()
    {
        float sY = SurfaceY;
        float bY = sY - barrierDepth;

        // Si hay plano asignado, centra el gizmo en él y toma su tamaño real (un Mesh Plane mide 10x10 por escala)
        Vector3 centerPos = surfacePlane != null ? surfacePlane.position : transform.position;
        float sizeX = surfacePlane != null ? Mathf.Max(surfacePlane.lossyScale.x * 10f, 100f) : 2000f;
        float sizeZ = surfacePlane != null ? Mathf.Max(surfacePlane.lossyScale.z * 10f, 100f) : 2000f;

        // Visualización en la Scene view:
        // Zona tenue donde empieza la advertencia y empuje
        Gizmos.color = new Color(0.1f, 0.6f, 1f, 0.2f);
        Vector3 zoneCenter = new Vector3(centerPos.x, (sY + bY) * 0.5f, centerPos.z);
        Gizmos.DrawCube(zoneCenter, new Vector3(sizeX, barrierDepth, sizeZ));

        // Línea roja que marca la superficie límite exacta
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(new Vector3(centerPos.x, sY, centerPos.z), new Vector3(sizeX, 0.2f, sizeZ));
    }
}

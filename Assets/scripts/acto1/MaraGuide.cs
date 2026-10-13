using UnityEngine;

// Poner este script en Mara. Ella avanza sola por una serie de puntos (waypoints),
// pero se detiene a esperar si Nami (el jugador) se queda muy atrás, y retoma
// el camino cuando Nami vuelve a estar cerca.
[RequireComponent(typeof(Rigidbody))]
public class MaraGuide : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private Transform player;
    [Tooltip("Puntos por los que Mara va pasando en orden.")]
    [SerializeField] private Transform[] waypoints;

    [Header("Movimiento")]
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private float rotationSpeed = 4f;
    [Tooltip("Distancia al llegar a un waypoint para considerarlo 'alcanzado'.")]
    [SerializeField] private float waypointThreshold = 0.5f;

    [Header("Correa (espera al jugador)")]
    [Tooltip("Si el jugador se aleja más que esto, Mara se detiene a esperar.")]
    [SerializeField] private float leashDistance = 8f;
    [Tooltip("Debe ser MENOR a Leash Distance. Evita que Mara arranque/pare " +
             "constantemente justo en el borde (histéresis).")]
    [SerializeField] private float resumeDistance = 5f;
    [Tooltip("Tiempo (segundos) que la condición de espera/avance debe mantenerse ANTES " +
             "de que el cambio se haga efectivo. Si el jugador se queda justo rozando el " +
             "límite, esto evita que isWaiting (y su animación) parpadeen entre true/false.")]
    [SerializeField] private float waitStateChangeDelay = 0.2f;
    [Tooltip("Tiempo MÍNIMO (segundos) que Mara se queda comprometida en un estado (esperando " +
             "o avanzando) antes de poder volver a cambiar, aunque la distancia ya lo pida. " +
             "Sin esto, apenas retoma el camino su propio movimiento puede volver a cruzar el " +
             "límite de inmediato y se ve como pasos cortos de avance-freno-avance-freno.")]
    [SerializeField] private float minCommitDuration = 1.5f;

    [Header("Animación")]
    [Tooltip("Si lo dejas vacío, busca un Animator en este mismo GameObject.")]
    [SerializeField] private Animator animator;

    private int currentWaypointIndex;
    private bool isWaiting;
    private Rigidbody rb;
    private int lastAnimState = -1; // -1 fuerza el primer SetInteger; evita reenviar el mismo valor cada frame
    private bool pendingWaitState;
    private float waitStateTimer;
    private float timeSinceLastCommit = Mathf.Infinity; // Infinity: el primer cambio no debe esperar nada

    public bool IsWaiting => isWaiting;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        if (animator == null)
            animator = GetComponent<Animator>();

        // Forzamos Kinematic sin importar lo que tuvieras marcado en el Inspector:
        // Mara se mueve por script (waypoints), no por física, así que un Rigidbody
        // NO kinemático solo generaría conflicto (la física pelearía con el script
        // por controlar la posición, y Mara terminaría sin moverse bien).
        rb.isKinematic = true;
        rb.useGravity = false;
    }

    private void Update()
    {
        if (waypoints.Length == 0 || player == null)
            return;

        UpdateWaitingState();

        // La animación de Mara sigue el mismo criterio simple que su movimiento:
        // nadando (1) mientras avanza hacia un waypoint, idle (0) mientras espera.
        // OpeningCinematicAct1 sigue pudiendo pisar esto manualmente mientras
        // MaraGuide está desactivado (enabled = false) durante una cinemática.
        //
        // Solo llamamos a SetInteger cuando el valor REALMENTE cambia. No hace
        // diferencia para Mecanim en sí (da igual poner el mismo valor), pero evita
        // cualquier posibilidad de "reiniciar" una transición en curso cada frame.
        int targetAnimState = isWaiting ? 0 : 1;
        if (animator != null && targetAnimState != lastAnimState)
        {
            animator.SetInteger("AnimationState", targetAnimState);
            lastAnimState = targetAnimState;
        }
    }

    private void FixedUpdate()
    {
        if (waypoints.Length == 0 || player == null || isWaiting)
            return;

        MoveTowardCurrentWaypoint();
    }

    private void UpdateWaitingState()
    {
        float distanceToPlayer = Vector3.Distance(transform.position, player.position);
        timeSinceLastCommit += Time.deltaTime;

        // Histéresis por DISTANCIA (un límite para empezar a esperar, otro más cercano
        // para retomar): evita el parpadeo si el jugador se queda quieto justo en el borde.
        bool shouldWait = isWaiting
            ? distanceToPlayer > resumeDistance   // ya esperando: sigue esperando hasta estar BIEN cerca
            : distanceToPlayer >= leashDistance;  // avanzando: solo para si se aleja mucho

        if (shouldWait == isWaiting)
        {
            // Ya estamos en el estado que corresponde: no hay cambio pendiente.
            pendingWaitState = isWaiting;
            waitStateTimer = 0f;
            return;
        }

        // Histéresis por TIEMPO (corta): si el jugador roza el límite en milisegundos,
        // el cambio no se confirma de inmediato.
        if (shouldWait != pendingWaitState)
        {
            pendingWaitState = shouldWait;
            waitStateTimer = 0f;
        }

        waitStateTimer += Time.deltaTime;

        // Histéresis por TIEMPO (larga): además, Mara tiene que haber estado COMPROMETIDA
        // en su estado actual durante minCommitDuration antes de poder cambiarlo. Esto es
        // lo que evita el "avanza un poco, frena, avanza un poco" — su propio movimiento al
        // retomar el camino puede volver a cruzar el límite de inmediato, pero no la saca
        // de "avanzando" hasta que lleve un rato comprometida con eso.
        if (waitStateTimer >= waitStateChangeDelay && timeSinceLastCommit >= minCommitDuration)
        {
            isWaiting = shouldWait;
            timeSinceLastCommit = 0f;
        }
    }

    private void MoveTowardCurrentWaypoint()
    {
        Transform target = waypoints[currentWaypointIndex];
        Vector3 direction = target.position - rb.position;

        Vector3 newPosition = Vector3.MoveTowards(
            rb.position, target.position, moveSpeed * Time.fixedDeltaTime
        );
        rb.MovePosition(newPosition);

        if (direction.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime));
        }

        if (Vector3.Distance(rb.position, target.position) < waypointThreshold)
            currentWaypointIndex = Mathf.Min(currentWaypointIndex + 1, waypoints.Length - 1);
    }

    // Dibuja el camino y el rango de correa en el Scene View, para acomodar todo a ojo.
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        for (int i = 0; i < waypoints.Length - 1; i++)
        {
            if (waypoints[i] != null && waypoints[i + 1] != null)
                Gizmos.DrawLine(waypoints[i].position, waypoints[i + 1].position);
        }

        Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, leashDistance);

        Gizmos.color = new Color(0f, 1f, 0f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, resumeDistance);
    }
}
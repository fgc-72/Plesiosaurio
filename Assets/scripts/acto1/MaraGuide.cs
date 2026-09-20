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

    private int currentWaypointIndex;
    private bool isWaiting;
    private Rigidbody rb;

    public bool IsWaiting => isWaiting; // por si quieres usarlo para animación

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

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

        if (isWaiting)
        {
            // Solo retoma el camino cuando el jugador está BIEN cerca (resumeDistance),
            // no apenas cruza el límite de leashDistance otra vez.
            if (distanceToPlayer <= resumeDistance)
                isWaiting = false;
        }
        else
        {
            if (distanceToPlayer >= leashDistance)
                isWaiting = true;
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
            Debug.Log($"Mara está girando hacia el waypoint {currentWaypointIndex} en dirección {direction.normalized}");
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
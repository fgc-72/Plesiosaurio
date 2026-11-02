using UnityEngine;

// Poner este script en el tiburón. Igual que MaraGuide, avanza solo por una serie de
// puntos (waypoints) — aquí, el camino del laberinto — moviendo un Rigidbody kinemático.
// A diferencia de Mara, NO espera a Nami: cuando lo activas con StartChase() recorre el
// camino sin parar hasta el último punto. Mientras no esté activado, se queda quieto.
//
// Uso desde otro script (ej. un trigger del laberinto):
//   shark.StartChase();   // empieza a perseguir
//   shark.StopChase();    // se detiene
//   shark.ResetToStart(); // vuelve a su posición inicial y al primer waypoint
[RequireComponent(typeof(Rigidbody))]
public class SharkChaser : MonoBehaviour
{
    [Header("Camino")]
    [Tooltip("Puntos por los que pasa el tiburón, en orden (el recorrido del laberinto).")]
    [SerializeField] private Transform[] waypoints;

    [Header("Movimiento")]
    [SerializeField] private float moveSpeed = 4f;
    [Tooltip("Qué tan rápido alcanza la velocidad máxima al arrancar (unidades/s²).")]
    [SerializeField] private float acceleration = 3f;
    [SerializeField] private float rotationSpeed = 4f;
    [Tooltip("Distancia a un waypoint para considerarlo 'alcanzado'.")]
    [SerializeField] private float waypointThreshold = 0.5f;

    [Header("Inicio")]
    [Tooltip("Si es true, empieza a perseguir apenas carga la escena (útil para probar).")]
    [SerializeField] private bool startChasing = false;

    [Header("Animación")]
    [Tooltip("Si lo dejas vacío, busca un Animator en este mismo GameObject o en sus hijos.")]
    [SerializeField] private Animator animator;

    private Rigidbody rb;
    private int currentWaypointIndex;
    private float currentSpeed;
    private bool isChasing;

    private Vector3 startPosition;
    private Quaternion startRotation;

    public bool IsChasing => isChasing;
    public bool ReachedEnd { get; private set; }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        // Igual que Mara: se mueve por script, no por física.
        rb.isKinematic = true;
        rb.useGravity = false;

        startPosition = transform.position;
        startRotation = transform.rotation;

        isChasing = startChasing;
    }

    public void StartChase()
    {
        if (waypoints == null || waypoints.Length == 0)
        {
            Debug.LogWarning("SharkChaser: no hay waypoints asignados, el tiburón no tiene por dónde ir.");
            return;
        }

        ReachedEnd = false;
        isChasing = true;
    }

    public void StopChase()
    {
        isChasing = false;
    }

    // Vuelve a su posición inicial, al primer waypoint y se queda quieto.
    public void ResetToStart()
    {
        isChasing = false;
        ReachedEnd = false;
        currentWaypointIndex = 0;
        currentSpeed = 0f;

        rb.position = startPosition;
        rb.rotation = startRotation;
        transform.SetPositionAndRotation(startPosition, startRotation);
    }

    private void FixedUpdate()
    {
        bool shouldMove = isChasing && !ReachedEnd && waypoints != null && waypoints.Length > 0;

        float targetSpeed = shouldMove ? moveSpeed : 0f;
        currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, acceleration * Time.fixedDeltaTime);

        if (shouldMove)
            MoveTowardCurrentWaypoint();
    }

    private void Update()
    {
        if (animator == null) return;

        float normalized = moveSpeed > 0f ? currentSpeed / moveSpeed : 0f;
        animator.SetFloat("Speed", normalized * 0.4f, 0.1f, Time.deltaTime);
        animator.speed = Mathf.Lerp(0.15f, 0.4f, normalized);
    }

    private void MoveTowardCurrentWaypoint()
    {
        Transform target = waypoints[currentWaypointIndex];
        if (target == null) return;

        Vector3 direction = target.position - rb.position;

        rb.MovePosition(Vector3.MoveTowards(rb.position, target.position, currentSpeed * Time.fixedDeltaTime));

        if (direction.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime));
        }

        if (Vector3.Distance(rb.position, target.position) < waypointThreshold)
        {
            if (currentWaypointIndex >= waypoints.Length - 1)
            {
                // Llegó al último punto: se detiene (como Mara, no da la vuelta).
                ReachedEnd = true;
                isChasing = false;
            }
            else
            {
                currentWaypointIndex++;
            }
        }
    }

    // Dibuja el camino en el Scene View para acomodar los waypoints a ojo.
    private void OnDrawGizmos()
    {
        if (waypoints == null) return;

        Gizmos.color = Color.red;
        for (int i = 0; i < waypoints.Length - 1; i++)
        {
            if (waypoints[i] != null && waypoints[i + 1] != null)
                Gizmos.DrawLine(waypoints[i].position, waypoints[i + 1].position);
        }
    }
}

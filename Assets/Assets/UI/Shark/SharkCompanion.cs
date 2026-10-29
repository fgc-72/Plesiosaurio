using UnityEngine;

public class SharkCompanion : MonoBehaviour
{
    public void SetFollow(bool value) => followEnabled = value; // shark.SetFollow(true/false) para activar esta chimbada

    [SerializeField] Transform target;
    [SerializeField] Animator animator;

    [Header("Hide")]
    [SerializeField] PlayerHiding playerHiding;
    Vector3 lastSeenPos;

    [Header("Movimiento")]
    [SerializeField] float maxSpeed = 4f;
    [SerializeField] float acceleration = 3f;
    [SerializeField] float turnSpeed = 90f;
    [SerializeField] float followDistance = 4f;
    [SerializeField] float slowRadius = 3f;
    [SerializeField] float heightOffset = 0.5f;
    [SerializeField] float minDistance = 6f;

    [Header("Seguimiento")]
    [SerializeField] bool followEnabled = true;
    [SerializeField] float followRange = 40f;
    [SerializeField] float resumeRange = 35f;

    [Header("Terreno")]
    [SerializeField] float groundClearance = 3f;
    [SerializeField] float groundLookAhead = 8f;

    // ── NUEVO: aproximación guiada (la usa SharkListenEvent) ──────────────
    [Header("Aproximación guiada (evento)")]
    [Tooltip("A qué distancia del punto se considera que 'llegó' y se suelta a seguir por rango.")]
    [SerializeField] float arriveDistance = 2f;
    [Tooltip("Seguro: si en este tiempo no llega al punto (por ejemplo, el espacio personal " +
             "lo empuja hacia afuera), se suelta igual para no quedarse trabado.")]
    [SerializeField] float approachTimeout = 40f;

    // ── Deambular: nunca se queda quieto ──────────────────────────────────
    [Header("Deambular (cuando no sigue a Nami o ya está cerca de ella)")]
    [Tooltip("Cerca de Nami: nada por ahí dentro de este radio alrededor de ella, sin quedarse quieto.")]
    [SerializeField] float nearWanderRadius = 14f;
    [Tooltip("Lejos de Nami (fuera del rango): nada por ahí dentro de este radio alrededor de su zona.")]
    [SerializeField] float farWanderRadius = 25f;
    [Tooltip("Qué tan rápido nada mientras deambula (0.5 = la mitad de maxSpeed).")]
    [SerializeField] float wanderSpeedFactor = 0.5f;
    [Tooltip("A qué distancia de su punto de paseo se considera que llegó y elige otro.")]
    [SerializeField] float wanderPointReach = 2f;
    [Tooltip("Si en este tiempo no llega a su punto, elige otro (para no dar vueltas atascado).")]
    [SerializeField] float wanderPointTimeout = 15f;
    [Tooltip("Variación de altura de los puntos de paseo (arriba/abajo).")]
    [SerializeField] float wanderHeightRange = 3f;

    Vector3 homePos;
    Vector3 wanderPoint;
    bool hasWanderPoint;
    float wanderTimer;
    bool wasFollowing;

    float speed;
    bool isFollowing;

    Transform approachPoint;
    float approachTimer;
    System.Action onApproachArrived;

    // Hace que el tiburón vaya hacia 'point' IGNORANDO el rango. Al llegar, vuelve
    // solo al seguimiento normal por rango (followEnabled = true) y avisa con 'onArrived'.
    public void ApproachPoint(Transform point, System.Action onArrived = null)
    {
        approachPoint = point;
        onApproachArrived = onArrived;
        approachTimer = 0f;
    }

    void FinishApproach()
    {
        approachPoint = null;
        followEnabled = true; // desde aquí manda el rango, como siempre

        var callback = onApproachArrived;
        onApproachArrived = null;
        callback?.Invoke();
    }

    // NUEVO: control externo (lo usa SharkCaptureSequence para que el tiburón le dé
    // vueltas a Nami). Mientras sea true, este script NO mueve ni rota al tiburón:
    // quien lo controla lo hace por su cuenta, y usa SetSwimAnimation para la animación.
    public bool ExternalControl { get; set; }

    public void SetSwimAnimation(float normalizedSpeed, float dt)
    {
        animator.SetFloat("Speed", normalizedSpeed * 0.4f, 0.1f, dt);
        animator.speed = Mathf.Lerp(0.15f, 0.4f, normalizedSpeed);
    }
    // ──────────────────────────────────────────────────────────────────────

    void Start()
    {
        // NUEVO: si Nami viene de otra escena (DontDestroyOnLoad), no se puede arrastrar
        // en el Inspector de esta escena: la buscamos por tag.
        if (target == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                target = player.transform;
                if (playerHiding == null)
                    playerHiding = player.GetComponent<PlayerHiding>();
            }
        }

        homePos = transform.position; // su "zona" mientras Nami esté lejos

        if (target != null)
            lastSeenPos = target.position;
    }

    // Elige un punto al azar alrededor de 'center'. Si está cerca de Nami, evita
    // el espacio personal (minDistance) para que no se pegue a ella.
    void PickWanderPoint(Vector3 center, float radius, bool avoidTarget)
    {
        float minR = avoidTarget ? Mathf.Min(minDistance + 1f, radius * 0.8f) : 0f;
        float angle = Random.Range(0f, Mathf.PI * 2f);
        float r = Random.Range(minR, radius);

        wanderPoint = center
                    + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * r
                    + Vector3.up * Random.Range(-wanderHeightRange, wanderHeightRange);

        hasWanderPoint = true;
        wanderTimer = 0f;
    }

    void Update()
    {
        if (target == null) return; // NUEVO: sin Nami en la escena, el tiburón no hace nada

        if (ExternalControl)
        {
            speed = 0f; // al soltarlo, arranca desde cero en vez de pegar un salto de velocidad
            return;
        }

        float dt = Time.deltaTime;

        bool hidden = playerHiding != null && playerHiding.IsHidden;
        if (!hidden) lastSeenPos = target.position;

        bool approaching = approachPoint != null;

        // 0. ¿Sigue o no al jugador?
        float distToPlayer = Vector3.Distance(transform.position, target.position);
        if (approaching) isFollowing = true; // NUEVO: yendo al punto, el rango no cuenta
        else if (!followEnabled) isFollowing = false;
        else if (isFollowing && distToPlayer > followRange) isFollowing = false;
        else if (!isFollowing && distToPlayer < resumeRange) isFollowing = true;

        // Si acaba de dejar de seguir, su "zona" es donde está ahora.
        if (wasFollowing && !isFollowing)
        {
            homePos = transform.position;
            hasWanderPoint = false;
        }
        wasFollowing = isFollowing;

        // ¿Deambula? Sí cuando no sigue a Nami, o cuando ya está cerca de ella.
        bool wandering = false;
        Vector3 wanderCenter = homePos;
        float wanderRadius = farWanderRadius;

        if (!approaching)
        {
            if (!isFollowing)
            {
                wandering = true;
            }
            else if (Vector3.Distance(transform.position, lastSeenPos) <= nearWanderRadius)
            {
                wandering = true;
                wanderCenter = lastSeenPos;
                wanderRadius = nearWanderRadius;
            }
        }

        // 1. Punto meta
        Vector3 goal;
        if (approaching)
        {
            goal = approachPoint.position; // NUEVO
        }
        else if (wandering)
        {
            wanderTimer += dt;
            if (!hasWanderPoint
                || wanderTimer >= wanderPointTimeout
                || Vector3.Distance(transform.position, wanderPoint) <= wanderPointReach
                || Vector3.Distance(wanderPoint, wanderCenter) > wanderRadius * 1.1f)
            {
                PickWanderPoint(wanderCenter, wanderRadius, isFollowing);
            }
            goal = wanderPoint;
        }
        else
        {
            Vector3 flatOffset = transform.position - lastSeenPos;
            flatOffset.y = 0f;
            goal = lastSeenPos
                 + flatOffset.normalized * followDistance
                 + Vector3.up * heightOffset;
        }

        // El punto meta nunca queda bajo tierra
        float goalFloor = GroundHeight(goal) + groundClearance;
        if (goal.y < goalFloor) goal.y = goalFloor;

        // 2. Velocidad
        Vector3 toGoal = goal - transform.position;
        float dist = toGoal.magnitude;

        // NUEVO: ¿ya llegó al punto (o se acabó el tiempo)? Se suelta a seguir por rango.
        if (approaching)
        {
            approachTimer += dt;
            if (dist <= arriveDistance || approachTimer >= approachTimeout)
                FinishApproach();
        }

        float desired = wandering
            ? maxSpeed * wanderSpeedFactor                       // pasea a velocidad constante, sin frenar
            : maxSpeed * Mathf.Clamp01(dist / slowRadius);       // yendo a Nami / al punto: frena al llegar
        speed = Mathf.MoveTowards(speed, desired, acceleration * dt);

        // 3. Giro limitado
        if (dist > 0.05f)
        {
            Quaternion look = Quaternion.LookRotation(toGoal);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, look, turnSpeed * dt);
        }

        // 4. Avanzar
        transform.position += transform.forward * speed * dt;

        // Espacio personal
        Vector3 fromTarget = transform.position - target.position;
        if (fromTarget.magnitude < minDistance)
            transform.position = target.position + fromTarget.normalized * minDistance;

        // Protección dura del terreno
        float floorY = Mathf.Max(
            GroundHeight(transform.position),
            GroundHeight(transform.position + transform.forward * groundLookAhead)
        ) + groundClearance;

        if (transform.position.y < floorY)
        {
            Vector3 p = transform.position;
            p.y = Mathf.Lerp(p.y, floorY, 5f * dt);
            transform.position = p;
        }

        // 5. Animación
        animator.SetFloat("Speed", (speed / maxSpeed) * 0.4f, 0.1f, dt);
        animator.speed = Mathf.Lerp(0.15f, 0.4f, speed / maxSpeed);
    }

    float GroundHeight(Vector3 pos)
    {
        Terrain t = Terrain.activeTerrain;
        if (t == null) return float.NegativeInfinity;
        return t.SampleHeight(pos) + t.transform.position.y;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, followRange);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, nearWanderRadius);
        if (hasWanderPoint) Gizmos.DrawLine(transform.position, wanderPoint);
    }
}
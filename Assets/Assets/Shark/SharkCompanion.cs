using UnityEngine;

public class SharkCompanion : MonoBehaviour
{
    public void SetFollow(bool value) => followEnabled = value; // shark.SetFollow(true/false) para activar esta chimbada

    [SerializeField] Transform target;
    [SerializeField] Animator animator;

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

    float speed;
    bool isFollowing;

    void Update()
    {
        float dt = Time.deltaTime;

        // 0. ¿Sigue o no al jugador?
        float distToPlayer = Vector3.Distance(transform.position, target.position);
        if (!followEnabled) isFollowing = false;
        else if (isFollowing && distToPlayer > followRange) isFollowing = false;
        else if (!isFollowing && distToPlayer < resumeRange) isFollowing = true;

        // 1. Punto meta
        Vector3 flatOffset = transform.position - target.position;
        flatOffset.y = 0f;
        Vector3 goal = target.position
                     + flatOffset.normalized * followDistance
                     + Vector3.up * heightOffset;

        // El punto meta nunca queda bajo tierra
        float goalFloor = GroundHeight(goal) + groundClearance;
        if (goal.y < goalFloor) goal.y = goalFloor;

        // 2. Velocidad
        Vector3 toGoal = goal - transform.position;
        float dist = toGoal.magnitude;
        float desired = isFollowing ? maxSpeed * Mathf.Clamp01(dist / slowRadius) : 0f;
        speed = Mathf.MoveTowards(speed, desired, acceleration * dt);

        // 3. Giro limitado
        if (isFollowing && dist > 0.05f)
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
    }
}
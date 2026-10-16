using UnityEngine;

public class SharkCompanion : MonoBehaviour
{
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

    [Header("Terreno")]
    [SerializeField] float groundClearance = 3f;
    [SerializeField] float groundLookAhead = 8f;

    float speed;

    void Update()
    {
        float dt = Time.deltaTime;

        // 1. Punto meta: a followDistance del jugador, del lado donde ya está el tiburón
        Vector3 flatOffset = transform.position - target.position;
        flatOffset.y = 0f;
        Vector3 goal = target.position
                     + flatOffset.normalized * followDistance
                     + Vector3.up * heightOffset;

        // 2. Velocidad: frena al acercarse, acelera de a poco
        Vector3 toGoal = goal - transform.position;
        float dist = toGoal.magnitude;
        float desired = maxSpeed * Mathf.Clamp01(dist / slowRadius);
        speed = Mathf.MoveTowards(speed, desired, acceleration * dt);

        // 3. Giro limitado: nunca asignamos la rotación directa
        if (dist > 0.05f)
        {
            Quaternion look = Quaternion.LookRotation(toGoal);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, look, turnSpeed * dt);
        }

        // 4. Avanzar hacia donde mira
        transform.position += transform.forward * speed * dt;
        Vector3 fromTarget = transform.position - target.position;
        if (fromTarget.magnitude < minDistance)
        {
            transform.position = target.position + fromTarget.normalized * minDistance;
        }
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

        // 5. Animación según velocidad
        animator.SetFloat("Speed", (speed / maxSpeed) * 0.4f, 0.1f, dt);
        animator.speed = Mathf.Lerp(0.15f, 0.4f, speed / maxSpeed);
        float GroundHeight(Vector3 pos)
        {
            Terrain t = Terrain.activeTerrain;
            if (t == null) return float.NegativeInfinity;
            return t.SampleHeight(pos) + t.transform.position.y;

            float goalFloor = GroundHeight(goal) + groundClearance;
            if (goal.y < goalFloor) goal.y = goalFloor;
        }
    }
}
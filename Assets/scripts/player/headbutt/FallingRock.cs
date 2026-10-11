using UnityEngine;

// Va en cada prefab de roca que cae desde la superficie.
// Requiere DestructibleRock (para poder romperse) y un Rigidbody, que movemos
// a mano con MovePosition — igual que Mara y las cinemáticas — para que la
// caída sea 100% predecible y nunca se pelee con la física del motor.
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(DestructibleRock))]
public class FallingRock : MonoBehaviour
{
    public enum RockTarget { Nami, Mara }

    [Header("Caída")]
    [SerializeField] private float fallSpeed = 3f;
    [Tooltip("Si la roca cae por debajo de esta altura Y sin haber llegado a su objetivo " +
             "(por ejemplo, el objetivo se movió lejos), se autodestruye para no dejar basura.")]
    [SerializeField] private float destroyBelowY = -50f;

    [Header("Proximidad")]
    [Tooltip("A qué distancia del objetivo se considera que la roca 'llegó' y resuelve su efecto.")]
    [SerializeField] private float proximityThreshold = 1.5f;

    private Rigidbody rb;
    private DestructibleRock destructibleRock;
    private RockSequenceManager sequenceManager;

    private RockTarget targetType;
    private Transform target;
    private bool resolved; // ya rompió o ya impactó: dejar de revisar cada frame

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true; // la movemos a mano
        rb.useGravity = false;

        destructibleRock = GetComponent<DestructibleRock>();
    }

    // Llamado por RockFallSpawner justo después de instanciar la roca.
    public void Setup(RockTarget type, Transform targetTransform, RockSequenceManager manager)
    {
        targetType = type;
        target = targetTransform;
        sequenceManager = manager;

        // Si esta roca en particular la rompe el JUGADOR a cabezazos (con Y),
        // avisamos al manager para que sume al contador del objetivo.
        destructibleRock.BrokenByPlayer += HandlePlayerBreak;
    }

    private void HandlePlayerBreak()
    {
        sequenceManager?.RegisterPlayerBreak();
    }

    private void FixedUpdate()
    {
        if (resolved) return;

        // Si ya se rompió (cabezazo del jugador, o ForceBreak), DestructibleRock
        // ya destruyó el GameObject: no hay nada más que hacer acá.
        if (destructibleRock.IsBroken)
        {
            resolved = true;
            return;
        }

        rb.MovePosition(rb.position + Vector3.down * fallSpeed * Time.fixedDeltaTime);

        if (rb.position.y < destroyBelowY)
        {
            resolved = true;
            Destroy(gameObject);
            return;
        }

        if (target != null && Vector3.Distance(rb.position, target.position) <= proximityThreshold)
            ResolveProximity();
    }

    private void ResolveProximity()
    {
        resolved = true;

        if (targetType == RockTarget.Mara)
        {
            // Mara la controla el juego: "rompe"/esquiva la roca sola.
            // No cuenta como logro del jugador (no se llamó TakeHit).
            destructibleRock.ForceBreak();
        }
        else
        {
            // Le llegó a Nami sin que el jugador alcanzara a romperla con Y:
            // avisamos al manager (ralentiza + Mara dice "cuidado") y la rompemos.
            sequenceManager?.OnRockHitPlayer();
            destructibleRock.ForceBreak();
        }
    }
}

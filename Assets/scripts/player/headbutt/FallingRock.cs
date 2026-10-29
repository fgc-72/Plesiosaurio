using UnityEngine;

// Va en cada prefab de roca que cae desde la superficie.
// Requiere DestructibleRock (para poder romperse) y un Rigidbody, que movemos
// a mano con MovePosition — igual que Mara y las cinemáticas — para que la
// caída sea 100% predecible y nunca se pelee con la física del motor.
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(DestructibleRock))]
public class FallingRock : MonoBehaviour
{
    // Ambient: roca puramente decorativa, no persigue a nadie, no rompe nada,
    // solo cae para ambientar la zona (sensación de peligro de fondo).
    public enum RockTarget { Nami, Mara, Ambient }

    [Header("Caída")]
    [SerializeField] private float fallSpeed = 3f;
    [Tooltip("Si la roca cae por debajo de esta altura Y sin haber llegado a su objetivo " +
             "(o si es ambiental, sin más), se autodestruye para no dejar basura. Las " +
             "ambientales normalmente pueden usar un valor más alto (más cerca de la " +
             "superficie visible) ya que no necesitan llegar hasta ningún personaje.")]
    [SerializeField] private float destroyBelowY = -50f;

    [Header("Proximidad")]
    [Tooltip("Margen extra (en metros) que se suma al radio real de la roca y al del " +
             "objetivo. Súbelo un poco si todavía ves que la roca se mete antes de romperse.")]
    [SerializeField] private float proximityMargin = 0.3f;
    [Tooltip("Radio aproximado del objetivo (Nami o Mara), para que la roca resuelva " +
             "justo cuando su SUPERFICIE toca al objetivo, no cuando sus centros coinciden.")]
    [SerializeField] private float targetRadius = 1.2f;

    [Header("Ventana de reacción (cabezazo)")]
    [Tooltip("Solo aplica a rocas dirigidas a NAMI. Cuando la roca entra en el radio de " +
             "proximidad, en vez de resolverse en ese mismo instante se QUEDA QUIETA este " +
             "tiempo, dándote una ventana real para pegarle con Y antes de que te golpee. " +
             "Si el cabezazo se siente imposible de conectar, sube este valor.")]
    [SerializeField] private float hitGracePeriod = 0.5f;

    private Rigidbody rb;
    private DestructibleRock destructibleRock;
    private RockSequenceManager sequenceManager;

    private RockTarget targetType;
    private Transform target;
    private bool resolved; // ya rompió o ya impactó: dejar de revisar cada frame
    private bool inGracePeriod; // ya entró en rango y está "congelada" dando tiempo a reaccionar
    private float proximityThreshold; // calculado en Awake según el tamaño real de ESTA roca

    private void OnTriggerEnter(Collider other) => TryHitByBody(other);
    private void OnCollisionEnter(Collision collision) => TryHitByBody(collision.collider);

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true; // la movemos a mano
        rb.useGravity = false;

        destructibleRock = GetComponent<DestructibleRock>();

        proximityThreshold = GetOwnRadius() + targetRadius + proximityMargin;
    }

    // Radio aproximado de ESTA roca, sacado de su propio Collider (ya escalado).
    // Así una roca grande resuelve más lejos que una chica, en vez de usar la
    // misma distancia fija para todos los tamaños.
    private float GetOwnRadius()
    {
        Collider col = GetComponent<Collider>();
        if (col == null) return 1f;

        if (col is SphereCollider sphere)
            return sphere.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z);

        // Para cualquier otro tipo de collider, usamos la mitad de la diagonal de sus bounds
        // como aproximación razonable del radio.
        return col.bounds.extents.magnitude;
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

        // Mientras está en la ventana de gracia, la roca se queda quieta en el aire
        // (telegraph) — el coroutine de abajo decide cuándo se resuelve.
        if (inGracePeriod)
            return;

        rb.MovePosition(rb.position + Vector3.down * fallSpeed * Time.fixedDeltaTime);

        if (rb.position.y < destroyBelowY)
        {
            resolved = true;
            Destroy(gameObject);
            return;
        }

        // Mara es controlada por IA: si la roca llega a su radio de proximidad, se resuelve/rompe sola
        if (target != null && targetType == RockTarget.Mara && Vector3.Distance(rb.position, target.position) <= proximityThreshold)
        {
            ResolveProximity();
        }
    }

    private System.Collections.IEnumerator GracePeriodThenResolve()
    {
        inGracePeriod = true;

        float elapsed = 0f;
        while (elapsed < hitGracePeriod)
        {
            // Si el jugador logró el cabezazo durante la ventana de gracia,
            // DestructibleRock ya la rompió: cortamos acá, nada que resolver.
            if (destructibleRock.IsBroken)
            {
                resolved = true;
                yield break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (!resolved && !destructibleRock.IsBroken)
            ResolveProximity();
    }

    private void ResolveProximity()
    {
        resolved = true;
        inGracePeriod = false;

        if (targetType == RockTarget.Mara)
        {
            // Mara la controla el juego: "rompe"/esquiva la roca sola.
            // No cuenta como logro del jugador (no se llamó TakeHit).
            destructibleRock.ForceBreak();
        }
        else
        {
            Debug.Log($"💥 {name} alcanzó a Nami sin que la rompieran");
            // Le llegó a Nami sin que el jugador alcanzara a romperla con Y:
            // avisamos al manager (ralentiza + Mara dice "cuidado") y la rompemos.
            if (sequenceManager == null)
                sequenceManager = Object.FindFirstObjectByType<RockSequenceManager>();

            sequenceManager?.OnRockHitPlayer();
            destructibleRock.ForceBreak();
        }
    }

    private void TryHitByBody(Collider other)
    {
    

        if (resolved || destructibleRock.IsBroken) return;

        PlayerControls player = other.GetComponentInParent<PlayerControls>();
        if (player == null) return;

        // Si Nami está ejecutando un cabezazo activo, dejamos que HeadbuttHitbox se encargue de TakeHit()
        HeadbuttHitbox hitbox = player.GetComponentInChildren<HeadbuttHitbox>();
        if (hitbox != null && hitbox.IsActive)
            return;

        // Nami tocó la roca físicamente con su cuerpo: se rompe inmediatamente y NO cuenta
        // como que Nami la destruyó (no suma al contador de misión), porque fue golpe a Nami.
        resolved = true;
        inGracePeriod = false;

        Debug.Log($"💥 {name} golpeó el cuerpo de Nami (se rompe sin contar para la misión)");

        if (sequenceManager == null)
            sequenceManager = Object.FindFirstObjectByType<RockSequenceManager>();

        sequenceManager?.OnRockHitPlayer();
        destructibleRock.ForceBreak();
    }
}

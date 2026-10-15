using UnityEngine;

// "Correa invisible" entre Nami y Mara: deja explorar libremente alrededor de
// ella, pero frena poco a poco al jugador si se aleja demasiado, para que no
// pueda llegar solo hasta el evento que depende de ir acompañado.
//
// No es una pared: dentro de 'softRadius' no pasa nada; entre 'softRadius' y
// 'hardRadius' se va frenando SOLO el movimiento que aleja de Mara (moverse de
// lado o acercarse sigue igual de libre); en 'hardRadius' ya no puede alejarse más.
//
// Ponlo en Nami (mismo GameObject que PlayerControls). Si Mara no existe (por
// ejemplo, escena nueva donde Nami está sola) o se destruye, no hace nada.
[DefaultExecutionOrder(100)] // corre DESPUÉS de PlayerControls en cada FixedUpdate
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(PlayerControls))]
public class MaraTether : MonoBehaviour
{
    [Tooltip("Opcional: si lo dejas vacío (o Mara se destruye al cambiar de escena), se " +
             "busca sola por el tag de abajo. Si en la escena actual no hay Mara, la " +
             "correa simplemente no hace nada.")]
    [SerializeField] private Transform mara;
    [Tooltip("Tag que tiene Mara en las escenas donde aparece. Déjalo vacío para no buscarla " +
             "automáticamente (solo usaría la asignada a mano).")]
    [SerializeField] private string maraTag = "Mara";
    [SerializeField] private float searchInterval = 1f;

    [Header("Radios (metros)")]
    [Tooltip("Hasta aquí Nami se mueve con total libertad. Ponlo MÁS GRANDE que la " +
             "'leashDistance' de MaraGuide, para que Mara se detenga a esperar antes " +
             "de que la correa empiece a notarse.")]
    [SerializeField] private float softRadius = 25f;
    [Tooltip("Aquí Nami ya no puede alejarse más de Mara.")]
    [SerializeField] private float hardRadius = 40f;

    [Header("Aviso (opcional)")]
    [Tooltip("ID de diálogo de Mara tipo 'no te alejes tanto'. Sale cuando Nami llega " +
             "al límite, como máximo una vez cada 'warningCooldown' segundos.")]
    [SerializeField] private string warningDialogueId;
    [SerializeField] private float warningCooldown = 20f;

    private Rigidbody rb;
    private PlayerControls playerControls;
    private float nextWarningTime;
    private float nextSearchTime;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        playerControls = GetComponent<PlayerControls>();
    }

    private void FixedUpdate()
    {
        // Una cinemática tiene a Nami: no interferimos.
        if (!playerControls.IsControlEnabled)
            return;

        // Mara destruida (cambio de escena) o sin asignar: intentamos encontrarla de
        // nuevo, pero solo cada 'searchInterval' segundos para no gastar rendimiento.
        if (mara == null)
        {
            TryFindMara();
            if (mara == null)
                return; // en esta escena no hay Mara: la correa no hace nada
        }

        // Mara existe pero está desactivada (ej. ya se separaron): tampoco aplica.
        if (!mara.gameObject.activeInHierarchy)
            return;

        Vector3 offset = rb.position - mara.position;
        float distance = offset.magnitude;

        if (distance <= softRadius || distance < 0.001f)
            return;

        Vector3 awayDirection = offset / distance;

        // 0 en el radio suave, 1 en el radio duro.
        float t = Mathf.InverseLerp(softRadius, hardRadius, distance);

        // Solo tocamos la parte de la velocidad que ALEJA de Mara.
        Vector3 velocity = rb.linearVelocity;
        float outwardSpeed = Vector3.Dot(velocity, awayDirection);

        if (outwardSpeed > 0f)
            rb.linearVelocity = velocity - awayDirection * (outwardSpeed * t);

        if (t >= 1f)
            TryShowWarning();
    }

    private void TryFindMara()
    {
        if (string.IsNullOrEmpty(maraTag) || Time.time < nextSearchTime)
            return;

        nextSearchTime = Time.time + searchInterval;

        // FindWithTag devuelve null si no hay nadie con ese tag (no lanza error).
        // Si el tag ni siquiera existe en el proyecto, Unity lanza una excepción:
        // la atrapamos para que nunca rompa el juego en una escena sin ese tag.
        try
        {
            GameObject found = GameObject.FindWithTag(maraTag);
            if (found != null)
                mara = found.transform;
        }
        catch (UnityException)
        {
            maraTag = ""; // el tag no existe: dejamos de buscarla
            Debug.LogWarning("MaraTether: el tag de Mara no existe en el proyecto, " +
                              "no se buscará automáticamente. Crea el tag 'Mara' o asigna a Mara a mano.");
        }
    }

    private void TryShowWarning()
    {
        if (string.IsNullOrEmpty(warningDialogueId) || Time.time < nextWarningTime)
            return;

        if (ThoughtUI.Instance == null || ThoughtUI.Instance.IsShowing)
            return;

        nextWarningTime = Time.time + warningCooldown;
        ThoughtUI.Instance.ShowById(warningDialogueId);
    }

    // Dibuja los dos radios alrededor de Mara en el Scene View para ajustarlos a ojo.
    private void OnDrawGizmosSelected()
    {
        if (mara == null) return;

        Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.5f);
        Gizmos.DrawWireSphere(mara.position, softRadius);
        Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.6f);
        Gizmos.DrawWireSphere(mara.position, hardRadius);
    }
}
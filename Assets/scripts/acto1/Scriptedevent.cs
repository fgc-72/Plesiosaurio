using UnityEngine;
using Unity.Cinemachine;
using UnityEngine.InputSystem;
using System.Collections;

// Componente genérico para las cinemáticas/eventos scriptados que se repiten
// en varios actos (ej. Acto I: "Separación de Mara", Acto IV: "Cápsula de Dimitri").
//
// Ponlo en un GameObject con un Collider marcado como Trigger, en el punto del
// mapa donde debe dispararse el evento. Configura SOLO lo que necesites:
// movimiento forzado, cámara guiada, vibración — cualquier combinación es válida.
[RequireComponent(typeof(Collider))]
public class ScriptedEvent : MonoBehaviour
{
    public enum ForcedMovementMode { Ninguno, EmpujeEnDireccion, SeguirCamino }

    [Header("Activación")]
    [SerializeField] private string playerTag = "Player";
    [Tooltip("Si es true, el evento solo puede dispararse una vez.")]
    [SerializeField] private bool triggerOnce = true;

    [Header("Condición: Mara cerca (opcional)")]
    [Tooltip("Si es true, el evento NO se activa hasta que Mara esté cerca de Nami. Si Nami " +
             "llega sola, no pasa nada; apenas Mara llega (y Nami sigue dentro del trigger), " +
             "el evento arranca. Actívalo solo en el evento que depende de ir con Mara.")]
    [SerializeField] private bool requireMaraNearby = false;
    [SerializeField] private Transform maraTransform;
    [Tooltip("Distancia máxima entre Mara y Nami para considerar que 'vienen juntas'.")]
    [SerializeField] private float maxMaraDistance = 15f;
    [Tooltip("Opcional: la correa de Nami (MaraTether) se apaga cuando este evento arranca, " +
             "porque desde aquí ya no se necesita.")]
    [SerializeField] private MaraTether tetherToRelease;

    [Header("Movimiento forzado (opcional)")]
    [SerializeField] private ForcedMovementMode movementMode = ForcedMovementMode.Ninguno;
    [Tooltip("Dirección LOCAL del empuje (relativa a este objeto). Solo para 'Empuje En Dirección'.")]
    [SerializeField] private Vector3 pushDirection = Vector3.forward;
    [SerializeField] private float pushSpeed = 4f;
    [Tooltip("Puntos a seguir en orden. Solo para 'Seguir Camino'.")]
    [SerializeField] private Transform[] pathWaypoints;
    [SerializeField] private float pathSpeed = 3f;
    [Tooltip("Duración del empuje forzado (solo aplica al modo 'Empuje En Dirección').")]
    [SerializeField] private float forcedDuration = 3f;

    [Header("Cámara guiada (opcional)")]
    [Tooltip("Cámara virtual de Cinemachine a activar durante el evento. Vacío = no cambia cámara.")]
    [SerializeField] private CinemachineCamera guidedCamera;
    [SerializeField] private int guidedCameraPriority = 20;

    [Header("Diálogo (opcional)")]
    [Tooltip("Si lo llenas Y el movimiento es 'Ninguno', el evento espera a que termine este " +
             "diálogo (en vez de 'forcedDuration') antes de devolver el control. Ideal para " +
             "cinemáticas de solo cámara+texto, como 'Nami despierta sola en otro lugar'.")]
    [SerializeField] private string dialogueId;

    [Header("Misión al terminar (opcional)")]
    [Tooltip("ID de la misión (MissionDatabase) a mostrar justo cuando el jugador recupera el " +
             "control, al final del evento. Déjalo vacío si este evento no activa ninguna misión.")]
    [SerializeField] private string missionIdOnFinish;

    [Header("Vibración del control (opcional)")]
    [SerializeField] private bool useVibration = false;
    [Range(0f, 1f)] [SerializeField] private float vibrationStrength = 0.6f;

    [Header("Animación durante el evento (opcional)")]
    [Tooltip("Si es true, congela la animación normal y usa el valor de abajo en su lugar.")]
    [SerializeField] private bool overrideAnimation = false;
    [Tooltip("Valor que se le pone al parámetro 'AnimationState' del Animator mientras dura el evento. " +
             "Por ahora puedes reutilizar uno que ya exista (0=Idle, 1=Nadando, 2=Subiendo, 3=Bajando) " +
             "hasta que tengas una animación específica de 'arrastrado/indefenso'.")]
    [SerializeField] private int animationStateDuringEvent = 1;

    [Header("Activar algo al soltar al jugador (opcional)")]
    [Tooltip("Spawner de rocas que debe arrancar justo cuando este evento termina y el " +
             "jugador recupera el control (no antes, mientras todavía está bloqueado " +
             "viendo texto/cámara). Déjalo vacío si este evento no tiene que ver con rocas.")]
    [SerializeField] private RockFallSpawner rockSpawner;

    private bool hasTriggered;

    private void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        TryActivate(other);
    }

    // Mientras Nami siga dentro del trigger reintentamos: si llegó sin Mara y Mara
    // llega después, el evento arranca en ese momento (OnTriggerEnter solo corre una vez).
    private void OnTriggerStay(Collider other)
    {
        if (requireMaraNearby)
            TryActivate(other);
    }

    private void TryActivate(Collider other)
    {
        if (hasTriggered && triggerOnce)
            return;

        if (!other.CompareTag(playerTag))
            return;

        if (requireMaraNearby && !IsMaraNearby(other.transform))
            return;

        hasTriggered = true;

        if (tetherToRelease != null)
            tetherToRelease.enabled = false;

        PlayerControls playerControls = other.GetComponent<PlayerControls>();
        Rigidbody rb = other.GetComponent<Rigidbody>();

        StartCoroutine(RunEvent(playerControls, rb));
    }

    private bool IsMaraNearby(Transform player)
    {
        if (maraTransform == null || !maraTransform.gameObject.activeInHierarchy)
            return false; // sin Mara asignada/activa, la condición no se cumple

        return Vector3.Distance(maraTransform.position, player.position) <= maxMaraDistance;
    }

    // Dispara el evento directamente por CÓDIGO, sin esperar a que el jugador pise
    // un Collider — para cuando otro sistema (ej. RockFallSpawner apenas empiezan a
    // caer las rocas) ya sabe que es el momento exacto de activar cámara/diálogo/etc.
    public void TriggerManually(PlayerControls playerControls, Rigidbody rb)
    {
        if (hasTriggered && triggerOnce)
            return;

        hasTriggered = true;
        StartCoroutine(RunEvent(playerControls, rb));
    }

    private IEnumerator RunEvent(PlayerControls playerControls, Rigidbody rb)
    {
        if (playerControls != null)
            playerControls.SetControlEnabled(false);

        PlayerAnimation playerAnimation = playerControls != null
            ? playerControls.GetComponent<PlayerAnimation>()
            : null;

        if (overrideAnimation && playerAnimation != null)
        {
            playerAnimation.SetAnimationOverride(true);
            playerControls.GetComponent<Animator>().SetInteger("AnimationState", animationStateDuringEvent);
        }

        int originalPriority = 0;
        if (guidedCamera != null)
        {
            originalPriority = guidedCamera.Priority;
            guidedCamera.Priority = guidedCameraPriority;
        }

        if (useVibration)
            StartCoroutine(Vibrate(forcedDuration, vibrationStrength));

        switch (movementMode)
        {
            case ForcedMovementMode.EmpujeEnDireccion:
                yield return RunPush(rb);
                break;
            case ForcedMovementMode.SeguirCamino:
                yield return RunPath(rb);
                break;
            default:
                // Sin movimiento forzado y sin diálogo: comportamiento viejo, esperar un
                // tiempo fijo (útil para eventos puramente de cámara, como la cápsula de
                // Dimitri). Si HAY diálogo, el tiempo lo decide el diálogo (ver abajo),
                // así que acá no esperamos nada para no sumar una espera doble.
                if (string.IsNullOrEmpty(dialogueId))
                    yield return new WaitForSeconds(forcedDuration);
                break;
        }

        // El diálogo se maneja APARTE del movimiento (antes estaba metido solo en el
        // caso 'Ninguno' del switch de arriba, así que si el evento tenía empuje o
        // camino, el texto nunca se mostraba). Ahora se muestra sin importar el modo
        // de movimiento, justo después de que ese movimiento (si lo hay) termine.
        if (!string.IsNullOrEmpty(dialogueId))
        {
            if (ThoughtUI.Instance == null)
            {
                Debug.LogWarning($"ScriptedEvent en '{name}': dialogueId = '{dialogueId}' pero " +
                                  "ThoughtUI.Instance es null (no hay ningún ThoughtUI activo en la " +
                                  "escena en este momento), así que no se pudo mostrar el texto.");
            }
            else
            {
                // Si hay rocas asociadas a este evento, no esperamos a que el diálogo
                // TERMINE para activarlas: arrancan a la MITAD de su duración, en
                // paralelo, mientras Mara sigue hablando — así se sienten inmediatas
                // en vez de aparecer recién cuando el jugador recupera el control.
                if (rockSpawner != null)
                {
                    float duration = ThoughtUI.Instance.GetDurationById(dialogueId);
                    StartCoroutine(BeginRocksPartwayThroughDialogue(duration * 0.5f));
                }

                ThoughtUI.Instance.ShowById(dialogueId);
                // Primero esperamos a que REALMENTE aparezca (evita que el segundo
                // WaitUntil se resuelva de inmediato si el fade-in todavía no arrancó).
                yield return new WaitUntil(() => ThoughtUI.Instance.IsShowing);
                yield return new WaitUntil(() => !ThoughtUI.Instance.IsShowing);
            }
        }

        if (guidedCamera != null)
            guidedCamera.Priority = originalPriority;

        if (overrideAnimation && playerAnimation != null)
            playerAnimation.SetAnimationOverride(false);

        if (playerControls != null)
            playerControls.SetControlEnabled(true);

        // Apenas el jugador recupera el control, mostramos la misión nueva (si hay una).
        if (!string.IsNullOrEmpty(missionIdOnFinish))
        {
            if (MissionUI.Instance != null)
                MissionUI.Instance.ShowMission(missionIdOnFinish);
            else
                Debug.LogWarning($"ScriptedEvent en '{name}': missionIdOnFinish = " +
                                  $"'{missionIdOnFinish}' pero MissionUI.Instance es null.");
        }

        // Red de seguridad: si por lo que sea el disparo de arriba (a mitad del
        // diálogo) no llegó a pasar — por ejemplo, este evento no tiene dialogueId,
        // o usa movimiento forzado en vez de 'Ninguno' — las rocas igual arrancan
        // acá al final. BeginSpawning() ya se protege solo contra arrancar dos veces.
        if (rockSpawner != null)
            rockSpawner.BeginSpawning();
    }

    // Arranca el RockFallSpawner a mitad de la duración del diálogo, mientras Mara
    // sigue hablando, en vez de esperar a que termine.
    private IEnumerator BeginRocksPartwayThroughDialogue(float delay)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        if (rockSpawner != null)
            rockSpawner.BeginSpawning();
    }

    private IEnumerator RunPush(Rigidbody rb)
    {
        if (rb == null) yield break;

        Vector3 worldDirection = transform.TransformDirection(pushDirection.normalized);
        float elapsed = 0f;

        while (elapsed < forcedDuration)
        {
            rb.linearVelocity = worldDirection * pushSpeed;
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private IEnumerator RunPath(Rigidbody rb)
    {
        if (rb == null || pathWaypoints == null) yield break;

        foreach (Transform point in pathWaypoints)
        {
            if (point == null) continue;

            while (Vector3.Distance(rb.position, point.position) > 0.2f)
            {
                Vector3 direction = (point.position - rb.position).normalized;
                rb.MovePosition(rb.position + direction * pathSpeed * Time.fixedDeltaTime);
                yield return new WaitForFixedUpdate();
            }
        }
    }

    private IEnumerator Vibrate(float duration, float strength)
    {
        Gamepad pad = Gamepad.current;
        if (pad == null) yield break;

        pad.SetMotorSpeeds(strength, strength);
        yield return new WaitForSeconds(duration);
        pad.SetMotorSpeeds(0f, 0f);
    }

    // Dibuja el camino en el Scene View para que puedas acomodar los waypoints a ojo.
    private void OnDrawGizmos()
    {
        if (pathWaypoints == null || pathWaypoints.Length < 2)
            return;

        Gizmos.color = Color.cyan;
        for (int i = 0; i < pathWaypoints.Length - 1; i++)
        {
            if (pathWaypoints[i] != null && pathWaypoints[i + 1] != null)
                Gizmos.DrawLine(pathWaypoints[i].position, pathWaypoints[i + 1].position);
        }
    }
}
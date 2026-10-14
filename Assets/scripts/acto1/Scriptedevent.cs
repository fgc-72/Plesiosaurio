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
        if (hasTriggered && triggerOnce)
            return;

        if (!other.CompareTag(playerTag))
            return;

        hasTriggered = true;

        PlayerControls playerControls = other.GetComponent<PlayerControls>();
        Rigidbody rb = other.GetComponent<Rigidbody>();

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

        // Justo AQUÍ, no antes: recién cuando el jugador recupera el control de
        // verdad (ya pasó el texto/cámara/movimiento forzado) arrancan las rocas.
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
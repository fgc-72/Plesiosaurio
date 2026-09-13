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
                // Sin movimiento forzado: solo esperamos la duración (útil para
                // eventos que son puramente de cámara, como la cápsula de Dimitri).
                yield return new WaitForSeconds(forcedDuration);
                break;
        }

        if (guidedCamera != null)
            guidedCamera.Priority = originalPriority;

        if (overrideAnimation && playerAnimation != null)
            playerAnimation.SetAnimationOverride(false);

        if (playerControls != null)
            playerControls.SetControlEnabled(true);
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
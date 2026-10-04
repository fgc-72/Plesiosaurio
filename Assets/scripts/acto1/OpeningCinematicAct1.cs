using UnityEngine;
using Unity.Cinemachine;
using System.Collections;

// Coordina la apertura del Acto I: Nami está acostada (bloqueada), Mara nada
// hacia ella, aparece el diálogo, ambas suben y se colocan lado a lado, se
// desbloquea el jugador, y se guarda el primer checkpoint justo ahí.
public class OpeningCinematicAct1 : MonoBehaviour
{
    [Header("Nami (jugador)")]
    [SerializeField] private PlayerControls playerControls;
    [SerializeField] private PlayerAnimation playerAnimation;
    [SerializeField] private Rigidbody namiRigidbody;

    [Header("Mara")]
    [SerializeField] private Rigidbody maraRigidbody;
    [Tooltip("El MaraGuide que vive EN Mara. Se desactiva durante la cinemática " +
             "para que no pelee por el control de su Rigidbody, y se reactiva al final.")]
    [SerializeField] private MaraGuide maraGuide;
    [Tooltip("Opcional: si Mara tiene Animator con el mismo parámetro 'AnimationState' que Nami.")]
    [SerializeField] private Animator maraAnimator;

    [Header("Puntos de la coreografía")]
    [Tooltip("A dónde nada Mara para acercarse a Nami (que sigue acostada).")]
    [SerializeField] private Transform maraApproachPoint;
    [Tooltip("Dónde terminan Nami y Mara, lado a lado, listas para nadar juntas.")]
    [SerializeField] private Transform namiSideBySidePoint;
    [SerializeField] private Transform maraSideBySidePoint;

    [Header("Diálogo (usa el sistema ThoughtUI del equipo)")]
    [Tooltip("IDs de las líneas en el DialogueDatabase, en el orden en que deben mostrarse.")]
    [SerializeField] private string[] dialogueLineIds;

    [Header("Movimiento de la cinemática")]
    [SerializeField] private float swimSpeed = 2.5f;
    [SerializeField] private float rotationSpeed = 3f;
    [SerializeField] private float arrivalThreshold = 0.3f;

    [Header("Cámara de la intro (dedicada, posicionada a mano)")]
    [Tooltip("Cinemachine Camera nueva, SIN Orbital Follow, posicionada manualmente " +
             "para encuadrar a Nami acostada. Cinemachine hace el blend suave " +
             "automáticamente al cambiar de prioridad, sin saltos ni desincronización.")]
    [SerializeField] private CinemachineCamera introCamera;
    [SerializeField] private int introCameraPriority = 20;

    [Header("Checkpoint")]
    [SerializeField] private string sceneName = "Acto1";

    private int introCameraOriginalPriority;

    private void Start()
    {
        if (CheckpointManager.Instance != null && CheckpointManager.Instance.HasCheckpointFor(sceneName))
            SkipToCheckpoint();
        else
            StartCoroutine(PlayIntro());
    }

    private void SkipToCheckpoint()
    {
        Vector3 savedPosition = CheckpointManager.Instance.GetCheckpointPosition();
        namiRigidbody.position = savedPosition;
        playerControls.SetControlEnabled(true);

        // Restauramos también qué habilidades tenía desbloqueadas, para que no
        // "olvide" el progreso del tutorial al retomar la partida.
        CheckpointUnlocks unlocks = CheckpointManager.Instance.GetCheckpointUnlocks();
        InputManagerBueno inputManager = playerControls.GetComponent<InputManagerBueno>();
        if (inputManager != null)
        {
            inputManager.canMoveVertical = unlocks.canMoveVertical;
            inputManager.canSprint = unlocks.canSprint;
            inputManager.canHeadbutt = unlocks.canHeadbutt;
            inputManager.canListen = unlocks.canListen;
        }

        // No hubo cinemática que lo apagara, así que nos aseguramos de que
        // quede activo desde el arranque.
        if (maraGuide != null)
            maraGuide.enabled = true;
    }

    private IEnumerator PlayIntro()
    {
        // Nami se queda bloqueada (acostada) desde el principio: controlEnabled
        // ya debería estar en false por defecto mientras tanto no la desbloqueemos.
        playerControls.SetControlEnabled(false);

        // IMPORTANTE: apagamos MaraGuide mientras dure la cinemática. Si se queda
        // activo, su propio FixedUpdate sigue intentando mover/rotar a Mara hacia
        // SUS waypoints al mismo tiempo que esta coroutine la mueve hacia los
        // puntos de la cinemática — los dos peleando por el mismo Rigidbody cada
        // frame es justo lo que causa el "quiere girar y vuelve" en bucle.
        if (maraGuide != null)
            maraGuide.enabled = false;

        // Subimos la prioridad de la cámara de intro: Cinemachine hace el blend
        // automático desde la cámara normal hacia esta, sin que tengamos que
        // sincronizar ningún valor a mano.
        if (introCamera != null)
        {
            introCameraOriginalPriority = introCamera.Priority;
            introCamera.Priority = introCameraPriority;
        }

        // 1) Mara nada hacia Nami.
        SetSwimmingAnim(maraAnimator, true);
        yield return SwimTo(maraRigidbody, maraApproachPoint.position);
        SetSwimmingAnim(maraAnimator, false);

        // 2) Diálogo: se las pasamos todas a ThoughtUI, que las encola y las
        //    muestra una por una (fade in, espera su duración, fade out).
        //    Esperamos a que termine TODA la cola antes de seguir.
        if (ThoughtUI.Instance != null)
        {
            foreach (string id in dialogueLineIds)
                ThoughtUI.Instance.ShowById(id);

            yield return new WaitUntil(() => !ThoughtUI.Instance.IsShowing);
        }

        // Bajamos la prioridad AQUÍ, antes de que empiecen a subir — así el
        // jugador ya puede mirar alrededor (Cinemachine vuelve a la cámara
        // normal, con blend suave) mientras ve la subida, aunque todavía no
        // pueda MOVER a Nami (eso se desbloquea después, en el paso 4).
        if (introCamera != null)
            introCamera.Priority = introCameraOriginalPriority;

        // 3) Ambas suben y se colocan lado a lado, al mismo tiempo.
        //    Nami se mueve "a mano" (rb.MovePosition) mientras su control sigue
        //    desactivado — igual que hace ScriptedEvent durante una cinemática.
        playerAnimation.SetAnimationOverride(true);
        SetPlayerAnimState(1); // "Nadando"
        SetSwimmingAnim(maraAnimator, true);

        Coroutine namiMove = StartCoroutine(SwimTo(namiRigidbody, namiSideBySidePoint.position));
        Coroutine maraMove = StartCoroutine(SwimTo(maraRigidbody, maraSideBySidePoint.position));
        yield return namiMove;
        yield return maraMove;

        SetSwimmingAnim(maraAnimator, false);
        playerAnimation.SetAnimationOverride(false); // PlayerAnimation vuelve a decidir sola

        // 4) Desbloquear al jugador (movimiento horizontal libre; subir/bajar,
        //    sprint, cabezazo y modo escucha siguen bloqueados hasta sus propios
        //    InputUnlockZone más adelante en el nivel).
        playerControls.SetControlEnabled(true);

        // Recién AHORA reactivamos MaraGuide: a partir de aquí ella retoma su
        // patrullaje normal (nadar sola, esperar si Nami se aleja), que es
        // justo el comportamiento que describiste para después de esta escena.
        if (maraGuide != null)
            maraGuide.enabled = true;

        // 5) Primer checkpoint, justo aquí.
        if (CheckpointManager.Instance != null)
        {
            InputManagerBueno inputManager = playerControls.GetComponent<InputManagerBueno>();
            CheckpointUnlocks unlocks = inputManager != null
                ? new CheckpointUnlocks
                {
                    canMoveVertical = inputManager.canMoveVertical,
                    canSprint = inputManager.canSprint,
                    canHeadbutt = inputManager.canHeadbutt,
                    canListen = inputManager.canListen
                }
                : default;

            CheckpointManager.Instance.SaveCheckpoint(sceneName, namiRigidbody.position, unlocks);
        }
    }

    private IEnumerator SwimTo(Rigidbody rb, Vector3 destination)
    {
        while (Vector3.Distance(rb.position, destination) > arrivalThreshold)
        {
            Vector3 direction = (destination - rb.position).normalized;
            rb.MovePosition(rb.position + direction * swimSpeed * Time.fixedDeltaTime);

            if (direction.sqrMagnitude > 0.01f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
                rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime));
            }

            yield return new WaitForFixedUpdate();
        }
    }

    private void SetPlayerAnimState(int state)
    {
        Animator animator = playerControls.GetComponent<Animator>();
        if (animator != null)
            animator.SetInteger("AnimationState", state);
    }

    private void SetSwimmingAnim(Animator animator, bool isSwimming)
    {
        if (animator == null) return;
        animator.SetInteger("AnimationState", isSwimming ? 1 : 0);
    }
}
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
    private bool forcingLyingPose; // true mientras reafirmamos AnimationState=4 todos los frames

    private void Awake()
    {
        // Nos aseguramos de usar el MISMO PlayerAnimation que ya está usando
        // PlayerManager (GetComponent desde el Rigidbody de Nami), en vez de
        // confiar en lo que haya quedado arrastrado en el Inspector. Si hubiera
        // dos referencias distintas por error, el override que ponemos acá
        // nunca llegaría al PlayerAnimation real, y por eso se veía que Nami
        // dejaba de obedecer el override apenas dejábamos de forzar el valor.
        if (namiRigidbody != null)
        {
            PlayerAnimation realPlayerAnimation = namiRigidbody.GetComponent<PlayerAnimation>();
            if (realPlayerAnimation != null)
                playerAnimation = realPlayerAnimation;

            PlayerControls realPlayerControls = namiRigidbody.GetComponent<PlayerControls>();
            if (realPlayerControls != null)
                playerControls = realPlayerControls;
        }
    }

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

        // También restauramos a Mara en su posición guardada, para que no
        // aparezca en su punto de partida original de la cinemática.
        Vector3 savedMaraPosition = CheckpointManager.Instance.GetCheckpointMaraPosition();
        maraRigidbody.position = savedMaraPosition;

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

        // Forzamos la pose "acostada" (AnimationState = 4) desde el primer frame.
        // Con override activado, PlayerAnimation deja de decidir sola el estado
        // y se queda fija en la que le pongamos acá, hasta que lo liberemos más abajo.
        playerAnimation.SetAnimationOverride(true);
        SetPlayerAnimState(4); // Acostada (transición Any State -> Acostada, condición == 4)

        // Algo más (todavía sin identificar del todo) reescribe AnimationState en
        // paralelo, así que mientras tanto reafirmamos el 4 en TODOS los frames que
        // dura la pose acostada — no solo una vez. Se apaga justo antes del paso 3,
        // cuando toca cambiar a la animación de nado.
        forcingLyingPose = true;
        StartCoroutine(ForceAnimationStateWhile(() => forcingLyingPose, 4));

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

            // Primero esperamos a que el diálogo REALMENTE empiece a mostrarse.
            // Si no hacemos esto, puede pasar que ThoughtUI todavía no haya puesto
            // IsShowing en true en este mismo frame (arranca el fade-in después),
            // y entonces el siguiente WaitUntil (!IsShowing) se cumple de inmediato
            // porque nunca llegó a ser true — como si el diálogo nunca hubiera
            // empezado. Por eso se veía a Nami "levantarse" antes de que acabara el texto.
            yield return new WaitUntil(() => ThoughtUI.Instance.IsShowing);

            // Y ahora sí, esperamos a que termine de verdad (toda la cola de líneas).
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
        forcingLyingPose = false; // apaga el forzado de "Acostada" antes de pasar a nadar

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

            CheckpointManager.Instance.SaveCheckpoint(sceneName, namiRigidbody.position, maraRigidbody.position, unlocks);
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

    // Reescribe el parámetro todos los frames mientras condition() sea true. Lo usamos
    // para sostener la pose "Acostada" a la fuerza mientras investigamos qué más la
    // está pisando. 'state' es fijo (no necesitamos variarlo todavía).
    private IEnumerator ForceAnimationStateWhile(System.Func<bool> condition, int state)
    {
        while (condition())
        {
            SetPlayerAnimState(state);
            yield return null;
        }
    }

    private void SetPlayerAnimState(int state)
    {
        Animator animator = playerControls.GetComponent<Animator>();

        if (animator == null)
        {
            Debug.LogWarning("OpeningCinematicAct1: Nami no tiene componente Animator en el mismo " +
                              "GameObject que PlayerControls.");
            return;
        }

        animator.SetInteger("AnimationState", state);
    
    }

    private void SetSwimmingAnim(Animator animator, bool isSwimming)
    {
        if (animator == null)
        {
            Debug.LogWarning("OpeningCinematicAct1: 'Mara Animator' está vacío en el Inspector, " +
                              "así que no se le puede poner animación de nado a Mara durante la intro.");
            return;
        }

        animator.SetInteger("AnimationState", isSwimming ? 1 : 0);
    }
}
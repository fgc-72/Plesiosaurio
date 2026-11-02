using System.Collections;
using UnityEngine;

// Va en el tiburón. Maneja la cinemática de muerte cuando el tiburón alcanza a Nami:
// 1) Si Nami entra en el rango del tiburón (o toca su collider) MIENTRAS tiene control:
//    se para todo, tanto el tiburón como Nami dejan de andar de inmediato.
//    (Si el jugador esquiva fuera de rango, el tiburón sigue de largo y Nami queda a salvo).
// 2) Se funde a negro rápidamente.
// 3) Nami reaparece en el último checkpoint y el tiburón regresa a su inicio para reintentar.
public class SharkHitDetector : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private SharkChaser shark;
    [SerializeField] private CheckpointRespawner respawner;
    [SerializeField] private string playerTag = "Player";

    [Header("Hitbox de Ataque (Cubo / Barrido Frontal)")]
    [Tooltip("Centro de la caja de muerte relativo al tiburón (espacio local).")]
    [SerializeField] private Vector3 hitboxCenter = new Vector3(0f, 0f, 2.8f);

    [Tooltip("Dimensiones de la caja de muerte (ancho X para barrer el pasillo, alto Y, largo Z).")]
    [SerializeField] private Vector3 hitboxSize = new Vector3(14f, 6f, 3f);

    [Header("Escondites")]
    [Tooltip("Si es true y Nami está oculta en un HideZone (PlayerHiding.IsHidden), el tiburón no la atacará.")]
    [SerializeField] private bool respectHidingZones = true;

    [Header("Cinemática de muerte")]
    [Tooltip("Pausa en segundos con todo congelado (tiburón y Nami quietos) antes de iniciar el fundido a negro.")]
    [SerializeField] private float freezeDelay = 0.25f;
    [Tooltip("Duración rápida del fundido a negro (segundos).")]
    [SerializeField] private float fastFadeDuration = 0.35f;
    [Tooltip("Segundos en negro antes de reaparecer.")]
    [SerializeField] private float blackHoldDuration = 0.35f;
    [Tooltip("Duración del desvanecimiento de negro a la escena al revivir.")]
    [SerializeField] private float fadeFromBlackDuration = 0.5f;

    [Header("Después de reiniciar a Nami")]
    [Tooltip("El evento que activa al tiburón. Se vuelve a armar para que el reintento vuelva a mostrar al tiburón.")]
    [SerializeField] private SharkChaseEvent chaseEvent;
    [Tooltip("Si es true, el tiburón se vuelve a ocultar al reiniciar (y reaparece con el evento).")]
    [SerializeField] private bool hideAfterReset = true;

    private bool isResetting;
    private PlayerControls cachedControls;

    private void Awake()
    {
        if (shark == null)
            shark = GetComponent<SharkChaser>();

        if (respawner == null)
            respawner = GetComponent<CheckpointRespawner>() ?? FindFirstObjectByType<CheckpointRespawner>();

        if (chaseEvent == null)
            chaseEvent = FindFirstObjectByType<SharkChaseEvent>();
    }

    private void Update()
    {
        if (isResetting || shark == null || !shark.IsChasing)
            return;

        if (cachedControls == null)
            cachedControls = Object.FindFirstObjectByType<PlayerControls>();

        if (cachedControls == null)
            return;

        // IMPORTANTE: Nunca matar a Nami si está en plena cinemática o diálogo (controles desactivados).
        if (!cachedControls.IsControlEnabled)
            return;

        // Si Nami está dentro de un escondite, el tiburón la ignora
        if (respectHidingZones)
        {
            PlayerHiding hiding = cachedControls.GetComponent<PlayerHiding>()
                ?? cachedControls.GetComponentInParent<PlayerHiding>()
                ?? cachedControls.GetComponentInChildren<PlayerHiding>();

            if (hiding != null && hiding.IsHidden)
                return;
        }

        // Comprobamos si Nami se encuentra dentro de la hitbox cúbica en el espacio local del tiburón
        Vector3 localPlayerPos = transform.InverseTransformPoint(cachedControls.transform.position);
        Vector3 half = hitboxSize * 0.5f;

        bool insideX = Mathf.Abs(localPlayerPos.x - hitboxCenter.x) <= half.x;
        bool insideY = Mathf.Abs(localPlayerPos.y - hitboxCenter.y) <= half.y;
        bool insideZ = Mathf.Abs(localPlayerPos.z - hitboxCenter.z) <= half.z;

        if (insideX && insideY && insideZ)
        {
            TriggerKill(cachedControls.gameObject);
        }
    }

    public void TriggerKill(GameObject player)
    {
        if (isResetting) return;
        StartCoroutine(HitRoutine(player));
    }

    private IEnumerator HitRoutine(GameObject player)
    {
        isResetting = true;

        // 1) "se para todo, tanto el tiburón como Nami dejan de andar"
        if (shark != null)
            shark.StopChase();

        PlayerControls controls = player.GetComponent<PlayerControls>()
            ?? player.GetComponentInParent<PlayerControls>()
            ?? player.GetComponentInChildren<PlayerControls>()
            ?? Object.FindFirstObjectByType<PlayerControls>();

        PlayerAnimation playerAnim = controls != null ? controls.GetComponent<PlayerAnimation>() : null;
        Animator playerAnimator = controls != null ? controls.GetComponent<Animator>() : null;
        Rigidbody playerRb = controls != null ? controls.GetComponent<Rigidbody>() : player.GetComponent<Rigidbody>();

        if (controls != null)
            controls.SetControlEnabled(false);

        if (playerAnim != null)
            playerAnim.SetAnimationOverride(true);

        if (playerAnimator != null)
            playerAnimator.SetInteger("AnimationState", 0);

        if (playerRb != null && !playerRb.isKinematic)
        {
            playerRb.linearVelocity = Vector3.zero;
            playerRb.angularVelocity = Vector3.zero;
        }

        // Breve pausa para notar el impacto del alcance
        if (freezeDelay > 0f)
            yield return new WaitForSeconds(freezeDelay);

        // 2) "se funde a negro la escena rápidamente y se reinicia"
        if (respawner == null)
            respawner = GetComponent<CheckpointRespawner>() ?? FindFirstObjectByType<CheckpointRespawner>();

        GameObject targetPlayer = controls != null ? controls.gameObject : player;

        if (respawner != null)
        {
            Coroutine respawn = respawner.Respawn(targetPlayer, fastFadeDuration, blackHoldDuration);
            if (respawn != null)
                yield return respawn;
        }
        else
        {
            // Respaldo directo en caso de no tener CheckpointRespawner en escena
            ScreenBlackoutEffect blackout = ScreenBlackoutEffect.GetOrCreate();
            if (blackout != null)
                yield return blackout.FadeToBlack(fastFadeDuration);

            if (blackHoldDuration > 0f)
                yield return new WaitForSeconds(blackHoldDuration);

            string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (playerRb != null && controls != null && SaveSystem.IsSceneMatch(currentScene, "acto2") && CheckpointRespawner.HasAct2InitialPosition)
            {
                Vector3 cp = CheckpointRespawner.Act2InitialPosition;
                Quaternion cr = CheckpointRespawner.Act2InitialRotation;
                playerRb.position = cp;
                playerRb.rotation = cr;
                controls.transform.SetPositionAndRotation(cp, cr);
                Physics.SyncTransforms();
                playerRb.linearVelocity = Vector3.zero;
                playerRb.angularVelocity = Vector3.zero;
                controls.SyncRotationToCurrentTransform();
            }
            else if (playerRb != null && controls != null && CheckpointManager.Instance != null &&
                CheckpointManager.Instance.HasCheckpointFor(currentScene))
            {
                Vector3 cp = CheckpointManager.Instance.GetCheckpointPosition();
                playerRb.position = cp;
                playerRb.transform.position = cp;
                Physics.SyncTransforms();
                playerRb.linearVelocity = Vector3.zero;
                playerRb.angularVelocity = Vector3.zero;
                controls.SyncRotationToCurrentTransform();
            }

            if (blackout != null)
                yield return blackout.FadeFromBlack(fadeFromBlackDuration);
        }

        // Red de seguridad: asegurar que Nami siempre recupere el control tras reaparecer
        if (playerAnim != null)
            playerAnim.SetAnimationOverride(false);

        if (controls != null)
            controls.SetControlEnabled(true);

        // 3) El tiburón vuelve a su punto de inicio
        if (shark != null)
            shark.ResetToStart();

        isResetting = false;

        // 4) El evento de persecución se rearma para que el reintento vuelva a activarlo
        if (chaseEvent != null)
            chaseEvent.ResetEvent();

        // Si hideAfterReset es true, se oculta el tiburón hasta que Nami vuelva a pisar el trigger del evento
        if (hideAfterReset)
            gameObject.SetActive(false);
    }

    private void OnDrawGizmosSelected()
    {
        Matrix4x4 prevMatrix = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;

        // Dibujar el cubo de muerte con su borde visible y relleno translúcido
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.85f);
        Gizmos.DrawWireCube(hitboxCenter, hitboxSize);

        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.15f);
        Gizmos.DrawCube(hitboxCenter, hitboxSize);

        Gizmos.matrix = prevMatrix;
    }
}

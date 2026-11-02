using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// Devuelve a Nami al último checkpoint con fundido a negro (y borroso, si hay un
// un Volume con Depth Of Field en la escena). Es reutilizable: cualquier sistema que "mate" o
// "devuelva" al jugador (rocas, tiburón, etc.) solo llama a Respawn().
//
// Dónde reaparece:
//   1) El último checkpoint guardado POR CheckpointTrigger para esta escena.
//   2) Si no hay (todavía no pasó por ninguno), 'Fallback Spawn Point'.
//   3) Si tampoco hay, avisa en consola y Nami se queda donde estaba.
public class CheckpointRespawner : MonoBehaviour
{
    [Header("Dónde reaparece")]
    [SerializeField] private bool useCheckpointIfAvailable = true;
    [Tooltip("Respaldo para cuando todavía no hay ningún checkpoint guardado en esta escena.")]
    [SerializeField] private Transform fallbackSpawnPoint;

    [Header("Mara (opcional)")]
    [Tooltip("Si lo asignas, Mara también vuelve a su posición del checkpoint (solo si hay uno guardado).")]
    [SerializeField] private Transform mara;

    [Header("Pantalla negra")]
    [Tooltip("Segundos que se queda en negro antes de reaparecer.")]
    [SerializeField] private float blackHoldSeconds = 0.5f;
    [Tooltip("Opcional: diálogo que sale mientras la pantalla está en negro (se espera a que termine).")]
    [SerializeField] private string blackScreenDialogueId;

    [Header("Efectos")]
    [Tooltip("Si lo dejas vacío, se busca un ScreenBlackoutEffect en la escena.")]
    [SerializeField] private ScreenBlackoutEffect blackout;
    [Header("Desenfoque (se busca solo un Volume con Depth Of Field en la escena)")]
    [SerializeField] private float blurredAperture = 1f;
    [SerializeField] private float clearAperture = 16f;
    [SerializeField] private float blurredFocusDistance = 0.3f;
    [SerializeField] private float normalFocusDistance = 10f;
    [SerializeField] private float blurClearDuration = 2f;

    private DepthOfField depthOfField;

    private bool isRespawning;

    public bool IsRespawning => isRespawning;

    private static Vector3 s_act2InitialPosition;
    private static Quaternion s_act2InitialRotation = Quaternion.identity;
    private static bool s_hasAct2InitialPosition;

    public static bool HasAct2InitialPosition => s_hasAct2InitialPosition;
    public static Vector3 Act2InitialPosition => s_act2InitialPosition;
    public static Quaternion Act2InitialRotation => s_act2InitialRotation;

    public static void SetAct2InitialSpawn(Vector3 pos, Quaternion rot)
    {
        s_act2InitialPosition = pos;
        s_act2InitialRotation = rot;
        s_hasAct2InitialPosition = true;
        Debug.Log($"[CheckpointRespawner] Registrada posición inicial de Acto 2 en: {pos}");
    }

    public static void ClearAct2InitialSpawn()
    {
        s_hasAct2InitialPosition = false;
    }

    // 'player' puede ser null: entonces se busca a Nami por el tag "Player".
    public Coroutine Respawn(GameObject player = null, float customFadeDuration = -1f, float customHoldDuration = -1f)
    {
        if (isRespawning) return null;
        return StartCoroutine(RespawnRoutine(player, customFadeDuration, customHoldDuration));
    }

    private IEnumerator RespawnRoutine(GameObject player, float customFadeDuration, float customHoldDuration)
    {
        if (player == null)
            player = GameObject.FindWithTag("Player");

        PlayerControls controls = null;
        Rigidbody namiRb = null;

        if (player != null)
        {
            controls = player.GetComponent<PlayerControls>()
                ?? player.GetComponentInChildren<PlayerControls>()
                ?? player.GetComponentInParent<PlayerControls>();

            namiRb = player.GetComponent<Rigidbody>()
                ?? player.GetComponentInChildren<Rigidbody>()
                ?? player.GetComponentInParent<Rigidbody>();
        }

        if (controls == null)
            controls = Object.FindFirstObjectByType<PlayerControls>();

        if (controls != null && namiRb == null)
            namiRb = controls.GetComponent<Rigidbody>();

        if (controls == null || namiRb == null)
        {
            Debug.LogWarning("CheckpointRespawner: no encontré a Nami (tag 'Player' o PlayerControls/Rigidbody).");
            yield break;
        }

        PlayerAnimation playerAnimation = controls.GetComponent<PlayerAnimation>();
        Animator namiAnimator = controls.GetComponent<Animator>();

        isRespawning = true;

        if (blackout == null) blackout = ScreenBlackoutEffect.GetOrCreate();
        FindDepthOfField();

        // 1) Nami pierde el control y se queda quieta.
        controls.SetControlEnabled(false);
        if (playerAnimation != null) playerAnimation.SetAnimationOverride(true);
        if (namiAnimator != null) namiAnimator.SetInteger("AnimationState", 0);
        namiRb.linearVelocity = Vector3.zero;
        namiRb.angularVelocity = Vector3.zero;

        // 2) A negro rápido.
        if (blackout != null)
            yield return blackout.FadeToBlack(customFadeDuration);

        float hold = customHoldDuration >= 0f ? customHoldDuration : blackHoldSeconds;
        if (hold > 0f)
            yield return new WaitForSeconds(hold);

        // 3) (Opcional) diálogo con la pantalla en negro.
        yield return ShowDialogueAndWait(blackScreenDialogueId);

        // 4) Teletransporte, todavía en negro.
        Teleport(namiRb, controls);
        SetBlurred();

        // Restaurar habilidades para que persistan tras reaparecer
        RestoreCheckpointUnlocks(controls);

        // 5) Vuelve de negro (y el borroso se aclara, si hay).
        if (blackout != null)
            yield return blackout.FadeFromBlack(customFadeDuration > 0f ? customFadeDuration : -1f);
        if (depthOfField != null)
            yield return ClearBlur();

        // 6) Nami recupera el control.
        if (playerAnimation != null) playerAnimation.SetAnimationOverride(false);
        controls.SetControlEnabled(true);

        isRespawning = false;
    }

    public void TeleportPlayerDirect(Rigidbody namiRb, PlayerControls controls)
    {
        Teleport(namiRb, controls);
        RestoreCheckpointUnlocks(controls);
    }

    private void RestoreCheckpointUnlocks(PlayerControls controls)
    {
        if (controls == null || CheckpointManager.Instance == null) return;
        InputManagerBueno inputMgr = controls.GetComponent<InputManagerBueno>() ?? controls.GetComponentInParent<InputManagerBueno>();
        if (inputMgr != null)
        {
            CheckpointUnlocks u = CheckpointManager.Instance.GetCheckpointUnlocks();
            inputMgr.canMoveVertical = u.canMoveVertical;
            inputMgr.canSprint = u.canSprint;
            inputMgr.canHeadbutt = u.canHeadbutt;
            inputMgr.canListen = u.canListen;
        }
    }

    // Busca en la escena un Volume que tenga Depth Of Field y toma ese override.
    // Si no hay ninguno, no hay borroso y todo lo demás funciona igual.
    private void FindDepthOfField()
    {
        depthOfField = null;

        foreach (Volume candidate in FindObjectsByType<Volume>(FindObjectsSortMode.None))
        {
            if (candidate.profile != null && candidate.profile.TryGet(out depthOfField))
                return;
        }

        depthOfField = null;
    }

    private void SetBlurred()
    {
        if (depthOfField == null) return;
        depthOfField.aperture.value = blurredAperture;
        depthOfField.focusDistance.value = blurredFocusDistance;
    }

    private IEnumerator ClearBlur()
    {
        if (depthOfField == null) yield break;

        float startAperture = depthOfField.aperture.value;
        float startFocus = depthOfField.focusDistance.value;
        float elapsed = 0f;

        while (elapsed < blurClearDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / blurClearDuration;
            depthOfField.aperture.value = Mathf.Lerp(startAperture, clearAperture, t);
            depthOfField.focusDistance.value = Mathf.Lerp(startFocus, normalFocusDistance, t);
            yield return null;
        }

        depthOfField.aperture.value = clearAperture;
        depthOfField.focusDistance.value = normalFocusDistance;
    }

    private void Teleport(Rigidbody namiRb, PlayerControls controls)
    {
        Vector3 position = namiRb.position;
        Quaternion rotation = namiRb.rotation;
        bool found = false;

        string sceneName = SceneManager.GetActiveScene().name;
        bool isActo2 = SaveSystem.IsSceneMatch(sceneName, "acto2");

        // En Acto 2, la muerte a manos del tiburón durante la persecución SIEMPRE debe
        // reaparecer a Nami en la posición inicial de la escena donde comenzó a jugar libremente,
        // sin mandarla detrás del terreno ni a triggers avanzados cerca del laboratorio.
        if (isActo2)
        {
            if (s_hasAct2InitialPosition)
            {
                position = s_act2InitialPosition;
                rotation = s_act2InitialRotation;
                found = true;
                Debug.Log($"[CheckpointRespawner] Reapareciendo en la posición inicial capturada de Acto 2: {position}");
            }
            else if (SaveSystem.HasSaveFile())
            {
                SaveData save = SaveSystem.Load();
                if (save != null && SaveSystem.IsSceneMatch(save.sceneName, "acto2") && save.savepointName == "Acto2_WakeUp")
                {
                    position = save.namiPosition;
                    rotation = Quaternion.Euler(save.namiEulerAngles);
                    found = true;
                    Debug.Log($"[CheckpointRespawner] Reapareciendo usando guardado Acto2_WakeUp: {position}");
                }
            }

            if (!found && fallbackSpawnPoint != null)
            {
                position = fallbackSpawnPoint.position;
                rotation = fallbackSpawnPoint.rotation;
                found = true;
                Debug.Log($"[CheckpointRespawner] Reapareciendo usando fallbackSpawnPoint en {position}.");
            }
        }
        else
        {
            bool hasCheckpoint = useCheckpointIfAvailable &&
                                 CheckpointManager.Instance != null &&
                                 CheckpointManager.Instance.HasCheckpointFor(sceneName);

            if (hasCheckpoint)
            {
                position = CheckpointManager.Instance.GetCheckpointPosition();
                found = true;
                Debug.Log($"CheckpointRespawner: reaparece en el checkpoint {position} (escena '{sceneName}').");

                if (mara != null)
                    MoveTo(mara, CheckpointManager.Instance.GetCheckpointMaraPosition());
            }
            else if (fallbackSpawnPoint != null)
            {
                position = fallbackSpawnPoint.position;
                rotation = fallbackSpawnPoint.rotation;
                found = true;
                Debug.Log($"CheckpointRespawner: usando fallbackSpawnPoint en {position}.");
            }
        }

        if (!found)
        {
            GameObject wakeUp = GameObject.Find("WakeUpPoint");
            if (wakeUp != null)
            {
                position = wakeUp.transform.position;
                rotation = wakeUp.transform.rotation;
                found = true;
                Debug.Log($"CheckpointRespawner: usando WakeUpPoint como respaldo en {position}.");
            }
        }

        if (!found)
            Debug.LogWarning("CheckpointRespawner: no hay checkpoint guardado para la escena " +
                              $"'{sceneName}' ni 'Fallback Spawn Point'. Nami se queda donde estaba. " +
                              $"(CheckpointManager existe: {CheckpointManager.Instance != null}. " +
                              "¿Salió el log 'Checkpoint guardado en...' al pasar por el trigger?)");

        // Se mueve el Rigidbody Y el Transform, y se sincroniza la física, para que
        // ningún script (ni la interpolación) la devuelva a la posición vieja.
        namiRb.position = position;
        namiRb.rotation = rotation;
        namiRb.transform.SetPositionAndRotation(position, rotation);
        Physics.SyncTransforms();
        namiRb.linearVelocity = Vector3.zero;
        namiRb.angularVelocity = Vector3.zero;

        // Evita un giro brusco hacia la orientación vieja al devolver el control.
        controls.SyncRotationToCurrentTransform();
    }

    private static void MoveTo(Transform target, Vector3 position)
    {
        Rigidbody rb = target.GetComponent<Rigidbody>();
        if (rb != null)
            rb.position = position;

        target.position = position;
    }

    // Muestra un diálogo y espera a que termine. Si el ID no existe, no se cuelga: espera 1 s y sigue.
    private IEnumerator ShowDialogueAndWait(string id)
    {
        if (string.IsNullOrEmpty(id) || ThoughtUI.Instance == null)
            yield break;

        ThoughtUI.Instance.ShowById(id);

        float waited = 0f;
        while (!ThoughtUI.Instance.IsShowing && waited < 1f)
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
        }

        while (ThoughtUI.Instance.IsShowing)
            yield return null;
    }
}

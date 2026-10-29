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

    // 'player' puede ser null: entonces se busca a Nami por el tag "Player".
    public Coroutine Respawn(GameObject player = null)
    {
        if (isRespawning) return null;
        return StartCoroutine(RespawnRoutine(player));
    }

    private IEnumerator RespawnRoutine(GameObject player)
    {
        if (player == null)
            player = GameObject.FindWithTag("Player");

        if (player == null)
        {
            Debug.LogWarning("CheckpointRespawner: no encontré a Nami (tag 'Player').");
            yield break;
        }

        Rigidbody namiRb = player.GetComponent<Rigidbody>();
        PlayerControls controls = player.GetComponent<PlayerControls>();
        PlayerAnimation playerAnimation = player.GetComponent<PlayerAnimation>();
        Animator namiAnimator = player.GetComponent<Animator>();

        if (namiRb == null || controls == null)
        {
            Debug.LogWarning("CheckpointRespawner: Nami necesita Rigidbody y PlayerControls.");
            yield break;
        }

        isRespawning = true;

        if (blackout == null) blackout = FindFirstObjectByType<ScreenBlackoutEffect>();
        FindDepthOfField();

        // 1) Nami pierde el control y se queda quieta.
        controls.SetControlEnabled(false);
        if (playerAnimation != null) playerAnimation.SetAnimationOverride(true);
        if (namiAnimator != null) namiAnimator.SetInteger("AnimationState", 0);
        namiRb.linearVelocity = Vector3.zero;
        namiRb.angularVelocity = Vector3.zero;

        // 2) A negro.
        if (blackout != null)
            yield return blackout.FadeToBlack();
        else
            Debug.LogWarning("CheckpointRespawner: no hay ScreenBlackoutEffect en la escena, no habrá fundido.");

        if (blackHoldSeconds > 0f)
            yield return new WaitForSeconds(blackHoldSeconds);

        // 3) (Opcional) diálogo con la pantalla en negro.
        yield return ShowDialogueAndWait(blackScreenDialogueId);

        // 4) Teletransporte, todavía en negro.
        Teleport(namiRb, controls);
        SetBlurred();

        // 5) Vuelve de negro (y el borroso se aclara, si hay).
        if (blackout != null)
            yield return blackout.FadeFromBlack();
        if (depthOfField != null)
            yield return ClearBlur();

        // 6) Nami recupera el control.
        if (playerAnimation != null) playerAnimation.SetAnimationOverride(false);
        controls.SetControlEnabled(true);

        isRespawning = false;
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
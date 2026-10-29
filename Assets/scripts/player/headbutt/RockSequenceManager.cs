using System.Collections;
using UnityEngine;

// El "director" de la secuencia de rocas cayendo: cuenta cuántas rompió el
// JUGADOR (a cabezazos), reacciona cuando una roca golpea a Nami sin que
// alcanzara a romperla, y dispara la cinemática de la roca grande al llegar
// al objetivo. Ponlo en un GameObject vacío de la escena (ej. "RockDirector").
public class RockSequenceManager : MonoBehaviour
{
    [Header("Objetivo")]
    [SerializeField] private int requiredBreaks = 4;

    [Header("Referencias")]
    [SerializeField] private RockFallSpawner spawner;
    [SerializeField] private PlayerControls playerControls;
    [SerializeField] private BigRockCinematic bigRockCinematic;

    [Header("Cuando una roca golpea a Nami (sin romperla a tiempo)")]
    [Tooltip("Multiplicador de velocidad mientras dura la ralentización (0.5 = mitad de velocidad).")]
    [SerializeField] private float hitSlowMultiplier = 0.5f;
    [SerializeField] private float hitSlowDuration = 1.5f;
    [Tooltip("ID del diálogo de Mara ('¡cuidado!') en el DialogueDatabase que usa ThoughtUI.")]
    [SerializeField] private string warningDialogueId;

    [Header("Shake al recibir un golpe")]
    [SerializeField] private float hitShakeDuration = 0.35f;
    [SerializeField] private float hitShakeMagnitude = 0.6f;

    [Header("Fallar: demasiados golpes")]
    [Tooltip("Si Nami recibe esta cantidad de golpes (rocas que NO alcanzó a romper), vuelve " +
             "al último checkpoint. 0 = nunca falla por golpes.")]
    [SerializeField] private int maxHitsBeforeRespawn = 4;
    [Tooltip("Quien devuelve a Nami al checkpoint (fundido a negro + teletransporte).")]
    [SerializeField] private CheckpointRespawner respawner;

    [Header("Misión")]
    [Tooltip("ID de la misión de romper rocas en MissionDatabase.")]
    [SerializeField] private string missionId;

    private int brokenByPlayerCount;
    private int hitCount;
    private bool sequenceFinished;
    private bool isRespawning;
    private bool hasWarnedPlayer; // true después de la PRIMERA vez que Mara avisa "cuidado"

    public event System.Action<int, int> BreakCountChanged; // (actual, requerido)
    public int BrokenCount => brokenByPlayerCount;
    public int RequiredBreaks => requiredBreaks;

    private void Start()
    {
        UpdateMissionCounter();
    }

    private void UpdateMissionCounter()
    {
        if (string.IsNullOrEmpty(missionId))
        {
            Debug.LogWarning("RockSequenceManager: missionId está vacío en el Inspector.");
            return;
        }

        if (MissionUI.Instance == null)
        {
            Debug.LogWarning("RockSequenceManager: no hay MissionUI.Instance en la escena.");
            return;
        }

        Debug.Log($"Contador de misión → '{missionId}' {brokenByPlayerCount}/{requiredBreaks}");
        MissionUI.Instance.SetProgress(missionId, brokenByPlayerCount, requiredBreaks);
    }

    // Llamado desde FallingRock (vía DestructibleRock.BrokenByPlayer) cada vez
    // que el jugador rompe una roca a cabezazos.
    public void RegisterPlayerBreak()
    {
        if (sequenceFinished || isRespawning) return;

        brokenByPlayerCount++;
        Debug.Log($"Rocas rotas por el jugador: {brokenByPlayerCount}/{requiredBreaks}");

        BreakCountChanged?.Invoke(brokenByPlayerCount, requiredBreaks); // ANTES de TriggerBigRock
        UpdateMissionCounter();

        if (brokenByPlayerCount >= requiredBreaks)
            TriggerBigRock();
    }

    // Llamado desde FallingRock cuando una roca dirigida a Nami la alcanza sin romperse.
    public void OnRockHitPlayer()
    {
        Debug.Log("RockSequenceManager.OnRockHitPlayer llamado");
        if (sequenceFinished || isRespawning) return;

        CameraShake.Shake(hitShakeDuration, hitShakeMagnitude);

        if (playerControls != null)
            playerControls.ApplySlow(hitSlowMultiplier, hitSlowDuration);

        // La advertencia de Mara solo se muestra UNA vez en total (la primera roca
        // que te golpea), no cada vez — ya entendiste el peligro, no hace falta repetirlo.
        if (!hasWarnedPlayer && ThoughtUI.Instance != null && !string.IsNullOrEmpty(warningDialogueId))
        {
            ThoughtUI.Instance.ShowById(warningDialogueId);
            hasWarnedPlayer = true;
        }

        // Demasiados golpes sin romper: vuelve al último checkpoint.
        hitCount++;
        Debug.Log($"Golpes recibidos: {hitCount}/{maxHitsBeforeRespawn}");

        if (maxHitsBeforeRespawn > 0 && hitCount >= maxHitsBeforeRespawn)
        {
            if (respawner != null)
                StartCoroutine(FailAndRespawn());
            else
                Debug.LogWarning("RockSequenceManager: llegó al máximo de golpes pero falta asignar " +
                                  "'Respawner' en el Inspector.");
        }
    }

    private IEnumerator FailAndRespawn()
    {
        isRespawning = true;

        // Paramos las rocas y limpiamos las que sigan en el aire mientras dura el fundido.
        if (spawner != null)
            spawner.SetActive(false);

        foreach (FallingRock rock in FindObjectsByType<FallingRock>(FindObjectsSortMode.None))
            Destroy(rock.gameObject);

        GameObject player = playerControls != null ? playerControls.gameObject : null;
        Coroutine respawn = respawner.Respawn(player);
        if (respawn != null)
            yield return respawn;

        // Reintento limpio: se reinician los contadores (golpes Y rocas rotas) y vuelven a caer rocas.
        hitCount = 0;
        brokenByPlayerCount = 0;
        BreakCountChanged?.Invoke(brokenByPlayerCount, requiredBreaks);
        UpdateMissionCounter();

        if (spawner != null)
            spawner.SetActive(true);

        isRespawning = false;
    }

    private void TriggerBigRock()
    {
        sequenceFinished = true;
        Debug.Log("🪨 Objetivo cumplido: disparando la cinemática de la roca grande.");

        if (spawner != null)
            spawner.SetActive(false);

        if (bigRockCinematic != null)
            bigRockCinematic.Play();
        else
            Debug.LogWarning("RockSequenceManager: falta asignar 'Big Rock Cinematic' en el Inspector, " +
                              "así que se cumplió el objetivo pero no va a pasar nada visualmente.");
    }
}
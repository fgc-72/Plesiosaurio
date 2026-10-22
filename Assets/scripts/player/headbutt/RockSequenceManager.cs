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

    [Header("Misión")]
    [Tooltip("ID de la misión de romper rocas en MissionDatabase.")]
    [SerializeField] private string missionId;

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

    private int brokenByPlayerCount;
    private bool sequenceFinished;
    private bool hasWarnedPlayer; // true después de la PRIMERA vez que Mara avisa "cuidado"

    public event System.Action<int, int> BreakCountChanged; // (actual, requerido)
    public int BrokenCount => brokenByPlayerCount;
    public int RequiredBreaks => requiredBreaks;

    // Llamado desde FallingRock (vía DestructibleRock.BrokenByPlayer) cada vez
    // que el jugador rompe una roca a cabezazos.
    public void RegisterPlayerBreak()
    {
        if (sequenceFinished) return;

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
        if (sequenceFinished) return;

        if (playerControls != null)
            playerControls.ApplySlow(hitSlowMultiplier, hitSlowDuration);

        // La advertencia de Mara solo se muestra UNA vez en total (la primera roca
        // que te golpea), no cada vez — ya entendiste el peligro, no hace falta repetirlo.
        if (!hasWarnedPlayer && ThoughtUI.Instance != null && !string.IsNullOrEmpty(warningDialogueId))
        {
            ThoughtUI.Instance.ShowById(warningDialogueId);
            hasWarnedPlayer = true;
        }
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
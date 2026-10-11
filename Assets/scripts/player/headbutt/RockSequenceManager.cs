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

    private int brokenByPlayerCount;
    private bool sequenceFinished;

    // Llamado desde FallingRock (vía DestructibleRock.BrokenByPlayer) cada vez
    // que el jugador rompe una roca a cabezazos.
    public void RegisterPlayerBreak()
    {
        if (sequenceFinished) return;

        brokenByPlayerCount++;
        Debug.Log($"Rocas rotas por el jugador: {brokenByPlayerCount}/{requiredBreaks}");

        if (brokenByPlayerCount >= requiredBreaks)
            TriggerBigRock();
    }

    // Llamado desde FallingRock cuando una roca dirigida a Nami la alcanza sin romperse.
    public void OnRockHitPlayer()
    {
        if (sequenceFinished) return;

        if (playerControls != null)
            playerControls.ApplySlow(hitSlowMultiplier, hitSlowDuration);

        if (ThoughtUI.Instance != null && !string.IsNullOrEmpty(warningDialogueId))
            ThoughtUI.Instance.ShowById(warningDialogueId);
    }

    private void TriggerBigRock()
    {
        sequenceFinished = true;

        if (spawner != null)
            spawner.SetActive(false);

        if (bigRockCinematic != null)
            bigRockCinematic.Play();
    }
}

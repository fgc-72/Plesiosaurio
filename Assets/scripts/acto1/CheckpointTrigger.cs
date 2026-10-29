using UnityEngine;

// Coloca uno de estos en cualquier punto del nivel donde quieras que se
// guarde el progreso. Cada vez que el jugador lo toca, sobreescribe el
// checkpoint anterior — siempre se recuerda el más reciente, no una lista.
[RequireComponent(typeof(Collider))]
public class CheckpointTrigger : MonoBehaviour
{
    [SerializeField] private string playerTag = "Player";
    [Tooltip("Nombre EXACTO de esta escena (debe coincidir con el que usa OpeningCinematicAct1).")]
    [SerializeField] private string sceneName = "Acto1";
    [Tooltip("Referencia al Transform de Mara, para guardar también su posición al pasar por este checkpoint.")]
    [SerializeField] private Transform maraTransform;

    [Tooltip("Si es true, solo guarda la primera vez que se toca (ej. para evitar " +
             "guardar de nuevo si el jugador pasa por aquí varias veces). Normalmente " +
             "puedes dejarlo en false sin problema: sobreescribir con la misma zona no hace daño.")]
    [SerializeField] private bool saveOnlyOnce = false;

    private bool hasSaved;

    private void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (saveOnlyOnce && hasSaved)
            return;

        if (!other.CompareTag(playerTag))
            return;

        if (CheckpointManager.Instance == null)
            return;

        InputManagerBueno inputManager = other.GetComponent<InputManagerBueno>();
        CheckpointUnlocks unlocks = inputManager != null
            ? new CheckpointUnlocks
            {
                canMoveVertical = inputManager.canMoveVertical,
                canSprint = inputManager.canSprint,
                canHeadbutt = inputManager.canHeadbutt,
                canListen = inputManager.canListen
            }
            : default;

        // Si no asignaste a Mara en el Inspector, guardamos su propia posición
        // actual como respaldo (mejor que romper el checkpoint por completo).
        Vector3 maraPosition = maraTransform != null ? maraTransform.position : other.transform.position;

        CheckpointManager.Instance.SaveCheckpoint(sceneName, other.transform.position, maraPosition, unlocks);
        hasSaved = true;
    }
}
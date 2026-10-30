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

        // Comprueba si el objeto o su raíz tienen el tag de Player
        if (!other.CompareTag(playerTag) && !other.transform.root.CompareTag(playerTag))
            return;

        // 1. Guardar a través del SaveSystem JSON
        SaveData data = SaveSystem.CreateSaveDataFromCurrentScene(gameObject.name);
        if (data != null)
        {
            if (maraTransform != null)
            {
                data.hasMara = true;
                data.maraPosition = maraTransform.position;
                data.maraEulerAngles = maraTransform.eulerAngles;
            }
            SaveSystem.Save(data);
        }

        // 2. Compatibilidad con CheckpointManager
        if (CheckpointManager.Instance != null)
        {
            InputManagerBueno inputManager = other.GetComponent<InputManagerBueno>() ?? other.GetComponentInParent<InputManagerBueno>();
            CheckpointUnlocks unlocks = inputManager != null
                ? new CheckpointUnlocks
                {
                    canMoveVertical = inputManager.canMoveVertical,
                    canSprint = inputManager.canSprint,
                    canHeadbutt = inputManager.canHeadbutt,
                    canListen = inputManager.canListen
                }
                : default;

            Vector3 maraPosition = maraTransform != null ? maraTransform.position : other.transform.position;
            CheckpointManager.Instance.SaveCheckpoint(sceneName, other.transform.position, maraPosition, unlocks);
        }

        hasSaved = true;
    }
}

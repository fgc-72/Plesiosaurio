using UnityEngine;
using UnityEngine.SceneManagement;

// Coloca uno de estos en cualquier punto del nivel donde quieras que se
// guarde el progreso. Cada vez que el jugador lo toca, sobreescribe el
// checkpoint anterior — siempre se recuerda el más reciente, no una lista.
[RequireComponent(typeof(Collider))]
public class CheckpointTrigger : MonoBehaviour
{
    [SerializeField] private string playerTag = "Player";

    [Tooltip("Si es true (recomendado), guarda el checkpoint con el nombre REAL de la escena " +
             "activa, así nunca hay un desajuste de nombres. Si es false usa 'Scene Name'.")]
    [SerializeField] private bool useActiveSceneName = true;
    [Tooltip("Solo se usa si 'Use Active Scene Name' está desactivado.")]
    [SerializeField] private string sceneName = "Acto1";
    [Tooltip("Referencia al Transform de Mara, para guardar también su posición al pasar por este checkpoint.")]
    [SerializeField] private Transform maraTransform;

    [Tooltip("Si es true, solo guarda la primera vez que se toca.")]
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

        // Nami puede tener el collider en un hijo: miramos también el objeto del Rigidbody.
        GameObject player = other.attachedRigidbody != null ? other.attachedRigidbody.gameObject : other.gameObject;
        if (!player.CompareTag(playerTag) && !other.CompareTag(playerTag))
            return;

        // Si no hay CheckpointManager en la escena, lo creamos (antes fallaba en silencio).
        if (CheckpointManager.Instance == null)
        {
            Debug.LogWarning("CheckpointTrigger: no había CheckpointManager en la escena, creo uno automáticamente. " +
                             "Mejor ponlo tú en un GameObject vacío.");
            new GameObject("CheckpointManager").AddComponent<CheckpointManager>();
        }

        InputManagerBueno inputManager = player.GetComponent<InputManagerBueno>();
        CheckpointUnlocks unlocks = inputManager != null
            ? new CheckpointUnlocks
            {
                canMoveVertical = inputManager.canMoveVertical,
                canSprint = inputManager.canSprint,
                canHeadbutt = inputManager.canHeadbutt,
                canListen = inputManager.canListen
            }
            : default;

        Vector3 position = player.transform.position;
        Vector3 maraPosition = maraTransform != null ? maraTransform.position : position;
        string savedScene = useActiveSceneName ? SceneManager.GetActiveScene().name : sceneName;

        CheckpointManager.Instance.SaveCheckpoint(savedScene, position, maraPosition, unlocks);
        hasSaved = true;
    }
}
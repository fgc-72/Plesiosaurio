using UnityEngine;

// Ponlo en el MISMO objeto del trigger que ya activa el diálogo (o en uno nuevo con Collider).
// Cuando Nami entra, muestra la misión con el contador ("Busca pistas (0/4)").
// Es independiente del otro script del trigger: los dos corren a la vez.
[RequireComponent(typeof(Collider))]
public class ClueCounterTrigger : MonoBehaviour
{
    [SerializeField] private string playerTag = "Player";
    [Tooltip("Si lo dejas vacío, se busca el ClueCollectionManager de la escena.")]
    [SerializeField] private ClueCollectionManager manager;
    [Tooltip("Segundos de espera antes de mostrar la misión (útil para que salga después del diálogo).")]
    [SerializeField] private float delaySeconds = 0f;

    private bool used;

    private void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (used || !other.CompareTag(playerTag))
            return;

        if (manager == null)
            manager = FindFirstObjectByType<ClueCollectionManager>();

        if (manager == null)
        {
            Debug.LogWarning("ClueCounterTrigger: no hay ClueCollectionManager en la escena.");
            return;
        }

        used = true;
        Invoke(nameof(Show), delaySeconds);
    }

    private void Show()
    {
        manager.ShowCounter();
    }
}

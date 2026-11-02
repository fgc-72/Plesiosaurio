using System.Collections;
using UnityEngine;

// Va en el tiburón, junto a un Collider marcado como Trigger (el "cuerpo" con el que
// golpea). Si toca a Nami: el tiburón se detiene, Nami vuelve al último checkpoint
// (CheckpointRespawner, con su fundido a negro) y el tiburón regresa a su punto de inicio.
[RequireComponent(typeof(Collider))]
public class SharkHitDetector : MonoBehaviour
{
    [SerializeField] private SharkChaser shark;
    [SerializeField] private CheckpointRespawner respawner;
    [SerializeField] private string playerTag = "Player";

    [Header("Después de reiniciar a Nami")]
    [Tooltip("El evento que activa al tiburón. Se vuelve a armar para que el reintento lo muestre otra vez.")]
    [SerializeField] private SharkChaseEvent chaseEvent;
    [Tooltip("Si es true, el tiburón se vuelve a ocultar al reiniciar (y reaparece con el evento).")]
    [SerializeField] private bool hideAfterReset = true;

    private bool isResetting;

    private void Awake()
    {
        GetComponent<Collider>().isTrigger = true;

        if (shark == null)
            shark = GetComponent<SharkChaser>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isResetting || !other.CompareTag(playerTag))
            return;

        if (respawner == null)
        {
            Debug.LogWarning("SharkHitDetector: falta asignar 'Respawner' (CheckpointRespawner).");
            return;
        }

        StartCoroutine(HitRoutine(other.gameObject));
    }

    private IEnumerator HitRoutine(GameObject player)
    {
        isResetting = true;

        if (shark != null)
            shark.StopChase();

        Coroutine respawn = respawner.Respawn(player);
        if (respawn != null)
            yield return respawn;

        if (shark != null)
            shark.ResetToStart();

        isResetting = false;

        if (chaseEvent != null)
            chaseEvent.ResetEvent();

        // Último: desactivar este objeto detiene esta corrutina.
        if (hideAfterReset)
            gameObject.SetActive(false);
    }
}
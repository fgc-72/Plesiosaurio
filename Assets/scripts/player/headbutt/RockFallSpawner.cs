using UnityEngine;
using System.Collections;

// Hace caer rocas desde la superficie en intervalos aleatorios, apuntando
// (con offset aleatorio) a Nami o a Mara. RockSequenceManager lo desactiva
// apenas el jugador cumple el objetivo de rocas rotas, para dejar el
// escenario limpio justo antes de la cinemática de la roca grande.
public class RockFallSpawner : MonoBehaviour
{
    [Header("Prefabs (distintos tamaños)")]
    [Tooltip("Cada prefab debe tener FallingRock + DestructibleRock ya configurados " +
             "(por ejemplo, hitsToBreak más alto en las rocas grandes).")]
    [SerializeField] private GameObject[] rockPrefabs;

    [Header("Objetivos")]
    [SerializeField] private Transform namiTarget;
    [SerializeField] private Transform maraTarget;
    [Tooltip("Probabilidad (0-1) de que la próxima roca vaya hacia Mara en vez de hacia Nami.")]
    [Range(0f, 1f)]
    [SerializeField] private float maraTargetChance = 0.35f;

    [Header("Ritmo de aparición")]
    [SerializeField] private float minInterval = 2f;
    [SerializeField] private float maxInterval = 5f;

    [Header("Posición de aparición")]
    [Tooltip("A qué altura POR ENCIMA del objetivo aparece la roca (simula que cae desde la superficie).")]
    [SerializeField] private float spawnHeight = 25f;
    [Tooltip("Qué tan lejos del objetivo, en el plano horizontal, puede aparecer la roca.")]
    [SerializeField] private float horizontalSpawnRadius = 4f;

    [SerializeField] private RockSequenceManager sequenceManager;

    // Arranca APAGADO: no cae ninguna roca hasta que algo externo (normalmente
    // RockZoneTrigger, al entrar el jugador a la zona) llame a BeginSpawning().
    private bool isActive;
    private bool hasStarted;

    // Llamado por RockZoneTrigger cuando el jugador entra a la zona de rocas.
    public void BeginSpawning()
    {
        isActive = true;

        // El coroutine de spawn solo se arranca UNA vez. Si luego se llama a
        // SetActive(false) y SetActive(true) de nuevo (no debería pasar en este
        // flujo, pero por seguridad), no queremos dos loops corriendo a la vez.
        if (!hasStarted)
        {
            hasStarted = true;
            StartCoroutine(SpawnLoop());
        }
    }

    // Llamado por RockSequenceManager al llegar al objetivo de rocas rotas,
    // para parar de generar más mientras pasa la cinemática de la roca grande.
    public void SetActive(bool active)
    {
        isActive = active;
    }

    private IEnumerator SpawnLoop()
    {
        while (true)
        {
            float wait = Random.Range(minInterval, maxInterval);
            yield return new WaitForSeconds(wait);

            if (isActive)
                SpawnRock();
        }
    }

    private void SpawnRock()
    {
        if (rockPrefabs == null || rockPrefabs.Length == 0)
            return;

        bool goesToMara = maraTarget != null && Random.value < maraTargetChance;
        Transform target = goesToMara ? maraTarget : namiTarget;

        if (target == null)
            return;

        Vector2 randomOffset = Random.insideUnitCircle * horizontalSpawnRadius;
        Vector3 spawnPosition = target.position + new Vector3(randomOffset.x, spawnHeight, randomOffset.y);

        GameObject prefab = rockPrefabs[Random.Range(0, rockPrefabs.Length)];
        GameObject rockInstance = Instantiate(prefab, spawnPosition, Random.rotation);

        FallingRock fallingRock = rockInstance.GetComponent<FallingRock>();
        if (fallingRock != null)
        {
            FallingRock.RockTarget targetType = goesToMara ? FallingRock.RockTarget.Mara : FallingRock.RockTarget.Nami;
            fallingRock.Setup(targetType, target, sequenceManager);
        }
    }
}
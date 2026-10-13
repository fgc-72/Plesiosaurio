using UnityEngine;
using System.Collections;

// Hace caer rocas desde la superficie. Dos tipos, con su propio ritmo:
// - PELIGROSAS: apuntan (con offset) a Nami o a Mara, cuentan para el objetivo
//   de "romper 4" y pueden golpear al jugador si no las rompe a tiempo.
// - AMBIENTALES: puramente decorativas, caen en zonas marcadas por Transforms
//   sin perseguir a nadie, solo para que se sienta que el lugar es peligroso.
// RockSequenceManager desactiva el lado peligroso apenas el jugador cumple el
// objetivo (SetActive), para dejar limpio el escenario antes de la roca grande;
// el lado ambiental puede seguir o no, según cómo lo uses.
public class RockFallSpawner : MonoBehaviour
{
    [Header("Rocas peligrosas — prefabs (distintos tamaños)")]
    [Tooltip("Cada prefab debe tener FallingRock + DestructibleRock ya configurados " +
             "(por ejemplo, hitsToBreak más alto en las rocas grandes).")]
    [SerializeField] private GameObject[] rockPrefabs;

    [Header("Rocas peligrosas — objetivos")]
    [SerializeField] private Transform namiTarget;
    [SerializeField] private Transform maraTarget;
    [Tooltip("Probabilidad (0-1) de que la próxima roca peligrosa vaya hacia Mara en vez de Nami.")]
    [Range(0f, 1f)]
    [SerializeField] private float maraTargetChance = 0.35f;

    [Header("Rocas peligrosas — ritmo de aparición")]
    [Tooltip("Más bajo = caen más seguido. Si lo bajas mucho, sube minInterval un poco " +
             "para no saturar al jugador de golpes imposibles de esquivar.")]
    [SerializeField] private float minInterval = 1f;
    [SerializeField] private float maxInterval = 2.5f;

    [Header("Rocas peligrosas — posición de aparición")]
    [Tooltip("A qué altura POR ENCIMA del objetivo aparece la roca (simula que cae desde la superficie).")]
    [SerializeField] private float spawnHeight = 25f;
    [Tooltip("Qué tan lejos del objetivo, en el plano horizontal, puede aparecer la roca. " +
             "Súbelo para que la 'zona de peligro' se sienta más amplia.")]
    [SerializeField] private float horizontalSpawnRadius = 6f;

    [Header("Rocas ambientales — prefabs")]
    [Tooltip("Pueden ser los mismos prefabs de arriba, o unos más chicos/decorativos. " +
             "Igual necesitan FallingRock + DestructibleRock (aunque nunca las rompan).")]
    [SerializeField] private GameObject[] ambientRockPrefabs;

    [Header("Rocas ambientales — zonas")]
    [Tooltip("Coloca GameObjects vacíos en el nivel marcando por dónde quieres que caigan " +
             "rocas de fondo (puede ser detrás, a los lados, lejos del camino del jugador). " +
             "Cada vez que toca generar una, se elige una zona al azar de esta lista.")]
    [SerializeField] private Transform[] ambientSpawnZones;
    [SerializeField] private float ambientSpawnRadius = 6f;
    [SerializeField] private float ambientSpawnHeight = 20f;

    [Header("Rocas ambientales — ritmo de aparición")]
    [Tooltip("Mucho más frecuente que las peligrosas: son solo decoración, no hay que " +
             "cuidar que no abrumen al jugador en términos de dificultad.")]
    [SerializeField] private float ambientMinInterval = 0.4f;
    [SerializeField] private float ambientMaxInterval = 1.2f;

    [SerializeField] private RockSequenceManager sequenceManager;

    // Arranca APAGADO: no cae ninguna roca hasta que algo externo (normalmente
    // OpeningCinematicAct1/ScriptedEvent) llame a BeginSpawning().
    private bool isActive;
    private bool hasStarted;

    // Llamado cuando el jugador recupera el control justo donde debe empezar la secuencia.
    public void BeginSpawning()
    {
        isActive = true;

        // Los coroutines de spawn solo se arrancan UNA vez, para no duplicar loops
        // si BeginSpawning se llama más de una vez por error.
        if (!hasStarted)
        {
            hasStarted = true;

            // La primera roca de cada tipo cae YA, sin esperar el intervalo random —
            // así el peligro se nota de inmediato apenas termina el diálogo, en vez
            // de sentirse como que "no pasa nada" durante los primeros segundos.
            SpawnDangerRock();
            SpawnAmbientRock();

            StartCoroutine(DangerSpawnLoop());
            StartCoroutine(AmbientSpawnLoop());
        }
    }

    // Llamado por RockSequenceManager al llegar al objetivo de rocas rotas,
    // para parar de generar más mientras pasa la cinemática de la roca grande.
    // Nota: esto apaga AMBOS tipos (peligrosas y ambientales). Si más adelante
    // quieres que las ambientales sigan cayendo incluso durante la cinemática,
    // avísame y separamos el apagado en dos banderas.
    public void SetActive(bool active)
    {
        isActive = active;
    }

    private IEnumerator DangerSpawnLoop()
    {
        while (true)
        {
            float wait = Random.Range(minInterval, maxInterval);
            yield return new WaitForSeconds(wait);

            if (isActive)
                SpawnDangerRock();
        }
    }

    private IEnumerator AmbientSpawnLoop()
    {
        while (true)
        {
            float wait = Random.Range(ambientMinInterval, ambientMaxInterval);
            yield return new WaitForSeconds(wait);

            if (isActive)
                SpawnAmbientRock();
        }
    }

    private void SpawnDangerRock()
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

    private void SpawnAmbientRock()
    {
        if (ambientRockPrefabs == null || ambientRockPrefabs.Length == 0)
            return;

        if (ambientSpawnZones == null || ambientSpawnZones.Length == 0)
            return;

        Transform zone = ambientSpawnZones[Random.Range(0, ambientSpawnZones.Length)];
        if (zone == null)
            return;

        Vector2 randomOffset = Random.insideUnitCircle * ambientSpawnRadius;
        Vector3 spawnPosition = zone.position + new Vector3(randomOffset.x, ambientSpawnHeight, randomOffset.y);

        GameObject prefab = ambientRockPrefabs[Random.Range(0, ambientRockPrefabs.Length)];
        GameObject rockInstance = Instantiate(prefab, spawnPosition, Random.rotation);

        FallingRock fallingRock = rockInstance.GetComponent<FallingRock>();
        if (fallingRock != null)
            fallingRock.Setup(FallingRock.RockTarget.Ambient, targetTransform: null, manager: null);
    }

    // Dibuja las zonas ambientales en el Scene View para acomodarlas a ojo.
    private void OnDrawGizmosSelected()
    {
        if (ambientSpawnZones == null) return;

        Gizmos.color = new Color(0.4f, 0.7f, 1f, 0.35f);
        foreach (Transform zone in ambientSpawnZones)
        {
            if (zone == null) continue;
            Gizmos.DrawWireSphere(zone.position, ambientSpawnRadius);
        }
    }
}
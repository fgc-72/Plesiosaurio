using System.Collections;
using UnityEngine;

// Evento del tiburón: Nami pasa por un trigger, sale el texto "oigo algo...", y el
// jugador tiene que activar el MODO ESCUCHA (mantenerlo un momento) para descubrir qué
// es. Entonces el tiburón se acerca al punto que definas y, al llegar, se suelta a
// seguir a Nami por rango, como siempre.
//
// No toca DetectiveController: solo lee 'detectiveInput' de InputManagerBueno, que ya
// es true mientras el jugador mantiene el modo escucha.
[RequireComponent(typeof(Collider))]
public class SharkListenEvent : MonoBehaviour
{
    [Header("Activación")]
    [SerializeField] private string playerTag = "Player";

    [Header("Tiburón")]
    [SerializeField] private SharkCompanion shark;
    [Tooltip("Punto (un Empty en la escena) al que el tiburón se acerca. Ponlo A MÁS de " +
             "'minDistance' (6 m por defecto) de donde estará Nami, o el espacio personal " +
             "del tiburón lo empujará hacia afuera y no llegará.")]
    [SerializeField] private Transform approachPoint;
    [Tooltip("Si 'Approach Point' está vacío, se busca por código un GameObject con este nombre " +
             "EXACTO en la escena (debe estar activo). Útil cuando no se puede arrastrar en el Inspector.")]
    [SerializeField] private string approachPointName = "SharkApproachPoint";

    [Header("Diálogos (IDs del DialogueDatabase)")]
    [Tooltip("'¿Oigo algo?...' — sale al pasar por el trigger.")]
    [SerializeField] private string hearDialogueId;
    [Tooltip("Opcional: sale cuando el jugador descubre el tiburón con el modo escucha.")]
    [SerializeField] private string discoveredDialogueId;

    [Header("Misiones (IDs del MissionDatabase)")]
    [Tooltip("Opcional: misión que sale junto con el diálogo de 'oigo algo'.")]
    [SerializeField] private string hearMissionId;
    [Tooltip("Opcional: misión que sale cuando el jugador descubre el tiburón.")]
    [SerializeField] private string discoveredMissionId;

    [Header("Modo escucha")]
    [Tooltip("Segundos que el jugador debe mantener el modo escucha para 'descubrir' al tiburón.")]
    [SerializeField] private float listenHoldSeconds = 1.5f;
    [Tooltip("Si es true, desbloquea el modo escucha (canListen) al empezar el evento, por " +
             "si el jugador todavía no lo tenía disponible.")]
    [SerializeField] private bool unlockListeningMode = true;

    private bool hasTriggered;

    private void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
        MusicManager.Instance.Play("Nami_Theme");
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasTriggered || !other.CompareTag(playerTag))
            return;

        InputManagerBueno input = other.GetComponent<InputManagerBueno>();
        if (input == null)
        {
            Debug.LogWarning("SharkListenEvent: el jugador no tiene InputManagerBueno, el evento no puede arrancar.");
            return;
        }

        hasTriggered = true;
        ResolveReferences();
        StartCoroutine(RunEvent(input));
    }

    // Busca por código lo que no se pudo arrastrar en el Inspector. Se hace al
    // activarse el evento (no antes), así ya cargó todo lo de la escena.
    private void ResolveReferences()
    {
        if (shark == null)
            shark = FindFirstObjectByType<SharkCompanion>();

        if (approachPoint == null && !string.IsNullOrEmpty(approachPointName))
        {
            GameObject found = GameObject.Find(approachPointName);
            if (found != null)
                approachPoint = found.transform;
        }
    }

    private void ShowMission(string missionId)
    {
        if (string.IsNullOrEmpty(missionId))
            return;

        if (MissionUI.Instance != null)
            MissionUI.Instance.ShowMission(missionId);
        else
            Debug.LogWarning($"SharkListenEvent: misión '{missionId}' pero MissionUI.Instance es null " +
                              "(¿hay un MissionUI en esta escena?).");
    }

    private IEnumerator RunEvent(InputManagerBueno input)
    {
        if (unlockListeningMode)
            input.canListen = true;

        // 1) Texto "oigo algo" — NO bloquea: el jugador sigue moviéndose.
        if (!string.IsNullOrEmpty(hearDialogueId) && ThoughtUI.Instance != null)
            ThoughtUI.Instance.ShowById(hearDialogueId);

        ShowMission(hearMissionId);

        // 2) Esperamos a que mantenga el modo escucha el tiempo pedido.
        float held = 0f;
        while (held < listenHoldSeconds)
        {
            held = input.detectiveInput ? held + Time.deltaTime : 0f;
            yield return null;
        }

        // 3) Lo descubrió.
        if (!string.IsNullOrEmpty(discoveredDialogueId) && ThoughtUI.Instance != null)
            ThoughtUI.Instance.ShowById(discoveredDialogueId);

        ShowMission(discoveredMissionId);

        // 4) El tiburón se acerca al punto; al llegar vuelve solo al seguimiento por rango.
        if (shark == null || approachPoint == null)
        {
            Debug.LogWarning("SharkListenEvent: no encontré el tiburón o el punto de aproximación. " +
                              $"Revisa que exista un SharkCompanion y un GameObject activo llamado '{approachPointName}' " +
                              "(el nombre debe ser exacto, con mayúsculas).");
            yield break;
        }

        shark.ApproachPoint(approachPoint);
    }
}
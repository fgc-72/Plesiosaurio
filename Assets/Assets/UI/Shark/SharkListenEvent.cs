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

    [Header("Diálogos (IDs del DialogueDatabase)")]
    [Tooltip("'¿Oigo algo?...' — sale al pasar por el trigger.")]
    [SerializeField] private string hearDialogueId;
    [Tooltip("Opcional: sale cuando el jugador descubre el tiburón con el modo escucha.")]
    [SerializeField] private string discoveredDialogueId;

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
        StartCoroutine(RunEvent(input));
    }

    private IEnumerator RunEvent(InputManagerBueno input)
    {
        if (unlockListeningMode)
            input.canListen = true;

        // 1) Texto "oigo algo" — NO bloquea: el jugador sigue moviéndose.
        if (!string.IsNullOrEmpty(hearDialogueId) && ThoughtUI.Instance != null)
            ThoughtUI.Instance.ShowById(hearDialogueId);

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

        // 4) El tiburón se acerca al punto; al llegar vuelve solo al seguimiento por rango.
        if (shark == null || approachPoint == null)
        {
            Debug.LogWarning("SharkListenEvent: falta asignar 'Shark' o 'Approach Point' en el Inspector.");
            yield break;
        }

        shark.ApproachPoint(approachPoint);
    }
}
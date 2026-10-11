using UnityEngine;
using UnityEngine.Events;

// Poner este componente en cada roca destructible del mapa (o en cada prefab
// de roca que cae). hitsToBreak se ajusta INDIVIDUALMENTE por roca en el
// Inspector, así puedes tener rocas de 1 golpe y otras más resistentes sin
// escribir código extra.
public class DestructibleRock : MonoBehaviour
{
    [Tooltip("Cuántos cabezazos aguanta esta roca antes de romperse.")]
    [SerializeField] private int hitsToBreak = 1;

    [Tooltip("Se dispara SIEMPRE que la roca se rompe (por cabezazo o de forma forzada). " +
             "Engancha aquí partículas, sonido, o una animación de rotura desde el Inspector.")]
    [SerializeField] private UnityEvent onBroken;

    [Tooltip("Se dispara SOLO cuando el jugador la rompió a cabezazos (no cuando se " +
             "rompe sola con ForceBreak). Útil para VFX/sonido específicos de logro.")]
    [SerializeField] private UnityEvent onBrokenByPlayer;

    // Evento de código (no de Inspector): lo usa FallingRock para avisarle a
    // RockSequenceManager que ESTA roca en concreto la rompió el jugador,
    // sin tener que cablear nada a mano por cada roca que se instancia en runtime.
    public event System.Action BrokenByPlayer;

    private int currentHits;
    private bool isBroken; // evita romper dos veces si TakeHit y ForceBreak llegan el mismo frame

    public bool IsBroken => isBroken;

    public void TakeHit()
    {
        if (isBroken) return;

        currentHits++;
        Debug.Log($"🪨 Roca golpeada: {currentHits}/{hitsToBreak}"); // TEMPORAL: borrar cuando tengas la animación

        if (currentHits >= hitsToBreak)
            Break(wasPlayerAction: true);
    }

    // Rompe la roca SIN que haya sido un cabezazo del jugador. La usan, por ejemplo,
    // las rocas que se acercan demasiado a Mara (ella "esquiva"/rompe sola, controlada
    // por el juego) o una roca que alcanza a golpear a Nami antes de que el jugador
    // llegara a romperla con Y.
    public void ForceBreak()
    {
        if (isBroken) return;
        Break(wasPlayerAction: false);
    }

    private void Break(bool wasPlayerAction)
    {
        isBroken = true;

        onBroken?.Invoke();

        if (wasPlayerAction)
        {
            onBrokenByPlayer?.Invoke();
            BrokenByPlayer?.Invoke();
        }

        // Por ahora destruimos el objeto de inmediato. Si más adelante agregas una
        // animación o partículas de rotura, puedes retrasar este Destroy (por ejemplo,
        // llamándolo desde un evento de animación) en vez de aquí directamente.
        Destroy(gameObject);
    }
}
using UnityEngine;
using UnityEngine.Events;

// Poner este componente en cada roca destructible del mapa.
// hitsToBreak se ajusta INDIVIDUALMENTE por roca en el Inspector,
public class DestructibleRock : MonoBehaviour
{
    [Tooltip("Cuántos cabezazos aguanta esta roca antes de romperse.")]
    [SerializeField] private int hitsToBreak = 1;

    [Tooltip("Se dispara cuando la roca se rompe. Engancha aquí partículas, sonido, " +
             "o una animación de rotura desde el Inspector, sin tocar este script.")]
    [SerializeField] private UnityEvent onBroken;

    private int currentHits;

    public void TakeHit()
    {
        currentHits++;
        Debug.Log($"🪨 Roca golpeada: {currentHits}/{hitsToBreak}"); // TEMPORAL: borrar cuando tengas la animación

        if (currentHits >= hitsToBreak)
            Break();
    }

    private void Break()
    {
        onBroken?.Invoke();

        // Por ahora destruimos el objeto de inmediato. Si más adelante agregas una
        // animación o partículas de rotura, puedes retrasar este Destroy (por ejemplo,
        // llamándolo desde un evento de animación) en vez de aquí directamente.
        Destroy(gameObject);
    }
}
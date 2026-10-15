using UnityEngine;

namespace UnderwaterFX
{
    /// <summary>
    /// Zona (collider en modo Trigger) que controla el modo lluvia cuando el jugador la toca.
    /// Ponlo en un objeto con un Collider; al anadirlo, el collider se marca solo como Trigger.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class RainTrigger : MonoBehaviour
    {
        public enum Mode
        {
            [Tooltip("Al entrar se activa (o desactiva) la lluvia y se queda asi.")]
            SetOnEnter,
            [Tooltip("Cada vez que se entra, la lluvia cambia: si estaba apagada se enciende y viceversa.")]
            ToggleOnEnter,
            [Tooltip("Llueve solo mientras el jugador esta dentro de la zona.")]
            FollowPresence
        }

        [Tooltip("Si se deja vacio se usa el WaterRainController de la escena.")]
        [SerializeField] WaterRainController rain;
        [Tooltip("Etiqueta del jugador.")]
        [SerializeField] string playerTag = "Player";

        public Mode mode = Mode.SetOnEnter;
        [Tooltip("Valor que se aplica al entrar (SetOnEnter y FollowPresence). Marcado = empieza a llover.")]
        public bool rainOnEnter = true;
        [Tooltip("Solo se dispara la primera vez que el jugador entra.")]
        public bool triggerOnce = false;

        bool used;

        void Reset()
        {
            Collider c = GetComponent<Collider>();
            if (c != null) c.isTrigger = true;
        }

        void OnTriggerEnter(Collider other)
        {
            if (!IsPlayer(other)) return;
            if (triggerOnce && used) return;
            if (!Resolve()) return;

            switch (mode)
            {
                case Mode.ToggleOnEnter:
                    rain.ToggleRain();
                    break;
                default: // SetOnEnter y FollowPresence
                    rain.SetRaining(rainOnEnter);
                    break;
            }
            used = true;
        }

        void OnTriggerExit(Collider other)
        {
            if (mode != Mode.FollowPresence) return;
            if (!IsPlayer(other)) return;
            if (!Resolve()) return;

            rain.SetRaining(!rainOnEnter);
        }

        bool IsPlayer(Collider other)
        {
            if (other.CompareTag(playerTag)) return true;
            if (other.attachedRigidbody != null && other.attachedRigidbody.CompareTag(playerTag)) return true;
            return other.transform.root.CompareTag(playerTag);
        }

        bool Resolve()
        {
            if (rain == null) rain = FindFirstObjectByType<WaterRainController>();
            if (rain == null)
            {
                Debug.LogWarning("[RainTrigger] No hay ningun WaterRainController en la escena.");
                return false;
            }
            return true;
        }
    }
}
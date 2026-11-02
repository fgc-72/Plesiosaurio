using System.Collections;
using UnityEngine;
using Unity.Cinemachine;

// Trigger del laberinto (al terminar la zona recta). Cuando Nami lo cruza:
//   1) Nami pierde el control (queda quieta, en idle).
//   2) La cámara cambia a 'sharkCamera', que ve al tiburón de lejos.
//   3) Sale el texto, y el tiburón empieza a perseguirla (StartChase).
//   4) Pasado el tiempo (o al terminar el texto, lo que dure más), la cámara vuelve
//      y Nami recupera el control.
// Solo se activa una vez.
[RequireComponent(typeof(Collider))]
public class SharkChaseEvent : MonoBehaviour
{
    [Header("Activación")]
    [SerializeField] private string playerTag = "Player";

    [Header("Tiburón")]
    [Tooltip("El tiburón puede empezar DESACTIVADO en la escena: el evento lo activa. Si lo dejas " +
             "vacío, se busca un SharkChaser en la escena (incluso desactivado).")]
    [SerializeField] private SharkChaser shark;
    [Tooltip("Segundos de cámara ANTES de que el tiburón empiece a moverse (para que el " +
             "jugador lo vea quieto un momento). 0 = empieza de inmediato.")]
    [SerializeField] private float delayBeforeChase = 1f;

    [Header("Cámara")]
    [Tooltip("CinemachineCamera puesta a mano, mirando al tiburón de lejos.")]
    [SerializeField] private CinemachineCamera sharkCamera;
    [SerializeField] private int sharkCameraPriority = 30;
    [Tooltip("Mínimo de segundos que se queda la cámara en el tiburón, aunque el texto sea corto.")]
    [SerializeField] private float minCameraSeconds = 4f;

    [Header("Texto y misión (IDs)")]
    [SerializeField] private string dialogueId;
    [Tooltip("Opcional: misión que sale al terminar el evento (ej. 'escapa del tiburón').")]
    [SerializeField] private string missionIdOnFinish;

    private bool hasTriggered;

    // Permite que el evento vuelva a dispararse (lo usa SharkHitDetector tras reiniciar
    // al jugador, para que el reintento vuelva a mostrar al tiburón).
    public void ResetEvent()
    {
        hasTriggered = false;
    }

    private void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasTriggered || !other.CompareTag(playerTag))
            return;

        PlayerControls controls = other.GetComponentInParent<PlayerControls>();
        if (controls == null)
        {
            Debug.LogWarning("SharkChaseEvent: el jugador no tiene PlayerControls, el evento no puede arrancar.");
            return;
        }

        hasTriggered = true;
        StartCoroutine(RunEvent(controls));
    }

    private IEnumerator RunEvent(PlayerControls controls)
    {
        if (shark == null)
            shark = FindFirstObjectByType<SharkChaser>(FindObjectsInactive.Include);

        // El tiburón aparece aquí: se activa antes de que la cámara lo enfoque.
        if (shark != null && !shark.gameObject.activeSelf)
            shark.gameObject.SetActive(true);

        PlayerAnimation playerAnimation = controls.GetComponent<PlayerAnimation>();
        Animator namiAnimator = controls.GetComponent<Animator>();
        Rigidbody namiRb = controls.GetComponent<Rigidbody>();

        // 1) Nami queda quieta.
        controls.SetControlEnabled(false);
        if (playerAnimation != null) playerAnimation.SetAnimationOverride(true);
        if (namiAnimator != null) namiAnimator.SetInteger("AnimationState", 0);
        if (namiRb != null && !namiRb.isKinematic)
        {
            namiRb.linearVelocity = Vector3.zero;
            namiRb.angularVelocity = Vector3.zero;
        }

        // 2) Cámara al tiburón.
        int originalPriority = 0;
        if (sharkCamera != null)
        {
            originalPriority = sharkCamera.Priority;
            sharkCamera.Priority = sharkCameraPriority;
        }
        else
        {
            Debug.LogWarning("SharkChaseEvent: falta asignar 'Shark Camera'. Nami se bloquea pero la cámara no cambia.");
        }

        // 3) Texto (sale ya mientras la cámara enfoca al tiburón a lo lejos).
        float dialogueDuration = 0f;
        if (!string.IsNullOrEmpty(dialogueId) && ThoughtUI.Instance != null)
        {
            dialogueDuration = ThoughtUI.Instance.GetDurationById(dialogueId);
            ThoughtUI.Instance.ShowById(dialogueId);
        }

        // 4) Se queda en cámara el tiempo del texto o el mínimo, lo que sea mayor.
        //    (El tiburón permanece quieto en su posición para NO devorarse a Nami mientras ella está quieta).
        float total = Mathf.Max(minCameraSeconds, dialogueDuration);
        if (total > 0f)
            yield return new WaitForSeconds(total);

        // 5) La cámara vuelve a Nami y Nami recupera el control para poder huir.
        if (sharkCamera != null)
            sharkCamera.Priority = originalPriority;

        if (playerAnimation != null) playerAnimation.SetAnimationOverride(false);
        controls.SetControlEnabled(true);

        // 6) Breve margen para que el jugador empiece a nadar y luego el tiburón arranca la persecución desde lejos.
        if (delayBeforeChase > 0f)
            yield return new WaitForSeconds(delayBeforeChase);

        if (shark != null)
            shark.StartChase();
        else
            Debug.LogWarning("SharkChaseEvent: no encontré ningún SharkChaser en la escena.");

        if (!string.IsNullOrEmpty(missionIdOnFinish))
        {
            if (MissionUI.Instance != null)
            {
                try
                {
                    MissionUI.Instance.ShowMission(missionIdOnFinish);
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"SharkChaseEvent: no se pudo mostrar la misión '{missionIdOnFinish}': {ex.Message}");
                }
            }
            else
            {
                Debug.LogWarning($"SharkChaseEvent: misión '{missionIdOnFinish}' pero no hay MissionUI en la escena.");
            }
        }
    }
}
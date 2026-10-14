using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using System.Collections;

// La roca grande que choca a Nami tras romper las 4 rocas pequeñas: viene
// directo y sin que se pueda evitar, pantalla a negro + desenfoque (misma
// idea que el "despertar" del Acto I), y Nami aparece en otra parte del
// mapa, sola, sin Mara. Dispáralo desde RockSequenceManager.
public class BigRockCinematic : MonoBehaviour
{
    [Header("Nami")]
    [SerializeField] private PlayerControls playerControls;
    [SerializeField] private PlayerAnimation playerAnimation;
    [SerializeField] private Rigidbody namiRigidbody;

    [Header("Mara")]
    [Tooltip("Se congela en su sitio durante la cinemática. Al terminar retoma su " +
             "camino por su cuenta: a partir de aquí Nami y Mara quedan separadas, " +
             "por eso NO la movemos junto con Nami.")]
    [SerializeField] private MaraGuide maraGuide;

    [Header("Roca grande")]
    [Tooltip("El GameObject de la roca grande ya puesto en la escena (puede empezar desactivado).")]
    [SerializeField] private Transform bigRockObject;
    [SerializeField] private Transform bigRockStartPoint;
    [SerializeField] private float bigRockSpeed = 15f;

    [Header("Pantalla negra + desenfoque")]
    [SerializeField] private ScreenBlackoutEffect blackout;
    [SerializeField] private Volume wakeUpVolume;
    [SerializeField] private float blurredAperture = 1f;
    [SerializeField] private float clearAperture = 16f;
    [SerializeField] private float blurredFocusDistance = 0.3f;
    [SerializeField] private float normalFocusDistance = 10f;
    [SerializeField] private float blurClearDuration = 2f;

    [Header("Dónde despierta Nami")]
    [Tooltip("Usado SOLO si 'Scene To Load' está vacío (teletransporte dentro de la misma escena).")]
    [SerializeField] private Transform wakeUpPoint;

    [Header("Cambio de escena (opcional)")]
    [Tooltip("Nombre EXACTO de la escena a cargar (como aparece en Build Settings). Si lo " +
             "llenas, en vez de teletransportar a Nami dentro de esta misma escena, se carga " +
             "esa escena y Nami aparece ahí, en el GameObject llamado 'Wake Up Point Name'. " +
             "Mara y el resto del mapa viejo NO pasan con ella (se quedan en la escena vieja) " +
             "— coherente con que despierta sola.")]
    [SerializeField] private string sceneToLoad;
    [Tooltip("Nombre EXACTO del GameObject, ya puesto en la escena nueva, que marca dónde " +
             "debe aparecer Nami (puede ser un Empty GameObject cualquiera, solo importa el nombre).")]
    [SerializeField] private string wakeUpPointName = "WakeUpPoint";

    [Header("Diálogo")]
    [Tooltip("ID en el DialogueDatabase para '¿Dónde está mamá?'")]
    [SerializeField] private string wakeUpDialogueId;

    private DepthOfField depthOfField;
    private bool hasPlayed;

    public void Play()
    {
        if (hasPlayed) return;
        hasPlayed = true;
        Debug.Log("🪨 BigRockCinematic: arrancó.");
        StartCoroutine(PlaySequence());
    }

    private IEnumerator PlaySequence()
    {
        bool changingScene = !string.IsNullOrEmpty(sceneToLoad);

        if (maraGuide != null)
            maraGuide.enabled = false; // Mara se queda quieta donde esté (y, si cambiamos de
                                        // escena, se queda ahí para siempre: no persiste)

        // 1) La roca grande viene directo a Nami: no hay cabezazo que la detenga.
        if (bigRockObject == null || bigRockStartPoint == null)
            Debug.LogWarning("BigRockCinematic: falta asignar 'Big Rock Object' o 'Big Rock Start Point' " +
                              "en el Inspector, así que se salta la parte de la roca viniendo volando.");

        if (bigRockObject != null && bigRockStartPoint != null)
        {
            bigRockObject.position = bigRockStartPoint.position;
            bigRockObject.gameObject.SetActive(true);

            float startDistance = Vector3.Distance(bigRockObject.position, namiRigidbody.position);
            Debug.Log($"🪨 BigRockCinematic: la roca grande arranca a {startDistance:F1}m de Nami " +
                      $"(velocidad {bigRockSpeed}/s → ~{startDistance / bigRockSpeed:F1}s de viaje).");

            // Si esto imprime ~0m o un número de segundos casi nulo, el 'Big Rock Start Point'
            // está puesto demasiado cerca de Nami: muévelo bien lejos (por ejemplo, a la altura
            // de la superficie, igual que haces con las rocas chicas) para que de verdad se vea venir.
            if (startDistance < 1f)
                Debug.LogWarning("BigRockCinematic: el Start Point está a menos de 1m de Nami, " +
                                  "por eso la roca 'no se ve' — pasa directo a la pantalla negra.");

            while (Vector3.Distance(bigRockObject.position, namiRigidbody.position) > 1f)
            {
                bigRockObject.position = Vector3.MoveTowards(
                    bigRockObject.position, namiRigidbody.position, bigRockSpeed * Time.deltaTime);
                yield return null;
            }

            bigRockObject.gameObject.SetActive(false);
        }

        // 2) Impacto: se bloquea el control de Nami y empezamos a ir a negro.
        playerControls.SetControlEnabled(false);
        if (playerAnimation != null)
            playerAnimation.SetAnimationOverride(true);

        // Antes de ir a negro: si vamos a CAMBIAR DE ESCENA, todo lo que necesite seguir
        // existiendo del otro lado (Nami, la pantalla negra, el volumen de desenfoque y
        // este mismo script, que sigue corriendo a mitad de la corrutina) tiene que
        // marcarse para sobrevivir la carga — si no, Unity lo destruye junto con la
        // escena vieja y todo esto se corta a medias. Mara y el mapa viejo NO se marcan
        // a propósito: se quedan atrás.
        if (changingScene)
        {
            DontDestroyOnLoad(namiRigidbody.transform.root.gameObject);
            DontDestroyOnLoad(gameObject);
            if (blackout != null)
                DontDestroyOnLoad(blackout.gameObject);
            if (wakeUpVolume != null)
                DontDestroyOnLoad(wakeUpVolume.gameObject);
        }

        if (blackout != null)
            yield return blackout.FadeToBlack();

        // 3) Mientras está en negro: O cargamos la escena nueva y ubicamos a Nami en su
        //    punto de despertar, O (comportamiento viejo) la teletransportamos dentro de
        //    esta misma escena. Cualquiera de los dos, dejamos el desenfoque preparado
        //    (bien borroso) para que al volver de negro ya esté listo.
        if (changingScene)
        {
            yield return SceneManager.LoadSceneAsync(sceneToLoad);

            GameObject point = GameObject.Find(wakeUpPointName);
            if (point != null)
            {
                namiRigidbody.position = point.transform.position;
                namiRigidbody.rotation = point.transform.rotation;
            }
            else
            {
                Debug.LogWarning($"BigRockCinematic: no encontré ningún GameObject llamado " +
                                  $"'{wakeUpPointName}' en la escena '{sceneToLoad}'. Revisa que " +
                                  "el nombre esté EXACTO (sensible a mayúsculas) — Nami queda " +
                                  "donde la haya dejado la carga de escena.");
            }
        }
        else if (wakeUpPoint != null)
        {
            namiRigidbody.position = wakeUpPoint.position;
            namiRigidbody.rotation = wakeUpPoint.rotation;
        }

        if (wakeUpVolume != null && wakeUpVolume.profile.TryGet(out depthOfField))
        {
            depthOfField.aperture.value = blurredAperture;
            depthOfField.focusDistance.value = blurredFocusDistance;
        }

        // 4) Volvemos de negro ya en el nuevo lugar (todavía borroso).
        if (blackout != null)
            yield return blackout.FadeFromBlack();

        // 5) El desenfoque se aclara progresivamente.
        if (depthOfField != null)
            yield return ClearBlur();

        // 6) Diálogo: "¿Dónde está mamá?"
        if (ThoughtUI.Instance != null && !string.IsNullOrEmpty(wakeUpDialogueId))
        {
            ThoughtUI.Instance.ShowById(wakeUpDialogueId);
            yield return new WaitUntil(() => !ThoughtUI.Instance.IsShowing);
        }

        // 7) Nami recupera el control, sola. Mara retoma su camino por su cuenta.
        if (playerAnimation != null)
            playerAnimation.SetAnimationOverride(false);
        playerControls.SetControlEnabled(true);

        // Si cambiamos de escena, Mara se quedó en la vieja (no persiste): no hay
        // nada que reactivar. Si fue teletransporte dentro de la misma escena, sigue
        // su camino normalmente, como antes.
        if (!changingScene && maraGuide != null)
            maraGuide.enabled = true;
    }

    private IEnumerator ClearBlur()
    {
        float elapsed = 0f;
        float startAperture = depthOfField.aperture.value;
        float startFocus = depthOfField.focusDistance.value;

        while (elapsed < blurClearDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / blurClearDuration;

            depthOfField.aperture.value = Mathf.Lerp(startAperture, clearAperture, t);
            depthOfField.focusDistance.value = Mathf.Lerp(startFocus, normalFocusDistance, t);

            yield return null;
        }

        depthOfField.aperture.value = clearAperture;
        depthOfField.focusDistance.value = normalFocusDistance;
    }
}
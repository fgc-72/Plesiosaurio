using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Unity.Cinemachine;
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
    [Tooltip("Si lo dejas vacío, se busca solo en namiRigidbody al arrancar.")]
    [SerializeField] private Animator namiAnimator;

    [Header("Pose acostada al despertar (opcional)")]
    [Tooltip("Si es true, Nami aparece ACOSTADA (AnimationState 4) apenas llega al punto de " +
             "despertar, se queda así mientras sale el diálogo final, y pasa a Idle justo " +
             "cuando el diálogo termina (antes de devolver el control).")]
    [SerializeField] private bool wakeUpLyingDown = true;
    [Tooltip("Nombre EXACTO del estado de la pose acostada en el Animator (en tu grafo se " +
             "llama 'Sleeping'). Se reproduce directo, sin depender de la transición.")]
    [SerializeField] private string lyingStateName = "Sleeping";

    [Header("Mara")]
    [Tooltip("Se congela en su sitio durante la cinemática. Al terminar retoma su " +
             "camino por su cuenta: a partir de aquí Nami y Mara quedan separadas, " +
             "por eso NO la movemos junto con Nami.")]
    [SerializeField] private MaraGuide maraGuide;

    [Header("Cámara (opcional)")]
    [Tooltip("Solo arrastra la cámara: apenas se activa la roca grande, esta cámara toma " +
             "prioridad (primer plano de la caída). Apenas se termina de ir a negro, se le " +
             "devuelve la prioridad original — así la cámara que esté activa en el lugar " +
             "donde Nami despierta vuelve a tomar el control sola.")]
    [SerializeField] private CinemachineCamera closeUpCamera;
    [SerializeField] private int closeUpCameraPriority = 20;

    [Header("Roca grande")]
    [Tooltip("El GameObject de la roca grande ya puesto en la escena (puede empezar desactivado).")]
    [SerializeField] private Transform bigRockObject;
    [SerializeField] private Transform bigRockStartPoint;
    [SerializeField] private float bigRockSpeed = 15f;
    [Tooltip("A qué distancia de Nami se considera que 'ya casi la golpea' y se corta a negro. " +
             "Súbelo si quieres que el fundido arranque con la roca todavía más lejos.")]
    [SerializeField] private float impactDistance = 1f;

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
    [Tooltip("Opcional: diálogo que sale MIENTRAS la roca grande va cayendo hacia Nami " +
             "(en paralelo, no bloquea la caída). Déjalo vacío si no quieres ninguno acá.")]
    [SerializeField] private string fallingDialogueId;

    [Header("Misión al terminar (opcional)")]
    [Tooltip("ID de la misión (MissionDatabase) a mostrar cuando Nami recupera el control " +
             "en el lugar nuevo, al final de toda la cinemática. Vacío = no activa ninguna.")]
    [SerializeField] private string missionIdOnFinish;

    private DepthOfField depthOfField;
    private bool hasPlayed;
    private bool forcingLyingPose;

    private void Awake()
    {
        // GetComponent (sin 'InChildren') solo busca en el MISMO GameObject que el
        // Rigidbody. Si el Animator de Nami vive en un hijo (el modelo/mesh, que es
        // lo normal), eso da null en silencio — por eso probamos primero ahí y
        // recién si falla probamos en los hijos.
        if (namiAnimator == null && namiRigidbody != null)
        {
            namiAnimator = namiRigidbody.GetComponent<Animator>();
            if (namiAnimator == null)
                namiAnimator = namiRigidbody.GetComponentInChildren<Animator>();
        }

        // Mismo blindaje que en OpeningCinematicAct1: usamos SIEMPRE los componentes que
        // están en el Nami real, por si lo arrastrado en el Inspector apunta a otra copia.
        if (namiRigidbody != null)
        {
            PlayerAnimation realAnimation = namiRigidbody.GetComponent<PlayerAnimation>();
            if (realAnimation != null) playerAnimation = realAnimation;

            PlayerControls realControls = namiRigidbody.GetComponent<PlayerControls>();
            if (realControls != null) playerControls = realControls;
        }

        if (wakeUpLyingDown && namiAnimator == null)
            Debug.LogWarning("BigRockCinematic: 'Wake Up Lying Down' está activado pero no " +
                              "encontré ningún Animator (ni en Nami Rigidbody ni en sus hijos). " +
                              "Asigna 'Nami Animator' a mano en el Inspector.");
    }

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

        // Limpiamos las rocas normales que todavía estén en el aire (el spawner ya dejó
        // de crear nuevas, pero las existentes siguen cayendo solas) para que no se
        // crucen visualmente con la roca grande durante la cinemática.
        foreach (FallingRock rock in FindObjectsByType<FallingRock>(FindObjectsSortMode.None))
            Destroy(rock.gameObject);

        // Primer plano de la caída: apenas se activa la roca grande, esta cámara toma el
        // control (prioridad alta) hasta que se termine de ir a negro, más abajo.
        int closeUpOriginalPriority = 0;
        if (closeUpCamera != null)
        {
            closeUpOriginalPriority = closeUpCamera.Priority;
            closeUpCamera.Priority = closeUpCameraPriority;
        }

        // 1) La roca grande viene directo a Nami: no hay cabezazo que la detenga.
        if (bigRockObject == null || bigRockStartPoint == null)
            Debug.LogWarning("BigRockCinematic: falta asignar 'Big Rock Object' o 'Big Rock Start Point' " +
                              "en el Inspector, así que se salta la parte de la roca viniendo volando.");

        if (bigRockObject != null && bigRockStartPoint != null)
        {
            bigRockObject.position = bigRockStartPoint.position;
            bigRockObject.gameObject.SetActive(true);

            // Diálogo en PARALELO a la caída (no usamos yield/WaitUntil acá a propósito:
            // la roca tiene que seguir viniendo igual, hable Mara/Nami o no).
            if (!string.IsNullOrEmpty(fallingDialogueId) && ThoughtUI.Instance != null)
                ThoughtUI.Instance.ShowById(fallingDialogueId);

            float startDistance = Vector3.Distance(bigRockObject.position, namiRigidbody.position);
            Debug.Log($"🪨 BigRockCinematic: la roca grande arranca a {startDistance:F1}m de Nami " +
                      $"(velocidad {bigRockSpeed}/s → ~{startDistance / bigRockSpeed:F1}s de viaje).");

            // Si esto imprime ~0m o un número de segundos casi nulo, el 'Big Rock Start Point'
            // está puesto demasiado cerca de Nami: muévelo bien lejos (por ejemplo, a la altura
            // de la superficie, igual que haces con las rocas chicas) para que de verdad se vea venir.
            if (startDistance < impactDistance)
                Debug.LogWarning($"BigRockCinematic: el Start Point está a menos de {impactDistance}m " +
                                  "de Nami, por eso la roca 'no se ve' — pasa directo a la pantalla negra.");

            while (Vector3.Distance(bigRockObject.position, namiRigidbody.position) > impactDistance)
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

        // Ya está todo negro: devolvemos la prioridad de la cámara de primer plano a
        // como estaba, para que no se quede "pegada" cuando reaparezca la imagen.
        if (closeUpCamera != null)
            closeUpCamera.Priority = closeUpOriginalPriority;

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

        // Nami aparece ACOSTADA en el lugar nuevo. Reafirmamos el valor cada frame
        // (en vez de ponerlo una sola vez) porque en el Acto I detectamos que algo más
        // pisaba 'AnimationState' si no se insistía constantemente — sigue sin saberse
        // la causa raíz exacta, así que lo forzamos igual acá por seguridad.
        if (wakeUpLyingDown && namiAnimator != null)
        {
            forcingLyingPose = true;

            // Reproducimos el estado acostado DIRECTO (sin esperar la transición desde
            // Any State, que depende de condiciones/exit time del grafo). El parámetro
            // forzado a 4 cada frame evita que 'Sleeping -> Idle' la saque de ahí.
            int lyingHash = Animator.StringToHash(lyingStateName);
            if (namiAnimator.HasState(0, lyingHash))
                namiAnimator.Play(lyingHash, 0, 0f);
            else
                Debug.LogWarning($"BigRockCinematic: el Animator de Nami no tiene un estado llamado " +
                                  $"'{lyingStateName}' en la capa 0. Revisa el nombre en el Inspector.");

            StartCoroutine(ForceAnimationStateWhile(() => forcingLyingPose, 4));
            StartCoroutine(LogLyingPoseStatus());
        }

        // 4) Volvemos de negro ya en el nuevo lugar (todavía borroso).
        if (blackout != null)
            yield return blackout.FadeFromBlack();

        // 5) El desenfoque se aclara progresivamente.
        if (depthOfField != null)
            yield return ClearBlur();

        // 6) Diálogo: "¿Dónde está mamá?" — Nami se queda acostada durante TODO este diálogo.
        if (ThoughtUI.Instance != null && !string.IsNullOrEmpty(wakeUpDialogueId))
        {
            ThoughtUI.Instance.ShowById(wakeUpDialogueId);
            // Primero esperamos a que REALMENTE aparezca (si no, el WaitUntil de abajo
            // puede resolverse de inmediato si el fade-in todavía no arrancó).
            yield return new WaitUntil(() => ThoughtUI.Instance.IsShowing);
            yield return new WaitUntil(() => !ThoughtUI.Instance.IsShowing);
        }

        // Apenas termina el diálogo (o de una, si no había diálogo asignado): pasa a Idle.
        if (wakeUpLyingDown && namiAnimator != null)
        {
            forcingLyingPose = false;
            namiAnimator.SetInteger("AnimationState", 0);
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

        // Nueva misión: ya terminó toda la cinemática y Nami tiene el control.
        if (!string.IsNullOrEmpty(missionIdOnFinish))
        {
            if (MissionUI.Instance != null)
                MissionUI.Instance.ShowMission(missionIdOnFinish);
            else
                Debug.LogWarning($"BigRockCinematic: missionIdOnFinish = '{missionIdOnFinish}' pero " +
                                  "MissionUI.Instance es null (¿hay un MissionUI en la escena nueva?).");
        }
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

    // Diagnóstico temporal: medio segundo después de aparecer, dice en consola qué
    // está sonando de verdad en el Animator, para saber si la pose se aplicó.
    private IEnumerator LogLyingPoseStatus()
    {
        yield return new WaitForSeconds(0.5f);

        if (!forcingLyingPose || namiAnimator == null)
            yield break;

        bool isLying = namiAnimator.GetCurrentAnimatorStateInfo(0).IsName(lyingStateName);
        Debug.Log($"🛌 BigRockCinematic: AnimationState = {namiAnimator.GetInteger("AnimationState")}, " +
                  $"¿está en '{lyingStateName}'? {isLying}, animator enabled = {namiAnimator.enabled}.");
    }

    // Reafirma 'AnimationState' en el Animator TODOS los frames mientras 'condition'
    // sea true. Mismo patrón que OpeningCinematicAct1: evita que la pose se rompa si
    // algún otro sistema sigue tocando el parámetro por su cuenta.
    private IEnumerator ForceAnimationStateWhile(System.Func<bool> condition, int state)
    {
        while (condition())
        {
            namiAnimator.SetInteger("AnimationState", state);
            yield return null;
        }
    }
}
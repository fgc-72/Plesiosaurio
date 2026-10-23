using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Unity.Cinemachine;
using System.Collections;

// La roca grande que choca a Nami tras romper las 4 rocas pequeñas:
// 1. La roca grande desciende a gran velocidad e impacta a Nami.
// 2. Al recibir el golpe, la cámara reacciona con una sacudida de impacto y la imagen se pone borrosa progresivamente.
// 3. La pantalla se funde a negro en paralelo.
// 4. Se carga la siguiente escena (acto2) donde Nami despierta sola y recupera la nitidez progresivamente.
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
    [SerializeField] private float bigRockSpeed = 45f;
    [Tooltip("A qué distancia de Nami se considera que impacta visualmente.")]
    [SerializeField] private float impactDistance = 1.2f;

    [Header("Pantalla negra + desenfoque")]
    [SerializeField] private ScreenBlackoutEffect blackout;
    [SerializeField] private Volume wakeUpVolume;
    [SerializeField] private float blurredAperture = 1f;
    [SerializeField] private float clearAperture = 16f;
    [SerializeField] private float blurredFocusDistance = 0.1f;
    [SerializeField] private float normalFocusDistance = 10f;
    [SerializeField] private float blurClearDuration = 3f;

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

    private DepthOfField depthOfField;
    private bool hasPlayed;
    private bool forcingLyingPose;

    private void Awake()
    {
        // Auto-asignación de referencias si faltan en el Inspector
        if (playerControls == null)
            playerControls = FindFirstObjectByType<PlayerControls>();

        if (playerControls != null)
        {
            if (namiRigidbody == null)
                namiRigidbody = playerControls.GetComponent<Rigidbody>();
            if (playerAnimation == null)
                playerAnimation = playerControls.GetComponent<PlayerAnimation>();
        }

        if (namiAnimator == null && namiRigidbody != null)
        {
            namiAnimator = namiRigidbody.GetComponent<Animator>();
            if (namiAnimator == null)
                namiAnimator = namiRigidbody.GetComponentInChildren<Animator>();
        }

        if (blackout == null)
            blackout = GetComponent<ScreenBlackoutEffect>();
        if (blackout == null)
            blackout = FindFirstObjectByType<ScreenBlackoutEffect>();
    }

    public void Play()
    {
        if (hasPlayed) return;
        hasPlayed = true;
        Debug.Log("🪨 BigRockCinematic: arrancó la cinemática de la roca grande.");
        StartCoroutine(PlaySequence());
    }

    private IEnumerator PlaySequence()
    {
        bool changingScene = !string.IsNullOrEmpty(sceneToLoad);

        if (maraGuide != null)
            maraGuide.enabled = false;

        // Limpiamos las rocas normales que sigan en el aire
        foreach (FallingRock rock in FindObjectsByType<FallingRock>(FindObjectsSortMode.None))
            Destroy(rock.gameObject);

        // Primer plano de la caída
        int closeUpOriginalPriority = 0;
        if (closeUpCamera != null)
        {
            closeUpOriginalPriority = closeUpCamera.Priority;
            closeUpCamera.Priority = closeUpCameraPriority;
        }

        // 1) La roca grande viene directo a Nami
        if (bigRockObject != null && bigRockStartPoint != null)
        {
            bigRockObject.position = bigRockStartPoint.position;
            bigRockObject.gameObject.SetActive(true);

            if (!string.IsNullOrEmpty(fallingDialogueId) && ThoughtUI.Instance != null)
                ThoughtUI.Instance.ShowById(fallingDialogueId);

            float startDistance = Vector3.Distance(bigRockObject.position, namiRigidbody.position);
            Debug.Log($"🪨 BigRockCinematic: la roca grande arranca a {startDistance:F1}m de Nami " +
                      $"(velocidad {bigRockSpeed}/s → ~{startDistance / bigRockSpeed:F1}s de viaje).");

            // Aseguramos que la roca llegue hasta Nami (máximo 1.5m para que el golpe sea real)
            float actualImpactDistance = Mathf.Min(impactDistance, 1.5f);

            while (namiRigidbody != null && Vector3.Distance(bigRockObject.position, namiRigidbody.position) > actualImpactDistance)
            {
                bigRockObject.position = Vector3.MoveTowards(
                    bigRockObject.position, namiRigidbody.position, bigRockSpeed * Time.deltaTime);
                yield return null;
            }

            // ¡Impacto! La roca desaparece al chocar
            bigRockObject.gameObject.SetActive(false);
        }

        // 2) Impacto: se bloquea el control de Nami
        if (playerControls != null)
            playerControls.SetControlEnabled(false);
        if (playerAnimation != null)
            playerAnimation.SetAnimationOverride(true);

        // Marcamos persistencia si vamos a cambiar de escena
        if (changingScene)
        {
            if (namiRigidbody != null)
                DontDestroyOnLoad(namiRigidbody.transform.root.gameObject);
            DontDestroyOnLoad(gameObject);
            if (blackout != null)
                DontDestroyOnLoad(blackout.gameObject);
            if (wakeUpVolume != null)
                DontDestroyOnLoad(wakeUpVolume.gameObject);
        }

        // 3) Reacción de la cámara al golpe + Desenfoque progresivo + Fundido a negro en paralelo
        yield return StartCoroutine(ImpactAndFadeOut());

        // Ya está todo negro: devolvemos la prioridad de la cámara
        if (closeUpCamera != null)
            closeUpCamera.Priority = closeUpOriginalPriority;

        // 4) Carga de la siguiente escena
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
                                  $"'{wakeUpPointName}' en la escena '{sceneToLoad}'.");
            }
        }
        else if (wakeUpPoint != null)
        {
            namiRigidbody.position = wakeUpPoint.position;
            namiRigidbody.rotation = wakeUpPoint.rotation;
        }

        // Preparamos el desenfoque al despertar (borroso para que aclare poco a poco)
        InitDepthOfField();
        ApplyBlur(1f);

        // Nami aparece acostada al despertar
        if (wakeUpLyingDown && namiAnimator != null)
        {
            forcingLyingPose = true;
            StartCoroutine(ForceAnimationStateWhile(() => forcingLyingPose, 4));
        }

        // 5) Volvemos de negro en el nuevo lugar (todavía borroso)
        if (blackout != null)
            yield return blackout.FadeFromBlack();

        // 6) El desenfoque se aclara progresivamente
        yield return ClearBlur();

        // 7) Diálogo: "¿Dónde está mamá?"
        if (ThoughtUI.Instance != null && !string.IsNullOrEmpty(wakeUpDialogueId))
        {
            ThoughtUI.Instance.ShowById(wakeUpDialogueId);
            yield return new WaitUntil(() => ThoughtUI.Instance.IsShowing);
            yield return new WaitUntil(() => !ThoughtUI.Instance.IsShowing);
        }

        // Pasa a Idle
        if (wakeUpLyingDown && namiAnimator != null)
        {
            forcingLyingPose = false;
            namiAnimator.SetInteger("AnimationState", 0);
        }

        // 8) Nami recupera el control
        if (playerAnimation != null)
            playerAnimation.SetAnimationOverride(false);
        if (playerControls != null)
            playerControls.SetControlEnabled(true);

        if (!changingScene && maraGuide != null)
            maraGuide.enabled = true;
    }

    private IEnumerator ImpactAndFadeOut()
    {
        InitDepthOfField();

        float fadeDuration = blackout != null ? blackout.FadeDuration : 2.5f;

        // Disparamos el FadeToBlack en paralelo
        Coroutine blackoutRoutine = null;
        if (blackout != null)
            blackoutRoutine = blackout.FadeToBlack();

        // Cámara que sacudiremos (la closeUpCamera si está activa, o Camera.main)
        Transform camToShake = (closeUpCamera != null && closeUpCamera.gameObject.activeInHierarchy)
            ? closeUpCamera.transform
            : (Camera.main != null ? Camera.main.transform : null);

        Vector3 camOriginalPos = camToShake != null ? camToShake.localPosition : Vector3.zero;
        Quaternion camOriginalRot = camToShake != null ? camToShake.localRotation : Quaternion.identity;

        float hitShakeDuration = 1.0f; // La sacudida del golpe dura el primer segundo
        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / fadeDuration);

            // A) Desenfoque que aumenta progresivamente con el tiempo
            ApplyBlur(t);

            // B) Movimiento de la cámara como si hubiese sido golpeada
            if (camToShake != null && elapsed < hitShakeDuration)
            {
                float shakeFactor = 1f - Mathf.Clamp01(elapsed / hitShakeDuration);
                float intensity = shakeFactor * shakeFactor; // decaimiento no lineal rápido

                // Desplazamiento brusco (sacudón lateral y retroceso hacia atrás)
                float offsetX = (Mathf.PerlinNoise(elapsed * 28f, 0f) - 0.5f) * 2f * 0.45f * intensity;
                float offsetY = (Mathf.PerlinNoise(0f, elapsed * 28f) - 0.5f) * 2f * 0.35f * intensity;
                float offsetZ = -0.6f * intensity; // retroceso hacia atrás por el golpe

                // Rotación brusca por el impacto (cabeceo, alabeo y guiñada)
                float rotPitch = (Mathf.PerlinNoise(elapsed * 32f, 15f) - 0.5f) * 9f * intensity;
                float rotYaw = (Mathf.PerlinNoise(15f, elapsed * 32f) - 0.5f) * 7f * intensity;
                float rotRoll = (Mathf.PerlinNoise(elapsed * 22f, 25f) - 0.5f) * 12f * intensity;

                camToShake.localPosition = camOriginalPos + new Vector3(offsetX, offsetY, offsetZ);
                camToShake.localRotation = camOriginalRot * Quaternion.Euler(rotPitch, rotYaw, rotRoll);
            }
            else if (camToShake != null && elapsed >= hitShakeDuration)
            {
                camToShake.localPosition = camOriginalPos;
                camToShake.localRotation = camOriginalRot;
            }

            yield return null;
        }

        // Restauramos el transform de la cámara
        if (camToShake != null)
        {
            camToShake.localPosition = camOriginalPos;
            camToShake.localRotation = camOriginalRot;
        }

        // Aseguramos desenfoque completo antes de que cargue la escena
        ApplyBlur(1f);

        // Esperamos que el fade a negro termine
        if (blackoutRoutine != null)
            yield return blackoutRoutine;
    }

    private void InitDepthOfField()
    {
        if (depthOfField != null) return;

        // 1. Probar en wakeUpVolume
        if (wakeUpVolume != null && wakeUpVolume.profile != null)
            wakeUpVolume.profile.TryGet(out depthOfField);

        // 2. Probar en Main Camera
        if (depthOfField == null && Camera.main != null)
        {
            Volume camVol = Camera.main.GetComponent<Volume>();
            if (camVol != null && camVol.profile != null)
                camVol.profile.TryGet(out depthOfField);
        }

        // 3. Fallback: buscar en cualquier volumen de la escena
        if (depthOfField == null)
        {
            foreach (Volume vol in FindObjectsByType<Volume>(FindObjectsSortMode.None))
            {
                if (vol.profile != null && vol.profile.TryGet(out depthOfField))
                    break;
            }
        }

        if (depthOfField != null)
        {
            depthOfField.active = true;
            depthOfField.mode.overrideState = true;
            depthOfField.focusDistance.overrideState = true;
            depthOfField.aperture.overrideState = true;
            depthOfField.focalLength.overrideState = true;
            depthOfField.gaussianStart.overrideState = true;
            depthOfField.gaussianEnd.overrideState = true;
            depthOfField.gaussianMaxRadius.overrideState = true;
        }
    }

    private void ApplyBlur(float t)
    {
        if (depthOfField == null) return;

        if (depthOfField.mode.value == DepthOfFieldMode.Bokeh)
        {
            // Bokeh: reducimos focusDistance y aumentamos apertura para desenfocar todo el campo visual
            depthOfField.focusDistance.value = Mathf.Lerp(normalFocusDistance, blurredFocusDistance, t);
            depthOfField.aperture.value = Mathf.Lerp(clearAperture, blurredAperture, t);
            depthOfField.focalLength.value = Mathf.Lerp(100f, 150f, t);
        }
        else
        {
            // Gaussiano: el blur empieza desde la lente hacia el frente
            depthOfField.gaussianStart.value = Mathf.Lerp(30f, 0f, t);
            depthOfField.gaussianEnd.value = Mathf.Lerp(80f, 2f, t);
            depthOfField.gaussianMaxRadius.value = Mathf.Lerp(0.2f, 1.5f, t);
        }
    }

    private IEnumerator ClearBlur()
    {
        if (depthOfField == null) yield break;

        float elapsed = 0f;

        while (elapsed < blurClearDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / blurClearDuration;
            // 1f - t va de desenfocado (1) a nítido (0)
            ApplyBlur(1f - t);
            yield return null;
        }

        ApplyBlur(0f);
    }

    private IEnumerator ForceAnimationStateWhile(System.Func<bool> condition, int state)
    {
        while (condition())
        {
            namiAnimator.SetInteger("AnimationState", state);
            yield return null;
        }
    }
}

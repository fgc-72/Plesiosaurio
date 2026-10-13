using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections;
using SceneManagement;

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
    [SerializeField] private Transform wakeUpPoint;

    [Header("Diálogo")]
    [Tooltip("ID en el DialogueDatabase para '¿Dónde está mamá?'")]
    [SerializeField] private string wakeUpDialogueId;

    private DepthOfField depthOfField;
    private bool hasPlayed;

    public void Play()
    {
        if (hasPlayed) return;
        hasPlayed = true;
        StartCoroutine(PlaySequence());
    }

    private IEnumerator PlaySequence()
    {
        if (maraGuide != null)
            maraGuide.enabled = false; // Mara se queda quieta donde esté

        // 1) La roca grande viene directo a Nami: no hay cabezazo que la detenga.
        if (bigRockObject != null && bigRockStartPoint != null)
        {
            bigRockObject.position = bigRockStartPoint.position;
            bigRockObject.gameObject.SetActive(true);

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

        if (blackout != null)
            yield return blackout.FadeToBlack();

        // 3) Mientras está en negro: teletransportamos a Nami al nuevo lugar
        //    y dejamos el desenfoque preparado (bien borroso, como al despertar).
        if (wakeUpPoint != null)
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

        if (maraGuide != null)
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

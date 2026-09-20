using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections;

// Vive en la escena de gameplay (Acto 1). Coordina la apertura:
// 1) Suena la voz de Mara mientras la pantalla sigue negra (gracias al SceneFader).
// 2) El Volume de Depth of Field empieza al máximo desenfoque.
// 3) Una vez la escena ya es visible, el desenfoque se aclara gradualmente.
public class OpeningSequence : MonoBehaviour
{
    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip maraVoiceLine;

    [Header("Desenfoque (Volume con Depth of Field en modo Bokeh)")]
    [SerializeField] private Volume postProcessVolume;
    [Tooltip("Qué tan borroso arranca. Número BAJO = MÁS desenfoque (así funciona un lente real).")]
    [SerializeField] private float blurredAperture = 1f;
    [Tooltip("Valor nítido/normal al terminar de enfocar. Número ALTO = nítido.")]
    [SerializeField] private float clearAperture = 16f;
    [Tooltip("Focus Distance mientras está borroso (casi 0 = nada está en foco).")]
    [SerializeField] private float blurredFocusDistance = 0.1f;
    [Tooltip("Focus Distance normal de gameplay (la distancia real cámara-personaje). " +
             "IMPORTANTE: sin esto, el desenfoque nunca se va del todo aunque el Aperture ya esté 'nítido'.")]
    [SerializeField] private float normalFocusDistance = 8f;
    [SerializeField] private float clearDuration = 1.5f;
    [Tooltip("Debe ser IGUAL o un poco mayor a la duración del fade de SceneFader, " +
             "para que el desenfoque empiece a aclararse justo cuando la escena ya es visible.")]
    [SerializeField] private float delayBeforeClear = 1.1f;

    private DepthOfField depthOfField;

    private void Start()
    {
        if (postProcessVolume.profile.TryGet(out depthOfField))
        {
            depthOfField.aperture.value = blurredAperture;
            depthOfField.focusDistance.value = blurredFocusDistance;
        }
        else
        {
            Debug.LogWarning("El Volume asignado no tiene un override de Depth of Field.");
        }

        if (audioSource != null && maraVoiceLine != null)
            audioSource.PlayOneShot(maraVoiceLine);

        StartCoroutine(ClearBlurAfterDelay());
    }

    private IEnumerator ClearBlurAfterDelay()
    {
        yield return new WaitForSeconds(delayBeforeClear);

        float elapsed = 0f;
        while (elapsed < clearDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / clearDuration;
            depthOfField.aperture.value = Mathf.Lerp(blurredAperture, clearAperture, t);
            depthOfField.focusDistance.value = Mathf.Lerp(blurredFocusDistance, normalFocusDistance, t);
            yield return null;
        }

        depthOfField.aperture.value = clearAperture;
        depthOfField.focusDistance.value = normalFocusDistance;
    }
}
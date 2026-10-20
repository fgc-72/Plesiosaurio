using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.EventSystems;

public class UISfx : MonoBehaviour
{
    [SerializeField] private AudioClip move;
    [SerializeField] private AudioClip select;
    [SerializeField] private AudioClip back;
    [SerializeField, Range(0f, 1f)] private float volume = 1f;

    private static UISfx instance;
    private AudioSource source;
    private GameObject lastSelected;

    public static void PlayMove()   => instance?.Play(instance.move);
    public static void PlaySelect() => instance?.Play(instance.select);
    public static void PlayBack()   => instance?.Play(instance.back);

[SerializeField] private AudioMixerGroup uiMixerGroup;

private void Awake()
{
    if (instance != null && instance != this)
    {
        Destroy(this);   // solo el componente, no el GameObject entero
        return;
    }
    instance = this;

    source = GetComponent<AudioSource>();
    if (source == null) source = gameObject.AddComponent<AudioSource>();
    source.playOnAwake = false;
    source.spatialBlend = 0f;
    if (uiMixerGroup != null) source.outputAudioMixerGroup = uiMixerGroup;
}    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private void Update()
    {
        var es = EventSystem.current;
        GameObject cur = es != null ? es.currentSelectedGameObject : null;

        if (cur != lastSelected)
        {
            // Suena solo si pasamos de un objeto visible a otro (no al abrir/cerrar paneles)
            if (cur != null && lastSelected != null && lastSelected.activeInHierarchy)
                PlayMove();
            lastSelected = cur;
        }
    }

    private void Play(AudioClip clip)
    {
        if (clip != null) source.PlayOneShot(clip, volume);
    }
}
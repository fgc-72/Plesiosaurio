using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;


public class ListenableSoundRegistry : MonoBehaviour
{
    [Header("Qué se detecta")]
    [SerializeField] private LayerMask listenableLayers; // el mismo layer Highlightable

    [Header("Audio")]
    [SerializeField] private AudioMixerGroup listenGroup; // grupo ListenMode del mixer

    [Header("Ajustes")]
    [SerializeField] private float refreshInterval = 1f;

    private readonly List<AudioSource> sources = new List<AudioSource>();
    private float nextRefresh;

    public IReadOnlyList<AudioSource> Sources => sources;

    private void Start()
    {
        Refresh();
    }

    private void Update()
    {
        if (Time.time >= nextRefresh)
            Refresh();
    }

    public void Refresh()
    {
        nextRefresh = Time.time + refreshInterval;
        sources.Clear();

        AudioSource[] all = FindObjectsByType<AudioSource>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        foreach (AudioSource s in all)
        {
            // ¿El layer del objeto está dentro de la máscara?
            if ((listenableLayers.value & (1 << s.gameObject.layer)) == 0) continue;

            if (listenGroup != null && s.outputAudioMixerGroup != listenGroup)
                s.outputAudioMixerGroup = listenGroup;

            sources.Add(s);
        }
    }
}
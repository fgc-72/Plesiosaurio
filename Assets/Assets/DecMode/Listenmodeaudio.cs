using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Rendering;

public class ListenModeAudio : MonoBehaviour
{
    [SerializeField] private Volume grayscaleVolume;
    [SerializeField] private AudioMixer mixer;
    [SerializeField] private string exposedParam = "ListenVolume";
    [SerializeField] private float silentDb = -80f;

    private void Update()
    {
        if (grayscaleVolume == null || mixer == null) return;

        float w = grayscaleVolume.weight;

        // El mixer trabaja en decibeles (escala logarítmica), no en lineal.
        // Convertimos el weight (0..1) a dB para que el fade suene natural.
        float db = w > 0.0001f ? Mathf.Max(silentDb, 20f * Mathf.Log10(w)) : silentDb;

        mixer.SetFloat(exposedParam, db);
    }
}
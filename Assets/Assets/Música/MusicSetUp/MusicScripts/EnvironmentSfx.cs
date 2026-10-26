using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Sonidos del entorno (salen por el grupo Environment del mixer).
/// Sonidos puntuales:   EnvironmentSfx.Play("Puerta");
/// Ambientes en loop:   EnvironmentSfx.PlayLoop("Viento");  EnvironmentSfx.StopLoop("Viento");
/// Pónlo en un objeto hijo del prefab de audio (junto a UISfx).
/// </summary>
public class EnvironmentSfx : MonoBehaviour
{
    [Serializable]
    public class Entry
    {
        public string id;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 1f;
    }

    [SerializeField] private AudioMixerGroup environmentGroup;
    [SerializeField] private List<Entry> sounds = new List<Entry>();

    private static EnvironmentSfx instance;
    private AudioSource oneShotSource;
    private readonly Dictionary<string, AudioSource> loops = new Dictionary<string, AudioSource>();

    public static void Play(string id)
    {
        if (instance != null) instance.PlayOneShot(id);
    }

    public static void PlayLoop(string id)
    {
        if (instance != null) instance.StartLoop(id);
    }

    public static void StopLoop(string id)
    {
        if (instance != null) instance.EndLoop(id);
    }

    private void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;

        oneShotSource = CreateSource(false);
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private AudioSource CreateSource(bool loop)
    {
        var s = gameObject.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.loop = loop;
        s.spatialBlend = 0f; // 2D
        s.outputAudioMixerGroup = environmentGroup;
        return s;
    }

    private Entry Find(string id)
    {
        var e = sounds.Find(x => x.id == id);
        if (e == null || e.clip == null)
            Debug.LogWarning($"[EnvironmentSfx] Sonido '{id}' no existe o no tiene clip.");
        return e;
    }

    private void PlayOneShot(string id)
    {
        var e = Find(id);
        if (e != null && e.clip != null) oneShotSource.PlayOneShot(e.clip, e.volume);
    }

    private void StartLoop(string id)
    {
        if (loops.ContainsKey(id)) return; // ya está sonando
        var e = Find(id);
        if (e == null || e.clip == null) return;

        var s = CreateSource(true);
        s.clip = e.clip;
        s.volume = e.volume;
        s.Play();
        loops[id] = s;
    }

    private void EndLoop(string id)
    {
        if (!loops.TryGetValue(id, out var s)) return;
        s.Stop();
        Destroy(s);
        loops.Remove(id);
    }
}

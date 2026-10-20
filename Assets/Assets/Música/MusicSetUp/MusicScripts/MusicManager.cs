using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

[Serializable]
public class MusicTrack
{
    public string id;
    [Tooltip("Opcional. Suena una sola vez antes del loop.")]
    public AudioClip intro;
    public AudioClip loop;
    [Range(0f, 1f)] public float volume = 1f;
}

/// <summary>
/// Gestiona la música del juego: loops, intros, crossfade y pausa.
/// Poner en un Empty (primera escena). Sobrevive entre escenas.
/// </summary>
public class MusicManager : MonoBehaviour
{
    public static MusicManager Instance { get; private set; }

    [Header("Audio")]
    [SerializeField] AudioMixerGroup musicMixerGroup;

    [Header("Tracks")]
    [SerializeField] List<MusicTrack> tracks = new List<MusicTrack>();
    [SerializeField] string startTrackId;
    [SerializeField] string pauseTrackId = "Pause";

    [Header("Transiciones")]
    [SerializeField] float defaultCrossfade = 1.5f;

    // ---------- Canal: un intro + un loop, con pausa/reanudar ----------
    class Channel
    {
        readonly AudioSource introSrc;
        readonly AudioSource loopSrc;
        double loopStartDsp;
        int introPos, loopPos;
        bool introWasPlaying;

        public MusicTrack Track { get; private set; }
        public bool IsPaused { get; private set; }
        public Coroutine FadeRoutine;
        float fade;
        public float Fade
        {
            get => fade;
            set
            {
                fade = value;
                if (Track == null) return;
                float v = Track.volume * fade;
                introSrc.volume = v;
                loopSrc.volume = v;
            }
        }

        public Channel(GameObject host, AudioMixerGroup group)
        {
            introSrc = Make(host, group, false);
            loopSrc = Make(host, group, true);
        }

        static AudioSource Make(GameObject host, AudioMixerGroup group, bool loop)
        {
            var s = host.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.loop = loop;
            s.outputAudioMixerGroup = group;
            s.spatialBlend = 0f;
            return s;
        }

        bool InIntro => Track != null && Track.intro != null && AudioSettings.dspTime < loopStartDsp;

        public void Play(MusicTrack track, float startFade)
        {
            Stop();
            Track = track;
            Fade = startFade;

            double start = AudioSettings.dspTime + 0.1;
            loopSrc.clip = track.loop;
            loopSrc.timeSamples = 0;

            if (track.intro != null)
            {
                introSrc.clip = track.intro;
                introSrc.timeSamples = 0;
                introSrc.PlayScheduled(start);
                // El loop arranca exactamente cuando termina el intro (sin hueco).
                loopStartDsp = start + (double)track.intro.samples / track.intro.frequency;
                loopSrc.PlayScheduled(loopStartDsp);
            }
            else
            {
                loopStartDsp = start;
                loopSrc.PlayScheduled(start);
            }
        }

        public void Pause()
        {
            if (Track == null || IsPaused) return;
            IsPaused = true;
            introWasPlaying = InIntro;
            if (introWasPlaying) introPos = introSrc.timeSamples;
            else loopPos = loopSrc.timeSamples;
            introSrc.Stop();
            loopSrc.Stop();
        }

        public void Resume()
        {
            if (Track == null || !IsPaused) return;
            IsPaused = false;

            if (introWasPlaying)
            {
                introSrc.timeSamples = introPos;
                introSrc.Play();
                double remaining = (double)(Track.intro.samples - introPos) / Track.intro.frequency;
                loopStartDsp = AudioSettings.dspTime + remaining;
                loopSrc.timeSamples = 0;
                loopSrc.PlayScheduled(loopStartDsp);
            }
            else
            {
                loopSrc.timeSamples = loopPos;
                loopSrc.Play();
            }
        }

        public void Stop()
        {
            introSrc.Stop();
            loopSrc.Stop();
            Track = null;
            IsPaused = false;
        }
    }

    Channel[] gameChannels;
    Channel pauseChannel;
    int activeIdx;
    bool isPaused;
    string pendingId;

    Channel Active => gameChannels[activeIdx];

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        gameChannels = new[]
        {
            new Channel(gameObject, musicMixerGroup),
            new Channel(gameObject, musicMixerGroup)
        };
        pauseChannel = new Channel(gameObject, musicMixerGroup);
    }

    void Start()
    {
        if (!string.IsNullOrEmpty(startTrackId)) Play(startTrackId, 0f);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    MusicTrack Find(string id) => tracks.Find(t => t.id == id);

    // ================= API pública =================

    /// <summary>Reproduce un track con crossfade. Si ya está sonando, no hace nada.</summary>
    public void Play(string id) => Play(id, defaultCrossfade);

    public void Play(string id, float crossfade)
    {
        if (isPaused) { pendingId = id; return; } // se aplica al reanudar

        var track = Find(id);
        if (track == null) { Debug.LogWarning($"[MusicManager] Track '{id}' no existe."); return; }
        if (Active.Track == track) return;

        var old = Active;
        activeIdx = 1 - activeIdx;
        var next = Active;

        if (next.FadeRoutine != null) StopCoroutine(next.FadeRoutine);
        next.Play(track, crossfade > 0f ? 0f : 1f);

        if (crossfade > 0f)
        {
            next.FadeRoutine = StartCoroutine(FadeTo(next, 1f, crossfade, false));
            if (old.Track != null)
            {
                if (old.FadeRoutine != null) StopCoroutine(old.FadeRoutine);
                old.FadeRoutine = StartCoroutine(FadeTo(old, 0f, crossfade, true));
            }
        }
        else if (old.Track != null)
        {
            old.Stop();
        }
    }

    public void StopMusic(float fadeOut = 1f)
    {
        var ch = Active;
        if (ch.Track == null) return;
        if (ch.FadeRoutine != null) StopCoroutine(ch.FadeRoutine);
        ch.FadeRoutine = StartCoroutine(FadeTo(ch, 0f, fadeOut, true));
    }

    /// <summary>Entrar al menú de pausa: congela la música del juego y suena el track de pausa.</summary>
    public void EnterPause()
    {
        if (isPaused) return;
        isPaused = true;

        // Limpia el canal que estaba saliendo en un crossfade, si lo hay.
        var other = gameChannels[1 - activeIdx];
        if (other.FadeRoutine != null) StopCoroutine(other.FadeRoutine);
        other.Stop();

        var cur = Active;
        if (cur.FadeRoutine != null) StopCoroutine(cur.FadeRoutine);
        if (cur.Track != null) { cur.Fade = 1f; cur.Pause(); }

        var pauseTrack = Find(pauseTrackId);
        if (pauseTrack != null) pauseChannel.Play(pauseTrack, 1f);
    }

    /// <summary>Salir de pausa: el track del juego continúa donde se quedó.</summary>
    public void ExitPause()
    {
        if (!isPaused) return;
        isPaused = false;
        pauseChannel.Stop();

        if (pendingId != null)
        {
            // Durante la pausa se pidió otro track: reemplaza al que estaba congelado.
            Active.Stop();
            string id = pendingId;
            pendingId = null;
            Play(id, 0f);
        }
        else
        {
            Active.Resume();
        }
    }

    // ================= Utilidades =================

    IEnumerator FadeTo(Channel ch, float target, float duration, bool stopAtEnd)
    {
        float from = ch.Fade;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime; // funciona con Time.timeScale = 0
            ch.Fade = Mathf.Lerp(from, target, t / duration);
            yield return null;
        }
        ch.Fade = target;
        if (stopAtEnd) ch.Stop();
        ch.FadeRoutine = null;
    }
}

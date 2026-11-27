using System;
using System.Collections.Generic;
using Combat;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Background music + every sound effect goes through here, so the two Settings sliders (Music, Effects) control all of it.
    /// Put one on the GameController prefab (Tools > Kaiju > Audio > Set Up Audio does it). If none exists the first sound effect
    /// makes a silent stand-in, so effects always work.
    /// Slider values are saved on the phone (PlayerPrefs). A slider position is turned into loudness with a square curve
    /// (50% on the slider is a quarter of the power), which sounds like an even change to the ear.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameAudio : MonoBehaviour
    {
        const string MusicKey = "kaiju.musicVolume";
        const string EffectsKey = "kaiju.effectsVolume";

        [Header("Music")]
        [Tooltip("Loops forever. Use the seamless GameTest_loop.ogg.")]
        public AudioClip musicClip;
        [Tooltip("How loud the music is with the Music slider all the way up. Keep it lowish so effects are heard.")]
        [Range(0f, 1f)] public float musicLoudness = 0.5f;
        [Tooltip("Seconds the music takes to fade in when the game starts.")]
        public float fadeInSeconds = 2f;
        public bool playMusic = true;

        [Header("Slider start values (first launch only)")]
        [Range(0f, 1f)] public float defaultMusicSlider = 0.7f;
        [Range(0f, 1f)] public float defaultEffectsSlider = 0.8f;

        [Header("Effects")]
        [Tooltip("Played when you let go of the Effects slider, so you can hear the level.")]
        public AudioClip effectsPreview;
        [Range(0f, 1f)] public float previewVolume = 0.8f;
        [Range(2, 32)] public int effectVoices = 12;

        // ------------------------------------------------------------------ static API
        static GameAudio _inst;
        static bool _creating;
        static float _music = -1f, _fx = -1f;

        /// <summary>Raised whenever a slider changes.</summary>
        public static event Action VolumesChanged;

        /// <summary>0..1 slider position.</summary>
        public static float MusicSlider
        {
            get { if (_music < 0f) _music = Load(MusicKey, _inst != null ? _inst.defaultMusicSlider : 0.7f); return _music; }
        }
        public static float EffectsSlider
        {
            get { if (_fx < 0f) _fx = Load(EffectsKey, _inst != null ? _inst.defaultEffectsSlider : 0.8f); return _fx; }
        }

        /// <summary>Loudness multiplier for sound effects (1 = full). Use this for any AudioSource you play yourself.</summary>
        public static float EffectsGain { get { float s = EffectsSlider; return s * s; } }
        public static float MusicGain { get { float s = MusicSlider; return s * s; } }

        public static void SetMusicSlider(float v)
        {
            _music = Mathf.Clamp01(v);
            PlayerPrefs.SetFloat(MusicKey, _music);
            if (VolumesChanged != null) VolumesChanged();
        }

        public static void SetEffectsSlider(float v)
        {
            _fx = Mathf.Clamp01(v);
            PlayerPrefs.SetFloat(EffectsKey, _fx);
            if (VolumesChanged != null) VolumesChanged();
        }

        public static void SaveNow() { PlayerPrefs.Save(); }

        static float Load(string key, float fallback)
        {
            return PlayerPrefs.HasKey(key) ? Mathf.Clamp01(PlayerPrefs.GetFloat(key)) : fallback;
        }

        /// <summary>Plays a sound effect (2D). volume 0..1 is multiplied by the Effects slider.</summary>
        public static void PlayEffect(AudioClip clip, float volume = 1f, float pitch = 1f, float delaySeconds = 0f)
        {
            if (clip == null || !Application.isPlaying) return;
            if (volume * EffectsGain <= 0.0005f) return;
            Instance().PlayInternal(clip, volume, pitch, delaySeconds);
        }

        /// <summary>
        /// A voice line. Normal voices (and styles without effects) go through the normal pool; Undead / Robot voices go through their own
        /// small pool whose sources carry the sound filters, set from the VoiceLook every time (so edits in Play mode are heard).
        /// </summary>
        public static void PlayVoice(AudioClip clip, float volume, float pitch, float delaySeconds, VoiceStyle style, VoiceLook look)
        {
            if (clip == null || !Application.isPlaying) return;
            if (volume * EffectsGain <= 0.0005f) return;
            var inst = Instance();
            if (look == null || !look.UsesEffects) inst.PlayInternal(clip, volume, pitch, delaySeconds);
            else inst.PlayStyled(clip, volume, pitch, delaySeconds, style, look);
        }

        /// <summary>The short sample played when the Effects slider is let go.</summary>
        public static void PlayPreview()
        {
            if (_inst != null && _inst.effectsPreview != null) PlayEffect(_inst.effectsPreview, _inst.previewVolume);
        }

        static GameAudio Instance()
        {
            if (_inst != null) return _inst;
            _creating = true;
            var go = new GameObject("GameAudio (auto)");
            DontDestroyOnLoad(go);
            go.AddComponent<GameAudio>();   // Awake sets _inst
            _creating = false;
            return _inst;
        }

        // ------------------------------------------------------------------ instance
        bool _isAuto;
        AudioSource _musicSource;
        AudioSource[] _pool;
        float[] _busyUntil;
        float _fade;

        void Awake()
        {
            if (_inst != null && _inst != this)
            {
                if (_inst._isAuto) Destroy(_inst.gameObject);   // a real one (on your prefab) replaces the stand-in
                else { Destroy(this); return; }
            }
            _inst = this;
            _isAuto = _creating;
            if (_isAuto) playMusic = false;

            var fx = new GameObject("Effects");
            fx.transform.SetParent(transform, false);
            _pool = new AudioSource[Mathf.Max(2, effectVoices)];
            _busyUntil = new float[_pool.Length];
            for (int i = 0; i < _pool.Length; i++)
            {
                var s = fx.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.spatialBlend = 0f;   // plain 2D, not tied to the tiny arena
                _pool[i] = s;
            }

            _musicSource = gameObject.AddComponent<AudioSource>();
            _musicSource.playOnAwake = false;
            _musicSource.loop = true;
            _musicSource.spatialBlend = 0f;
        }

        void Start()
        {
            if (_inst != this || !playMusic || musicClip == null) return;
            _musicSource.clip = musicClip;
            _musicSource.volume = 0f;
            _fade = fadeInSeconds <= 0.01f ? 1f : 0f;
            _musicSource.Play();
        }

        void Update()
        {
            if (_inst != this || _musicSource == null) return;
            if (_fade < 1f) _fade = Mathf.MoveTowards(_fade, 1f, Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeInSeconds));
            _musicSource.volume = musicLoudness * MusicGain * _fade;
        }

        void OnDestroy()
        {
            if (_inst == this) _inst = null;
        }

        // Sources with sound filters, one small pool per voice style (made the first time that style is heard).
        sealed class StyledSource
        {
            public AudioSource source;
            public AudioLowPassFilter lowPass;
            public AudioDistortionFilter distortion;
            public AudioEchoFilter echo;
            public AudioChorusFilter chorus;
            public AudioReverbFilter reverb;
            public float busyUntil;
        }

        readonly Dictionary<VoiceStyle, StyledSource[]> _styled = new Dictionary<VoiceStyle, StyledSource[]>();
        const int StyledVoices = 3;

        StyledSource[] StyledPool(VoiceStyle style)
        {
            StyledSource[] pool;
            if (_styled.TryGetValue(style, out pool)) return pool;

            pool = new StyledSource[StyledVoices];
            for (int i = 0; i < pool.Length; i++)
            {
                var go = new GameObject("Voice " + style + " " + i);
                go.transform.SetParent(transform, false);
                var q = new StyledSource();
                q.source = go.AddComponent<AudioSource>();
                q.source.playOnAwake = false;
                q.source.spatialBlend = 0f;
                q.lowPass = go.AddComponent<AudioLowPassFilter>();
                q.distortion = go.AddComponent<AudioDistortionFilter>();
                q.echo = go.AddComponent<AudioEchoFilter>();
                q.chorus = go.AddComponent<AudioChorusFilter>();
                q.reverb = go.AddComponent<AudioReverbFilter>();
                pool[i] = q;
            }
            _styled[style] = pool;
            return pool;
        }

        void PlayStyled(AudioClip clip, float volume, float pitch, float delay, VoiceStyle style, VoiceLook look)
        {
            var pool = StyledPool(style);
            float now = Time.unscaledTime;
            StyledSource q = null;
            foreach (var p in pool) if (p.busyUntil <= now) { q = p; break; }
            if (q == null) { q = pool[0]; foreach (var p in pool) if (p.busyUntil < q.busyUntil) q = p; }   // all busy: take the one finishing first

            q.lowPass.enabled = look.lowPass;
            if (look.lowPass) q.lowPass.cutoffFrequency = look.lowPassHz;
            q.distortion.enabled = look.distortion;
            if (look.distortion) q.distortion.distortionLevel = look.distortionLevel;
            q.echo.enabled = look.echo;
            if (look.echo) { q.echo.delay = look.echoDelayMs; q.echo.decayRatio = look.echoDecay; q.echo.wetMix = look.echoWet; q.echo.dryMix = 1f; }
            q.chorus.enabled = look.chorus;
            if (look.chorus) { q.chorus.depth = look.chorusDepth; q.chorus.rate = look.chorusRate; q.chorus.dryMix = 0.6f; q.chorus.wetMix1 = 0.4f; q.chorus.wetMix2 = 0.4f; q.chorus.wetMix3 = 0f; }
            q.reverb.enabled = look.reverb;
            if (look.reverb) q.reverb.reverbPreset = look.reverbPreset;

            var s = q.source;
            s.Stop();
            s.clip = clip;
            s.volume = Mathf.Clamp01(volume * EffectsGain);
            s.pitch = pitch;
            // leave room after the clip for echo / reverb tails
            q.busyUntil = now + Mathf.Max(0f, delay) + clip.length / Mathf.Max(0.1f, Mathf.Abs(pitch)) + 0.6f;
            if (delay > 0.005f) s.PlayDelayed(delay); else s.Play();
        }

        void PlayInternal(AudioClip clip, float volume, float pitch, float delay)
        {
            float now = Time.unscaledTime;
            int idx = -1;
            for (int i = 0; i < _pool.Length; i++)
                if (_busyUntil[i] <= now) { idx = i; break; }
            if (idx < 0)
            {
                // all voices busy: take the one that finishes first
                float best = float.MaxValue;
                for (int i = 0; i < _pool.Length; i++)
                    if (_busyUntil[i] < best) { best = _busyUntil[i]; idx = i; }
            }

            var s = _pool[idx];
            s.Stop();
            s.clip = clip;
            s.volume = Mathf.Clamp01(volume * EffectsGain);
            s.pitch = pitch;
            _busyUntil[idx] = now + Mathf.Max(0f, delay) + clip.length / Mathf.Max(0.1f, Mathf.Abs(pitch)) + 0.05f;
            if (delay > 0.005f) s.PlayDelayed(delay); else s.Play();
        }
    }
}

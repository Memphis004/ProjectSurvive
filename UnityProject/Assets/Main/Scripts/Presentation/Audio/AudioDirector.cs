using System;
using ProjectSurvive.Game.Presentation.Web;
using R3;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;

namespace ProjectSurvive.Game.Presentation.Audio
{
    /// <summary>
    /// The game's only audio output: a fixed pool of voices, one music source, and one report a
    /// browser test can read (Stage 13c-1).
    /// </summary>
    /// <remarks>
    /// <para>
    /// It lives on the <b>App</b> scope's root (created by the Boot scene and persisted), so the
    /// music survives a scene change instead of restarting every crossing, and so there is exactly
    /// one <see cref="AudioListener"/> in the whole game — a listener per scene is how a project
    /// ends up with Unity's "2 audio listeners in the scene" warning on every load.
    /// </para>
    /// <para>
    /// Voices are preallocated at <c>Awake</c> and reused round-robin: a hit is an <c>int++</c> and
    /// a <c>Play</c>, never an <c>Instantiate</c> or a <c>PlayClipAtPoint</c> (AGENTS rule 8). When
    /// every voice is busy the oldest is taken and the takeover is counted, so "the mix clipped a
    /// cue" is a fact in the report rather than a guess.
    /// </para>
    /// <para>
    /// Levels: the master volume is <see cref="AudioListener.volume"/>, written by
    /// <c>SettingsPresenter</c> and by nothing here. This class only applies its own music and
    /// effects levels, so the master is not applied twice (which would square it).
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class AudioDirector : MonoBehaviour
    {
        /// <summary>Page global carrying the audio report.</summary>
        public const string ReportGlobal = WebGlBridge.AudioGlobal;

        [Tooltip("The App scope, for the UI cue channel. Authored by the Boot scene builder.")]
        [SerializeField] private RootLifetimeScope _scope;

        [Tooltip("One clip per AudioCue, in AudioCue order. Assigned by the Boot scene builder.")]
        [SerializeField] private AudioClip[] _clips = Array.Empty<AudioClip>();

        [Tooltip("Simultaneous effect voices. Placeholder sounds are short; eight is plenty.")]
        [SerializeField, Range(1, 32)] private int _voices = 8;

        [Tooltip("Effects level (the master volume is AudioListener.volume, applied once).")]
        [SerializeField, Range(0f, 1f)] private float _sfxVolume = 1f;

        [Tooltip("Music level, relative to the master volume.")]
        [SerializeField, Range(0f, 1f)] private float _musicVolume = 0.55f;

        [Tooltip(
            "Minimum seconds between two plays of the same cue. A floor-1 wave lands dozens of " +
            "hits a second; without this the same clip phases into a buzz.")]
        [SerializeField] private float _minRetriggerSeconds = 0.035f;

        [Tooltip("Publish __projectSurviveAudio. Only a browser test reads it.")]
        [SerializeField] private bool _publishReport = true;

        [Tooltip("Seconds between report publishes while cues are still arriving.")]
        [SerializeField] private float _reportIntervalSeconds = 0.25f;

        private AudioSource[] _voiceSources;
        private AudioSource _music;
        private readonly int[] _counts = new int[AudioCueTable.Count];
        private readonly float[] _lastPlayTime = new float[AudioCueTable.Count];
        private AudioCue? _musicCue;
        private UiState _ui;
        private IDisposable _cueSubscription;
        private long _totalPlays;
        private long _stolen;
        private long _throttled;
        private long _missing;
        private float _reportTimer;
        private bool _dirty;

        /// <summary>The live director, or null in a scene that has none (a test, a direct load).</summary>
        public static AudioDirector Instance { get; private set; }

        /// <summary>The last report published, for PlayMode tests and diagnostics.</summary>
        public static string LastReportJson { get; private set; }

        /// <summary>Voices the pool owns (constant after <c>Awake</c>).</summary>
        public int VoiceCount => _voiceSources?.Length ?? 0;

        /// <summary>Plays of one cue since the app started.</summary>
        public int PlayCount(AudioCue cue) => _counts[(int)cue];

        /// <summary>Every play, including ones that were throttled or stolen.</summary>
        public long TotalPlays => _totalPlays;

        /// <summary>Plays whose voice was still busy (the pool never grows).</summary>
        public long Stolen => _stolen;

        /// <summary>Plays dropped because the same cue had just played.</summary>
        public long Throttled => _throttled;

        /// <summary>Plays dropped because the cue has no clip assigned.</summary>
        public long MissingClips => _missing;

        /// <summary>The music cue currently owned by the music source, if any.</summary>
        public AudioCue? MusicCue => _musicCue;

        /// <summary>True while the music source is playing.</summary>
        public bool IsMusicPlaying => _music is not null && _music.isPlaying;

        private void Awake()
        {
            if (Instance is not null && Instance != this)
            {
                // A second Boot (a re-entered PlayMode session) must not fight the first for
                // the audio device or the listener.
                Destroy(gameObject);
                return;
            }

            Instance = this;

            // Always on this object, never "somebody in the scene has one": this object is the one that
            // persists (it is a child of the persisted app root), so a listener found *in a scene* would
            // be unloaded with that scene and every playing source would then log "no audio listeners"
            // once per frame. No authored scene carries a listener (Stage 13c-1), so there is still
            // exactly one in the game.
            if (GetComponent<AudioListener>() is null)
            {
                gameObject.AddComponent<AudioListener>();
            }

            CreateVoices();

            if (_clips.Length > 0)
            {
                try
                {
                    AudioCueTable.ValidateClipCount(_clips.Length);
                }
                catch (InvalidOperationException exception)
                {
                    // A mis-authored array is a build defect, not a crash: the game still runs
                    // silent for the cues it cannot index.
                    Debug.LogError(exception.Message, this);
                }

                for (int i = 0; i < _clips.Length && i < _counts.Length; i++)
                {
                    if (_clips[i] is null)
                    {
                        Debug.LogWarning(
                            "AudioDirector has no clip for cue " + (AudioCue)i + " ('"
                            + AudioCueTable.Names[i] + "'); that cue will be silent.", this);
                    }
                }
            }

            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void Start()
        {
            if (_scope is null || _scope.Container is null)
            {
                return;
            }

            _ui = _scope.Container.Resolve<UiState>();
            // `Subscribe(Action<T>)` is R3's extension method, so the R3 namespace has to be in
            // scope: without it the only visible member is Subscribe(Observer<T>), which a
            // lambda cannot satisfy (Stage 13c-1).
            _cueSubscription = _ui.Cues.Subscribe(cue => Play(cue));
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            _cueSubscription?.Dispose();
            _cueSubscription = null;
            _ui = null;

            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            if (!_dirty)
            {
                return;
            }

            _reportTimer -= Time.unscaledDeltaTime;
            if (_reportTimer > 0f)
            {
                return;
            }

            _dirty = false;
            _reportTimer = _reportIntervalSeconds;

            if (_publishReport)
            {
                Publish();
            }
        }

        /// <summary>Plays one cue on a pooled voice. Safe to call with no clip assigned.</summary>
        public bool Play(AudioCue cue)
        {
            if (AudioCueTable.IsMusic(cue))
            {
                return PlayMusic(cue);
            }

            if (_voiceSources is null || _voiceSources.Length == 0)
            {
                return false;
            }

            float now = Time.unscaledTime;
            int index = (int)cue;
            if (now - _lastPlayTime[index] < _minRetriggerSeconds)
            {
                _throttled++;
                return false;
            }

            AudioClip clip = Clip(cue);
            if (clip is null)
            {
                _missing++;
                return false;
            }

            _lastPlayTime[index] = now;
            _counts[index]++;
            _totalPlays++;

            AudioSource voice = NextVoice();
            if (voice.isPlaying)
            {
                _stolen++;
            }

            voice.Stop();
            voice.clip = clip;
            voice.volume = _sfxVolume;
            voice.loop = false;
            voice.Play();

            _dirty = true;
            return true;
        }

        /// <summary>
        /// Switches the music. Asking for the cue that is already playing does nothing, so a
        /// scene that is re-entered does not restart the track.
        /// </summary>
        public bool PlayMusic(AudioCue cue)
        {
            if (_music is null)
            {
                return false;
            }

            if (_musicCue == cue && _music.isPlaying)
            {
                return false;
            }

            AudioClip clip = Clip(cue);
            if (clip is null)
            {
                _missing++;
                return false;
            }

            _musicCue = cue;
            _counts[(int)cue]++;
            _totalPlays++;

            _music.Stop();
            _music.clip = clip;
            _music.loop = true;
            _music.volume = _musicVolume;
            _music.Play();

            _dirty = true;
            return true;
        }

        /// <summary>Stops the music (the Boot scene, before a player gesture exists).</summary>
        public void StopMusic()
        {
            _musicCue = null;
            if (_music is not null)
            {
                _music.Stop();
            }

            _dirty = true;
        }

        /// <summary>Publishes the report now instead of waiting for the next quiet frame.</summary>
        public string Publish()
        {
            var writer = new JsonReportWriter();
            writer.BeginObject()
                .Write("voices", VoiceCount)
                .Write("totalPlays", _totalPlays)
                .Write("stolen", _stolen)
                .Write("throttled", _throttled)
                .Write("missingClips", _missing)
                .Write("musicPlaying", IsMusicPlaying)
                .Write("musicCue", _musicCue is null ? string.Empty : _musicCue.Value.ToString())
                .Write("sfxVolume", _sfxVolume)
                .Write("musicVolume", _musicVolume);

            writer.BeginObjectProperty("cueCounts");
            for (int i = 0; i < _counts.Length; i++)
            {
                if (_counts[i] > 0)
                {
                    writer.Write(AudioCueTable.Names[i], _counts[i]);
                }
            }

            writer.EndObject();
            writer.EndObject();

            LastReportJson = writer.ToString();
            WebGlBridge.Publish(ReportGlobal, LastReportJson);
            return LastReportJson;
        }

        private AudioClip Clip(AudioCue cue)
        {
            int index = (int)cue;
            return _clips is not null && index >= 0 && index < _clips.Length ? _clips[index] : null;
        }

        private AudioSource NextVoice()
        {
            // Round-robin: one index for the pool, so a burst cannot starve the same voice twice
            // in a row. Only when every voice is busy does this take a playing one.
            _next = (_next + 1) % _voiceSources.Length;
            return _voiceSources[_next];
        }

        private int _next = -1;

        private void CreateVoices()
        {
            int count = Mathf.Max(1, _voices);
            _voiceSources = new AudioSource[count];

            for (int i = 0; i < count; i++)
            {
                var voiceObject = new GameObject("voice-" + i);
                voiceObject.transform.SetParent(transform, worldPositionStays: false);

                AudioSource source = voiceObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = false;
                source.spatialBlend = 0f;          // 2D: a placeholder cue is a UI sound.
                source.volume = _sfxVolume;
                _voiceSources[i] = source;
            }

            var musicObject = new GameObject("music");
            musicObject.transform.SetParent(transform, worldPositionStays: false);

            _music = musicObject.AddComponent<AudioSource>();
            _music.playOnAwake = false;
            _music.loop = true;
            _music.spatialBlend = 0f;
            _music.volume = _musicVolume;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            switch (scene.name)
            {
                case "Battle":
                    PlayMusic(AudioCue.MusicBattle);
                    break;
                case "Title":
                case "Camp":
                case "Result":
                    PlayMusic(AudioCue.MusicCamp);
                    break;
                default:
                    // Boot, or a spike scene: silence is the honest default, and it is also the
                    // one scene where the browser has not seen a gesture yet (WebGL autoplay).
                    StopMusic();
                    break;
            }
        }
    }
}

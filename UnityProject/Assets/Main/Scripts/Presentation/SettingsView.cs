using System;
using ProjectSurvive.Game.Presentation.Camp;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace ProjectSurvive.Game.Presentation
{
    /// <summary>
    /// The title screen's settings overlay (Stage 12). A view: it mirrors strings from
    /// <see cref="UiState"/> and forwards presses to <see cref="SettingsPresenter"/>; it computes
    /// no value and decides no language rule.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SettingsView : MonoBehaviour
    {
        /// <summary>The panel's controls, by the name the report gives them (Stage 13c-2a).</summary>
        public enum SettingsButton
        {
            Open,
            Close,
            VolumeDown,
            VolumeUp,
            MusicDown,
            MusicUp,
            SfxDown,
            SfxUp,
            Language,
            Speed,
        }

        [SerializeField] private RootLifetimeScope _scope;
        [SerializeField] private GameObject _panel;
        [SerializeField] private TMP_Text _titleLabel;
        [SerializeField] private TMP_Text _volumeLabel;
        [SerializeField] private TMP_Text _musicLabel;
        [SerializeField] private TMP_Text _sfxLabel;
        [SerializeField] private TMP_Text _languageLabel;
        [SerializeField] private TMP_Text _speedLabel;
        [SerializeField] private Button _openButton;
        [SerializeField] private Button _closeButton;
        [SerializeField] private Button _volumeDownButton;
        [SerializeField] private Button _volumeUpButton;
        [SerializeField] private Button _musicDownButton;
        [SerializeField] private Button _musicUpButton;
        [SerializeField] private Button _sfxDownButton;
        [SerializeField] private Button _sfxUpButton;
        [SerializeField] private Button _languageButton;
        [SerializeField] private Button _speedButton;

        private UiState _state;
        private SettingsPresenter _presenter;
        private IDisposable _titleSubscription;
        private IDisposable _volumeSubscription;
        private IDisposable _musicSubscription;
        private IDisposable _sfxSubscription;
        private IDisposable _languageSubscription;
        private IDisposable _speedSubscription;
        private IDisposable _openSubscription;
        private IDisposable _closeSubscription;
        private IDisposable _decreaseSubscription;
        private IDisposable _increaseSubscription;

        /// <summary>True once the overlay has a presenter (a test may wait for it).</summary>
        public bool Bound => _presenter is not null;

        /// <summary>
        /// The panel's report, refreshed whenever an open/close or a value changes. The title screen
        /// owns it rather than the director because the panel owns its own controls and state.
        /// </summary>
        public void PublishReport()
        {
            if (_state is not null)
            {
                SettingsReport.Publish(_state, this, IsOpen);
            }
        }

        /// <summary>
        /// Where a control is on screen right now, in screen pixels with the origin at the
        /// bottom-left, or false when it is not drawn (the panel is closed).
        /// </summary>
        public bool TryGetButtonScreenPoint(SettingsButton button, out Vector2 screen)
        {
            Button target = button switch
            {
                SettingsButton.Open => _openButton,
                SettingsButton.Close => _closeButton,
                SettingsButton.VolumeDown => _volumeDownButton,
                SettingsButton.VolumeUp => _volumeUpButton,
                SettingsButton.MusicDown => _musicDownButton,
                SettingsButton.MusicUp => _musicUpButton,
                SettingsButton.SfxDown => _sfxDownButton,
                SettingsButton.SfxUp => _sfxUpButton,
                SettingsButton.Language => _languageButton,
                SettingsButton.Speed => _speedButton,
                _ => null,
            };

            // A row inside a closed panel is not pressable, so it is not published: the report's
            // buttons are where a click would land on this screen as it is drawn.
            if (target is null || !target.isActiveAndEnabled || !target.gameObject.activeInHierarchy)
            {
                screen = default;
                return false;
            }

            return CampScreenRects.TryCentre(target.transform as RectTransform, out screen);
        }

        /// <summary>True while the panel is drawn.</summary>
        public bool IsOpen => _panel is not null && _panel.activeSelf;

        private void Start() => Bind();

        private void OnDestroy()
        {
            _titleSubscription?.Dispose();
            _volumeSubscription?.Dispose();
            _musicSubscription?.Dispose();
            _sfxSubscription?.Dispose();
            _languageSubscription?.Dispose();
            _speedSubscription?.Dispose();
            _openSubscription?.Dispose();
            _closeSubscription?.Dispose();
            _decreaseSubscription?.Dispose();
            _increaseSubscription?.Dispose();
            _titleSubscription = null;
            _volumeSubscription = null;
            _musicSubscription = null;
            _sfxSubscription = null;
            _languageSubscription = null;
            _speedSubscription = null;
            _openSubscription = null;
            _closeSubscription = null;
            _decreaseSubscription = null;
            _increaseSubscription = null;

            Remove(_openButton, Open);
            Remove(_closeButton, Close);
            Remove(_volumeDownButton, VolumeDown);
            Remove(_volumeUpButton, VolumeUp);
            Remove(_musicDownButton, MusicDown);
            Remove(_musicUpButton, MusicUp);
            Remove(_sfxDownButton, SfxDown);
            Remove(_sfxUpButton, SfxUp);
            Remove(_languageButton, ToggleLanguage);
            Remove(_speedButton, ToggleSpeed);

            _state = null;
            _presenter = null;
        }

        /// <summary>Connects to the App scope and pushes the current values. Public for tests.</summary>
        public void Bind()
        {
            if (_state is not null)
            {
                return;
            }

            if (_scope is null || _scope.Container is null)
            {
                Debug.LogError("SettingsView needs a built RootLifetimeScope.", this);
                return;
            }

            _state = _scope.Container.Resolve<UiState>();
            _presenter = _scope.Container.Resolve<SettingsPresenter>();
            _presenter.Refresh();

            _titleSubscription = _state.SettingsTitle.Subscribe(value => Assign(_titleLabel, value));
            _volumeSubscription = _state.VolumeLabel.Subscribe(value =>
            {
                Assign(_volumeLabel, value);
                PublishReport();
            });
            _musicSubscription = _state.MusicLabel.Subscribe(value =>
            {
                Assign(_musicLabel, value);
                PublishReport();
            });
            _sfxSubscription = _state.SfxLabel.Subscribe(value =>
            {
                Assign(_sfxLabel, value);
                PublishReport();
            });
            _languageSubscription = _state.LanguageLabel.Subscribe(value => Assign(_languageLabel, value));
            _speedSubscription = _state.SpeedLabel.Subscribe(value => Assign(_speedLabel, value));
            _openSubscription = _state.SettingsOpenLabel.Subscribe(value => AssignButton(_openButton, value));
            _closeSubscription = _state.SettingsCloseLabel.Subscribe(value => AssignButton(_closeButton, value));

            // One caption for every "−" and one for every "+", so the three volume rows cannot
            // disagree about what their controls say.
            _decreaseSubscription = _state.DecreaseLabel.Subscribe(value =>
            {
                AssignButton(_volumeDownButton, value);
                AssignButton(_musicDownButton, value);
                AssignButton(_sfxDownButton, value);
            });
            _increaseSubscription = _state.IncreaseLabel.Subscribe(value =>
            {
                AssignButton(_volumeUpButton, value);
                AssignButton(_musicUpButton, value);
                AssignButton(_sfxUpButton, value);
            });

            Add(_openButton, Open);
            Add(_closeButton, Close);
            Add(_volumeDownButton, VolumeDown);
            Add(_volumeUpButton, VolumeUp);
            Add(_musicDownButton, MusicDown);
            Add(_musicUpButton, MusicUp);
            Add(_sfxDownButton, SfxDown);
            Add(_sfxUpButton, SfxUp);
            Add(_languageButton, ToggleLanguage);
            Add(_speedButton, ToggleSpeed);

            if (_panel is not null)
            {
                _panel.SetActive(false);
            }

            PublishReport();
        }

        /// <summary>Opens the panel. Public so a test can drive it without a pointer.</summary>
        public void Open()
        {
            if (_panel is not null)
            {
                _panel.SetActive(true);
            }

            PublishReport();
        }

        /// <summary>Closes the panel.</summary>
        public void Close()
        {
            if (_panel is not null)
            {
                _panel.SetActive(false);
            }

            PublishReport();
        }

        /// <summary>Forwards one press; public for tests.</summary>
        public void VolumeDown() => _presenter?.AdjustVolume(-0.1f);

        /// <summary>Forwards one press; public for tests.</summary>
        public void VolumeUp() => _presenter?.AdjustVolume(0.1f);

        /// <summary>Forwards one press to the music bus (Stage 13c-2a); public for tests.</summary>
        public void MusicDown() => _presenter?.AdjustMusic(-0.1f);

        /// <summary>Forwards one press to the music bus; public for tests.</summary>
        public void MusicUp() => _presenter?.AdjustMusic(0.1f);

        /// <summary>Forwards one press to the effects bus; public for tests.</summary>
        public void SfxDown() => _presenter?.AdjustSfx(-0.1f);

        /// <summary>Forwards one press to the effects bus; public for tests.</summary>
        public void SfxUp() => _presenter?.AdjustSfx(0.1f);

        /// <summary>Forwards one press; public for tests.</summary>
        public void ToggleLanguage() => _presenter?.ToggleLanguage();

        /// <summary>Forwards one press; public for tests.</summary>
        public void ToggleSpeed() => _presenter?.ToggleSpeed();

        private static void Assign(TMP_Text label, string value)
        {
            if (label is not null)
            {
                label.text = value;
            }
        }

        private static void AssignButton(Button button, string value)
        {
            if (button is not null && button.GetComponentInChildren<TMP_Text>() is { } label)
            {
                label.text = value;
            }
        }

        private static void Add(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button is not null)
            {
                button.onClick.AddListener(action);
            }
        }

        private static void Remove(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button is not null)
            {
                button.onClick.RemoveListener(action);
            }
        }
    }
}

using System;
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
        [SerializeField] private RootLifetimeScope _scope;
        [SerializeField] private GameObject _panel;
        [SerializeField] private TMP_Text _titleLabel;
        [SerializeField] private TMP_Text _volumeLabel;
        [SerializeField] private TMP_Text _languageLabel;
        [SerializeField] private TMP_Text _speedLabel;
        [SerializeField] private Button _openButton;
        [SerializeField] private Button _closeButton;
        [SerializeField] private Button _volumeDownButton;
        [SerializeField] private Button _volumeUpButton;
        [SerializeField] private Button _languageButton;
        [SerializeField] private Button _speedButton;

        private UiState _state;
        private SettingsPresenter _presenter;
        private IDisposable _titleSubscription;
        private IDisposable _volumeSubscription;
        private IDisposable _languageSubscription;
        private IDisposable _speedSubscription;
        private IDisposable _openSubscription;
        private IDisposable _closeSubscription;

        /// <summary>True once the overlay has a presenter (a test may wait for it).</summary>
        public bool Bound => _presenter is not null;

        /// <summary>True while the panel is drawn.</summary>
        public bool IsOpen => _panel is not null && _panel.activeSelf;

        private void Start() => Bind();

        private void OnDestroy()
        {
            _titleSubscription?.Dispose();
            _volumeSubscription?.Dispose();
            _languageSubscription?.Dispose();
            _speedSubscription?.Dispose();
            _openSubscription?.Dispose();
            _closeSubscription?.Dispose();
            _titleSubscription = null;
            _volumeSubscription = null;
            _languageSubscription = null;
            _speedSubscription = null;
            _openSubscription = null;
            _closeSubscription = null;

            Remove(_openButton, Open);
            Remove(_closeButton, Close);
            Remove(_volumeDownButton, VolumeDown);
            Remove(_volumeUpButton, VolumeUp);
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
            _volumeSubscription = _state.VolumeLabel.Subscribe(value => Assign(_volumeLabel, value));
            _languageSubscription = _state.LanguageLabel.Subscribe(value => Assign(_languageLabel, value));
            _speedSubscription = _state.SpeedLabel.Subscribe(value => Assign(_speedLabel, value));
            _openSubscription = _state.SettingsOpenLabel.Subscribe(value => AssignButton(_openButton, value));
            _closeSubscription = _state.SettingsCloseLabel.Subscribe(value => AssignButton(_closeButton, value));

            Add(_openButton, Open);
            Add(_closeButton, Close);
            Add(_volumeDownButton, VolumeDown);
            Add(_volumeUpButton, VolumeUp);
            Add(_languageButton, ToggleLanguage);
            Add(_speedButton, ToggleSpeed);

            if (_panel is not null)
            {
                _panel.SetActive(false);
            }
        }

        /// <summary>Opens the panel. Public so a test can drive it without a pointer.</summary>
        public void Open()
        {
            if (_panel is not null)
            {
                _panel.SetActive(true);
            }
        }

        /// <summary>Closes the panel.</summary>
        public void Close()
        {
            if (_panel is not null)
            {
                _panel.SetActive(false);
            }
        }

        /// <summary>Forwards one press; public for tests.</summary>
        public void VolumeDown() => _presenter?.AdjustVolume(-0.1f);

        /// <summary>Forwards one press; public for tests.</summary>
        public void VolumeUp() => _presenter?.AdjustVolume(0.1f);

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

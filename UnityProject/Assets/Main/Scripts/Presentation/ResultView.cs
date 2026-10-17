using System;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace ProjectSurvive.Game.Presentation
{
    /// <summary>
    /// The Result screen's view: it mirrors five strings into five labels and forwards two button
    /// presses. Nothing else.
    /// </summary>
    /// <remarks>
    /// <para>
    /// AGENTS rule 10: a View has no logic and computes no numbers. Every string here was already
    /// decided by <see cref="ResultPresenter"/> — including the button captions, which are
    /// player-facing text like any other.
    /// </para>
    /// <para>
    /// The two presses become <see cref="ResultPresenter.RequestRestart"/> and
    /// <see cref="ResultPresenter.RequestTitle"/>, which only record the intent; the scene change
    /// belongs to the director. Subscriptions are disposed and listeners removed in
    /// <c>OnDestroy</c>, because this scene is loaded once per finished run.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class ResultView : MonoBehaviour
    {
        [SerializeField] private RootLifetimeScope _scope;
        [SerializeField] private TMP_Text _headlineLabel;
        [SerializeField] private TMP_Text _summaryLabel;
        [SerializeField] private TMP_Text _seedLabel;
        [SerializeField] private Button _restartButton;
        [SerializeField] private Button _titleButton;
        [SerializeField] private TMP_Text _restartLabel;
        [SerializeField] private TMP_Text _titleLabel;

        private ResultUiState _state;
        private ResultPresenter _presenter;
        private IDisposable _headline;
        private IDisposable _summary;
        private IDisposable _seed;
        private IDisposable _restartCaption;
        private IDisposable _titleCaption;

        /// <summary>The screen's two controls, named for the page's layout report.</summary>
        public enum ResultButton
        {
            /// <summary>The control that starts a new run.</summary>
            Restart = 0,

            /// <summary>The control that leaves for the title.</summary>
            BackToTitle = 1,
        }

        /// <summary>True once the view has a state to read (a test may wait for it).</summary>
        public bool Bound => _state is not null;

        /// <summary>The logic this view drives; null until <see cref="Bind"/>.</summary>
        public ResultPresenter Presenter => _presenter;

        private void Start() => Bind();

        private void OnDestroy()
        {
            _headline?.Dispose();
            _summary?.Dispose();
            _seed?.Dispose();
            _restartCaption?.Dispose();
            _titleCaption?.Dispose();
            _headline = null;
            _summary = null;
            _seed = null;
            _restartCaption = null;
            _titleCaption = null;

            if (_restartButton is not null)
            {
                _restartButton.onClick.RemoveListener(PressRestart);
            }

            if (_titleButton is not null)
            {
                _titleButton.onClick.RemoveListener(PressTitle);
            }

            _state = null;
            _presenter = null;
        }

        /// <summary>
        /// Connects to the scope and pushes the first values. Public and idempotent so a test can
        /// drive it without waiting for <c>Start</c>.
        /// </summary>
        public void Bind()
        {
            if (_state is not null)
            {
                return;
            }

            if (_scope is null || _scope.Container is null)
            {
                Debug.LogError("ResultView needs a built RootLifetimeScope.", this);
                return;
            }

            _state = _scope.Container.Resolve<ResultUiState>();
            _presenter = _scope.Container.Resolve<ResultPresenter>();

            _headline = _state.Headline.Subscribe(value => Assign(_headlineLabel, value));
            _summary = _state.SummaryLine.Subscribe(value => Assign(_summaryLabel, value));
            _seed = _state.SeedLine.Subscribe(value => Assign(_seedLabel, value));
            _restartCaption = _state.RestartLabel.Subscribe(value => Assign(_restartLabel, value));
            _titleCaption = _state.TitleLabel.Subscribe(value => Assign(_titleLabel, value));

            if (_restartButton is not null)
            {
                _restartButton.onClick.AddListener(PressRestart);
            }

            if (_titleButton is not null)
            {
                _titleButton.onClick.AddListener(PressTitle);
            }

            if (_presenter.IsFinished)
            {
                _presenter.Begin();
            }
        }

        /// <summary>Where one of the screen's controls is, in screen pixels (origin bottom-left).</summary>
        public bool TryGetButtonScreenPoint(ResultButton button, out Vector2 screen)
        {
            Button target = button == ResultButton.Restart ? _restartButton : _titleButton;

            return UiScreenRects.TryCentre(target is null ? null : target.transform as RectTransform, out screen);
        }

        /// <summary>What the restart control does. Public so a test can press it without a pointer.</summary>
        public void PressRestart()
        {
            if (_presenter is not null)
            {
                _presenter.RequestRestart();
            }
        }

        /// <summary>What the title control does. Public so a test can press it without a pointer.</summary>
        public void PressTitle()
        {
            if (_presenter is not null)
            {
                _presenter.RequestTitle();
            }
        }

        private static void Assign(TMP_Text label, string value)
        {
            if (label is not null)
            {
                label.text = value;
            }
        }
    }
}

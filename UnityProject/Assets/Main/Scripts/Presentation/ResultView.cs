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
    /// The Result screen's view: it mirrors seven strings into seven labels and forwards two button
    /// presses. Nothing else.
    /// </summary>
    /// <remarks>
    /// <para>
    /// AGENTS rule 10: a View has no logic and computes no numbers. Every string here was already
    /// decided by <see cref="ResultPresenter"/> from the run and the localization table — including
    /// the button captions, which are player-facing text like any other.
    /// </para>
    /// <para>
    /// The two button presses become <see cref="ResultPresenter.RequestRestart"/> and
    /// <see cref="ResultPresenter.RequestTitle"/>, which only <i>record</i> the intent: the scene
    /// change and the run's own transitions belong to
    /// <see cref="ProjectSurvive.Game.Result.ResultSceneDirector"/>, so a refused scene load cannot
    /// leave this screen having already ended the run.
    /// </para>
    /// <para>
    /// Subscriptions are disposed and listeners removed in <c>OnDestroy</c>. The Result scene is
    /// loaded once per finished run, and a leaked subscription would accumulate silently — the
    /// same guard <c>TitleView</c> carries for the same reason.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class ResultView : MonoBehaviour
    {
        [SerializeField] private RootLifetimeScope _scope;
        [SerializeField] private TMP_Text _headlineLabel;
        [SerializeField] private TMP_Text _floorLabel;
        [SerializeField] private TMP_Text _killsLabel;
        [SerializeField] private TMP_Text _biaLabel;
        [SerializeField] private TMP_Text _hpLabel;
        [SerializeField] private Button _restartButton;
        [SerializeField] private Button _titleButton;
        [SerializeField] private TMP_Text _restartLabel;
        [SerializeField] private TMP_Text _titleLabel;

        private ResultUiState _state;
        private ResultPresenter _presenter;
        private IDisposable _headline;
        private IDisposable _floor;
        private IDisposable _kills;
        private IDisposable _bia;
        private IDisposable _hp;
        private IDisposable _restartLabelSubscription;
        private IDisposable _titleLabelSubscription;

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
            _floor?.Dispose();
            _kills?.Dispose();
            _bia?.Dispose();
            _hp?.Dispose();
            _restartLabelSubscription?.Dispose();
            _titleLabelSubscription?.Dispose();

            _headline = null;
            _floor = null;
            _kills = null;
            _bia = null;
            _hp = null;
            _restartLabelSubscription = null;
            _titleLabelSubscription = null;

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
        /// drive it without waiting for <c>Start</c>, exactly like <c>TitleView.Bind</c>.
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
            _floor = _state.FloorLine.Subscribe(value => Assign(_floorLabel, value));
            _kills = _state.KillsLine.Subscribe(value => Assign(_killsLabel, value));
            _bia = _state.BiaLine.Subscribe(value => Assign(_biaLabel, value));
            _hp = _state.HpLine.Subscribe(value => Assign(_hpLabel, value));
            _restartLabelSubscription = _state.RestartLabel.Subscribe(value => Assign(_restartLabel, value));
            _titleLabelSubscription = _state.TitleLabel.Subscribe(value => Assign(_titleLabel, value));

            if (_restartButton is not null)
            {
                _restartButton.onClick.AddListener(PressRestart);
            }

            if (_titleButton is not null)
            {
                _titleButton.onClick.AddListener(PressTitle);
            }

            // The strings were decided when the run ended; asking again here is what makes the
            // screen correct even when it is opened after the presenter already ran.
            if (_presenter.IsFinished)
            {
                _presenter.Begin();
            }
        }

        /// <summary>
        /// Where one of the screen's controls is on screen right now, in screen pixels with the
        /// origin at the bottom-left.
        /// </summary>
        /// <remarks>
        /// The page's layout report needs somewhere to aim a click, and a click has to land
        /// <i>inside</i> the control: the conversion is the same one the camp's views publish with
        /// (<see cref="CampScreenRects.TryCentre"/>) rather than a second copy of it here, because the
        /// defect it exists for — a <c>RectTransform</c> whose pivot is a corner reporting that corner
        /// as its position — is a property of the transform, not of the camp. A null control answers
        /// false, which is what a release build's page has to see.
        /// </remarks>
        public bool TryGetButtonScreenPoint(ResultButton button, out Vector2 screen)
        {
            Button target = button == ResultButton.Restart ? _restartButton : _titleButton;

            return CampScreenRects.TryCentre(
                target is null ? null : target.transform as RectTransform, out screen);
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

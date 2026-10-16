using System;
using ProjectSurvive.Game.Presentation.Camp;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using VContainer;

namespace ProjectSurvive.Game.Presentation
{
    /// <summary>
    /// Title screen view. It assigns text and forwards one button press. Nothing else.
    /// </summary>
    /// <remarks>
    /// <para>
    /// AGENTS rule 10: a View has no logic and computes no numbers. Everything this
    /// class does is mirror a reactive value into a label, which is exactly the
    /// "thin MVP" boundary — the strings were already decided by
    /// <see cref="TitlePresenter"/>.
    /// </para>
    /// <para>
    /// <b>The start control (Stage 10d).</b> The press becomes
    /// <see cref="TitlePresenter.RequestStart"/>, which <i>records</i> the intent and starts the run
    /// through the Application layer; the scene change belongs to
    /// <see cref="Flow.TitleSceneDirector"/>, the way the summary screen's two ways out belong to
    /// <c>ResultSceneDirector</c>. The button is also focused on arrival, because Unity submits a
    /// <c>Button</c> with Enter/Space only while it is the EventSystem's selected object — and with one
    /// control on the screen there is no ambiguity about which one that should be.
    /// </para>
    /// <para>
    /// Subscriptions are disposed and the listener removed in <c>OnDestroy</c>. A title screen is
    /// reloaded every time the player returns to it, so a leaked subscription here would accumulate
    /// silently — that is what the R3 smoke probe guards against.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class TitleView : MonoBehaviour
    {
        [SerializeField] private RootLifetimeScope _scope;
        [SerializeField] private TMP_Text _titleLabel;
        [SerializeField] private TMP_Text _diagnosticsLabel;

        [Tooltip("The control that starts a run (เริ่มเล่น).")]
        [SerializeField] private Button _startButton;

        [SerializeField] private TMP_Text _startLabel;

        private UiState _state;
        private TitlePresenter _presenter;
        private IDisposable _titleSubscription;
        private IDisposable _diagnosticsSubscription;
        private IDisposable _startLabelSubscription;

        /// <summary>The screen's controls, named for the page's own report.</summary>
        public enum TitleButton
        {
            /// <summary>The control that begins a run.</summary>
            Start = 0,
        }

        /// <summary>True once the view has a state to read (a test may wait for it).</summary>
        public bool Bound => _state is not null;

        /// <summary>The logic this view drives; null until <see cref="Bind"/>.</summary>
        public TitlePresenter Presenter => _presenter;

        private void Start() => Bind();

        /// <summary>
        /// Starts the run when the space bar is pressed.
        /// </summary>
        /// <remarks>
        /// Enter arrives as the input module's own submit event, because the control is the
        /// EventSystem's selected object (<see cref="FocusStartControl"/>). Space is the other key a
        /// player expects to press and the module's default submit bindings do not carry it on the Input
        /// System version this project pins — measured: with the focus in place Enter starts the run and
        /// Space does nothing at all. Reading the key here is the same thing the camp does for R and
        /// Escape, and it forwards to the same use case, so an Input System version that binds Space as
        /// well would simply call a second time and be refused.
        /// </remarks>
        private void Update()
        {
            if (_presenter is null)
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard is not null && keyboard.spaceKey.wasPressedThisFrame)
            {
                PressStart();
            }
        }

        private void OnDestroy()
        {
            _titleSubscription?.Dispose();
            _diagnosticsSubscription?.Dispose();
            _startLabelSubscription?.Dispose();
            _titleSubscription = null;
            _diagnosticsSubscription = null;
            _startLabelSubscription = null;

            if (_startButton is not null)
            {
                _startButton.onClick.RemoveListener(PressStart);
            }

            _state = null;
            _presenter = null;
        }

        /// <summary>
        /// Connects to the scope and pushes the first values. Public so a test can
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
                Debug.LogError("TitleView needs a built RootLifetimeScope.", this);
                return;
            }

            _state = _scope.Container.Resolve<UiState>();
            _presenter = _scope.Container.Resolve<TitlePresenter>();

            _titleSubscription = _state.Title.Subscribe(ApplyTitle);
            _diagnosticsSubscription = _state.Diagnostics.Subscribe(ApplyDiagnostics);
            _startLabelSubscription = _state.StartLabel.Subscribe(ApplyStartLabel);

            if (_startButton is not null)
            {
                _startButton.onClick.AddListener(PressStart);
            }

            // What the screen *says* — the title, the start caption and the build line — is written by
            // the director, which is also what publishes the page's report. The view stays what it was:
            // a mirror of the state plus one forwarding press.
            FocusStartControl();
        }

        /// <summary>
        /// Where the start control is on screen right now, in screen pixels with the origin at the
        /// bottom-left.
        /// </summary>
        /// <remarks>
        /// The page's report needs somewhere to aim a click, and a click has to land <i>inside</i> the
        /// control: the conversion is the camp's own (<see cref="CampScreenRects.TryCentre"/>) rather
        /// than a second copy of it here, for the reason that helper exists — a <c>RectTransform</c>
        /// whose pivot is a corner reports that corner as its position.
        /// </remarks>
        public bool TryGetButtonScreenPoint(TitleButton button, out Vector2 screen)
        {
            Button target = button == TitleButton.Start ? _startButton : null;

            return CampScreenRects.TryCentre(
                target is null ? null : target.transform as RectTransform, out screen);
        }

        /// <summary>What the start control does. Public so a test can press it without a pointer.</summary>
        public void PressStart()
        {
            if (_presenter is not null)
            {
                _presenter.RequestStart();
            }
        }

        private void ApplyTitle(string value) => Assign(_titleLabel, value);

        private void ApplyDiagnostics(string value) => Assign(_diagnosticsLabel, value);

        private void ApplyStartLabel(string value) => Assign(_startLabel, value);

        /// <summary>
        /// Makes the start control the EventSystem's selected object when the screen comes up.
        /// </summary>
        /// <remarks>
        /// Enter and Space reach a Unity <c>Button</c> as the input module's submit event, and the
        /// module sends it to whatever is selected; with nothing selected they do nothing at all. The
        /// title screen has exactly one control, so which one to focus is not a judgement — and
        /// nothing already selected is taken away, because a test or an accessibility tool may have
        /// chosen something on purpose.
        /// </remarks>
        private void FocusStartControl()
        {
            if (_startButton is null || EventSystem.current is null)
            {
                return;
            }

            if (EventSystem.current.currentSelectedGameObject is null)
            {
                EventSystem.current.SetSelectedGameObject(_startButton.gameObject);
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

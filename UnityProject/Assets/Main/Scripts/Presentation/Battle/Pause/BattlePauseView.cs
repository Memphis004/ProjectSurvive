using System;
using ProjectSurvive.Game.Presentation.Battle.Pause;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using VContainer;
using VContainer.Unity;

namespace ProjectSurvive.Game.Presentation.Battle
{
    /// <summary>
    /// The battle's pause overlay: a heading, the warning that a retreat forfeits the floor, and the
    /// two ways out (Stage 13c-2b). A view — it mirrors <see cref="BattlePauseState"/> into labels and
    /// forwards presses to <see cref="BattlePausePresenter"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It computes nothing (AGENTS rule 10): the two captions and the warning are localized strings the
    /// presenter wrote, the panel's visibility is the state's own flag, and every press is one call into
    /// the presenter. The one thing it reads for itself is the Escape key, which is a device event
    /// rather than a game rule — the same way <c>BattleInputAdapter</c> owns the movement keys.
    /// </para>
    /// <para>
    /// The overlay lives on its own canvas above the HUD, so it covers the HUD rather than sitting
    /// beside it, and its buttons are reachable by the pointer while the floor is frozen. The report it
    /// publishes (<see cref="BattlePauseReport"/>) is how a browser test presses หยุด, waits, resumes
    /// and leaves — a WebGL player cannot run Unity's test framework.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class BattlePauseView : MonoBehaviour
    {
        /// <summary>The overlay's pressable controls, by the name the report gives them.</summary>
        public enum PauseButton
        {
            /// <summary>The HUD's own หยุด control, which opens the overlay.</summary>
            Open,

            /// <summary>เล่นต่อ, inside the overlay.</summary>
            Resume,

            /// <summary>กลับศาลา, inside the overlay.</summary>
            Leave,
        }

        [SerializeField] private LifetimeScope _scope;

        [Tooltip("The runner whose floor is held still. Authored by ProjectSetup.")]
        [SerializeField] private BattleSimulationRunner _runner;

        [Tooltip("The overlay's canvas object. Authored inactive: the scene is authored between floors.")]
        [SerializeField] private GameObject _panel;

        [SerializeField] private TMP_Text _titleLabel;
        [SerializeField] private TMP_Text _warningLabel;

        [Tooltip("The HUD control that opens the overlay, outside the panel.")]
        [SerializeField] private Button _openButton;

        [SerializeField] private Button _resumeButton;
        [SerializeField] private Button _leaveButton;

        /// <summary>How often the open overlay rewrites its document, in unscaled seconds.</summary>
        private const float ReportIntervalSeconds = 0.5f;

        private BattlePauseState _state;
        private BattlePausePresenter _presenter;
        private IDisposable _openSubscription;
        private IDisposable _titleSubscription;
        private IDisposable _warningSubscription;
        private IDisposable _resumeSubscription;
        private IDisposable _leaveSubscription;
        private IDisposable _openLabelSubscription;
        private float _publishTimer;
        private bool _publishedAttached;

        /// <summary>True once the view has a presenter (a test may wait for it).</summary>
        public bool Bound => _presenter is not null;

        /// <summary>The logic this view drives; null until <see cref="Bind"/>.</summary>
        public BattlePausePresenter Presenter => _presenter;

        /// <summary>True while the overlay is drawn.</summary>
        public bool IsOpen => _panel is not null && _panel.activeSelf;

        private void Start() => Bind();

        private void Update()
        {
            if (_presenter is null)
            {
                return;
            }

            // The scene's director attaches the floor in its own Start, and a view's Start can run
            // first: the document written at bind time then carries empty strings and no HP. The
            // first frame after the attach rewrites it, so a browser never reads that half-built
            // document as the overlay's state.
            if (!_publishedAttached && _presenter.IsAttached)
            {
                _publishedAttached = true;
                PublishReport();
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard is not null && keyboard.escapeKey.wasPressedThisFrame)
            {
                _presenter.Toggle();
            }

            // While the overlay is up, its document is rewritten twice a second. That is not a hot
            // path — it runs only while the floor is frozen — and it is the only way a browser can
            // see that the steps stopped: the battle's own report is written by the runner, and a
            // paused runner writes none.
            if (!IsOpen)
            {
                return;
            }

            _publishTimer += Time.unscaledDeltaTime;
            if (_publishTimer >= ReportIntervalSeconds)
            {
                _publishTimer = 0f;
                PublishReport();
            }
        }

        private void OnDestroy()
        {
            _openSubscription?.Dispose();
            _titleSubscription?.Dispose();
            _warningSubscription?.Dispose();
            _resumeSubscription?.Dispose();
            _leaveSubscription?.Dispose();
            _openLabelSubscription?.Dispose();
            _openSubscription = null;
            _titleSubscription = null;
            _warningSubscription = null;
            _resumeSubscription = null;
            _leaveSubscription = null;
            _openLabelSubscription = null;

            Remove(_openButton, Open);
            Remove(_resumeButton, Resume);
            Remove(_leaveButton, LeaveForCamp);

            _state = null;
            _presenter = null;
        }

        /// <summary>Connects to the scene scope and starts mirroring. Public for tests.</summary>
        public void Bind()
        {
            if (_state is not null)
            {
                return;
            }

            if (_scope is null || _scope.Container is null)
            {
                Debug.LogError("BattlePauseView needs a built scene scope.", this);
                return;
            }

            _state = _scope.Container.Resolve<BattlePauseState>();
            _presenter = _scope.Container.Resolve<BattlePausePresenter>();

            _openSubscription = _state.IsOpen.Subscribe(SetOpen);
            _titleSubscription = _state.Title.Subscribe(value => Assign(_titleLabel, value));
            _warningSubscription = _state.Warning.Subscribe(value => Assign(_warningLabel, value));
            _resumeSubscription = _state.ResumeLabel.Subscribe(value => AssignButton(_resumeButton, value));
            _leaveSubscription = _state.LeaveLabel.Subscribe(value => AssignButton(_leaveButton, value));
            _openLabelSubscription = _state.OpenLabel.Subscribe(value => AssignButton(_openButton, value));

            Add(_openButton, Open);
            Add(_resumeButton, Resume);
            Add(_leaveButton, LeaveForCamp);

            if (_panel is not null)
            {
                _panel.SetActive(false);
            }

            PublishReport();
        }

        /// <summary>Opens the overlay through the presenter. Public for tests.</summary>
        public void Open()
        {
            _presenter?.Open();
            PublishReport();
        }

        /// <summary>Resumes the floor through the presenter. Public for tests.</summary>
        public void Resume()
        {
            _presenter?.Resume();
            PublishReport();
        }

        /// <summary>Leaves for the camp through the presenter. Public for tests.</summary>
        public void LeaveForCamp()
        {
            _presenter?.LeaveForCamp();
            PublishReport();
        }

        /// <summary>
        /// Where a control is on screen right now, in screen pixels with the origin at the bottom-left,
        /// or false when it is not pressable (the overlay's rows while it is closed).
        /// </summary>
        public bool TryGetButtonScreenPoint(PauseButton button, out Vector2 screen)
        {
            Button target = button switch
            {
                PauseButton.Open => _openButton,
                PauseButton.Resume => _resumeButton,
                PauseButton.Leave => _leaveButton,
                _ => null,
            };

            if (target is null || !target.isActiveAndEnabled || !target.gameObject.activeInHierarchy)
            {
                screen = default;
                return false;
            }

            return Camp.CampScreenRects.TryCentre(target.transform as RectTransform, out screen);
        }

        /// <summary>Hands the overlay's state and its control points to the page.</summary>
        public void PublishReport()
        {
            if (_state is null || _panel is null)
            {
                return;
            }

            BattlePauseReport.Publish(_state, this, _presenter, _runner, gameObject.scene.name);
        }

        private void SetOpen(bool open)
        {
            if (_panel is not null)
            {
                _panel.SetActive(open);
            }

            PublishReport();
        }

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

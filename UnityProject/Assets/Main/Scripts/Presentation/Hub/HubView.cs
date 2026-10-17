using System;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace ProjectSurvive.Game.Presentation.Hub
{
    /// <summary>
    /// The Hub screen's view: it mirrors five strings into five labels and forwards two presses.
    /// </summary>
    /// <remarks>
    /// AGENTS rule 10: no logic, no numbers computed here. Every string was decided by
    /// <see cref="HubPresenter"/>; the presses only record an intent that the scene's director turns
    /// into a scene change.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class HubView : MonoBehaviour
    {
        [SerializeField] private RootLifetimeScope _scope;
        [SerializeField] private TMP_Text _titleLabel;
        [SerializeField] private TMP_Text _instructionsLabel;
        [SerializeField] private TMP_Text _encountersLabel;
        [SerializeField] private Button _departButton;
        [SerializeField] private Button _finishButton;
        [SerializeField] private TMP_Text _departLabel;
        [SerializeField] private TMP_Text _finishLabel;

        private HubUiState _state;
        private HubPresenter _presenter;
        private bool _reportPending;
        private IDisposable _title;
        private IDisposable _instructions;
        private IDisposable _encounters;
        private IDisposable _departCaption;
        private IDisposable _finishCaption;

        /// <summary>True once the view has a state to read.</summary>
        public bool Bound => _state is not null;

        /// <summary>The logic this view drives; null until <see cref="Bind"/>.</summary>
        public HubPresenter Presenter => _presenter;

        /// <summary>The controls the page's report publishes a click point for.</summary>
        public enum HubButton
        {
            /// <summary>Enters the next encounter.</summary>
            Depart,

            /// <summary>Ends the run.</summary>
            Finish,
        }

        private void Start() => Bind();

        private void OnDestroy()
        {
            _title?.Dispose();
            _instructions?.Dispose();
            _encounters?.Dispose();
            _departCaption?.Dispose();
            _finishCaption?.Dispose();
            _title = null;
            _instructions = null;
            _encounters = null;
            _departCaption = null;
            _finishCaption = null;

            if (_departButton is not null)
            {
                _departButton.onClick.RemoveListener(PressDepart);
            }

            if (_finishButton is not null)
            {
                _finishButton.onClick.RemoveListener(PressFinish);
            }

            _state = null;
            _presenter = null;
        }

        /// <summary>Connects to the scene scope and pushes the first values. Idempotent.</summary>
        public void Bind()
        {
            if (_state is not null)
            {
                return;
            }

            if (_scope is null || _scope.Container is null)
            {
                Debug.LogError("HubView needs a built RootLifetimeScope.", this);
                return;
            }

            _state = _scope.Container.Resolve<HubUiState>();
            _presenter = _scope.Container.Resolve<HubPresenter>();

            _title = _state.Title.Subscribe(value => Assign(_titleLabel, value));
            _instructions = _state.Instructions.Subscribe(value => Assign(_instructionsLabel, value));
            _encounters = _state.EncountersLine.Subscribe(value => Assign(_encountersLabel, value));
            _departCaption = _state.DepartLabel.Subscribe(value => Assign(_departLabel, value));
            _finishCaption = _state.FinishLabel.Subscribe(value => Assign(_finishLabel, value));

            if (_departButton is not null)
            {
                _departButton.onClick.AddListener(PressDepart);
            }

            if (_finishButton is not null)
            {
                _finishButton.onClick.AddListener(PressFinish);
            }

            // The page's report is published rather than by the director because this is the only object
            // holding the control references: a director would need a new serialized field and a scene
            // rebuild for a diagnostics-only need. The view computes nothing — the strings are the state's
            // and the points are the controls' own (AGENTS rule 10).
            //
            // It is published from `LateUpdate`, not here: the presenter writes the strings in `Start`
            // too, and the order of the two `Start` calls is undefined, so reporting now could capture a
            // state nobody has filled yet. One frame later the layout is also final, which is what a click
            // point needs (gotcha `layout-report-one-frame-early`).
            _reportPending = true;
        }

        private void LateUpdate()
        {
            if (!_reportPending || _state is null)
            {
                return;
            }

            _reportPending = false;
            HubReport.Publish(_state, this, gameObject.scene.name);
        }

        /// <summary>
        /// Where one of the screen's controls is on screen, in screen pixels with the origin at the
        /// bottom-left. False when the scene has not wired that control.
        /// </summary>
        public bool TryGetButtonScreenPoint(HubButton button, out Vector2 screen)
        {
            Button target = button == HubButton.Depart ? _departButton : _finishButton;

            return UiScreenRects.TryCentre(target is null ? null : target.transform as RectTransform, out screen);
        }

        /// <summary>What the depart control does. Public so a test can press it without a pointer.</summary>
        public void PressDepart()
        {
            if (_presenter is not null)
            {
                _presenter.RequestDepart();
            }
        }

        /// <summary>What the finish control does. Public so a test can press it without a pointer.</summary>
        public void PressFinish()
        {
            if (_presenter is not null)
            {
                _presenter.RequestFinish();
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

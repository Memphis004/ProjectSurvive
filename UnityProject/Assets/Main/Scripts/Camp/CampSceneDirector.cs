using System;
using ProjectSurvive.Core.Application.Run;
using ProjectSurvive.Core.Domain.Run;
using ProjectSurvive.Core.Shared;
using ProjectSurvive.Game.Presentation.Camp;
using ProjectSurvive.Game.Presentation.Web;
using UnityEngine;
using VContainer;

namespace ProjectSurvive.Game.Camp
{
    /// <summary>
    /// Brings the Camp scene up: resolves the run the screen edits, starts it if it has not been
    /// started, hands every view its model, and keeps the browser report current.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Deliberately thin, like <c>BattleSceneDirector</c>. Everything it composes is either the
    /// scene's wiring (authored by <c>ProjectSetup</c> through the Editor API, AGENTS rule 11) or a
    /// service resolved from <see cref="CampSceneScope"/>. It decides nothing about the game: a
    /// start that cannot happen is logged and leaves the scene inert rather than half-working.
    /// </para>
    /// <para>
    /// <b>It owns the report's clock, and only that.</b> A camp screen has no per-frame work — the
    /// whole point of replacing models rather than streaming them — so this <c>Update</c> exists
    /// solely to notice that the run changed and to say so to the page. It is gated twice: never
    /// while a drag is live (a gesture is not a state change, and building a string per pointer
    /// move is the GC spike the stage forbids), and never twice for the same key.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class CampSceneDirector : MonoBehaviour
    {
        [SerializeField] private CampSceneScope _scope;
        [SerializeField] private CampWorldView _world;
        [SerializeField] private CampPlayerView _player;
        [SerializeField] private CampBagScreenView _bag;
        [SerializeField] private CampStatusBarView _status;
        [SerializeField] private CampTooltipView _tooltip;

        [Tooltip("Publish __projectSurviveCamp and __projectSurviveCampGeometry. Only a browser test reads them.")]
        [SerializeField] private bool _publishReport = true;

        [Tooltip("Seconds between report checks. A check compares one hash and usually stops there.")]
        [SerializeField] private float _reportIntervalSeconds = 0.25f;

        private int _lastKey;
        private int _lastWidth = -1;
        private int _lastHeight = -1;
        private bool _lastBagVisible = true;
        private float _lastCameraX = float.NaN;
        private float _lastCameraZ = float.NaN;
        private float _reportTimer;
        private bool _started;

        /// <summary>The run every command goes through (null until the scene has started).</summary>
        public RunSession Run { get; private set; }

        /// <summary>The screen's logic (null until the scene has started).</summary>
        public CampPresenter Presenter { get; private set; }

        /// <summary>Why the last start refused, or an empty string.</summary>
        public string StartError { get; private set; } = string.Empty;

        /// <summary>True once the camp is up and every view has its model.</summary>
        public bool Started => _started;

        private void Start() => TryStart();

        /// <summary>
        /// Starts the camp: resolves the run, opens it at the first camp when it is still at the
        /// title, binds every view. Idempotent, so a test may drive it without waiting for
        /// <c>Start</c>.
        /// </summary>
        public bool TryStart()
        {
            if (_started)
            {
                return true;
            }

            if (_scope == null || _scope.Container == null)
            {
                StartError = "CampSceneDirector has no built CampSceneScope.";
                Debug.LogError(StartError, this);
                return false;
            }

            Run = _scope.Container.Resolve<RunSession>();
            Presenter = _scope.Container.Resolve<CampPresenter>();

            if (Run.FlowState == RunFlowState.Title)
            {
                Result started = Run.StartRun();
                if (started.IsFailure)
                {
                    StartError = "The run could not start: " + started.Error.Code;
                    Debug.LogError(StartError, this);
                    return false;
                }
            }

            if (Run.FlowState != RunFlowState.Camp)
            {
                // Reached by a scene that was opened mid-run (a `?scene=Camp` handover after a
                // battle, say). The camp screen is the wrong place for that phase, and saying so is
                // better than drawing a shop the run would refuse to sell from.
                StartError = "The camp scene needs a run in Camp; this run is in " + Run.FlowState + ".";
                Debug.LogError(StartError, this);
                return false;
            }

            StartError = string.Empty;

            // The scene marker, published by the scene that is on screen. Without it a
            // `?scene=Camp` handover is invisible to a browser test, which is the only place the
            // handover can be observed.
            WebGlBridge.PublishScene(gameObject.scene.name);

            // The presenter first: it fills the state every view subscribes to, so a view that
            // binds afterwards receives a complete model immediately rather than an empty one.
            Presenter.Begin();

            if (_world != null)
            {
                _world.Bind();
            }

            if (_player != null)
            {
                _player.Bind();
            }

            if (_bag != null)
            {
                _bag.Bind();
            }

            if (_status != null)
            {
                _status.Bind();
            }

            if (_tooltip != null)
            {
                _tooltip.Bind();
            }

            _started = true;
            Publish(true);
            Debug.Log("Camp scene started: floor " + Run.FloorIndex + ", " + Run.Wallet.Bia + " bia.", this);
            return true;
        }

        private void Update()
        {
            if (!_started || !_publishReport)
            {
                return;
            }

            _reportTimer += Time.unscaledDeltaTime;
            if (_reportTimer < _reportIntervalSeconds)
            {
                return;
            }

            _reportTimer = 0f;
            Publish(false);
        }

        /// <summary>
        /// Republishes the layout when the picture moved and the state when the run did.
        /// </summary>
        /// <remarks>
        /// Both are published even while the จัดย่าม panel is closed — since Stage 09c that is the
        /// camp's normal state, and the sala's geometry (where each shelf is on screen) is what a
        /// browser test aims at. The bag panel's own rectangles are still in the report, tagged with
        /// <c>bagVisible</c>, so a test can wait for the panel before dragging at a cell.
        /// </remarks>
        private void Publish(bool force)
        {
            if (!_publishReport)
            {
                return;
            }

            PublishGeometry(force);

            // A live gesture changes the picture on screen but not the run, so the state report
            // waits for the drop. This is the one gate that keeps a drag allocation-free.
            if (Presenter.IsDragging)
            {
                return;
            }

            int key = CampReport.Key(Run, Presenter, _player, _world);
            if (!force && key == _lastKey)
            {
                return;
            }

            _lastKey = key;
            CampReport.PublishState(Run, Presenter, gameObject.scene.name, StartError, _player, _world);
        }

        private void PublishGeometry(bool force)
        {
            if (_bag == null)
            {
                return;
            }

            // The layout is published once and then only when the picture moved: the cell positions
            // are what a browser test aims its mouse at, so they have to be right after a resize,
            // and since Stage 09c also after the follow camera moved, or a click would be aimed at
            // where a shelf used to be. The จัดย่าม panel opening or closing is the third such
            // change: the panel's own controls come and go with it, and a report that still said
            // `bagVisible: false` after it opened would be describing the previous screen.
            bool resized = Screen.width != _lastWidth || Screen.height != _lastHeight;
            bool cameraMoved = _world != null
                && (Mathf.Abs(_world.CameraPosition.x - _lastCameraX) > 0.05f
                    || Mathf.Abs(_world.CameraPosition.z - _lastCameraZ) > 0.05f);
            bool bagToggled = _bag.PanelVisible != _lastBagVisible;

            if (!force && !resized && !cameraMoved && !bagToggled)
            {
                return;
            }

            if (_bag.BoardWidth <= 0)
            {
                // No model has been rendered yet, so there is no geometry to report.
                return;
            }

            _lastWidth = Screen.width;
            _lastHeight = Screen.height;
            _lastBagVisible = _bag.PanelVisible;

            if (_world != null)
            {
                _lastCameraX = _world.CameraPosition.x;
                _lastCameraZ = _world.CameraPosition.z;
            }

            CampReport.PublishGeometry(_bag, _world, _player, _status);
        }
    }
}

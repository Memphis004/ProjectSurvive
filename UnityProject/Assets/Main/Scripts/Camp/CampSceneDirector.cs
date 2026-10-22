using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using ProjectSurvive.Core.Application.Run;
using ProjectSurvive.Core.Application.Settings;
using ProjectSurvive.Core.Application.Text;
using ProjectSurvive.Core.Domain.Run;
using ProjectSurvive.Core.Shared;
using ProjectSurvive.Game.Presentation.Audio;
using ProjectSurvive.Game.Presentation.Camp;
using ProjectSurvive.Game.Presentation.Flow;
using ProjectSurvive.Game.Presentation.Web;
using R3;
using TMPro;
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

        [Tooltip("Shown when a checkpoint write failed (Stage 12).")]
        [SerializeField] private TMP_Text _saveWarning;

        [Tooltip("Publish __projectSurviveCamp and __projectSurviveCampGeometry. Only a browser test reads them.")]
        [SerializeField] private bool _publishReport = true;

        [Tooltip("Seconds between report checks. A check compares one hash and usually stops there.")]
        [SerializeField] private float _reportIntervalSeconds = 0.25f;

        private SceneLoader _loader;
        private GameSave _save;
        private SettingsService _settings;
        private LocalizationService _text;
        private CancellationTokenSource _lifetime;
        private IDisposable _cueSubscription;
        private bool _departed;
        private bool _saveDirty;

        /// <summary>
        /// Set when the first-run checklist was finished and its flag still has to reach the
        /// settings file (Stage 13b). Unlike the run, this is a once-per-profile fact: it is written
        /// as soon as it is known, and the departure path writes it before the scene changes.
        /// </summary>
        private bool _tutorialDirty;
        private bool _warningVisible;
        private float _saveTimer;
        private int _lastSaveKey;

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

            // The run comes from the App scope's run owner, not from this scene: walking back to
            // the camp after a floor must edit the wallet and the bag the battle left behind
            // (Stage 10a), and a scene-scoped run is exactly what used to make that impossible.
            Run = _scope.Container.Resolve<RunScopeOwner>().RequireRun();
            _loader = _scope.Container.Resolve<SceneLoader>();
            Presenter = _scope.Container.Resolve<CampPresenter>();

            // Stage 13c-1: the presenter asks for sounds, this scene hands them to the app's audio
            // device. One subscription for the scene's life, disposed with it.
            _cueSubscription = _scope.Container.Resolve<CampUiState>().Cues.Subscribe(
                cue => AudioDirector.Instance?.Play(cue));

            _save = _scope.Container.Resolve<GameSave>();
            _settings = _scope.Container.Resolve<SettingsService>();
            _text = _scope.Container.Resolve<LocalizationService>();
            _lifetime = new CancellationTokenSource();
            _lastSaveKey = -1;

            // A run that has already ended belongs on the summary screen, not in the camp. This is
            // the guard for a handover that lands here after the last floor (a `?scene=Camp`
            // handover, or the browser's back button) — on the normal path the battle leaves for the
            // summary screen itself, and this is what makes the camp refuse to shop for a run that
            // is over.
            if (Run.IsRunOver)
            {
                if (!_loader.TryGo(SceneId.Result))
                {
                    StartError = "The finished run could not be summarised: " + _loader.LastError;
                    Debug.LogError(StartError, this);
                }

                return false;
            }

            if (Run.FlowState == RunFlowState.Title)
            {
                // Fully qualified: `R3` is in scope for the cue channel below, and it also has a
                // `Result` type, so the shared one would be an ambiguous reference.
                ProjectSurvive.Core.Shared.Result started = Run.StartRun();
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
            if (!_started)
            {
                return;
            }

            // Taken before the departure is handled: the checklist's last step is the departure
            // itself, so the flag must already be pending when DepartAsync starts writing.
            if (Presenter != null && Presenter.TakeTutorialCompletion())
            {
                _tutorialDirty = true;
            }

            UpdateAutosave();

            // The departure is the camp's one way out: the presenter has already performed the
            // real RunSession.StartFloor (and the run holds the resulting setup), so all that is
            // left is the scene change. It is asked once — the loader refuses a second transition
            // anyway, but a director that retried every frame would make that refusal the normal
            // path instead of an error.
            if (!_departed && Presenter != null && Presenter.Departed)
            {
                // Stage 12: the checkpoint that carries this floor's seed must be durable before
                // the arena starts. The scene change waits for the write instead of racing it.
                _departed = true;
                DepartAsync(_lifetime.Token).Forget();
            }

            if (!_publishReport)
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
        /// Debounced autosave: a run change schedules a write half a second later, and a second
        /// change before then simply moves the deadline. The key is the run's own state (bag,
        /// purse, HP, slot flags), never the player's walking position.
        /// </summary>
        private void UpdateAutosave()
        {
            if (_save is null || Run is null)
            {
                return;
            }

            int key = SaveKey(Run);
            if (key != _lastSaveKey)
            {
                _lastSaveKey = key;
                _saveDirty = true;
                _saveTimer = 0.5f;
            }

            if (_saveDirty)
            {
                _saveTimer -= Time.unscaledDeltaTime;
                if (_saveTimer <= 0f)
                {
                    _saveDirty = false;
                    _save.SaveAsync(Run, _lifetime.Token).Forget();
                }
            }

            if (_tutorialDirty)
            {
                _tutorialDirty = false;
                WriteTutorialAsync(_lifetime.Token).Forget();
            }

            bool failed = _save.LastSaveFailed;
            if (failed != _warningVisible)
            {
                _warningVisible = failed;
                if (_saveWarning != null)
                {
                    _saveWarning.text = failed ? _text.Get("ui.save.failed") : string.Empty;
                }

                // The warning is not part of the run key, so the report would otherwise keep saying
                // `saveFailed: false` after the write it is reporting on had failed. One forced
                // publish, on the transition only — never in a loop.
                Publish(true);
            }
        }

        /// <summary>
        /// Writes the "checklist finished" flag into the settings file. The settings service lives in
        /// the App scope, so the write is not part of this scene's state either.
        /// </summary>
        private async UniTask SaveTutorialAsync(CancellationToken token)
        {
            _settings.Apply(settings => settings.SetTutorialCompleted(true));
            await _settings.SaveAsync(token);
            _save.PublishReport();
        }

        /// <summary>
        /// Fire-and-forget form of <see cref="SaveTutorialAsync"/>. A failed write is logged and
        /// ignored: the checklist staying off for the rest of this session is the right behaviour
        /// when storage is unavailable, and the next camp would show it again rather than break.
        /// </summary>
        private async UniTaskVoid WriteTutorialAsync(CancellationToken token)
        {
            try
            {
                await SaveTutorialAsync(token);
            }
            catch (OperationCanceledException)
            {
                _tutorialDirty = true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private async UniTaskVoid DepartAsync(CancellationToken token)
        {
            try
            {
                // "Never show the checklist again" is durable before the floor starts, so a player
                // who reloads mid-battle does not meet it a second time. Same write the autosave
                // would have made; this one is ordered against the scene change instead of a
                // timer.
                if (_tutorialDirty)
                {
                    _tutorialDirty = false;
                    await SaveTutorialAsync(token);
                }

                await _save.SaveAsync(Run, token);
            }
            catch (OperationCanceledException)
            {
                _tutorialDirty = true;
                return;
            }

            if (_loader == null || !_loader.TryGo(SceneId.Battle))
            {
                StartError = "the battle scene could not be entered: " + (_loader == null ? "no loader" : _loader.LastError);
                Debug.LogError(StartError, this);
            }
        }

        /// <summary>A cheap fold of every run field a reload must restore (no allocations).</summary>
        private static int SaveKey(RunSession run)
        {
            unchecked
            {
                int hash = run.Backpack.Revision;
                hash = (hash * 397) ^ run.Wallet.Bia;
                hash = (hash * 397) ^ run.Hp;
                hash = (hash * 397) ^ run.FloorIndex;
                hash = (hash * 397) ^ run.Stock.RefreshCount;
                for (int i = 0; i < run.Stock.SlotCount; i++)
                {
                    hash = (hash * 397) ^ (run.Stock[i].OfferedId ?? string.Empty).GetHashCode();
                    hash = (hash * 397) ^ (run.Stock[i].IsPurchased ? 1 : 0);
                    hash = (hash * 397) ^ (run.Stock[i].IsLocked ? 2 : 0);
                }

                return hash;
            }
        }

        private void OnDestroy()
        {
            _cueSubscription?.Dispose();
            _cueSubscription = null;

            if (_lifetime is null)
            {
                return;
            }

            _lifetime.Cancel();
            _lifetime.Dispose();
            _lifetime = null;
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
            // Stage 09j: the same lifetime guard the rest of the camp uses. A director whose start
            // refused (a run that ended, a scene opened mid-run) has `_started` false and never gets
            // here, but `Update` runs its own copy of this gate — and a presenter that is gone while
            // the director still ticks threw from `Presenter.IsDragging`.
            if (!_publishReport || Presenter == null)
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
            CampReport.PublishState(
                Run, Presenter, gameObject.scene.name, StartError, _player, _world,
                _save != null && _save.LastSaveFailed,
                _saveWarning != null ? _saveWarning.text : string.Empty,
                _save != null ? _save.LastError : string.Empty);

            // Stage 13: every label on the camp screen, measured in the player with the shipped
            // font (the จัดย่าม panel included, because the scan visits inactive children too).
            UiTextFitReport.Publish(gameObject.scene.name);
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

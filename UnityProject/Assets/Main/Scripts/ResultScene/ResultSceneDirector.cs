using ProjectSurvive.Core.Application.Run;
using ProjectSurvive.Core.Domain.Run;
using ProjectSurvive.Game.Presentation;
using ProjectSurvive.Game.Presentation.Flow;
using ProjectSurvive.Game.Presentation.Web;
using UnityEngine;
using VContainer;

namespace ProjectSurvive.Game.ResultScene
{
    /// <summary>
    /// Shows how the run ended, and is the one place that knows both halves of leaving it: the
    /// scene change and the run's own transition.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Deliberately thin, like <c>BattleSceneDirector</c> and <c>CampSceneDirector</c>. Everything
    /// it composes is either the scene's wiring (authored by <c>ProjectSetup</c> through the Editor
    /// API, AGENTS rule 11) or a service resolved from <see cref="ResultSceneScope"/>. It decides
    /// nothing about the game: it asks the presenter to fill the state and then performs the way
    /// out the player chose.
    /// </para>
    /// <para>
    /// <b>The two ways out.</b> <i>Restart</i> loads the camp and then disposes the run scope, so
    /// the camp starts a brand-new run — floor 1, the starting purse, an empty bag — which is what
    /// "Restart สร้าง Run ใหม่ ไม่คง state จากรอบก่อน" asks for. <i>Back to title</i> loads the
    /// Title, performs the run's own Result → Title transition once the move is committed, and the
    /// scene loader disposes the run on arrival (that disposal is a property of the Title
    /// transition, not something this screen has to remember).
    /// </para>
    /// <para>
    /// Both are <b>committed before they are applied</b>: the loader is asked first, and only a
    /// transition it accepted is followed by a change to the run. A refused load — a scene missing
    /// from the build, or another transition in flight — therefore leaves the screen exactly as it
    /// was, still showing the summary of a run that is still over.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class ResultSceneDirector : MonoBehaviour
    {
        [SerializeField] private ResultSceneScope _scope;

        private RunScopeOwner _runs;
        private RunSession _run;
        private SceneLoader _loader;
        private bool _started;
        private bool _departed;

        /// <summary>The screen's logic (null until the scene has started).</summary>
        public ResultPresenter Presenter { get; private set; }

        /// <summary>The run being summarised (null until the scene has started).</summary>
        public RunSession Run => _run;

        /// <summary>What the screen shows, as of the start (default while not started).</summary>
        public RunSummary Summary { get; private set; }

        /// <summary>Why the last start refused, or an empty string.</summary>
        public string StartError { get; private set; } = string.Empty;

        /// <summary>True once the summary is on screen.</summary>
        public bool Started => _started;

        /// <summary>True once a way out has been committed.</summary>
        public bool Departed => _departed;

        private void Start() => TryStart();

        /// <summary>
        /// Brings the screen up: resolves the run the battle settled, fills the state, and publishes
        /// the scene marker. Idempotent, so a test may drive it without waiting for <c>Start</c>.
        /// </summary>
        /// <returns>
        /// False when there is no finished run to summarise — the screen then sends the player to
        /// the camp, which is where a run that is still going belongs.
        /// </returns>
        public bool TryStart()
        {
            if (_started)
            {
                return true;
            }

            if (_scope is null || _scope.Container is null)
            {
                StartError = "ResultSceneDirector has no built ResultSceneScope.";
                Debug.LogError(StartError, this);
                return false;
            }

            _runs = _scope.Container.Resolve<RunScopeOwner>();
            _run = _runs.RequireRun();
            _loader = _scope.Container.Resolve<SceneLoader>();
            Presenter = _scope.Container.Resolve<ResultPresenter>();

            Summary = _run.Summary();
            if (!Summary.IsFinished)
            {
                // A Result screen with nothing to summarise: the scene was opened on its own, or
                // reached with a run that never ended. Showing "Victory" for a live run would be a
                // lie, and leaving the player on an empty screen would be a dead end — so the camp,
                // which is where a live run belongs, is where this goes.
                StartError =
                    "the Result scene needs a finished run; this run is in " + _run.FlowState + ".";
                Debug.LogWarning(StartError, this);

                if (!_loader.TryGo(SceneId.Camp))
                {
                    StartError += " The camp could not be entered either: " + _loader.LastError;
                    Debug.LogError(StartError, this);
                }

                return false;
            }

            StartError = string.Empty;

            // The scene marker, published by the scene that is on screen. Without it a handover
            // into this scene is invisible to the log a bug report quotes.
            WebGlBridge.PublishScene(gameObject.scene.name);

            Presenter.Begin();

            // What the screen shows, and where its two ways out are, for the page: this is the only
            // leg of the loop a browser test could not reach before (see ResultReport). Published after
            // Begin, so the document carries the strings the player is about to read rather than the
            // empty ones the state started with.
            PublishReport();

            _started = true;
            Debug.Log("Result screen started: " + Summary, this);
            return true;
        }

        /// <summary>
        /// Hands the screen's state and its controls to the page, and says so when the scene has no
        /// <see cref="ResultView"/> to measure its buttons in (a wiring fault, not a display state).
        /// </summary>
        private void PublishReport()
        {
            var view = FindFirstObjectByType<ResultView>(FindObjectsInactive.Include);
            if (view is null)
            {
                Debug.LogWarning(
                    "The Result scene has no ResultView, so nothing was published for the page.", this);
                return;
            }

            ResultReport.Publish(
                Summary, _scope.Container.Resolve<ResultUiState>(), view, gameObject.scene.name,
                StartError);

            // Stage 13: the summary screen's labels, measured for the browser suite.
            UiTextFitReport.Publish(gameObject.scene.name);
        }

        private void Update()
        {
            if (!_started || _departed || Presenter is null)
            {
                return;
            }

            SceneId? requested = Presenter.RequestedScene;
            if (requested is not SceneId target)
            {
                return;
            }

            if (_loader is null || !_loader.TryGo(target))
            {
                // Retried on the next frame: the only refusal a screen can hit here is a transition
                // that is still in flight (the other scenes are all in the build), and a retry that
                // is refused again costs one comparison per frame — far less than a queue would.
                StartError = "the move to " + target + " was refused: " + (_loader is null ? "no loader" : _loader.LastError);
                Debug.LogError(StartError, this);
                return;
            }

            _departed = true;
            StartError = string.Empty;

            if (target == SceneId.Camp)
            {
                // Restart. The run scope goes away here — after the loader accepted the move, so a
                // refused load cannot drop a run the player is still looking at — and the camp
                // builds and starts a new one exactly as it does at the beginning of the game.
                _runs.DisposeRun();
                Debug.Log("The run was ended on the summary screen; the camp will start a new one.", this);
                return;
            }

            if (Presenter.CompleteTitleTransition().IsFailure)
            {
                StartError = "the run refused the move back to the title.";
                Debug.LogError(StartError, this);
            }
        }
    }
}

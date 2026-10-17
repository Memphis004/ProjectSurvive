using System;
using ProjectSurvive.Core.Application.Flow;
using ProjectSurvive.Core.Application.Run;
using ProjectSurvive.Game.Presentation;
using ProjectSurvive.Game.Presentation.Flow;
using ProjectSurvive.Game.Presentation.Web;
using UnityEngine;
using VContainer;

namespace ProjectSurvive.Game.ResultScene
{
    /// <summary>
    /// Shows how the run ended, and is the one place that knows both ways out of it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Deliberately thin. Everything it composes is the scene's wiring (authored by
    /// <c>ProjectSetup</c> through the Editor API, AGENTS rule 11) or a service resolved from
    /// <see cref="ResultSceneScope"/>.
    /// </para>
    /// <para>
    /// <b>Restart</b> loads the Hub and then disposes the run scope, so the Hub starts a brand-new
    /// run — the seed's first encounter, no carried state. <b>Back to title</b> loads the title, and
    /// the scene loader disposes the run on arrival (that disposal is a property of the Title
    /// transition, not something this screen remembers).
    /// </para>
    /// <para>
    /// Both are committed before they are applied: the loader is asked first, and only a transition
    /// it accepted is followed by a change to the run.
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

        /// <summary>Why the last start refused, or an empty string.</summary>
        public string StartError { get; private set; } = string.Empty;

        /// <summary>True once the summary is on screen.</summary>
        public bool Started => _started;

        /// <summary>True once a way out has been committed.</summary>
        public bool Departed => _departed;

        private void Start() => TryStart();

        /// <summary>
        /// Brings the screen up: resolves the run, fills the state and publishes the marker.
        /// Idempotent, so a test may drive it without waiting for <c>Start</c>.
        /// </summary>
        /// <returns>False when there is no finished run to summarise.</returns>
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

            if (!_run.IsFinished)
            {
                // A Result screen with nothing to summarise: opened on its own, or reached with a run
                // that never ended. Showing "Run complete" for a live run would be a lie, and an empty
                // screen a dead end — the Hub is where a live run belongs.
                StartError = "the Result scene needs a finished run; this run is still in progress.";
                Debug.LogWarning(StartError, this);

                if (!_loader.TryGo(SceneId.Hub))
                {
                    StartError += " The Hub could not be entered either: " + _loader.LastError;
                    Debug.LogError(StartError, this);
                }

                return false;
            }

            StartError = string.Empty;
            WebGlBridge.PublishScene(gameObject.scene.name);
            Presenter.Begin();
            PublishReport();

            _started = true;
            return true;
        }

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
                _run, _scope.Container.Resolve<ResultUiState>(), view, gameObject.scene.name, StartError);
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
                string detail = _loader is null ? "no loader" : _loader.LastError;
                StartError = "the move to " + target + " was refused: " + detail;

                // `transition.in_progress` is the loader being busy with the move that brought this
                // scene up: the active scene changes one fade before the loader is free, so the first
                // press after arrival can land mid-transition. It is retried on the next frame; only a
                // refusal that will not clear on its own is worth an error.
                if (!string.Equals(detail, TransitionError.InProgress, StringComparison.Ordinal))
                {
                    Debug.LogError(StartError, this);
                }

                return;
            }

            _departed = true;
            StartError = string.Empty;

            if (target == SceneId.Hub)
            {
                // Restart: after the loader accepted the move, so a refused load cannot drop a run the
                // player is still looking at. The Hub builds and starts a new one.
                _runs.DisposeRun();
            }
        }
    }
}

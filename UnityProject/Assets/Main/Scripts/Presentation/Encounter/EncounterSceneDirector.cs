using System;
using ProjectSurvive.Core.Application.Flow;
using ProjectSurvive.Core.Application.Run;
using ProjectSurvive.Game.Presentation.Flow;
using ProjectSurvive.Game.Presentation.Web;
using UnityEngine;
using VContainer;

namespace ProjectSurvive.Game.Presentation.Encounter
{
    /// <summary>
    /// Brings the Encounter up, makes sure an encounter is actually running, and commits its way out.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A scene reached directly (a <c>?scene=Encounter</c> handover, a PlayMode fixture) has no live
    /// encounter, and the run refuses to end one that never began. Rather than special-casing that in
    /// the presenter, this director asks the run to begin one when the scene starts — the same call the
    /// Hub makes when the player departs, which is why both roads end in the same state.
    /// </para>
    /// <para>
    /// Deliberately thin, like the other directors: strings from the presenter, scene change from the
    /// loader.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class EncounterSceneDirector : MonoBehaviour
    {
        [SerializeField] private EncounterSceneScope _scope;

        private SceneLoader _loader;
        private RunSession _run;
        private EncounterPresenter _presenter;
        private bool _started;
        private bool _departed;

        /// <summary>The screen's logic (null until the scene has started).</summary>
        public EncounterPresenter Presenter => _presenter;

        /// <summary>Why the last start or move was refused, or an empty string.</summary>
        public string StartError { get; private set; } = string.Empty;

        /// <summary>True once the screen is up.</summary>
        public bool Started => _started;

        /// <summary>True once a way out has been committed.</summary>
        public bool Departed => _departed;

        private void Start() => TryStart();

        /// <summary>Brings the screen up. Idempotent, so a test may drive it without <c>Start</c>.</summary>
        public bool TryStart()
        {
            if (_started)
            {
                return true;
            }

            if (_scope is null || _scope.Container is null)
            {
                StartError = "EncounterSceneDirector has no built EncounterSceneScope.";
                Debug.LogError(StartError, this);
                return false;
            }

            _presenter = _scope.Container.Resolve<EncounterPresenter>();
            _loader = _scope.Container.Resolve<SceneLoader>();

            RunScopeOwner owner = _scope.Container.Resolve<RunScopeOwner>();
            _run = owner.RequireRun();
            owner.Flow.SyncTo(SceneId.Encounter);

            // A scene opened on its own (the Editor's scene list, a PlayMode fixture) has a run but
            // none that was ever started. Starting it here is the same call the title makes, so a
            // direct load reaches the same state as the loop does rather than failing on a rule.
            if (!_run.IsStarted)
            {
                var started = _run.Start();
                if (started.IsFailure)
                {
                    StartError = "the run could not start: " + started.Error.Code;
                    Debug.LogError(StartError, this);
                    return false;
                }
            }

            if (!_run.IsInEncounter)
            {
                var begun = _run.BeginEncounter();
                if (begun.IsFailure)
                {
                    StartError = "the encounter could not begin: " + begun.Error.Code;
                    Debug.LogError(StartError, this);
                    return false;
                }
            }

            _presenter.Refresh();
            WebGlBridge.PublishScene(gameObject.scene.name);

            _started = true;
            return true;
        }

        private void Update()
        {
            if (!_started || _departed || _presenter is null)
            {
                return;
            }

            SceneId? requested = _presenter.RequestedScene;
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
        }
    }
}

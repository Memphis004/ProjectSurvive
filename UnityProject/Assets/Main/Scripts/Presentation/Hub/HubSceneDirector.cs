using System;
using ProjectSurvive.Core.Application.Flow;
using ProjectSurvive.Game.Presentation.Flow;
using ProjectSurvive.Game.Presentation.Web;
using UnityEngine;
using VContainer;

namespace ProjectSurvive.Game.Presentation.Hub
{
    /// <summary>
    /// Brings the Hub up and is the one place that turns its two intents into scene changes.
    /// </summary>
    /// <remarks>
    /// Deliberately thin, like <c>TitleSceneDirector</c> and <c>ResultSceneDirector</c>: the screen's
    /// strings come from its presenter, the scene change from the <see cref="SceneLoader"/>. The run's
    /// own state was already changed by the presenter — this class only commits the move, and retries a
    /// refusal on the next frame because the only refusals here are transient ones.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class HubSceneDirector : MonoBehaviour
    {
        [SerializeField] private HubSceneScope _scope;

        private SceneLoader _loader;
        private HubPresenter _presenter;
        private bool _started;
        private bool _departed;

        /// <summary>The screen's logic (null until the scene has started).</summary>
        public HubPresenter Presenter => _presenter;

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
                StartError = "HubSceneDirector has no built HubSceneScope.";
                Debug.LogError(StartError, this);
                return false;
            }

            _presenter = _scope.Container.Resolve<HubPresenter>();
            _loader = _scope.Container.Resolve<SceneLoader>();

            // "Which scene is on screen" is a fact the flow is told, not one it guesses.
            RunScopeOwner owner = _scope.Container.Resolve<RunScopeOwner>();
            owner.Flow.SyncTo(SceneId.Hub);

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

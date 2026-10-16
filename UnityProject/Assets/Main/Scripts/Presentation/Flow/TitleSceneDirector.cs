using System;
using ProjectSurvive.Core.Domain.Run;
using ProjectSurvive.Game.Presentation.Web;
using UnityEngine;
using VContainer;

namespace ProjectSurvive.Game.Presentation.Flow
{
    /// <summary>
    /// Brings the Title scene up, and is the one place that knows how the game leaves it: the player
    /// presses เริ่มเล่น, the presenter starts a run, and this asks the <see cref="SceneLoader"/> for
    /// the camp.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Deliberately thin, like <c>CampSceneDirector</c>, <c>BattleSceneDirector</c> and
    /// <c>ResultSceneDirector</c>. Everything it composes is either the scene's wiring (authored by
    /// <c>ProjectSetup</c> through the Editor API, AGENTS rule 11) or a service resolved from the
    /// scene's own scope. It decides nothing about the game: the run is started by the presenter's
    /// Application call, and the scene change is the loader's.
    /// </para>
    /// <para>
    /// <b>Boot stops here on purpose.</b> The boot scene runs its self-check and hands over to the
    /// title, and the title waits for the player: no screen walks into the camp on its own. That is
    /// the whole reason this director exists — until Stage 10d nothing on the title screen could leave
    /// it, so a player (or a browser test) that opened the game sat there. See
    /// <c>docs/adr/0022-the-title-starts-a-run.md</c>.
    /// </para>
    /// <para>
    /// <b>The press is committed before it is applied.</b> The presenter only records the intent; the
    /// loader is asked first, and a refusal (a transition already in flight — the typed
    /// <see cref="TransitionError.InProgress"/> — or a scene that is not in the build) leaves the
    /// screen as it was rather than half-moved. The request is retried on the next frame because a
    /// refusal here can only be transient, and the retry is free: it is one comparison per frame until
    /// the fade takes over. That is the same shape the summary screen's two ways out take.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class TitleSceneDirector : MonoBehaviour
    {
        [SerializeField] private RootLifetimeScope _scope;

        [Tooltip("Publish __projectSurviveTitle. Only a browser test reads it.")]
        [SerializeField] private bool _publishReport = true;

        private SceneLoader _loader;
        private TitlePresenter _presenter;
        private TitleView _view;
        private bool _started;
        private bool _departed;
        private int _publishedWidth = -1;
        private int _publishedHeight = -1;
        private Vector2 _publishedPoint;
        private bool _hasPublishedPoint;

        /// <summary>Why the last refusal was refused, or an empty string.</summary>
        private string _lastRefusal = string.Empty;

        /// <summary>The screen's logic, or null until <see cref="TryStart"/>.</summary>
        public TitlePresenter Presenter => _presenter;

        /// <summary>Why the scene could not start, or the last refusal, or an empty string.</summary>
        public string StartError { get; private set; } = string.Empty;

        /// <summary>True once the title screen is up and the page has its report.</summary>
        public bool Started => _started;

        /// <summary>True once a way out has been accepted by the loader.</summary>
        public bool Departed => _departed;

        private void Start() => TryStart();

        /// <summary>
        /// Brings the screen up: writes the strings the player reads, says which scene is on screen and
        /// publishes the page's report. Idempotent, so a test may drive it without waiting for
        /// <c>Start</c>.
        /// </summary>
        /// <returns>False when the scene has no built scope to resolve from.</returns>
        public bool TryStart()
        {
            if (_started)
            {
                return true;
            }

            if (_scope is null || _scope.Container is null)
            {
                StartError = "TitleSceneDirector has no built RootLifetimeScope.";
                Debug.LogError(StartError, this);
                return false;
            }

            _presenter = _scope.Container.Resolve<TitlePresenter>();
            _loader = _scope.Container.Resolve<SceneLoader>();

            // Found once, like every other scene director does with its own views: `Update` compares the
            // control's position every frame, and a scene search per frame would be the expensive part.
            _view = FindFirstObjectByType<TitleView>(FindObjectsInactive.Include);

            // The strings first: the report below is meant to carry what the player is about to read,
            // not the empty values the state starts with (the same order ResultSceneDirector uses).
            _presenter.Refresh();

            // The scene marker, published by the scene that is on screen. Without it a handover into
            // the title is invisible to the log a bug report quotes, and to the browser suite.
            WebGlBridge.PublishScene(gameObject.scene.name);

            if (_publishReport)
            {
                PublishReport();
            }

            _started = true;
            return true;
        }

        /// <summary>True while the document was written for a canvas size the screen no longer has.</summary>
        private bool _hasStaleSize =>
            Screen.width != _publishedWidth || Screen.height != _publishedHeight;

        /// <summary>
        /// True while the control is not where the published document says it is — the canvas catching
        /// up with a size the document already carries.
        /// </summary>
        private bool HasMovedSincePublish() =>
            _hasPublishedPoint
            && TryReadStartPoint(out Vector2 live)
            && live != _publishedPoint;

        /// <summary>Where the start control is on screen right now, when the scene has a view to ask.</summary>
        private bool TryReadStartPoint(out Vector2 screen)
        {
            TitleView view = _view;
            if (view is null || !view.TryGetButtonScreenPoint(TitleView.TitleButton.Start, out screen))
            {
                screen = default;
                return false;
            }

            return true;
        }

        /// <summary>
        /// Hands the screen's state and its control to the page, and says so when the scene has no
        /// <see cref="TitleView"/> to measure it in (a wiring fault, not a display state).
        /// </summary>
        private void PublishReport()
        {
            // The size is recorded before anything can fail: the report carries a point in the canvas'
            // pixels, so the size it was written for is what decides whether it needs rewriting.
            _publishedWidth = Screen.width;
            _publishedHeight = Screen.height;
            _hasPublishedPoint = TryReadStartPoint(out _publishedPoint);

            if (_view is null)
            {
                Debug.LogWarning(
                    "The Title scene has no TitleView, so nothing was published for the page.", this);
                return;
            }

            TitleReport.Publish(_scope.Container.Resolve<UiState>(), _view, gameObject.scene.name, StartError);
        }

        private void Update()
        {
            if (!_started || _presenter is null)
            {
                return;
            }

            // A point in canvas pixels goes stale the moment the canvas changes size, and the player's
            // own fullscreen control resizes the canvas rather than the window — so the document is
            // rewritten when it does. Two things have to be true for the rewrite to be honest, and both
            // cost one comparison per frame while nothing moves:
            //
            // * the size is compared, because a document written for 960×600 aims a click at where the
            //   control was;
            // * the point is compared too, because `Screen` reports the new size in the frame the
            //   element changed while the canvas lays its widgets out of it during that frame's own
            //   render — so the first rewrite after a resize is a frame early, and would otherwise be
            //   the last word. This was measured, not assumed: the browser check read a document with
            //   1280 in `canvasWidth` and the control's 960×600 position in `buttons[0]`, 134 px from
            //   where the canvas' centre had moved to.
            if (_publishReport && (_hasStaleSize || HasMovedSincePublish()))
            {
                PublishReport();
            }

            if (_departed)
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
                // The refusal is typed and it is not silent: a second press while the move is in
                // flight is refused by the presenter (one request at a time) and a loader that is
                // already busy refuses here. Retrying costs one comparison per frame and is what
                // makes a refusal that was only a race recover on its own.
                string detail = _loader is null ? "no loader" : _loader.LastError;
                StartError = "the move to " + target + " was refused: " + detail;

                if (!string.Equals(detail, _lastRefusal, StringComparison.Ordinal))
                {
                    _lastRefusal = detail;
                    Debug.LogError(StartError, this);
                }

                return;
            }

            _departed = true;
            _lastRefusal = string.Empty;
            StartError = string.Empty;
            Debug.Log("The title screen is starting a run: the camp is next.", this);
        }
    }
}

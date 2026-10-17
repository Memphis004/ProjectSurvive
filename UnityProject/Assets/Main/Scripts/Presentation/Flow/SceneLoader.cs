using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using ProjectSurvive.Core.Application.Flow;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ProjectSurvive.Game.Presentation.Flow
{
    /// <summary>
    /// The one way the game changes scene: a single transition at a time, behind a fade, with a
    /// typed refusal instead of a half-applied move.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Stage 10's loop crosses scenes four times per floor (Camp → Battle → Camp → …), and every
    /// crossing is a chance to leave something behind or to run two of them at once. So the loader
    /// is deliberately narrow:
    /// </para>
    /// <list type="bullet">
    /// <item><description>
    /// <b>One transition at a time.</b> A second <see cref="TryGo"/> while one is in flight is
    /// refused with <see cref="TransitionError.InProgress"/> and changes nothing — no queue, no
    /// cancellation, no two scenes fighting over the active one.
    /// </description></item>
    /// <item><description>
    /// <b>Loading and error recovery.</b> The target is checked against the build settings
    /// <i>before</i> the fade (a scene that is not in the build cannot be loaded, and the caller
    /// finds out while the screen is still the one the player was looking at), and a failure of
    /// the load itself unfades and records <see cref="LastError"/> rather than leaving the player
    /// on a black screen.
    /// </description></item>
    /// <item><description>
    /// <b>Title means no run.</b> Arriving at <see cref="SceneId.Title"/> disposes the run scope:
    /// "กลับ Title แล้ว dispose Run scope" is a property of the transition, not something each
    /// screen has to remember. That is also what makes the acceptance check
    /// "Title ไม่มี Run scope เก่าค้าง" a one-line assertion.
    /// </description></item>
    /// </list>
    /// <para>
    /// The fade is built at runtime on its own <c>DontDestroyOnLoad</c> canvas rather than authored
    /// into every scene: one overlay for the whole game, nothing to keep in step across five
    /// scenes, and scenes stay free of YAML (AGENTS rule 11) beyond what the Editor API produced.
    /// </para>
    /// </remarks>
    public sealed class SceneLoader : IDisposable
    {
        /// <summary>Sorting order of the fade overlay: above every HUD the scenes author.</summary>
        public const int FadeSortingOrder = 3000;

        private readonly RunScopeOwner _run;
        private readonly float _fadeSeconds;
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();

        private GameObject _fadeObject;
        private CanvasGroup _fade;
        private GameObject _fallbackEvents;
        private UniTask _transition = UniTask.CompletedTask;
        private bool _busy;
        private SceneId _target;
        private bool _disposed;

        /// <summary>Creates the loader for a run owner.</summary>
        /// <param name="run">The run scope to dispose when the title is reached.</param>
        /// <param name="fadeSeconds">Length of one half of the fade, in unscaled seconds.</param>
        /// <exception cref="ArgumentNullException"><paramref name="run"/> is null.</exception>
        public SceneLoader(RunScopeOwner run, float fadeSeconds = 0.2f)
        {
            _run = run is null ? throw new ArgumentNullException(nameof(run)) : run;
            _fadeSeconds = fadeSeconds < 0f ? 0f : fadeSeconds;
        }

        /// <summary>True while a transition is in flight.</summary>
        public bool IsTransitioning => _busy;

        /// <summary>The scene the current transition is going to; stale when none is in flight.</summary>
        public SceneId Target => _target;

        /// <summary>Why the last attempt was refused or failed, or an empty string.</summary>
        public string LastError { get; private set; } = string.Empty;

        /// <summary>The transition in flight, for a caller that wants to await it (a test, or a
        /// screen that must not draw until the scene underneath it is real).</summary>
        public UniTask Transition => _transition;

        /// <summary>
        /// Begins a move to <paramref name="target"/>.
        /// </summary>
        /// <returns>
        /// False — with nothing changed — while another transition is in flight, or when the target
        /// is not in the build settings. <see cref="LastError"/> names which.
        /// </returns>
        public bool TryGo(SceneId target)
        {
            if (_disposed)
            {
                LastError = TransitionError.Disposed;
                return false;
            }

            if (_busy)
            {
                // One at a time, and the refusal is not silent: a screen that asks twice is a bug
                // the caller should be able to see rather than a race the loader should absorb.
                LastError = TransitionError.InProgress;
                return false;
            }

            string sceneName = SceneName(target);
            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                // The "loading/error recovery" half, checked before anything is hidden: a scene
                // that is not in the build settings cannot be loaded, and the player keeps looking
                // at the screen they were on. (Stage 10a has no Result scene yet — asking for it
                // lands here, which is exactly how the recovery path is exercised.)
                LastError = TransitionError.SceneMissing + ":" + sceneName;
                Debug.LogError(
                    "SceneLoader cannot load '" + sceneName + "': it is not in the build settings.",
                    _fadeObject);
                return false;
            }

            _busy = true;
            _target = target;
            LastError = string.Empty;
            _transition = GoAsync(sceneName, target, _lifetime.Token);
            return true;
        }

        /// <summary>The Unity scene name a <see cref="SceneId"/> loads. The enum's name is the
        /// scene's name (<c>Title</c>, <c>Camp</c>, <c>Battle</c>, <c>Result</c>).</summary>
        public static string SceneName(SceneId id) => id.ToString();

        /// <inheritdoc />
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _lifetime.Cancel();
            _lifetime.Dispose();

            if (_fadeObject is not null)
            {
                UnityEngine.Object.Destroy(_fadeObject);
                _fadeObject = null;
                _fade = null;
            }
        }

        private async UniTask GoAsync(string sceneName, SceneId target, CancellationToken token)
        {
            try
            {
                await FadeToAsync(1f, token);

                AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
                if (operation is null)
                {
                    LastError = TransitionError.LoadFailed + ":" + sceneName;
                    Debug.LogError("SceneLoader: the load of '" + sceneName + "' was refused.", _fadeObject);
                    await FadeToAsync(0f, token);
                    return;
                }

                while (!operation.isDone)
                {
                    await UniTask.Yield(token);
                }

                if (target == SceneId.Title)
                {
                    // "กลับ Title แล้ว dispose Run scope": the title is the one screen that has no
                    // run, so the transition is where the run ends. Doing it after the load (not
                    // before) means a refused load cannot drop a live run.
                    _run.DisposeRun();
                }

                // One frame with the new scene rendered before the fade lifts, so the player never
                // sees a frame of the un-faded previous scene.
                await UniTask.Yield(token);
                await FadeToAsync(0f, token);
            }
            catch (OperationCanceledException)
            {
                // The loader's owner went away mid-transition (the app is closing); there is
                // nothing left to restore the fade for.
            }
            catch (Exception exception)
            {
                LastError = exception.GetType().Name;
                Debug.LogException(exception);
            }
            finally
            {
                // The fallback EventSystem lives only while a transition does. Keeping it would
                // leave the overlay's own EventSystem in the scene the transition arrived at, which
                // already authors one — Unity then warns "There are 2 event systems in the scene"
                // every frame for the rest of the session, and the run is 20 scenes long.
                if (_fallbackEvents is not null)
                {
                    UnityEngine.Object.Destroy(_fallbackEvents);
                    _fallbackEvents = null;
                }

                _busy = false;
            }
        }

        private async UniTask FadeToAsync(float alpha, CancellationToken token)
        {
            EnsureOverlay();

            float from = _fade.alpha;
            if (Mathf.Approximately(from, alpha))
            {
                Apply(alpha);
                return;
            }

            float elapsed = 0f;
            while (elapsed < _fadeSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = _fadeSeconds <= 0f ? 1f : Mathf.Clamp01(elapsed / _fadeSeconds);
                _fade.alpha = Mathf.Lerp(from, alpha, t);
                await UniTask.Yield(token);
            }

            Apply(alpha);
        }

        private void Apply(float alpha)
        {
            _fade.alpha = alpha;

            // Only a fully opaque overlay eats input: a partly faded one would make the fade feel
            // like an unresponsive frame.
            _fade.blocksRaycasts = alpha >= 1f;
        }

        /// <summary>
        /// Builds the fade overlay on first use: one canvas above everything, a black image that
        /// covers it, and the raycaster that makes it swallow a stray click mid-transition.
        /// </summary>
        private void EnsureOverlay()
        {
            if (_fadeObject is not null && _fade is not null)
            {
                return;
            }

            _fadeObject = new GameObject("SceneFade");
            UnityEngine.Object.DontDestroyOnLoad(_fadeObject);

            var canvas = _fadeObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = FadeSortingOrder;
            _fadeObject.AddComponent<GraphicRaycaster>();

            _fade = _fadeObject.AddComponent<CanvasGroup>();
            _fade.alpha = 0f;
            _fade.blocksRaycasts = false;

            var imageObject = new GameObject("Fade", typeof(RectTransform), typeof(Image));
            imageObject.transform.SetParent(_fadeObject.transform, worldPositionStays: false);

            var rect = (RectTransform)imageObject.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var image = imageObject.GetComponent<Image>();
            image.color = Color.black;
            image.raycastTarget = true;

            // A scene with no EventSystem would still fade, but nothing would be blocked; the
            // scenes author one, and this covers the one that does not (Boot, which has no UI and
            // hands over to the title). It is torn down when the transition ends — see GoAsync.
            if (_fallbackEvents is null && UnityEngine.Object.FindFirstObjectByType<EventSystem>() is null)
            {
                _fallbackEvents = new GameObject("SceneFadeEventSystem");
                _fallbackEvents.transform.SetParent(_fadeObject.transform, worldPositionStays: false);
                _fallbackEvents.AddComponent<EventSystem>();
            }
        }
    }

    /// <summary>The stable, machine-readable reasons a transition was refused or failed.</summary>
    public static class TransitionError
    {
        /// <summary>A transition is already in flight ("transition รับได้ทีละรายการ").</summary>
        public const string InProgress = "transition.in_progress";

        /// <summary>The target scene is not in the build settings.</summary>
        public const string SceneMissing = "transition.scene_missing";

        /// <summary>The engine refused the load itself.</summary>
        public const string LoadFailed = "transition.load_failed";

        /// <summary>The loader has been disposed.</summary>
        public const string Disposed = "transition.disposed";
    }
}

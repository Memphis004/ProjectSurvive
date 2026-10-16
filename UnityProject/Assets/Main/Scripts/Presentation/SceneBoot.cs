using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using ProjectSurvive.Game.Presentation.Smoke;
using ProjectSurvive.Game.Presentation.Web;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;

namespace ProjectSurvive.Game.Presentation
{
    /// <summary>
    /// The Boot scene's only job: prove the build works, then hand over to Title.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Boot runs the smoke suite <b>before</b> any gameplay scene, so a broken
    /// serializer or a missing config table fails on the first frame with a
    /// readable verdict instead of surfacing later as a mysterious crash. The
    /// verdict is both logged and published to the page
    /// (<see cref="WebGlBridge"/>), which is what makes a WebGL build testable.
    /// </para>
    /// <para>
    /// Cancellation is tied to this component's lifetime (AGENTS rule 9): the token
    /// is cancelled in <c>OnDestroy</c>, so a scene change or a domain reload cannot
    /// leave an await running against destroyed objects. <c>async void</c> is not
    /// used — the method returns <see cref="UniTaskVoid"/> and logs its own
    /// failures.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class SceneBoot : MonoBehaviour
    {
        [SerializeField] private RootLifetimeScope _scope;

        [Tooltip("Load the Title scene once the smoke suite has run.")]
        [SerializeField] private bool _continueToTitleScene = true;

        [SerializeField] private string _titleSceneName = "Title";

        [Tooltip("Scenes the page URL may request with ?scene=<name>. Only these names are accepted.")]
        [SerializeField] private string[] _urlSceneOverrides = { "ThaiTextSpike", "Battle" };

        private CancellationTokenSource _lifetime;

        /// <summary>
        /// The most recent boot report. This is the in-process mirror of the page
        /// global the WebGL bridge publishes, and the seam the PlayMode tests read
        /// instead of scraping the log.
        /// </summary>
        public static SmokeReport LastReport { get; private set; }

        /// <summary>
        /// How many boots have finished the suite. Together with
        /// <see cref="LastReport"/> this lets a test prove the report it reads came
        /// from the run it triggered, not from an earlier scene load.
        /// </summary>
        public static int CompletedBootCount { get; private set; }

        private void Awake() => _lifetime = new CancellationTokenSource();

        private void Start()
        {
            // The shell scope outlives every scene (Stage 10a): Title, Camp, Battle and Result are
            // its children, so the run it owns survives a scene load and the wallet is not rebuilt
            // every time the player walks back to the camp. This runs before the first handover,
            // because Boot is what performs it.
            if (_scope is not null)
            {
                _scope.PersistAcrossScenes();
            }

            BootAsync(_lifetime.Token).Forget();
        }

        private void OnDestroy()
        {
            if (_lifetime is null)
            {
                return;
            }

            _lifetime.Cancel();
            _lifetime.Dispose();
            _lifetime = null;
        }

        private async UniTaskVoid BootAsync(CancellationToken token)
        {
            try
            {
                if (_scope is null || _scope.Container is null)
                {
                    const string message = "SceneBoot has no built RootLifetimeScope - the container cannot resolve anything.";
                    Debug.LogError(message, this);
                    PublishFailed(message);
                    return;
                }

                SmokeRunner runner = _scope.Container.Resolve<SmokeRunner>();
                SmokeReport report = await runner.RunAsync(token);

                LastReport = report;
                CompletedBootCount++;
                WebGlBridge.PublishSmokeReport(report.ToJson());
                _scope.Container.Resolve<TitlePresenter>().ReportSmoke(report);

                if (!report.AllPassed)
                {
                    Debug.LogError(report.ToLog());
                }

                string nextScene = ResolveNextScene();
                if (_continueToTitleScene && !string.IsNullOrEmpty(nextScene))
                {
                    await SceneManager.LoadSceneAsync(nextScene).ToUniTask(cancellationToken: token);
                }
            }
            catch (OperationCanceledException)
            {
                // The boot object went away; there is nothing left to report to.
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                PublishFailed(exception.GetType().Name + ": " + exception.Message);
            }
        }

        /// <summary>
        /// Which scene Boot hands over to. Normally the title screen; a page URL may
        /// ask for an allow-listed diagnostic scene instead
        /// (<c>?scene=ThaiTextSpike</c>).
        /// </summary>
        /// <remarks>
        /// A browser has no other way to choose a scene, and the Thai text spike only
        /// produces meaningful numbers inside the WebGL player (ADR 0003/0008), so
        /// the browser test needs a way in. The allow-list is what keeps an arbitrary
        /// URL from loading something the build never meant to expose.
        /// </remarks>
        private string ResolveNextScene()
        {
            string requested = ParseQueryValue(ReadCurrentUrl(), "scene");
            if (!string.IsNullOrEmpty(requested) && IsAllowListed(requested))
            {
                return requested;
            }

            return _titleSceneName;
        }

        private bool IsAllowListed(string sceneName)
        {
            if (_urlSceneOverrides is null)
            {
                return false;
            }

            for (int i = 0; i < _urlSceneOverrides.Length; i++)
            {
                if (string.Equals(_urlSceneOverrides[i], sceneName, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// The page URL, or an empty string where there is none. The accessor is not
        /// meaningful off the Web player, so it is guarded rather than assumed.
        /// </summary>
        private static string ReadCurrentUrl()
        {
            try
            {
                return Application.absoluteURL ?? string.Empty;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Reads one query parameter from a URL (the part after <c>?</c>). Pure, so a
        /// test can exercise it without a browser; returns an empty string when the
        /// key is absent.
        /// </summary>
        public static string ParseQueryValue(string url, string key)
        {
            if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(key))
            {
                return string.Empty;
            }

            int query = url.IndexOf('?');
            if (query < 0)
            {
                return string.Empty;
            }

            string prefix = key + "=";
            int cursor = query + 1;
            while (cursor < url.Length)
            {
                int end = url.IndexOf('&', cursor);
                if (end < 0)
                {
                    end = url.Length;
                }

                int length = end - cursor;
                if (length > prefix.Length && string.CompareOrdinal(url, cursor, prefix, 0, prefix.Length) == 0)
                {
                    return url.Substring(cursor + prefix.Length, length - prefix.Length);
                }

                cursor = end + 1;
            }

            return string.Empty;
        }

        private static void PublishFailed(string message)
        {
            LastReport = FailedReportObject(message);
            WebGlBridge.PublishSmokeReport(LastReport.ToJson());
        }

        /// <summary>
        /// A report for the case where no probe could run at all. Without it a
        /// browser test would wait forever for a global that never appears.
        /// </summary>
        private static SmokeReport FailedReportObject(string message)
        {
            var report = new SmokeReport("BootFailure", string.Empty);
            report.Add(ProbeResult.Fail("boot", message));
            return report;
        }
    }
}

using ProjectSurvive.Game.Presentation.Web;
using UnityEngine;
using VContainer;

namespace ProjectSurvive.Game.Presentation.Thai
{
    /// <summary>
    /// Runs both Thai text stacks over the same strings and publishes the numbers.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Both stacks measure identical strings from the same font on the same machine,
    /// so the ADR's decision can cite a real comparison instead of a preference.
    /// The report is logged for the Editor and published to the page for a WebGL
    /// run, which is the only place the Web numbers are meaningful (ADR 0003).
    /// </para>
    /// <para>
    /// Deliberately a MonoBehaviour with no async work: the spike must not depend on
    /// a frame loop it is trying to measure.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class ThaiSpikeReporter : MonoBehaviour
    {
        [SerializeField] private RootLifetimeScope _scope;
        [SerializeField] private ThaiSpikeTmpView _tmpView;
        [SerializeField] private ThaiSpikeToolkitView _toolkitView;

        [Tooltip("Font file the spike renders with; recorded in the report for provenance.")]
        [SerializeField] private string _fontName = "Sarabun-Regular.ttf";

        private ThaiSpikeReport _report;

        /// <summary>The report from the last run, or null before <c>Start</c>.</summary>
        public ThaiSpikeReport Report => _report;

        private void Start() => Run();

        /// <summary>
        /// Measures both stacks. Public so a PlayMode test can drive it directly.
        /// </summary>
        public ThaiSpikeReport Run()
        {
            if (_scope is null || _scope.Container is null)
            {
                Debug.LogError("ThaiSpikeReporter needs a built RootLifetimeScope.", this);
                return null;
            }

            ThaiTextSpike spike = _scope.Container.Resolve<ThaiTextSpike>();
            string[] texts = spike.Resolve();

            var report = new ThaiSpikeReport(DetectPlatform(), _fontName);
            _tmpView?.AppendRows(texts, report);
            _toolkitView?.AppendRows(texts, report);

            _report = report;

            Debug.Log(report.ToLog());
            WebGlBridge.Publish(WebGlBridge.ThaiSpikeGlobal, report.ToJson());

            // Same scene marker the title screen publishes, so a browser test can tell
            // "the spike scene is running" from "the spike global happened to exist".
            WebGlBridge.PublishScene(gameObject.scene.name);

            return report;
        }

        private static string DetectPlatform()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return "WebGLPlayer";
#elif UNITY_EDITOR
            return "Editor";
#else
            return UnityEngine.Application.platform.ToString();
#endif
        }
    }
}

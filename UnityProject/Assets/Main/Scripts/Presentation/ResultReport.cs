using ProjectSurvive.Core.Application.Run;
using ProjectSurvive.Game.Presentation.Web;
using UnityEngine;

namespace ProjectSurvive.Game.Presentation
{
    /// <summary>
    /// Publishes what the summary screen shows, and where its two controls are, so a browser test can
    /// read the loop's last leg and press its way out of it.
    /// </summary>
    /// <remarks>
    /// Every string in here was composed by <see cref="ResultPresenter"/> from the localization table,
    /// so a test asserts on the words the player reads rather than on a second copy of them. The
    /// document is built once, when the screen starts: this screen is a static column.
    /// </remarks>
    public static class ResultReport
    {
        /// <summary>On-screen name of the page global that carries the summary screen.</summary>
        public const string Global = "__projectSurviveResult";

        /// <summary>The names the report uses for the screen's controls.</summary>
        public static class Control
        {
            /// <summary>The control that starts a new run.</summary>
            public const string Restart = "restart";

            /// <summary>The control that leaves for the title.</summary>
            public const string BackToTitle = "title";
        }

        /// <summary>The most recent report, in-process. The tests read this.</summary>
        public static string LastJson { get; private set; } = string.Empty;

        /// <summary>How many reports have been published.</summary>
        public static int PublishCount { get; private set; }

        /// <summary>Clears the mirror, so a test starts from a known empty state.</summary>
        public static void Reset()
        {
            LastJson = string.Empty;
            PublishCount = 0;
        }

        /// <summary>Writes the report and hands it to the page. Returns the JSON it published.</summary>
        public static string Publish(
            RunSession run, ResultUiState state, ResultView view, string sceneName, string startError)
        {
            var report = new JsonReportWriter();
            report.BeginObject();

            report.Write("scene", sceneName ?? string.Empty);
            report.Write("startError", startError ?? string.Empty);
            report.Write("outcome", run.Outcome.ToString());
            report.Write("finished", run.IsFinished);
            report.Write("encounters", run.EncounterIndex);
            report.Write("seed", run.Seed.ToString());

            report.Write("headline", state.Headline.Value);
            report.Write("summaryLine", state.SummaryLine.Value);
            report.Write("seedLine", state.SeedLine.Value);
            report.Write("restartLabel", state.RestartLabel.Value);
            report.Write("titleLabel", state.TitleLabel.Value);

            report.Write("canvasWidth", Screen.width);
            report.Write("canvasHeight", Screen.height);

            report.BeginArrayProperty("buttons");
            AddControl(report, view, ResultView.ResultButton.Restart, Control.Restart);
            AddControl(report, view, ResultView.ResultButton.BackToTitle, Control.BackToTitle);
            report.EndArray();

            report.EndObject();

            string json = report.ToString();
            LastJson = json;
            PublishCount++;
            WebGlBridge.Publish(Global, json);
            return json;
        }

        /// <summary>Writes one control's centre, or nothing when the scene has not wired it.</summary>
        private static void AddControl(
            JsonReportWriter report, ResultView view, ResultView.ResultButton button, string name)
        {
            if (view is null || !view.TryGetButtonScreenPoint(button, out Vector2 screen))
            {
                return;
            }

            report.BeginObject();
            report.Write("name", name);
            report.Write("x", screen.x);
            report.Write("y", screen.y);
            report.EndObject();
        }
    }
}

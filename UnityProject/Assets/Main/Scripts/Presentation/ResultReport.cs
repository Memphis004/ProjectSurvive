using ProjectSurvive.Core.Domain.Run;
using ProjectSurvive.Game.Presentation.Web;
using UnityEngine;

namespace ProjectSurvive.Game.Presentation
{
    /// <summary>
    /// Publishes what the summary screen shows, and where its two controls are, so a browser test can
    /// read the last leg of the loop and press its way out of it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The same reason every other report exists (Stage 03's smoke verdict, Stage 08's battle
    /// progress, Stage 09's camp state): a WebGL player cannot run Unity's test framework, so the only
    /// place "the run ends on a screen that reports it, and Restart starts a new one" can be observed
    /// in a built player is the page the player published. This is the leg Stage 10 could not cover —
    /// the browser suite's floor 1 is won by a standing player, so the suite never reached the summary.
    /// </para>
    /// <para>
    /// <b>One document, not two.</b> The camp splits its state from its layout because the camp's
    /// layout changes as the player walks and as the bag is dragged, and rebuilding it four times a
    /// second during a drag would be the very cost Stage 09 measured. This screen is a static column
    /// that is built once, so its strings and its controls travel together and are written when the
    /// screen starts.
    /// </para>
    /// <para>
    /// Every string in here was composed by <see cref="ResultPresenter"/> from the localization table
    /// — including the button captions — so a browser test asserts on the words the player reads
    /// rather than on a second copy of them.
    /// </para>
    /// </remarks>
    public static class ResultReport
    {
        /// <summary>On-screen name of the page global that carries the summary screen.</summary>
        public const string Global = "__projectSurviveResult";

        /// <summary>
        /// The names the report uses for the screen's controls, so a browser test presses the control
        /// by name instead of at a coordinate it guessed from a screenshot.
        /// </summary>
        public static class Control
        {
            /// <summary>The control that starts a new run (<c>Restart</c>).</summary>
            public const string Restart = "restart";

            /// <summary>The control that leaves for the title (กลับ Title).</summary>
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

        /// <summary>
        /// Writes the report and hands it to the page. Returns the JSON it published.
        /// </summary>
        /// <param name="summary">What the run reports about itself.</param>
        /// <param name="state">The strings the screen is showing, as the presenter filled them.</param>
        /// <param name="view">The screen, asked where its controls are.</param>
        /// <param name="sceneName">The scene on screen, so the report can be checked against the marker.</param>
        /// <param name="startError">Why the screen could not start, or an empty string.</param>
        public static string Publish(
            in RunSummary summary, ResultUiState state, ResultView view, string sceneName,
            string startError)
        {
            var report = new JsonReportWriter();
            report.BeginObject();

            report.Write("scene", sceneName ?? string.Empty);
            report.Write("startError", startError ?? string.Empty);

            // The run's own answer, not the screen's: `outcome` is what the rule says happened and
            // `victory` is the same fact as the screen reads it, so a headline that disagrees with the
            // outcome is visible in the document rather than hidden behind a formatted string.
            report.Write("outcome", summary.Outcome.ToString());
            report.Write("finished", summary.IsFinished);
            report.Write("victory", summary.IsVictory);
            report.Write("floor", summary.FloorReached);
            report.Write("totalFloors", summary.TotalFloors);
            report.Write("floorsCleared", summary.FloorsCleared);
            report.Write("kills", summary.GhostsDefeated);
            report.Write("bia", summary.Bia);
            report.Write("hp", summary.Hp);
            report.Write("maxHp", summary.MaxHp);
            report.Write("seed", summary.Seed.ToString());

            report.Write("headline", state.Headline.Value);
            report.Write("floorLine", state.FloorLine.Value);
            report.Write("killsLine", state.KillsLine.Value);
            report.Write("biaLine", state.BiaLine.Value);
            report.Write("hpLine", state.HpLine.Value);
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

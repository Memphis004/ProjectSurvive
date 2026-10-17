using ProjectSurvive.Game.Presentation.Web;
using UnityEngine;

namespace ProjectSurvive.Game.Presentation.Encounter
{
    /// <summary>
    /// Publishes what the Encounter screen shows, and where its two controls are, so a browser test can
    /// settle an encounter the way a player does.
    /// </summary>
    /// <remarks>
    /// Same contract as <see cref="HubReport"/>: strings from the presenter, points from the view, and the
    /// game's own answer (the scene marker and the next screen's report) is what a test asserts on.
    /// </remarks>
    public static class EncounterReport
    {
        /// <summary>On-screen name of the page global that carries the Encounter screen.</summary>
        public const string Global = "__projectSurviveEncounter";

        /// <summary>The names the report uses for the screen's controls.</summary>
        public static class Control
        {
            /// <summary>The control that clears the encounter.</summary>
            public const string Win = "win";

            /// <summary>The control that loses it.</summary>
            public const string Lose = "lose";
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
        /// <param name="state">The strings the screen is showing, as the presenter filled them.</param>
        /// <param name="view">The screen, asked where its controls are.</param>
        /// <param name="sceneName">The scene on screen, so the report can be checked against the marker.</param>
        public static string Publish(EncounterUiState state, EncounterView view, string sceneName)
        {
            var report = new JsonReportWriter();
            report.BeginObject();

            report.Write("scene", sceneName ?? string.Empty);
            report.Write("title", state.Title.Value);
            report.Write("progressLine", state.ProgressLine.Value);
            report.Write("winLabel", state.WinLabel.Value);
            report.Write("loseLabel", state.LoseLabel.Value);

            report.Write("canvasWidth", Screen.width);
            report.Write("canvasHeight", Screen.height);

            report.BeginArrayProperty("buttons");
            AddControl(report, view, EncounterView.EncounterButton.Win, Control.Win);
            AddControl(report, view, EncounterView.EncounterButton.Lose, Control.Lose);
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
            JsonReportWriter report, EncounterView view, EncounterView.EncounterButton button, string name)
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

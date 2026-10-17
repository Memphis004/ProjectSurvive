using ProjectSurvive.Game.Presentation.Web;
using UnityEngine;

namespace ProjectSurvive.Game.Presentation.Hub
{
    /// <summary>
    /// Publishes what the Hub screen shows, and where its two controls are, so a browser test can
    /// leave the screen the way a player does — by pressing a control the scene drew.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Same contract as <see cref="TitleReport"/> and <see cref="ResultReport"/>: a WebGL player cannot
    /// run Unity's test framework, so the only place "the loop can be walked from the Hub" is observable
    /// in a built player is the page the player published.
    /// </para>
    /// <para>
    /// <b>Coordinates, never a permission.</b> A point is where a control is, not proof a click will be
    /// accepted: the test clicks there and the game's own answer (the scene marker changing, the next
    /// screen's report) is what it asserts on.
    /// </para>
    /// <para>
    /// Every string came from <see cref="LocalizationService"/> through <see cref="HubPresenter"/>, so a
    /// test asserts on the words the player reads rather than on a second copy of them.
    /// </para>
    /// </remarks>
    public static class HubReport
    {
        /// <summary>On-screen name of the page global that carries the Hub screen.</summary>
        public const string Global = "__projectSurviveHub";

        /// <summary>The names the report uses for the screen's controls.</summary>
        public static class Control
        {
            /// <summary>The control that enters the next encounter.</summary>
            public const string Depart = "depart";

            /// <summary>The control that ends the run.</summary>
            public const string Finish = "finish";
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
        public static string Publish(HubUiState state, HubView view, string sceneName)
        {
            var report = new JsonReportWriter();
            report.BeginObject();

            report.Write("scene", sceneName ?? string.Empty);
            report.Write("title", state.Title.Value);
            report.Write("instructions", state.Instructions.Value);
            report.Write("encountersLine", state.EncountersLine.Value);
            report.Write("departLabel", state.DepartLabel.Value);
            report.Write("finishLabel", state.FinishLabel.Value);

            report.Write("canvasWidth", Screen.width);
            report.Write("canvasHeight", Screen.height);

            report.BeginArrayProperty("buttons");
            AddControl(report, view, HubView.HubButton.Depart, Control.Depart);
            AddControl(report, view, HubView.HubButton.Finish, Control.Finish);
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
            JsonReportWriter report, HubView view, HubView.HubButton button, string name)
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

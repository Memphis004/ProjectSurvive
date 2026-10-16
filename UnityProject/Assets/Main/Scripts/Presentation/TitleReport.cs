using ProjectSurvive.Game.Presentation.Web;
using UnityEngine;

namespace ProjectSurvive.Game.Presentation
{
    /// <summary>
    /// Publishes what the title screen shows, and where its start control is, so a browser test can
    /// begin a run the way a player does — by pressing the control the scene drew.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The same reason every other report exists (Stage 03's smoke verdict, Stage 08's battle
    /// progress, Stage 09's camp state, Stage 10's summary): a WebGL player cannot run Unity's test
    /// framework, so the only place "the first screen of the game can be left, and leaving it starts a
    /// run" can be observed in a built player is the page the player published. Until Stage 10d the
    /// suite could open the title and nothing more — there was no control to press, which is the gap
    /// this document closes.
    /// </para>
    /// <para>
    /// <b>Coordinates, never a permission.</b> A point is where a control is, not proof a click will
    /// be accepted: the test clicks there and the game's own answer (the scene marker becoming
    /// <c>Camp</c>, the camp's own report) is what it asserts on.
    /// </para>
    /// <para>
    /// Every string in here came from <see cref="LocalizationService"/> through
    /// <see cref="TitlePresenter"/>, so a browser test asserts on the words the player reads rather
    /// than on a second copy of them. The diagnostics line is the one exception and is published as
    /// the build fact it is.
    /// </para>
    /// </remarks>
    public static class TitleReport
    {
        /// <summary>On-screen name of the page global that carries the title screen.</summary>
        public const string Global = "__projectSurviveTitle";

        /// <summary>
        /// The names the report uses for the screen's controls, so a browser test presses a control by
        /// name instead of at a coordinate it guessed from a screenshot.
        /// </summary>
        public static class Control
        {
            /// <summary>The control that begins a run (เริ่มเล่น).</summary>
            public const string Start = "start";
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
        /// <param name="view">The screen, asked where its control is.</param>
        /// <param name="sceneName">The scene on screen, so the report can be checked against the marker.</param>
        /// <param name="startError">Why the screen could not start, or an empty string.</param>
        public static string Publish(UiState state, TitleView view, string sceneName, string startError)
        {
            var report = new JsonReportWriter();
            report.BeginObject();

            report.Write("scene", sceneName ?? string.Empty);
            report.Write("startError", startError ?? string.Empty);
            report.Write("title", state.Title.Value);
            report.Write("startLabel", state.StartLabel.Value);
            report.Write("diagnostics", state.Diagnostics.Value);
            report.Write("ready", state.Ready.Value);

            report.Write("canvasWidth", Screen.width);
            report.Write("canvasHeight", Screen.height);

            report.BeginArrayProperty("buttons");
            AddControl(report, view, TitleView.TitleButton.Start, Control.Start);
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
            JsonReportWriter report, TitleView view, TitleView.TitleButton button, string name)
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

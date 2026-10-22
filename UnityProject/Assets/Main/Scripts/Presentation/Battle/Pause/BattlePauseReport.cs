using ProjectSurvive.Game.Presentation.Battle.Pause;
using ProjectSurvive.Game.Presentation.Web;
using UnityEngine;

namespace ProjectSurvive.Game.Presentation.Battle
{
    /// <summary>
    /// Publishes the pause overlay's state and where its controls are (Stage 13c-2b), so a browser test
    /// can open the overlay, watch the floor stop, resume it and leave for the camp.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The overlay publishes itself rather than growing the battle's document
    /// (<c>__projectSurviveBattle</c>): the battle report is written by the runner while it steps, and
    /// a paused runner steps no frames at all — the one moment the overlay exists is the one moment
    /// that document cannot change. The buttons are published only while they are pressable, so a
    /// closed overlay publishes the HUD's หยุด control and nothing else, which is exactly where a click
    /// would land on the screen as it is drawn.
    /// </para>
    /// <para>
    /// <c>paused</c> is read from the runner, not from the state's flag: the test then asserts the
    /// clock, not the label the overlay drew about the clock.
    /// </para>
    /// </remarks>
    public static class BattlePauseReport
    {
        /// <summary>On-screen name of the page global that carries the overlay.</summary>
        public const string Global = WebGlBridge.PauseGlobal;

        /// <summary>The names the report uses for the overlay's controls.</summary>
        public static class Control
        {
            public const string Open = "open";
            public const string Resume = "resume";
            public const string Leave = "leave";
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
            BattlePauseState state, BattlePauseView view, BattlePausePresenter presenter,
            BattleSimulationRunner runner, string sceneName)
        {
            var report = new JsonReportWriter();
            report.BeginObject();

            report.Write("scene", sceneName);
            report.Write("canvasWidth", Screen.width);
            report.Write("canvasHeight", Screen.height);
            report.Write("open", view.IsOpen);
            report.Write("paused", runner is not null && runner.IsPaused);
            report.Write("tick", runner is null ? "0" : runner.StepsRun.ToString());
            report.Write("hp", presenter?.CurrentHp ?? 0);
            report.Write("requestedScene", presenter?.RequestedScene?.ToString());
            report.Write("lastError", presenter?.LastError ?? string.Empty);

            report.BeginObjectProperty("labels");
            report.Write("title", state.Title.Value);
            report.Write("warning", state.Warning.Value);
            report.Write("resume", state.ResumeLabel.Value);
            report.Write("leave", state.LeaveLabel.Value);
            report.Write("open", state.OpenLabel.Value);
            report.EndObject();

            // Only what a click can reach is published. The HUD's หยุด control is covered by the dim
            // sheet while the overlay is up — it is still active, but a press at its centre lands on
            // the overlay — so it is left out then, and the two ways out are left out while closed.
            report.BeginArrayProperty("buttons");
            if (!view.IsOpen)
            {
                AddControl(report, view, BattlePauseView.PauseButton.Open, Control.Open);
            }

            AddControl(report, view, BattlePauseView.PauseButton.Resume, Control.Resume);
            AddControl(report, view, BattlePauseView.PauseButton.Leave, Control.Leave);
            report.EndArray();

            report.EndObject();

            string json = report.ToString();
            LastJson = json;
            PublishCount++;
            WebGlBridge.Publish(Global, json);
            return json;
        }

        private static void AddControl(
            JsonReportWriter report, BattlePauseView view, BattlePauseView.PauseButton button, string name)
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

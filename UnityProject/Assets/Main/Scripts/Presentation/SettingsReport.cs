using ProjectSurvive.Game.Presentation.Web;
using UnityEngine;

namespace ProjectSurvive.Game.Presentation
{
    /// <summary>
    /// Publishes the settings panel's state and where its controls are (Stage 13c-2a), so a browser
    /// test can open the panel, press a bus row and read what the player reads.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The title's own report (<see cref="TitleReport"/>) says where the start control is because that
    /// is the one thing a browser test needed to begin a run. The panel has nine controls and three
    /// live values, and it is the panel this stage's acceptance is about, so it publishes itself
    /// rather than growing the title's document: <see cref="SettingsView"/> owns both the controls and
    /// the open state.
    /// </para>
    /// <para>
    /// Values and labels are published together on purpose. The label is what the player reads (a
    /// localized format filled by <see cref="SettingsPresenter"/>), and the value is what a test can
    /// compare across a press; publishing only one of them would let the other drift unobserved.
    /// </para>
    /// </remarks>
    public static class SettingsReport
    {
        /// <summary>On-screen name of the page global that carries the panel.</summary>
        public const string Global = WebGlBridge.SettingsGlobal;

        /// <summary>The names the report uses for the panel's controls.</summary>
        public static class Control
        {
            public const string Open = "open";
            public const string Close = "close";
            public const string VolumeDown = "volumeDown";
            public const string VolumeUp = "volumeUp";
            public const string MusicDown = "musicDown";
            public const string MusicUp = "musicUp";
            public const string SfxDown = "sfxDown";
            public const string SfxUp = "sfxUp";
            public const string Language = "language";
            public const string Speed = "speed";
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
        public static string Publish(UiState state, SettingsView view, bool open)
        {
            var report = new JsonReportWriter();
            report.BeginObject();

            report.Write("open", open);
            report.Write("canvasWidth", Screen.width);
            report.Write("canvasHeight", Screen.height);
            report.Write("volume", state.MasterVolume.Value);
            report.Write("music", state.MusicVolume.Value);
            report.Write("sfx", state.SfxVolume.Value);

            report.BeginObjectProperty("labels");
            report.Write("volume", state.VolumeLabel.Value);
            report.Write("music", state.MusicLabel.Value);
            report.Write("sfx", state.SfxLabel.Value);
            report.Write("language", state.LanguageLabel.Value);
            report.Write("speed", state.SpeedLabel.Value);
            report.EndObject();

            report.BeginArrayProperty("buttons");
            AddControl(report, view, SettingsView.SettingsButton.Open, Control.Open);
            AddControl(report, view, SettingsView.SettingsButton.Close, Control.Close);
            AddControl(report, view, SettingsView.SettingsButton.VolumeDown, Control.VolumeDown);
            AddControl(report, view, SettingsView.SettingsButton.VolumeUp, Control.VolumeUp);
            AddControl(report, view, SettingsView.SettingsButton.MusicDown, Control.MusicDown);
            AddControl(report, view, SettingsView.SettingsButton.MusicUp, Control.MusicUp);
            AddControl(report, view, SettingsView.SettingsButton.SfxDown, Control.SfxDown);
            AddControl(report, view, SettingsView.SettingsButton.SfxUp, Control.SfxUp);
            AddControl(report, view, SettingsView.SettingsButton.Language, Control.Language);
            AddControl(report, view, SettingsView.SettingsButton.Speed, Control.Speed);
            report.EndArray();

            report.EndObject();

            string json = report.ToString();
            LastJson = json;
            PublishCount++;
            WebGlBridge.Publish(Global, json);
            return json;
        }

        /// <summary>
        /// Writes one control's centre, or nothing when it is not on screen (the panel is closed, so
        /// its rows are not pressable and are not published).
        /// </summary>
        private static void AddControl(
            JsonReportWriter report, SettingsView view, SettingsView.SettingsButton button, string name)
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

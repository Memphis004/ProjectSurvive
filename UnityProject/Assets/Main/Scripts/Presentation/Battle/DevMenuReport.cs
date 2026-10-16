using ProjectSurvive.Game.Presentation.Web;

namespace ProjectSurvive.Game.Presentation.Battle
{
    /// <summary>
    /// Publishes where the dev menu's controls are on screen, so a browser test can press one by name.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The dev menu is a development tool, and the one thing a browser test cannot do without it is
    /// <b>end a floor on purpose</b>: the suite's floor 1 is won by a standing player, and surrendering
    /// is not an input the suite has. Stage 10's report recorded the consequence — the summary screen
    /// was the one leg of the loop no browser run ever reached, which is also the leg where a stripped
    /// DI constructor or an unimported font would only ever show in a real build.
    /// </para>
    /// <para>
    /// <b>Coordinates, not a decision.</b> This document says where six buttons are; it does not say
    /// whether they may be pressed. That answer stays where it was: the panel is only shown when
    /// <c>DevCheats.Allowed</c> — which is false in a release build — and the floor refuses every one of
    /// these commands unless it was started with <c>BattleSetup.DevCheatsAllowed</c> (AGENTS rule 7's
    /// typed refusal, <c>DevCheatTests</c>' subject). A release build therefore publishes nothing here,
    /// because there is nothing on screen to publish.
    /// </para>
    /// <para>
    /// Published when the panel becomes measurable and republished when the canvas changes size, so
    /// the click a test aims is at a control that is really where the report says.
    /// </para>
    /// </remarks>
    public static class DevMenuReport
    {
        /// <summary>On-screen name of the page global that carries the dev menu's controls.</summary>
        public const string Global = "__projectSurviveDevMenu";

        /// <summary>The names the report uses for the panel's controls.</summary>
        public static class Control
        {
            /// <summary>อมตะ / god mode.</summary>
            public const string GodMode = "godMode";

            /// <summary>เพิ่มเบี้ย / add bia.</summary>
            public const string AddBia = "addBia";

            /// <summary>เพิ่มผี / spawn ghost.</summary>
            public const string SpawnGhost = "spawnGhost";

            /// <summary>ฆ่าผีทั้งหมด / kill all.</summary>
            public const string KillAll = "killAll";

            /// <summary>ข้ามชั้น (ชนะ) / win now — the skip-floor control.</summary>
            public const string WinNow = "winNow";

            /// <summary>แพ้ทันที / lose now — the control that ends the floor as a defeat.</summary>
            public const string LoseNow = "loseNow";
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
        public static string Publish(DevMenuView menu)
        {
            var report = new JsonReportWriter();
            report.BeginObject();

            report.Write("scene", menu is null ? string.Empty : menu.gameObject.scene.name);
            report.Write("visible", menu is not null && menu.PanelVisible);
            report.Write("canvasWidth", UnityEngine.Screen.width);
            report.Write("canvasHeight", UnityEngine.Screen.height);

            report.BeginArrayProperty("buttons");
            AddControl(report, menu, DevMenuView.DevMenuButton.GodMode, Control.GodMode);
            AddControl(report, menu, DevMenuView.DevMenuButton.AddBia, Control.AddBia);
            AddControl(report, menu, DevMenuView.DevMenuButton.SpawnGhost, Control.SpawnGhost);
            AddControl(report, menu, DevMenuView.DevMenuButton.KillAll, Control.KillAll);
            AddControl(report, menu, DevMenuView.DevMenuButton.WinNow, Control.WinNow);
            AddControl(report, menu, DevMenuView.DevMenuButton.LoseNow, Control.LoseNow);
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
            JsonReportWriter report, DevMenuView menu, DevMenuView.DevMenuButton button, string name)
        {
            if (menu is null || !menu.TryGetButtonScreenPoint(button, out UnityEngine.Vector2 screen))
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

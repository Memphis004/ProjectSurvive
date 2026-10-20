using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using ProjectSurvive.Core.Application.Run;
using ProjectSurvive.Core.Domain.Config;
using ProjectSurvive.Core.Domain.Inventory;
using ProjectSurvive.Core.Domain.Run;
using ProjectSurvive.Game.Camp;
using ProjectSurvive.Game.Presentation;
using ProjectSurvive.Game.Presentation.Battle;
using ProjectSurvive.Game.Presentation.Battle.Hud;
using ProjectSurvive.Game.Presentation.Camp;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace ProjectSurvive.Game.Editor
{
    /// <summary>
    /// Takes the UI shots and judges the layout <b>inside the Editor</b>, so a layout can be checked
    /// without a WebGL build and without a browser.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Why this exists: until now the only honest picture of the จัดย่าม screen was a screenshot of the
    /// WebGL player (Stage 09d), which costs a full build plus a Playwright boot to look at a
    /// rectangle that overlaps a label. This command opens the scene, renders it off-screen at the two
    /// sizes the GDD names, writes the images under <c>artifacts/ui/</c>, and fails when a panel that
    /// is supposed to stand clear of another one does not. It covers every screen of the loop the
    /// player actually sees — the title (Title), ศาลาพัก (Camp), the arena (Battle) and the summary
    /// (Result) — so a screen's look is judged from one tool's output at two sizes rather than from
    /// whatever a play session happened to be showing.
    /// </para>
    /// <para>
    /// How a shot is taken, and why this way. A canvas in <c>ScreenSpaceOverlay</c> is laid out to the
    /// game view's size, which the Editor cannot be asked for from a script without reaching into
    /// undocumented <c>GameView</c> internals. Instead the root canvases are switched to
    /// <c>ScreenSpaceCamera</c> for the duration of the shot, pointed at the scene's own camera, whose
    /// <c>targetTexture</c> is a <c>RenderTexture</c> of exactly the size being measured. A canvas in
    /// that mode takes its size from the camera's pixel rectangle, so the layout the shot contains is
    /// the layout a player would get at that size — measured, not assumed: at 1280×720 the board comes
    /// out at <c>{x:26.667,y:213.333,width:493.333,height:386.667}</c>, the rectangle the browser
    /// reported for the same screen in Stage 09 (<c>artifacts/stage09/playwright-web.log</c>).
    /// </para>
    /// <para>
    /// The rectangles are read through <c>SerializedObject</c> from the views' own serialized fields —
    /// the same <c>RectTransform</c>s the scene was authored with — because the views' public geometry
    /// API (<c>CampScreenRects</c>) resolves against <b>overlay</b> canvases and would report nonsense
    /// while the canvas is in camera mode (measured: <c>-11.244,4.231</c> instead of
    /// <c>26.667,213.333</c>). No game code is changed by this tool.
    /// </para>
    /// <para>
    /// Entry points: the menu item below, and <c>-executeMethod
    /// ProjectSurvive.Game.Editor.UiCaptureTool.CaptureAllAndExit</c> for a scripted run. The batch
    /// one needs the Editor to be closed, exactly like <c>tools/build-web.ps1</c>: one process owns
    /// the project lock at a time.
    /// </para>
    /// <para>
    /// A <b>third</b> entry point shoots one screen this one cannot: the merge chooser (Stage 09g) is
    /// built by its view at <i>runtime</i> from the state the presenter publishes, so there is no
    /// rectangle in the scene to measure and no accessor answers before the window is up. That run
    /// therefore enters play mode, opens the window through the real merge rule and writes its own
    /// report (<c>ui-report-playmode.md</c>); everything it measures, it measures with the same
    /// <see cref="UiLayoutCheck"/> verdicts as the states above.
    /// </para>
    /// <para>
    /// The tool never saves a scene. It restores the panel state and the canvases it borrowed, and it
    /// leaves whatever scene the Editor started in open — unless one of them is dirty, in which case
    /// it says so instead of opening a scene Unity would ask a human about.
    /// </para>
    /// </remarks>
    public static class UiCaptureTool
    {
        private const string TitleScenePath = "Assets/Main/Scenes/Title.unity";
        private const string CampScenePath = "Assets/Main/Scenes/Camp.unity";
        private const string BattleScenePath = "Assets/Main/Scenes/Battle.unity";
        private const string ResultScenePath = "Assets/Main/Scenes/Result.unity";

        /// <summary>The Camp scene's name, which is what a play-mode load by name needs.</summary>
        private const string CampSceneName = "Camp";

        /// <summary>The two sizes the GDD names, in the order it names them.</summary>
        private static readonly Vector2Int[] CapturedSizes =
        {
            new Vector2Int(1920, 1080),
            new Vector2Int(1280, 720),
        };

        /// <summary>
        /// The panel's surfaces, as the view serializes them. Names match <c>CampBagSurface</c> so the
        /// report reads the same as the PlayMode test's failure messages.
        /// </summary>
        private static readonly (string Name, string Field)[] PanelSurfaces =
        {
            ("Board", "_boardRect"),
            (UiLayoutCheck.ChestPanel, "_chestPanelRect"),
            (UiLayoutCheck.Chest, "_chestRect"),
            ("Altar", "_altarRect"),
            ("Instructions", "_instructionsRect"),
            ("Stats", "_statsRect"),
        };

        /// <summary>
        /// The title screen's three regions, as <c>TitleView</c> serializes them: the heading, the
        /// build/config line and the control that starts a run (Stage 10d).
        /// </summary>
        /// <remarks>
        /// All three are <see cref="UiRegionKind.Control"/> and therefore have to stand clear of one
        /// another, which is exactly the acceptance this screen has — "ไม่ทับข้อความ build/config". The
        /// boxes are the line boxes the scene really occupies rather than placeholders, so a collision
        /// this reports is one on screen (see <c>ProjectSetup.CreateTitleScene</c>).
        /// </remarks>
        private static readonly (string Name, string Field)[] TitleRegions =
        {
            ("TitleLabel", "_titleLabel"),
            ("DiagnosticsLabel", "_diagnosticsLabel"),
            ("StartButton", "_startButton"),
        };

        /// <summary>
        /// The summary screen's five lines, as <c>ResultView</c> serializes them. They are measured as
        /// <see cref="UiRegionKind.Control"/> regions because that kind is the one the overlap rule lets
        /// sit inside a surface it belongs to ("a control drawn inside its own surface is by design"):
        /// what the check is for here is that no line escapes the panel and no two of them land on one
        /// another, which is the same question the camp's controls answer.
        /// </summary>
        private static readonly (string Name, string Field)[] ResultLines =
        {
            ("Headline", "_headlineLabel"),
            ("FloorLine", "_floorLabel"),
            ("KillsLine", "_killsLabel"),
            ("BiaLine", "_biaLabel"),
            ("HpLine", "_hpLabel"),
        };

        /// <summary>The summary screen's two ways out, as the view serializes them.</summary>
        private static readonly (string Name, string Field)[] ResultControls =
        {
            ("Restart", "_restartButton"),
            ("BackToTitle", "_titleButton"),
        };

        /// <summary>
        /// The panel's controls, as the view serializes them. กลับ (Close) is in here because it lives
        /// inside the panel; จัดย่าม (Open) lives on the canvas outside it and is measured with the
        /// closed camp instead.
        /// </summary>
        private static readonly (string Name, string Field)[] PanelControls =
        {
            ("Close", "_closeButton"),
            ("Rotate", "_rotateButton"),
            ("Undo", "_undoButton"),
            ("Reset", "_resetButton"),
            ("Lock", "_lockButton"),
            ("LockRestore", "_lockRestoreToggle"),
            ("NextFloor", "_nextFloorButton"),
        };

        /// <summary>
        /// The battle HUD's controls, as the view serializes them: the two bars, the timeline's track,
        /// the speed control and the six dev rows. Every one of them has to be drawn inside the panel it
        /// belongs to — the half of the rule a screenshot cannot check.
        /// </summary>
        private static readonly (string Name, string Field)[] BattleControls =
        {
            (UiLayoutCheck.HpBar, "_hpBar"),
            (UiLayoutCheck.ManaBar, "_manaBar"),
            (UiLayoutCheck.SpeedButton, "_speedButton"),
            ("Portrait", "_portrait"),
        };

        /// <summary>
        /// The battle HUD's labels, as the view serializes them. They are measured so that a caption
        /// outside its panel or two labels on one another is a failure rather than something a reviewer
        /// has to spot in a picture.
        /// </summary>
        private static readonly (string Name, string Field)[] BattleLabels =
        {
            ("FloorLabel", "_floorLabel"),
            ("FloorIndexLabel", "_floorIndexLabel"),
            (UiLayoutCheck.BagCaption, "_bagCaption"),
            ("StatusCaption", "_statusCaption"),
            ("PossessionLabel", "_possessionLabel"),
            ("HpCaption", "_hpCaption"),
            ("HpValue", "_hpValue"),
            ("ManaCaption", "_manaCaption"),
            ("ManaValue", "_manaValue"),
            ("BiaCaption", "_biaCaption"),
            ("BiaValue", "_biaValue"),
            ("TimeCaption", "_timeCaption"),
            ("TimeValue", "_timeValue"),
            ("WaveCaption", "_waveCaption"),
            ("WaveValue", "_waveValue"),
            ("KillsCaption", "_killsCaption"),
            ("KillsValue", "_killsValue"),
            ("TimelineCaption", "_timelineCaption"),
        };

        /// <summary>
        /// The labels only a boss floor shows, measured when the band they sit in is on the screen. The
        /// speed button's own label is deliberately not here: it is drawn <i>inside</i> the button, and a
        /// control inside another control is the one nested pair the rule does not allow — the button's own
        /// rectangle is what is measured.
        /// </summary>
        private static readonly (string Name, string Field)[] BossFloorLabels =
        {
            ("BossCaption", "_bossCaption"),
        };

        /// <summary>The dev menu's own controls, as its view serializes them.</summary>
        private static readonly (string Name, string Field)[] DevRows =
        {
            ("DevStatus", "_statusLabel"),
            ("DevGodMode", "_godModeButton"),
            ("DevAddBia", "_addBiaButton"),
            ("DevSpawnGhost", "_spawnGhostButton"),
            ("DevKillAll", "_killAllButton"),
            ("DevWinNow", "_winNowButton"),
            ("DevLoseNow", "_loseNowButton"),
        };

        /// <summary>
        /// The opacity the tooltip's own background has to reach ("พื้นหลังทึบ (alpha ≥ 0.92)").
        /// </summary>
        private const float TooltipAlphaFloor = 0.92f;

        /// <summary>
        /// How much closer to the overlay's own background colour a pixel has to land for the overlay
        /// to count as painted there. Two steps of an 8-bit channel (2/255): small on purpose, because
        /// a dark tooltip over a dark panel only moves the pixel by a couple of levels — and honest,
        /// because an overlay that is not drawn there at all moves it by <b>nothing</b>.
        /// </summary>
        private const float OverlayPixelProofFloor = 0.008f;

        private static readonly Vector3[] Corners = new Vector3[4];

        private static string OutputDirectory =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "artifacts", "ui"));

        /// <summary>Captures both scenes at both sizes and judges the layouts.</summary>
        [MenuItem(
            "ProjectSurvive/Tools/1. Capture UI (Title + Camp + Battle + Result, 1920x1080 + 1280x720)",
            priority = 130)]
        private static void CaptureFromMenu()
        {
            CaptureAll(exitEditor: false);
        }

        /// <summary>
        /// The <c>-executeMethod</c> entry point. Exits the Editor with 0 when every check passed and
        /// 1 otherwise, so a script can branch on the result.
        /// </summary>
        public static void CaptureAllAndExit()
        {
            CaptureAll(exitEditor: true);
        }

        /// <summary>
        /// The same run with one invented region dropped on top of the board, which is the only way to
        /// show that this command <b>fails</b> when panels collide without editing a scene to break it.
        /// Its report goes to <c>ui-report-selftest.*</c>, so it cannot be mistaken for the real one.
        /// </summary>
        [MenuItem("ProjectSurvive/Tools/2. Capture UI self-test (expected to FAIL)", priority = 131)]
        private static void SelfTestFromMenu()
        {
            RunSelfTest(exitEditor: false);
        }

        /// <summary>The <c>-executeMethod</c> form of <see cref="SelfTestFromMenu"/>.</summary>
        public static void CaptureSelfTestAndExit()
        {
            RunSelfTest(exitEditor: true);
        }

        private static void RunSelfTest(bool exitEditor)
        {
            sDeliberateOverlap = true;
            try
            {
                CaptureAll(exitEditor);
            }
            finally
            {
                sDeliberateOverlap = false;
            }
        }

        /// <summary>Set only by <see cref="CaptureSelfTestAndExit"/>.</summary>
        private static bool sDeliberateOverlap;

        /// <summary>Runs the whole capture. Returns the process exit code the batch path would use.</summary>
        public static int CaptureAll(bool exitEditor)
        {
            var started = System.Diagnostics.Stopwatch.StartNew();
            var report = new CaptureReport();
            string startedIn = SceneManager.GetActiveScene().path;

            try
            {
                Directory.CreateDirectory(OutputDirectory);
            }
            catch (Exception error)
            {
                report.Failures.Add("cannot write to " + OutputDirectory + ": " + error.Message);
                WriteReports(report, started.ElapsedMilliseconds);
                if (exitEditor)
                {
                    EditorApplication.Exit(1);
                }

                return 1;
            }

            CaptureTitle(report);
            CaptureCamp(report);
            CaptureBattle(report);
            CaptureResult(report);
            RestoreScene(startedIn, report);

            WriteReports(report, started.ElapsedMilliseconds);

            int exitCode = report.Failures.Count == 0 ? 0 : 1;
            if (exitEditor)
            {
                EditorApplication.Exit(exitCode);
            }

            return exitCode;
        }

        // ------------------------------------------------------------------
        // the merge chooser: play mode, because the window is built at runtime
        // ------------------------------------------------------------------

        /// <summary>
        /// Captures the merge chooser at the Game View's own resolution, in Play Mode.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Why this is a second entry point rather than one more state of <see cref="CaptureFromMenu"/>:
        /// the window is built by <c>CampBagScreenView</c> at <b>runtime</b> from the state the
        /// presenter publishes, and the states above are measured from the <i>authored</i> scene with no
        /// play mode at all. There is no rectangle in <c>Camp.unity</c> for a window that does not exist
        /// until a merge asks a question, and the view's own geometry accessors only answer while it is
        /// showing that window — so the window has to be opened for real.
        /// </para>
        /// <para>
        /// Opened through the <b>real rule</b>, not by hand: a second copy of the camp's first item is
        /// added and placed, and the presenter's own <c>DragStart</c>/<c>DragMove</c>/<c>Drop</c> — the
        /// calls the pointer handlers make — are what raises the window. If the rule stops offering a
        /// choice, this run fails and says so instead of photographing an empty panel.
        /// </para>
        /// <para>
        /// The Game View's resolution <i>is</i> the layout size here, so the two sizes the GDD names are
        /// taken by running this twice with the Game View set to each; the report names the size it
        /// actually shot, so a third size cannot be mistaken for one of them, and the picture is a
        /// <c>ScreenCapture</c> of that same view — the image and the rectangles are of one state at one
        /// size. It is deliberately <b>not</b> re-rendered through a RenderTexture the way the states
        /// above are: borrowing the canvases into camera mode invalidates the view's accessors, which
        /// resolve against an overlay canvas.
        /// </para>
        /// <para>
        /// Interactive only, and the Game View has to be the focused window while it runs: entering play
        /// mode needs real frames, so there is no batch <c>-executeMethod</c> form, and a window that is
        /// not in the foreground does not render (the same fact that made the test harness submit
        /// asynchronous test runs).
        /// </para>
        /// </remarks>
        [MenuItem("ProjectSurvive/Tools/3. Capture the merge chooser in Play Mode (Camp)", priority = 132)]
        private static void CaptureChooserFromMenu()
        {
            ChooserCaptureRun.Start();
        }

        /// <summary>
        /// Shoots the camp tooltip before and after an upgrade, in Play Mode, where its labels carry
        /// their real text (Stage 09h).
        /// </summary>
        /// <remarks>
        /// The edit-mode capture cannot show this: every label in <c>Camp.unity</c> is empty until the
        /// runtime writes it. This run starts a real camp, merges the starting weapon twice (choosing
        /// a cooldown variant when the offer has one), and shoots the tooltip at tier 1 and tier 3 at
        /// the Game View's own size — 1920×1080 here — with the strings it drew written into
        /// <c>ui-report-tooltip.md</c> so the picture and the numbers can be read together.
        /// </remarks>
        [MenuItem("ProjectSurvive/Tools/4. Capture the tooltip before and after an upgrade (Play Mode)", priority = 133)]
        private static void CaptureTooltipFromMenu()
        {
            TooltipCaptureRun.Start();
        }

        // ------------------------------------------------------------------
        // the Title: shot and measured, so its control cannot land on the build line
        // ------------------------------------------------------------------

        /// <summary>
        /// Shoots the first screen of the game at both sizes and measures its three regions.
        /// </summary>
        /// <remarks>
        /// The screen the player meets before anything else, and (since Stage 10d) the one screen that
        /// begins a run: this is where "เริ่มเล่น is on screen, is legible and is not drawn on the
        /// build/config line" is judged. Measured from the scene, not from a run — the only thing that
        /// moves on this screen is the text the runtime writes into it, and the strings themselves are
        /// asserted by the tests that drive it.
        /// </remarks>
        private static void CaptureTitle(CaptureReport report)
        {
            if (!EnsureOpen(TitleScenePath, report))
            {
                return;
            }

            var view = Object.FindFirstObjectByType<TitleView>(FindObjectsInactive.Include);
            Camera camera = Camera.main;

            if (view == null || camera == null)
            {
                report.Failures.Add(
                    "the Title scene is missing " + (view == null ? "TitleView " : string.Empty)
                    + (camera == null ? "a MainCamera" : string.Empty));
                return;
            }

            var fields = new SerializedObject(view);
            var regions = new List<(string Name, UiRegionKind Kind, RectTransform Rect)>();
            AddSerializedComponents(fields, TitleRegions, UiRegionKind.Control, regions);

            if (regions.Count < TitleRegions.Length)
            {
                report.Failures.Add(
                    "the Title scene wires " + regions.Count + " of its " + TitleRegions.Length
                    + " regions, so a control that overlaps another one could go unnoticed");
            }

            report.Notes.Add(
                "the title screen is measured from the authored scene: its heading, its build line and "
                + "เริ่มเล่น are Control regions, which the overlap rule holds apart — a start control "
                + "drawn over the build/config line fails this run rather than being noticed on a "
                + "screenshot. Every label is empty in Title.unity until the runtime fills it from the "
                + "localization tables, so what this reports is geometry; the strings (and the fact that "
                + "pressing the control starts a run) are asserted by `TitleScreenTests` in EditMode and "
                + "`TitleScenePlayModeTests` over the real scene");

            for (int i = 0; i < CapturedSizes.Length; i++)
            {
                Vector2Int size = CapturedSizes[i];
                Capture(SceneKind.Title, "start", "title", camera, size, regions, report);
            }
        }

        // ------------------------------------------------------------------
        // the Camp: the panel's layout is what this command is for
        // ------------------------------------------------------------------

        private static void CaptureCamp(CaptureReport report)
        {
            if (!EnsureOpen(CampScenePath, report))
            {
                return;
            }

            var bag = Object.FindFirstObjectByType<CampBagScreenView>(FindObjectsInactive.Include);
            var bar = Object.FindFirstObjectByType<CampStatusBarView>(FindObjectsInactive.Include);
            var tooltip = Object.FindFirstObjectByType<CampTooltipView>(FindObjectsInactive.Include);
            Camera camera = Camera.main;

            if (bag == null || bar == null || tooltip == null || camera == null)
            {
                report.Failures.Add(
                    "the Camp scene is missing " + (bag == null ? "CampBagScreenView " : string.Empty)
                    + (bar == null ? "CampStatusBarView " : string.Empty)
                    + (tooltip == null ? "CampTooltipView " : string.Empty)
                    + (camera == null ? "a MainCamera" : string.Empty));
                return;
            }

            var bagFields = new SerializedObject(bag);
            var fixedRects = new List<(string Name, UiRegionKind Kind, RectTransform Rect)>();
            AddSerializedRects(bagFields, PanelSurfaces, UiRegionKind.Surface, fixedRects);
            AddSerializedRects(bagFields, PanelControls, UiRegionKind.Control, fixedRects);

            var tooltipFields = new SerializedObject(tooltip);

            // The tooltip serializes its panel as a GameObject, and the inactive panel is exactly the
            // state the scene is authored in, so the lookups here include inactive objects.
            var tooltipPanel = tooltipFields.FindProperty("_root")?.objectReferenceValue as GameObject;
            var tooltipRegion = new List<(string Name, UiRegionKind Kind, RectTransform Rect)>();

            if (tooltipPanel == null)
            {
                report.Failures.Add("the tooltip has no root panel, so nothing can measure where it is drawn");
                return;
            }

            tooltipRegion.Add((UiLayoutCheck.Tooltip, UiRegionKind.Overlay, (RectTransform)tooltipPanel.transform));

            // The overlay's own contract, checked once: it is drawn by a canvas above the screen's and
            // it cannot be clicked. Both are properties of the authored scene, so they are read from it
            // rather than inferred from the picture.
            CheckTooltipContract(tooltip, bag, report);

            report.Notes.Add(
                "the merge chooser (Stage 09g) is built by the view at runtime, so this command — which "
                + "never enters play mode — has no rectangle in the scene to measure for it, and the "
                + "view's accessors answer only while the window is up: it is shot by "
                + "`ProjectSurvive/Tools/3. Capture the merge chooser in Play Mode (Camp)`, which opens the "
                + "window through the real merge rule and writes `ui-report-playmode.md`");

            var barFields = new SerializedObject(bar);
            var statusBar = barFields.FindProperty("_panelRect")?.objectReferenceValue as RectTransform;
            var openButton = bagFields.FindProperty("_openButton")?.objectReferenceValue as Button;

            if (statusBar == null || openButton == null)
            {
                report.Failures.Add("the camp's status bar or จัดย่าม button is not wired in the scene");
                return;
            }

            var closed = new List<(string Name, UiRegionKind Kind, RectTransform Rect)>
            {
                ("StatusBar", UiRegionKind.Surface, statusBar),
                ("Open", UiRegionKind.Control, openButton.transform as RectTransform),
            };

            report.Notes.Add(
                "the shapes are the authored ones: every label in Camp.unity is empty until the runtime "
                + "fills it from the localization tables, so these shots show the panels and their "
                + "backgrounds without their text, and the board's cells are built at runtime "
                + "(CampBagDragPlayModeTests measures that layout in play mode)");
            report.Notes.Add(
                "the world behind the UI is whatever the scene's own camera is aimed at in the Editor: "
                + "the camp's camera follows the player at runtime, and nothing here runs the player");

            for (int i = 0; i < CapturedSizes.Length; i++)
            {
                Vector2Int size = CapturedSizes[i];

                // The closed camp first: this is the state the status bar is the top layer in, so
                // "จัดย่าม does not sit on the bar" is checked here.
                bag.ShowPanel(false);
                Capture(SceneKind.Camp, "closed", "camp-closed", camera, size, closed, report);

                // Then the panel, which is where the board, the chest and the altar live.
                bag.ShowPanel(true);
                Capture(SceneKind.Camp, "panel", "camp", camera, size, fixedRects, report);

                // Then the tooltip shown over the panel: the tooltip's own rectangle is measured
                // against the canvas, and the picture has to prove it drew over what is behind it —
                // the board is opaque, so a tooltip on a lower canvas would be invisible here and the
                // two samples would agree.
                var tooltipState = new List<(string Name, UiRegionKind Kind, RectTransform Rect)>(fixedRects);
                tooltipState.AddRange(tooltipRegion);
                tooltipPanel.SetActive(true);
                Capture(
                    SceneKind.Camp, "tooltip", "camp-tooltip", camera, size, tooltipState, report,
                    overlay: tooltipPanel);
                tooltipPanel.SetActive(false);
            }

            // Leave the panel and the tooltip exactly as the scene was authored: closed.
            bag.ShowPanel(false);
        }

        /// <summary>
        /// The tooltip's contract: it draws on a canvas above the screen's own and no click can reach
        /// it.
        /// </summary>
        /// <remarks>
        /// Read from the objects in the scene rather than from the running view, because this command
        /// never enters play mode: the canvas a component sits under and the flags on its graphics are
        /// serialized facts. The two halves are the two ways a tooltip breaks the thing it is for if
        /// they are missing — a lower canvas draws it under the board, and a raycast target eats the
        /// pointer events that move it.
        /// </remarks>
        private static void CheckTooltipContract(
            CampTooltipView tooltip, CampBagScreenView bag, CaptureReport report)
        {
            Canvas tooltipCanvas = tooltip.GetComponentInParent<Canvas>();
            Canvas screenCanvas = bag.GetComponentInParent<Canvas>();

            if (tooltipCanvas == null || screenCanvas == null)
            {
                report.Failures.Add("the tooltip or the จัดย่าม panel is not under a canvas");
                return;
            }

            Canvas tooltipRoot = tooltipCanvas.rootCanvas;
            Canvas screenRoot = screenCanvas.rootCanvas;

            if (tooltipRoot == screenRoot)
            {
                report.Failures.Add(
                    "the tooltip shares the camp's canvas: 'on top' would then be the creation order of "
                    + "the board's cells, not a draw order");
            }
            else if (tooltipRoot.sortingOrder <= screenRoot.sortingOrder)
            {
                report.Failures.Add(
                    "the tooltip's canvas sorts at " + tooltipRoot.sortingOrder + ", not above the camp's "
                    + screenRoot.sortingOrder);
            }

            if (tooltipRoot.GetComponent<GraphicRaycaster>() != null)
            {
                report.Failures.Add("the tooltip's canvas carries a GraphicRaycaster, so it can be clicked");
            }

            var background = tooltip.GetComponent<Image>();
            if (background == null)
            {
                report.Failures.Add("the tooltip has no background of its own");
                return;
            }

            if (background.raycastTarget)
            {
                report.Failures.Add("the tooltip's background is a raycast target");
            }

            if (background.color.a < TooltipAlphaFloor - 0.001f)
            {
                report.Failures.Add(
                    "the tooltip's background is alpha " + background.color.a.ToString("0.###")
                    + ", below the " + TooltipAlphaFloor.ToString("0.###") + " floor");
            }
        }

        // ------------------------------------------------------------------
        // the Battle scene: shots only, its HUD is not part of the region check
        // ------------------------------------------------------------------

        /// <summary>
        /// The Battle HUD at each of the three moments the stage asks for, at both sizes.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Three states, not one, because the screen is nearly all runtime state.</b> A shot of the
        /// authored scene would show empty labels, an empty mini ย่าม and no cooldown plates at all —
        /// which is exactly the state the screen is in before the floor starts, not the state Stage 08c
        /// has to be judged on. So the floor is played for real by <see cref="BattleCaptureState"/>:
        /// the run departs on the shipped tables, the dev battle is stepped, and the frame it publishes
        /// is handed to the same objects the runner hands it to. <c>begin</c> is tick 0 (the first frame
        /// the runner renders), <c>midfight</c> is the first frame whose casters sit at several
        /// different cooldowns, and <c>manashort</c> is that same frame with one entry carrying the
        /// flag — the one state floor 1's data cannot reach; the report says so and so does the helper's
        /// own remarks.
        /// </para>
        /// <para>
        /// <b>Everything on this screen is measured, the labels included.</b> The six panels are
        /// surfaces and each label, bar, control and the mini bag's grid area is a control inside one of
        /// them, so a caption that escaped its panel, a control drawn on another control or a panel
        /// overlapping a panel is reported rather than photographed and left for a human to notice. Two
        /// regions only <i>touching</i> is not a collision, so the adjacencies the HUD is built from (a
        /// caption above its value, a bar under its caption) stay silent.
        /// </para>
        /// <para>
        /// The dev column is authored <b>inactive</b> — a release build must never show it — so this run
        /// turns it on for the shots and turns it off again, which is also what makes it measurable:
        /// <c>ScreenRectOf</c> answers nothing for an inactive object. The boss band is the opposite case:
        /// the presenter hides it on a floor without a boss, so floor 1 does not measure it, and the
        /// report notes it instead of reporting a missing rectangle as a layout failure.
        /// </para>
        /// </remarks>
        private static void CaptureBattle(CaptureReport report)
        {
            if (!EnsureOpen(BattleScenePath, report))
            {
                return;
            }

            Camera camera = Camera.main;
            if (camera == null)
            {
                report.Failures.Add("the Battle scene has no MainCamera");
                return;
            }

            var hud = Object.FindFirstObjectByType<BattleHudView>(FindObjectsInactive.Include);
            var miniBag = Object.FindFirstObjectByType<BattleMiniBagView>(FindObjectsInactive.Include);
            var devMenu = Object.FindFirstObjectByType<DevMenuView>(FindObjectsInactive.Include);

            if (hud == null || miniBag == null)
            {
                report.Failures.Add(
                    "the Battle scene is missing " + (hud == null ? "BattleHudView" : "BattleMiniBagView"));
                return;
            }

            var devFields = devMenu == null ? null : new SerializedObject(devMenu);
            var devPanel = devFields?.FindProperty("_panel")?.objectReferenceValue as GameObject;
            bool devWasActive = devPanel != null && devPanel.activeSelf;

            if (devPanel != null)
            {
                devPanel.SetActive(true);
                Canvas.ForceUpdateCanvases();
            }
            else
            {
                report.Notes.Add("the Battle scene has no dev menu, so its column is not measured");
            }

            BattleCaptureState state = null;

            try
            {
                if (!BattleCaptureState.TryBuild(hud, miniBag, out state, out string error))
                {
                    report.Failures.Add("the Battle floor could not be started for the shots: " + error);
                    return;
                }

                // The mid-fight frame is found once and both later shots are taken from it, so the two
                // pictures differ only by the flag the MP-short one adds.
                state.StepToMidFight();

                var regions = BattleRegionsOf(hud, miniBag, devMenu, devFields, state.HasVisibleBoss, report);

                report.Notes.Add(
                    "the Battle HUD is measured from a <b>running</b> floor: the run departs on the shipped "
                    + "tables and the dev battle's own frames are handed to the scene's HUD, so the mini "
                    + "ย่าม is the run's bag and every plate is the frame's number; the mid-fight shot is "
                    + "tick " + state.MidFightTick + " with " + state.CasterCount + " caster(s) at "
                    + state.DistinctLevels + " distinct cooldown level(s)");

                report.Notes.Add(
                    "the MP-short shot is the <b>only</b> authored state: neither session reaches "
                    + "`LoadoutCooldownFlags.ManaShort` on floor 1 (the dev session's MP pool never falls "
                    + "below a single cast's cost, and the Editor assembly may not name the EcsLite session "
                    + "to reach the one that spends MP), so that shot hands the mini ย่าม a real "
                    + "`LoadoutCooldown` carrying the flag `FramePublishSystem` sets when a ready cast cannot "
                    + "be paid for. The picture proves the tone is visible at both sizes; that the flag "
                    + "reaches the plate is proved by `BattleMiniBagTests` and the PlayMode suite");

                var shots = new[]
                {
                    (BattleShot.FloorStart, "begin"),
                    (BattleShot.MidFight, "midfight"),
                    (BattleShot.ManaShort, "manashort"),
                };

                for (int s = 0; s < shots.Length; s++)
                {
                    BattleShot shot = shots[s].Item1;
                    string slug = "battle-" + shots[s].Item2;

                    report.Notes.Add("`" + slug + "` is the floor's " + shots[s].Item2 + " state: " + state.Describe(shot));

                    for (int i = 0; i < CapturedSizes.Length; i++)
                    {
                        Vector2Int size = CapturedSizes[i];

                        // Applied again after the canvas has been laid out at this size, and after the
                        // mini bag has re-fitted its cells to it: the picture is of the size it claims.
                        Capture(
                            SceneKind.Battle, "hud-" + shots[s].Item2, slug, camera, size, regions, report,
                            afterLayout: () =>
                            {
                                miniBag.RefreshIfResized();
                                state.Apply(shot);
                            });
                    }
                }
            }
            catch (Exception error)
            {
                report.Failures.Add("the Battle HUD could not be photographed: " + error.Message);
            }
            finally
            {
                state?.ClearBagObjects();
                state?.Dispose();

                if (devPanel != null)
                {
                    devPanel.SetActive(devWasActive);
                }
            }
        }

        /// <summary>
        /// The Battle screen's regions: its six panels, everything they hold, and the mini ย่าม's own
        /// grid area.
        /// </summary>
        /// <remarks>
        /// The panels are named from <see cref="UiLayoutCheck"/>, so the report, the rule and the scene
        /// read the same words. The labels are named after the field they are serialized in, the way the
        /// จัดย่าม screen's controls are, because a label is only interesting here as "the box this string
        /// occupies" — its name is what tells a reader <i>which</i> string escaped its panel.
        /// </remarks>
        private static List<(string Name, UiRegionKind Kind, RectTransform Rect)> BattleRegionsOf(
            BattleHudView hud,
            BattleMiniBagView miniBag,
            DevMenuView devMenu,
            SerializedObject devFields,
            bool bossVisible,
            CaptureReport report)
        {
            var fields = new SerializedObject(hud);
            var regions = new List<(string Name, UiRegionKind Kind, RectTransform Rect)>(40);

            AddSurface(regions, UiLayoutCheck.MiniBag, miniBag.Panel);
            AddSurface(regions, UiLayoutCheck.FloorPanel, ParentOf(fields, "_floorLabel"));
            AddSurface(regions, UiLayoutCheck.ResourcePanel, ParentOf(fields, "_timeCaption"));
            AddSurface(regions, UiLayoutCheck.StatusPanel, ParentOf(fields, "_hpCaption"));
            AddSurface(regions, UiLayoutCheck.TimelinePanel, ParentOf(fields, "_timelineCaption"));

            if (devFields != null)
            {
                AddSurface(regions, UiLayoutCheck.DevPanel, ParentOf(devFields, "_statusLabel"));
                AddSerializedComponents(devFields, DevRows, UiRegionKind.Control, regions);
            }

            AddSerializedComponents(fields, BattleControls, UiRegionKind.Control, regions);
            AddSerializedComponents(fields, BattleLabels, UiRegionKind.Control, regions);

            if (miniBag.Content != null)
            {
                regions.Add((UiLayoutCheck.MiniBagContent, UiRegionKind.Control, miniBag.Content));
            }

            // The timeline's track lives on the timeline view rather than on the HUD, so it is reached
            // through the reference the HUD serializes.
            var timeline = fields.FindProperty("_timeline")?.objectReferenceValue as BattleTimelineView;
            if (timeline != null)
            {
                AddSerializedComponents(
                    new SerializedObject(timeline),
                    new[] { (UiLayoutCheck.TimelineTrack, "_track") }, UiRegionKind.Control, regions);
            }

            // The boss band is a control inside the resource panel on a floor that has a boss. Floor 1
            // does not, and a region that is not on the screen is not a region.
            if (bossVisible)
            {
                var boss = fields.FindProperty("_bossRoot")?.objectReferenceValue as GameObject;
                if (boss != null)
                {
                    regions.Add((UiLayoutCheck.BossPanel, UiRegionKind.Control, boss.transform as RectTransform));
                }

                AddSerializedComponents(fields, BossFloorLabels, UiRegionKind.Control, regions);
            }
            else
            {
                report.Notes.Add(
                    "the boss band is not measured: floor 1 has no boss, so the presenter hides it "
                    + "(the band's own rectangle is checked on a boss floor)");
            }

            return regions;
        }

        private static void AddSurface(
            List<(string Name, UiRegionKind Kind, RectTransform Rect)> into, string name, RectTransform rect)
        {
            if (rect != null)
            {
                into.Add((name, UiRegionKind.Surface, rect));
            }
        }

        private static RectTransform ParentOf(SerializedObject source, string field)
        {
            var component = source.FindProperty(field)?.objectReferenceValue as Component;
            return component != null ? component.transform.parent as RectTransform : null;
        }

        // ------------------------------------------------------------------
        // the Result screen: shot and measured, like the camp
        // ------------------------------------------------------------------

        /// <summary>
        /// Shoots the summary screen the run ends on (Stage 10b) at both sizes, and measures the
        /// panel, its five lines and its two ways out.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Measured from the scene, not from a run: the screen's <i>geometry</i> is authored in
        /// <c>Result.unity</c> — the panel, where each line sits in it, the two buttons under them —
        /// and this command never enters play mode. That is the same division the camp's shots make:
        /// a picture of what the scene says, with the numbers the screen prints proved by the tests
        /// that run it (<c>ResultScreenTests</c> in EditMode, <c>ResultScenePlayModeTests</c> for the
        /// real scene change).
        /// </para>
        /// <para>
        /// Every label in the scene is empty until the runtime fills it from the localization tables,
        /// so these shots show the panel, the lines' own rectangles and the two button backgrounds
        /// without their text. The report says so rather than inventing strings: a shot filled in by
        /// this tool would be a picture of the tool's own text, which is exactly the thing a layout
        /// check must not be able to mistake for the game's.
        /// </para>
        /// </remarks>
        private static void CaptureResult(CaptureReport report)
        {
            if (!EnsureOpen(ResultScenePath, report))
            {
                return;
            }

            var view = Object.FindFirstObjectByType<ResultView>(FindObjectsInactive.Include);
            Camera camera = Camera.main;

            if (view == null || camera == null)
            {
                report.Failures.Add(
                    "the Result scene is missing " + (view == null ? "ResultView " : string.Empty)
                    + (camera == null ? "a MainCamera" : string.Empty));
                return;
            }

            var fields = new SerializedObject(view);
            var panel = fields.FindProperty("_headlineLabel")?.objectReferenceValue as Component;
            var summary = panel != null ? panel.transform.parent as RectTransform : null;

            if (summary == null)
            {
                report.Failures.Add(
                    "the Result screen's headline is not under a panel of its own, so there is nothing "
                    + "to measure the lines against");
                return;
            }

            var regions = new List<(string Name, UiRegionKind Kind, RectTransform Rect)>
            {
                (UiLayoutCheck.Summary, UiRegionKind.Surface, summary),
            };

            AddSerializedComponents(fields, ResultLines, UiRegionKind.Control, regions);
            AddSerializedComponents(fields, ResultControls, UiRegionKind.Control, regions);

            report.Notes.Add(
                "the Result screen is measured from the authored scene, so its five lines and two "
                + "buttons are placeholders' rectangles: every label in Result.unity is empty until the "
                + "runtime fills it from the localization tables, and the strings themselves (and the "
                + "run's numbers in them) are asserted by `ResultScreenTests` and "
                + "`ResultScenePlayModeTests`, which drive the real screen");

            for (int i = 0; i < CapturedSizes.Length; i++)
            {
                Vector2Int size = CapturedSizes[i];
                Capture(SceneKind.Result, "summary", "result", camera, size, regions, report);
            }
        }

        // ------------------------------------------------------------------
        // one shot
        // ------------------------------------------------------------------

        /// <param name="afterLayout">
        /// Run after the canvases have been laid out at this size and before anything is measured or
        /// rendered: the hook a state that changed with the size (the mini bag's cells are fitted to the
        /// area they are drawn in) puts itself back through. Null for a screen whose layout is entirely
        /// authored.
        /// </param>
        private static void Capture(
            SceneKind scene,
            string state,
            string fileSlug,
            Camera camera,
            Vector2Int size,
            List<(string Name, UiRegionKind Kind, RectTransform Rect)> regions,
            CaptureReport report,
            GameObject overlay = null,
            Action afterLayout = null)
        {
            var borrowed = new List<(Canvas Canvas, RenderMode Mode, Camera Camera, float Plane)>();
            RenderTexture target = null;
            Texture2D image = null;
            var started = System.Diagnostics.Stopwatch.StartNew();

            string fileName = fileSlug + "-" + size.x + "x" + size.y + ".png";
            string path = Path.Combine(OutputDirectory, fileName);

            try
            {
                target = RenderTexture.GetTemporary(size.x, size.y, 24, RenderTextureFormat.ARGB32);

                Canvas[] canvases = Object.FindObjectsByType<Canvas>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None);
                for (int i = 0; i < canvases.Length; i++)
                {
                    Canvas canvas = canvases[i];
                    if (canvas.rootCanvas != canvas
                        || canvas.renderMode != RenderMode.ScreenSpaceOverlay
                        || !canvas.gameObject.activeInHierarchy)
                    {
                        continue;
                    }

                    borrowed.Add((canvas, canvas.renderMode, canvas.worldCamera, canvas.planeDistance));
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = camera;
                    canvas.planeDistance = camera.nearClipPlane + 1f;
                }

                RenderTexture previousTarget = camera.targetTexture;
                camera.targetTexture = target;

                Canvas.ForceUpdateCanvases();

                if (afterLayout != null)
                {
                    afterLayout();
                    Canvas.ForceUpdateCanvases();
                }

                var measured = new List<UiRegion>();
                if (sDeliberateOverlap && regions != null && regions.Count > 0)
                {
                    // A collision nothing in the scene asked for: the board's rectangle again, under
                    // another surface's name. Two surfaces may only share space when they are the
                    // chest panel and its grid, so this pair has to be reported.
                    Rect board = ScreenRectOf(regions[0].Rect, camera);
                    measured.Add(new UiRegion("SelfTestCollision", board, UiRegionKind.Surface));
                }

                if (regions != null)
                {
                    for (int i = 0; i < regions.Count; i++)
                    {
                        Rect rect = ScreenRectOf(regions[i].Rect, camera);
                        if (rect.width <= 0f || rect.height <= 0f)
                        {
                            report.Failures.Add(
                                state + " " + size.x + "x" + size.y + ": " + regions[i].Name
                                + " has no size (is it inactive in the scene?)");
                            continue;
                        }

                        measured.Add(new UiRegion(
                            regions[i].Name, rect, regions[i].Kind, AlphaOf(regions[i].Rect)));
                    }

                    CheckInside(measured, size, state, report);
                }

                camera.Render();

                image = new Texture2D(size.x, size.y, TextureFormat.RGBA32, false);
                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0f, 0f, size.x, size.y), 0, 0);
                image.Apply();
                RenderTexture.active = previous;

                byte[] png = image.EncodeToPNG();
                File.WriteAllBytes(path, png);

                if (overlay != null)
                {
                    Image paint = overlay.GetComponent<Image>();
                    ProveOverlayIsOnTop(
                        overlay, paint != null ? paint.color : Color.black, measured, target, camera,
                        state, size, report);
                }

                camera.targetTexture = previousTarget;

                var violations = new List<string>();
                if (measured.Count > 0)
                {
                    violations = UiLayoutCheck.FindViolations(measured);
                    for (int i = 0; i < violations.Count; i++)
                    {
                        report.Failures.Add(state + " " + size.x + "x" + size.y + ": " + violations[i]);
                    }

                    var opacity = UiLayoutCheck.FindOpacityViolations(measured);
                    for (int i = 0; i < opacity.Count; i++)
                    {
                        report.Failures.Add(state + " " + size.x + "x" + size.y + ": " + opacity[i]);
                        violations.Add(opacity[i]);
                    }

                    // Stage 08d: a grid disjoint from its caption but floating in the middle of the
                    // panel is not an overlap, so the battle HUD's stacking rule is asked separately.
                    var stacked = UiLayoutCheck.FindMiniBagStackViolations(measured);
                    for (int i = 0; i < stacked.Count; i++)
                    {
                        report.Failures.Add(state + " " + size.x + "x" + size.y + ": " + stacked[i]);
                        violations.Add(stacked[i]);
                    }
                }

                report.Shots.Add(new Shot(
                    scene, state, fileName, size.x, size.y, png.Length, started.ElapsedMilliseconds,
                    measured, violations));
            }
            catch (Exception error)
            {
                report.Failures.Add(fileName + ": " + error.GetType().Name + " " + error.Message);
            }
            finally
            {
                for (int i = 0; i < borrowed.Count; i++)
                {
                    borrowed[i].Canvas.renderMode = borrowed[i].Mode;
                    borrowed[i].Canvas.worldCamera = borrowed[i].Camera;
                    borrowed[i].Canvas.planeDistance = borrowed[i].Plane;
                }

                if (image != null)
                {
                    Object.DestroyImmediate(image);
                }

                if (target != null)
                {
                    RenderTexture.ReleaseTemporary(target);
                    if (camera.targetTexture == target)
                    {
                        camera.targetTexture = null;
                    }
                }

                Canvas.ForceUpdateCanvases();
            }
        }

        /// <summary>
        /// Renders the shot a second time with the overlay switched off and asks the pixels whether
        /// the overlay was painted where its rectangle is: the pixel at the overlay's own centre has
        /// to sit <b>closer to the overlay's background colour</b> with it on than with it off.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This is the only check in the run that reads pixels rather than rectangles, and it is the
        /// one that can tell "drawn on top" from "drawn underneath": every other check would pass
        /// with the tooltip painted below the board, because the rectangles would still be where the
        /// scene says.
        /// </para>
        /// <para>
        /// A plain "the pixel changed" test is not enough here, which is a measured fact rather than a
        /// preference: the tooltip is a dark panel over a dark จัดย่าม panel, and the change is 0.024
        /// per channel at 1920×1080 — real, but below the 0.05 first used, and it would have been a
        /// false failure. The direction of the move is what carries the meaning, so that is what is
        /// tested: alpha compositing can only pull a pixel toward the colour being painted over it.
        /// </para>
        /// <para>The scene is left as it was found: the overlay's active state is the caller's.</para>
        /// </remarks>
        private static void ProveOverlayIsOnTop(
            GameObject overlay,
            Color paint,
            List<UiRegion> measured,
            RenderTexture target,
            Camera camera,
            string state,
            Vector2Int size,
            CaptureReport report)
        {
            Rect rect = default;
            string name = "the overlay";

            for (int i = 0; i < measured.Count; i++)
            {
                if (measured[i].Kind == UiRegionKind.Overlay)
                {
                    rect = measured[i].Rect;
                    name = measured[i].Name;
                }
            }

            if (rect.width <= 0f || rect.height <= 0f)
            {
                report.Failures.Add(
                    state + " " + size.x + "x" + size.y + ": " + name + " was not measured");
                return;
            }

            // The centre, and the four points a tenth of the way in from each side: on a rectangle the
            // centre alone would do, and the four are there because a label could sit on it.
            var samples = new Vector2[5];
            samples[0] = rect.center;
            samples[1] = new Vector2(Mathf.Lerp(rect.xMin, rect.xMax, 0.1f), Mathf.Lerp(rect.yMin, rect.yMax, 0.1f));
            samples[2] = new Vector2(Mathf.Lerp(rect.xMin, rect.xMax, 0.9f), Mathf.Lerp(rect.yMin, rect.yMax, 0.1f));
            samples[3] = new Vector2(Mathf.Lerp(rect.xMin, rect.xMax, 0.1f), Mathf.Lerp(rect.yMin, rect.yMax, 0.9f));
            samples[4] = new Vector2(Mathf.Lerp(rect.xMin, rect.xMax, 0.9f), Mathf.Lerp(rect.yMin, rect.yMax, 0.9f));

            var shown = new Color[samples.Length];
            var behind = new Color[samples.Length];

            for (int i = 0; i < samples.Length; i++)
            {
                shown[i] = ReadPixel(target, samples[i]);
            }

            bool wasActive = overlay.activeSelf;
            overlay.SetActive(false);
            Canvas.ForceUpdateCanvases();
            camera.Render();

            for (int i = 0; i < samples.Length; i++)
            {
                behind[i] = ReadPixel(target, samples[i]);
            }

            overlay.SetActive(wasActive);
            Canvas.ForceUpdateCanvases();
            camera.Render();

            string story = string.Empty;
            float best = 0f;

            for (int i = 0; i < samples.Length; i++)
            {
                float before = Distance(behind[i], paint);
                float after = Distance(shown[i], paint);
                float improvement = before - after;

                if (improvement > best)
                {
                    best = improvement;
                }

                story +=
                    (i == 0 ? "centre" : "point " + i)
                    + " (" + Mathf.RoundToInt(samples[i].x) + "," + Mathf.RoundToInt(samples[i].y) + ")"
                    + " is " + improvement.ToString("0.###") + " closer to the tooltip's own colour"
                    + (i == samples.Length - 1 ? string.Empty : ", ");
            }

            if (best < OverlayPixelProofFloor)
            {
                report.Failures.Add(
                    state + " " + size.x + "x" + size.y + ": " + name
                    + " does not paint the pixels inside its own rectangle (the best of "
                    + samples.Length + " samples moved " + best.ToString("0.###") + " toward its "
                    + "background colour when it was switched on, floor "
                    + OverlayPixelProofFloor.ToString("0.###") + "): " + story);
            }
            else
            {
                report.Notes.Add(
                    state + " " + size.x + "x" + size.y + ": " + name
                    + " paints its own rectangle and is therefore drawn above the panel (" + story + ")");
            }
        }

        /// <summary>The largest per-channel difference between two colours.</summary>
        private static float Distance(Color a, Color b) =>
            Mathf.Max(
                Mathf.Abs(a.r - b.r),
                Mathf.Max(Mathf.Abs(a.g - b.g), Mathf.Abs(a.b - b.b)));

        /// <summary>Reads one pixel of <paramref name="target"/> at a point in its own pixel space.</summary>
        private static Color ReadPixel(RenderTexture target, Vector2 point)
        {
            int x = Mathf.Clamp(Mathf.RoundToInt(point.x), 0, target.width - 1);
            int y = Mathf.Clamp(Mathf.RoundToInt(point.y), 0, target.height - 1);

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;

            var pixel = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            pixel.ReadPixels(new Rect(x, y, 1, 1), 0, 0);
            pixel.Apply();
            Color color = pixel.GetPixel(0, 0);
            Object.DestroyImmediate(pixel);

            RenderTexture.active = previous;
            return color;
        }

        /// <summary>
        /// The opacity of the region's own background — the <see cref="Image"/> on the rect the view
        /// serialized, or 1 when the region has none of its own and is only a frame for its children.
        /// </summary>
        private static float AlphaOf(RectTransform rect)
        {
            Image image = rect != null ? rect.GetComponent<Image>() : null;
            return image != null ? image.color.a : 1f;
        }

        /// <summary>
        /// A region that runs off the canvas is a defect the overlap check cannot see: two things can
        /// be disjoint and both half off the screen.
        /// </summary>
        private static void CheckInside(List<UiRegion> regions, Vector2Int size, string state, CaptureReport report)
        {
            for (int i = 0; i < regions.Count; i++)
            {
                if (regions[i].Rect.xMin < -0.5f || regions[i].Rect.yMin < -0.5f
                    || regions[i].Rect.xMax > size.x + 0.5f || regions[i].Rect.yMax > size.y + 0.5f)
                {
                    report.Failures.Add(
                        state + " " + size.x + "x" + size.y + ": " + regions[i]
                        + " is not inside the canvas");
                }
            }
        }

        // ------------------------------------------------------------------
        // scene bookkeeping
        // ------------------------------------------------------------------

        /// <summary>
        /// Opens the scene unless the Editor is on it already. Refuses to open one while another
        /// loaded scene is dirty: Unity would put a save prompt in front of a scripted run, which
        /// looks exactly like a hang.
        /// </summary>
        private static bool EnsureOpen(string scenePath, CaptureReport report)
        {
            Scene active = SceneManager.GetActiveScene();
            if (active.IsValid() && active.path == scenePath && active.isLoaded)
            {
                return true;
            }

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.isDirty)
                {
                    report.Failures.Add(
                        "refusing to open " + scenePath + ": " + scene.path
                        + " has unsaved changes (save or discard it first)");
                    return false;
                }
            }

            try
            {
                Scene opened = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                if (opened.IsValid() && opened.isLoaded)
                {
                    return true;
                }

                report.Failures.Add("could not open " + scenePath);
                return false;
            }
            catch (Exception error)
            {
                report.Failures.Add("could not open " + scenePath + ": " + error.Message);
                return false;
            }
        }

        /// <summary>Puts the Editor back on the scene it started on, when that is safe.</summary>
        private static void RestoreScene(string scenePath, CaptureReport report)
        {
            if (string.IsNullOrEmpty(scenePath) || SceneManager.GetActiveScene().path == scenePath)
            {
                return;
            }

            if (SceneManager.GetActiveScene().isDirty)
            {
                report.Notes.Add("left " + SceneManager.GetActiveScene().path + " open (it has unsaved changes)");
                return;
            }

            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        }

        // ------------------------------------------------------------------
        // measurement
        // ------------------------------------------------------------------

        private static void AddSerializedRects(
            SerializedObject source,
            (string Name, string Field)[] fields,
            UiRegionKind kind,
            List<(string Name, UiRegionKind Kind, RectTransform Rect)> into)
        {
            for (int i = 0; i < fields.Length; i++)
            {
                SerializedProperty property = source.FindProperty(fields[i].Field);
                var rect = property?.objectReferenceValue as RectTransform;
                if (rect == null)
                {
                    continue;
                }

                into.Add((fields[i].Name, kind, rect));
            }
        }

        /// <summary>
        /// The same as <see cref="AddSerializedRects"/>, for a view that serializes a <b>component</b>
        /// rather than the <c>RectTransform</c> itself (the summary screen serializes its labels and
        /// its buttons, which is what its view needs to write into them).
        /// </summary>
        private static void AddSerializedComponents(
            SerializedObject source,
            (string Name, string Field)[] fields,
            UiRegionKind kind,
            List<(string Name, UiRegionKind Kind, RectTransform Rect)> into)
        {
            for (int i = 0; i < fields.Length; i++)
            {
                SerializedProperty property = source.FindProperty(fields[i].Field);
                var component = property?.objectReferenceValue as Component;
                var rect = component != null ? component.transform as RectTransform : null;
                if (rect == null)
                {
                    continue;
                }

                into.Add((fields[i].Name, kind, rect));
            }
        }

        /// <summary>
        /// A transform's rectangle in the pixels of <paramref name="camera"/>'s target: for a camera
        /// whose target is a 1920×1080 texture, that is the 1920×1080 pixel space, bottom-left origin.
        /// </summary>
        private static Rect ScreenRectOf(RectTransform rect, Camera camera)
        {
            if (rect == null || !rect.gameObject.activeInHierarchy)
            {
                return default;
            }

            rect.GetWorldCorners(Corners);

            Vector2 min = RectTransformUtility.WorldToScreenPoint(camera, Corners[0]);
            Vector2 max = RectTransformUtility.WorldToScreenPoint(camera, Corners[2]);

            return Rect.MinMaxRect(
                Mathf.Min(min.x, max.x), Mathf.Min(min.y, max.y),
                Mathf.Max(min.x, max.x), Mathf.Max(min.y, max.y));
        }

        // ------------------------------------------------------------------
        // the report
        // ------------------------------------------------------------------

        private static void WriteReports(
            CaptureReport report,
            long elapsedMs,
            string suffix = "",
            string takenBy = "`ProjectSurvive/Tools/1. Capture UI` (or `-executeMethod "
                + "ProjectSurvive.Game.Editor.UiCaptureTool.CaptureAllAndExit`)",
            string title = "Title, Camp, Battle and Result, in the Editor")
        {
            if (sDeliberateOverlap)
            {
                suffix += "-selftest";
            }

            var text = new StringBuilder();
            text.AppendLine("# UI capture — " + title);
            text.AppendLine();
            text.AppendLine(
                "Taken by " + takenBy + ", "
                + (report.Failures.Count == 0 ? "**PASS**" : "**FAIL**")
                + $" — {report.Failures.Count} failure(s), {elapsedMs} ms total.");
            text.AppendLine();
            text.AppendLine("| Scene | State | Size | Image | Bytes | Regions | Overlaps | ms |");
            text.AppendLine("| --- | --- | --- | --- | --- | --- | --- | --- |");

            for (int i = 0; i < report.Shots.Count; i++)
            {
                Shot shot = report.Shots[i];
                text.AppendLine(string.Format(
                    "| {0} | {1} | {2}×{3} | `artifacts/ui/{4}` | {5} | {6} | {7} | {8} |",
                    shot.Scene, shot.State, shot.Width, shot.Height, shot.FileName, shot.Bytes,
                    shot.Regions.Count, shot.Violations.Count, shot.Milliseconds));
            }

            text.AppendLine();
            text.AppendLine("## Measured regions (pixels, origin bottom-left)");
            text.AppendLine();

            for (int i = 0; i < report.Shots.Count; i++)
            {
                Shot shot = report.Shots[i];
                if (shot.Regions.Count == 0)
                {
                    text.AppendLine($"- **{shot.FileName}** — not measured (no region check for this scene).");
                    continue;
                }

                text.AppendLine($"- **{shot.FileName}**");
                for (int r = 0; r < shot.Regions.Count; r++)
                {
                    UiRegion region = shot.Regions[r];
                    text.AppendLine(
                        $"  - {region.Name} ({region.Kind}, alpha {region.Alpha.ToString("0.###")}): "
                        + string.Format("{0:0.###},{1:0.###} {2:0.###}×{3:0.###}",
                            region.Rect.x, region.Rect.y, region.Rect.width, region.Rect.height));
                }
            }

            if (report.Failures.Count > 0)
            {
                text.AppendLine();
                text.AppendLine("## Failures");
                text.AppendLine();
                for (int i = 0; i < report.Failures.Count; i++)
                {
                    text.AppendLine("- " + report.Failures[i]);
                }
            }

            if (report.Notes.Count > 0)
            {
                text.AppendLine();
                text.AppendLine("## Notes");
                text.AppendLine();
                for (int i = 0; i < report.Notes.Count; i++)
                {
                    text.AppendLine("- " + report.Notes[i]);
                }
            }

            File.WriteAllText(Path.Combine(OutputDirectory, "ui-report" + suffix + ".md"), text.ToString());

            var json = new StringBuilder();
            json.Append("{\n  \"passed\": ").Append(report.Failures.Count == 0 ? "true" : "false");
            json.Append(",\n  \"elapsedMs\": ").Append(elapsedMs);
            json.Append(",\n  \"failures\": [");
            for (int i = 0; i < report.Failures.Count; i++)
            {
                json.Append(i == 0 ? "\n    " : ",\n    ").Append(Quote(report.Failures[i]));
            }

            json.Append(report.Failures.Count > 0 ? "\n  ],\n  \"shots\": [" : "],\n  \"shots\": [");
            for (int i = 0; i < report.Shots.Count; i++)
            {
                Shot shot = report.Shots[i];
                json.Append(i == 0 ? "\n    {" : ",\n    {");
                json.Append("\"scene\": ").Append(Quote(shot.Scene.ToString()));
                json.Append(", \"state\": ").Append(Quote(shot.State));
                json.Append(", \"file\": ").Append(Quote(shot.FileName));
                json.Append(", \"width\": ").Append(shot.Width);
                json.Append(", \"height\": ").Append(shot.Height);
                json.Append(", \"bytes\": ").Append(shot.Bytes);
                json.Append(", \"milliseconds\": ").Append(shot.Milliseconds);
                json.Append(", \"violations\": [");
                for (int v = 0; v < shot.Violations.Count; v++)
                {
                    json.Append(v == 0 ? "" : ", ").Append(Quote(shot.Violations[v]));
                }

                json.Append("], \"regions\": [");
                for (int r = 0; r < shot.Regions.Count; r++)
                {
                    UiRegion region = shot.Regions[r];
                    json.Append(r == 0 ? "\n      " : ",\n      ");
                    json.Append("{\"name\": ").Append(Quote(region.Name));
                    json.Append(", \"kind\": ").Append(Quote(region.Kind.ToString()));
                    json.Append(", \"x\": ").Append(Round(region.Rect.x));
                    json.Append(", \"y\": ").Append(Round(region.Rect.y));
                    json.Append(", \"width\": ").Append(Round(region.Rect.width));
                    json.Append(", \"height\": ").Append(Round(region.Rect.height));
                    json.Append(", \"alpha\": ").Append(Round(region.Alpha));
                    json.Append("}");
                }

                json.Append(shot.Regions.Count > 0 ? "\n    ]}" : "]}");
            }

            json.Append("\n  ]\n}\n");

            File.WriteAllText(Path.Combine(OutputDirectory, "ui-report" + suffix + ".json"), json.ToString());

            string line = string.Format(
                "[ProjectSurvive] UI capture: {0} image(s), {1} failure(s), {2} ms -> {3}",
                report.Shots.Count, report.Failures.Count, elapsedMs, OutputDirectory);

            if (report.Failures.Count == 0)
            {
                Debug.Log(line);
            }
            else
            {
                Debug.LogError(line + "\n" + string.Join("\n", report.Failures));
            }
        }

        private static string Round(float value) =>
            value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);

        private static string Quote(string value)
        {
            var builder = new StringBuilder("\"");
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c == '"' || c == '\\')
                {
                    builder.Append('\\').Append(c);
                }
                else if (c == '\n')
                {
                    builder.Append("\\n");
                }
                else
                {
                    builder.Append(c);
                }
            }

            return builder.Append('"').ToString();
        }

        /// <summary>
        /// One run of the play-mode capture of the merge chooser: opens the window through the real
        /// merge rule, shoots it, takes the window away and judges the rectangles.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A frame at a time out of <c>EditorApplication.update</c>, because entering play mode returns
        /// immediately and the scene is only up a few frames later. Every step either advances or waits;
        /// the whole run is bounded (<see cref="FrameBudget"/>) so a window that never appears fails with
        /// a reason instead of leaving the Editor in play mode for ever.
        /// </para>
        /// <para>
        /// The window is judged twice over: from its <b>rectangles</b> — the cards may not overlap each
        /// other, none may leave the window's frame, the window has to be inside its dim layer, the dim
        /// layer has to cover the screen, and the cards have to be in the left-to-right order the 1/2/3
        /// keys promise — and from the <b>pixels</b>, by taking a second shot with the window closed and
        /// requiring the pixels inside the window's own rectangle to have changed. The second half is what
        /// separates "drawn where it says" from "measured where it says", which is the same distinction
        /// the tooltip's proof draws.
        /// </para>
        /// </remarks>
        private sealed class ChooserCaptureRun
        {
            private const int FrameBudget = 900;
            private const int WindowWaitFrames = 300;
            private const int SettleFrames = 2;

            private static ChooserCaptureRun sRunning;

            private readonly CaptureReport _report = new CaptureReport();
            private readonly System.Diagnostics.Stopwatch _clock = System.Diagnostics.Stopwatch.StartNew();
            private readonly string _startedIn = SceneManager.GetActiveScene().path;

            private Step _step = Step.Prepare;
            private int _frames;
            private int _waited;

            private CampSceneDirector _director;
            private CampBagScreenView _bag;
            private PieceRef _twin;
            private CellOffset _target;

            private Vector2Int _size;
            private string _file = string.Empty;
            private Rect _dim;
            private Rect _panel;
            private Rect[] _cards = new Rect[0];
            private Vector2 _back;
            private string[] _texts = new string[0];
            private Texture2D _withWindow;
            private Texture2D _withoutWindow;

            private enum Step
            {
                Prepare,
                StartCamp,
                OpenPanel,
                BuildPair,
                OpenChooser,
                WaitForWindow,
                Settle,
                ShootOpen,
                ShootClosed,
                Judge,
                Done,
            }

            /// <summary>Starts the run when one is not already in flight.</summary>
            internal static void Start()
            {
                if (sRunning != null)
                {
                    Debug.LogWarning("[ProjectSurvive] the merge-chooser capture is already running.");
                    return;
                }

                if (EditorApplication.isPlaying)
                {
                    Debug.LogWarning(
                        "[ProjectSurvive] the merge-chooser capture starts from edit mode: leave play "
                        + "mode, then run the menu item again.");
                    return;
                }

                sRunning = new ChooserCaptureRun();
                EditorApplication.update += sRunning.Tick;
            }

            private void Tick()
            {
                _frames++;
                _waited++;

                if (_frames > FrameBudget)
                {
                    Fail("the run did not finish within " + FrameBudget + " frames");
                    Finish();
                }

                try
                {
                    switch (_step)
                    {
                        case Step.Prepare:
                            if (!EnsureOpen(CampScenePath, _report))
                            {
                                _report.Notes.Add("the camp scene could not be opened, so nothing was shot");
                                Finish();
                                return;
                            }

                            EditorApplication.isPlaying = true;
                            Advance(Step.StartCamp);
                            return;

                        case Step.StartCamp:
                        {
                            if (!EditorApplication.isPlaying || EditorApplication.isCompiling)
                            {
                                return;
                            }

                            if (SceneManager.GetActiveScene().name != CampSceneName)
                            {
                                return;
                            }

                            _director = Object.FindFirstObjectByType<CampSceneDirector>();
                            if (_director == null)
                            {
                                return;
                            }

                            if (!_director.TryStart())
                            {
                                Fail("the camp did not start: " + _director.StartError);
                                Finish();
                                return;
                            }

                            Advance(Step.OpenPanel);
                            return;
                        }

                        case Step.OpenPanel:
                        {
                            _bag = Object.FindFirstObjectByType<CampBagScreenView>(FindObjectsInactive.Include);
                            if (_bag == null)
                            {
                                Fail("the Camp scene has no CampBagScreenView");
                                Finish();
                                return;
                            }

                            _bag.ShowPanel(true);
                            Canvas.ForceUpdateCanvases();
                            Advance(Step.BuildPair);
                            return;
                        }

                        case Step.BuildPair:
                        {
                            RunSession run = _director.Run;
                            if (run == null || run.Backpack.Items.Count == 0)
                            {
                                Fail("the camp's run holds no item to merge");
                                Finish();
                                return;
                            }

                            ItemInstance first = run.Backpack.Items[0];
                            var added = run.Backpack.AddItem(first.ItemId);
                            if (added.IsFailure)
                            {
                                Fail("a second " + first.ItemId + " could not be added: " + added.Error.Code);
                                Finish();
                                return;
                            }

                            _twin = added.Value;
                            _target = first.Origin;

                            if (!TryPlaceTwin(run, _twin))
                            {
                                Fail("no board cell accepted the second " + first.ItemId);
                                Finish();
                                return;
                            }

                            _director.Presenter.Refresh();
                            Canvas.ForceUpdateCanvases();
                            Advance(Step.OpenChooser);
                            return;
                        }

                        case Step.OpenChooser:
                        {
                            CampPresenter presenter = _director.Presenter;
                            presenter.DragStart(_twin, Rotation.Degrees0);

                            if (!presenter.IsDragging)
                            {
                                Fail(
                                    "the drag never started, so no merge could be asked for (the bag is "
                                    + (presenter.Run.IsBagLocked ? "locked" : "not locked")
                                    + ", the flow state is " + presenter.Run.FlowState + ")");
                                Finish();
                                return;
                            }

                            presenter.DragMove(BagDropZone.Board, _target);
                            presenter.Drop();
                            Advance(Step.WaitForWindow);
                            return;
                        }

                        case Step.WaitForWindow:
                        {
                            if (_bag.ChooserVisible && _director.Presenter.IsMergeChooserOpen)
                            {
                                Advance(Step.Settle);
                                return;
                            }

                            if (_waited <= WindowWaitFrames)
                            {
                                return;
                            }

                            Fail(
                                "the merge never opened the window: after " + WindowWaitFrames
                                + " frames the rule " + (_director.Presenter.IsMergeChooserOpen ? "has it open" : "has not opened it")
                                + " and the screen draws " + _bag.ChooserCardCount + " card(s)");
                            Finish();
                            return;
                        }

                        case Step.Settle:
                        {
                            if (_waited < SettleFrames)
                            {
                                return;
                            }

                            if (!Measure())
                            {
                                Finish();
                                return;
                            }

                            Advance(Step.ShootOpen);
                            return;
                        }

                        case Step.ShootOpen:
                        {
                            if (!Shoot(withWindow: true))
                            {
                                Finish();
                                return;
                            }

                            _texts = Texts();
                            _director.Presenter.CancelMergeChooser();
                            Canvas.ForceUpdateCanvases();
                            Advance(Step.ShootClosed);
                            return;
                        }

                        case Step.ShootClosed:
                        {
                            if (_waited < SettleFrames || _bag.ChooserVisible)
                            {
                                return;
                            }

                            if (!Shoot(withWindow: false))
                            {
                                Finish();
                                return;
                            }

                            Advance(Step.Judge);
                            return;
                        }

                        case Step.Judge:
                            Judge();
                            Finish();
                            return;

                        case Step.Done:
                            if (EditorApplication.isPlaying || _waited < SettleFrames)
                            {
                                return;
                            }

                            Complete();
                            return;

                        default:
                            return;
                    }
                }
                catch (Exception error)
                {
                    Fail(error.GetType().Name + ": " + error.Message);
                    Finish();
                }
            }

            private void Advance(Step next)
            {
                _step = next;
                _waited = 0;
            }

            /// <summary>Leaves play mode and moves to the reporting step.</summary>
            private void Finish()
            {
                EditorApplication.isPlaying = false;
                _step = Step.Done;
                _waited = 0;
            }

            private void Fail(string reason) => _report.Failures.Add("merge chooser: " + reason);

            /// <summary>
            /// Stands the twin on the first board cell the run accepts, scanning from the far corner. The
            /// placement rule is the run's — this asks it and never re-implements it.
            /// </summary>
            private static bool TryPlaceTwin(RunSession run, PieceRef twin)
            {
                for (int y = run.Backpack.Board.Height - 1; y >= 0; y--)
                {
                    for (int x = run.Backpack.Board.Width - 1; x >= 0; x--)
                    {
                        if (run.MovePiece(twin, new CellOffset(x, y), Rotation.Degrees0).IsSuccess)
                        {
                            return true;
                        }
                    }
                }

                return false;
            }

            /// <summary>Reads the window's own rectangles. False, with a failure, when one is missing.</summary>
            private bool Measure()
            {
                if (!_bag.TryGetChooserDimRect(out _dim))
                {
                    Fail("the window is up but the view did not report its dim layer");
                    return false;
                }

                if (!_bag.TryGetChooserPanelRect(out _panel))
                {
                    Fail("the window is up but the view did not report its own frame");
                    return false;
                }

                int cards = _bag.ChooserCardCount;
                if (cards < 2)
                {
                    Fail("the window is offering " + cards + " card(s): there is nothing to choose between");
                    return false;
                }

                _cards = new Rect[cards];
                for (int i = 0; i < cards; i++)
                {
                    if (!_bag.TryGetChoiceRect(i, out _cards[i]))
                    {
                        Fail("card " + i + " has no rectangle on screen");
                        return false;
                    }
                }

                if (!_bag.TryGetChooserBackScreenPoint(out _back))
                {
                    Fail("the window's back button is not on screen");
                    return false;
                }

                _size = new Vector2Int(Screen.width, Screen.height);
                _file = "camp-merge-chooser-" + _size.x + "x" + _size.y + ".png";
                return true;
            }

            /// <summary>One shot of the Game View as it stands, with the window up or taken away.</summary>
            private bool Shoot(bool withWindow)
            {
                Canvas.ForceUpdateCanvases();

                Texture2D shot = ScreenCapture.CaptureScreenshotAsTexture();
                if (shot == null)
                {
                    Fail("the Game View could not be captured");
                    return false;
                }

                if (shot.width != _size.x || shot.height != _size.y)
                {
                    // Fatal on purpose: the whole reason this run does not re-render the screen through a
                    // RenderTexture is that the picture and the rectangles have to be of one size.
                    Fail(
                        "the shot came back " + shot.width + "x" + shot.height + " while the screen reports "
                        + _size.x + "x" + _size.y + ": the picture and the rectangles are not of one size");
                    Object.DestroyImmediate(shot);
                    return false;
                }

                if (withWindow)
                {
                    _withWindow = shot;
                }
                else
                {
                    _withoutWindow = shot;
                }

                return true;
            }

            /// <summary>The strings the window is drawing, so the picture and the report can be read together.</summary>
            private string[] Texts()
            {
                CampMergeChooserModel chooser = _director.Presenter.MergeChooser;
                var texts = new List<string>(chooser.ChoiceCount + 3)
                {
                    "title=\"" + chooser.Title + "\"",
                    "hint=\"" + chooser.Hint + "\"",
                    "back=\"" + chooser.BackLabel + "\"",
                };

                for (int i = 0; i < chooser.ChoiceCount; i++)
                {
                    CampMergeChoiceModel card = chooser.Choices[i];
                    texts.Add(
                        "card " + (i + 1) + "=\"" + card.Title + "\" " + card.LevelLine + " ["
                        + string.Join(" · ", card.Changes) + "]");
                }

                return texts.ToArray();
            }

            /// <summary>
            /// The layout verdict for the window: the same rules the other states are judged by, plus the
            /// two a modal adds — its controls stay inside its frame, and its cards are laid out in the
            /// left-to-right order the number keys promise.
            /// </summary>
            private void Judge()
            {
                var regions = new List<UiRegion>
                {
                    new UiRegion(UiLayoutCheck.MergeChooserDim, _dim, UiRegionKind.Overlay),
                    new UiRegion(UiLayoutCheck.MergeChooser, _panel, UiRegionKind.Overlay),
                };

                for (int i = 0; i < _cards.Length; i++)
                {
                    regions.Add(new UiRegion(
                        UiLayoutCheck.MergeChooserCard(i), _cards[i], UiRegionKind.ModalControl));
                }

                CheckInside(regions, _size, "merge chooser", _report);

                var violations = UiLayoutCheck.FindViolations(regions);
                violations.AddRange(UiLayoutCheck.FindModalEscapees(regions, UiLayoutCheck.MergeChooser));

                for (int i = 1; i < _cards.Length; i++)
                {
                    if (_cards[i].center.x <= _cards[i - 1].center.x)
                    {
                        violations.Add(
                            UiLayoutCheck.MergeChooserCard(i) + " is not to the right of "
                            + UiLayoutCheck.MergeChooserCard(i - 1)
                            + ": the 1/2/3 keys and the cards would disagree");
                    }
                }

                if (!UiLayoutCheck.Contains(_panel, _dim) && !UiLayoutCheck.Contains(_dim, _panel))
                {
                    violations.Add("the window " + _panel + " and its dim layer " + _dim + " are not nested");
                }

                if (!UiLayoutCheck.Contains(new Rect(0f, 0f, _size.x, _size.y), _dim))
                {
                    violations.Add("the dim layer " + _dim + " does not cover the screen");
                }

                if (!_panel.Contains(_back))
                {
                    violations.Add("the back button " + _back + " is not inside the window " + _panel);
                }

                for (int i = 0; i < violations.Count; i++)
                {
                    _report.Failures.Add("merge chooser " + _size.x + "x" + _size.y + ": " + violations[i]);
                }

                _report.Notes.Add("the window is drawing: " + string.Join(" | ", _texts));

                // The pixel half is wrapped on its own: a shot whose pixels cannot be read is worth a
                // note, and it must not cost the run the images and the rectangles it already has.
                try
                {
                    _report.Notes.Add("dimming: " + Dimming());
                    _report.Notes.Add("painted: " + PaintedProof());
                }
                catch (Exception error)
                {
                    _report.Notes.Add(
                        "the shots' pixels could not be read (" + error.GetType().Name + ": "
                        + error.Message + "), so the pixel proof and the dimming number are missing");
                }

                _report.Notes.Add(
                    "two numbers this command reads for the other panels are not read here, because the "
                    + "view hands back rectangles rather than labels and images: the window's label sizes "
                    + "(the 22 px floor is therefore not a number in this report) and the dim layer's own "
                    + "opacity (what is measured instead is the luminance change over its rectangle, above)");

                byte[] png = Save(_withWindow, _file);
                if (png == null)
                {
                    return;
                }

                Save(_withoutWindow, "camp-merge-chooser-closed-" + _size.x + "x" + _size.y + ".png");

                _report.Shots.Add(new Shot(
                    SceneKind.Camp, "merge-chooser", _file, _size.x, _size.y, png.Length,
                    _clock.ElapsedMilliseconds, regions, violations));
            }

            /// <summary>
            /// The pixel half of the judgement: five points inside the window's own rectangle have to have
            /// changed when the window was taken away, which is what separates "drawn there" from
            /// "measured there".
            /// </summary>
            private string PaintedProof()
            {
                if (_withWindow == null || _withoutWindow == null)
                {
                    return "not measured (a shot is missing)";
                }

                var points = new Vector2[5];
                points[0] = _panel.center;
                points[1] = new Vector2(Mathf.Lerp(_panel.xMin, _panel.xMax, 0.25f), Mathf.Lerp(_panel.yMin, _panel.yMax, 0.25f));
                points[2] = new Vector2(Mathf.Lerp(_panel.xMin, _panel.xMax, 0.75f), Mathf.Lerp(_panel.yMin, _panel.yMax, 0.25f));
                points[3] = new Vector2(Mathf.Lerp(_panel.xMin, _panel.xMax, 0.25f), Mathf.Lerp(_panel.yMin, _panel.yMax, 0.75f));
                points[4] = new Vector2(Mathf.Lerp(_panel.xMin, _panel.xMax, 0.75f), Mathf.Lerp(_panel.yMin, _panel.yMax, 0.75f));

                int moved = 0;
                float best = 0f;
                for (int i = 0; i < points.Length; i++)
                {
                    Color shown = _withWindow.GetPixel(
                        Mathf.Clamp(Mathf.RoundToInt(points[i].x), 0, _withWindow.width - 1),
                        Mathf.Clamp(Mathf.RoundToInt(points[i].y), 0, _withWindow.height - 1));
                    Color behind = _withoutWindow.GetPixel(
                        Mathf.Clamp(Mathf.RoundToInt(points[i].x), 0, _withoutWindow.width - 1),
                        Mathf.Clamp(Mathf.RoundToInt(points[i].y), 0, _withoutWindow.height - 1));

                    float difference = Distance(shown, behind);
                    if (difference > OverlayPixelProofFloor)
                    {
                        moved++;
                    }

                    if (difference > best)
                    {
                        best = difference;
                    }
                }

                if (moved == 0)
                {
                    _report.Failures.Add(
                        "merge chooser " + _size.x + "x" + _size.y
                        + ": none of the " + points.Length + " sample points inside the window's own rectangle "
                        + "changed when it was closed, so the window is not drawn where it says (best "
                        + best.ToString("0.###") + ", floor " + OverlayPixelProofFloor.ToString("0.###") + ")");
                }

                return moved + " of " + points.Length + " sample points inside the window changed when it was "
                    + "closed (best " + best.ToString("0.###") + ", floor "
                    + OverlayPixelProofFloor.ToString("0.###") + ")";
            }

            /// <summary>How dark the dim layer makes the screen, as a mean over its own rectangle.</summary>
            private string Dimming()
            {
                if (_withWindow == null || _withoutWindow == null)
                {
                    return "not measured (a shot is missing)";
                }

                return "mean luminance over the dim layer "
                    + MeanLuminance(_withWindow, _dim).ToString("0.###") + " with the window up, "
                    + MeanLuminance(_withoutWindow, _dim).ToString("0.###") + " with it closed";
            }

            /// <summary>The mean 0..1 luminance of a rectangle of a shot, over a 16×9 grid of samples.</summary>
            private static float MeanLuminance(Texture2D image, Rect rect)
            {
                const int Columns = 16;
                const int Rows = 9;

                float total = 0f;
                int samples = 0;

                for (int row = 0; row < Rows; row++)
                {
                    for (int column = 0; column < Columns; column++)
                    {
                        float x = Mathf.Lerp(rect.xMin, rect.xMax, (column + 0.5f) / Columns);
                        float y = Mathf.Lerp(rect.yMin, rect.yMax, (row + 0.5f) / Rows);

                        if (x < 0f || y < 0f || x >= image.width || y >= image.height)
                        {
                            continue;
                        }

                        Color color = image.GetPixel(Mathf.RoundToInt(x), Mathf.RoundToInt(y));
                        total += (color.r + color.g + color.b) / 3f;
                        samples++;
                    }
                }

                return samples == 0 ? 0f : total / samples;
            }

            private byte[] Save(Texture2D texture, string fileName)
            {
                if (texture == null)
                {
                    return null;
                }

                try
                {
                    byte[] png = texture.EncodeToPNG();
                    File.WriteAllBytes(Path.Combine(OutputDirectory, fileName), png);
                    return png;
                }
                catch (Exception error)
                {
                    Fail("could not write " + fileName + ": " + error.Message);
                    return null;
                }
            }

            private void Complete()
            {
                EditorApplication.update -= Tick;
                sRunning = null;

                if (_withWindow != null)
                {
                    Object.DestroyImmediate(_withWindow);
                }

                if (_withoutWindow != null)
                {
                    Object.DestroyImmediate(_withoutWindow);
                }

                RestoreScene(_startedIn, _report);

                string title = _size.x > 0 && _size.y > 0
                    ? "the merge chooser, in Play Mode (" + _size.x + "x" + _size.y + ")"
                    : "the merge chooser, in Play Mode (no shot was taken)";

                WriteReports(
                    _report, _clock.ElapsedMilliseconds, "-playmode",
                    "`ProjectSurvive/Tools/3. Capture the merge chooser in Play Mode (Camp)`",
                    title);
            }
        }

        /// <summary>
        /// The tooltip's numbers before and after an upgrade, shot in Play Mode — the one state the
        /// edit-mode capture cannot show, because its labels are empty until the runtime fills them.
        /// </summary>
        private sealed class TooltipCaptureRun
        {
            private const int FrameBudget = 900;
            private const int SettleFrames = 3;

            /// <summary>
            /// Session marker for "a capture is waiting for Play Mode". Entering Play Mode reloads the
            /// script domain in this project (verified: <c>EditorSettings.enterPlayModeOptions</c> is
            /// <c>None</c>), which drops both the static run and its <c>EditorApplication.update</c>
            /// handler — so the run is re-created from this flag after the reload instead of dying with
            /// the domain it started in.
            /// </summary>
            private const string PendingKey = "ProjectSurvive.TooltipCapture.Pending";

            private static TooltipCaptureRun sRunning;

            private readonly CaptureReport _report = new CaptureReport();
            private readonly System.Diagnostics.Stopwatch _clock = System.Diagnostics.Stopwatch.StartNew();
            private readonly string _startedIn = SceneManager.GetActiveScene().path;

            private Step _step;
            private int _frames;
            private int _waited;

            private CampSceneDirector _director;
            private CampBagScreenView _bag;
            private CampTooltipView _tooltip;
            private PieceRef _weapon;

            private Vector2Int _size;
            private Rect _rect;
            private Texture2D _before;
            private Texture2D _withoutTooltip;
            private Texture2D _after;
            private string[] _beforeTexts = new string[0];
            private string[] _afterTexts = new string[0];

            private enum Step
            {
                Prepare,
                StartCamp,
                OpenPanel,
                Hover,
                SettleBefore,
                ShootBefore,
                HideTooltip,
                ShootHidden,
                Upgrade,
                HoverAgain,
                SettleAfter,
                ShootAfter,
                Judge,
                Done,
            }

            private TooltipCaptureRun(Step start)
            {
                _step = start;
            }

            /// <summary>Starts the run when one is not already in flight.</summary>
            internal static void Start()
            {
                if (sRunning != null)
                {
                    Debug.LogWarning("[ProjectSurvive] the tooltip capture is already running.");
                    return;
                }

                if (EditorApplication.isPlaying)
                {
                    Debug.LogWarning(
                        "[ProjectSurvive] the tooltip capture starts from edit mode: leave play mode, then "
                        + "run the menu item again.");
                    return;
                }

                sRunning = new TooltipCaptureRun(Step.Prepare);
                SessionState.SetBool(PendingKey, true);
                EditorApplication.update += sRunning.Tick;
            }

            /// <summary>
            /// Picks the run back up after the domain reload that entering Play Mode causes. A no-op
            /// when no capture was waiting.
            /// </summary>
            [InitializeOnLoadMethod]
            private static void ResumeAfterReload()
            {
                if (!SessionState.GetBool(PendingKey, false) || sRunning != null)
                {
                    return;
                }

                sRunning = new TooltipCaptureRun(Step.StartCamp);
                EditorApplication.update += sRunning.Tick;
            }

            private void Tick()
            {
                _frames++;
                _waited++;

                if (_frames > FrameBudget)
                {
                    Fail("the run did not finish within " + FrameBudget + " frames");
                    Finish();
                }

                try
                {
                    switch (_step)
                    {
                        case Step.Prepare:
                            if (!EnsureOpen(CampScenePath, _report))
                            {
                                _report.Notes.Add("the camp scene could not be opened, so nothing was shot");
                                Finish();
                                return;
                            }

                            EditorApplication.isPlaying = true;
                            Advance(Step.StartCamp);
                            return;

                        case Step.StartCamp:
                        {
                            if (!EditorApplication.isPlaying || EditorApplication.isCompiling)
                            {
                                return;
                            }

                            if (SceneManager.GetActiveScene().name != CampSceneName)
                            {
                                return;
                            }

                            _director = Object.FindFirstObjectByType<CampSceneDirector>();
                            if (_director == null)
                            {
                                return;
                            }

                            if (!_director.TryStart())
                            {
                                Fail("the camp did not start: " + _director.StartError);
                                Finish();
                                return;
                            }

                            Advance(Step.OpenPanel);
                            return;
                        }

                        case Step.OpenPanel:
                        {
                            _bag = Object.FindFirstObjectByType<CampBagScreenView>(FindObjectsInactive.Include);
                            _tooltip = Object.FindFirstObjectByType<CampTooltipView>(FindObjectsInactive.Include);

                            if (_bag == null || _tooltip == null)
                            {
                                Fail(
                                    "the Camp scene is missing "
                                    + (_bag == null ? "CampBagScreenView " : string.Empty)
                                    + (_tooltip == null ? "CampTooltipView" : string.Empty));
                                Finish();
                                return;
                            }

                            _bag.ShowPanel(true);
                            Canvas.ForceUpdateCanvases();
                            Advance(Step.Hover);
                            return;
                        }

                        case Step.Hover:
                        {
                            RunSession run = _director.Run;
                            if (run == null || run.Backpack.Items.Count == 0)
                            {
                                Fail("the camp's run holds no item to hover");
                                Finish();
                                return;
                            }

                            _weapon = run.Backpack.Items[0].Ref;
                            _director.Presenter.Hover(_weapon);
                            Canvas.ForceUpdateCanvases();
                            Advance(Step.SettleBefore);
                            return;
                        }

                        case Step.SettleBefore:
                        {
                            if (_waited < SettleFrames)
                            {
                                return;
                            }

                            if (!_tooltip.Visible)
                            {
                                Fail("hovering the piece did not put the tooltip on screen");
                                Finish();
                                return;
                            }

                            if (!_tooltip.TryGetScreenRect(out _rect))
                            {
                                Fail("the tooltip did not report a rectangle, so nothing can be measured");
                                Finish();
                                return;
                            }

                            // The acceptance size is the tool's own, not the window's: the shot is
                            // rendered into a 1920x1080 target below, so the report names the size it
                            // really has even when the Editor is not that size.
                            _size = CapturedSizes[0];
                            _beforeTexts = Texts();
                            Advance(Step.ShootBefore);
                            return;
                        }

                        case Step.ShootBefore:
                        {
                            if (!Shoot(ref _before))
                            {
                                Finish();
                                return;
                            }

                            // Take the tooltip away for one shot, so "it is drawn where it says" is a
                            // measurement of the pixels rather than a claim about the rect.
                            TooltipRoot().SetActive(false);
                            Canvas.ForceUpdateCanvases();
                            Advance(Step.HideTooltip);
                            return;
                        }

                        case Step.HideTooltip:
                        {
                            if (_waited < SettleFrames)
                            {
                                return;
                            }

                            Advance(Step.ShootHidden);
                            return;
                        }

                        case Step.ShootHidden:
                        {
                            if (!Shoot(ref _withoutTooltip))
                            {
                                Finish();
                                return;
                            }

                            Advance(Step.Upgrade);
                            return;
                        }

                        case Step.Upgrade:
                        {
                            RunSession run = _director.Run;

                            if (!TryUpgrade(run, _weapon, out string failure))
                            {
                                Fail(failure);
                                Finish();
                                return;
                            }

                            // The tooltip must be republished from the run's new state: refresh the
                            // screen, then take the pointer off and back on the same piece.
                            _director.Presenter.Refresh();
                            _director.Presenter.ClearHover();
                            _director.Presenter.Hover(_weapon);
                            Canvas.ForceUpdateCanvases();
                            Advance(Step.HoverAgain);
                            return;
                        }

                        case Step.HoverAgain:
                        {
                            if (!_tooltip.Visible)
                            {
                                Fail("the tooltip did not come back after the upgrade");
                                Finish();
                                return;
                            }

                            Advance(Step.SettleAfter);
                            return;
                        }

                        case Step.SettleAfter:
                        {
                            if (_waited < SettleFrames)
                            {
                                return;
                            }

                            if (!_tooltip.TryGetScreenRect(out _rect))
                            {
                                Fail("the tooltip did not report a rectangle after the upgrade");
                                Finish();
                                return;
                            }

                            _afterTexts = Texts();
                            Advance(Step.ShootAfter);
                            return;
                        }

                        case Step.ShootAfter:
                        {
                            if (!Shoot(ref _after))
                            {
                                Finish();
                                return;
                            }

                            Advance(Step.Judge);
                            return;
                        }

                        case Step.Judge:
                            Judge();
                            Finish();
                            return;

                        case Step.Done:
                            if (EditorApplication.isPlaying || _waited < SettleFrames)
                            {
                                return;
                            }

                            Complete();
                            return;

                        default:
                            return;
                    }
                }
                catch (Exception error)
                {
                    Fail(error.GetType().Name + ": " + error.Message);
                    Finish();
                }
            }

            private void Advance(Step next)
            {
                _step = next;
                _waited = 0;
            }

            private void Finish()
            {
                EditorApplication.isPlaying = false;
                _step = Step.Done;
                _waited = 0;
            }

            private void Fail(string reason) => _report.Failures.Add("tooltip: " + reason);

            /// <summary>The panel the tooltip draws on, for the "taken away" shot.</summary>
            private GameObject TooltipRoot()
            {
                var fields = new SerializedObject(_tooltip);
                return fields.FindProperty("_root")?.objectReferenceValue as GameObject;
            }

            private TextMeshProUGUI LabelNamed(string field)
            {
                var fields = new SerializedObject(_tooltip);
                var property = fields.FindProperty(field);
                return property?.objectReferenceValue as TextMeshProUGUI;
            }

            /// <summary>The six strings the tooltip is drawing, in the screen's own order.</summary>
            private string[] Texts()
            {
                string[] fields =
                {
                    "_titleLabel", "_tierLabel", "_statusLabel", "_upgradesLabel",
                    "_descriptionLabel", "_combosLabel",
                };

                var texts = new List<string>(fields.Length);

                for (int i = 0; i < fields.Length; i++)
                {
                    TextMeshProUGUI label = LabelNamed(fields[i]);
                    texts.Add(
                        fields[i] + (label == null
                            ? "=<missing label>"
                            : label.gameObject.activeSelf ? "=\"" + label.text.Replace("\n", " | ") + "\"" : "=(hidden)"));
                }

                return texts.ToArray();
            }

            /// <summary>
            /// Two merges through the run's own commands, preferring a cooldown variant at each step so
            /// the after-shot shows a number that moved for a stated reason.
            /// </summary>
            private static bool TryUpgrade(RunSession run, PieceRef weapon, out string failure)
            {
                failure = string.Empty;

                if (!run.Backpack.TryGetPiece(weapon, out PieceInstance? piece) || piece is not ItemInstance item)
                {
                    failure = "the hovered piece is not an item";
                    return false;
                }

                string itemId = item.ItemId;

                if (!MergeOne(run, weapon, itemId, out failure))
                {
                    return false;
                }

                // A tier-2 partner for the last step: two fresh copies folded together.
                var a = run.Backpack.AddItem(itemId);
                if (a.IsFailure)
                {
                    failure = "a copy of " + itemId + " could not be added: " + a.Error;
                    return false;
                }

                if (!MergeOne(run, a.Value, itemId, out failure))
                {
                    return false;
                }

                return MergeOne(run, weapon, itemId, out failure, a.Value);
            }

            private static bool MergeOne(
                RunSession run, PieceRef target, string itemId, out string failure, PieceRef? source = null)
            {
                failure = string.Empty;

                PieceRef twin = source ?? default;
                if (source is null)
                {
                    var added = run.Backpack.AddItem(itemId);
                    if (added.IsFailure)
                    {
                        failure = "a copy of " + itemId + " could not be added: " + added.Error;
                        return false;
                    }

                    twin = added.Value;
                }

                var offered = run.OfferMerge(twin, target);
                if (offered.IsFailure)
                {
                    failure = "the merge offer was refused: " + offered.Error;
                    return false;
                }

                string chosen = offered.Value.Options[0].Id;
                for (int i = 0; i < offered.Value.Options.Count; i++)
                {
                    if (!TouchesCooldown(offered.Value.Options[i]))
                    {
                        continue;
                    }

                    chosen = offered.Value.Options[i].Id;
                    break;
                }

                var merged = run.ConfirmMerge(twin, target, chosen);
                if (merged.IsFailure)
                {
                    failure = "the merge was refused: " + merged.Error;
                    return false;
                }

                return true;
            }

            private static bool TouchesCooldown(UpgradeVariantConfig variant)
            {
                for (int i = 0; i < variant.Modifiers.Count; i++)
                {
                    if (variant.Modifiers[i].Stat == UpgradeStat.Cooldown)
                    {
                        return true;
                    }
                }

                return false;
            }

            /// <summary>
            /// Renders one frame into a <see cref="_size"/>-sized target and reads it back.
            /// </summary>
            /// <remarks>
            /// <para>
            /// <c>ScreenCapture.CaptureScreenshotAsTexture</c> is deliberately not used: from an
            /// unfocused Editor it comes back all black (measured in this stage: both shots were
            /// 1920x1080 and one flat colour), which is also why the "is it drawn there" check
            /// failed rather than passed. The rest of this tool already solves that by borrowing the
            /// scene's camera: the root overlay canvases are switched to camera mode for the shot,
            /// pointed at that camera, whose <c>targetTexture</c> is a render texture of exactly the
            /// size being measured. The tooltip and the camp are then drawn by the same camera that
            /// draws the game, in one pass, in the Editor and regardless of window focus.
            /// </para>
            /// <para>
            /// The tooltip's rectangle is re-measured in the target's pixels while the canvases are
            /// in that mode, so the pixels the painted check samples and the rectangle the report
            /// prints are of the same frame.
            /// </para>
            /// </remarks>
            private bool Shoot(ref Texture2D into)
            {
                Canvas.ForceUpdateCanvases();

                Camera camera = Camera.main;
                if (camera == null)
                {
                    Fail("the Camp scene has no MainCamera to render the shot with");
                    return false;
                }

                var borrowed = new List<(Canvas Canvas, RenderMode Mode, Camera Camera, float Plane)>();
                RenderTexture target = RenderTexture.GetTemporary(
                    _size.x, _size.y, 24, RenderTextureFormat.ARGB32);
                RenderTexture previousTarget = camera.targetTexture;
                Texture2D shot = null;

                try
                {
                    Canvas[] canvases = Object.FindObjectsByType<Canvas>(
                        FindObjectsInactive.Include, FindObjectsSortMode.None);

                    for (int i = 0; i < canvases.Length; i++)
                    {
                        Canvas canvas = canvases[i];
                        if (canvas.rootCanvas != canvas
                            || canvas.renderMode != RenderMode.ScreenSpaceOverlay
                            || !canvas.gameObject.activeInHierarchy)
                        {
                            continue;
                        }

                        borrowed.Add((canvas, canvas.renderMode, canvas.worldCamera, canvas.planeDistance));
                        canvas.renderMode = RenderMode.ScreenSpaceCamera;
                        canvas.worldCamera = camera;
                        canvas.planeDistance = camera.nearClipPlane + 1f;
                    }

                    camera.targetTexture = target;
                    Canvas.ForceUpdateCanvases();

                    RectTransform panel = TooltipRoot()?.transform as RectTransform;
                    Rect measured = ScreenRectOf(panel, camera);
                    if (measured.width > 0f && measured.height > 0f)
                    {
                        _rect = measured;
                    }

                    camera.Render();

                    shot = new Texture2D(_size.x, _size.y, TextureFormat.RGBA32, false);
                    RenderTexture previous = RenderTexture.active;
                    RenderTexture.active = target;
                    shot.ReadPixels(new Rect(0f, 0f, _size.x, _size.y), 0, 0);
                    shot.Apply();
                    RenderTexture.active = previous;
                }
                catch (Exception error)
                {
                    Fail("the frame could not be rendered (" + error.GetType().Name + ": " + error.Message + ")");
                    return false;
                }
                finally
                {
                    camera.targetTexture = previousTarget;

                    for (int i = 0; i < borrowed.Count; i++)
                    {
                        borrowed[i].Canvas.renderMode = borrowed[i].Mode;
                        borrowed[i].Canvas.worldCamera = borrowed[i].Camera;
                        borrowed[i].Canvas.planeDistance = borrowed[i].Plane;
                    }

                    RenderTexture.ReleaseTemporary(target);
                    Canvas.ForceUpdateCanvases();
                }

                if (shot == null)
                {
                    Fail("the frame produced no texture");
                    return false;
                }

                into = shot;
                return true;
            }

            /// <summary>
            /// The stage's verdict: the tooltip stays on screen, the two shots differ, the after-shot
            /// names a variant and prints the resolved cooldown the read model reports, and the pixels
            /// inside the tooltip's rectangle change when it is taken away.
            /// </summary>
            private void Judge()
            {
                var regions = new List<UiRegion> { new UiRegion(UiLayoutCheck.Tooltip, _rect, UiRegionKind.Overlay) };
                CheckInside(regions, _size, "tooltip", _report);

                _report.Notes.Add("drawing before the upgrade: " + string.Join(" | ", _beforeTexts));
                _report.Notes.Add("drawing after the upgrade:  " + string.Join(" | ", _afterTexts));

                string before = string.Join("\n", _beforeTexts);
                string after = string.Join("\n", _afterTexts);

                if (before == after)
                {
                    Fail("the tooltip says the same thing before and after the upgrade");
                }

                RunSession run = _director.Run;
                var readout = run.ExplainPiece(_weapon);
                if (readout.IsFailure)
                {
                    Fail("the run cannot explain the hovered piece: " + readout.Error);
                    return;
                }

                PieceReadout explained = readout.Value;
                _report.Notes.Add(
                    "resolved after the upgrade: tier " + explained.Tier
                    + ", damage " + explained.Resolved.Damage + " (base " + explained.Base.Damage + ")"
                    + ", cooldown " + explained.Resolved.CooldownSeconds.ToString("0.###")
                    + " (base " + explained.Base.CooldownSeconds.ToString("0.###") + ")"
                    + ", focus " + explained.Resolved.MpCost + " (base " + explained.Base.MpCost + ")"
                    + ", steps " + explained.Steps.Count
                    + ", combos " + explained.ComboRuleIds.Count
                    + ", min_cooldown " + explained.MinCooldownSeconds.ToString("0.###")
                    + " (at minimum: " + explained.CooldownAtMinimum + ")");

                if (explained.Steps.Count == 0)
                {
                    Fail("the piece has no upgrade step after two merges");
                }

                TextMeshProUGUI statusLabel = LabelNamed("_statusLabel");
                TextMeshProUGUI upgradesLabel = LabelNamed("_upgradesLabel");
                string cooldown = explained.Resolved.CooldownSeconds.ToString("0.##", CultureInfo.InvariantCulture);

                if (statusLabel == null || !statusLabel.text.Contains(cooldown, StringComparison.Ordinal))
                {
                    Fail(
                        "the สถานะ line does not print the resolved cooldown " + cooldown + ": "
                        + (statusLabel == null ? "<no label>" : statusLabel.text));
                }

                if (upgradesLabel == null || !upgradesLabel.gameObject.activeSelf
                    || string.IsNullOrEmpty(upgradesLabel.text))
                {
                    Fail("the รุ่นย่อย line is not drawn after the upgrade");
                }

                try
                {
                    _report.Notes.Add("painted: " + PaintedProof());
                }
                catch (Exception error)
                {
                    _report.Notes.Add(
                        "the shots' pixels could not be read (" + error.GetType().Name + ": "
                        + error.Message + "), so the pixel proof is missing");
                }

                string size = _size.x + "x" + _size.y;
                byte[] png = Save(_before, "stage09h-tooltip-before-" + size + ".png");
                if (png == null)
                {
                    return;
                }

                Save(_after, "stage09h-tooltip-after-" + size + ".png");

                _report.Shots.Add(new Shot(
                    SceneKind.Camp, "tooltip", "stage09h-tooltip-after-" + size + ".png",
                    _size.x, _size.y, png.Length, _clock.ElapsedMilliseconds, regions, new List<string>()));
            }

            /// <summary>
            /// Five points inside the tooltip's own rectangle have to change when it is taken away —
            /// "drawn there" rather than "measured there".
            /// </summary>
            private string PaintedProof()
            {
                if (_before == null || _withoutTooltip == null)
                {
                    return "not measured (a shot is missing)";
                }

                var points = new Vector2[5];
                points[0] = _rect.center;
                points[1] = new Vector2(Mathf.Lerp(_rect.xMin, _rect.xMax, 0.25f), Mathf.Lerp(_rect.yMin, _rect.yMax, 0.25f));
                points[2] = new Vector2(Mathf.Lerp(_rect.xMin, _rect.xMax, 0.75f), Mathf.Lerp(_rect.yMin, _rect.yMax, 0.25f));
                points[3] = new Vector2(Mathf.Lerp(_rect.xMin, _rect.xMax, 0.25f), Mathf.Lerp(_rect.yMin, _rect.yMax, 0.75f));
                points[4] = new Vector2(Mathf.Lerp(_rect.xMin, _rect.xMax, 0.75f), Mathf.Lerp(_rect.yMin, _rect.yMax, 0.75f));

                int moved = 0;
                for (int i = 0; i < points.Length; i++)
                {
                    Color shown = _before.GetPixel(
                        Mathf.Clamp(Mathf.RoundToInt(points[i].x), 0, _before.width - 1),
                        Mathf.Clamp(Mathf.RoundToInt(points[i].y), 0, _before.height - 1));
                    Color behind = _withoutTooltip.GetPixel(
                        Mathf.Clamp(Mathf.RoundToInt(points[i].x), 0, _withoutTooltip.width - 1),
                        Mathf.Clamp(Mathf.RoundToInt(points[i].y), 0, _withoutTooltip.height - 1));

                    if (Distance(shown, behind) > OverlayPixelProofFloor)
                    {
                        moved++;
                    }
                }

                if (moved == 0)
                {
                    Fail(
                        "none of the " + points.Length + " sample points inside the tooltip changed when it was "
                        + "taken away, so it is not drawn where it says");
                }

                return moved + " of " + points.Length + " sample points inside the tooltip changed when it was "
                    + "taken away (floor " + OverlayPixelProofFloor.ToString("0.###") + ")";
            }

            private byte[] Save(Texture2D texture, string fileName)
            {
                if (texture == null)
                {
                    return null;
                }

                try
                {
                    byte[] png = texture.EncodeToPNG();
                    File.WriteAllBytes(Path.Combine(OutputDirectory, fileName), png);
                    return png;
                }
                catch (Exception error)
                {
                    Fail("could not write " + fileName + ": " + error.Message);
                    return null;
                }
            }

            private void Complete()
            {
                EditorApplication.update -= Tick;
                sRunning = null;

                if (_before != null)
                {
                    Object.DestroyImmediate(_before);
                }

                if (_withoutTooltip != null)
                {
                    Object.DestroyImmediate(_withoutTooltip);
                }

                if (_after != null)
                {
                    Object.DestroyImmediate(_after);
                }

                RestoreScene(_startedIn, _report);

                SessionState.EraseBool(PendingKey);

                WriteReports(
                    _report, _clock.ElapsedMilliseconds, "-tooltip",
                    "`ProjectSurvive/Tools/4. Capture the tooltip before and after an upgrade (Play Mode)`",
                    "the tooltip's resolved numbers, in Play Mode" + (_size.x > 0 ? " (" + _size.x + "x" + _size.y + ")" : string.Empty));
            }
        }

        private enum SceneKind
        {
            Title,
            Camp,
            Battle,
            Result,
        }

        private readonly struct Shot
        {
            public Shot(
                SceneKind scene, string state, string fileName, int width, int height, int bytes,
                long milliseconds, List<UiRegion> regions, List<string> violations)
            {
                Scene = scene;
                State = state;
                FileName = fileName;
                Width = width;
                Height = height;
                Bytes = bytes;
                Milliseconds = milliseconds;
                Regions = regions;
                Violations = violations;
            }

            public readonly SceneKind Scene;
            public readonly string State;
            public readonly string FileName;
            public readonly int Width;
            public readonly int Height;
            public readonly int Bytes;
            public readonly long Milliseconds;
            public readonly List<UiRegion> Regions;
            public readonly List<string> Violations;
        }

        private sealed class CaptureReport
        {
            public readonly List<Shot> Shots = new List<Shot>();
            public readonly List<string> Failures = new List<string>();
            public readonly List<string> Notes = new List<string>();
        }
    }
}

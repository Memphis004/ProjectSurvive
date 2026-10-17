using ProjectSurvive.Game.Battle;
using ProjectSurvive.Game.Presentation.Battle;
using ProjectSurvive.Game.Presentation.Battle.Hud;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Button = UnityEngine.UI.Button;
using Image = UnityEngine.UI.Image;

namespace ProjectSurvive.Game.Editor
{
    /// <summary>
    /// Builds the 2.5D battle scene: the scope that picks the battle, the tilted camera, the
    /// pooled view root, the runner, the HUD and the dev menu.
    /// </summary>
    /// <remarks>
    /// <para>
    /// One file per scene (Stage 10t): the construction and the menu item that builds it live
    /// together, so the scene can be dropped by deleting this file. The helpers it uses are
    /// <see cref="ProjectSetup"/>'s.
    /// </para>
    /// <para>
    /// Everything here is created through the Editor API (AGENTS rule 11) — no
    /// hand-written <c>.unity</c> YAML — so the scene is reproducible from the menu item and
    /// reviewable as code. The serialized references the runtime needs (the view root, the
    /// camera, the HUD and the font) are wired through <c>SerializedProperty</c> rather than
    /// by guessing at file ids.
    /// </para>
    /// <para>
    /// The camera is authored at the Stage 08 decision: see
    /// <c>docs/adr/0018-battle-camera-projection.md</c>. The projection is stored on the
    /// runner's settings as well, so a scene can flip it without editing this file, and the
    /// rig re-applies it at runtime either way.
    /// </para>
    /// </remarks>
    public static class BattleSceneBuilder
    {
        /// <summary>
        /// Margin between a battle HUD panel and the canvas edge it is anchored to, in pixels at the
        /// 1920×1080 reference (Stage 08c).
        /// </summary>
        /// <remarks>
        /// A constant inset rather than a fraction of the canvas: a panel's <i>edge distance</i>
        /// should not grow with the screen, and at 1280×720 the same 24 px is the same 24 px.
        /// </remarks>
        private const float UiMargin = 24f;

        /// <summary>The panel background the battle HUD's four bordered panels share.</summary>
        private static readonly Color PanelBackdrop = new Color(0.07f, 0.08f, 0.11f, 0.92f);

        /// <summary>
        /// Height of the dev menu's single column, in pixels: the heading (44), six 52 px rows (312)
        /// and the 60 px status line, with the margins inside it. The panel is anchored to the bottom
        /// edge, so this is the one dimension that is a number rather than a fraction.
        /// </summary>
        private const float DevMenuHeight = 440f;

        [MenuItem("ProjectSurvive/Stage 08/3. Create Battle scene", priority = 102)]
        public static void CreateFromMenu()
        {
            ProjectSetup.EnsureFolder(ProjectSetup.ScenesFolder);

            Create();
            ProjectSetup.ConfigureBuildSettings();

            ProjectSetup.SaveAndRefresh("ProjectSurvive: Battle scene created and added to build settings.");
        }

        public static void Create()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            (TMP_FontAsset font, _) = ProjectSetup.EnsureFontAssets();

            var scopeObject = new GameObject("Battle");
            BattleSceneScope scope = scopeObject.AddComponent<BattleSceneScope>();
            BattleSceneDirector director = scopeObject.AddComponent<BattleSceneDirector>();

            Camera camera = CreateBattleCamera();
            ProjectSetup.CreateEventSystem();

            var viewRoot = new GameObject("ViewRoot");

            Canvas canvas = ProjectSetup.CreateCanvas(out GameObject canvasObject);
            BattleHudView hud = BuildBattleHud(canvas, canvasObject, font, scope, out BattleMiniBagView miniBag);

            var runnerObject = new GameObject("BattleRunner");
            BattleSimulationRunner runner = runnerObject.AddComponent<BattleSimulationRunner>();

            ProjectSetup.SetObjectReference(runner, "_viewSettings.ViewRoot", viewRoot.transform);
            ProjectSetup.SetObjectReference(runner, "_viewSettings.Camera", camera);
            ProjectSetup.SetObjectReference(runner, "_viewSettings.Hud", hud);
            ProjectSetup.SetObjectReference(runner, "_viewSettings.MiniBag", miniBag);
            ProjectSetup.SetObjectReference(runner, "_viewSettings.Font", font);
            ProjectSetup.SetEnum(runner, "_viewSettings.Projection", (int)BattleProjection.Orthographic);

            ProjectSetup.SetObjectReference(director, "_scope", scope);
            ProjectSetup.SetObjectReference(director, "_runner", runner);

            BuildDevMenu(font, scope, runner);

            EditorSceneManager.SaveScene(scene, ProjectSetup.BattleScenePath);
        }

        /// <summary>
        /// The dev menu (Stage 10c): a panel of six test controls and a status line, authored on its
        /// own canvas above the HUD and <b>inactive</b>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// "ตั้งชื่อ test-only ให้ชัดเจน" is why the heading says <c>DEV MENU (test only)</c> in
        /// capitals and why the panel sits in a corner rather than among the run's own controls: it
        /// is not part of the game, and nothing about it should be mistaken for one.
        /// </para>
        /// <para>
        /// The panel starts inactive because that is the honest authored state — a release build
        /// must never show it, and the view is what turns it on, from
        /// <c>DevCheats.Allowed</c> (a development build or the Editor). The captions are written
        /// from <see cref="DevMenuText"/> rather than typed here, so the words exist in one place and
        /// a test can compare what it reads on screen with what the presenter sends.
        /// </para>
        /// <para>
        /// It is on its own canvas with <c>sortingOrder</c> 200: above the HUD (0), below the scene
        /// loader's fade (3000), so a control is never drawn under the HUD and is never drawn over a
        /// transition.
        /// </para>
        /// <para>
        /// <b>One column at the bottom right (Stage 08c).</b> It used to be a two-column block in the
        /// bottom-left corner, where the status panel now is and where the timeline is one row above
        /// it; the column form is both the brief's own arrangement and the one that cannot reach
        /// either. Every button stretches to the panel's width, so the column reads as a list at
        /// 1280×720 as well as at 1920×1080.
        /// </para>
        /// </remarks>
        private static void BuildDevMenu(TMP_FontAsset font, BattleSceneScope scope, BattleSimulationRunner runner)
        {
            Canvas canvas = ProjectSetup.CreateCanvas(out GameObject canvasObject, sortingOrder: 200, withRaycaster: true);
            var canvasRect = (RectTransform)canvas.transform;

            // Bottom-right band: a quarter of the width at the canvas edge, tall enough for the
            // heading, six 44 px rows and the status line (24 + 416 + 12 = 452 px at the reference).
            RectTransform panel = ProjectSetup.StretchUiRect(
                canvasRect, UiLayoutCheck.DevPanel, new Vector2(0.75f, 0f), new Vector2(1f, 0f),
                new Vector2(16f, UiMargin), new Vector2(-UiMargin, UiMargin + DevMenuHeight));

            var background = panel.gameObject.AddComponent<Image>();
            background.color = new Color(0.08f, 0.10f, 0.13f, 0.88f);
            background.raycastTarget = true;

            TMP_Text heading = ProjectSetup.StretchUiText(
                panel, "DevHeading", new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(12f, -38f), new Vector2(-12f, -8f), ProjectSetup.UiFontSize, font);
            heading.color = new Color(1f, 0.82f, 0.35f, 1f);
            heading.text = DevMenuText.Title;

            Button godMode = CreateDevButton(panel, "DevGodMode", 0, font, out TMP_Text godModeLabel);
            godModeLabel.text = DevMenuText.GodModeOff;

            Button addBia = CreateDevButton(panel, "DevAddBia", 1, font, out TMP_Text addBiaLabel);
            addBiaLabel.text = DevMenuText.AddBia;

            Button spawnGhost = CreateDevButton(panel, "DevSpawnGhost", 2, font, out TMP_Text spawnGhostLabel);
            spawnGhostLabel.text = DevMenuText.SpawnGhost;

            Button killAll = CreateDevButton(panel, "DevKillAll", 3, font, out TMP_Text killAllLabel);
            killAllLabel.text = DevMenuText.KillAll;

            Button winNow = CreateDevButton(panel, "DevWinNow", 4, font, out TMP_Text winNowLabel);
            winNowLabel.text = DevMenuText.WinNow;

            Button loseNow = CreateDevButton(panel, "DevLoseNow", 5, font, out TMP_Text loseNowLabel);
            loseNowLabel.text = DevMenuText.LoseNow;

            TMP_Text status = ProjectSetup.StretchUiText(
                panel, "DevStatus", new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(12f, -416f), new Vector2(-12f, -356f), ProjectSetup.UiFontSize, font);
            status.text = DevMenuText.Disabled;

            DevMenuView view = canvasObject.AddComponent<DevMenuView>();
            ProjectSetup.SetObjectReference(view, "_scope", scope);
            ProjectSetup.SetObjectReference(view, "_runner", runner);
            ProjectSetup.SetObjectReference(view, "_panel", panel.gameObject);
            ProjectSetup.SetObjectReference(view, "_statusLabel", status);
            ProjectSetup.SetObjectReference(view, "_godModeLabel", godModeLabel);
            ProjectSetup.SetObjectReference(view, "_godModeButton", godMode);
            ProjectSetup.SetObjectReference(view, "_addBiaButton", addBia);
            ProjectSetup.SetObjectReference(view, "_spawnGhostButton", spawnGhost);
            ProjectSetup.SetObjectReference(view, "_killAllButton", killAll);
            ProjectSetup.SetObjectReference(view, "_winNowButton", winNow);
            ProjectSetup.SetObjectReference(view, "_loseNowButton", loseNow);

            // Authored hidden: the one build that must never show it is the one that cannot turn it
            // on, and a scene saved with the panel off says so in the scene itself.
            panel.gameObject.SetActive(false);
        }

        /// <summary>
        /// One row of the dev menu's single column: the <paramref name="row"/>-th 44 px control,
        /// stretched to the panel's width between 12 px insets.
        /// </summary>
        /// <remarks>
        /// Rows start under the heading and are 52 px apart (44 px of control and an 8 px gap), so the
        /// panel's own height is the only number that has to be right — <see cref="DevMenuHeight"/>.
        /// </remarks>
        private static Button CreateDevButton(
            RectTransform panel, string name, int row, TMP_FontAsset font, out TMP_Text label)
        {
            float top = 44f + (row * 52f);

            RectTransform rect = ProjectSetup.StretchUiRect(
                panel, name, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(12f, -(top + 44f)), new Vector2(-12f, -top));

            Image background = rect.gameObject.AddComponent<Image>();
            background.color = new Color(0.16f, 0.17f, 0.22f, 0.95f);

            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = background;

            label = ProjectSetup.StretchUiText(
                rect, name + "Label", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                ProjectSetup.UiFontSize, font, TextAlignmentOptions.Center);

            return button;
        }

        /// <summary>
        /// The battle camera: a 50° tilt that follows the player.
        /// </summary>
        /// <remarks>
        /// The camera itself is built by <see cref="BattleCameraProbe.CreateAuthoredCamera"/>, which
        /// poses it with <c>BattleCameraRig</c> — the same rig that re-poses it on the first frame
        /// of a battle. One factory rather than two copies of the arithmetic means the scene an
        /// artist opens is the scene the game runs, and the camera the ADR's experiment measured is
        /// the camera the scene ships.
        /// </remarks>
        private static Camera CreateBattleCamera() => BattleCameraProbe.CreateAuthoredCamera();

        /// <summary>
        /// The battle HUD as Stage 08c lays it out: the mini ย่าม at the top left, the floor's name
        /// and number at the top centre, its stats and the speed control at the top right, the
        /// status panel (figure, HP, MP) at the bottom left, the timeline along the bottom and the
        /// dev menu in the bottom-right column.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>Every panel is anchored to a canvas edge and stretched, not placed at a pixel.</b>
        /// The layout is therefore one arrangement at 1920×1080 and 1280×720 rather than two: the
        /// mini bag is a quarter of the canvas's width by construction (<c>anchorMax.x = 0.25</c>),
        /// the two bottom panels stop at the same fraction of the height, and the timeline takes
        /// the middle half of the width. The vertical split is what keeps the bottom-left status
        /// panel and the top-left mini bag apart at both sizes — the mini bag starts at 45 % of the
        /// height and the status panel ends at 30 %, so the band between them belongs to neither.
        /// </para>
        /// <para>
        /// The mini ย่าม replaced the line of equipped spell names ("เอาข้อความรายการคาถาที่ติดตั้งออก"):
        /// the bag itself now says which pieces are equipped, and the piece that can cast carries a
        /// cooldown plate (Stage 08b's <c>IBattleFrame.Cooldowns</c>).
        /// </para>
        /// <para>
        /// All text is at or above <see cref="ProjectSetup.UiFontSize"/> (22 px), which is the Thai floor the
        /// project has held since Stage 09d; the mini bag's tier numbers are sized from its own cells
        /// with the same floor (see <c>BattleMiniBagView</c>).
        /// </para>
        /// <para>
        /// The mini bag is handed back through <paramref name="miniBag"/> because the HUD view does not
        /// own it: it is wired straight into <c>BattleViewSettings.MiniBag</c>, which is the reference
        /// <c>BattleViewScene</c> builds and plates the bag from. Without that one
        /// <c>SetObjectReference</c> the panel draws its caption and nothing else — a defect the scene's
        /// own PlayMode test caught ("the scene wired no mini bag").
        /// </para>
        /// </remarks>
        private static BattleHudView BuildBattleHud(
            Canvas canvas, GameObject canvasObject, TMP_FontAsset font, BattleSceneScope scope,
            out BattleMiniBagView miniBag)
        {
            var canvasRect = (RectTransform)canvas.transform;

            // ---- top left: the mini ย่าม, at most a quarter of the canvas wide ----
            RectTransform miniBagPanel = ProjectSetup.StretchUiRect(
                canvasRect, UiLayoutCheck.MiniBag, new Vector2(0f, 0.45f), new Vector2(0.25f, 1f),
                new Vector2(UiMargin, UiMargin), new Vector2(-UiMargin, -UiMargin));
            ProjectSetup.AddPanelBackground(miniBagPanel, MiniBagPalette.Backdrop);

            TMP_Text bagCaption = ProjectSetup.CreateUiText(
                miniBagPanel, "BagCaption", new Vector2(10f, -6f), new Vector2(240f, 30f), ProjectSetup.UiFontSize, font);

            // The grid's own area: below the caption, inside the panel's edges. The view measures
            // this rect and sizes its cells to it, so a canvas resize re-fits the bag rather than
            // clipping it.
            RectTransform miniBagContent = ProjectSetup.StretchUiRect(
                miniBagPanel, "MiniBagContent", Vector2.zero, Vector2.one,
                new Vector2(10f, 10f), new Vector2(-10f, -42f));

            miniBag = miniBagPanel.gameObject.AddComponent<BattleMiniBagView>();
            ProjectSetup.SetObjectReference(miniBag, "_panel", miniBagPanel);
            ProjectSetup.SetObjectReference(miniBag, "_content", miniBagContent);
            ProjectSetup.SetObjectReference(miniBag, "_font", font);

            // ---- top centre: the floor's name and its number in the run ----
            RectTransform floorPanel = ProjectSetup.StretchUiRect(
                canvasRect, UiLayoutCheck.FloorPanel, new Vector2(0.35f, 1f), new Vector2(0.65f, 1f),
                new Vector2(0f, -140f), new Vector2(0f, -UiMargin));
            ProjectSetup.AddPanelBackground(floorPanel, PanelBackdrop);

            TMP_Text floorLabel = ProjectSetup.StretchUiText(
                floorPanel, "FloorLabel", new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(12f, -54f), new Vector2(-12f, -10f), 30f, font, TextAlignmentOptions.Center);

            TMP_Text floorIndexLabel = ProjectSetup.StretchUiText(
                floorPanel, "FloorIndexLabel", new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(12f, -96f), new Vector2(-12f, -58f), ProjectSetup.UiFontSize, font, TextAlignmentOptions.Center);

            // ---- top right: time, bia, kills, wave, the boss bar and the speed control ----
            //
            // The panel is 276 reference pixels tall, and that number is measured rather than chosen:
            // three stat rows (a caption over its value), the boss band and the speed control do not
            // fit in the 220 the rows alone needed once the controls were moved in under them, and
            // `ProjectSurvive/Tools/1. Capture UI` reports the collision ("SpeedButton overlaps
            // KillsValue") rather than letting a player find it. Every band is separated by at least
            // 2 reference pixels: two boxes that merely *touch* are not a collision, but at 1280×720
            // the canvas scalar rounds a shared edge to different floats on each side and a tangent
            // pair reads as an overlap.
            RectTransform resourcePanel = ProjectSetup.StretchUiRect(
                canvasRect, UiLayoutCheck.ResourcePanel, new Vector2(0.65f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, -300f), new Vector2(-UiMargin, -UiMargin));
            ProjectSetup.AddPanelBackground(resourcePanel, PanelBackdrop);

            TMP_Text biaCaption = ProjectSetup.CreateUiText(resourcePanel, "BiaCaption", new Vector2(0f, -8f), new Vector2(160f, 30f), ProjectSetup.UiFontSize, font);
            TMP_Text biaValue = ProjectSetup.CreateUiText(resourcePanel, "BiaValue", new Vector2(0f, -40f), new Vector2(200f, 40f), 30f, font);
            TMP_Text timeCaption = ProjectSetup.CreateUiText(resourcePanel, "TimeCaption", new Vector2(220f, -8f), new Vector2(160f, 30f), ProjectSetup.UiFontSize, font);
            TMP_Text timeValue = ProjectSetup.CreateUiText(resourcePanel, "TimeValue", new Vector2(220f, -40f), new Vector2(200f, 40f), 30f, font);
            TMP_Text waveCaption = ProjectSetup.CreateUiText(resourcePanel, "WaveCaption", new Vector2(0f, -86f), new Vector2(160f, 28f), ProjectSetup.UiFontSize, font);
            TMP_Text waveValue = ProjectSetup.CreateUiText(resourcePanel, "WaveValue", new Vector2(220f, -86f), new Vector2(200f, 28f), ProjectSetup.UiFontSize, font);
            TMP_Text killsCaption = ProjectSetup.CreateUiText(resourcePanel, "KillsCaption", new Vector2(0f, -120f), new Vector2(200f, 28f), ProjectSetup.UiFontSize, font);
            TMP_Text killsValue = ProjectSetup.CreateUiText(resourcePanel, "KillsValue", new Vector2(220f, -120f), new Vector2(200f, 28f), ProjectSetup.UiFontSize, font);

            // The boss bar appears only on a boss floor; the presenter tells the view when. It is a
            // band inside the resource panel, never a panel of its own — the layout check holds a
            // control to its surface.
            RectTransform bossRoot = ProjectSetup.StretchUiRect(
                resourcePanel, UiLayoutCheck.BossPanel, new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(0f, 70f), new Vector2(0f, 114f));
            TMP_Text bossCaption = ProjectSetup.CreateUiText(bossRoot, "BossCaption", new Vector2(0f, -4f), new Vector2(200f, 22f), ProjectSetup.UiFontSize, font);
            RectTransform bossBar = ProjectSetup.StretchUiBar(
                bossRoot, "BossBar", new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, -42f), new Vector2(0f, -30f), new Color(0.85f, 0.32f, 0.34f, 1f));

            RectTransform speedObject = ProjectSetup.StretchUiRect(
                resourcePanel, UiLayoutCheck.SpeedButton, new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(0f, 8f), new Vector2(0f, 66f));
            Image speedBackground = speedObject.gameObject.AddComponent<Image>();
            speedBackground.color = new Color(0.16f, 0.17f, 0.22f, 0.9f);
            Button speedButton = speedObject.gameObject.AddComponent<Button>();
            speedButton.targetGraphic = speedBackground;
            TMP_Text speedLabel = ProjectSetup.StretchUiText(
                speedObject, "SpeedLabel", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                28f, font, TextAlignmentOptions.Center);

            // ---- bottom left: the status panel (figure, HP, MP) ----
            RectTransform statusPanel = ProjectSetup.StretchUiRect(
                canvasRect, UiLayoutCheck.StatusPanel, Vector2.zero, new Vector2(0.25f, 0.30f),
                new Vector2(UiMargin, UiMargin), new Vector2(-UiMargin, -UiMargin));
            ProjectSetup.AddPanelBackground(statusPanel, PanelBackdrop);

            // The caption sits over the figure, not beside it: "สถานะ" is 200 px wide and the figure
            // starts 46 px below the panel's top for exactly that reason — as first authored, the two
            // shared their top-right corner and the capture command reported
            // "Portrait overlaps StatusCaption".
            TMP_Text statusCaption = ProjectSetup.CreateUiText(
                statusPanel, "StatusCaption", new Vector2(10f, -8f), new Vector2(200f, 28f), ProjectSetup.UiFontSize, font);

            RectTransform portraitObject = ProjectSetup.CreateUiRect(
                statusPanel, "Portrait", new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(10f, -46f), new Vector2(72f, 72f));
            Image portrait = portraitObject.gameObject.AddComponent<Image>();
            portrait.raycastTarget = false;
            portrait.preserveAspect = true;

            // A caption and its value are two boxes with 2 px between them (116..174 and 176..284):
            // sharing an edge exactly is what makes a tangent pair read as a collision once the canvas
            // scalar has rounded both sides differently.
            TMP_Text hpCaption = ProjectSetup.CreateUiText(statusPanel, "HpCaption", new Vector2(92f, -50f), new Vector2(58f, 26f), ProjectSetup.UiFontSize, font);
            TMP_Text hpValue = ProjectSetup.CreateUiText(statusPanel, "HpValue", new Vector2(152f, -50f), new Vector2(108f, 26f), ProjectSetup.UiFontSize, font);
            RectTransform hpBar = ProjectSetup.StretchUiBar(
                statusPanel, "HpBar", new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(92f, -92f), new Vector2(-10f, -78f), new Color(0.85f, 0.28f, 0.30f, 1f));
            TMP_Text manaCaption = ProjectSetup.CreateUiText(statusPanel, "ManaCaption", new Vector2(92f, -100f), new Vector2(58f, 24f), ProjectSetup.UiFontSize, font);
            TMP_Text manaValue = ProjectSetup.CreateUiText(statusPanel, "ManaValue", new Vector2(152f, -100f), new Vector2(108f, 24f), ProjectSetup.UiFontSize, font);
            RectTransform manaBar = ProjectSetup.StretchUiBar(
                statusPanel, "ManaBar", new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(92f, -140f), new Vector2(-10f, -126f), new Color(0.42f, 0.72f, 0.95f, 1f));

            // ---- bottom centre: the floor's timeline ----
            RectTransform timelinePanel = ProjectSetup.StretchUiRect(
                canvasRect, UiLayoutCheck.TimelinePanel, new Vector2(0.25f, 0f), new Vector2(0.75f, 0.20f),
                new Vector2(16f, UiMargin), new Vector2(-16f, -16f));
            ProjectSetup.AddPanelBackground(timelinePanel, PanelBackdrop);

            TMP_Text timelineCaption = ProjectSetup.StretchUiText(
                timelinePanel, "TimelineCaption", new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(12f, -36f), new Vector2(-12f, -6f), ProjectSetup.UiFontSize, font);

            RectTransform track = ProjectSetup.StretchUiRect(
                timelinePanel, "TimelineTrack", new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(12f, -70f), new Vector2(-12f, -44f));
            Image trackImage = track.gameObject.AddComponent<Image>();
            trackImage.color = new Color(0.12f, 0.13f, 0.17f, 0.85f);

            // Markers stretch across the track and are positioned by their anchors, so the bar
            // needs no per-frame layout maths: the view writes anchorMin/anchorMax and moves on.
            RectTransform timelineMarkers = ProjectSetup.StretchUiRect(
                track, "Markers", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            RectTransform playhead = ProjectSetup.StretchUiRect(
                track, "Playhead", new Vector2(0f, 0f), new Vector2(0f, 1f),
                new Vector2(0f, 0f), new Vector2(6f, 0f));
            Image playheadImage = playhead.gameObject.AddComponent<Image>();
            playheadImage.color = new Color(0.95f, 0.95f, 0.98f, 0.9f);

            TMP_Text statusLabel = ProjectSetup.CreateUiText(
                canvasRect, "StatusLabel", new Vector2(-600f, 120f), new Vector2(1200f, 90f), 56f, font,
                TextAlignmentOptions.Center);
            statusLabel.gameObject.SetActive(false);

            BattleTimelineView timeline = track.gameObject.AddComponent<BattleTimelineView>();
            ProjectSetup.SetObjectReference(timeline, "_track", timelineMarkers);
            ProjectSetup.SetObjectReference(timeline, "_playhead", playhead);

            BattleHudView hud = canvasObject.AddComponent<BattleHudView>();
            ProjectSetup.SetObjectReference(hud, "_scope", scope);
            ProjectSetup.SetObjectReference(hud, "_floorLabel", floorLabel);
            ProjectSetup.SetObjectReference(hud, "_floorIndexLabel", floorIndexLabel);
            ProjectSetup.SetObjectReference(hud, "_hpCaption", hpCaption);
            ProjectSetup.SetObjectReference(hud, "_hpValue", hpValue);
            ProjectSetup.SetObjectReference(hud, "_manaCaption", manaCaption);
            ProjectSetup.SetObjectReference(hud, "_manaValue", manaValue);
            ProjectSetup.SetObjectReference(hud, "_biaCaption", biaCaption);
            ProjectSetup.SetObjectReference(hud, "_biaValue", biaValue);
            ProjectSetup.SetObjectReference(hud, "_timeCaption", timeCaption);
            ProjectSetup.SetObjectReference(hud, "_timeValue", timeValue);
            ProjectSetup.SetObjectReference(hud, "_waveCaption", waveCaption);
            ProjectSetup.SetObjectReference(hud, "_waveValue", waveValue);
            ProjectSetup.SetObjectReference(hud, "_killsCaption", killsCaption);
            ProjectSetup.SetObjectReference(hud, "_killsValue", killsValue);
            ProjectSetup.SetObjectReference(hud, "_bagCaption", bagCaption);
            ProjectSetup.SetObjectReference(hud, "_statusCaption", statusCaption);
            ProjectSetup.SetObjectReference(hud, "_timelineCaption", timelineCaption);
            ProjectSetup.SetObjectReference(hud, "_bossCaption", bossCaption);
            ProjectSetup.SetObjectReference(hud, "_statusLabel", statusLabel);
            ProjectSetup.SetObjectReference(hud, "_speedLabel", speedLabel);
            ProjectSetup.SetObjectReference(hud, "_portrait", portrait);
            ProjectSetup.SetObjectReference(hud, "_hpBar", hpBar);
            ProjectSetup.SetObjectReference(hud, "_manaBar", manaBar);
            ProjectSetup.SetObjectReference(hud, "_bossBar", bossBar);
            ProjectSetup.SetObjectReference(hud, "_bossRoot", bossRoot.gameObject);
            ProjectSetup.SetObjectReference(hud, "_speedButton", speedButton);
            ProjectSetup.SetObjectReference(hud, "_timeline", timeline);

            return hud;
        }
    }
}

using ProjectSurvive.Core.Domain.Combat.Battle;
using ProjectSurvive.Game.Battle;
using ProjectSurvive.Game.Presentation.Battle;
using ProjectSurvive.Game.Presentation.Battle.Hud;
using ProjectSurvive.Game.Presentation.Battle.Pause;
using ProjectSurvive.Game.Presentation.Figure;
using ProjectSurvive.Game.Presentation.GenericCute;
using ProjectSurvive.Game.Presentation.SpineActors;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Button = UnityEngine.UI.Button;
using Image = UnityEngine.UI.Image;
using RectMask2D = UnityEngine.UI.RectMask2D;

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
        /// Gap between the mini ย่าม and the dev menu above it, in reference pixels (Stage 08i).
        /// </summary>
        /// <remarks>
        /// The bag owns the bottom-right corner; the dev menu keeps its size and is anchored above
        /// the bag instead, so opening it can never cover the bag or the fight's own corner. Both
        /// stay in the right-hand column the 08c layout gave the dev menu.
        /// </remarks>
        private const float MiniBagGap = 16f;

        /// <summary>
        /// How dark the pause overlay's dim sheet is (Stage 13c-2b). The arena stays visible behind the
        /// menu — the player has to recognise the floor they are about to leave — while the HUD's small
        /// labels are dark enough not to compete with the card's own text.
        /// </summary>
        private const float PauseDimAlpha = 0.72f;

        /// <summary>
        /// The pause card's background, at the opacity the layout check requires of it: the warning is
        /// long and the arena behind a translucent card would make it unreadable.
        /// </summary>
        /// <remarks>
        /// Lighter than the HUD's panels on purpose. The first capture of the overlay showed a card
        /// that shared the HUD's near-black, drawn on a dim sheet over a dark arena: the heading read
        /// as text floating on the frozen screen rather than as a menu, and no test could say so. The
        /// card now sits a few steps above the dim so its edge is visible at both sizes.
        /// </remarks>
        private static readonly Color PausePanelBackdrop = new Color(0.13f, 0.14f, 0.19f, 0.98f);

        /// <summary>Draw order of the pause overlay's canvas: above the HUD's 0 and the dev menu's 20.</summary>
        private const int PauseCanvasSortingOrder = 200;

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

            // Stage 17b: the ghost-doctor's Spine figure, parented to the view root so it lives and
            // dies with the floor's own visuals. Its profile is the asset the 17a tool created; the
            // figure is begun at floor start by the view, never by the scene.
            MonoBehaviour playerFigure = BuildPlayerFigure(viewRoot);

            Canvas canvas = ProjectSetup.CreateCanvas(out GameObject canvasObject);
            BattleHudView hud = BuildBattleHud(
                canvas, canvasObject, font, scope, out BattleMiniBagView miniBag, out Button pauseButton);

            var runnerObject = new GameObject("BattleRunner");
            BattleSimulationRunner runner = runnerObject.AddComponent<BattleSimulationRunner>();

            // Stage 13c-2b: the pause overlay, on its own canvas above the HUD. Built before the
            // runner's own references are wired because it needs the runner itself, not the reverse.
            BuildPauseOverlay(font, scope, runner, pauseButton);

            ProjectSetup.SetObjectReference(runner, "_viewSettings.ViewRoot", viewRoot.transform);
            ProjectSetup.SetObjectReference(runner, "_viewSettings.Camera", camera);
            ProjectSetup.SetObjectReference(runner, "_viewSettings.Hud", hud);
            ProjectSetup.SetObjectReference(runner, "_viewSettings.MiniBag", miniBag);
            ProjectSetup.SetObjectReference(runner, "_viewSettings.Font", font);
            ProjectSetup.SetObjectReference(runner, "_viewSettings.PlayerFigure", playerFigure);
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

            // Bottom-right column, above the mini ย่าม (Stage 08i). It keeps the size 08c gave it —
            // a quarter of the width, tall enough for the heading, six 44 px rows and the status
            // line — but its bottom edge starts one bag-plus-gap up, so the panel the player always
            // sees owns the corner and the test panel cannot cover it.
            float bottom = UiMargin + BattleMiniBagView.PanelHeight + MiniBagGap;
            RectTransform panel = ProjectSetup.StretchUiRect(
                canvasRect, UiLayoutCheck.DevPanel, new Vector2(0.75f, 0f), new Vector2(1f, 0f),
                new Vector2(16f, bottom), new Vector2(-UiMargin, bottom + DevMenuHeight));

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
        /// The ghost-doctor's figure (Stage 17b): one <see cref="SpinePlayerView"/> under the view
        /// root, with the profile the Spine tool created.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The scene carries the <b>profile</b>, not a skeleton: the skeleton is loaded from
        /// <c>Resources</c> by the view, so the three scenes that will draw the doctor (Battle, Camp,
        /// Title) name one asset each and no path is written down twice (ADR 0037 §2).
        /// </para>
        /// <para>
        /// A missing profile is an <b>error</b> here rather than a silently empty figure: the figure
        /// would fail its own <c>Begin</c> at floor start and the arena would quietly fall back to the
        /// placeholder sprite, which is the state the stage exists to replace.
        /// </para>
        /// </remarks>
        private static MonoBehaviour BuildPlayerFigure(GameObject viewRoot)
        {
            var figureObject = new GameObject("PlayerFigure");
            figureObject.transform.SetParent(viewRoot.transform, worldPositionStays: false);

            // Stage 17f: one settings asset names the backend; the arena carries the component and its
            // profile, so nothing is chosen at floor time. The draw order the figure starts at is the
            // same row rule the sprites use at the arena's centre, and the presenter recomputes it per
            // row crossed while the floor runs.
            PlayerFigureBuildSettings figureSettings = PlayerFigureBuildSettings.Load();
            int sorting = BattleWorldMap.SortingOrder(RenderEntityKind.Player, 0f);

            if (figureSettings.Backend == PlayerFigureBackend.GenericCute)
            {
                GenericCutePlayerView cuteFigure = figureObject.AddComponent<GenericCutePlayerView>();
                var cuteProfile = figureSettings.BattleGenericCuteProfile != null
                    ? figureSettings.BattleGenericCuteProfile
                    : AssetDatabase.LoadAssetAtPath<GenericCutePlayerProfile>(PlayerFigureBackendTool.BattleProfilePath);

                if (cuteProfile is null)
                {
                    Debug.LogError(
                        "[BattleSceneBuilder] " + PlayerFigureBackendTool.BattleProfilePath + " is missing: run " +
                        "ProjectSurvive/Player Figure/1. Create GenericCute profiles. The arena will fall back " +
                        "to the placeholder sprite for the player.");
                }

                ProjectSetup.SetObjectReference(cuteFigure, "profile", cuteProfile);
                ProjectSetup.SetInt(cuteFigure, "sortingOrder", sorting);
                return cuteFigure;
            }

            SpinePlayerView figure = figureObject.AddComponent<SpinePlayerView>();
            var profile = figureSettings.BattleSpineProfile != null
                ? figureSettings.BattleSpineProfile
                : AssetDatabase.LoadAssetAtPath<SpinePlayerProfile>(SpineProfileTool.BattleProfileAssetPath);

            if (profile is null)
            {
                Debug.LogError(
                    "[BattleSceneBuilder] " + SpineProfileTool.BattleProfileAssetPath + " is missing: run " +
                    "ProjectSurvive/Spine/2. Create player profile. The arena will fall back to the " +
                    "placeholder sprite for the player.");
                return figure;
            }

            ProjectSetup.SetObjectReference(figure, "profile", profile);
            ProjectSetup.SetInt(figure, "sortingOrder", sorting);
            return figure;
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
        /// The battle HUD as Stage 08i lays it out: the floor's name and number at the top left, its
        /// stats and the speed control at the top right, the status panel (figure, HP, MP) at the
        /// bottom left, the timeline along the top, the mini ย่าม at the bottom right and the dev
        /// menu above it.
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
        /// <b>The mini ย่าม came back in Stage 08i, at the bottom right.</b> Stage 08g-2 had removed it
        /// to meet a 15 % HUD budget; the player could no longer see which items were ready or on
        /// cooldown. It returns at a measured 288 × 216 (3.00 %), with its cooldown plates read from
        /// <c>IBattleFrame.Cooldowns</c>, and the ceiling moved to 18 % so the fifth panel fits
        /// without shrinking any of the four (see <see cref="BattleHudArea"/>). Its panel is more
        /// transparent than the others so the fight stays visible behind it.
        /// </para>
        /// <para>
        /// All text is at or above <see cref="ProjectSetup.UiFontSize"/> (22 px), which is the Thai floor the
        /// project has held since Stage 09d, and the panels are sized so that the screen area they
        /// cover stays under the 18 % the Stage 08i budget allows (see
        /// <see cref="BattleHudArea"/> and the test that measures the scene).
        /// </para>
        /// </remarks>
        /// <summary>
        /// The pause overlay (Stage 13c-2b): a dim sheet over the whole screen and a card with the
        /// heading, the warning and the two ways out, on a canvas above the HUD's.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Its own canvas rather than a panel inside the HUD, because it has to cover the HUD's panels
        /// rather than sit beside them, and because the dim sheet is what takes the screen out of play:
        /// it is a raycast target, so a press lands on the overlay and nowhere behind it.
        /// </para>
        /// <para>
        /// Authored <b>inactive</b>: the scene is authored between floors, and the overlay appears only
        /// when the presenter's flag says so. The view lives on the canvas object rather than on the
        /// panel, so its <c>Start</c> still runs while the panel is off and its subscriptions are wired
        /// before the first press.
        /// </para>
        /// </remarks>
        private static BattlePauseView BuildPauseOverlay(
            TMP_FontAsset font, BattleSceneScope scope, BattleSimulationRunner runner, Button pauseButton)
        {
            Canvas canvas = ProjectSetup.CreateCanvas(
                out GameObject canvasObject, PauseCanvasSortingOrder, withRaycaster: true);
            var canvasRect = (RectTransform)canvas.transform;

            RectTransform overlay = ProjectSetup.StretchUiRect(
                canvasRect, "PauseOverlay", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            RectTransform dim = ProjectSetup.StretchUiRect(
                overlay, UiLayoutCheck.PauseOverlayDim, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Image dimImage = dim.gameObject.AddComponent<Image>();
            dimImage.color = new Color(0f, 0f, 0f, PauseDimAlpha);

            // The dim sheet swallows the click: while the floor is held still, a press on the arena or
            // on the HUD's speed toggle must not reach the thing underneath the menu the player is
            // reading (the sheet's own raycast flag is the whole mechanism).
            dimImage.raycastTarget = true;

            RectTransform card = ProjectSetup.CreateUiRect(
                overlay, UiLayoutCheck.PausePanel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(820f, 380f));
            ProjectSetup.AddPanelBackground(card, PausePanelBackdrop);

            TMP_Text title = ProjectSetup.CreateUiText(
                card, UiLayoutCheck.PauseTitle, new Vector2(0f, -28f), new Vector2(820f, 60f),
                40f, font, TextAlignmentOptions.Center);

            // The warning is the reason the overlay is not just two buttons: a retreat is a real loss,
            // and the player is told so before pressing กลับศาลา, not after arriving in the camp.
            TMP_Text warning = ProjectSetup.CreateUiText(
                card, UiLayoutCheck.PauseWarning, new Vector2(0f, -104f), new Vector2(760f, 96f),
                24f, font, TextAlignmentOptions.Center);

            Button resumeButton = ProjectSetup.CreateUiButton(
                card, UiLayoutCheck.PauseResume, new Vector2(-150f, 44f), new Vector2(280f, 92f),
                font, out _, new Vector2(0.5f, 0f));
            Button leaveButton = ProjectSetup.CreateUiButton(
                card, UiLayoutCheck.PauseLeave, new Vector2(150f, 44f), new Vector2(280f, 92f),
                font, out _, new Vector2(0.5f, 0f));

            BattlePauseView view = canvasObject.AddComponent<BattlePauseView>();
            ProjectSetup.SetObjectReference(view, "_scope", scope);
            ProjectSetup.SetObjectReference(view, "_runner", runner);
            ProjectSetup.SetObjectReference(view, "_panel", overlay.gameObject);
            ProjectSetup.SetObjectReference(view, "_titleLabel", title);
            ProjectSetup.SetObjectReference(view, "_warningLabel", warning);
            ProjectSetup.SetObjectReference(view, "_openButton", pauseButton);
            ProjectSetup.SetObjectReference(view, "_resumeButton", resumeButton);
            ProjectSetup.SetObjectReference(view, "_leaveButton", leaveButton);

            overlay.gameObject.SetActive(false);
            return view;
        }

        private static BattleHudView BuildBattleHud(
            Canvas canvas, GameObject canvasObject, TMP_FontAsset font, BattleSceneScope scope,
            out BattleMiniBagView miniBag, out Button pauseButton)
        {
            var canvasRect = (RectTransform)canvas.transform;

            // ---- bottom right: the mini ย่าม (Stage 08c, restored by Stage 08i) ----
            //
            // The player has to see which item is ready and which is on cooldown while fighting,
            // so the bag is back — but as a small corner panel, not the quarter-screen block 08g
            // removed. It is anchored to the screen's right and bottom edges with the same 24 px
            // margin as the status panel at the bottom left, and is never wider than 288 px or
            // taller than 216 px at 1080p (the view's named constants). Its background is 0.72
            // opaque instead of 0.92, so the arena still reads through it.
            RectTransform miniBagPanel = ProjectSetup.StretchUiRect(
                canvasRect, UiLayoutCheck.MiniBag, new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-UiMargin - BattleMiniBagView.PanelWidth, UiMargin),
                new Vector2(-UiMargin, UiMargin + BattleMiniBagView.PanelHeight));
            ProjectSetup.AddPanelBackground(miniBagPanel, MiniBagPalette.Backdrop);

            // The caption and the grid share one left inset, and the grid starts one caption line box
            // plus `CaptionGap` below the caption's top (Stage 08d). The numbers are the view's named
            // constants rather than literals here, so the scene, the layout and the layout test read
            // the same definition of "under the caption".
            TMP_Text bagCaption = ProjectSetup.CreateUiText(
                miniBagPanel, "BagCaption",
                new Vector2(BattleMiniBagView.LeftInset, -BattleMiniBagView.CaptionTop),
                new Vector2(
                    BattleMiniBagView.PanelWidth - BattleMiniBagView.LeftInset - BattleMiniBagView.RightInset,
                    BattleMiniBagView.CaptionHeight),
                ProjectSetup.UiFontSize, font);

            // The grid's own area: anchored at the panel's top-left corner, below the caption, and
            // stretched to the panel's right and bottom edges. The view measures this rect and sizes
            // its cells to it, so a canvas resize re-fits the bag rather than clipping it.
            RectTransform miniBagContent = ProjectSetup.StretchUiRect(
                miniBagPanel, "MiniBagContent", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            BattleMiniBagView.AnchorContent(miniBagContent);

            miniBag = miniBagPanel.gameObject.AddComponent<BattleMiniBagView>();
            ProjectSetup.SetObjectReference(miniBag, "_panel", miniBagPanel);
            ProjectSetup.SetObjectReference(miniBag, "_content", miniBagContent);
            ProjectSetup.SetObjectReference(miniBag, "_font", font);

            // ---- top left: the floor's name, its number in the run, and หยุด ----
            //
            // Stage 08g-2 moved this block from the top centre (576 x 152, 4.22 % of the screen) into
            // the corner the mini ย่าม left, at 380 x 72 (1.32 %). The floor's identity is read once a
            // floor, so it is two lines of text and a control rather than a panel in the middle of the
            // fight. The pause control keeps its own band on the right of the block: cramming it into
            // the resource panel would put a third pressable thing beside the speed toggle.
            RectTransform floorPanel = ProjectSetup.StretchUiRect(
                canvasRect, UiLayoutCheck.FloorPanel, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(UiMargin, -96f), new Vector2(UiMargin + 380f, -UiMargin));
            ProjectSetup.AddPanelBackground(floorPanel, PanelBackdrop);

            TMP_Text floorLabel = ProjectSetup.CreateUiText(
                floorPanel, "FloorLabel", new Vector2(12f, -4f), new Vector2(250f, 32f), 30f, font);

            TMP_Text floorIndexLabel = ProjectSetup.CreateUiText(
                floorPanel, "FloorIndexLabel", new Vector2(12f, -40f), new Vector2(250f, 28f),
                ProjectSetup.UiFontSize, font);

            // Stage 13c-2b: the HUD's own หยุด control. Its caption is a localization row the pause
            // view assigns, so the button says what it does the moment the scene binds.
            RectTransform pauseObject = ProjectSetup.CreateUiRect(
                floorPanel, UiLayoutCheck.PauseButton, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-16f, -16f), new Vector2(68f, 40f));
            Image pauseBackground = pauseObject.gameObject.AddComponent<Image>();
            pauseBackground.color = new Color(0.22f, 0.24f, 0.31f, 0.95f);
            pauseButton = pauseObject.gameObject.AddComponent<Button>();
            pauseButton.targetGraphic = pauseBackground;
            ProjectSetup.StretchUiText(
                pauseObject, "Label", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                ProjectSetup.UiFontSize, font, TextAlignmentOptions.Center);

            // ---- top right: time, bia, kills, wave, the boss bar and the speed control ----
            //
            // Stage 08g-2 took this panel from 648 x 276 (8.62 %) to 514 x 216 (5.35 %): the two
            // numbers a player glances at — bia and the clock — keep their 30 px figures over a
            // caption, while the wave and the kill count became one-line rows, which is all they ever
            // needed. The boss band and the speed control still sit in their own bands under the rows,
            // and every band is separated by at least 2 reference pixels: two boxes that merely
            // *touch* are not a collision, but at 1280×720 the canvas scalar rounds a shared edge to
            // different floats on each side and a tangent pair reads as an overlap.
            RectTransform resourcePanel = ProjectSetup.StretchUiRect(
                canvasRect, UiLayoutCheck.ResourcePanel, new Vector2(0.72f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, -312f), new Vector2(-UiMargin, -96f));
            ProjectSetup.AddPanelBackground(resourcePanel, PanelBackdrop);

            TMP_Text biaCaption = ProjectSetup.CreateUiText(resourcePanel, "BiaCaption", new Vector2(12f, -8f), new Vector2(140f, 24f), ProjectSetup.UiFontSize, font);
            TMP_Text biaValue = ProjectSetup.CreateUiText(resourcePanel, "BiaValue", new Vector2(12f, -32f), new Vector2(200f, 34f), 30f, font);
            TMP_Text timeCaption = ProjectSetup.CreateUiText(resourcePanel, "TimeCaption", new Vector2(268f, -8f), new Vector2(140f, 24f), ProjectSetup.UiFontSize, font);
            TMP_Text timeValue = ProjectSetup.CreateUiText(resourcePanel, "TimeValue", new Vector2(268f, -32f), new Vector2(200f, 34f), 30f, font);
            TMP_Text waveCaption = ProjectSetup.CreateUiText(resourcePanel, "WaveCaption", new Vector2(12f, -74f), new Vector2(100f, 28f), ProjectSetup.UiFontSize, font);
            TMP_Text waveValue = ProjectSetup.CreateUiText(resourcePanel, "WaveValue", new Vector2(116f, -74f), new Vector2(140f, 28f), ProjectSetup.UiFontSize, font);
            // The kills caption needs the width of its own words: "ศัตรูที่กำจัด" measures 100.3 px and
            // "Enemies slain" 129.7 px with Sarabun at 22 px, so the 100 px the first cut of this row
            // used wrapped the Thai caption onto a second line the row has no band for (the capture's
            // screenshot showed "ศัตรูที่กำจั / ด 360"). 136 px clears the English by 6 and the Thai by
            // 36; the value starts 4 px after it, which is the rule every other pair in the HUD keeps.
            TMP_Text killsCaption = ProjectSetup.CreateUiText(resourcePanel, "KillsCaption", new Vector2(268f, -74f), new Vector2(136f, 28f), ProjectSetup.UiFontSize, font);
            TMP_Text killsValue = ProjectSetup.CreateUiText(resourcePanel, "KillsValue", new Vector2(408f, -74f), new Vector2(94f, 28f), ProjectSetup.UiFontSize, font);

            // The boss bar appears only on a boss floor; the presenter tells the view when. It is a
            // band inside the resource panel, never a panel of its own — the layout check holds a
            // control to its surface.
            RectTransform bossRoot = ProjectSetup.StretchUiRect(
                resourcePanel, UiLayoutCheck.BossPanel, new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(12f, 62f), new Vector2(-12f, 102f));
            TMP_Text bossCaption = ProjectSetup.CreateUiText(bossRoot, "BossCaption", new Vector2(0f, -4f), new Vector2(200f, 22f), ProjectSetup.UiFontSize, font);
            RectTransform bossBar = ProjectSetup.StretchUiBar(
                bossRoot, "BossBar", new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, -42f), new Vector2(0f, -30f), new Color(0.85f, 0.32f, 0.34f, 1f));

            RectTransform speedObject = ProjectSetup.StretchUiRect(
                resourcePanel, UiLayoutCheck.SpeedButton, new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(12f, 8f), new Vector2(-12f, 58f));
            Image speedBackground = speedObject.gameObject.AddComponent<Image>();
            speedBackground.color = new Color(0.16f, 0.17f, 0.22f, 0.9f);
            Button speedButton = speedObject.gameObject.AddComponent<Button>();
            speedButton.targetGraphic = speedBackground;
            TMP_Text speedLabel = ProjectSetup.StretchUiText(
                speedObject, "SpeedLabel", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                28f, font, TextAlignmentOptions.Center);

            // ---- bottom left: the status frame (figure, HP, MP) ----
            //
            // Stage 08g-2 made this "กรอบเล็กมุมซ้ายล่าง" as the item asks: 380 x 212 (3.89 %) instead
            // of a quarter-wide 432 x 276 panel (5.75 %), with the figure, the two bars and the
            // possession warning of Stage 11c all still inside it. Shrinking it was a placement
            // exercise, not a deletion: the figure gave up 4 px of width and 6 of height, and the
            // rows moved from an absolute grid to the bands the smaller frame has.
            RectTransform statusPanel = ProjectSetup.StretchUiRect(
                canvasRect, UiLayoutCheck.StatusPanel, Vector2.zero, Vector2.zero,
                new Vector2(UiMargin, UiMargin), new Vector2(UiMargin + 380f, UiMargin + 212f));
            ProjectSetup.AddPanelBackground(statusPanel, PanelBackdrop);

            // The caption sits over the figure, not beside it — as first authored, the two shared
            // their top-right corner and the capture command reported "Portrait overlaps
            // StatusCaption". Stage 13 measured every label in the player (`UiTextFitReport`): 22 px
            // Sarabun needs 28.61 px of line box, so a 28 px rectangle is 0.6 px short and the
            // caption's box is 24 px wide by 24 high only because it sits alone on its line.
            TMP_Text statusCaption = ProjectSetup.CreateUiText(
                statusPanel, "StatusCaption", new Vector2(10f, -4f), new Vector2(140f, 24f), ProjectSetup.UiFontSize, font);

            // Stage 17d: the figure is a Spine SkeletonGraphic drawn through the UI material
            // `ProjectSurvive/Spine/3` writes — never a placeholder sprite and never a shader looked
            // up by name. Stage 17e replaces the walker's skeleton with the uploaded halfbody bust
            // (2110101, its own profile) and clips it to its column with a RectMask2D, so a bust
            // taller than the column is cut at the panel's edge instead of drawn over the HP rows.
            RectTransform portraitObject = ProjectSetup.CreateUiRect(
                statusPanel, UiLayoutCheck.Portrait, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(10f, -BattleHudPortrait.TopInset),
                new Vector2(BattleHudPortrait.BoxWidth, BattleHudPortrait.BoxHeight));
            portraitObject.gameObject.AddComponent<RectMask2D>();
            BattleHudPortrait portrait = portraitObject.gameObject.AddComponent<BattleHudPortrait>();
            var portraitProfile = AssetDatabase.LoadAssetAtPath<SpinePortraitProfile>(
                SpinePortraitTool.PortraitProfileAssetPath);
            if (portraitProfile == null)
            {
                Debug.LogError(
                    "[BattleSceneBuilder] " + SpinePortraitTool.PortraitProfileAssetPath + " is missing: run " +
                    "ProjectSurvive/Spine/4. Create portrait profile. The status panel will draw no figure.");
            }

            ProjectSetup.SetObjectReference(portrait, "profile", portraitProfile);
            ProjectSetup.SetObjectReference(
                portrait, "material",
                AssetDatabase.LoadAssetAtPath<Material>(
                    SpineMaterialTool.MaterialFolder + "/SpineGraphic-Normal.mat"));

            // Stage 17f: the panel's figure follows the arena's backend. On the 2D Animation backend the
            // bust is the pack's portrait sprite, drawn as one Image in the same measured box — a Spine
            // halfbody beside a rigged student would be two different doctors on one screen.
            PlayerFigureBuildSettings portraitSettings = PlayerFigureBuildSettings.Load();
            Sprite portraitSprite = AssetDatabase.LoadAssetAtPath<Sprite>(PlayerFigureBackendTool.PortraitSpritePath);
            if (portraitSettings.Backend == PlayerFigureBackend.GenericCute && portraitSprite == null)
            {
                Debug.LogError(
                    "[BattleSceneBuilder] " + PlayerFigureBackendTool.PortraitSpritePath + " is missing: " +
                    "the GenericCute backend would draw no bust in the status panel.");
            }

            ProjectSetup.SetEnum(portrait, "backend", (int)portraitSettings.Backend);
            ProjectSetup.SetObjectReference(portrait, "spritePortrait", portraitSprite);

            // A caption and its value are two boxes with 2 px between them (116..174 and 176..284):
            // sharing an edge exactly is what makes a tangent pair read as a collision once the canvas
            // scalar has rounded both sides differently.
            TMP_Text hpCaption = ProjectSetup.CreateUiText(statusPanel, "HpCaption", new Vector2(100f, -30f), new Vector2(62f, 26f), ProjectSetup.UiFontSize, font);
            TMP_Text hpValue = ProjectSetup.CreateUiText(statusPanel, "HpValue", new Vector2(166f, -30f), new Vector2(130f, 26f), ProjectSetup.UiFontSize, font);
            RectTransform hpBar = ProjectSetup.StretchUiBar(
                statusPanel, "HpBar", new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(100f, -72f), new Vector2(-12f, -60f), new Color(0.85f, 0.28f, 0.30f, 1f));

            // Stage 13: "พลังอาคม" (MP) needs 89 px and the caption's box was 58, so it wrapped onto
            // a second line inside a 24 px box — the one genuinely clipped label the audit found.
            // The caption takes 104 px and the value moves right to clear it.
            TMP_Text manaCaption = ProjectSetup.CreateUiText(statusPanel, "ManaCaption", new Vector2(100f, -78f), new Vector2(104f, 26f), ProjectSetup.UiFontSize, font);
            TMP_Text manaValue = ProjectSetup.CreateUiText(statusPanel, "ManaValue", new Vector2(208f, -78f), new Vector2(130f, 26f), ProjectSetup.UiFontSize, font);
            RectTransform manaBar = ProjectSetup.StretchUiBar(
                statusPanel, "ManaBar", new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(100f, -120f), new Vector2(-12f, -108f), new Color(0.42f, 0.72f, 0.95f, 1f));

            // Stage 11c: the one line the status panel gains — a boss's possession has the movement
            // input reversed, and the player has to be told in words. It is authored empty rather than
            // inactive so its rectangle is laid out and measured like every other label; the presenter
            // writes the localized line into it only while the frame says the controls are reversed
            // (hud.possession). It sits in the band under the two bars, which nothing else uses.
            TMP_Text possessionLabel = ProjectSetup.CreateUiText(
                statusPanel, "PossessionLabel", new Vector2(10f, -142f), new Vector2(358f, 52f), 22f, font);
            possessionLabel.color = new Color(1f, 0.55f, 0.45f, 1f);

            // ---- top: the floor's timeline, as a thin bar (Stage 08g-2) ----
            //
            // It was a 928 x 176 panel along the bottom centre (7.88 % of the screen) with a caption
            // band above a 26 px track. The item asks for "แถบบางด้านบน": one 48 px strip from the
            // floor block's right edge to the screen's, the caption inline on the left and the track
            // filling the rest. The windows, the elite and boss marks and the playhead are the same
            // model drawn smaller, so nothing about what it says changed.
            RectTransform timelinePanel = ProjectSetup.StretchUiRect(
                canvasRect, UiLayoutCheck.TimelinePanel, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(UiMargin + 380f + 24f, -72f), new Vector2(-UiMargin, -UiMargin));
            ProjectSetup.AddPanelBackground(timelinePanel, PanelBackdrop);

            TMP_Text timelineCaption = ProjectSetup.CreateUiText(
                timelinePanel, "TimelineCaption", new Vector2(12f, -12f), new Vector2(132f, 24f),
                ProjectSetup.UiFontSize, font);

            RectTransform track = ProjectSetup.StretchUiRect(
                timelinePanel, "TimelineTrack", new Vector2(0f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(156f, -10f), new Vector2(-12f, 10f));
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
            ProjectSetup.SetObjectReference(hud, "_possessionLabel", possessionLabel);
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

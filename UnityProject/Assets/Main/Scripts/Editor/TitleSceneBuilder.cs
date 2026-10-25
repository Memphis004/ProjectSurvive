using ProjectSurvive.Game.Presentation;
using ProjectSurvive.Game.Presentation.Flow;
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
    /// Builds the title screen: the game's name, the build line under it, and the control that
    /// starts a run (Stage 10d).
    /// </summary>
    /// <remarks>
    /// <para>
    /// One file per scene (Stage 10t): the construction and both menu items that build it live
    /// together — Stage 03's third scene item and Stage 10d's "rebuild with the start control"
    /// item — so the scene can be dropped by deleting this file. The helpers it uses are
    /// <see cref="ProjectSetup"/>'s.
    /// </para>
    /// <para>
    /// The screen is three centred regions with nothing in common: the heading (96 px) at the
    /// canvas' centre +120, the build/config line (28 px) at −40, and a 400×76 control at −160.
    /// Every one of them is authored above the 22 px Thai floor (Stage 09d) and each box is the
    /// line box it holds rather than a 200 px placeholder, so that the layout the capture tool
    /// <b>measures</b> is the layout on screen: the heading's box is 140 px, the build line's 60 px
    /// and the button's 76 px, and the three are disjoint with ≥ 50 px between them.
    /// </para>
    /// <para>
    /// <b>All three are anchored to the canvas' centre</b>, which is what makes the screen centred
    /// at any canvas rather than only at the 1920×1080 reference: the two labels were already
    /// (<see cref="ProjectSetup.CreateTmpLabel"/>), and the control is given the same anchor here. A corner
    /// anchor would put it ~26 px right of the centre on the template's own 960×600 canvas — measured
    /// in the browser before this was fixed. The control is the same size as the summary screen's two
    /// (400×76), which are laid out from their columns' corners. Its caption comes from the
    /// localization table at runtime (AGENTS rule 5): the scene authors an empty label, like every
    /// other label here.
    /// </para>
    /// <para>
    /// เริ่มเล่น is what leaves the screen. The button records the press, the
    /// <see cref="TitlePresenter"/> starts the run through the Application layer, and
    /// <c>TitleSceneDirector</c> — added here, on the scope object — performs the move through the
    /// <c>SceneLoader</c>. Boot still stops at this screen on purpose.
    /// </para>
    /// </remarks>
    public static class TitleSceneBuilder
    {
        /// <summary>Stage 03's title screen.</summary>
        [MenuItem("ProjectSurvive/Stage 03/3. Create Title scene", priority = 102)]
        public static void CreateFromMenu()
        {
            ProjectSetup.EnsureFolder(ProjectSetup.ScenesFolder);

            Create();
            ProjectSetup.ConfigureBuildSettings();

            ProjectSetup.SaveAndRefresh("ProjectSurvive: Title scene created and added to build settings.");
        }

        /// <summary>
        /// Rebuilds the title scene with its start control, and refreshes the build list.
        /// </summary>
        /// <remarks>
        /// Its own menu item even though <see cref="Create"/> is also what Stage 03's scene item calls: a
        /// checkout that predates Stage 10d needs the title screen <i>rebuilt</i> to gain the control,
        /// and running this cannot touch Boot, the spike scene or the build list of the other stages.
        /// The two items produce byte-identical scenes — this one simply says which change made you run
        /// it.
        /// </remarks>
        [MenuItem("ProjectSurvive/Stage 10d/7. Create Title scene (start control)", priority = 106)]
        public static void CreateFromTitleMenu()
        {
            ProjectSetup.EnsureFolder(ProjectSetup.ScenesFolder);

            Create();
            ProjectSetup.ConfigureBuildSettings();

            ProjectSetup.SaveAndRefresh("ProjectSurvive: Title scene created with its start control.");
        }

        public static void Create()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            RootLifetimeScope scope = ProjectSetup.CreateScopeObject();
            TitleSceneDirector director = scope.gameObject.AddComponent<TitleSceneDirector>();

            ProjectSetup.CreateCamera();
            ProjectSetup.CreateEventSystem();
            Canvas canvas = ProjectSetup.CreateCanvas(out GameObject canvasObject);
            var canvasRect = (RectTransform)canvas.transform;

            (TMP_FontAsset tmpFont, _) = ProjectSetup.EnsureFontAssets();

            TMP_Text titleLabel = ProjectSetup.CreateTmpLabel(
                canvas, "TitleLabel", new Vector2(0f, 120f), 96f, tmpFont, new Vector2(1800f, 140f));

            TMP_Text diagnosticsLabel = ProjectSetup.CreateTmpLabel(
                canvas, "DiagnosticsLabel", new Vector2(0f, -40f), 28f, tmpFont, new Vector2(1800f, 60f));

            // Anchored to the canvas' centre rather than laid out from its top-left corner, because the
            // control belongs to content that is centred: the heading and the build line above it are
            // centre-anchored (CreateTmpLabel), and a CanvasScaler's virtual width follows the screen's
            // aspect, so 1920 px of corner arithmetic would put this control ~26 px right of the centre
            // on the template's own 960×600 canvas while the two labels stayed centred. 160 px below the
            // centre is where 760/-662 put it at the 1920×1080 reference: the same place on screen, now
            // the same place on any canvas (measured in the browser: 480 of 960 and 640 of 1280).
            Button startButton = ProjectSetup.CreateUiButton(
                canvasRect, "StartButton", new Vector2(0f, -160f), new Vector2(400f, 76f), tmpFont,
                out TMP_Text startLabel, anchor: new Vector2(0.5f, 0.5f));

            Button settingsButton = ProjectSetup.CreateUiButton(
                canvasRect, "SettingsButton", new Vector2(0f, -260f), new Vector2(300f, 56f), tmpFont,
                out _, anchor: new Vector2(0.5f, 0.5f));

            // Stage 12 settings overlay: one panel with five rows (master, music, effects,
            // language, starting speed) and a close control. It is inactive until the settings
            // button is pressed, so the title screen still shows one obvious action on arrival.
            //
            // 820 px tall, not 600: Stage 13c-2a added the two bus rows, and every row is a
            // 48 px caption above a 50-56 px control. At the 1080 reference height the panel is
            // 76 % of the screen; at 960×600 the canvas is scaled by half, so it is 410 px — still
            // clear of each edge, and the panel is ten px inside itself top and bottom.
            RectTransform panelRect = ProjectSetup.CreateUiRect(
                canvasRect, "SettingsPanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(760f, 820f));
            var panelImage = panelRect.gameObject.AddComponent<Image>();
            // Fully opaque, not 0.96: the panel covers the title label and the start control, and at
            // 0.96 the white title showed through it as a grey ghost (measured in the browser capture,
            // Stage 13c-2a). A modal surface in front of text is not a place for partial alpha.
            panelImage.color = new Color(0.08f, 0.09f, 0.12f, 1f);

            // The labels sit 20 px inside each edge rather than sharing the panel's exact width:
            // a full-width box puts its left edge on the panel's own boundary, where a sub-pixel
            // difference between the two measured rectangles makes a control look as if it escapes
            // the surface. The capture tool reports that as an overlap, so the inset is deliberate.
            TMP_Text settingsTitle = ProjectSetup.CreateUiText(
                panelRect, "SettingsTitle", new Vector2(20f, -28f), new Vector2(720f, 56f), 40f, tmpFont,
                TextAlignmentOptions.Center);

            TMP_Text volumeLabel = ProjectSetup.CreateUiText(
                panelRect, "VolumeLabel", new Vector2(20f, -112f), new Vector2(720f, 48f), 28f, tmpFont,
                TextAlignmentOptions.Center);

            Button volumeDown = ProjectSetup.CreateUiButton(
                panelRect, "VolumeDown", new Vector2(180f, -178f), new Vector2(120f, 50f), tmpFont, out _);
            Button volumeUp = ProjectSetup.CreateUiButton(
                panelRect, "VolumeUp", new Vector2(420f, -178f), new Vector2(120f, 50f), tmpFont, out _);

            // Stage 13c-2a: the two buses, directly under the master. The rows are identical in
            // shape on purpose — the same 120×50 pair at x = 180 and x = 420 — so a player learns
            // one row and the other two look the same.
            TMP_Text musicLabel = ProjectSetup.CreateUiText(
                panelRect, "MusicLabel", new Vector2(20f, -252f), new Vector2(720f, 48f), 28f, tmpFont,
                TextAlignmentOptions.Center);

            Button musicDown = ProjectSetup.CreateUiButton(
                panelRect, "MusicDown", new Vector2(180f, -318f), new Vector2(120f, 50f), tmpFont, out _);
            Button musicUp = ProjectSetup.CreateUiButton(
                panelRect, "MusicUp", new Vector2(420f, -318f), new Vector2(120f, 50f), tmpFont, out _);

            TMP_Text sfxLabel = ProjectSetup.CreateUiText(
                panelRect, "SfxLabel", new Vector2(20f, -388f), new Vector2(720f, 48f), 28f, tmpFont,
                TextAlignmentOptions.Center);

            Button sfxDown = ProjectSetup.CreateUiButton(
                panelRect, "SfxDown", new Vector2(180f, -454f), new Vector2(120f, 50f), tmpFont, out _);
            Button sfxUp = ProjectSetup.CreateUiButton(
                panelRect, "SfxUp", new Vector2(420f, -454f), new Vector2(120f, 50f), tmpFont, out _);

            // -512 and -632, not -522 and -642: the last two rows had their caption box ending 4 px
            // *inside* the button below it (measured by the capture tool at both sizes — the caption
            // at y 380..428 against a button at 328..384 in 1080p). Ten pixels up leaves 8 and 6 px
            // of clear space instead of an overlap; the gap is smaller than the 18 the upper rows use
            // because five rows and the close control have to fit inside the panel's 820 px.
            TMP_Text languageLabel = ProjectSetup.CreateUiText(
                panelRect, "LanguageLabel", new Vector2(20f, -512f), new Vector2(720f, 48f), 28f, tmpFont,
                TextAlignmentOptions.Center);

            Button languageButton = ProjectSetup.CreateUiButton(
                panelRect, "LanguageButton", new Vector2(100f, -566f), new Vector2(560f, 56f), tmpFont, out _);

            TMP_Text speedLabel = ProjectSetup.CreateUiText(
                panelRect, "SpeedLabel", new Vector2(20f, -632f), new Vector2(720f, 48f), 28f, tmpFont,
                TextAlignmentOptions.Center);

            Button speedButton = ProjectSetup.CreateUiButton(
                panelRect, "SpeedButton", new Vector2(100f, -686f), new Vector2(560f, 56f), tmpFont, out _);

            // -756, not -496: with five rows the old value would have put the control's lower half
            // below the panel's own bottom edge. This one sits 14 px inside the 820 px panel and
            // clears the speed row above it by the same margin the rows use.
            Button closeButton = ProjectSetup.CreateUiButton(
                panelRect, "CloseButton", new Vector2(230f, -756f), new Vector2(300f, 50f), tmpFont, out _);

            panelRect.gameObject.SetActive(false);

            SettingsView settingsView = canvasObject.AddComponent<SettingsView>();
            ProjectSetup.SetObjectReference(settingsView, "_scope", scope);
            ProjectSetup.SetObjectReference(settingsView, "_panel", panelRect.gameObject);
            ProjectSetup.SetObjectReference(settingsView, "_titleLabel", settingsTitle);
            ProjectSetup.SetObjectReference(settingsView, "_volumeLabel", volumeLabel);
            ProjectSetup.SetObjectReference(settingsView, "_musicLabel", musicLabel);
            ProjectSetup.SetObjectReference(settingsView, "_sfxLabel", sfxLabel);
            ProjectSetup.SetObjectReference(settingsView, "_languageLabel", languageLabel);
            ProjectSetup.SetObjectReference(settingsView, "_speedLabel", speedLabel);
            ProjectSetup.SetObjectReference(settingsView, "_openButton", settingsButton);
            ProjectSetup.SetObjectReference(settingsView, "_closeButton", closeButton);
            ProjectSetup.SetObjectReference(settingsView, "_volumeDownButton", volumeDown);
            ProjectSetup.SetObjectReference(settingsView, "_volumeUpButton", volumeUp);
            ProjectSetup.SetObjectReference(settingsView, "_musicDownButton", musicDown);
            ProjectSetup.SetObjectReference(settingsView, "_musicUpButton", musicUp);
            ProjectSetup.SetObjectReference(settingsView, "_sfxDownButton", sfxDown);
            ProjectSetup.SetObjectReference(settingsView, "_sfxUpButton", sfxUp);
            ProjectSetup.SetObjectReference(settingsView, "_languageButton", languageButton);
            ProjectSetup.SetObjectReference(settingsView, "_speedButton", speedButton);

            TitleView view = canvasObject.AddComponent<TitleView>();
            ProjectSetup.SetObjectReference(view, "_scope", scope);
            ProjectSetup.SetObjectReference(view, "_titleLabel", titleLabel);
            ProjectSetup.SetObjectReference(view, "_diagnosticsLabel", diagnosticsLabel);
            ProjectSetup.SetObjectReference(view, "_startButton", startButton);
            ProjectSetup.SetObjectReference(view, "_startLabel", startLabel);

            ProjectSetup.SetObjectReference(director, "_scope", scope);

            EditorSceneManager.SaveScene(scene, ProjectSetup.TitleScenePath);
        }
    }
}

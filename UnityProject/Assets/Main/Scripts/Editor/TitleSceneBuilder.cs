using ProjectSurvive.Game.Presentation;
using ProjectSurvive.Game.Presentation.Flow;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Button = UnityEngine.UI.Button;

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

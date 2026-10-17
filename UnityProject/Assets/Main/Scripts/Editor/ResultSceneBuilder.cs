using ProjectSurvive.Game.Presentation;
using ProjectSurvive.Game.ResultScene;
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
    /// Builds the summary scene: the scope that shows a finished run, one centred column of
    /// summary lines, and the two ways out (Restart, back to the title).
    /// </summary>
    /// <remarks>
    /// <para>
    /// One file per scene (Stage 10t): the construction and the menu item that builds it live
    /// together, so the scene can be dropped by deleting this file. The helpers it uses are
    /// <see cref="ProjectSetup"/>'s.
    /// </para>
    /// <para>
    /// Everything is created through the Editor API (AGENTS rule 11) — no hand-written
    /// <c>.unity</c> YAML — and every string on the screen arrives at runtime over
    /// <c>ResultUiState</c> from the localization table (AGENTS rule 5), so this method only
    /// places nine empty widgets and wires the references the view needs.
    /// </para>
    /// <para>
    /// <b>One centred column, measured rather than guessed.</b> The <c>Summary</c> rect is
    /// centred on the canvas (anchor and pivot both 0.5) and 1200×1000 at the 1920×1080
    /// reference, so nothing here is positioned in absolute screen pixels: at 1280×720 the
    /// column keeps the same proportions and the same gaps between lines. The rows are 60 px
    /// apart with 44 px line boxes and the headings are 96 px, all above the 22 px Thai floor
    /// (Stage 09d) — a 32 px line box holds a Thai line with its upper and lower marks inside
    /// the box instead of clipping them.
    /// </para>
    /// <para>
    /// The two buttons are 400×76 and centred in the column: the same touch target the camp
    /// author their panel controls at, so the summary screen cannot be the one screen whose
    /// buttons are too small to press on a phone.
    /// </para>
    /// </remarks>
    public static class ResultSceneBuilder
    {
        /// <summary>The summary screen's own line size: comfortably above the 22 px Thai floor.</summary>
        private const float ResultLineFontSize = 32f;

        [MenuItem("ProjectSurvive/Stage 10/6. Create Result scene", priority = 105)]
        public static void CreateFromMenu()
        {
            ProjectSetup.EnsureFolder(ProjectSetup.ScenesFolder);

            Create();
            ProjectSetup.ConfigureBuildSettings();
            ProjectSetup.RefreshUrlSceneAllowList();

            ProjectSetup.SaveAndRefresh("ProjectSurvive: Result scene created and added to build settings.");
        }

        public static void Create()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            (TMP_FontAsset font, _) = ProjectSetup.EnsureFontAssets();

            var scopeObject = new GameObject("Result");
            ResultSceneScope scope = scopeObject.AddComponent<ResultSceneScope>();
            ResultSceneDirector director = scopeObject.AddComponent<ResultSceneDirector>();

            ProjectSetup.CreateCamera();
            ProjectSetup.CreateEventSystem();

            Canvas canvas = ProjectSetup.CreateCanvas(out GameObject canvasObject);
            var canvasRect = (RectTransform)canvas.transform;

            RectTransform column = ProjectSetup.CreateUiRect(
                canvasRect, "Summary", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(1200f, 1000f));

            // The column carries a background of its own, at the camp's panel alpha (0.92 — the value
            // `ProjectSurvive/Tools/1. Capture UI` measures on the camp's Board, ChestPanel and Altar,
            // and the one `UiLayoutCheck.OpaqueFloor` names for a panel the reader must not see through).
            // It is not the จัดย่าม screen's 0.62 full-screen overlay: that one is translucent so ศาลาพัก
            // stays readable behind it, and over this scene's flat clear colour 0.62 is invisible
            // (0.06/0.08/0.10 at 62 % over 13/15/20 is a 0.5 %-per-channel shift). A summary is a column
            // of text, so it gets the panel treatment the other screens' panels get.
            var columnBackground = column.gameObject.AddComponent<Image>();
            columnBackground.color = new Color(0.07f, 0.09f, 0.12f, 0.92f);

            // The buttons answer their own clicks, so the column behind them must not swallow one: the
            // camp's own non-blocking panels (Instructions, Board, ChestPanel) are authored the same way.
            columnBackground.raycastTarget = false;

            TMP_Text headline = ProjectSetup.CreateUiText(
                column, "Headline", new Vector2(0f, -40f), new Vector2(1200f, 140f), 96f, font,
                TextAlignmentOptions.Center);

            TMP_Text floor = ProjectSetup.CreateUiText(
                column, "FloorLine", new Vector2(0f, -200f), new Vector2(1200f, 44f),
                ResultLineFontSize, font, TextAlignmentOptions.Center);

            TMP_Text kills = ProjectSetup.CreateUiText(
                column, "KillsLine", new Vector2(0f, -260f), new Vector2(1200f, 44f),
                ResultLineFontSize, font, TextAlignmentOptions.Center);

            TMP_Text bia = ProjectSetup.CreateUiText(
                column, "BiaLine", new Vector2(0f, -320f), new Vector2(1200f, 44f),
                ResultLineFontSize, font, TextAlignmentOptions.Center);

            TMP_Text hp = ProjectSetup.CreateUiText(
                column, "HpLine", new Vector2(0f, -380f), new Vector2(1200f, 44f),
                ResultLineFontSize, font, TextAlignmentOptions.Center);

            Button restart = ProjectSetup.CreateUiButton(
                column, "RestartButton", new Vector2(400f, -560f), new Vector2(400f, 76f), font,
                out TMP_Text restartLabel);

            Button title = ProjectSetup.CreateUiButton(
                column, "TitleButton", new Vector2(400f, -660f), new Vector2(400f, 76f), font,
                out TMP_Text titleLabel);

            ResultView view = canvasObject.AddComponent<ResultView>();
            ProjectSetup.SetObjectReference(view, "_scope", scope);
            ProjectSetup.SetObjectReference(view, "_headlineLabel", headline);
            ProjectSetup.SetObjectReference(view, "_floorLabel", floor);
            ProjectSetup.SetObjectReference(view, "_killsLabel", kills);
            ProjectSetup.SetObjectReference(view, "_biaLabel", bia);
            ProjectSetup.SetObjectReference(view, "_hpLabel", hp);
            ProjectSetup.SetObjectReference(view, "_restartButton", restart);
            ProjectSetup.SetObjectReference(view, "_titleButton", title);
            ProjectSetup.SetObjectReference(view, "_restartLabel", restartLabel);
            ProjectSetup.SetObjectReference(view, "_titleLabel", titleLabel);

            ProjectSetup.SetObjectReference(director, "_scope", scope);

            EditorSceneManager.SaveScene(scene, ProjectSetup.ResultScenePath);
        }
    }
}

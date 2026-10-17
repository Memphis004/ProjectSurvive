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
    /// Builds the summary scene: the scope that shows a finished run, one centred column of lines, and
    /// the two ways out (a new run, or back to the title).
    /// </summary>
    /// <remarks>
    /// <para>
    /// One file per scene (Stage 10t): the construction and the menu item that builds it live together,
    /// so the scene can be dropped by deleting this file. The helpers it uses are
    /// <see cref="ProjectSetup"/>'s.
    /// </para>
    /// <para>
    /// Everything is created through the Editor API (AGENTS rule 11) — no hand-written <c>.unity</c>
    /// YAML — and every string arrives at runtime over <see cref="ResultUiState"/> from the
    /// localization table (AGENTS rule 5), so this method only places empty widgets and wires the
    /// references the view needs.
    /// </para>
    /// </remarks>
    public static class ResultSceneBuilder
    {
        /// <summary>The summary screen's own line size: comfortably above the 22 px Thai floor.</summary>
        private const float ResultLineFontSize = 32f;

        [MenuItem("ProjectSurvive/Scenes/5. Create Result scene", priority = 105)]
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
                Vector2.zero, new Vector2(1200f, 900f));

            // A panel the reader must not see through, at the same alpha the game repo measured (0.92).
            var columnBackground = column.gameObject.AddComponent<Image>();
            columnBackground.color = new Color(0.07f, 0.09f, 0.12f, 0.92f);
            columnBackground.raycastTarget = false;

            TMP_Text headline = ProjectSetup.CreateUiText(
                column, "Headline", new Vector2(0f, -40f), new Vector2(1200f, 140f), 96f, font,
                TextAlignmentOptions.Center);

            TMP_Text summary = ProjectSetup.CreateUiText(
                column, "SummaryLine", new Vector2(0f, -200f), new Vector2(1200f, 44f),
                ResultLineFontSize, font, TextAlignmentOptions.Center);

            TMP_Text seed = ProjectSetup.CreateUiText(
                column, "SeedLine", new Vector2(0f, -260f), new Vector2(1200f, 44f),
                ResultLineFontSize, font, TextAlignmentOptions.Center);

            Button restart = ProjectSetup.CreateUiButton(
                column, "RestartButton", new Vector2(400f, -420f), new Vector2(400f, 76f), font,
                out TMP_Text restartLabel);

            Button title = ProjectSetup.CreateUiButton(
                column, "TitleButton", new Vector2(400f, -520f), new Vector2(400f, 76f), font,
                out TMP_Text titleLabel);

            ResultView view = canvasObject.AddComponent<ResultView>();
            ProjectSetup.SetObjectReference(view, "_scope", scope);
            ProjectSetup.SetObjectReference(view, "_headlineLabel", headline);
            ProjectSetup.SetObjectReference(view, "_summaryLabel", summary);
            ProjectSetup.SetObjectReference(view, "_seedLabel", seed);
            ProjectSetup.SetObjectReference(view, "_restartButton", restart);
            ProjectSetup.SetObjectReference(view, "_titleButton", title);
            ProjectSetup.SetObjectReference(view, "_restartLabel", restartLabel);
            ProjectSetup.SetObjectReference(view, "_titleLabel", titleLabel);

            ProjectSetup.SetObjectReference(director, "_scope", scope);

            EditorSceneManager.SaveScene(scene, ProjectSetup.ResultScenePath);
        }
    }
}

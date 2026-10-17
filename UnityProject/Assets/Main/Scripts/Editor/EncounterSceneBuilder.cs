using ProjectSurvive.Game.Presentation.Encounter;
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
    /// Builds the Encounter scene: the placeholder unit of play, with a heading, a progress line and
    /// the two ways it can end.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A game replaces this scene with its arena: the same scope/director/view wiring, plus its own
    /// camera and simulation. What the template guarantees is the contract around it — the run is the
    /// same run the Hub handed over, and the screen's two outcomes are the run's own
    /// <c>EndEncounter</c>.
    /// </para>
    /// <para>
    /// Built through the Editor API (AGENTS rule 11); strings arrive at runtime from the localization
    /// table (AGENTS rule 5).
    /// </para>
    /// </remarks>
    public static class EncounterSceneBuilder
    {
        private const float LineFontSize = 32f;

        [MenuItem("ProjectSurvive/Scenes/4. Create Encounter scene", priority = 104)]
        public static void CreateFromMenu()
        {
            ProjectSetup.EnsureFolder(ProjectSetup.ScenesFolder);

            Create();
            ProjectSetup.ConfigureBuildSettings();
            ProjectSetup.RefreshUrlSceneAllowList();

            ProjectSetup.SaveAndRefresh("ProjectSurvive: Encounter scene created and added to build settings.");
        }

        public static void Create()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            (TMP_FontAsset font, _) = ProjectSetup.EnsureFontAssets();

            var scopeObject = new GameObject("Encounter");
            EncounterSceneScope scope = scopeObject.AddComponent<EncounterSceneScope>();
            EncounterSceneDirector director = scopeObject.AddComponent<EncounterSceneDirector>();

            ProjectSetup.CreateCamera();
            ProjectSetup.CreateEventSystem();

            Canvas canvas = ProjectSetup.CreateCanvas(out GameObject canvasObject);
            var canvasRect = (RectTransform)canvas.transform;

            RectTransform column = ProjectSetup.CreateUiRect(
                canvasRect, "Panel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(1200f, 900f));

            var background = column.gameObject.AddComponent<Image>();
            background.color = new Color(0.07f, 0.09f, 0.12f, 0.92f);
            background.raycastTarget = false;

            TMP_Text title = ProjectSetup.CreateUiText(
                column, "Title", new Vector2(0f, -40f), new Vector2(1200f, 140f), 96f, font,
                TextAlignmentOptions.Center);

            TMP_Text progress = ProjectSetup.CreateUiText(
                column, "ProgressLine", new Vector2(0f, -200f), new Vector2(1200f, 44f),
                LineFontSize, font, TextAlignmentOptions.Center);

            Button win = ProjectSetup.CreateUiButton(
                column, "WinButton", new Vector2(400f, -420f), new Vector2(400f, 76f), font,
                out TMP_Text winLabel);

            Button lose = ProjectSetup.CreateUiButton(
                column, "LoseButton", new Vector2(400f, -520f), new Vector2(400f, 76f), font,
                out TMP_Text loseLabel);

            EncounterView view = canvasObject.AddComponent<EncounterView>();
            ProjectSetup.SetObjectReference(view, "_scope", scope);
            ProjectSetup.SetObjectReference(view, "_titleLabel", title);
            ProjectSetup.SetObjectReference(view, "_progressLabel", progress);
            ProjectSetup.SetObjectReference(view, "_winButton", win);
            ProjectSetup.SetObjectReference(view, "_loseButton", lose);
            ProjectSetup.SetObjectReference(view, "_winLabel", winLabel);
            ProjectSetup.SetObjectReference(view, "_loseLabel", loseLabel);

            ProjectSetup.SetObjectReference(director, "_scope", scope);

            EditorSceneManager.SaveScene(scene, ProjectSetup.EncounterScenePath);
        }
    }
}

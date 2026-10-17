using ProjectSurvive.Game.Presentation.Hub;
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
    /// Builds the Hub scene: the scope that prepares a run, two lines, and the two ways out (depart, or
    /// end the run).
    /// </summary>
    /// <remarks>
    /// One file per scene, built through the Editor API (AGENTS rule 11). Every string arrives at
    /// runtime over <see cref="HubUiState"/> from the localization table (AGENTS rule 5), so this only
    /// places empty widgets and wires the references the view needs. The layout is a centred column at
    /// the 1920×1080 reference, like the summary screen, so both sizes scale from the same proportions.
    /// </remarks>
    public static class HubSceneBuilder
    {
        private const float LineFontSize = 32f;

        [MenuItem("ProjectSurvive/Scenes/3. Create Hub scene", priority = 103)]
        public static void CreateFromMenu()
        {
            ProjectSetup.EnsureFolder(ProjectSetup.ScenesFolder);

            Create();
            ProjectSetup.ConfigureBuildSettings();
            ProjectSetup.RefreshUrlSceneAllowList();

            ProjectSetup.SaveAndRefresh("ProjectSurvive: Hub scene created and added to build settings.");
        }

        public static void Create()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            (TMP_FontAsset font, _) = ProjectSetup.EnsureFontAssets();

            var scopeObject = new GameObject("Hub");
            HubSceneScope scope = scopeObject.AddComponent<HubSceneScope>();
            HubSceneDirector director = scopeObject.AddComponent<HubSceneDirector>();

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

            TMP_Text instructions = ProjectSetup.CreateUiText(
                column, "Instructions", new Vector2(0f, -200f), new Vector2(1200f, 44f),
                LineFontSize, font, TextAlignmentOptions.Center);

            TMP_Text encounters = ProjectSetup.CreateUiText(
                column, "EncountersLine", new Vector2(0f, -260f), new Vector2(1200f, 44f),
                LineFontSize, font, TextAlignmentOptions.Center);

            Button depart = ProjectSetup.CreateUiButton(
                column, "DepartButton", new Vector2(400f, -420f), new Vector2(400f, 76f), font,
                out TMP_Text departLabel);

            Button finish = ProjectSetup.CreateUiButton(
                column, "FinishButton", new Vector2(400f, -520f), new Vector2(400f, 76f), font,
                out TMP_Text finishLabel);

            HubView view = canvasObject.AddComponent<HubView>();
            ProjectSetup.SetObjectReference(view, "_scope", scope);
            ProjectSetup.SetObjectReference(view, "_titleLabel", title);
            ProjectSetup.SetObjectReference(view, "_instructionsLabel", instructions);
            ProjectSetup.SetObjectReference(view, "_encountersLabel", encounters);
            ProjectSetup.SetObjectReference(view, "_departButton", depart);
            ProjectSetup.SetObjectReference(view, "_finishButton", finish);
            ProjectSetup.SetObjectReference(view, "_departLabel", departLabel);
            ProjectSetup.SetObjectReference(view, "_finishLabel", finishLabel);

            ProjectSetup.SetObjectReference(director, "_scope", scope);

            EditorSceneManager.SaveScene(scene, ProjectSetup.HubScenePath);
        }
    }
}

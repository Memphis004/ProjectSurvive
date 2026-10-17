using ProjectSurvive.Game.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectSurvive.Game.Editor
{
    /// <summary>
    /// Builds the Boot scene: the app-scope root and the boot flow that hands over to the title.
    /// </summary>
    /// <remarks>
    /// One file per scene (Stage 10t): this scene's construction and the menu item that
    /// builds it live together, so the scene can be dropped by deleting this file. The
    /// helpers it uses are <see cref="ProjectSetup"/>'s.
    /// </remarks>
    public static class BootSceneBuilder
    {
        [MenuItem("ProjectSurvive/Stage 03/2. Create Boot scene", priority = 101)]
        public static void CreateFromMenu()
        {
            ProjectSetup.EnsureFolder(ProjectSetup.ScenesFolder);

            Create();
            ProjectSetup.ConfigureBuildSettings();

            ProjectSetup.SaveAndRefresh("ProjectSurvive: Boot scene created and added to build settings.");
        }

        public static void Create()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var root = new GameObject("Boot");
            RootLifetimeScope scope = root.AddComponent<RootLifetimeScope>();
            SceneBoot boot = root.AddComponent<SceneBoot>();

            ProjectSetup.SetObjectReference(boot, "_scope", scope);
            ProjectSetup.SetBool(boot, "_continueToTitleScene", true);
            ProjectSetup.SetString(boot, "_titleSceneName", "Title");

            // `?scene=Battle` is how a browser test reaches the battle without playing through
            // the camp, and `?scene=Camp` is how it reaches the camp. It is an allow-list, not a
            // router: only the scenes named here can be requested from a URL.
            ProjectSetup.SetStringArray(boot, "_urlSceneOverrides", ProjectSetup.UrlSceneAllowList);

            EditorSceneManager.SaveScene(scene, ProjectSetup.BootScenePath);
        }
    }
}

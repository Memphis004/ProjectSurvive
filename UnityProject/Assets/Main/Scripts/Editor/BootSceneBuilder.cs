using ProjectSurvive.Game.Presentation;
using ProjectSurvive.Game.Presentation.Audio;
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
            // The clips are imported before the scene references them, and with the Web build's
            // load types (Stage 13c-1).
            ProjectSetup.ConfigureAudio();

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

            // Stage 13c-1: the game's one audio device. It is a child of the app root, which Boot
            // moves into DontDestroyOnLoad, so music survives a scene change and the game has
            // exactly one AudioListener for the whole session.
            //
            // The listener is **authored here** rather than added by AudioDirector at runtime:
            // a listener the component added from Awake (and from Start) never appeared, so the
            // game ran silent and Unity logged "There are no audio listeners in the scene" once
            // per frame for every playing source — ~1,750 lines per camp/battle visit, which the
            // PlayMode runner keeps in memory. In the scene data it exists before the first frame
            // and travels with the object into DontDestroyOnLoad. Measured after this change:
            // zero warnings over a full 20-visit loop.
            var audioObject = new GameObject("Audio");
            audioObject.transform.SetParent(root.transform, worldPositionStays: false);
            audioObject.AddComponent<AudioListener>();
            AudioDirector audio = audioObject.AddComponent<AudioDirector>();
            ProjectSetup.SetObjectReference(audio, "_scope", scope);
            ProjectSetup.SetObjectArray(audio, "_clips", ProjectSetup.LoadAudioClips());

            EditorSceneManager.SaveScene(scene, ProjectSetup.BootScenePath);
        }
    }
}

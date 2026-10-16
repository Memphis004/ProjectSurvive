using System.Collections;
using NUnit.Framework;
using ProjectSurvive.Game.Battle;
using ProjectSurvive.Game.Camp;
using ProjectSurvive.Game.Presentation;
using ProjectSurvive.Game.Presentation.Battle.Hud;
using ProjectSurvive.Game.Presentation.Flow;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;

namespace ProjectSurvive.Game.Tests.PlayMode
{
    /// <summary>
    /// Scene loading for the PlayMode fixtures, and the one thing Stage 10a added that every
    /// fixture has to know about: the App scope persists.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Since Stage 10a the Boot scene's <see cref="RootLifetimeScope"/> moves itself into
    /// <c>DontDestroyOnLoad</c> and becomes the parent of every scene scope loaded afterwards, which
    /// is what makes a run outlive a scene. That persistence is a <b>global</b> setting
    /// (<c>LifetimeScope.EnqueueParent</c>), so a fixture that loads Boot leaves it on for whatever
    /// runs next — the next test would quietly inherit a run, a wallet and a shop from a test that
    /// has already finished. <see cref="ResetShell"/> is the counterpart: it stops the persistence
    /// and destroys the App scope, so every fixture drives a shell it owns.
    /// </para>
    /// <para>
    /// The other half is the load itself. A scene scope is built in <c>Awake</c>, so nothing may
    /// touch the scene's container on the frame the load returns; every helper here yields a second
    /// frame before the caller reads anything.
    /// </para>
    /// </remarks>
    public static class SceneFlowHarness
    {
        public const string BootSceneName = "Boot";
        public const string TitleSceneName = "Title";
        public const string CampSceneName = "Camp";
        public const string BattleSceneName = "Battle";
        public const string ResultSceneName = "Result";

        /// <summary>How long a scene handover may take before a test calls it stuck.</summary>
        public const float SceneBudgetSeconds = 45f;

        /// <summary>
        /// Releases the App scope's global parent override and destroys every
        /// <see cref="RootLifetimeScope"/> in the project, in whatever scene it is living in.
        /// </summary>
        public static void ResetShell()
        {
            RootLifetimeScope[] scopes = Object.FindObjectsByType<RootLifetimeScope>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            for (int i = 0; i < scopes.Length; i++)
            {
                if (scopes[i] is null)
                {
                    continue;
                }

                // Order matters: the override has to come off before the scope goes away, or a
                // scene scope loaded a frame later would find a destroyed parent.
                scopes[i].StopPersisting();
                Object.Destroy(scopes[i].gameObject);
            }
        }

        /// <summary>
        /// Waits for a scene the game itself chose to load: the battle leaves for the camp or the
        /// summary screen on its own, so a test has to ride that transition rather than ask for one
        /// (a second ask is refused while the first is in flight).
        /// </summary>
        public static IEnumerator WaitForSceneAndIdle(string sceneName)
        {
            yield return WaitForScene(sceneName);
            yield return WaitForIdle(Loader());
        }

        /// <summary>Loads one scene on a shell this test owns.</summary>
        public static IEnumerator Load(string sceneName)
        {
            ResetShell();
            yield return null;

            SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
            yield return null;
            yield return null;
        }

        /// <summary>Loads the camp and starts it, failing the test when the camp refuses.</summary>
        public static IEnumerator LoadCamp()
        {
            yield return Load(CampSceneName);

            var director = Find<CampSceneDirector>();
            Assert.That(director, Is.Not.Null, "the Camp scene has no CampSceneDirector");
            Assert.That(director.TryStart(), Is.True, "the camp did not start: " + director.StartError);

            yield return null;
        }

        /// <summary>
        /// Boots the shell the way the game does, and stops at the title: one App scope, which is
        /// what every scene loaded afterwards becomes a child of.
        /// </summary>
        /// <remarks>
        /// A fixture that wants a run to survive a scene change has to come through here. Loading
        /// the camp directly makes the camp its own root — right for the fixtures that photograph
        /// and drive one scene, and wrong for anything about the loop, since a second scene would
        /// then build a second App scope and a second run.
        /// </remarks>
        public static IEnumerator BootShell()
        {
            ResetShell();
            yield return null;

            SceneManager.LoadScene(BootSceneName, LoadSceneMode.Single);
            yield return WaitForScene(TitleSceneName);
        }

        /// <summary>Boots the shell and walks into the camp through the real scene loader.</summary>
        public static IEnumerator EnterCamp()
        {
            yield return BootShell();

            SceneLoader loader = Loader();
            Assert.That(loader.TryGo(Core.Domain.Run.SceneId.Camp), Is.True, "the camp was refused: " + loader.LastError);
            yield return WaitForScene(CampSceneName);
            yield return WaitForIdle(loader);

            var director = Find<CampSceneDirector>();
            Assert.That(director, Is.Not.Null, "the Camp scene has no CampSceneDirector");
            Assert.That(director.TryStart(), Is.True, "the camp did not start: " + director.StartError);

            yield return null;
        }

        /// <summary>
        /// Waits for the loader to be idle again. The scene is active before the fade has lifted, so
        /// a second transition asked for on that frame is refused with <c>transition.in_progress</c>
        /// — correctly, and this is what stops a loop from racing its own fade.
        /// </summary>
        public static IEnumerator WaitForIdle(SceneLoader loader)
        {
            yield return WaitUntil(() => !loader.IsTransitioning, "the scene loader never went idle");
            yield return null;
        }

        /// <summary>Waits until <paramref name="sceneName"/> is the active scene.</summary>
        public static IEnumerator WaitForScene(string sceneName)
        {
            float deadline = Time.realtimeSinceStartup + SceneBudgetSeconds;
            while (SceneManager.GetActiveScene().name != sceneName && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(
                SceneManager.GetActiveScene().name, Is.EqualTo(sceneName),
                "the scene never became '" + sceneName + "'");

            // Let the new scene's own Start run before the caller reads it.
            yield return null;
            yield return null;
        }

        /// <summary>Waits for a condition, or fails the test with <paramref name="because"/>.</summary>
        public static IEnumerator WaitUntil(System.Func<bool> condition, string because)
        {
            float deadline = Time.realtimeSinceStartup + SceneBudgetSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(condition(), Is.True, because);
        }

        /// <summary>The first loaded object of a type, active or not.</summary>
        public static T Find<T>() where T : Object =>
            Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);

        /// <summary>How many loaded objects of a type there are, active or not.</summary>
        public static int Count<T>() where T : Object =>
            Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;

        /// <summary>
        /// The scene loader of whichever scope is on screen — the App scope while it persists, or
        /// the scene's own scope when the scene is a root. Both resolve it.
        /// </summary>
        public static SceneLoader Loader()
        {
            var scope = Find<RootLifetimeScope>();
            Assert.That(scope, Is.Not.Null, "no RootLifetimeScope is loaded, so there is no scene loader");
            Assert.That(scope.Container, Is.Not.Null, "the loaded scope was never built");
            return scope.Container.Resolve<SceneLoader>();
        }

        /// <summary>
        /// The battle scene's HUD state, from the battle scene's <b>own</b> scope.
        /// </summary>
        /// <remarks>
        /// Deliberately not resolved from the App scope: the HUD belongs to the scene that shows it,
        /// and while the App scope persists there are two loaded scopes at once. That difference is
        /// the lifetime this stage is about, so the test reads the HUD from the scope that owns it
        /// rather than from whichever one happens to be found first.
        /// </remarks>
        public static BattleHudState BattleHud()
        {
            var scope = Find<BattleSceneScope>();
            Assert.That(scope, Is.Not.Null, "the Battle scene has no BattleSceneScope");
            Assert.That(scope.Container, Is.Not.Null, "the Battle scene's scope was never built");
            return scope.Container.Resolve<BattleHudState>();
        }

        /// <summary>
        /// The run scope owner of whichever scope is on screen: it lives in the App scope, so a
        /// scene scope reaches it through the parent chain, and a standalone scene has one of its
        /// own.
        /// </summary>
        public static RunScopeOwner Owner()
        {
            var scope = Find<RootLifetimeScope>();
            Assert.That(scope, Is.Not.Null, "no RootLifetimeScope is loaded, so there is no run owner");
            Assert.That(scope.Container, Is.Not.Null, "the loaded scope was never built");
            return scope.Container.Resolve<RunScopeOwner>();
        }

        /// <summary>
        /// Managed bytes held right now, with the collectors run first so the number is the live
        /// set rather than whatever has not been collected yet.
        /// </summary>
        public static long ManagedBytes()
        {
            System.GC.Collect();
            System.GC.WaitForPendingFinalizers();
            System.GC.Collect();
            return System.GC.GetTotalMemory(true);
        }

        /// <summary>Unity's allocated memory, the half that a leaked GameObject shows up in.</summary>
        public static long AllocatedBytes() => UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
    }
}

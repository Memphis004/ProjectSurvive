using System.Collections;
using NUnit.Framework;
using ProjectSurvive.Core.Application.Flow;
using ProjectSurvive.Game.Presentation;
using ProjectSurvive.Game.Presentation.Encounter;
using ProjectSurvive.Game.Presentation.Flow;
using ProjectSurvive.Game.Presentation.Hub;
using ProjectSurvive.Game.ResultScene;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VContainer;

namespace ProjectSurvive.Game.Tests.PlayMode
{
    /// <summary>
    /// The template's loop, played through the real scenes: Boot → Title → Hub ⇄ Encounter → Result →
    /// Title.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the one check the EditMode suites cannot make: that the scenes exist in the build list,
    /// that each scene's scope builds, and that the Run scope created at the title survives every
    /// crossing and is disposed when the title comes back (ADR 0022). It drives the same controls a
    /// player presses, through the views' own public methods.
    /// </para>
    /// <para>
    /// Boot runs its smoke suite first, so the wait is on the Title scene being active rather than on a
    /// frame count. Every wait has a timeout: a scene that never loads fails the test instead of
    /// hanging the run.
    /// </para>
    /// </remarks>
    public sealed class LoopPlayModeTests
    {
        private const float TimeoutSeconds = 60f;

        /// <summary>
        /// Starts each test from an empty shell.
        /// </summary>
        /// <remarks>
        /// Boot moves the composition root into <c>DontDestroyOnLoad</c> and keeps a global parent
        /// override alive, so it outlives the scene load that ends the previous test — and with it the
        /// run that test ended. Without this, the second test would begin on a run that is already over
        /// and the Hub would (correctly) refuse to depart. Destroying the scope also destroys the run
        /// scope parented under it.
        /// </remarks>
        [SetUp]
        public void ResetShell()
        {
            RootLifetimeScope[] scopes = UnityEngine.Object.FindObjectsByType<RootLifetimeScope>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            for (int i = 0; i < scopes.Length; i++)
            {
                if (scopes[i] is null)
                {
                    continue;
                }

                scopes[i].StopPersisting();
                UnityEngine.Object.Destroy(scopes[i].gameObject);
            }
        }

        [UnityTest]
        public IEnumerator The_loop_reaches_the_summary_and_comes_back_to_the_title()
        {
            yield return LoadSceneAsync("Boot");
            yield return WaitForActiveScene("Title", TimeoutSeconds);

            var title = Object.FindFirstObjectByType<TitleSceneDirector>(FindObjectsInactive.Include);
            Assert.That(title, Is.Not.Null, "the Title scene has no TitleSceneDirector");
            Assert.That(title.TryStart(), Is.True);

            var view = Object.FindFirstObjectByType<TitleView>(FindObjectsInactive.Include);
            Assert.That(view, Is.Not.Null, "the Title scene has no TitleView");
            view.PressStart();

            yield return WaitForActiveScene("Hub", TimeoutSeconds);

            RunScopeOwner owner = Object.FindFirstObjectByType<RootLifetimeScope>(FindObjectsInactive.Include)
                .Container.Resolve<RunScopeOwner>();
            Assert.That(owner.HasRun, Is.True, "no run survived the move into the Hub");
            Assert.That(owner.Current.EncounterIndex, Is.EqualTo(0));

            HubView hub = Object.FindFirstObjectByType<HubView>(FindObjectsInactive.Include);
            Assert.That(hub, Is.Not.Null, "the Hub scene has no HubView");
            hub.PressDepart();

            yield return WaitForActiveScene("Encounter", TimeoutSeconds);

            Assert.That(owner.HasRun, Is.True, "the run did not survive the move into the Encounter");
            Assert.That(owner.Current.IsInEncounter, Is.True);

            EncounterView encounter = Object.FindFirstObjectByType<EncounterView>(FindObjectsInactive.Include);
            Assert.That(encounter, Is.Not.Null, "the Encounter scene has no EncounterView");
            encounter.PressWin();

            yield return WaitForActiveScene("Hub", TimeoutSeconds);
            Assert.That(owner.Current.EncounterIndex, Is.EqualTo(1), "a won encounter was not counted");

            hub = Object.FindFirstObjectByType<HubView>(FindObjectsInactive.Include);
            Assert.That(hub, Is.Not.Null);
            hub.PressFinish();

            yield return WaitForActiveScene("Result", TimeoutSeconds);

            var result = Object.FindFirstObjectByType<ResultSceneDirector>(FindObjectsInactive.Include);
            Assert.That(result, Is.Not.Null, "the Result scene has no ResultSceneDirector");
            Assert.That(result.Started, Is.True, "the summary did not start: " + result.StartError);

            var resultView = Object.FindFirstObjectByType<ResultView>(FindObjectsInactive.Include);
            Assert.That(resultView, Is.Not.Null);
            resultView.PressTitle();

            yield return WaitForActiveScene("Title", TimeoutSeconds);

            Assert.That(owner.HasRun, Is.False, "arriving at the title must dispose the run scope");
        }

        [UnityTest]
        public IEnumerator A_lost_encounter_ends_the_run_on_the_summary()
        {
            yield return LoadSceneAsync("Boot");
            yield return WaitForActiveScene("Title", TimeoutSeconds);

            Object.FindFirstObjectByType<TitleView>(FindObjectsInactive.Include).PressStart();
            yield return WaitForActiveScene("Hub", TimeoutSeconds);

            Object.FindFirstObjectByType<HubView>(FindObjectsInactive.Include).PressDepart();
            yield return WaitForActiveScene("Encounter", TimeoutSeconds);

            Object.FindFirstObjectByType<EncounterView>(FindObjectsInactive.Include).PressLose();
            yield return WaitForActiveScene("Result", TimeoutSeconds);

            var director = Object.FindFirstObjectByType<ResultSceneDirector>(FindObjectsInactive.Include);
            Assert.That(director, Is.Not.Null);
            Assert.That(director.Started, Is.True, director.StartError);
            Assert.That(director.Run.IsFinished, Is.True);
        }

        private static IEnumerator LoadSceneAsync(string sceneName)
        {
            AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            Assert.That(operation, Is.Not.Null, "the scene '" + sceneName + "' is not in the build settings");

            while (!operation.isDone)
            {
                yield return null;
            }

            // One frame for the new scene's Start to run before anyone looks for its views.
            yield return null;
        }

        private static IEnumerator WaitForActiveScene(string sceneName, float timeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (SceneManager.GetActiveScene().name != sceneName)
            {
                if (Time.realtimeSinceStartup > deadline)
                {
                    Assert.Fail(
                        "the '" + sceneName + "' scene never became active; on '" +
                        SceneManager.GetActiveScene().name + "'");
                }

                yield return null;
            }

            yield return null;
        }
    }
}

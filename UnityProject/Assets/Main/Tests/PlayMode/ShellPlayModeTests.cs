using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using ProjectSurvive.Core.Application.Text;
using ProjectSurvive.Game.Presentation;
using ProjectSurvive.Game.Presentation.Smoke;
using ProjectSurvive.Game.Presentation.Thai;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VContainer;

namespace ProjectSurvive.Game.Tests.PlayMode
{
    /// <summary>
    /// Stage 03 acceptance in PlayMode: the scenes load, the boot flow runs, no
    /// component reference is broken, and the Thai spike measures both stacks.
    /// </summary>
    /// <remarks>
    /// <para>
    /// PlayMode is the layer that actually proves the shell works: a scene can only
    /// be loaded, a lifetime scope only built, and a UI Toolkit panel only realised
    /// once the player loop is running. EditMode tests cover the same primitives
    /// without a loop, so a green pair is real evidence and a single green suite is
    /// not.
    /// </para>
    /// <para>
    /// Scene indices come from <see cref="SceneManager.sceneCountInBuildSettings"/>
    /// and <see cref="SceneUtility.GetScenePathByBuildIndex"/> rather than hardcoded
    /// names, so the tests follow whatever <c>EditorBuildSettings</c> was configured
    /// with (see <c>ProjectSetup.CreateScenes</c>).
    /// </para>
    /// </remarks>
    public sealed class ShellPlayModeTests
    {
        private const string TitleSceneName = "Title";

        [UnityTest]
        public IEnumerator Every_scene_in_build_settings_loads_without_missing_scripts()
        {
            int sceneCount = SceneManager.sceneCountInBuildSettings;
            Assert.That(sceneCount, Is.GreaterThanOrEqualTo(3), "expected Boot, Title and ThaiTextSpike in build settings");

            for (int index = 0; index < sceneCount; index++)
            {
                // Each scene is loaded on a shell this fixture owns. Since Stage 10a the App scope
                // persists across scene loads, so without the reset this loop would carry a run out
                // of the scene that made it — the Camp scene loaded after the Battle scene would
                // find a run in the Battle phase and (correctly) refuse to start, which is a
                // statement about the loop, not about the scene. The question here is whether each
                // scene loads on its own, so each scene gets its own shell.
                SceneFlowHarness.ResetShell();
                yield return null;

                string path = SceneUtility.GetScenePathByBuildIndex(index);
                yield return SceneManager.LoadSceneAsync(index, LoadSceneMode.Single);
                yield return null;

                var offenders = new List<string>();
                Scene scene = SceneManager.GetActiveScene();
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    Component[] components = root.GetComponentsInChildren<Component>(true);
                    for (int i = 0; i < components.Length; i++)
                    {
                        if (components[i] is null)
                        {
                            offenders.Add(root.name);
                            break;
                        }
                    }
                }

                Assert.That(offenders, Is.Empty, path + " has missing scripts on: " + string.Join(", ", offenders));
            }

            // Whatever the last scene left behind goes away with the fixture, not with the next one.
            SceneFlowHarness.ResetShell();
            yield return null;
        }

        [UnityTest]
        public IEnumerator Boot_runs_the_smoke_suite_and_hands_over_to_title()
        {
            int completedBefore = SceneBoot.CompletedBootCount;

            SceneFlowHarness.ResetShell();
            yield return null;

            yield return SceneManager.LoadSceneAsync(SceneIndex("Boot"), LoadSceneMode.Single);
            yield return null;

            float deadline = Time.realtimeSinceStartup + 60f;
            while (SceneManager.GetActiveScene().name != TitleSceneName && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            YieldBreakIfTimedOut(deadline, TitleSceneName);

            Assert.That(SceneBoot.CompletedBootCount, Is.GreaterThan(completedBefore), "the boot suite did not run");
            Assert.That(SceneBoot.LastReport, Is.Not.Null);

            TestContext.WriteLine(SceneBoot.LastReport.ToLog());
            Assert.That(SceneBoot.LastReport.AllPassed, Is.True, SceneBoot.LastReport.ToLog());
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(TitleSceneName));
        }

        [UnityTest]
        public IEnumerator Title_scene_shows_the_localized_title()
        {
            SceneFlowHarness.ResetShell();
            yield return null;

            yield return SceneManager.LoadSceneAsync(SceneIndex(TitleSceneName), LoadSceneMode.Single);
            yield return null;
            yield return null;

            RootLifetimeScope scope = UnityEngine.Object.FindFirstObjectByType<RootLifetimeScope>();
            Assert.That(scope, Is.Not.Null, "the Title scene has no RootLifetimeScope");
            Assert.That(scope.Container, Is.Not.Null, "the Title scene's scope was never built");

            string expected = scope.Container.Resolve<LocalizationService>().Get("ui.title");

            TitleView view = UnityEngine.Object.FindFirstObjectByType<TitleView>();
            Assert.That(view, Is.Not.Null, "the Title scene has no TitleView");

            TMP_Text titleLabel = FindLabel("TitleLabel");
            Assert.That(titleLabel, Is.Not.Null, "the Title scene has no TitleLabel");
            Assert.That(titleLabel.text, Is.EqualTo(expected));

            TMP_Text diagnosticsLabel = FindLabel("DiagnosticsLabel");
            Assert.That(diagnosticsLabel, Is.Not.Null);
            Assert.That(diagnosticsLabel.text, Is.Not.Empty, "the diagnostics line was never written");
        }

        [UnityTest]
        public IEnumerator Thai_spike_scene_measures_both_stacks_without_missing_glyphs()
        {
            SceneFlowHarness.ResetShell();
            yield return null;

            yield return SceneManager.LoadSceneAsync(SceneIndex("ThaiTextSpike"), LoadSceneMode.Single);
            yield return null;
            yield return null;

            ThaiSpikeReporter reporter = UnityEngine.Object.FindFirstObjectByType<ThaiSpikeReporter>();
            Assert.That(reporter, Is.Not.Null, "the ThaiTextSpike scene has no ThaiSpikeReporter");

            // The reporter runs from Start; if the panel never materialised it would
            // still produce a row, so the row count is what proves both stacks ran.
            Assert.That(reporter.Report, Is.Not.Null, "the Thai spike reporter did not run in Start");

            TestContext.WriteLine(reporter.Report.ToLog());

            Assert.That(
                reporter.Report.Rows.Count,
                Is.EqualTo(ThaiTextSpike.SpikeKeys.Length * 2),
                "expected one row per stack per spike string: " + reporter.Report.ToLog());
            Assert.That(
                reporter.Report.TotalMissingGlyphs,
                Is.EqualTo(0),
                "the Thai font does not cover every code point: " + reporter.Report.ToLog());
        }

        [UnityTest]
        public IEnumerator Unirask_delay_is_cancelled_by_the_owner_token()
        {
            var cts = new CancellationTokenSource();
            UniTask delay = UniTask.Delay(TimeSpan.FromSeconds(30), cancellationToken: cts.Token);
            cts.Cancel();

            Exception observed = null;
            yield return delay.ToCoroutine(exception => observed = exception);

            cts.Dispose();

            Assert.That(
                observed,
                Is.InstanceOf<OperationCanceledException>(),
                "a cancelled UniTask.Delay must surface OperationCanceledException, not complete");
        }

        private static void YieldBreakIfTimedOut(float deadline, string expectedScene)
        {
            if (Time.realtimeSinceStartup >= deadline)
            {
                Assert.Fail(
                    "Boot never reached scene '" + expectedScene + "'; active scene is '" +
                    SceneManager.GetActiveScene().name + "'");
            }
        }

        private static int SceneIndex(string sceneName)
        {
            int sceneCount = SceneManager.sceneCountInBuildSettings;
            for (int index = 0; index < sceneCount; index++)
            {
                string path = SceneUtility.GetScenePathByBuildIndex(index);
                if (System.IO.Path.GetFileNameWithoutExtension(path) == sceneName)
                {
                    return index;
                }
            }

            Assert.Fail("scene '" + sceneName + "' is not in the build settings");
            return -1;
        }

        private static TMP_Text FindLabel(string objectName)
        {
            GameObject found = GameObject.Find(objectName);
            return found is null ? null : found.GetComponent<TMP_Text>();
        }
    }
}

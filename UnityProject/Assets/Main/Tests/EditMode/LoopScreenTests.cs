using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ProjectSurvive.Core.Application.Flow;
using ProjectSurvive.Core.Application.Run;
using ProjectSurvive.Core.Application.Text;
using ProjectSurvive.Core.Domain.Config;
using ProjectSurvive.Core.Infrastructure.Config;
using ProjectSurvive.Game.Presentation;
using ProjectSurvive.Game.Presentation.Encounter;
using ProjectSurvive.Game.Presentation.Hub;
using UnityEngine;

namespace ProjectSurvive.Game.Tests.EditMode
{
    /// <summary>
    /// The loop's screen logic, driven through the real localization table the build ships.
    /// </summary>
    /// <remarks>
    /// <para>
    /// These are the Unity-side halves of the loop: the presenters that turn an Application answer
    /// into a screen's strings and a recorded intent. The rules themselves are tested headlessly in
    /// <c>RunSkeletonTests</c>; what is added here is the part only Unity can answer — that the screens'
    /// keys exist in both languages in the table the player build actually loads, and that a press
    /// leaves both the run and the request in the state the director expects.
    /// </para>
    /// <para>
    /// A scene scope is not built: the presenters take the run, the flow and the text they need, which
    /// is what makes them testable without a container, a scene or play mode.
    /// </para>
    /// </remarks>
    public sealed class LoopScreenTests
    {
        private static LocalizationService Text()
        {
            return new LocalizationService(Config());
        }

        private static IGameConfig Config()
        {
            var tables = new Dictionary<string, byte[]>();
            foreach (string name in LubanGameConfig.TableNames)
            {
                var asset = Resources.Load<TextAsset>("Config/" + name);
                Assert.That(asset, Is.Not.Null, "Resources/Config/" + name + ".bytes is missing");
                tables[name] = asset.bytes;
            }

            return LubanGameConfig.Load(name => tables[name], ProjectSurvive.Config.ConfigBuildMetadata.ConfigHash);
        }

        [Test]
        public void Every_key_the_loop_screens_read_exists_in_both_languages()
        {
            IGameConfig config = Config();
            string[] keys = TitleKeys.All
                .Concat(HubKeys.All)
                .Concat(EncounterKeys.All)
                .Concat(ResultKeys.All)
                .ToArray();

            var problems = new List<string>();
            foreach (string key in keys)
            {
                if (!config.TryGetLocalization(key, out LocalizationEntry entry))
                {
                    problems.Add("no row for " + key);
                    continue;
                }

                if (string.IsNullOrEmpty(entry.Th) || string.IsNullOrEmpty(entry.En))
                {
                    problems.Add(key + " is not translated in both languages");
                }
            }

            Assert.That(problems, Is.Empty, string.Join("; ", problems));
        }

        [Test]
        public void Departing_the_hub_begins_an_encounter_and_asks_for_that_scene()
        {
            var run = new RunSession(7u);
            Assert.That(run.Start().IsSuccess, Is.True);
            var flow = new SceneFlow(run);
            flow.SyncTo(SceneId.Hub);

            var presenter = new HubPresenter(run, flow, Text(), new HubUiState());

            Assert.That(presenter.RequestDepart(), Is.True);
            Assert.That(run.IsInEncounter, Is.True);
            Assert.That(flow.CurrentScene, Is.EqualTo(SceneId.Encounter));
            Assert.That(presenter.RequestedScene, Is.EqualTo(SceneId.Encounter));
        }

        [Test]
        public void Finishing_the_run_from_the_hub_asks_for_the_summary()
        {
            var run = new RunSession(7u);
            Assert.That(run.Start().IsSuccess, Is.True);
            var flow = new SceneFlow(run);
            flow.SyncTo(SceneId.Hub);

            var presenter = new HubPresenter(run, flow, Text(), new HubUiState());

            Assert.That(presenter.RequestFinish(), Is.True);
            Assert.That(run.Outcome, Is.EqualTo(RunOutcome.Completed));
            Assert.That(presenter.RequestedScene, Is.EqualTo(SceneId.Result));
        }

        [Test]
        public void A_won_encounter_counts_once_and_returns_to_the_hub()
        {
            var run = new RunSession(7u);
            Assert.That(run.Start().IsSuccess, Is.True);
            var flow = new SceneFlow(run);
            flow.SyncTo(SceneId.Encounter);
            Assert.That(run.BeginEncounter().IsSuccess, Is.True);

            var state = new EncounterUiState();
            var presenter = new EncounterPresenter(run, flow, Text(), state);
            presenter.Refresh();
            Assert.That(state.ProgressLine.Value, Does.Contain("1"), "the progress line names the round");

            Assert.That(presenter.RequestWin(), Is.True);
            Assert.That(run.EncounterIndex, Is.EqualTo(1));
            Assert.That(presenter.RequestedScene, Is.EqualTo(SceneId.Hub));

            // One request at a time: a second press changes nothing.
            Assert.That(presenter.RequestWin(), Is.False);
            Assert.That(run.EncounterIndex, Is.EqualTo(1));
        }

        [Test]
        public void A_lost_encounter_ends_the_run_and_asks_for_the_summary()
        {
            var run = new RunSession(7u);
            Assert.That(run.Start().IsSuccess, Is.True);
            var flow = new SceneFlow(run);
            flow.SyncTo(SceneId.Encounter);
            Assert.That(run.BeginEncounter().IsSuccess, Is.True);

            var presenter = new EncounterPresenter(run, flow, Text(), new EncounterUiState());

            Assert.That(presenter.RequestLose(), Is.True);
            Assert.That(run.Outcome, Is.EqualTo(RunOutcome.Failed));
            Assert.That(presenter.RequestedScene, Is.EqualTo(SceneId.Result));
        }

        [Test]
        public void The_summary_reads_the_run_and_records_one_way_out_at_a_time()
        {
            var run = new RunSession(7u);
            Assert.That(run.Start().IsSuccess, Is.True);
            Assert.That(run.BeginEncounter().IsSuccess, Is.True);
            Assert.That(run.EndEncounter(EncounterOutcome.Won).IsSuccess, Is.True);
            Assert.That(run.EndRun().IsSuccess, Is.True);

            var state = new ResultUiState();
            var presenter = new ResultPresenter(run, Text(), state);

            presenter.Begin();
            Assert.That(state.Headline.Value, Is.Not.Empty);
            Assert.That(state.SummaryLine.Value, Does.Contain("1"));
            Assert.That(state.SeedLine.Value, Does.Contain("7"));
            Assert.That(state.RestartLabel.Value, Is.Not.Empty);
            Assert.That(state.TitleLabel.Value, Is.Not.Empty);

            Assert.That(presenter.RequestRestart(), Is.True);
            Assert.That(presenter.RequestedScene, Is.EqualTo(SceneId.Hub));
            Assert.That(presenter.RequestTitle(), Is.False);
        }

        [Test]
        public void A_live_run_has_nothing_for_the_summary_to_show()
        {
            var run = new RunSession(7u);
            Assert.That(run.Start().IsSuccess, Is.True);

            var presenter = new ResultPresenter(run, Text(), new ResultUiState());

            Assert.That(presenter.IsFinished, Is.False);
        }
    }
}

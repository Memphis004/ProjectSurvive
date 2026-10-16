using System.Collections;
using NUnit.Framework;
using ProjectSurvive.Core.Application.Run;
using ProjectSurvive.Core.Application.Text;
using ProjectSurvive.Core.Domain.Combat.Battle;
using ProjectSurvive.Core.Domain.Config;
using ProjectSurvive.Core.Domain.Run;
using ProjectSurvive.Game.Battle;
using ProjectSurvive.Game.Camp;
using ProjectSurvive.Game.Presentation;
using ProjectSurvive.Game.Presentation.Flow;
using ProjectSurvive.Game.ResultScene;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;

namespace ProjectSurvive.Game.Tests.PlayMode
{
    /// <summary>
    /// Stage 10b in PlayMode: the real loop from ศาลาพัก to the summary screen and back, and the two
    /// things the screen can do there.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The EditMode fixture proves what the screen <i>says</i>; this one proves that a real scene
    /// change gets there with the run that was played, that <b>Restart</b> is a new run and not the
    /// old one resumed ("Restart สร้าง Run ใหม่ ไม่คง state จากรอบก่อน"), and that going back to the
    /// title still leaves no run behind. Everything goes through the scenes: the camp's own presenter
    /// performs the departure, the real <c>SceneLoader</c> performs each handover, and the floor is
    /// ended with commands of the battle contract rather than by reaching into the simulation.
    /// </para>
    /// <para>
    /// The floor is ended two ways on purpose. A <b>surrender</b> is a loss the game itself can
    /// produce on any build; the dev menu's <b>skip-floor</b> control is what makes "play it until
    /// you win" a test that does not need twenty minutes of real combat, and it only works because
    /// the Editor is a development environment (the shell decides that at startup).
    /// </para>
    /// </remarks>
    public sealed class ResultScenePlayModeTests
    {
        /// <summary>How long a floor may take to start before the test calls it stuck.</summary>
        private const float FloorStartBudgetSeconds = 30f;

        /// <summary>
        /// A lost floor lands on the summary screen, which reports it — and the restart control
        /// starts a brand-new run: floor 1, the starting purse, nothing carried over.
        /// </summary>
        [UnityTest]
        public IEnumerator A_lost_floor_lands_on_the_summary_screen_and_restart_starts_a_new_run()
        {
            yield return SceneFlowHarness.EnterCamp();

            var camp = SceneFlowHarness.Find<CampSceneDirector>();
            RunSession defeated = camp.Run;
            Assert.That(defeated, Is.Not.Null, "the camp has no run");

            // The App scope owns the config, so it survives the scene changes below and is what the
            // new run's purse is measured against.
            IGameConfig config = SceneFlowHarness.Find<CampSceneScope>().Container.Resolve<IGameConfig>();

            // Spend something, so "nothing was carried over" is a statement about a purse that moved.
            camp.Presenter.Rent(0);
            Assert.That(
                defeated.Wallet.Bia, Is.LessThan(config.Economy.StartBia),
                "the rent took no bia, so the restart below would prove nothing about the purse");

            camp.Presenter.Depart();
            Assert.That(camp.Presenter.Departed, Is.True, "the camp refused to depart: " + camp.Presenter.StatusCode);

            yield return SceneFlowHarness.WaitForSceneAndIdle(SceneFlowHarness.BattleSceneName);

            var battle = SceneFlowHarness.Find<BattleSceneDirector>();
            yield return WaitForFloorToStart(battle);

            Assert.That(
                battle.Runner.Submit(BattleCommand.Surrender()), Is.EqualTo(CommandResult.Ok),
                "the floor refused a surrender");

            yield return SceneFlowHarness.WaitUntil(() => battle.Settled, "the floor ended but was never settled");

            // The arena leaves for the summary screen by itself; the test rides that transition.
            yield return SceneFlowHarness.WaitForSceneAndIdle(SceneFlowHarness.ResultSceneName);

            var result = SceneFlowHarness.Find<ResultSceneDirector>();
            Assert.That(result, Is.Not.Null, "the Result scene has no ResultSceneDirector");
            Assert.That(result.Started, Is.True, "the summary screen did not start: " + result.StartError);
            Assert.That(result.Departed, Is.False, "the summary screen left before the player asked");

            // The run survives into the summary: it is the one that was played, not a new one built
            // to be summarised.
            Assert.That(result.Run, Is.SameAs(defeated), "the summary screen summarised a different run");
            Assert.That(result.Summary.IsFinished, Is.True);
            Assert.That(result.Summary.IsVictory, Is.False, "a surrender must not read as a victory");
            Assert.That(result.Summary.FloorReached, Is.EqualTo(1), "the run died on floor 1");
            Assert.That(defeated.FlowState, Is.EqualTo(RunFlowState.Result));
            Assert.That(
                SceneFlowHarness.Count<RunLifetimeScope>(), Is.EqualTo(1),
                "the summary screen has " + SceneFlowHarness.Count<RunLifetimeScope>() + " run scopes");

            // What the screen actually says, from the scope that shows it.
            var scope = SceneFlowHarness.Find<ResultSceneScope>();
            Assert.That(scope, Is.Not.Null, "the Result scene has no ResultSceneScope");
            Assert.That(scope.Container, Is.Not.Null, "the Result scene's scope was never built");

            var state = scope.Container.Resolve<ResultUiState>();
            var text = scope.Container.Resolve<LocalizationService>();
            Assert.That(state.Headline.Value, Is.EqualTo(text.Get(ResultKeys.Defeat)), "the headline is not the table's");
            Assert.That(state.FloorLine.Value, Is.Not.Empty, "the summary shows no floor line");
            Assert.That(state.FloorLine.Value, Does.Contain("1"), "the floor line does not name the floor reached");
            Assert.That(state.RestartLabel.Value, Is.EqualTo(text.Get(ResultKeys.Restart)));
            Assert.That(state.TitleLabel.Value, Is.EqualTo(text.Get(ResultKeys.BackToTitle)));

            // Restart: the run scope is replaced, and the camp starts a new run the way it starts the
            // first one.
            RunScopeOwner owner = SceneFlowHarness.Owner();
            Assert.That(result.Presenter.RequestRestart(), Is.True);

            yield return SceneFlowHarness.WaitForSceneAndIdle(SceneFlowHarness.CampSceneName);

            Assert.That(owner.HasRun, Is.True, "the restart left no run for the camp to start");
            RunSession fresh = owner.Current;
            Assert.That(fresh, Is.Not.SameAs(defeated), "restart resumed the finished run instead of starting a new one");
            Assert.That(
                SceneFlowHarness.Count<RunLifetimeScope>(), Is.EqualTo(1),
                "the finished run's scope outlived the restart");

            Assert.That(fresh.FlowState, Is.EqualTo(RunFlowState.Camp), "the new run is not at the camp");
            Assert.That(fresh.FloorIndex, Is.EqualTo(1), "a new run starts on floor 1");
            Assert.That(fresh.FloorsCleared, Is.EqualTo(0), "a new run has cleared nothing");
            Assert.That(fresh.GhostsDefeated, Is.EqualTo(0), "a new run has killed nothing");
            Assert.That(fresh.Hp, Is.EqualTo(fresh.MaxHp), "a new run starts at full HP");

            Assert.That(
                fresh.Wallet.Bia, Is.EqualTo(config.Economy.StartBia),
                "the new run kept the finished run's purse");

            var restarted = SceneFlowHarness.Find<CampSceneDirector>();
            Assert.That(restarted.Run, Is.SameAs(fresh), "the camp is showing a different run than the new one");
        }

        /// <summary>
        /// The other way out of the summary screen: the title, where the run ends for good. This is
        /// the acceptance line "Title ไม่มี Run scope เก่าค้าง".
        /// </summary>
        [UnityTest]
        public IEnumerator Leaving_the_summary_for_the_title_leaves_no_run_behind()
        {
            yield return SceneFlowHarness.EnterCamp();

            var camp = SceneFlowHarness.Find<CampSceneDirector>();
            camp.Presenter.Depart();
            yield return SceneFlowHarness.WaitForSceneAndIdle(SceneFlowHarness.BattleSceneName);

            var battle = SceneFlowHarness.Find<BattleSceneDirector>();
            yield return WaitForFloorToStart(battle);

            Assert.That(battle.Runner.Submit(BattleCommand.Surrender()), Is.EqualTo(CommandResult.Ok));
            yield return SceneFlowHarness.WaitUntil(() => battle.Settled, "the floor was never settled");
            yield return SceneFlowHarness.WaitForSceneAndIdle(SceneFlowHarness.ResultSceneName);

            var result = SceneFlowHarness.Find<ResultSceneDirector>();
            Assert.That(result.Started, Is.True, result.StartError);
            Assert.That(result.Presenter.RequestTitle(), Is.True);

            yield return SceneFlowHarness.WaitForSceneAndIdle(SceneFlowHarness.TitleSceneName);

            Assert.That(SceneFlowHarness.Count<RunLifetimeScope>(), Is.EqualTo(0), "the run scope outlived the title");
            Assert.That(SceneFlowHarness.Owner().HasRun, Is.False, "the App scope still holds the finished run");
            Assert.That(SceneFlowHarness.Count<ResultSceneScope>(), Is.EqualTo(0), "the summary scope outlived its scene");
            Assert.That(result.Run.FlowState, Is.EqualTo(RunFlowState.Title), "the run did not return to its title phase");
        }

        /// <summary>
        /// "เล่นตั้งแต่เริ่มจนชนะ": every floor cleared, one after another, ending in a victory summary.
        /// </summary>
        /// <remarks>
        /// The floors are skipped with the dev menu's own <c>DevWinNow</c> command, which the floor
        /// accepts because the Editor is a development environment — the same gate the dev menu
        /// shows itself behind. Nothing here is a special test path: it is the command the skip-floor
        /// button sends.
        /// </remarks>
        [UnityTest]
        public IEnumerator A_run_can_be_won_floor_by_floor_and_ends_on_a_victory_summary()
        {
            Assert.That(
                DevCheats.Allowed, Is.True,
                "the Editor must allow the dev cheats for a test that skips floors");

            yield return SceneFlowHarness.EnterCamp();

            var camp = SceneFlowHarness.Find<CampSceneDirector>();
            RunSession run = camp.Run;
            int totalFloors = run.TotalFloors;
            Assert.That(totalFloors, Is.GreaterThan(1), "the shipped floor table has one row: nothing to loop");

            for (int floor = 1; floor <= totalFloors; floor++)
            {
                var departing = SceneFlowHarness.Find<CampSceneDirector>();
                departing.Presenter.Depart();
                Assert.That(
                    departing.Presenter.Departed, Is.True,
                    "floor " + floor + ": the camp refused to depart: " + departing.Presenter.StatusCode);

                yield return SceneFlowHarness.WaitForSceneAndIdle(SceneFlowHarness.BattleSceneName);

                var battle = SceneFlowHarness.Find<BattleSceneDirector>();
                yield return WaitForFloorToStart(battle);

                Assert.That(
                    battle.Runner.Submit(BattleCommand.DevWinNow()), Is.EqualTo(CommandResult.Ok),
                    "floor " + floor + ": the shipped skip-floor control was refused");
                yield return SceneFlowHarness.WaitUntil(
                    () => battle.Settled, "floor " + floor + ": the floor was never settled");

                Assert.That(run.FloorsCleared, Is.EqualTo(floor), "floor " + floor + ": the clear was not counted");

                if (floor < totalFloors)
                {
                    // A cleared floor goes back to the camp on its own, with the run it left with.
                    yield return SceneFlowHarness.WaitForSceneAndIdle(SceneFlowHarness.CampSceneName);
                    Assert.That(run.FlowState, Is.EqualTo(RunFlowState.Camp));
                    Assert.That(run.FloorIndex, Is.EqualTo(floor + 1), "the run did not advance a floor");
                }
            }

            yield return SceneFlowHarness.WaitForSceneAndIdle(SceneFlowHarness.ResultSceneName);

            var result = SceneFlowHarness.Find<ResultSceneDirector>();
            Assert.That(result.Started, Is.True, result.StartError);
            Assert.That(result.Summary.IsVictory, Is.True, "clearing every floor is a victory");
            Assert.That(result.Summary.FloorsCleared, Is.EqualTo(totalFloors));
            Assert.That(result.Summary.FloorReached, Is.EqualTo(totalFloors));

            var scope = SceneFlowHarness.Find<ResultSceneScope>();
            var state = scope.Container.Resolve<ResultUiState>();
            var text = scope.Container.Resolve<LocalizationService>();
            Assert.That(state.Headline.Value, Is.EqualTo(text.Get(ResultKeys.Victory)), "the headline is not the table's");
        }

        private static IEnumerator WaitForFloorToStart(BattleSceneDirector battle)
        {
            float deadline = Time.realtimeSinceStartup + FloorStartBudgetSeconds;
            while ((battle.Runner is null || !battle.Runner.IsRunning) && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(battle.Runner, Is.Not.Null, "the battle scene has no runner");
            Assert.That(battle.Runner.IsRunning, Is.True, "the floor never started: " + battle.StartError);
        }
    }
}

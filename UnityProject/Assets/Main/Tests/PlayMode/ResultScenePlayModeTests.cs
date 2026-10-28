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
using ProjectSurvive.Game.Presentation.Battle;
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
            // The page reports are process-wide mirrors, so a stale document from an earlier fixture
            // must not be able to satisfy the assertions below.
            DevMenuReport.Reset();
            ResultReport.Reset();

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

            // What the browser check presses to reach this screen in reasonable time. The Editor is a
            // development environment exactly as the Development WebGL build the browser suite runs is,
            // so the panel exists and every control's position is measurable; a release build publishes
            // nothing here, which is the state `DevMenuReportTests` asserts on the authored scene.
            //
            // Since Stage 08g-2 the menu starts shut behind F1, so nothing is published until it opens:
            // a control on a hidden panel has no position a click could land on. The test opens it the
            // way the key does (`TogglePanel`), then reads what a browser would.
            var devMenu = SceneFlowHarness.Find<DevMenuView>();
            Assert.That(devMenu, Is.Not.Null, "the Battle scene has no dev menu");
            Assert.That(
                devMenu.PanelVisible, Is.False,
                "the dev panel is on screen before F1, so the item that hides it behind the key is not in effect");
            Assert.That(
                DevMenuReport.PublishCount, Is.Zero,
                "a shut dev panel published a report of controls nobody can click");

            devMenu.TogglePanel();
            Assert.That(devMenu.PanelVisible, Is.True, "the dev panel refused to open");

            yield return SceneFlowHarness.WaitUntil(
                () => DevMenuReport.PublishCount > 0, "the dev menu published no controls");

            var menu = JsonUtility.FromJson<MenuReportDto>(DevMenuReport.LastJson);
            Assert.That(menu, Is.Not.Null, "the dev menu report is not valid JSON: " + DevMenuReport.LastJson);
            Assert.That(menu.visible, Is.True, "the panel is on screen, so the report says so");
            Assert.That(menu.buttons.Length, Is.EqualTo(6), "the report does not name every control");

            var menuNames = new System.Collections.Generic.List<string>();
            for (int i = 0; i < menu.buttons.Length; i++)
            {
                menuNames.Add(menu.buttons[i].name);
                Assert.That(menu.buttons[i].y, Is.GreaterThan(0f), menu.buttons[i].name + " has no y");
            }

            Assert.That(
                menuNames, Does.Contain(DevMenuReport.Control.LoseNow),
                "the control a browser test presses to end a floor is not in the report");

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

            // The same state, as the page sees it: this is the document the browser check reads, so it
            // is asserted at the one place the real scene was really reached.
            Assert.That(result.Summary.IsVictory, Is.False);

            var published = JsonUtility.FromJson<SummaryReportDto>(ResultReport.LastJson);
            Assert.That(
                published, Is.Not.Null,
                "the summary screen published nothing for the page: " + ResultReport.LastJson);
            Assert.That(published.scene, Is.EqualTo(SceneFlowHarness.ResultSceneName));
            Assert.That(published.startError, Is.Empty);
            Assert.That(published.outcome, Is.EqualTo(nameof(RunOutcome.Defeat)));
            Assert.That(published.headline, Is.EqualTo(state.Headline.Value), "the page reads a different headline");
            Assert.That(published.headline, Is.EqualTo(text.Get(ResultKeys.Defeat)));
            Assert.That(published.floor, Is.EqualTo(1));
            Assert.That(published.kills, Is.EqualTo(result.Summary.GhostsDefeated));
            Assert.That(published.hp, Is.EqualTo(result.Summary.Hp));
            Assert.That(published.maxHp, Is.EqualTo(result.Summary.MaxHp));
            Assert.That(published.restartLabel, Is.EqualTo(text.Get(ResultKeys.Restart)));
            Assert.That(published.titleLabel, Is.EqualTo(text.Get(ResultKeys.BackToTitle)));
            Assert.That(published.seed, Is.EqualTo(result.Run.Seed.ToString()));
            Assert.That(
                (int)published.canvasWidth, Is.GreaterThan(0),
                "the report does not name the canvas its control points belong to");

            var controlNames = new System.Collections.Generic.List<string>();
            for (int i = 0; i < published.buttons.Length; i++)
            {
                controlNames.Add(published.buttons[i].name);
                Assert.That(published.buttons[i].x, Is.GreaterThan(0f), published.buttons[i].name + " has no x");
                Assert.That(published.buttons[i].y, Is.GreaterThan(0f), published.buttons[i].name + " has no y");
            }

            Assert.That(
                controlNames,
                Is.EquivalentTo(new[] { ResultReport.Control.Restart, ResultReport.Control.BackToTitle }),
                "the screen's two ways out are not both in the report");

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

            ResultReport.Reset();

            yield return SceneFlowHarness.EnterCamp();

            var camp = SceneFlowHarness.Find<CampSceneDirector>();
            RunSession run = camp.Run;
            int totalFloors = run.TotalFloors;
            Assert.That(totalFloors, Is.GreaterThan(1), "the shipped floor table has one row: nothing to loop");

            IGameConfig config = SceneFlowHarness.Find<CampSceneScope>().Container.Resolve<IGameConfig>();

            for (int floor = 1; floor <= totalFloors; floor++)
            {
                var departing = SceneFlowHarness.Find<CampSceneDirector>();

                if (floor == 1)
                {
                    // Stage 06b, in the scene rather than in the rule engine: rent a shelf and pin
                    // another before leaving, so the next sala has state that *could* be carried
                    // over. The player's report was one shelf that stayed empty for the rest of the
                    // run, which is what the panel check below would catch.
                    Assert.That(
                        run.Wallet.Bia, Is.GreaterThanOrEqualTo(run.Stock[0].Price),
                        "the starting purse cannot rent the first shelf: the fixture proves nothing");

                    departing.Presenter.Rent(0);
                    departing.Presenter.ToggleSlotLock(1);

                    Assert.That(run.Stock[0].IsPurchased, Is.True, "the first shelf was not rented");
                    Assert.That(run.Stock[1].IsLocked, Is.True, "the second shelf was not pinned");
                }

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

                    if (floor == 1)
                    {
                        AssertFreshCampPanel(run, config, "sala 2");
                    }
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

            // And the page's copy of it: the same document, for the other ending.
            var published = JsonUtility.FromJson<SummaryReportDto>(ResultReport.LastJson);
            Assert.That(
                published, Is.Not.Null,
                "the summary screen published nothing for the page: " + ResultReport.LastJson);
            Assert.That(published.outcome, Is.EqualTo(nameof(RunOutcome.Victory)));
            Assert.That(published.headline, Is.EqualTo(text.Get(ResultKeys.Victory)));
            Assert.That(published.floorsCleared, Is.EqualTo(totalFloors));
            Assert.That(published.buttonCount(), Is.EqualTo(2), "the screen's two ways out must both be named");
        }

        // DTOs for JsonUtility: the Unity-side stand-in for the page's JSON.parse.
        [System.Serializable]
        private sealed class MenuReportDto
        {
            public string scene;
            public bool visible;
            public int canvasWidth;
            public int canvasHeight;
            public MenuControlDto[] buttons;
        }

        [System.Serializable]
        private sealed class MenuControlDto
        {
            public string name;
            public float x;
            public float y;
        }

        [System.Serializable]
        private sealed class SummaryReportDto
        {
            public string scene;
            public string startError;
            public string outcome;
            public bool finished;
            public bool victory;
            public int floor;
            public int totalFloors;
            public int floorsCleared;
            public int kills;
            public int bia;
            public int hp;
            public int maxHp;
            public string seed;
            public string headline;
            public string floorLine;
            public string killsLine;
            public string biaLine;
            public string hpLine;
            public string restartLabel;
            public string titleLabel;
            public int canvasWidth;
            public int canvasHeight;
            public MenuControlDto[] buttons;

            /// <summary>How many controls the document names, or 0 when it names none.</summary>
            public int buttonCount() => buttons is null ? 0 : buttons.Length;
        }

        /// <summary>
        /// Stage 06b: every sala the run walks into shows a full panel again, and nothing the
        /// previous one carried — a rent, a pin — follows it in ("ทุกการเข้าศาลาสร้าง stock ใหม่ 4
        /// ช่อง; ช่องที่เช่าแล้วไม่เติมฟรีเฉพาะภายในศาลาเดียวกัน").
        /// </summary>
        private static void AssertFreshCampPanel(RunSession run, IGameConfig config, string context)
        {
            Assert.That(
                run.Stock.SlotCount, Is.EqualTo(config.Economy.ShopSlotCount),
                context + ": the panel is not the data's slot count");
            Assert.That(run.Stock.RefreshCount, Is.EqualTo(0), context + ": the re-roll counter was kept");
            Assert.That(
                run.Stock.AvailableCount, Is.EqualTo(run.Stock.SlotCount),
                context + ": only " + run.Stock.AvailableCount + " of " + run.Stock.SlotCount + " shelves are rentable");

            var seen = new System.Collections.Generic.HashSet<string>();
            for (int i = 0; i < run.Stock.SlotCount; i++)
            {
                Assert.That(run.Stock[i].IsEmpty, Is.False, context + ": shelf " + i + " is empty");
                Assert.That(run.Stock[i].IsPurchased, Is.False, context + ": shelf " + i + " is still rented");
                Assert.That(run.Stock[i].IsLocked, Is.False, context + ": shelf " + i + " is still pinned");
                Assert.That(
                    seen.Add(run.Stock[i].OfferedId), Is.True,
                    context + ": the same piece is offered twice: " + run.Stock[i].OfferedId);
            }
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

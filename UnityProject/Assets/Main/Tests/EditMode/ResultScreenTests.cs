using NUnit.Framework;
using ProjectSurvive.Core.Application.Battle.Fake;
using ProjectSurvive.Core.Application.Run;
using ProjectSurvive.Core.Application.Text;
using ProjectSurvive.Core.Domain.Combat.Battle;
using ProjectSurvive.Core.Domain.Config;
using ProjectSurvive.Core.Domain.Inventory;
using ProjectSurvive.Core.Domain.Run;
using ProjectSurvive.Core.Infrastructure.Save;
using ProjectSurvive.Core.Shared;
using ProjectSurvive.Game.Infrastructure;
using ProjectSurvive.Game.Presentation;

namespace ProjectSurvive.Game.Tests.EditMode
{
    /// <summary>
    /// Stage 10b's summary screen, without a scene: what the run reports when it ends, and what the
    /// screen makes of it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The screen computes nothing (AGENTS rule 10), so what there is to prove here is small and
    /// exact: <see cref="RunSummary"/> carries the right numbers for both endings,
    /// <see cref="ResultPresenter"/> turns them into the table's own strings, and every key the
    /// screen reads exists in <b>both</b> languages. The PlayMode fixture proves the rest — that a
    /// real scene change gets there.
    /// </para>
    /// <para>
    /// Balance numbers are read from the shipped tables rather than hard-coded (the same rule
    /// <c>RunFlowEditModeTests</c> follows), so retuning <c>data/</c> cannot make a correct rule
    /// look broken.
    /// </para>
    /// </remarks>
    public sealed class ResultScreenTests
    {
        /// <summary>A seed whose floor-1 run reaches the fake's win override cleanly.</summary>
        private const uint Seed = 20261014u;

        private IGameConfig _config;
        private IBackpackSnapshotCodec _codec;

        [SetUp]
        public void SetUp()
        {
            _config = ResourcesGameConfigLoader.Load();
            _codec = new MessagePackBackpackSnapshotCodec();
        }

        [Test]
        public void A_cleared_run_reports_a_victory_on_its_last_floor()
        {
            RunSession run = StartedRun();
            var flow = new SceneFlow(run, new ScriptedFactory(_config, lose: false));

            int kills = DriveEveryFloor(flow, run);

            RunSummary summary = run.Summary();

            Assert.That(summary.IsFinished, Is.True, "the run is over, so it has a summary");
            Assert.That(summary.IsVictory, Is.True, "every floor was cleared");
            Assert.That(summary.Outcome, Is.EqualTo(RunOutcome.Victory));
            Assert.That(summary.FloorsCleared, Is.EqualTo(run.TotalFloors));
            Assert.That(summary.FloorReached, Is.EqualTo(run.TotalFloors),
                "a cleared run reports the last floor it fought, not one past it");
            Assert.That(summary.TotalFloors, Is.EqualTo(run.TotalFloors));
            Assert.That(kills, Is.GreaterThan(0), "the scripted floors must really kill something, or the sum below is empty");
            Assert.That(summary.GhostsDefeated, Is.EqualTo(kills), "the run's total is the floors' own sum");
            Assert.That(summary.Bia, Is.EqualTo(run.Wallet.Bia));
            Assert.That(summary.Hp, Is.EqualTo(run.Hp));
            Assert.That(summary.MaxHp, Is.EqualTo(run.MaxHp));
            Assert.That(summary.Seed, Is.EqualTo(run.Seed), "the summary carries the seed a bug report needs");
        }

        [Test]
        public void A_lost_floor_reports_a_defeat_on_the_floor_it_was_fought_on()
        {
            RunSession run = StartedRun();
            var flow = new SceneFlow(run, new ScriptedFactory(_config, lose: true));

            // Floor 1 only: the loss is what ends the run, so nothing else is fought.
            Assert.That(flow.EnterBattle().IsSuccess, Is.True);
            Result<FloorSettlement> settled = SettleFirstFloor(flow);

            Assert.That(settled.IsSuccess, Is.True, settled.Error.ToString());
            Assert.That(settled.Value.RunCompleted, Is.True, "a lost floor ends the run");
            Assert.That(settled.Value.IsWin, Is.False);

            RunSummary summary = run.Summary();

            Assert.That(summary.IsVictory, Is.False, "a run that lost floor 1 did not win");
            Assert.That(summary.Outcome, Is.EqualTo(RunOutcome.Defeat));
            Assert.That(summary.FloorReached, Is.EqualTo(1), "the summary names the floor it died on");
            Assert.That(summary.FloorsCleared, Is.EqualTo(0));
            Assert.That(run.FlowState, Is.EqualTo(RunFlowState.Result), "the run ends at the summary phase");
            Assert.That(SceneFlowMap.SceneOf(run.FlowState), Is.EqualTo(SceneId.Result));
        }

        [Test]
        public void A_run_that_has_not_ended_has_a_summary_that_says_so()
        {
            RunSession run = StartedRun();

            RunSummary summary = run.Summary();

            Assert.That(summary.IsFinished, Is.False, "a live run must not be shown as a result");
            Assert.That(summary.Outcome, Is.EqualTo(RunOutcome.InProgress));
            Assert.That(summary.FloorsCleared, Is.EqualTo(0));
            Assert.Throws<System.InvalidOperationException>(
                () => summary.RequireFinished(),
                "asking a live run's summary to be a result is a bug, not a display state");
        }

        [Test]
        public void The_screen_renders_the_runs_numbers_through_the_localization_table()
        {
            RunSession run = StartedRun();
            var flow = new SceneFlow(run, new ScriptedFactory(_config, lose: true));
            Assert.That(flow.EnterBattle().IsSuccess, Is.True);
            Assert.That(SettleFirstFloor(flow).IsSuccess, Is.True);

            var state = new ResultUiState();
            var text = new LocalizationService(_config);
            var presenter = new ResultPresenter(run, text, state);

            presenter.Begin();

            RunSummary summary = run.Summary();

            Assert.That(presenter.IsFinished, Is.True);
            Assert.That(state.Headline.Value, Is.EqualTo(text.Get(ResultKeys.Defeat)));
            Assert.That(state.FloorLine.Value,
                Does.Contain(summary.FloorReached.ToString()),
                "the floor line carries the floor the run reached");
            Assert.That(state.FloorLine.Value, Does.Contain(summary.TotalFloors.ToString()));
            Assert.That(state.KillsLine.Value, Does.Contain(summary.GhostsDefeated.ToString()));
            Assert.That(state.HpLine.Value, Does.Contain(summary.MaxHp.ToString()));
            Assert.That(state.RestartLabel.Value, Is.EqualTo(text.Get(ResultKeys.Restart)));
            Assert.That(state.TitleLabel.Value, Is.EqualTo(text.Get(ResultKeys.BackToTitle)));

            // No raw format placeholder survives: a line that still holds "{0}" is a missing format
            // row, not a rendering choice.
            Assert.That(state.FloorLine.Value, Does.Not.Contain("{0}"));

            state.Dispose();
        }

        [Test]
        public void The_screen_offers_one_way_out_at_a_time()
        {
            RunSession run = StartedRun();
            var state = new ResultUiState();
            var presenter = new ResultPresenter(run, new LocalizationService(_config), state);

            Assert.That(presenter.RequestRestart(), Is.True, "the first press is recorded");
            Assert.That(presenter.RequestedScene, Is.EqualTo(SceneId.Camp), "restart goes through the camp");

            Assert.That(presenter.RequestRestart(), Is.False, "a second press while one is outstanding is refused");
            Assert.That(presenter.RequestTitle(), Is.False);
            Assert.That(presenter.RequestedScene, Is.EqualTo(SceneId.Camp), "a refusal changes nothing");

            state.Dispose();
        }

        [Test]
        public void Leaving_for_the_title_is_the_runs_own_transition()
        {
            RunSession run = StartedRun();
            var flow = new SceneFlow(run, new ScriptedFactory(_config, lose: true));
            Assert.That(flow.EnterBattle().IsSuccess, Is.True);
            Assert.That(SettleFirstFloor(flow).IsSuccess, Is.True);

            var state = new ResultUiState();
            var presenter = new ResultPresenter(run, new LocalizationService(_config), state);

            // The presenter records the intent; the scene performs it once the loader accepted the
            // move, so a refused load cannot leave a run whose screen is still up already finished.
            Assert.That(presenter.RequestTitle(), Is.True);
            Assert.That(run.FlowState, Is.EqualTo(RunFlowState.Result), "asking is not doing");

            Assert.That(presenter.CompleteTitleTransition().IsSuccess, Is.True);
            Assert.That(run.FlowState, Is.EqualTo(RunFlowState.Title), "the title has no run phase");

            state.Dispose();
        }

        [Test]
        public void Every_summary_string_exists_in_both_languages()
        {
            var text = new LocalizationService(_config);

            foreach (string key in ResultKeys.All)
            {
                Language[] languages = { Language.Th, Language.En };
                foreach (Language language in languages)
                {
                    text.Language = language;
                    Assert.That(text.TryGet(key, out string value), Is.True,
                        "the Result screen reads '" + key + "', which is not in the table");
                    Assert.That(string.IsNullOrWhiteSpace(value), Is.False,
                        "'" + key + "' has no " + language + " text");
                }
            }
        }

        /// <summary>A run that has started and is sitting in the first Camp.</summary>
        private RunSession StartedRun()
        {
            var run = new RunSession(_config, _codec, Seed);
            Result started = run.StartRun();
            Assert.That(started.IsSuccess, Is.True, "the run must start on the shipped tables: " + started.Error);
            return run;
        }

        /// <summary>
        /// Fights every floor to its end, reads each floor's own result before it is settled, and
        /// returns the sum of those kills.
        /// </summary>
        /// <remarks>
        /// Stepping by hand rather than calling <c>RunFloorToEnd</c> is the point: it lets the test
        /// see the <see cref="BattleResult"/> the run never stores, which is what makes "the run's
        /// total is the sum of the floors' own results" a real assertion instead of a repetition.
        /// </remarks>
        private static int DriveEveryFloor(SceneFlow flow, RunSession run)
        {
            int kills = 0;

            for (int floor = 1; floor <= run.TotalFloors; floor++)
            {
                Assert.That(flow.EnterBattle().IsSuccess, Is.True, "floor " + floor + " must be enterable");

                IBattleSession session = flow.Battle;
                int steps = 0;
                while (session.Phase == BattlePhase.Running && steps++ < 100_000)
                {
                    Assert.That(flow.StepBattle(PlayerInputFrame.Idle).IsSuccess, Is.True);
                }

                Assert.That(session.TryGetResult(out BattleResult result), Is.True, "floor " + floor + " ended");
                kills += result.Kills;

                Result<FloorSettlement> settled = flow.SettleBattle();
                Assert.That(settled.IsSuccess, Is.True, "floor " + floor + ": " + settled.Error);
                Assert.That(run.GhostsDefeated, Is.EqualTo(kills),
                    "floor " + floor + ": the run's total must be the floors' own sum");
            }

            return kills;
        }

        /// <summary>Walks floor 1 to its end and settles it, whatever that end is.</summary>
        private static Result<FloorSettlement> SettleFirstFloor(SceneFlow flow)
        {
            IBattleSession session = flow.Battle;
            int steps = 0;
            while (session.Phase == BattlePhase.Running && steps++ < 100_000)
            {
                Assert.That(flow.StepBattle(PlayerInputFrame.Idle).IsSuccess, Is.True);
            }

            return flow.SettleBattle();
        }

        /// <summary>
        /// Wins (or loses) every floor on its first tick through the port the run is allowed to see,
        /// so the test never names an implementation of the battle.
        /// </summary>
        private sealed class ScriptedFactory : IBattleSessionFactory
        {
            private readonly FakeBattleSessionFactory _factory;

            internal ScriptedFactory(IGameConfig config, bool lose) =>
                // A win at tick 1 would kill nothing, so the win path takes long enough for the
                // dummies to recycle: "the run's total is the sum of the floors" is only worth
                // asserting when the floors killed something.
                _factory = new FakeBattleSessionFactory(config, new FakeBattleOptions
                {
                    WinAtTick = lose ? 0 : 40,
                    LoseAtTick = lose ? 1 : 0,
                    DummyEntityCount = 4,
                    EntityLifetimeTicks = 8,
                });

            public IBattleSession Create(BattleSetup setup) => _factory.Create(setup);
        }
    }
}

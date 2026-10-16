using System;
using ProjectSurvive.Core.Domain.Combat.Battle;
using Xunit;

namespace ProjectSurvive.Core.Tests
{
    /// <summary>
    /// The battle contract, as tests any <see cref="IBattleSessionFactory"/> must
    /// pass. Derived (non-abstract) classes bind it to a concrete factory — today
    /// the fake, from Stage 07 the EcsLite simulation — so the real simulation is
    /// held to the rules the fake was proven against.
    /// </summary>
    /// <typeparam name="TFactory">The factory under test.</typeparam>
    public abstract class BattleSessionContractTests<TFactory>
        where TFactory : IBattleSessionFactory
    {
        /// <summary>The standard factory: a boss floor can still be survived to its timer.</summary>
        protected abstract TFactory CreateFactory();

        /// <summary>A factory that damages the player hard enough to die. Override when a factory has such a knob.</summary>
        protected virtual TFactory CreateFactoryWithPressure() => CreateFactory();

        /// <summary>A fresh setup. Override to change HP / floors / cheats.</summary>
        protected virtual BattleSetup CreateSetup(
            bool bossFloor,
            int currentHp = BattleTestFixtures.DefaultMaxHp,
            bool devCheatsAllowed = true) =>
            BattleTestFixtures.Setup(bossFloor, currentHp, devCheatsAllowed: devCheatsAllowed);

        /// <summary>Steps until the session ends (or the guard trips), returning how many steps were run.</summary>
        private static int RunToEnd(IBattleSession session, int maxSteps = 6000)
        {
            int steps = 0;
            while (session.Phase == BattlePhase.Running && steps < maxSteps)
            {
                session.Step(PlayerInputFrame.Idle);
                steps++;
            }

            Assert.True(session.Phase != BattlePhase.Running, "the session did not end within the guard of " + maxSteps + " steps");
            return steps;
        }

        [Fact]
        public void Step_while_not_running_does_not_advance_the_tick()
        {
            IBattleSession session = CreateFactory().Create(CreateSetup(false));

            session.Step(PlayerInputFrame.Idle);
            session.Step(PlayerInputFrame.Idle);
            long tickBefore = session.Tick;

            Assert.Equal(CommandResult.Ok, session.Submit(BattleCommand.Surrender()));
            Assert.Equal(BattlePhase.Aborted, session.Phase);

            for (int i = 0; i < 10; i++)
            {
                session.Step(PlayerInputFrame.Idle);
            }

            Assert.Equal(tickBefore, session.Tick);
        }

        [Fact]
        public void Surrender_aborts_with_the_surrendered_reason_and_locks_the_session()
        {
            IBattleSession session = CreateFactory().Create(CreateSetup(false));

            Assert.Equal(CommandResult.Ok, session.Submit(BattleCommand.Surrender()));
            Assert.Equal(BattlePhase.Aborted, session.Phase);

            Assert.True(session.TryGetResult(out BattleResult result));
            Assert.Equal(BattlePhase.Aborted, result.Outcome);
            Assert.Equal(BattleEndReason.Surrendered, result.EndReason);

            Assert.Equal(CommandResult.RejectedWrongPhase, session.Submit(BattleCommand.Surrender()));
            Assert.Equal(CommandResult.RejectedWrongPhase, session.Submit(BattleCommand.DevWinNow()));
        }

        [Fact]
        public void Dev_commands_are_rejected_when_cheats_are_disabled()
        {
            IBattleSession session = CreateFactory().Create(CreateSetup(false, devCheatsAllowed: false));

            Assert.Equal(CommandResult.RejectedNotAllowed, session.Submit(BattleCommand.DevWinNow()));
            Assert.Equal(CommandResult.RejectedNotAllowed, session.Submit(BattleCommand.DevLoseNow()));
            Assert.Equal(CommandResult.RejectedNotAllowed, session.Submit(BattleCommand.DevKillAll()));
            Assert.Equal(CommandResult.RejectedNotAllowed, session.Submit(BattleCommand.DevGodMode(true)));

            // Still running: a rejected command changes nothing.
            Assert.Equal(BattlePhase.Running, session.Phase);
        }

        [Fact]
        public void Dev_god_mode_rejects_an_out_of_range_argument()
        {
            IBattleSession session = CreateFactory().Create(CreateSetup(false));

            Assert.Equal(CommandResult.RejectedInvalidArg, session.Submit(new BattleCommand(BattleCommandKind.DevGodMode, 5)));
            Assert.Equal(CommandResult.Ok, session.Submit(BattleCommand.DevGodMode(true)));
            Assert.Equal(CommandResult.Ok, session.Submit(BattleCommand.DevGodMode(false)));
        }

        [Fact]
        public void A_normal_floor_survived_to_the_timer_is_won()
        {
            IBattleSession session = CreateFactory().Create(CreateSetup(false));

            RunToEnd(session);

            Assert.True(session.TryGetResult(out BattleResult result));
            Assert.Equal(BattlePhase.Won, result.Outcome);
            Assert.Equal(BattleEndReason.SurvivedAllWaves, result.EndReason);
        }

        [Fact]
        public void A_boss_floor_that_runs_out_of_time_is_lost_with_timeout()
        {
            IBattleSession session = CreateFactory().Create(CreateSetup(true));

            RunToEnd(session);

            Assert.True(session.TryGetResult(out BattleResult result));
            Assert.Equal(BattlePhase.Lost, result.Outcome);
            Assert.Equal(BattleEndReason.Timeout, result.EndReason);
        }

        [Fact]
        public void Dev_win_now_wins_a_boss_floor_as_a_boss_kill()
        {
            IBattleSession session = CreateFactory().Create(CreateSetup(true));

            session.Step(PlayerInputFrame.Idle);
            Assert.Equal(CommandResult.Ok, session.Submit(BattleCommand.DevWinNow()));

            Assert.True(session.TryGetResult(out BattleResult result));
            Assert.Equal(BattlePhase.Won, result.Outcome);
            Assert.Equal(BattleEndReason.BossDefeated, result.EndReason);
        }

        /// <summary>
        /// The lose-now cheat stands for the player dying, so the HP is spent before the floor is
        /// finished. Everything downstream reads <c>FinalHp</c> — the run settles it, the summary
        /// screen prints it — and a defeat reported at full health is a summary that lies about
        /// the fight that just ended.
        /// </summary>
        [Fact]
        public void Dev_lose_now_reports_the_death_it_stands_for()
        {
            IBattleSession session = CreateFactory().Create(CreateSetup(false));

            session.Step(PlayerInputFrame.Idle);
            Assert.Equal(CommandResult.Ok, session.Submit(BattleCommand.DevLoseNow()));

            Assert.True(session.TryGetResult(out BattleResult result));
            Assert.Equal(BattlePhase.Lost, result.Outcome);
            Assert.Equal(BattleEndReason.PlayerDied, result.EndReason);
            Assert.Equal(0, result.FinalHp);
        }

        [Fact]
        public void TryGetResult_is_false_while_running_and_true_once_ended()
        {
            IBattleSession session = CreateFactory().Create(CreateSetup(false));

            Assert.False(session.TryGetResult(out _));
            session.Step(PlayerInputFrame.Idle);
            Assert.False(session.TryGetResult(out _));

            session.Submit(BattleCommand.Surrender());

            Assert.True(session.TryGetResult(out BattleResult result));
            Assert.Equal(BattlePhase.Aborted, result.Outcome);
        }

        [Fact]
        public void FinalHp_is_within_bounds_on_both_a_win_and_a_loss()
        {
            BattleSetup winSetup = CreateSetup(false);
            IBattleSession winner = CreateFactory().Create(winSetup);
            RunToEnd(winner);
            Assert.True(winner.TryGetResult(out BattleResult winResult));
            Assert.InRange(winResult.FinalHp, 0, winSetup.Loadout.Stats.MaxHp);

            BattleSetup lossSetup = CreateSetup(false, currentHp: 100);
            IBattleSession loser = CreateFactoryWithPressure().Create(lossSetup);
            RunToEnd(loser);
            Assert.True(loser.TryGetResult(out BattleResult lossResult));
            Assert.InRange(lossResult.FinalHp, 0, lossSetup.Loadout.Stats.MaxHp);
        }

        [Fact]
        public void Replay_round_trip_reproduces_the_state_hash()
        {
            BattleSetup setup = CreateSetup(false);
            var replay = new BattleReplay(setup);
            IBattleSession original = CreateFactory().Create(setup);

            for (int i = 0; i < 300; i++)
            {
                PlayerInputFrame input = PlayerInputFrame.FromAxis(i % 2 == 0 ? 1f : -1f, 0.5f);
                replay.RecordStep(input);
                original.Step(input);
            }

            uint expected = original.ComputeStateHash();

            // A different factory instance, same setup: the replay must land on the
            // same state, which is what makes a desync detectable.
            uint actual = replay.ReplayHash(CreateFactory());

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Event_sequence_numbers_are_continuous()
        {
            IBattleSession session = CreateFactory().Create(CreateSetup(false));
            for (int i = 0; i < 50; i++)
            {
                session.Step(PlayerInputFrame.Idle);
            }

            var buffer = new BattleEvent[256];
            long cursor = 0;
            int read = session.Events.Read(ref cursor, buffer, out _);

            Assert.True(read > 0, "expected some events after 50 ticks");
            for (int i = 0; i < read; i++)
            {
                Assert.Equal(i, buffer[i].Seq);
            }
        }

        [Fact]
        public void A_reader_that_falls_behind_is_told_how_much_it_dropped()
        {
            // Never read during the battle, so the ring overflows well before the
            // floor ends. State must still be correct after the loss.
            IBattleSession session = CreateFactory().Create(CreateSetup(true));
            RunToEnd(session);

            var buffer = new BattleEvent[BattleConstants.EventRingCapacity];
            long cursor = 0;
            int read = session.Events.Read(ref cursor, buffer, out int dropped);

            Assert.True(dropped > 0, "a reader that never read should be told it dropped events");
            Assert.Equal(buffer.Length, read);

            Assert.True(session.TryGetResult(out BattleResult result));
            Assert.Equal(BattleEndReason.Timeout, result.EndReason);
            Assert.Equal(BattlePhase.Lost, result.Outcome);
        }

        [Fact]
        public void Entities_never_exceed_the_render_cap()
        {
            IBattleSession session = CreateFactory().Create(CreateSetup(false));
            session.Step(PlayerInputFrame.Idle);

            Assert.InRange(session.Frame.Entities.Length, 0, BattleConstants.MaxRenderEntities);
            Assert.InRange(session.Frame.PreviousEntities.Length, 0, BattleConstants.MaxRenderEntities);
        }

        /// <summary>
        /// The frame is a Prev/Cur pair so Presentation can interpolate between two
        /// ticks (Stage 08). The concrete obligation: after step N, the previous
        /// frame is the frame that step N-1 published.
        /// </summary>
        [Fact]
        public void The_previous_frame_is_the_one_the_tick_before_published()
        {
            IBattleSession session = CreateFactory().Create(CreateSetup(false));

            session.Step(PlayerInputFrame.Idle);

            // A reader must copy a span before the next Step - the contract says so,
            // and there is no other way to hold on to a frame's values.
            RenderEntity[] before = session.Frame.Entities.ToArray();
            long tickBefore = session.Frame.Tick;

            session.Step(PlayerInputFrame.Idle);
            IBattleFrame frame = session.Frame;

            Assert.Equal(tickBefore + 1, frame.Tick);
            Assert.Equal(before.Length, frame.PreviousEntities.Length);
            for (int i = 0; i < before.Length; i++)
            {
                Assert.Equal(before[i], frame.PreviousEntities[i]);
            }
        }

        [Fact]
        public void Step_does_not_allocate_after_warm_up()
        {
            IBattleSession session = CreateFactory().Create(CreateSetup(false));

            for (int i = 0; i < 200; i++)
            {
                session.Step(PlayerInputFrame.Idle);
            }

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 200; i++)
            {
                session.Step(PlayerInputFrame.Idle);
            }

            long after = GC.GetAllocatedBytesForCurrentThread();

            Assert.Equal(0L, after - before);
        }
    }
}
